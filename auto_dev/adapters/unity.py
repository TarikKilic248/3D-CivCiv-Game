from __future__ import annotations

import json
import os
import re
import subprocess
import sys
import time
import uuid
from pathlib import Path

from auto_dev.core.config import AutoDevConfig
from auto_dev.core.models import GateResult, Task

from .base import ProjectAdapter

_UNITY_ERROR_RE = re.compile(
    r"(error\s+CS\d+:|CompilerError|Script compilation failed|Exception:|FAIL\b|Assertion failed)",
    re.IGNORECASE,
)


class UnityAdapter(ProjectAdapter):
    """Unity 6 Hybrid Bridge adapter: live Editor bridge when open, -batchmode fallback when closed."""

    def __init__(self, cfg: AutoDevConfig) -> None:
        super().__init__(cfg)
        ad = cfg.adapter
        self.unity_exe: str = str(ad.get("unity_exe", "")).strip()
        self.scene_path: str = str(ad.get("scene_path", "")).strip()
        self.bridge_timeout_sec: int = int(ad.get("bridge_timeout_sec", 150))
        self.batch_timeout_sec: int = int(ad.get("batch_timeout_sec", 240))
        self.cmd_file: Path = cfg.runtime_dir / "unity_cmd.json"
        self.result_file: Path = cfg.runtime_dir / "unity_result.json"
        self.snapshot_file: Path = cfg.runtime_dir / "scene_hierarchy.txt"
        self.batch_log_file: Path = cfg.runtime_dir / "unity_batch.log"

    def is_unity_editor_open(self) -> bool:
        lockfile = self.cfg.repo_root / "Temp" / "UnityLockfile"
        if not lockfile.exists():
            return False
        if sys.platform == "win32":
            # Unity holds an exclusive OS lock on Temp/UnityLockfile for this exact project.
            # This avoids WMI/CIM dependencies (tasklist) and ignores stale lockfiles or other Unity projects.
            try:
                fd = os.open(str(lockfile), os.O_RDWR)
                os.close(fd)
                return False
            except PermissionError:
                return True
            except OSError:
                return True
        return True

    def _extract_batch_errors(self, max_lines: int = 25) -> str:
        if not self.batch_log_file.exists():
            return "Unity batch log dosyasi olusturulamadi."
        text = self.batch_log_file.read_text(encoding="utf-8", errors="ignore")
        matched: list[str] = []
        seen: set[str] = set()
        for line in text.splitlines():
            s = line.strip()
            if not s or not _UNITY_ERROR_RE.search(s):
                continue
            if s not in seen:
                seen.add(s)
                matched.append(s)
            if len(matched) >= max_lines:
                break
        if matched:
            return "\n".join(matched)
        tail = [l.strip() for l in text.splitlines() if l.strip()][-15:]
        return "\n".join(tail) or "Unity batchmode bilinmeyen hata ile sonlandi."

    def _dispatch_command(
        self,
        action: str,
        arg: str = "",
        needs_graphics: bool = False,
    ) -> tuple[bool, dict]:
        self.cfg.runtime_dir.mkdir(parents=True, exist_ok=True)
        cmd_id = uuid.uuid4().hex[:12]
        payload = {
            "id": cmd_id,
            "action": action,
            "arg": arg,
            "scenePath": self.scene_path,
            "phase": "NEW",
        }

        if self.result_file.exists():
            try:
                self.result_file.unlink()
            except OSError:
                pass

        self.cmd_file.write_text(json.dumps(payload, indent=2), encoding="utf-8")

        if self.is_unity_editor_open():
            print(f"[AutoDev/Unity] Acik Editor koprusune gonderiliyor: {action} {arg}".strip(), flush=True)
            return self._wait_live_editor(cmd_id)
        print(f"[AutoDev/Unity] Unity kapali — batchmode baslatiliyor: {action} {arg}".strip(), flush=True)
        return self._run_batchmode(cmd_id, needs_graphics=needs_graphics)

    def _wait_live_editor(self, cmd_id: str) -> tuple[bool, dict]:
        deadline = time.monotonic() + self.bridge_timeout_sec
        last_parse_err = ""
        while time.monotonic() < deadline:
            if self.result_file.exists():
                try:
                    data = json.loads(self.result_file.read_text(encoding="utf-8-sig"))
                    if data.get("id") == cmd_id:
                        return bool(data.get("passed", False)), data
                except (json.JSONDecodeError, OSError) as exc:
                    last_parse_err = str(exc)
            time.sleep(0.35)

        try:
            self.cmd_file.unlink(missing_ok=True)
        except OSError:
            pass
        extra = f" Son okuma hatasi: {last_parse_err}" if last_parse_err else ""
        return False, {
            "passed": False,
            "summary": f"Unity acik koprusu {self.bridge_timeout_sec}s icinde yanit vermedi.",
            "details": (
                "Unity Editor acik gorunuyor ancak AutoDevBridge.cs henuz derlenmemis veya "
                f"Editor odak/modal bir pencerede takili olabilir.{extra}"
            ),
        }

    def _run_batchmode(self, cmd_id: str, needs_graphics: bool = False) -> tuple[bool, dict]:
        if not self.unity_exe or not Path(self.unity_exe).exists():
            return False, {
                "passed": False,
                "summary": "Unity.exe yolu bulunamadi.",
                "details": f"auto_dev/config.json -> adapter.unity_exe gecersiz: '{self.unity_exe}'",
            }

        cmd = [self.unity_exe, "-batchmode"]
        if not needs_graphics:
            cmd.append("-nographics")
        cmd.extend(
            [
                "-quit",
                "-projectPath",
                str(self.cfg.repo_root),
                "-executeMethod",
                "AutoDevBridge.RunFromCommandLine",
                "-logFile",
                str(self.batch_log_file),
            ]
        )

        try:
            proc = subprocess.run(
                cmd,
                cwd=str(self.cfg.repo_root),
                capture_output=True,
                text=True,
                encoding="utf-8",
                errors="replace",
                timeout=self.batch_timeout_sec,
            )
        except subprocess.TimeoutExpired:
            return False, {
                "passed": False,
                "summary": f"Unity batchmode zaman asimi ({self.batch_timeout_sec}s).",
                "details": self._extract_batch_errors(),
            }

        if self.result_file.exists():
            try:
                data = json.loads(self.result_file.read_text(encoding="utf-8-sig"))
                if data.get("id") == cmd_id:
                    return bool(data.get("passed", False)), data
            except (json.JSONDecodeError, OSError):
                pass

        return False, {
            "passed": False,
            "summary": f"Unity batchmode basarisiz (kod {proc.returncode}).",
            "details": self._extract_batch_errors(),
        }

    def prepare_environment(self) -> None:
        self._dispatch_command("SNAPSHOT", str(self.snapshot_file))

    def pre_task_context(self, task: Task | None) -> str:
        if self.snapshot_file.exists():
            text = self.snapshot_file.read_text(encoding="utf-8", errors="ignore").strip()
            if text:
                return text[:3000]
        return (
            f"Unity Project | Scene: `{self.scene_path}` | "
            "Runtime scripts: `Assets/_GameAssets/Scripts/` | "
            "Behavioral checks: `Assets/_GameAssets/Scripts/Editor/AutoDev/Checks/`"
        )

    def agent_instructions(self, task: Task) -> str:
        check_id = task.verify_check or task.id
        return f"""### Unity 6 Specific Instructions
- Active scene: `{self.scene_path}` (NEVER read or edit `.unity`, `.prefab`, `.asset`, `.csproj` as text!).
- Place runtime C# scripts under `Assets/_GameAssets/Scripts/` (global namespace matching existing scripts).
- Do NOT create Editor menu boilerplate (`Uygula`/`Geri cek`/`Detay`); Git handles rollback.
- Create a small standalone behavioral verification check at `Assets/_GameAssets/Scripts/Editor/AutoDev/Checks/Check_{check_id}.cs` decorated with `[AutoDevCheck("{check_id}")]`:
  - Signature: `public static AutoDevCheckResult Run()`
  - If this task requires scene/asset wiring, perform it idempotently inside `Run()` using Unity Editor API (`SerializedObject`, `AssetDatabase`, `Undo`, `AutoDevVerifier.EnsureSceneOpen`), and set `SaveSceneOnPass = true` ONLY when scene changes must persist.
  - Behavioral rule: Do NOT stop at `GetComponent != null`! Trigger the actual mechanic / invoke `Awake`/`FixedUpdate`/`Physics.Simulate` or events and assert real numeric/state outcomes."""

    def verify_compile(self, task: Task) -> GateResult:
        started = time.monotonic()
        passed, data = self._dispatch_command("COMPILE_CHECK", "")
        elapsed = time.monotonic() - started
        return GateResult(
            name="unity_compile",
            passed=passed,
            summary=str(data.get("summary", "Derleme kontrolu tamamlandi.")),
            error_excerpt="" if passed else str(data.get("details", "")),
            duration_sec=elapsed,
        )

    def verify_behavior(self, task: Task) -> GateResult:
        started = time.monotonic()
        check_id = (task.verify_check or "").strip() or "SMOKE"
        passed, data = self._dispatch_command("VERIFY", check_id)
        elapsed = time.monotonic() - started
        return GateResult(
            name=f"unity_verify:{check_id}",
            passed=passed,
            summary=str(data.get("summary", "Davranissal dogrulama tamamlandi.")),
            error_excerpt="" if passed else str(data.get("details", "")),
            duration_sec=elapsed,
        )

    def capture_visual(self, task: Task, run_dir: Path) -> GateResult:
        started = time.monotonic()
        run_dir.mkdir(parents=True, exist_ok=True)
        out_png = run_dir / f"{task.id}_preview.png"
        passed, data = self._dispatch_command("SCREENSHOT", str(out_png), needs_graphics=True)
        elapsed = time.monotonic() - started
        artifacts = [str(out_png)] if passed and out_png.exists() else []
        return GateResult(
            name="unity_screenshot",
            passed=passed and bool(artifacts),
            summary=str(data.get("summary", "Ekran goruntusu alindi.")),
            error_excerpt="" if passed else str(data.get("details", "")),
            duration_sec=elapsed,
            artifacts=artifacts,
        )
