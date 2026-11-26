from __future__ import annotations

from dataclasses import asdict, dataclass, field
from datetime import datetime, timezone
from typing import Any


VALID_STATUSES = ("draft", "pending", "in_progress", "done", "blocked", "skipped")


def utc_now_iso() -> str:
    return datetime.now(timezone.utc).replace(microsecond=0).isoformat()


@dataclass
class TokenUsage:
    input_tokens: int = 0
    output_tokens: int = 0
    thinking_tokens: int = 0
    cache_read_tokens: int = 0
    total_tokens: int = 0

    def to_dict(self) -> dict[str, int]:
        return asdict(self)

    @classmethod
    def from_dict(cls, data: dict[str, Any] | None) -> "TokenUsage":
        if not isinstance(data, dict):
            return cls()
        inp = int(data.get("input_tokens", 0) or 0)
        out = int(data.get("output_tokens", 0) or 0)
        thk = int(data.get("thinking_tokens", 0) or 0)
        cache = int(data.get("cache_read_tokens", 0) or 0)
        tot = int(data.get("total_tokens", 0) or 0)
        if tot <= 0 and (inp + out + thk) > 0:
            tot = inp + out + thk
        return cls(
            input_tokens=inp,
            output_tokens=out,
            thinking_tokens=thk,
            cache_read_tokens=cache,
            total_tokens=tot,
        )

    def add(self, other: "TokenUsage") -> "TokenUsage":
        return TokenUsage(
            input_tokens=self.input_tokens + other.input_tokens,
            output_tokens=self.output_tokens + other.output_tokens,
            thinking_tokens=self.thinking_tokens + other.thinking_tokens,
            cache_read_tokens=self.cache_read_tokens + other.cache_read_tokens,
            total_tokens=self.total_tokens + other.total_tokens,
        )


@dataclass
class ModelCallMetric:
    role: str  # "code" | "visual" | "planner"
    pool: str
    model: str
    duration_sec: float = 0.0
    usage: TokenUsage = field(default_factory=TokenUsage)
    ok: bool = True
    is_quota_error: bool = False

    def to_dict(self) -> dict[str, Any]:
        return {
            "role": self.role,
            "pool": self.pool,
            "model": self.model,
            "duration_sec": round(self.duration_sec, 2),
            "usage": self.usage.to_dict(),
            "ok": self.ok,
            "is_quota_error": self.is_quota_error,
        }


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
    is_quota_error: bool = False
    quota_reset_hint_sec: int | None = None
    model_metric: ModelCallMetric | None = None

    def to_dict(self) -> dict[str, Any]:
        return {
            "name": self.name,
            "passed": self.passed,
            "summary": self.summary,
            "error_excerpt": self.error_excerpt,
            "duration_sec": round(self.duration_sec, 2),
            "artifacts": list(self.artifacts),
            "is_quota_error": self.is_quota_error,
        }


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
    usage: TokenUsage = field(default_factory=TokenUsage)
    is_quota_error: bool = False
    quota_reset_hint_sec: int | None = None

    def to_dict(self) -> dict[str, Any]:
        data = asdict(self)
        data["usage"] = self.usage.to_dict()
        return data


@dataclass
class AttemptRecord:
    attempt: int
    model: str
    effort: str
    agent_ok: bool
    agent_summary: str
    pool: str = "gemini"
    gates: list[GateResult] = field(default_factory=list)
    duration_sec: float = 0.0
    error: str = ""
    usage: TokenUsage = field(default_factory=TokenUsage)
    is_quota_error: bool = False
    model_calls: list[ModelCallMetric] = field(default_factory=list)

    def to_dict(self) -> dict[str, Any]:
        return {
            "attempt": self.attempt,
            "pool": self.pool,
            "model": self.model,
            "effort": self.effort,
            "agent_ok": self.agent_ok,
            "agent_summary": self.agent_summary,
            "gates": [g.to_dict() for g in self.gates],
            "duration_sec": round(self.duration_sec, 2),
            "error": self.error,
            "usage": self.usage.to_dict(),
            "is_quota_error": self.is_quota_error,
            "model_calls": [m.to_dict() for m in self.model_calls],
        }


@dataclass
class TaskRunRecord:
    task_id: str
    title: str
    outcome: str  # "done" | "blocked" | "deferred_quota" | "error"
    commit_sha: str = ""
    pool: str = "gemini"
    shift_name: str = ""
    attempts: list[AttemptRecord] = field(default_factory=list)
    duration_sec: float = 0.0
    total_usage: TokenUsage = field(default_factory=TokenUsage)
    pool_remaining_pct_after: dict[str, float] = field(default_factory=dict)
    started_at: str = field(default_factory=utc_now_iso)
    finished_at: str = ""

    def all_model_calls(self) -> list[ModelCallMetric]:
        calls: list[ModelCallMetric] = []
        for att in self.attempts:
            if att.model_calls:
                calls.extend(att.model_calls)
            else:
                calls.append(
                    ModelCallMetric(
                        role="code",
                        pool=att.pool,
                        model=att.model,
                        duration_sec=att.duration_sec,
                        usage=att.usage,
                        ok=att.agent_ok,
                        is_quota_error=att.is_quota_error,
                    )
                )
        return calls

    def model_breakdown(self) -> list[dict[str, Any]]:
        """Aggregate duration, tokens, and percentage shares per model for this task."""
        agg: dict[str, dict[str, Any]] = {}
        total_dur = 0.0
        total_tok = 0
        for c in self.all_model_calls():
            key = c.model
            if key not in agg:
                agg[key] = {
                    "model": c.model,
                    "pool": c.pool,
                    "calls": 0,
                    "duration_sec": 0.0,
                    "tokens": 0,
                }
            agg[key]["calls"] += 1
            agg[key]["duration_sec"] += c.duration_sec
            agg[key]["tokens"] += c.usage.total_tokens
            total_dur += c.duration_sec
            total_tok += c.usage.total_tokens

        out: list[dict[str, Any]] = []
        for item in sorted(agg.values(), key=lambda x: x["duration_sec"], reverse=True):
            dur_pct = (item["duration_sec"] / total_dur * 100.0) if total_dur > 0 else 0.0
            tok_pct = (item["tokens"] / total_tok * 100.0) if total_tok > 0 else dur_pct
            out.append(
                {
                    "model": item["model"],
                    "pool": item["pool"],
                    "calls": item["calls"],
                    "duration_sec": round(item["duration_sec"], 1),
                    "duration_pct": round(dur_pct, 1),
                    "tokens": item["tokens"],
                    "token_pct": round(tok_pct, 1),
                }
            )
        return out

    def to_dict(self) -> dict[str, Any]:
        return {
            "task_id": self.task_id,
            "title": self.title,
            "outcome": self.outcome,
            "commit_sha": self.commit_sha,
            "pool": self.pool,
            "shift_name": self.shift_name,
            "attempts": [a.to_dict() for a in self.attempts],
            "duration_sec": round(self.duration_sec, 2),
            "total_usage": self.total_usage.to_dict(),
            "model_breakdown": self.model_breakdown(),
            "pool_remaining_pct_after": {
                k: round(v, 1) for k, v in self.pool_remaining_pct_after.items()
            },
            "started_at": self.started_at,
            "finished_at": self.finished_at,
        }
