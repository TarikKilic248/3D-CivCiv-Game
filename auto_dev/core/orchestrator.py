from __future__ import annotations

import time
from datetime import datetime, timezone
from pathlib import Path
from typing import Any

from .agent_runner import AgentRunner
from .config import AutoDevConfig, ModelProfile
from .git_ops import GitOps
from .indexer import CodeIndexer
from .lock import RunnerLock
from .models import AttemptRecord, GateResult, Task, TaskRunRecord, utc_now_iso
from .reporter import RunReporter
from .task_store import TaskStore

TASK_AGENT_SCHEMA: dict[str, Any] = {
    "type": "object",
    "required": ["summary", "changed_files"],
    "properties": {
        "summary": {"type": "string"},
        "changed_files": {"type": "array", "items": {"type": "string"}},
        "notes": {"type": "string"},
    },
}

VISUAL_CHECK_SCHEMA: dict[str, Any] = {
    "type": "object",
    "required": ["passed", "summary", "issues"],
    "properties": {
        "passed": {"type": "boolean"},
        "summary": {"type": "string"},
        "issues": {"type": "array", "items": {"type": "string"}},
    },
}


class Orchestrator:
    """Deterministic zero-token task orchestrator with tiered model escalation and Git isolation."""

    def __init__(self, cfg: AutoDevConfig, adapter: Any) -> None:
        self.cfg = cfg
        self.adapter = adapter
        self.git = GitOps(cfg.repo_root, cfg.git)
        self.store = TaskStore(cfg.tasks_file, cfg.board_file)
        self.indexer = CodeIndexer(cfg.repo_root, cfg.scope, cfg.limits.max_index_chars)
        self.runner = AgentRunner(cfg.repo_root, cfg.agent_cli, cfg.runtime_dir)
        self.reporter = RunReporter(cfg.runtime_dir, cfg.report_file, cfg.project_name)

    def _select_model(self, task: Task, attempt: int) -> ModelProfile:
        if attempt >= 2 or task.tier == "smart":
            return self.cfg.models["smart"]
        return self.cfg.models["fast"]

    def _build_attempt1_prompt(
        self,
        task: Task,
        index_text: str,
        adapter_context: str,
        context_pack: str,
    ) -> str:
        criteria = "\n".join(f"- {c}" for c in task.acceptance_criteria) or "- Gorev eksiksiz calismali."
        targets = ", ".join(f"`{f}`" for f in task.target_files) or "(uygun konum)"
        adapter_rules = self.adapter.agent_instructions(task)

        return f"""You are an autonomous developer executing ONE atomic task for '{self.cfg.project_name}'.

## Strict Token & Safety Rules
- Do NOT run directory-wide scans (`tree`, recursive `Get-ChildItem`) or read unrelated files.
- Do NOT run `git commit`, `git checkout`, `git branch`, or `git push` — Git is managed externally.
- Do NOT directly edit forbidden file types: {self.cfg.scope.forbidden_agent_edit_extensions}
- Only modify files within allowed prefixes: {self.cfg.scope.allowed_write_prefixes}
- Keep changes minimal, clean, well-typed, and strictly scoped to this task.

## Task `{task.id}`: {task.title}
{task.description}

### Expected Target Files
{targets}

### Acceptance Criteria
{criteria}

{adapter_rules}

## Adapter Snapshot
{adapter_context}

## Compact Code Index
{index_text}

## Relevant Context Files
{context_pack if context_pack else "(none preloaded — read only the exact file you need)"}

## Output
Finish by returning ONLY JSON matching the schema (`summary`, `changed_files`, `notes`).
"""

    def _build_retry_prompt(
        self,
        task: Task,
        failed_gates: list[GateResult],
        diff_summary: str,
        adapter_context: str,
    ) -> str:
        errors_block = "\n\n".join(
            f"### Failed Gate: `{g.name}`\nSummary: {g.summary}\n```\n{g.error_excerpt[:2500]}\n```"
            for g in failed_gates
        )
        return f"""You are fixing a failed verification attempt for task `{task.id}`: {task.title}.

## Rules
- Do NOT rewrite from scratch unless necessary; fix the exact root cause shown below.
- Do NOT run `git commit`, `git checkout`, or `git push`.
- Do NOT directly edit forbidden extensions: {self.cfg.scope.forbidden_agent_edit_extensions}
- Only read the specific failing files mentioned in the errors.

## Current Diff Summary
```
{diff_summary[:2000]}
```

## Exact Verification Failures
{errors_block}

## Adapter Snapshot
{adapter_context}

## Output
Fix the issues cleanly and return ONLY JSON matching the schema (`summary`, `changed_files`, `notes`).
"""

    def _run_visual_gate(self, task: Task, run_dir: Path) -> GateResult:
        started = time.monotonic()
        cap = self.adapter.capture_visual(task, run_dir)
        if not cap.passed:
            return cap
        if not cap.artifacts:
            return GateResult(
                name="visual_check",
                passed=True,
                summary="Gorsel dosya uretilmedi, atlandi.",
                duration_sec=time.monotonic() - started,
            )

        screenshot = cap.artifacts[0]
        criteria = "\n".join(f"- {c}" for c in task.acceptance_criteria)
        prompt = f"""Visual QA for task `{task.id}` ({task.title}).
STRICT RULES (low-token fast check):
1. Call `read_file` ONCE on this exact PNG image ONLY: `{screenshot}`
2. Do NOT read any C# or text files. Do NOT run any shell commands. Do NOT explore the repo.
3. Evaluate ONLY obvious visual defects in the image (pitch-black/broken camera, visibly broken/overlapping HUD text, missing always-visible HUD widget). Note: panels that start hidden (e.g. EndGamePanel) are EXPECTED to be hidden, and the pre-existing centered Settings popup may appear visible in Edit Mode because SettingsUI.Start() hides it at runtime (ignore it).
4. Immediately return ONLY JSON matching the schema (`passed`, `summary`, `issues`).

Task criteria context:
{criteria}
"""
        res = self.runner.run_prompt(
            prompt=prompt,
            profile=self.cfg.models["visual"],
            schema=VISUAL_CHECK_SCHEMA,
            tag=f"{task.id}_visual",
            run_dir=run_dir,
            read_only=True,
        )
        elapsed = time.monotonic() - started
        if not res.ok:
            return GateResult(
                name="visual_check",
                passed=False,
                summary="Gorsel dogrulama modeli calistirilamadi.",
                error_excerpt=res.error,
                duration_sec=elapsed,
                artifacts=cap.artifacts,
            )
        passed = bool(res.structured.get("passed", True))
        issues = res.structured.get("issues", [])
        return GateResult(
            name="visual_check",
            passed=passed,
            summary=str(res.structured.get("summary", "Gorsel kontrol tamamlandi.")),
            error_excerpt="\n".join(str(x) for x in issues) if not passed else "",
            duration_sec=elapsed,
            artifacts=cap.artifacts,
        )

    def run(self, max_tasks: int | None = None, dry_run: bool = False) -> dict[str, Any]:
        task_limit = max_tasks if max_tasks is not None else self.cfg.limits.max_tasks_per_run
        run_id = "run_" + datetime.now(timezone.utc).strftime("%Y%m%d_%H%M%S")
        run_dir = self.cfg.runtime_dir / "runs" / run_id
        started_at = utc_now_iso()

        lock = RunnerLock(self.cfg.runtime_dir / "runner.lock")
        records: list[TaskRunRecord] = []
        stop_reason = "completed"

        with lock:
            tasks = self.store.load()
            dag_errors = self.store.validate_dag(tasks)
            if dag_errors:
                raise RuntimeError("Gorev grafigi hatali:\n" + "\n".join(dag_errors))

            if dry_run:
                ready = self.store.ready_tasks(tasks)[:task_limit]
                return {
                    "run_id": run_id,
                    "dry_run": True,
                    "ready_tasks": [t.to_dict() for t in ready],
                }

            self._sync_pending_state_files()
            self.git.ensure_safe_dev_branch()
            index_text = self.indexer.build_index(self.cfg.index_file)
            self.adapter.prepare_environment()
            self.store.render_board(tasks)
            self.git.commit_state_files(
                [self.cfg.index_file, self.cfg.board_file],
                f"chore(autodev): sync index and board before {run_id}",
            )

            consecutive_blocks = 0
            executed = 0

            while executed < task_limit:
                tasks = self.store.load()
                task = self.store.next_ready_task(tasks)
                if task is None:
                    stop_reason = "no_ready_tasks" if executed == 0 else "queue_drained"
                    break

                if consecutive_blocks >= self.cfg.limits.max_consecutive_blocks:
                    stop_reason = f"max_consecutive_blocks ({consecutive_blocks})"
                    break

                executed += 1
                print(f"\n[AutoDev] >>> Baslatiliyor: {task.id} — {task.title}")
                record = self._execute_single_task(task, index_text, run_dir)
                records.append(record)

                if record.outcome == "done":
                    consecutive_blocks = 0
                    index_text = self.indexer.build_index(self.cfg.index_file)
                    self.git.commit_state_files(
                        [self.cfg.tasks_file, self.cfg.board_file, self.cfg.index_file],
                        f"chore(autodev): update state after {task.id}",
                    )
                else:
                    consecutive_blocks += 1
                    self.git.commit_state_files(
                        [self.cfg.tasks_file, self.cfg.board_file],
                        f"chore(autodev): mark {task.id} blocked",
                    )
                    if consecutive_blocks >= self.cfg.limits.max_consecutive_blocks:
                        stop_reason = f"max_consecutive_blocks ({consecutive_blocks})"
                        break

            finished_at = utc_now_iso()
            report_path = self.reporter.save_run(
                run_id=run_id,
                records=records,
                stop_reason=stop_reason,
                started_at=started_at,
                finished_at=finished_at,
            )
            self.git.commit_state_files(
                [self.cfg.report_file, self.cfg.board_file, self.cfg.index_file],
                f"chore(autodev): record {run_id}",
            )
            if self.cfg.git.auto_push and any(r.outcome == "done" for r in records):
                self.git.push_dev_branch()

            return {
                "run_id": run_id,
                "stop_reason": stop_reason,
                "report_path": str(report_path),
                "records": [r.to_dict() for r in records],
            }

    def _sync_pending_state_files(self) -> None:
        """Auto-commit ONLY AutoDev state/plan files on dev_branch (e.g. right after `approve` or `plan`)."""
        if self.git.current_branch() != self.cfg.git.dev_branch:
            return
        state_paths = [
            self.cfg.tasks_file,
            self.cfg.board_file,
            self.cfg.index_file,
            self.cfg.report_file,
            self.cfg.auto_dev_dir / "ROADMAP.md",
        ]
        allowed_rel = {
            p.resolve().relative_to(self.cfg.repo_root.resolve()).as_posix()
            for p in state_paths
        }
        dirty = self.git.changed_files()
        if dirty and all(f in allowed_rel for f in dirty):
            self.git.commit_state_files(
                [p for p in state_paths if p.exists()],
                "chore(autodev): sync task queue and board state",
            )

    def _snapshot_file_mtimes(self, paths: list[str]) -> dict[str, tuple[int, int]]:
        snap: dict[str, tuple[int, int]] = {}
        for rel in paths:
            p = self.cfg.repo_root / rel
            if p.is_file():
                st = p.stat()
                snap[rel] = (st.st_mtime_ns, st.st_size)
        return snap

    def _execute_single_task(
        self,
        task: Task,
        index_text: str,
        run_dir: Path,
    ) -> TaskRunRecord:
        t_start = time.monotonic()
        record = TaskRunRecord(task_id=task.id, title=task.title, outcome="blocked")
        max_attempts = max(1, self.cfg.limits.max_retries_per_task)

        self.git.start_task_branch(task.id)
        adapter_context = self.adapter.pre_task_context(task)
        context_pack = self.indexer.build_context_pack(
            task,
            max_chars_per_file=self.cfg.limits.max_context_file_chars,
            forbidden_extensions=self.cfg.scope.forbidden_agent_edit_extensions,
        )

        last_failed_gates: list[GateResult] = []
        last_agent_summary = ""

        for attempt in range(1, max_attempts + 1):
            a_start = time.monotonic()
            profile = self._select_model(task, attempt)
            print(
                f"[AutoDev]     Deneme {attempt}/{max_attempts} | "
                f"Model: {profile.model} (effort={profile.effort})"
            )

            if attempt == 1:
                prompt = self._build_attempt1_prompt(task, index_text, adapter_context, context_pack)
            else:
                diff_summary = self.git.diff_stat_summary(base_ref=self.cfg.git.dev_branch)
                prompt = self._build_retry_prompt(task, last_failed_gates, diff_summary, adapter_context)

            pre_files = self.git.changed_files(base_ref=self.cfg.git.dev_branch)
            pre_snap = self._snapshot_file_mtimes(pre_files)

            agent_res = self.runner.run_prompt(
                prompt=prompt,
                profile=profile,
                schema=TASK_AGENT_SCHEMA,
                tag=f"{task.id}_a{attempt}",
                run_dir=run_dir,
                read_only=False,
            )
            last_agent_summary = agent_res.summary

            post_files = self.git.changed_files(base_ref=self.cfg.git.dev_branch)
            post_snap = self._snapshot_file_mtimes(post_files)
            agent_touched = [
                f for f in post_files if f not in pre_snap or post_snap.get(f) != pre_snap.get(f)
            ]

            gates: list[GateResult] = []
            if not agent_res.ok:
                gates.append(
                    GateResult(
                        name="agent_exec",
                        passed=False,
                        summary=f"Ajan oturumu basarisiz (kod {agent_res.exit_code}).",
                        error_excerpt=agent_res.error,
                    )
                )
            else:
                # Gate 1: Zero-token Scope & Diff Guard BEFORE adapter touches generated/scene files
                g_scope = self.git.validate_agent_diff(
                    allowed_prefixes=self.cfg.scope.allowed_write_prefixes,
                    forbidden_extensions=self.cfg.scope.forbidden_agent_edit_extensions,
                    max_files=self.cfg.limits.max_changed_files,
                    max_lines=self.cfg.limits.max_diff_lines,
                    base_ref=self.cfg.git.dev_branch,
                    forbidden_check_files=agent_touched,
                )
                gates.append(g_scope)

                # Gate 2: Zero-token Compile/Build Verification
                if g_scope.passed:
                    g_compile = self.adapter.verify_compile(task)
                    gates.append(g_compile)

                    # Gate 3: Zero-token Behavioral Verification / Scene Setup
                    if g_compile.passed:
                        g_behav = self.adapter.verify_behavior(task)
                        gates.append(g_behav)

                        # Gate 4: Conditional Visual Check (ONLY when task.visual_check is True)
                        if g_behav.passed and task.visual_check:
                            g_vis = self._run_visual_gate(task, run_dir)
                            gates.append(g_vis)

            all_passed = all(g.passed for g in gates)
            failed = [g for g in gates if not g.passed]
            att_rec = AttemptRecord(
                attempt=attempt,
                model=profile.model,
                effort=profile.effort,
                agent_ok=agent_res.ok,
                agent_summary=agent_res.summary,
                gates=gates,
                duration_sec=time.monotonic() - a_start,
                error=failed[0].error_excerpt[:400] if failed else "",
            )
            record.attempts.append(att_rec)

            for g in gates:
                icon = "PASS" if g.passed else "FAIL"
                print(f"[AutoDev]       [{icon}] {g.name}: {g.summary}")

            if all_passed:
                sha = self.git.commit_and_merge_task(task, detail_summary=last_agent_summary)
                task.status = "done"
                task.commit_sha = sha
                task.retries = attempt - 1
                task.last_error = ""
                self.store.update_task(task)
                record.outcome = "done"
                record.commit_sha = sha
                record.duration_sec = time.monotonic() - t_start
                record.finished_at = utc_now_iso()
                print(f"[AutoDev] <<< TAMAMLANDI: {task.id} -> commit {sha}")
                return record

            last_failed_gates = failed
            task.retries = attempt

        # Exhausted retries -> deterministic rollback, mark blocked
        print(f"[AutoDev] !!! BLOKLANDI: {task.id} — degisiklikler geri aliniyor.")
        self.git.rollback_task_branch(task.id)
        err_summary = (
            f"{last_failed_gates[0].name}: {last_failed_gates[0].summary}\n{last_failed_gates[0].error_excerpt}"
            if last_failed_gates
            else "Bilinmeyen hata"
        )
        task.status = "blocked"
        task.last_error = err_summary[:800]
        self.store.update_task(task)
        record.outcome = "blocked"
        record.duration_sec = time.monotonic() - t_start
        record.finished_at = utc_now_iso()
        return record
