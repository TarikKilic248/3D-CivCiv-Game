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
_REFRESH_HM_RE = re.compile(
    r"(?:refreshes?|resets?|try\s+again|retry)\s+(?:in\s+)?(?:(\d+)\s*d(?:ays?)?\s*)?"
    r"(?:(\d+)\s*h(?:ours?|r)?\s*)?(?:(\d+)\s*m(?:in(?:utes?)?)?\s*)?(?:(\d+)\s*s(?:ec(?:onds?)?)?)?",
    re.IGNORECASE,
)


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

    def resolve_executable(self) -> str:
        found = shutil.which(self.cli_cfg.executable)
        if found:
            return found
        return self.cli_cfg.executable

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

        effort_suffixes = ("-low", "-medium", "-high", "-xhigh", "-max")
        if profile.effort and not profile.model.lower().endswith(effort_suffixes):
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
        )
