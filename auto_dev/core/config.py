from __future__ import annotations

import json
from dataclasses import dataclass, field
from pathlib import Path
from typing import Any


@dataclass
class ModelProfile:
    model: str
    effort: str = "medium"
    timeout_sec: int = 300
    fallback_models: list[str] = field(default_factory=list)


@dataclass
class ShiftWindowConfig:
    name: str
    start: str  # "HH:MM"
    end: str    # "HH:MM"
    pool: str = "balanced"  # "balanced" | "gemini" | "claude" | "gemini+claude"
    pools: list[str] = field(default_factory=lambda: ["gemini", "claude"])
    mode: str = "balanced"  # "balanced" | "single"


@dataclass
class NightShiftConfig:
    shifts: list[ShiftWindowConfig] = field(
        default_factory=lambda: [
            ShiftWindowConfig("Shift1_Balanced", "00:00", "05:15", "balanced", ["gemini", "claude"], "balanced"),
            ShiftWindowConfig("Shift2_Balanced", "05:15", "10:15", "balanced", ["gemini", "claude"], "balanced"),
        ]
    )
    pool_strategy: str = "balanced"  # "balanced" | "single"
    cross_pool_escalation: bool = True
    prefer_gemini_for_visual: bool = True
    max_consecutive_blocks_night: int = 4
    failover_on_quota: bool = True
    quota_cooldown_minutes: int = 305
    cutoff_buffer_minutes: int = 10
    auto_promote_drafts: bool = True
    auto_promote_batch_size: int = 3
    auto_plan_when_empty: bool = True
    auto_plan_batch_size: int = 3
    max_auto_plan_batches_per_night: int = 2
    keep_awake: bool = True
    pool_5h_budgets: dict[str, int] = field(
        default_factory=lambda: {"gemini": 2_500_000, "claude": 550_000}
    )


@dataclass
class GitConfig:
    protected_branches: list[str] = field(default_factory=lambda: ["main", "master"])
    dev_branch: str = "auto-dev"
    task_branch_prefix: str = "task/"
    auto_push: bool = False
    commit_prefix: str = "feat"
    state_commit_enabled: bool = True


@dataclass
class LimitsConfig:
    max_tasks_per_run: int = 3
    max_retries_per_task: int = 2
    max_consecutive_blocks: int = 2
    max_changed_files: int = 25
    max_diff_lines: int = 1500
    max_context_file_chars: int = 8000
    max_index_chars: int = 14000


@dataclass
class ScopeConfig:
    allowed_write_prefixes: list[str] = field(default_factory=list)
    forbidden_agent_edit_extensions: list[str] = field(default_factory=list)
    index_include_roots: list[str] = field(default_factory=lambda: ["."])
    index_exclude_dirs: list[str] = field(
        default_factory=lambda: [
            ".git",
            ".autodev",
            ".vs",
            ".vscode",
            "Library",
            "Temp",
            "Logs",
            "Obj",
            "Build",
            "Builds",
            "UserSettings",
            "node_modules",
            "__pycache__",
            ".venv",
            "venv",
            "dist",
            "build",
        ]
    )
    index_extensions: list[str] = field(
        default_factory=lambda: [".cs", ".py", ".ts", ".tsx", ".js", ".go", ".rs"]
    )


@dataclass
class AgentCliConfig:
    executable: str = "agy"
    mode: str = "auto"
    skip_permissions: bool = False
    extra_args: list[str] = field(default_factory=list)


@dataclass
class AutoDevConfig:
    repo_root: Path
    project_name: str
    git: GitConfig
    limits: LimitsConfig
    scope: ScopeConfig
    agent_cli: AgentCliConfig
    models: dict[str, ModelProfile]
    model_pools: dict[str, dict[str, ModelProfile]]
    night_shift: NightShiftConfig
    adapter: dict[str, Any]
    auto_dev_dir: Path
    runtime_dir: Path
    tasks_file: Path
    board_file: Path
    index_file: Path
    report_file: Path
    brief_file: Path

    @classmethod
    def _parse_model_map(
        cls,
        raw_map: dict[str, Any],
        defaults: dict[str, ModelProfile],
    ) -> dict[str, ModelProfile]:
        out: dict[str, ModelProfile] = {}
        for key, default_prof in defaults.items():
            m = raw_map.get(key, {}) if isinstance(raw_map, dict) else {}
            out[key] = ModelProfile(
                model=str(m.get("model", default_prof.model)),
                effort=str(m.get("effort", default_prof.effort)),
                timeout_sec=int(m.get("timeout_sec", default_prof.timeout_sec)),
                fallback_models=[
                    str(x).strip()
                    for x in (m.get("fallback_models") or default_prof.fallback_models)
                    if str(x).strip()
                ],
            )
        return out

    @classmethod
    def load(cls, config_path: Path | None = None, repo_root: Path | None = None) -> "AutoDevConfig":
        if config_path is None:
            root = (repo_root or Path.cwd()).resolve()
            config_path = root / "auto_dev" / "config.json"
        else:
            config_path = config_path.resolve()
            root = (repo_root or config_path.parent.parent).resolve()

        raw: dict[str, Any] = {}
        if config_path.exists():
            raw = json.loads(config_path.read_text(encoding="utf-8"))

        git_raw = raw.get("git", {})
        git_cfg = GitConfig(
            protected_branches=list(git_raw.get("protected_branches", ["main", "master"])),
            dev_branch=str(git_raw.get("dev_branch", "auto-dev")),
            task_branch_prefix=str(git_raw.get("task_branch_prefix", "task/")),
            auto_push=bool(git_raw.get("auto_push", False)),
            commit_prefix=str(git_raw.get("commit_prefix", "feat")),
            state_commit_enabled=bool(git_raw.get("state_commit_enabled", True)),
        )

        lim_raw = raw.get("limits", {})
        limits_cfg = LimitsConfig(
            max_tasks_per_run=int(lim_raw.get("max_tasks_per_run", 3)),
            max_retries_per_task=int(lim_raw.get("max_retries_per_task", 2)),
            max_consecutive_blocks=int(lim_raw.get("max_consecutive_blocks", 2)),
            max_changed_files=int(lim_raw.get("max_changed_files", 25)),
            max_diff_lines=int(lim_raw.get("max_diff_lines", 2200)),
            max_context_file_chars=int(lim_raw.get("max_context_file_chars", 8000)),
            max_index_chars=int(lim_raw.get("max_index_chars", 14000)),
        )

        scope_raw = raw.get("scope", {})
        scope_cfg = ScopeConfig(
            allowed_write_prefixes=list(scope_raw.get("allowed_write_prefixes", [])),
            forbidden_agent_edit_extensions=list(
                scope_raw.get("forbidden_agent_edit_extensions", [])
            ),
            index_include_roots=list(scope_raw.get("index_include_roots", ["."])),
            index_exclude_dirs=list(
                scope_raw.get("index_exclude_dirs", ScopeConfig().index_exclude_dirs)
            ),
            index_extensions=list(
                scope_raw.get("index_extensions", ScopeConfig().index_extensions)
            ),
        )

        cli_raw = raw.get("agent_cli", {})
        cli_cfg = AgentCliConfig(
            executable=str(cli_raw.get("executable", "agy")),
            mode=str(cli_raw.get("mode", "auto")),
            skip_permissions=bool(cli_raw.get("skip_permissions", False)),
            extra_args=list(cli_raw.get("extra_args", [])),
        )

        default_gemini = {
            "planner": ModelProfile(
                "gemini-4-hydrogen-medium",
                "medium",
                300,
                fallback_models=["gemini-4-hydrogen-high", "gemini-3.8-flash-high", "gemini-3.8-flash-medium"],
            ),
            "fast": ModelProfile(
                "gemini-3.8-flash-medium",
                "medium",
                420,
                fallback_models=["gemini-3.8-flash-high", "gemini-4-hydrogen-low", "gemini-4-hydrogen-medium"],
            ),
            "smart": ModelProfile(
                "gemini-4-hydrogen-medium",
                "medium",
                420,
                fallback_models=["gemini-4-hydrogen-high", "gemini-4-hydrogen-xhigh", "gemini-3.8-flash-high"],
            ),
            "visual": ModelProfile(
                "gemini-3.8-flash-low",
                "low",
                90,
                fallback_models=["gemini-3.8-flash-medium", "gemini-4-hydrogen-low"],
            ),
        }
        default_claude = {
            "planner": ModelProfile(
                "claude-opus-4-6-thinking",
                "medium",
                360,
                fallback_models=["claude-sonnet-4-6"],
            ),
            "fast": ModelProfile(
                "claude-sonnet-4-6",
                "low",
                300,
                fallback_models=["claude-opus-4-6-thinking"],
            ),
            "smart": ModelProfile(
                "claude-sonnet-4-6",
                "medium",
                420,
                fallback_models=["claude-opus-4-6-thinking"],
            ),
            "visual": ModelProfile(
                "claude-sonnet-4-6",
                "low",
                90,
                fallback_models=["claude-opus-4-6-thinking"],
            ),
        }

        models_map = cls._parse_model_map(raw.get("models", {}), default_gemini)

        pools_raw = raw.get("model_pools", {})
        model_pools: dict[str, dict[str, ModelProfile]] = {}
        if isinstance(pools_raw, dict) and pools_raw:
            for pname, pval in pools_raw.items():
                base_def = default_claude if pname.lower().startswith("claude") else models_map
                model_pools[pname] = cls._parse_model_map(pval, base_def)
        else:
            model_pools["gemini"] = dict(models_map)
            model_pools["claude"] = dict(default_claude)

        ns_raw = raw.get("night_shift", {})
        all_pool_names = list(model_pools.keys())
        shifts_raw = ns_raw.get("shifts", [])
        shifts: list[ShiftWindowConfig] = []
        if isinstance(shifts_raw, list) and shifts_raw:
            for item in shifts_raw:
                raw_pool = str(item.get("pool", "balanced")).strip()
                raw_pools = item.get("pools")
                if isinstance(raw_pools, list) and raw_pools:
                    sh_pools = [str(p).strip() for p in raw_pools if str(p).strip()]
                    sh_mode = str(item.get("mode", "balanced" if len(sh_pools) > 1 else "single"))
                elif raw_pool.lower() == "balanced":
                    sh_pools = list(all_pool_names)
                    sh_mode = "balanced"
                elif "+" in raw_pool:
                    sh_pools = [p.strip() for p in raw_pool.split("+") if p.strip()]
                    sh_mode = "balanced"
                else:
                    sh_pools = [raw_pool]
                    sh_mode = str(item.get("mode", "single"))
                shifts.append(
                    ShiftWindowConfig(
                        name=str(item.get("name", "Shift")),
                        start=str(item.get("start", "00:00")),
                        end=str(item.get("end", "05:15")),
                        pool=raw_pool,
                        pools=sh_pools,
                        mode=sh_mode,
                    )
                )
        else:
            shifts = NightShiftConfig().shifts

        budgets_raw = ns_raw.get("pool_5h_budgets", {})
        budgets = {"gemini": 2_500_000, "claude": 550_000}
        if isinstance(budgets_raw, dict):
            for k, v in budgets_raw.items():
                budgets[str(k)] = int(v)

        night_cfg = NightShiftConfig(
            shifts=shifts,
            pool_strategy=str(ns_raw.get("pool_strategy", "balanced")),
            cross_pool_escalation=bool(ns_raw.get("cross_pool_escalation", True)),
            prefer_gemini_for_visual=bool(ns_raw.get("prefer_gemini_for_visual", True)),
            max_consecutive_blocks_night=int(ns_raw.get("max_consecutive_blocks_night", 4)),
            failover_on_quota=bool(ns_raw.get("failover_on_quota", True)),
            quota_cooldown_minutes=int(ns_raw.get("quota_cooldown_minutes", 305)),
            cutoff_buffer_minutes=int(ns_raw.get("cutoff_buffer_minutes", 10)),
            auto_promote_drafts=bool(ns_raw.get("auto_promote_drafts", True)),
            auto_promote_batch_size=int(ns_raw.get("auto_promote_batch_size", 3)),
            auto_plan_when_empty=bool(ns_raw.get("auto_plan_when_empty", True)),
            auto_plan_batch_size=int(ns_raw.get("auto_plan_batch_size", 3)),
            max_auto_plan_batches_per_night=int(ns_raw.get("max_auto_plan_batches_per_night", 2)),
            keep_awake=bool(ns_raw.get("keep_awake", True)),
            pool_5h_budgets=budgets,
        )

        auto_dev_dir = root / "auto_dev"
        runtime_dir = root / ".autodev"
        paths_raw = raw.get("paths", {})

        return cls(
            repo_root=root,
            project_name=str(raw.get("project_name", root.name)),
            git=git_cfg,
            limits=limits_cfg,
            scope=scope_cfg,
            agent_cli=cli_cfg,
            models=models_map,
            model_pools=model_pools,
            night_shift=night_cfg,
            adapter=dict(raw.get("adapter", {"type": "generic"})),
            auto_dev_dir=auto_dev_dir,
            runtime_dir=runtime_dir,
            tasks_file=root / str(paths_raw.get("tasks_file", "auto_dev/tasks.json")),
            board_file=root / str(paths_raw.get("board_file", "auto_dev/BOARD.md")),
            index_file=root / str(paths_raw.get("index_file", "auto_dev/PROJECT_INDEX.md")),
            report_file=root / str(paths_raw.get("report_file", "auto_dev/RUN_REPORT.md")),
            brief_file=root / str(paths_raw.get("brief_file", "auto_dev/MORNING_BRIEF.md")),
        )
