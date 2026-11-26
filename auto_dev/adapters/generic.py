from __future__ import annotations

import subprocess
import time
from pathlib import Path

from auto_dev.core.config import AutoDevConfig
from auto_dev.core.models import GateResult, Task

from .base import ProjectAdapter


def _trim_errors(stdout: str, stderr: str, max_lines: int = 35) -> str:
    combined = "\n".join(part for part in (stderr.strip(), stdout.strip()) if part)
    lines = combined.splitlines()
    if len(lines) <= max_lines:
        return combined
    return "\n".join(["... [truncated] ...", *lines[-max_lines:]])


class GenericAdapter(ProjectAdapter):
    """Portable shell-command adapter for any language/framework (Python, Node, .NET, Go, Rust)."""

    def __init__(self, cfg: AutoDevConfig) -> None:
        super().__init__(cfg)
        self.compile_cmd: str = str(cfg.adapter.get("compile_cmd", "")).strip()
        self.test_cmd: str = str(cfg.adapter.get("test_cmd", "")).strip()
        self.context_cmd: str = str(cfg.adapter.get("context_cmd", "")).strip()
        self.extra_instructions: str = str(cfg.adapter.get("instructions", "")).strip()
        self.timeout_sec: int = int(cfg.adapter.get("timeout_sec", 180))

    def _run_shell(self, command: str, gate_name: str, task: Task | None = None) -> GateResult:
        if not command:
            return GateResult(name=gate_name, passed=True, summary="Komut tanimli degil; atlandi.")

        rendered = command
        if task is not None:
            rendered = (
                rendered.replace("{task_id}", task.id)
                .replace("{verify_check}", task.verify_check or task.id)
            )

        started = time.monotonic()
        try:
            proc = subprocess.run(
                rendered,
                cwd=str(self.cfg.repo_root),
                shell=True,
                capture_output=True,
                text=True,
                encoding="utf-8",
                errors="replace",
                timeout=self.timeout_sec,
            )
        except subprocess.TimeoutExpired:
            return GateResult(
                name=gate_name,
                passed=False,
                summary=f"Komut zaman asimina ugradi ({self.timeout_sec}s).",
                error_excerpt=f"Timeout running: {rendered}",
                duration_sec=time.monotonic() - started,
            )

        elapsed = time.monotonic() - started
        if proc.returncode == 0:
            return GateResult(
                name=gate_name,
                passed=True,
                summary=f"Basarili ({elapsed:.1f}s).",
                duration_sec=elapsed,
            )
        return GateResult(
            name=gate_name,
            passed=False,
            summary=f"Basarisiz (cikis kodu {proc.returncode}).",
            error_excerpt=_trim_errors(proc.stdout, proc.stderr),
            duration_sec=elapsed,
        )

    def pre_task_context(self, task: Task | None) -> str:
        if not self.context_cmd:
            return f"Adapter: generic ({self.cfg.project_name})"
        res = self._run_shell(self.context_cmd, "context", task)
        return res.error_excerpt or res.summary

    def agent_instructions(self, task: Task) -> str:
        if self.extra_instructions:
            return f"### Project Instructions\n{self.extra_instructions}"
        return "### Project Instructions\nWrite clean, tested code that passes the project's build and test commands."

    def verify_compile(self, task: Task) -> GateResult:
        return self._run_shell(self.compile_cmd, "compile", task)

    def verify_behavior(self, task: Task) -> GateResult:
        return self._run_shell(self.test_cmd, "behavior_test", task)
