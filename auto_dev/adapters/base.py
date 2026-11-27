from __future__ import annotations

from abc import ABC, abstractmethod
from pathlib import Path

from auto_dev.core.config import AutoDevConfig
from auto_dev.core.models import GateResult, Task


class ProjectAdapter(ABC):
    """Pluggable adapter interface separating portable core from project-specific build/test tools."""

    def __init__(self, cfg: AutoDevConfig) -> None:
        self.cfg = cfg

    def prepare_environment(self) -> None:
        """Optional zero-token pre-run preparation (e.g., export scene snapshot)."""

    @abstractmethod
    def pre_task_context(self, task: Task | None) -> str:
        """Return a short, low-token runtime/project context string."""

    @abstractmethod
    def agent_instructions(self, task: Task) -> str:
        """Return concise project-specific instructions for the coding agent."""

    @abstractmethod
    def verify_compile(self, task: Task) -> GateResult:
        """Zero-token compile / static check gate."""

    @abstractmethod
    def verify_behavior(self, task: Task) -> GateResult:
        """Zero-token automated/behavioral test gate."""

    def capture_visual(self, task: Task, run_dir: Path) -> GateResult:
        """Optional screenshot capture gate (called only when task.visual_check is True)."""
        return GateResult(
            name="visual_capture",
            passed=True,
            summary="Bu adaptorde gorsel yakalama tanimli degil; atlandi.",
        )
