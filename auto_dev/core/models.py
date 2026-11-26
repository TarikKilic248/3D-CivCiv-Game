from __future__ import annotations

from dataclasses import asdict, dataclass, field
from datetime import datetime, timezone
from typing import Any


VALID_STATUSES = ("draft", "pending", "in_progress", "done", "blocked", "skipped")


def utc_now_iso() -> str:
    return datetime.now(timezone.utc).replace(microsecond=0).isoformat()


@dataclass
class Task:
    id: str
    title: str
    description: str
    acceptance_criteria: list[str] = field(default_factory=list)
    status: str = "draft"
    depends_on: list[str] = field(default_factory=list)
    target_files: list[str] = field(default_factory=list)
    context_files: list[str] = field(default_factory=list)
    tier: str = "fast"  # "fast" | "smart"
    visual_check: bool = False
    verify_check: str = ""
    retries: int = 0
    last_error: str = ""
    commit_sha: str = ""
    created_at: str = field(default_factory=utc_now_iso)
    updated_at: str = field(default_factory=utc_now_iso)

    def to_dict(self) -> dict[str, Any]:
        return asdict(self)

    @classmethod
    def from_dict(cls, data: dict[str, Any]) -> "Task":
        known = {
            "id": str(data["id"]),
            "title": str(data["title"]),
            "description": str(data.get("description", "")),
            "acceptance_criteria": [str(x) for x in data.get("acceptance_criteria", [])],
            "status": str(data.get("status", "draft")),
            "depends_on": [str(x) for x in data.get("depends_on", [])],
            "target_files": [str(x) for x in data.get("target_files", [])],
            "context_files": [str(x) for x in data.get("context_files", [])],
            "tier": str(data.get("tier", "fast")),
            "visual_check": bool(data.get("visual_check", False)),
            "verify_check": str(data.get("verify_check", "")),
            "retries": int(data.get("retries", 0)),
            "last_error": str(data.get("last_error", "")),
            "commit_sha": str(data.get("commit_sha", "")),
            "created_at": str(data.get("created_at", utc_now_iso())),
            "updated_at": str(data.get("updated_at", utc_now_iso())),
        }
        if known["status"] not in VALID_STATUSES:
            known["status"] = "draft"
        if known["tier"] not in ("fast", "smart"):
            known["tier"] = "fast"
        return cls(**known)


@dataclass
class GateResult:
    name: str
    passed: bool
    summary: str
    error_excerpt: str = ""
    duration_sec: float = 0.0
    artifacts: list[str] = field(default_factory=list)

    def to_dict(self) -> dict[str, Any]:
        return asdict(self)


@dataclass
class AgentResult:
    ok: bool
    model: str
    effort: str
    summary: str
    changed_files: list[str] = field(default_factory=list)
    structured: dict[str, Any] = field(default_factory=dict)
    raw_output: str = ""
    duration_sec: float = 0.0
    exit_code: int = 0
    error: str = ""

    def to_dict(self) -> dict[str, Any]:
        return asdict(self)


@dataclass
class AttemptRecord:
    attempt: int
    model: str
    effort: str
    agent_ok: bool
    agent_summary: str
    gates: list[GateResult] = field(default_factory=list)
    duration_sec: float = 0.0
    error: str = ""

    def to_dict(self) -> dict[str, Any]:
        return {
            "attempt": self.attempt,
            "model": self.model,
            "effort": self.effort,
            "agent_ok": self.agent_ok,
            "agent_summary": self.agent_summary,
            "gates": [g.to_dict() for g in self.gates],
            "duration_sec": round(self.duration_sec, 2),
            "error": self.error,
        }


@dataclass
class TaskRunRecord:
    task_id: str
    title: str
    outcome: str  # "done" | "blocked" | "error"
    commit_sha: str = ""
    attempts: list[AttemptRecord] = field(default_factory=list)
    duration_sec: float = 0.0
    started_at: str = field(default_factory=utc_now_iso)
    finished_at: str = ""

    def to_dict(self) -> dict[str, Any]:
        return {
            "task_id": self.task_id,
            "title": self.title,
            "outcome": self.outcome,
            "commit_sha": self.commit_sha,
            "attempts": [a.to_dict() for a in self.attempts],
            "duration_sec": round(self.duration_sec, 2),
            "started_at": self.started_at,
            "finished_at": self.finished_at,
        }
