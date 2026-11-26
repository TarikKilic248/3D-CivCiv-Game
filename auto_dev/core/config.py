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
    adapter: dict[str, Any]
    auto_dev_dir: Path
    runtime_dir: Path
    tasks_file: Path
    board_file: Path
    index_file: Path
    report_file: Path

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
            max_diff_lines=int(lim_raw.get("max_diff_lines", 1500)),
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

        default_models = {
            "planner": ModelProfile("gemini-4-neon-medium", "medium", 300),
            "fast": ModelProfile("gemini-3.8-flash-medium", "medium", 300),
            "smart": ModelProfile("gemini-4-neon-medium", "medium", 420),
            "visual": ModelProfile("gemini-3.8-flash-medium", "low", 180),
        }
        models_raw = raw.get("models", {})
        models_map: dict[str, ModelProfile] = {}
        for key, default_prof in default_models.items():
            m = models_raw.get(key, {})
            models_map[key] = ModelProfile(
                model=str(m.get("model", default_prof.model)),
                effort=str(m.get("effort", default_prof.effort)),
                timeout_sec=int(m.get("timeout_sec", default_prof.timeout_sec)),
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
            adapter=dict(raw.get("adapter", {"type": "generic"})),
            auto_dev_dir=auto_dev_dir,
            runtime_dir=runtime_dir,
            tasks_file=root / str(paths_raw.get("tasks_file", "auto_dev/tasks.json")),
            board_file=root / str(paths_raw.get("board_file", "auto_dev/BOARD.md")),
            index_file=root / str(paths_raw.get("index_file", "auto_dev/PROJECT_INDEX.md")),
            report_file=root / str(paths_raw.get("report_file", "auto_dev/RUN_REPORT.md")),
        )
