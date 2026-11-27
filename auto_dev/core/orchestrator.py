from __future__ import annotations

import time
from datetime import datetime, timedelta, timezone
from pathlib import Path
from typing import Any

from .agent_runner import AgentRunner
from .config import AutoDevConfig, ModelProfile
from .git_ops import GitError, GitOps
from .indexer import CodeIndexer
from .lock import RunnerLock
from .models import (
    AttemptRecord,
    GateResult,
    ModelCallMetric,
    Task,
    TaskRunRecord,
    TokenUsage,
    utc_now_iso,
)
from .planner import TaskPlanner
from .pool_manager import PoolManager
from .power import KeepAwake
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
    """Deterministic zero-token task orchestrator with dual quota pools, failover, and Git isolation."""

    def __init__(
        self,
        cfg: AutoDevConfig,
        adapter: Any,
        pool_manager: PoolManager | None = None,
    ) -> None:
        self.cfg = cfg
        self.adapter = adapter
        self.git = GitOps(cfg.repo_root, cfg.git)
        self.store = TaskStore(cfg.tasks_file, cfg.board_file)
        self.indexer = CodeIndexer(cfg.repo_root, cfg.scope, cfg.limits.max_index_chars)
        self.runner = AgentRunner(cfg.repo_root, cfg.agent_cli, cfg.runtime_dir)
        self.reporter = RunReporter(
            cfg.runtime_dir,
            cfg.report_file,
            cfg.project_name,
            brief_file=cfg.brief_file,
        )
        self.pool_manager = pool_manager or PoolManager(
            pools=cfg.model_pools,
            state_file=cfg.runtime_dir / "pool_state.json",
            default_cooldown_min=cfg.night_shift.quota_cooldown_minutes,
            pool_budgets=cfg.night_shift.pool_5h_budgets,
        )
        self.planner = TaskPlanner(cfg, self.store, self.runner, self.indexer, adapter)

    def _select_model(self, task: Task, attempt: int, pool_name: str) -> ModelProfile:
        role = "smart" if (attempt >= 2 or task.tier == "smart") else "fast"
        return self.pool_manager.get_profile(pool_name, role)

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

    def _run_visual_gate(self, task: Task, run_dir: Path, pool_name: str) -> GateResult:
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
3. Evaluate BOTH functional visuals AND art/style consistency:
   - Reject pitch-black/broken camera, visibly overlapping HUD text, or missing always-visible HUD widgets.
   - Reject unstyled default Unity grey/blue flat sliders or plain white/grey rectangles that clash with the game's existing warm golden-yellow/amber/orange cartoon UI style (matching Settings, Clock, Coin). New HUD bars/panels MUST use warm gold/amber/orange styling, crisp borders/outlines, and consistent font styling.
   - Note: panels that start hidden (e.g. EndGamePanel) are EXPECTED to be hidden, and the pre-existing centered Settings popup may appear visible in Edit Mode because SettingsUI.Start() hides it at runtime (ignore it).
4. Immediately return ONLY JSON matching the schema (`passed`, `summary`, `issues`).

Task criteria context:
{criteria}
"""
        vis_profile = self.pool_manager.get_profile(pool_name, "visual")
        res = self.runner.run_prompt(
            prompt=prompt,
            profile=vis_profile,
            schema=VISUAL_CHECK_SCHEMA,
            tag=f"{task.id}_visual",
            run_dir=run_dir,
            read_only=True,
        )
        self.pool_manager.record_usage(pool_name, res.usage.total_tokens)
        vis_metric = ModelCallMetric(
            role="visual",
            pool=pool_name,
            model=vis_profile.model,
            duration_sec=res.duration_sec,
            usage=res.usage,
            ok=res.ok,
            is_quota_error=res.is_quota_error,
        )
        elapsed = time.monotonic() - started

        if not res.ok:
            return GateResult(
                name="visual_check",
                passed=False,
                summary=(
                    "Gorsel dogrulamada kota limiti doldu."
                    if res.is_quota_error
                    else "Gorsel dogrulama modeli calistirilamadi."
                ),
                error_excerpt=res.error,
                duration_sec=elapsed,
                artifacts=cap.artifacts,
                is_quota_error=res.is_quota_error,
                quota_reset_hint_sec=res.quota_reset_hint_sec,
                model_metric=vis_metric,
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
            model_metric=vis_metric,
        )

    def _try_promote_drafts(self, batch_size: int) -> list[str]:
        tasks = self.store.load()
        status_by_id = {t.id: t.status for t in tasks}
        drafts = [t for t in tasks if t.status == "draft"]
        if not drafts:
            return []

        # Prefer drafts whose dependencies are already done or pending/promoted
        chosen: list[str] = []
        will_be_active = {t.id for t in tasks if t.status in ("done", "pending")}
        for t in drafts:
            if len(chosen) >= batch_size:
                break
            if all(dep in will_be_active for dep in t.depends_on):
                chosen.append(t.id)
                will_be_active.add(t.id)

        if not chosen:
            chosen = [t.id for t in drafts[:batch_size]]

        promoted = self.store.approve(task_ids=chosen)
        if promoted:
            print(f"[AutoDev] Otomatik draft terfi (0 token): {', '.join(promoted)}", flush=True)
            self.git.commit_state_files(
                [self.cfg.tasks_file, self.cfg.board_file],
                f"chore(autodev): auto-promote drafts {', '.join(promoted)}",
            )
        return promoted

    def _try_auto_plan(
        self,
        pool_name: str,
        batch_size: int,
        run_dir: Path,
        extra_calls: list[ModelCallMetric],
    ) -> tuple[list[str], bool, int | None]:
        """Returns (created_ids, is_quota_error, quota_hint_sec)."""
        profile = self.pool_manager.get_profile(pool_name, "planner")
        goal = (
            f"'{self.cfg.project_name}' projesinin mevcut tamamlanan sistemlerini incele ve "
            "oynanışı zenginleştiren, görsel/işitsel cilası (Dikey Dilim & UIThemeSO) tam, "
            f"tekrar etmeyen en fazla {batch_size} yeni küçük atomik geliştirme görevi üret."
        )
        print(
            f"[AutoDev] Kuyruk bitti — yeni gorevler uretiliyor "
            f"(Havuz: {pool_name}, Model: {profile.model})...",
            flush=True,
        )
        created, res = self.planner.plan_goal_with_result(
            goal=goal,
            max_tasks=batch_size,
            profile=profile,
            auto_approve=True,
            run_dir=run_dir,
        )
        self.pool_manager.record_usage(pool_name, res.usage.total_tokens)
        extra_calls.append(
            ModelCallMetric(
                role="planner",
                pool=pool_name,
                model=profile.model,
                duration_sec=res.duration_sec,
                usage=res.usage,
                ok=res.ok,
                is_quota_error=res.is_quota_error,
            )
        )
        if res.is_quota_error:
            return [], True, res.quota_reset_hint_sec
        if not created:
            print(f"[AutoDev] Otomatik planlama gorev uretemedi: {res.error}", flush=True)
            return [], False, None

        ids = [t.id for t in created]
        print(f"[AutoDev] Otomatik uretilen & onaylanan gorevler: {', '.join(ids)}", flush=True)
        self.git.commit_state_files(
            [self.cfg.tasks_file, self.cfg.board_file],
            f"chore(autodev): auto-plan new tasks {', '.join(ids)}",
        )
        return ids, False, None

    def run(
        self,
        max_tasks: int | None = None,
        dry_run: bool = False,
        pool_name: str | None = None,
        pool_strategy: str = "single",
        active_pools: list[str] | None = None,
        cross_pool_escalation: bool = False,
        prefer_gemini_for_visual: bool = True,
        max_consecutive_blocks: int | None = None,
        shift_name: str = "",
        deadline: datetime | None = None,
        cutoff_buffer_min: int | None = None,
        failover_on_quota: bool | None = None,
        auto_promote_drafts: bool = False,
        auto_promote_batch_size: int | None = None,
        auto_plan_when_empty: bool = False,
        auto_plan_batch_size: int | None = None,
        max_auto_plan_batches: int | None = None,
        wait_lock_sec: int = 0,
        keep_awake: bool = True,
    ) -> dict[str, Any]:
        task_limit = self.cfg.limits.max_tasks_per_run if max_tasks is None else int(max_tasks)
        unlimited_tasks = task_limit <= 0
        pools_list = [p for p in (active_pools or self.pool_manager.pool_names()) if p in self.pool_manager.pools]
        if not pools_list:
            pools_list = self.pool_manager.pool_names()
        preferred_pool = pool_name if (pool_name and pool_name in self.pool_manager.pools) else pools_list[0]
        is_balanced = pool_strategy == "balanced" and len(pools_list) > 1
        allow_failover = (
            self.cfg.night_shift.failover_on_quota
            if failover_on_quota is None
            else bool(failover_on_quota)
        )
        max_blocks_limit = (
            self.cfg.limits.max_consecutive_blocks
            if max_consecutive_blocks is None
            else max(1, int(max_consecutive_blocks))
        )
        buf_min = (
            self.cfg.night_shift.cutoff_buffer_minutes
            if cutoff_buffer_min is None
            else int(cutoff_buffer_min)
        )
        promote_batch = auto_promote_batch_size or self.cfg.night_shift.auto_promote_batch_size
        plan_batch = auto_plan_batch_size or self.cfg.night_shift.auto_plan_batch_size
        max_plan_batches = (
            self.cfg.night_shift.max_auto_plan_batches_per_night
            if max_auto_plan_batches is None
            else int(max_auto_plan_batches)
        )

        run_id = "run_" + datetime.now(timezone.utc).strftime("%Y%m%d_%H%M%S")
        run_dir = self.cfg.runtime_dir / "runs" / run_id
        started_at = utc_now_iso()

        lock = RunnerLock(self.cfg.runtime_dir / "runner.lock", wait_timeout_sec=wait_lock_sec)
        records: list[TaskRunRecord] = []
        extra_calls: list[ModelCallMetric] = []
        auto_planned_ids: list[str] = []
        stop_reason = "completed"
        next_available_at: datetime | None = None
        pushed_ok = False

        with KeepAwake(enabled=keep_awake and not dry_run), lock:
            tasks = self.store.load()
            dag_errors = self.store.validate_dag(tasks)
            if dag_errors:
                raise RuntimeError("Gorev grafigi hatali:\n" + "\n".join(dag_errors))

            if dry_run:
                ready = self.store.ready_tasks(tasks)
                if not unlimited_tasks:
                    ready = ready[:task_limit]
                return {
                    "run_id": run_id,
                    "dry_run": True,
                    "pool": "balanced" if is_balanced else preferred_pool,
                    "active_pools": pools_list,
                    "pool_status": self.pool_manager.status_summary(),
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
            executed_done_or_blocked = 0
            auto_plan_batches_used = 0
            active_pool: str | None = preferred_pool
            shift_pool_counts: dict[str, int] = {p: 0 for p in pools_list}

            try:
                while unlimited_tasks or executed_done_or_blocked < task_limit:
                    # 1. Check wall-clock shift deadline with buffer
                    if deadline is not None:
                        now_local = datetime.now(deadline.tzinfo)
                        remaining_min = (deadline - now_local).total_seconds() / 60.0
                        if remaining_min < buf_min:
                            stop_reason = f"window_deadline ({remaining_min:.1f}m left)"
                            break

                    # 2. Pick next ready task, or auto-promote drafts, or auto-plan if empty
                    tasks = self.store.load()
                    task = self.store.next_ready_task(tasks)
                    if task is None and auto_promote_drafts:
                        if self._try_promote_drafts(promote_batch):
                            tasks = self.store.load()
                            task = self.store.next_ready_task(tasks)

                    if task is None and auto_plan_when_empty and auto_plan_batches_used < max_plan_batches:
                        enough_time = True
                        if deadline is not None:
                            rem_min = (deadline - datetime.now(deadline.tzinfo)).total_seconds() / 60.0
                            enough_time = rem_min >= max(25, buf_min + 15)
                        if enough_time:
                            plan_pool, wake_at = (
                                self.pool_manager.select_balanced_pool(
                                    pools_list, task_tier="smart", shift_pool_counts=shift_pool_counts
                                )
                                if is_balanced
                                else self.pool_manager.select_available_pool(
                                    preferred_pool, allow_failover=allow_failover
                                )
                            )
                            if plan_pool is None:
                                stop_reason = "all_pools_exhausted"
                                next_available_at = wake_at
                                break
                            new_ids, plan_quota, plan_hint = self._try_auto_plan(
                                plan_pool, plan_batch, run_dir, extra_calls
                            )
                            if plan_quota:
                                self.pool_manager.mark_quota_exhausted(plan_pool, plan_hint)
                                continue
                            if new_ids:
                                auto_plan_batches_used += 1
                                auto_planned_ids.extend(new_ids)
                                tasks = self.store.load()
                                task = self.store.next_ready_task(tasks)

                    if task is None:
                        stop_reason = "no_ready_tasks" if not records else "queue_drained"
                        break

                    if consecutive_blocks >= max_blocks_limit:
                        stop_reason = f"max_consecutive_blocks ({consecutive_blocks})"
                        break

                    # 3. Select optimal quota pool (balanced dual-pool or single + failover)
                    if is_balanced:
                        selected_pool, wake_at = self.pool_manager.select_balanced_pool(
                            pools_list,
                            task_tier=task.tier,
                            shift_pool_counts=shift_pool_counts,
                        )
                    else:
                        selected_pool, wake_at = self.pool_manager.select_available_pool(
                            preferred_pool, allow_failover=allow_failover
                        )

                    if selected_pool is None:
                        stop_reason = "all_pools_exhausted"
                        next_available_at = wake_at
                        break
                    if selected_pool != active_pool:
                        print(
                            f"[AutoDev] Havuz secimi: {active_pool} -> {selected_pool} "
                            f"(kalan: %{self.pool_manager.get_remaining_pct(selected_pool)})",
                            flush=True,
                        )
                    active_pool = selected_pool

                    print(
                        f"\n[AutoDev] >>> Baslatiliyor: {task.id} — {task.title} "
                        f"[Havuz: {active_pool} | Kalan: %{self.pool_manager.get_remaining_pct(active_pool)}]",
                        flush=True,
                    )
                    record, quota_pool, quota_hint = self._execute_single_task(
                        task,
                        index_text,
                        run_dir,
                        pool_name=active_pool,
                        active_pools=pools_list,
                        cross_pool_escalation=cross_pool_escalation,
                        prefer_gemini_for_visual=prefer_gemini_for_visual,
                        shift_name=shift_name,
                    )
                    records.append(record)

                    if record.outcome == "deferred_quota":
                        exhausted_p = quota_pool or active_pool
                        until = self.pool_manager.mark_quota_exhausted(exhausted_p, quota_hint)
                        record.pool_remaining_pct_after = self.pool_manager.snapshot_remaining_pcts()
                        print(
                            f"[AutoDev] !!! Kota doldu ({exhausted_p}). {task.id} geri alindi ve 'pending' korundu. "
                            f"Soguma bitisi: {until.isoformat()}",
                            flush=True,
                        )
                        continue

                    used_pools = {att.pool for att in record.attempts if att.pool} or {active_pool}
                    for p_used in used_pools:
                        shift_pool_counts[p_used] = shift_pool_counts.get(p_used, 0) + 1
                    executed_done_or_blocked += 1
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
                        if consecutive_blocks >= max_blocks_limit:
                            stop_reason = f"max_consecutive_blocks ({consecutive_blocks})"
                            break
            except KeyboardInterrupt:
                stop_reason = "interrupted_by_user"
                print("\n[AutoDev] Kullanici tarafindan durduruldu — yarim kalan degisiklikler temizleniyor...", flush=True)
                cur_branch = self.git.current_branch()
                if cur_branch.startswith(self.cfg.git.task_branch_prefix):
                    tid = cur_branch[len(self.cfg.git.task_branch_prefix) :]
                    self.git.rollback_task_branch(tid)

            finished_at = utc_now_iso()
            pool_status_after = self.pool_manager.status_summary()
            display_pool = "balanced" if is_balanced else preferred_pool
            report_path = self.reporter.save_run(
                run_id=run_id,
                records=records,
                stop_reason=stop_reason,
                started_at=started_at,
                finished_at=finished_at,
                shift_name=shift_name or f"Run ({display_pool})",
                initial_pool=display_pool,
                pool_status_after=pool_status_after,
                extra_calls=extra_calls,
                auto_planned_ids=auto_planned_ids,
                pushed_to_remote=False,
            )
            self.git.commit_state_files(
                [self.cfg.report_file, self.cfg.brief_file, self.cfg.board_file, self.cfg.index_file],
                f"chore(autodev): record {run_id}",
            )

            if self.cfg.git.auto_push and any(r.outcome == "done" for r in records):
                try:
                    self.git.push_dev_branch()
                    pushed_ok = True
                    print(f"[AutoDev] Otomatik push tamamlandi: origin/{self.cfg.git.dev_branch}", flush=True)
                    # Update brief with pushed status (no extra push needed just for the flag)
                    self.reporter.save_run(
                        run_id=run_id,
                        records=records,
                        stop_reason=stop_reason,
                        started_at=started_at,
                        finished_at=finished_at,
                        shift_name=shift_name or f"Run ({display_pool})",
                        initial_pool=display_pool,
                        pool_status_after=pool_status_after,
                        extra_calls=extra_calls,
                        auto_planned_ids=auto_planned_ids,
                        pushed_to_remote=True,
                    )
                    self.git.commit_state_files(
                        [self.cfg.report_file, self.cfg.brief_file],
                        f"chore(autodev): update brief push status {run_id}",
                    )
                    self.git.push_dev_branch()
                except GitError as exc:
                    print(f"[AutoDev UYARI] Otomatik push basarisiz (yerel commitler guvende): {exc}", flush=True)

            return {
                "run_id": run_id,
                "shift_name": shift_name,
                "stop_reason": stop_reason,
                "next_available_at": next_available_at.isoformat() if next_available_at else None,
                "pushed_to_remote": pushed_ok,
                "report_path": str(report_path),
                "brief_path": str(self.cfg.brief_file),
                "pool_status": pool_status_after,
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
            self.cfg.brief_file,
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
        pool_name: str = "gemini",
        active_pools: list[str] | None = None,
        cross_pool_escalation: bool = False,
        prefer_gemini_for_visual: bool = True,
        shift_name: str = "",
    ) -> tuple[TaskRunRecord, str | None, int | None]:
        t_start = time.monotonic()
        pools_list = active_pools or self.pool_manager.pool_names()
        record = TaskRunRecord(
            task_id=task.id,
            title=task.title,
            outcome="blocked",
            pool=pool_name,
            shift_name=shift_name,
        )
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
        task_usage = TokenUsage()

        for attempt in range(1, max_attempts + 1):
            a_start = time.monotonic()
            attempt_pool = (
                pool_name
                if attempt == 1
                else self.pool_manager.select_escalation_pool(
                    pool_name, pools_list, cross_pool=cross_pool_escalation
                )
            )
            profile = self._select_model(task, attempt, attempt_pool)
            esc_note = " (capraz havuz onarim)" if (attempt >= 2 and attempt_pool != pool_name) else ""
            print(
                f"[AutoDev]     Deneme {attempt}/{max_attempts} | "
                f"Havuz: {attempt_pool}{esc_note} | Model: {profile.model}",
                flush=True,
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
                tag=f"{task.id}_{attempt_pool}_a{attempt}",
                run_dir=run_dir,
                read_only=False,
            )
            last_agent_summary = agent_res.summary
            self.pool_manager.record_usage(attempt_pool, agent_res.usage.total_tokens)
            task_usage = task_usage.add(agent_res.usage)

            model_calls: list[ModelCallMetric] = [
                ModelCallMetric(
                    role="code",
                    pool=attempt_pool,
                    model=profile.model,
                    duration_sec=agent_res.duration_sec,
                    usage=agent_res.usage,
                    ok=agent_res.ok,
                    is_quota_error=agent_res.is_quota_error,
                )
            ]

            # Quota exhausted during coding turn -> rollback cleanly, preserve task as pending!
            if agent_res.is_quota_error:
                g_quota = GateResult(
                    name="quota_guard",
                    passed=False,
                    summary=f"Havuz '{attempt_pool}' kotasi doldu; gorev geri aliniyor (pending korunacak).",
                    error_excerpt=agent_res.error,
                    is_quota_error=True,
                    quota_reset_hint_sec=agent_res.quota_reset_hint_sec,
                )
                record.attempts.append(
                    AttemptRecord(
                        attempt=attempt,
                        pool=attempt_pool,
                        model=profile.model,
                        effort=profile.effort,
                        agent_ok=False,
                        agent_summary=agent_res.summary,
                        gates=[g_quota],
                        duration_sec=time.monotonic() - a_start,
                        error=agent_res.error[:400],
                        usage=agent_res.usage,
                        is_quota_error=True,
                        model_calls=model_calls,
                    )
                )
                self.git.rollback_task_branch(task.id)
                if task.status != "pending" or task.retries != 0 or task.last_error:
                    task.status = "pending"
                    task.retries = 0
                    task.last_error = ""
                    self.store.update_task(task)
                    self.git.commit_state_files(
                        [self.cfg.tasks_file, self.cfg.board_file],
                        f"chore(autodev): reset {task.id} pending after quota",
                    )
                record.outcome = "deferred_quota"
                record.total_usage = task_usage
                record.duration_sec = time.monotonic() - t_start
                record.finished_at = utc_now_iso()
                record.pool_remaining_pct_after = self.pool_manager.snapshot_remaining_pcts()
                return record, attempt_pool, agent_res.quota_reset_hint_sec

            post_files = self.git.changed_files(base_ref=self.cfg.git.dev_branch)
            post_snap = self._snapshot_file_mtimes(post_files)
            agent_touched = [
                f for f in post_files if f not in pre_snap or post_snap.get(f) != pre_snap.get(f)
            ]

            gates: list[GateResult] = []
            vis_quota_hint: int | None = None
            vis_quota_pool: str | None = None
            vis_quota_hit = False

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
                g_scope = self.git.validate_agent_diff(
                    allowed_prefixes=self.cfg.scope.allowed_write_prefixes,
                    forbidden_extensions=self.cfg.scope.forbidden_agent_edit_extensions,
                    max_files=self.cfg.limits.max_changed_files,
                    max_lines=self.cfg.limits.max_diff_lines,
                    base_ref=self.cfg.git.dev_branch,
                    forbidden_check_files=agent_touched,
                )
                gates.append(g_scope)

                if g_scope.passed:
                    g_compile = self.adapter.verify_compile(task)
                    gates.append(g_compile)

                    if g_compile.passed:
                        g_behav = self.adapter.verify_behavior(task)
                        gates.append(g_behav)

                        if g_behav.passed and task.visual_check:
                            vis_pool = self.pool_manager.select_visual_pool(
                                pools_list,
                                fallback_pool=attempt_pool,
                                prefer_gemini=prefer_gemini_for_visual,
                            )
                            g_vis = self._run_visual_gate(task, run_dir, vis_pool)
                            if g_vis.model_metric is not None:
                                model_calls.append(g_vis.model_metric)
                                task_usage = task_usage.add(g_vis.model_metric.usage)
                            # If preferred visual pool hit quota while attempt_pool is still healthy, fail over visual QA in-place!
                            if (
                                g_vis.is_quota_error
                                and vis_pool != attempt_pool
                                and self.pool_manager.is_available(attempt_pool)
                            ):
                                self.pool_manager.mark_quota_exhausted(vis_pool, g_vis.quota_reset_hint_sec)
                                vis_pool = attempt_pool
                                g_vis = self._run_visual_gate(task, run_dir, vis_pool)
                                if g_vis.model_metric is not None:
                                    model_calls.append(g_vis.model_metric)
                                    task_usage = task_usage.add(g_vis.model_metric.usage)
                            gates.append(g_vis)
                            if g_vis.is_quota_error:
                                vis_quota_hit = True
                                vis_quota_pool = vis_pool
                                vis_quota_hint = g_vis.quota_reset_hint_sec

            all_passed = all(g.passed for g in gates)
            failed = [g for g in gates if not g.passed]
            att_usage = TokenUsage()
            for mc in model_calls:
                att_usage = att_usage.add(mc.usage)

            att_rec = AttemptRecord(
                attempt=attempt,
                pool=attempt_pool,
                model=profile.model,
                effort=profile.effort,
                agent_ok=agent_res.ok,
                agent_summary=agent_res.summary,
                gates=gates,
                duration_sec=time.monotonic() - a_start,
                error=failed[0].error_excerpt[:400] if failed else "",
                usage=att_usage,
                is_quota_error=vis_quota_hit,
                model_calls=model_calls,
            )
            record.attempts.append(att_rec)

            for g in gates:
                icon = "PASS" if g.passed else "FAIL"
                print(f"[AutoDev]       [{icon}] {g.name}: {g.summary}", flush=True)

            if vis_quota_hit:
                self.git.rollback_task_branch(task.id)
                if task.status != "pending" or task.retries != 0 or task.last_error:
                    task.status = "pending"
                    task.retries = 0
                    task.last_error = ""
                    self.store.update_task(task)
                    self.git.commit_state_files(
                        [self.cfg.tasks_file, self.cfg.board_file],
                        f"chore(autodev): reset {task.id} pending after visual quota",
                    )
                record.outcome = "deferred_quota"
                record.total_usage = task_usage
                record.duration_sec = time.monotonic() - t_start
                record.finished_at = utc_now_iso()
                record.pool_remaining_pct_after = self.pool_manager.snapshot_remaining_pcts()
                return record, vis_quota_pool, vis_quota_hint

            if all_passed:
                sha = self.git.commit_and_merge_task(task, detail_summary=last_agent_summary)
                task.status = "done"
                task.commit_sha = sha
                task.retries = attempt - 1
                task.last_error = ""
                self.store.update_task(task)
                record.outcome = "done"
                record.commit_sha = sha
                record.total_usage = task_usage
                record.duration_sec = time.monotonic() - t_start
                record.finished_at = utc_now_iso()
                record.pool_remaining_pct_after = self.pool_manager.snapshot_remaining_pcts()
                print(
                    f"[AutoDev] <<< TAMAMLANDI: {task.id} -> commit {sha} "
                    f"(Kalan {attempt_pool}: %{record.pool_remaining_pct_after.get(attempt_pool, '?')})",
                    flush=True,
                )
                return record, None, None

            last_failed_gates = failed
            task.retries = attempt

        print(f"[AutoDev] !!! BLOKLANDI: {task.id} — degisiklikler geri aliniyor.", flush=True)
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
        record.total_usage = task_usage
        record.duration_sec = time.monotonic() - t_start
        record.finished_at = utc_now_iso()
        record.pool_remaining_pct_after = self.pool_manager.snapshot_remaining_pcts()
        return record, None, None
