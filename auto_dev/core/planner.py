from __future__ import annotations

from typing import Any

from .agent_runner import AgentRunner
from .config import AutoDevConfig
from .indexer import CodeIndexer
from .models import Task
from .task_store import TaskStore

PLANNER_SCHEMA: dict[str, Any] = {
    "type": "object",
    "required": ["tasks"],
    "properties": {
        "tasks": {
            "type": "array",
            "items": {
                "type": "object",
                "required": [
                    "title",
                    "description",
                    "acceptance_criteria",
                    "target_files",
                    "context_files",
                    "tier",
                    "visual_check",
                ],
                "properties": {
                    "title": {"type": "string"},
                    "description": {"type": "string"},
                    "acceptance_criteria": {"type": "array", "items": {"type": "string"}},
                    "depends_on_indices": {"type": "array", "items": {"type": "integer"}},
                    "depends_on_ids": {"type": "array", "items": {"type": "string"}},
                    "target_files": {"type": "array", "items": {"type": "string"}},
                    "context_files": {"type": "array", "items": {"type": "string"}},
                    "tier": {"type": "string", "enum": ["fast", "smart"]},
                    "visual_check": {"type": "boolean"},
                    "verify_check": {"type": "string"},
                },
            },
        }
    },
}


class TaskPlanner:
    """Two-stage Planner: decomposes a high-level goal into small atomic `draft` tasks."""

    def __init__(
        self,
        cfg: AutoDevConfig,
        store: TaskStore,
        runner: AgentRunner,
        indexer: CodeIndexer,
        adapter: Any,
    ) -> None:
        self.cfg = cfg
        self.store = store
        self.runner = runner
        self.indexer = indexer
        self.adapter = adapter

    def plan_goal(self, goal: str, max_tasks: int = 8) -> list[Task]:
        index_text = self.indexer.build_index(self.cfg.index_file)
        adapter_ctx = self.adapter.pre_task_context(None)
        existing = self.store.load()

        existing_lines = [
            f"- {t.id} [{t.status}]: {t.title}" for t in existing[-30:]
        ] or ["- (yok)"]

        prompt = f"""You are a software architect creating an atomic task backlog for project '{self.cfg.project_name}'.
Do NOT modify any files. Return ONLY valid JSON matching the schema.

## User Goal
{goal}

## Planning Rules (Critical for Low Token Cost & Safe Incremental Progress)
1. Break the goal into at most {max_tasks} SMALL, ATOMIC tasks.
2. Each task should modify/create at most 1-3 files and be independently verifiable.
3. Order tasks cleanly using `depends_on_indices` (0-based indices into your returned `tasks` array) or `depends_on_ids` (existing task IDs).
4. Set `tier` to `"fast"` for routine single-feature coding tasks and `"smart"` ONLY for tricky multi-system architecture/refactors.
5. Set `visual_check` to `true` ONLY for UI/scene visual layout tasks; set `false` for logic/physics/backend tasks to save tokens.
6. Specify exact `context_files` (existing files the coder needs to read) and `target_files` (files to create/edit).
7. Every task must include measurable, behavioral `acceptance_criteria` (not just "file exists").
8. Write titles and descriptions in concise Turkish.
9. Allowed write prefixes: {self.cfg.scope.allowed_write_prefixes}
   Forbidden direct-edit extensions: {self.cfg.scope.forbidden_agent_edit_extensions}

## Existing Backlog
{chr(10).join(existing_lines)}

## Adapter Context
{adapter_ctx}

## Compact Code Index
{index_text}
"""

        result = self.runner.run_prompt(
            prompt=prompt,
            profile=self.cfg.models["planner"],
            schema=PLANNER_SCHEMA,
            tag="planner",
            read_only=True,
        )
        if not result.ok:
            raise RuntimeError(f"Planlayici basarisiz: {result.error}")

        raw_tasks = result.structured.get("tasks", [])
        if not isinstance(raw_tasks, list) or not raw_tasks:
            raise RuntimeError(f"Planlayici gecerli gorev listesi dondurmedi: {result.error or result.summary}")

        # Pre-assign sequential IDs so intra-batch dependencies resolve cleanly
        tasks = self.store.load()
        base_num = 0
        for t in tasks:
            if t.id.upper().startswith("T") and t.id[1:].isdigit():
                base_num = max(base_num, int(t.id[1:]))

        assigned_ids = [f"T{base_num + i + 1:03d}" for i in range(len(raw_tasks))]
        created: list[Task] = []

        for idx, item in enumerate(raw_tasks):
            tid = assigned_ids[idx]
            deps: list[str] = []
            for dep_id in item.get("depends_on_ids", []) or []:
                dep_id = str(dep_id).strip()
                if dep_id and dep_id not in deps:
                    deps.append(dep_id)
            for dep_idx in item.get("depends_on_indices", []) or []:
                if isinstance(dep_idx, int) and 0 <= dep_idx < idx:
                    dep_tid = assigned_ids[dep_idx]
                    if dep_tid not in deps:
                        deps.append(dep_tid)

            verify_check = str(item.get("verify_check", "")).strip() or tid
            task = Task(
                id=tid,
                title=str(item.get("title", f"Gorev {tid}")).strip(),
                description=str(item.get("description", "")).strip(),
                acceptance_criteria=[str(x).strip() for x in item.get("acceptance_criteria", []) if str(x).strip()],
                status="draft",
                depends_on=deps,
                target_files=[str(x).replace("\\", "/") for x in item.get("target_files", [])],
                context_files=[str(x).replace("\\", "/") for x in item.get("context_files", [])],
                tier="smart" if item.get("tier") == "smart" else "fast",
                visual_check=bool(item.get("visual_check", False)),
                verify_check=verify_check,
            )
            created.append(task)
            tasks.append(task)

        dag_errors = self.store.validate_dag(tasks)
        if dag_errors:
            raise RuntimeError("Planlayici gecersiz bagimlilik grafigi uretti: " + "; ".join(dag_errors))

        self.store.save(tasks)
        return created
