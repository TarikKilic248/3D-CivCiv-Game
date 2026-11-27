from __future__ import annotations

import json
import re
import shutil
import subprocess
import time
from pathlib import Path
from typing import Any

from .config import AgentCliConfig, ModelProfile
from .models import AgentResult, TokenUsage

_JSON_BLOCK_RE = re.compile(r"```(?:json)?\s*(\{.*?\}|\[.*?\])\s*```", re.DOTALL)
_QUOTA_ERROR_RE = re.compile(
    r"(resource_exhausted|quota\s*(exceeded|exhausted|limit|reached)|rate[\s_-]*limit|"
    r"too\s+many\s+requests|\b429\b|five[\s_-]*hour\s+limit|weekly\s+limit|"
    r"insufficient\s+quota|model\s+capacity|overloaded|usage\s+limit\s+reached|"
    r"exhausted\s+your\s+capacity|no\s+quota)",
    re.IGNORECASE,
)
_MODEL_UNAVAILABLE_RE = re.compile(
    r"(invalid\s+model\s+selection|not\s+recognized\s+as\s+a\s+known\s+model|"
    r"unknown\s+model|unsupported\s+model|"
    r"model\b[^\n]{0,80}\b(?:not\s+(?:found|available|recognized|supported)|unavailable|does\s+not\s+exist))",
    re.IGNORECASE,
)
_MODEL_ID_TOKEN_RE = re.compile(r"^[a-z0-9][a-z0-9._-]{2,}$", re.IGNORECASE)
_REFRESH_HM_RE = re.compile(
    r"(?:refreshes?|resets?|try\s+again|retry)\s+(?:in\s+)?(?:(\d+)\s*d(?:ays?)?\s*)?"
    r"(?:(\d+)\s*h(?:ours?|r)?\s*)?(?:(\d+)\s*m(?:in(?:utes?)?)?\s*)?(?:(\d+)\s*s(?:ec(?:onds?)?)?)?",
    re.IGNORECASE,
)


def detect_model_unavailable_error(text: str) -> bool:
    if not text:
        return False
    return bool(_MODEL_UNAVAILABLE_RE.search(text))


def parse_agy_models_output(raw_text: str) -> list[str]:
    """Parse `agy models` stdout into ordered list of model IDs (0 tokens)."""
    models: list[str] = []
    seen: set[str] = set()
    for line in raw_text.splitlines():
        stripped = line.strip()
        if not stripped:
            continue
        low = stripped.lower()
        if low.startswith(("fetching ", "available ", "error", "warning", "note:")):
            continue
        token = stripped.split("\t", 1)[0].split()[0].strip()
        if ("-" in token or "." in token) and _MODEL_ID_TOKEN_RE.match(token) and token not in seen:
            seen.add(token)
            models.append(token)
    return models


def fetch_available_models(executable: str = "agy", timeout_sec: int = 15) -> list[str]:
    """Query `<executable> models` once; returns [] cleanly on timeout/missing binary."""
    exe = shutil.which(executable) or executable
    try:
        proc = subprocess.run(
            [exe, "models"],
            capture_output=True,
            text=True,
            encoding="utf-8",
            errors="replace",
            timeout=timeout_sec,
        )
    except (OSError, subprocess.TimeoutExpired):
        return []
    if proc.returncode != 0:
        return []
    return parse_agy_models_output(proc.stdout or "")


def detect_quota_error(text: str) -> tuple[bool, int | None]:
    if not text or not _QUOTA_ERROR_RE.search(text):
        return False, None
    hint_sec: int | None = None
    m = _REFRESH_HM_RE.search(text)
    if m and any(m.groups()):
        days = int(m.group(1) or 0)
        hours = int(m.group(2) or 0)
        mins = int(m.group(3) or 0)
        secs = int(m.group(4) or 0)
        total = days * 86400 + hours * 3600 + mins * 60 + secs
        if total > 0:
            hint_sec = total
    return True, hint_sec


def _parse_outer_envelope(raw_text: str) -> dict[str, Any]:
    text = raw_text.strip()
    if not text:
        return {}
    try:
        parsed = json.loads(text)
        if isinstance(parsed, dict):
            return parsed
    except json.JSONDecodeError:
        pass
    for line in reversed(text.splitlines()):
        line = line.strip()
        if line.startswith("{"):
            try:
                parsed = json.loads(line)
                if isinstance(parsed, dict):
                    return parsed
            except json.JSONDecodeError:
                continue
    return {}


def _extract_json_payload(raw_text: str) -> dict[str, Any]:
    text = raw_text.strip()
    if not text:
        return {}
    # 1. Try direct JSON decode
    try:
        parsed = json.loads(text)
        if isinstance(parsed, dict):
            for key in ("structured_output", "result", "response", "output", "text", "content"):
                val = parsed.get(key)
                if isinstance(val, dict):
                    return val
                if isinstance(val, str) and val.strip():
                    nested = _extract_json_payload(val)
                    if nested:
                        return nested
            return parsed
    except json.JSONDecodeError:
        pass

    # 2. Try fenced JSON code blocks
    matches = _JSON_BLOCK_RE.findall(text)
    for block in reversed(matches):
        try:
            parsed = json.loads(block)
            if isinstance(parsed, dict):
                return parsed
        except json.JSONDecodeError:
            continue

    # 3. Try scanning lines from end (NDJSON / log preamble)
    for line in reversed(text.splitlines()):
        line = line.strip()
        if not line.startswith("{"):
            continue
        try:
            parsed = json.loads(line)
            if isinstance(parsed, dict):
                return parsed
        except json.JSONDecodeError:
            continue

    # 4. Last-resort brace slice
    start = text.find("{")
    end = text.rfind("}")
    if start != -1 and end > start:
        try:
            parsed = json.loads(text[start : end + 1])
            if isinstance(parsed, dict):
                return parsed
        except json.JSONDecodeError:
            pass
    return {}


class AgentRunner:
    """Spawns a fresh headless `agy -p` session per task/turn with usage & quota telemetry."""

    def __init__(self, repo_root: Path, cli_cfg: AgentCliConfig, runtime_dir: Path) -> None:
        self.repo_root = repo_root
        self.cli_cfg = cli_cfg
        self.runtime_dir = runtime_dir
        self._cached_models: list[str] | None = None

    def resolve_executable(self) -> str:
        found = shutil.which(self.cli_cfg.executable)
        if found:
            return found
        return self.cli_cfg.executable

    def list_available_models(self, force_refresh: bool = False) -> list[str]:
        if self._cached_models is None or force_refresh:
            self._cached_models = fetch_available_models(self.cli_cfg.executable)
        return list(self._cached_models)

    def _build_cmd(
        self,
        exe: str,
        cli_prompt: str,
        profile: ModelProfile,
        schema: dict[str, Any] | None,
        tag: str,
        read_only: bool,
        include_effort: bool = True,
    ) -> list[str]:
        cmd = [
            exe,
            "-p",
            cli_prompt,
            "--model",
            profile.model,
            "--output-format",
            "json",
            "--print-timeout",
            f"{profile.timeout_sec}s",
        ]

        effort_suffixes = ("-low", "-medium", "-high", "-xhigh", "-max", "-thinking")
        if include_effort and profile.effort and not profile.model.lower().endswith(effort_suffixes):
            cmd.extend(["--effort", profile.effort])

        if read_only:
            cmd.extend(["--mode", "plan"])
        elif self.cli_cfg.skip_permissions:
            cmd.append("--dangerously-skip-permissions")
        else:
            cmd.extend(["--mode", self.cli_cfg.mode])

        if schema is not None:
            schema_dir = self.runtime_dir / "schemas"
            schema_dir.mkdir(parents=True, exist_ok=True)
            schema_file = schema_dir / f"{tag}_schema.json"
            schema_file.write_text(json.dumps(schema, indent=2), encoding="utf-8")
            cmd.extend(["--json-schema", str(schema_file)])

        if self.cli_cfg.extra_args:
            cmd.extend(self.cli_cfg.extra_args)
        return cmd

    def run_prompt(
        self,
        prompt: str,
        profile: ModelProfile,
        schema: dict[str, Any] | None = None,
        tag: str = "task",
        run_dir: Path | None = None,
        read_only: bool = False,
    ) -> AgentResult:
        exe = self.resolve_executable()
        self.runtime_dir.mkdir(parents=True, exist_ok=True)
        audit_dir = run_dir or (self.runtime_dir / "prompts")
        audit_dir.mkdir(parents=True, exist_ok=True)

        prompt_file = audit_dir / f"{tag}_prompt.md"
        prompt_file.write_text(prompt, encoding="utf-8")

        cli_prompt = prompt
        if len(prompt) > 10000:
            rel_prompt = prompt_file.resolve().relative_to(self.repo_root.resolve()).as_posix()
            cli_prompt = (
                f"Read `{rel_prompt}` completely and execute the exact instructions inside. "
                "Respond strictly in the requested JSON schema."
            )

        effort_suffixes = ("-low", "-medium", "-high", "-xhigh", "-max", "-thinking")
        used_effort = bool(profile.effort and not profile.model.lower().endswith(effort_suffixes))
        cmd = self._build_cmd(exe, cli_prompt, profile, schema, tag, read_only, include_effort=used_effort)

        started = time.monotonic()
        try:
            proc = subprocess.run(
                cmd,
                cwd=str(self.repo_root),
                capture_output=True,
                text=True,
                encoding="utf-8",
                errors="replace",
                timeout=profile.timeout_sec + 30,
            )
            # If the CLI rejected `--effort` at 0 tokens (not quota/missing-model), retry once without `--effort`.
            if proc.returncode != 0 and used_effort:
                pre_err = f"{proc.stdout or ''}\n{proc.stderr or ''}"
                if (
                    "effort" in pre_err.lower()
                    and not detect_model_unavailable_error(pre_err)
                    and not detect_quota_error(pre_err)[0]
                ):
                    cmd_no_eff = self._build_cmd(
                        exe, cli_prompt, profile, schema, tag, read_only, include_effort=False
                    )
                    proc = subprocess.run(
                        cmd_no_eff,
                        cwd=str(self.repo_root),
                        capture_output=True,
                        text=True,
                        encoding="utf-8",
                        errors="replace",
                        timeout=profile.timeout_sec + 30,
                    )
        except FileNotFoundError:
            return AgentResult(
                ok=False,
                model=profile.model,
                effort=profile.effort,
                summary="",
                duration_sec=time.monotonic() - started,
                exit_code=-1,
                error=f"Agent CLI bulunamadi: '{self.cli_cfg.executable}'",
            )
        except subprocess.TimeoutExpired:
            return AgentResult(
                ok=False,
                model=profile.model,
                effort=profile.effort,
                summary="",
                duration_sec=time.monotonic() - started,
                exit_code=-2,
                error=f"Agent zaman asimina ugradi ({profile.timeout_sec}s).",
            )

        elapsed = time.monotonic() - started
        stdout = proc.stdout or ""
        stderr = proc.stderr or ""

        (audit_dir / f"{tag}_output.txt").write_text(
            f"EXIT: {proc.returncode}\n--- STDOUT ---\n{stdout}\n--- STDERR ---\n{stderr}\n",
            encoding="utf-8",
        )

        envelope = _parse_outer_envelope(stdout)
        usage = TokenUsage.from_dict(envelope.get("usage") if isinstance(envelope, dict) else None)
        envelope_error = str(envelope.get("error", "") or "") if isinstance(envelope, dict) else ""
        envelope_status = str(envelope.get("status", "") or "").upper() if isinstance(envelope, dict) else ""

        structured = _extract_json_payload(stdout)
        summary = str(structured.get("summary", "")).strip()
        if not summary and stdout.strip():
            summary = stdout.strip().splitlines()[-1][:240]

        changed_files = structured.get("changed_files", [])
        if not isinstance(changed_files, list):
            changed_files = []

        ok = proc.returncode == 0 and envelope_status != "ERROR"
        error_msg = ""
        if not ok:
            error_msg = (
                envelope_error
                or stderr.strip()
                or stdout.strip()
                or f"Exit code {proc.returncode}"
            )[-600:]
        elif schema is not None and not structured:
            error_msg = "JSON cikti ayristirilamadi."

        combined_err = "\n".join(x for x in (envelope_error, stderr, error_msg) if x)
        is_quota, quota_hint = (False, None) if ok else detect_quota_error(combined_err)
        is_model_unavail = False if (ok or is_quota) else detect_model_unavailable_error(combined_err)

        return AgentResult(
            ok=ok,
            model=profile.model,
            effort=profile.effort,
            summary=summary,
            changed_files=[str(x) for x in changed_files],
            structured=structured,
            raw_output=stdout[-4000:],
            duration_sec=elapsed,
            exit_code=proc.returncode,
            error=error_msg,
            usage=usage,
            is_quota_error=is_quota,
            quota_reset_hint_sec=quota_hint,
            is_model_unavailable=is_model_unavail,
        )
