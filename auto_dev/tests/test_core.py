from __future__ import annotations

import json
import os
import subprocess
import sys
import tempfile
import unittest
from pathlib import Path

REPO_ROOT = Path(__file__).resolve().parent.parent.parent
if str(REPO_ROOT) not in sys.path:
    sys.path.insert(0, str(REPO_ROOT))

from auto_dev.adapters.generic import GenericAdapter
from auto_dev.adapters.unity import UnityAdapter
from auto_dev.core.config import AutoDevConfig
from auto_dev.core.git_ops import GitError, GitOps
from auto_dev.core.indexer import CodeIndexer
from auto_dev.core.lock import LockError, RunnerLock
from auto_dev.core.models import AgentResult, Task
from auto_dev.core.orchestrator import Orchestrator
from auto_dev.core.task_store import TaskStore


def _init_temp_git_repo(tmp: Path) -> Path:
    def git(*args: str) -> None:
        res = subprocess.run(
            ["git", *args],
            cwd=str(tmp),
            capture_output=True,
            text=True,
            check=True,
        )
        _ = res

    git("init", "-b", "main")
    git("config", "user.name", "AutoDev Test")
    git("config", "user.email", "autodev@example.com")
    (tmp / ".gitignore").write_text("/.autodev/\n__pycache__/\n", encoding="utf-8")
    (tmp / "src").mkdir(parents=True, exist_ok=True)
    (tmp / "src" / "app.py").write_text("def add(a: int, b: int) -> int:\n    return a + b\n", encoding="utf-8")
    (tmp / "auto_dev").mkdir(parents=True, exist_ok=True)
    (tmp / "auto_dev" / "config.json").write_text(
        json.dumps(
            {
                "project_name": "TempProj",
                "git": {
                    "protected_branches": ["main"],
                    "dev_branch": "auto-dev",
                    "auto_push": False,
                    "state_commit_enabled": True,
                },
                "limits": {
                    "max_tasks_per_run": 3,
                    "max_retries_per_task": 2,
                    "max_consecutive_blocks": 2,
                },
                "scope": {
                    "allowed_write_prefixes": ["src/"],
                    "forbidden_agent_edit_extensions": [".unity", ".asset"],
                    "index_include_roots": ["src"],
                    "index_extensions": [".py", ".cs"],
                },
                "adapter": {
                    "type": "generic",
                    "compile_cmd": f'"{sys.executable}" -m py_compile src/app.py',
                    "test_cmd": f'"{sys.executable}" -c "import src.app as a; assert a.add(2, 3) == 5"',
                },
            }
        ),
        encoding="utf-8",
    )
    (tmp / "auto_dev" / "tasks.json").write_text('{"schema_version": 1, "tasks": []}\n', encoding="utf-8")
    git("add", "-A")
    git("commit", "-m", "init")
    git("checkout", "-b", "auto-dev")
    return tmp


class TestAutoDevCore(unittest.TestCase):
    def test_task_store_dag_and_lifecycle(self) -> None:
        with tempfile.TemporaryDirectory() as td:
            root = Path(td)
            store = TaskStore(root / "tasks.json", root / "BOARD.md")
            t1 = store.add_task("Ilk gorev", "Aciklama 1", target_files=["src/a.py"])
            t2 = store.add_task("Ikinci gorev", "Aciklama 2", depends_on=[t1.id])

            self.assertEqual(t1.id, "T001")
            self.assertEqual(t2.id, "T002")
            self.assertEqual(store.validate_dag(store.load()), [])
            # Draft tasks are NOT ready
            self.assertIsNone(store.next_ready_task(store.load()))

            approved = store.approve(approve_all=True)
            self.assertEqual(approved, ["T001", "T002"])
            # Only T001 is ready because T002 depends on T001
            self.assertEqual(store.next_ready_task(store.load()).id, "T001")

            t1_loaded = store.load()[0]
            t1_loaded.status = "done"
            store.update_task(t1_loaded)
            self.assertEqual(store.next_ready_task(store.load()).id, "T002")
            self.assertTrue((root / "BOARD.md").exists())

            # Detect cycle
            tasks = store.load()
            tasks[0].depends_on = ["T002"]
            errs = store.validate_dag(tasks)
            self.assertTrue(any("Dongusel" in e for e in errs))

    def test_lock_prevents_overlap_and_recovers_stale(self) -> None:
        with tempfile.TemporaryDirectory() as td:
            lock_path = Path(td) / ".autodev" / "runner.lock"
            lock1 = RunnerLock(lock_path)
            lock2 = RunnerLock(lock_path)
            with lock1:
                with self.assertRaises(LockError):
                    lock2.acquire()
            self.assertFalse(lock_path.exists())

            # Write stale lock with impossible PID and ensure recovery succeeds
            lock_path.parent.mkdir(parents=True, exist_ok=True)
            lock_path.write_text(json.dumps({"pid": 99999999, "started_at": "2020-01-01"}), encoding="utf-8")
            with lock2:
                self.assertTrue(lock_path.exists())

    def test_indexer_cs_and_py(self) -> None:
        with tempfile.TemporaryDirectory() as td:
            root = Path(td)
            (root / "src").mkdir()
            (root / "src" / "Hero.cs").write_text(
                "public class Hero : MonoBehaviour {\n"
                "  [SerializeField] private float _speed;\n"
                "  public event Action OnDash;\n"
                "  public void ExecuteDash(float power) {}\n"
                "}\n",
                encoding="utf-8",
            )
            (root / "src" / "scene.unity").write_text("YAML_SECRET", encoding="utf-8")
            cfg = AutoDevConfig.load(repo_root=root)
            cfg.scope.index_include_roots = ["src"]
            cfg.scope.index_extensions = [".cs"]
            indexer = CodeIndexer(root, cfg.scope)
            out = indexer.build_index(root / "PROJECT_INDEX.md")
            self.assertIn("class Hero", out)
            self.assertIn("ExecuteDash", out)
            self.assertIn("OnDash", out)
            self.assertNotIn("scene.unity", out)

            task = Task(id="T001", title="x", description="", context_files=["src/Hero.cs", "src/scene.unity"])
            pack = indexer.build_context_pack(task, forbidden_extensions=[".unity"])
            self.assertIn("Hero.cs", pack)
            self.assertNotIn("YAML_SECRET", pack)

    def test_git_ops_scope_guard_merge_and_rollback(self) -> None:
        with tempfile.TemporaryDirectory() as td:
            root = _init_temp_git_repo(Path(td))
            cfg = AutoDevConfig.load(repo_root=root)
            git = GitOps(root, cfg.git)

            # Protected branch guard
            cfg_bad = AutoDevConfig.load(repo_root=root)
            cfg_bad.git.dev_branch = "main"
            with self.assertRaises(GitError):
                GitOps(root, cfg_bad.git).ensure_safe_dev_branch()

            # Start task branch and trigger forbidden extension violation
            git.start_task_branch("T001")
            self.assertEqual(git.current_branch(), "task/t001")
            (root / "src" / "bad.unity").write_text("corrupt", encoding="utf-8")
            (root / ".autodev").mkdir(exist_ok=True)
            (root / ".autodev" / "keep.log").write_text("runtime", encoding="utf-8")

            gate = git.validate_agent_diff(
                allowed_prefixes=cfg.scope.allowed_write_prefixes,
                forbidden_extensions=cfg.scope.forbidden_agent_edit_extensions,
                max_files=10,
                max_lines=500,
                base_ref="auto-dev",
            )
            self.assertFalse(gate.passed)

            # Rollback must delete bad.unity & task branch while keeping ignored .autodev/keep.log
            git.rollback_task_branch("T001")
            self.assertEqual(git.current_branch(), "auto-dev")
            self.assertFalse((root / "src" / "bad.unity").exists())
            self.assertTrue((root / ".autodev" / "keep.log").exists())
            self.assertFalse(git.branch_exists("task/t001"))

            # Successful task branch merge
            git.start_task_branch("T002")
            (root / "src" / "app.py").write_text(
                "def add(a: int, b: int) -> int:\n    return a + b\n\ndef mul(a: int, b: int) -> int:\n    return a * b\n",
                encoding="utf-8",
            )
            sha = git.commit_and_merge_task(Task(id="T002", title="Add mul", description=""))
            self.assertTrue(len(sha) >= 6)
            self.assertEqual(git.current_branch(), "auto-dev")
            self.assertFalse(git.branch_exists("task/t002"))
            self.assertTrue(git.is_clean())

    def test_orchestrator_escalation_and_block_rollback(self) -> None:
        with tempfile.TemporaryDirectory() as td:
            root = _init_temp_git_repo(Path(td))
            cfg = AutoDevConfig.load(repo_root=root)
            store = TaskStore(cfg.tasks_file, cfg.board_file)
            store.add_task(
                title="Multiply helper",
                description="Add mul() cleanly",
                acceptance_criteria=["add(2,3)==5"],
                target_files=["src/app.py"],
                status="pending",
            )
            store.add_task(
                title="Always failing task",
                description="Will fail both attempts",
                target_files=["src/app.py"],
                status="pending",
            )
            # Intentionally do NOT commit tasks.json / BOARD.md to verify auto state-sync on dev_branch!

            adapter = GenericAdapter(cfg)
            orch = Orchestrator(cfg, adapter)

            calls: list[tuple[str, str]] = []

            def fake_run_prompt(prompt, profile, schema=None, tag="task", run_dir=None, read_only=False):
                calls.append((tag, profile.model))
                app_py = root / "src" / "app.py"
                if tag.startswith("T001_") and tag.endswith("_a1"):
                    # Attempt 1 (Flash) introduces a bug -> fails test -> triggers escalation!
                    app_py.write_text("def add(a: int, b: int) -> int:\n    return a - b\n", encoding="utf-8")
                elif tag.startswith("T001_") and tag.endswith("_a2"):
                    # Attempt 2 (Smart escalation) fixes it!
                    app_py.write_text(
                        "def add(a: int, b: int) -> int:\n    return a + b\n\ndef mul(a: int, b: int) -> int:\n    return a * b\n",
                        encoding="utf-8",
                    )
                else:
                    # T002 fails both attempts
                    app_py.write_text("def add(a: int, b: int) -> int:\n    return 0\n", encoding="utf-8")
                return AgentResult(
                    ok=True,
                    model=profile.model,
                    effort=profile.effort,
                    summary=f"Edited for {tag}",
                    changed_files=["src/app.py"],
                )

            orch.runner.list_available_models = lambda force_refresh=False: []  # type: ignore[method-assign]
            orch.runner.run_prompt = fake_run_prompt  # type: ignore[method-assign]
            res = orch.run(max_tasks=3, keep_awake=False)

            # Verify escalation occurred: T001_a1 used Flash, T001_a2 used Hydrogen
            self.assertEqual(calls[0], ("T001_gemini_a1", "gemini-3.8-flash-medium"))
            self.assertEqual(calls[1], ("T001_gemini_a2", "gemini-4-hydrogen-medium"))
            self.assertEqual(calls[2], ("T002_gemini_a1", "gemini-3.8-flash-medium"))
            self.assertEqual(calls[3], ("T002_gemini_a2", "gemini-4-hydrogen-medium"))

            final_tasks = {t.id: t for t in store.load()}
            self.assertEqual(final_tasks["T001"].status, "done")
            self.assertTrue(bool(final_tasks["T001"].commit_sha))
            self.assertEqual(final_tasks["T002"].status, "blocked")

            # Ensure T002 broken code was rolled back and T001 good code remains intact!
            final_code = (root / "src" / "app.py").read_text(encoding="utf-8")
            self.assertIn("def mul", final_code)
            self.assertNotIn("return 0", final_code)
            self.assertTrue((root / "auto_dev" / "RUN_REPORT.md").exists())
            self.assertEqual(res["stop_reason"], "queue_drained")

    def test_unity_adapter_reads_utf8_bom_result(self) -> None:
        with tempfile.TemporaryDirectory() as td:
            root = Path(td)
            cfg = AutoDevConfig.load(repo_root=root)
            cfg.adapter = {"type": "unity", "bridge_timeout_sec": 3}
            adapter = UnityAdapter(cfg)
            cfg.runtime_dir.mkdir(parents=True, exist_ok=True)
            payload = json.dumps({"id": "abc123", "passed": True, "summary": "OK", "details": ""})
            # Write with UTF-8 BOM to guarantee utf-8-sig decoding works
            adapter.result_file.write_bytes(b"\xef\xbb\xbf" + payload.encode("utf-8"))
            passed, data = adapter._wait_live_editor("abc123")
            self.assertTrue(passed)
            self.assertEqual(data["summary"], "OK")

    def test_agent_runner_omits_conflicting_effort_for_suffixed_model(self) -> None:
        from unittest.mock import patch
        from auto_dev.core.agent_runner import AgentRunner
        from auto_dev.core.config import AgentCliConfig, ModelProfile

        with tempfile.TemporaryDirectory() as td:
            root = Path(td)
            runner = AgentRunner(root, AgentCliConfig(executable="agy"), root / ".autodev")
            captured_cmd: list[str] = []

            class DummyProc:
                returncode = 0
                stdout = '{"summary": "ok", "changed_files": []}'
                stderr = ""

            def fake_run(cmd, **kwargs):
                captured_cmd.extend(cmd)
                return DummyProc()

            with patch("subprocess.run", side_effect=fake_run):
                res = runner.run_prompt(
                    "hi",
                    ModelProfile("gemini-4-neon-xhigh", "medium", 60),
                    tag="effort_test",
                )
            self.assertTrue(res.ok)
            self.assertNotIn("--effort", captured_cmd)

    def test_quota_error_detection_and_reset_hint(self) -> None:
        from auto_dev.core.agent_runner import detect_quota_error

        is_q, hint = detect_quota_error("RESOURCE_EXHAUSTED: Five Hour Limit reached. Refreshes in 3h 43m")
        self.assertTrue(is_q)
        self.assertEqual(hint, 3 * 3600 + 43 * 60)

        is_q2, hint2 = detect_quota_error("HTTP 429 Too Many Requests")
        self.assertTrue(is_q2)
        self.assertIsNone(hint2)

        is_q3, _ = detect_quota_error("CS0103: The name 'foo' does not exist in the current context")
        self.assertFalse(is_q3)

    def test_dual_pool_failover_clean_rollback_and_brief(self) -> None:
        from auto_dev.core.models import TokenUsage

        with tempfile.TemporaryDirectory() as td:
            root = _init_temp_git_repo(Path(td))
            cfg = AutoDevConfig.load(repo_root=root)
            store = TaskStore(cfg.tasks_file, cfg.board_file)
            store.add_task(
                title="Quota failover task",
                description="Add sub() cleanly",
                target_files=["src/app.py"],
                status="pending",
            )

            orch = Orchestrator(cfg, GenericAdapter(cfg))
            seen: list[tuple[str, str]] = []

            def fake_run_prompt(prompt, profile, schema=None, tag="task", run_dir=None, read_only=False):
                seen.append((tag, profile.model))
                app_py = root / "src" / "app.py"
                if "gemini" in profile.model:
                    # Simulate half-edited broken file before quota exhaustion
                    app_py.write_text("BROKEN_PARTIAL_SYNTAX(((", encoding="utf-8")
                    return AgentResult(
                        ok=False,
                        model=profile.model,
                        effort=profile.effort,
                        summary="",
                        duration_sec=12.5,
                        exit_code=1,
                        error="RESOURCE_EXHAUSTED: Five Hour Limit reached. Refreshes in 2h 10m",
                        usage=TokenUsage(input_tokens=40000, output_tokens=5000, total_tokens=45000),
                        is_quota_error=True,
                        quota_reset_hint_sec=2 * 3600 + 10 * 60,
                    )
                # Claude pool succeeds cleanly after rollback!
                self.assertNotIn("BROKEN_PARTIAL_SYNTAX", app_py.read_text(encoding="utf-8"))
                app_py.write_text(
                    "def add(a: int, b: int) -> int:\n    return a + b\n\ndef sub(a: int, b: int) -> int:\n    return a - b\n",
                    encoding="utf-8",
                )
                return AgentResult(
                    ok=True,
                    model=profile.model,
                    effort=profile.effort,
                    summary="Added sub() on Claude pool",
                    changed_files=["src/app.py"],
                    duration_sec=28.0,
                    usage=TokenUsage(input_tokens=60000, output_tokens=8000, total_tokens=68000),
                )

            orch.runner.list_available_models = lambda force_refresh=False: []  # type: ignore[method-assign]
            orch.runner.run_prompt = fake_run_prompt  # type: ignore[method-assign]
            res = orch.run(
                max_tasks=3,
                pool_name="gemini",
                shift_name="Shift1_Gemini",
                failover_on_quota=True,
                keep_awake=False,
            )

            # First attempt ran on Gemini (quota hit -> rolled back), second ran on Claude and passed!
            self.assertEqual(seen[0][1], "gemini-3.8-flash-medium")
            self.assertEqual(seen[1][1], "claude-sonnet-4-6")
            final_tasks = {t.id: t for t in store.load()}
            self.assertEqual(final_tasks["T001"].status, "done")
            self.assertEqual(orch.pool_manager.get_remaining_pct("gemini"), 0.0)
            self.assertLess(orch.pool_manager.get_remaining_pct("claude"), 100.0)

            brief = (root / "auto_dev" / "MORNING_BRIEF.md").read_text(encoding="utf-8")
            self.assertIn("Shift1_Gemini", brief)
            self.assertIn("claude-sonnet-4-6", brief)
            self.assertIn("Kalan Kota (%)", brief)
            self.assertEqual(res["stop_reason"], "queue_drained")

    def test_all_pools_exhausted_keeps_task_pending(self) -> None:
        with tempfile.TemporaryDirectory() as td:
            root = _init_temp_git_repo(Path(td))
            cfg = AutoDevConfig.load(repo_root=root)
            store = TaskStore(cfg.tasks_file, cfg.board_file)
            store.add_task(title="Wait for renewal", description="x", target_files=["src/app.py"], status="pending")

            orch = Orchestrator(cfg, GenericAdapter(cfg))

            def always_quota(prompt, profile, schema=None, tag="task", run_dir=None, read_only=False):
                return AgentResult(
                    ok=False,
                    model=profile.model,
                    effort=profile.effort,
                    summary="",
                    duration_sec=3.0,
                    exit_code=1,
                    error="429 quota exceeded",
                    is_quota_error=True,
                    quota_reset_hint_sec=3600,
                )

            orch.runner.list_available_models = lambda force_refresh=False: []  # type: ignore[method-assign]
            orch.runner.run_prompt = always_quota  # type: ignore[method-assign]
            res = orch.run(max_tasks=0, pool_name="gemini", failover_on_quota=True, keep_awake=False)
            self.assertEqual(res["stop_reason"], "all_pools_exhausted")
            self.assertIsNotNone(res["next_available_at"])
            t = store.load()[0]
            self.assertEqual(t.status, "pending")
            self.assertEqual(t.retries, 0)

    def test_auto_promote_drafts_and_resolve_midnight_shifts(self) -> None:
        from datetime import datetime, timezone
        from auto_dev.night_runner import parse_shift_specs, resolve_shift_schedule

        specs = parse_shift_specs("22:00-03:00:gemini,03:00-08:00:claude")
        tz = timezone.utc

        # Case 1: 20:30 -> both shifts upcoming starting tonight 22:00
        s1 = resolve_shift_schedule(specs, now=datetime(2026, 11, 26, 20, 30, tzinfo=tz))
        self.assertEqual(len(s1), 2)
        self.assertEqual((s1[0].start_dt.day, s1[0].start_dt.hour, s1[0].end_dt.day, s1[0].end_dt.hour), (26, 22, 27, 3))
        self.assertEqual((s1[1].start_dt.day, s1[1].start_dt.hour, s1[1].end_dt.day, s1[1].end_dt.hour), (27, 3, 27, 8))

        # Case 2: 23:15 -> currently inside Shift 1, followed by Shift 2
        s2 = resolve_shift_schedule(specs, now=datetime(2026, 11, 26, 23, 15, tzinfo=tz))
        self.assertEqual(len(s2), 2)
        self.assertEqual(s2[0].pool, "gemini")
        self.assertEqual(s2[0].end_dt.hour, 3)

        # Case 3: 03:20 AM -> Shift 1 finished, currently inside Shift 2 (03:00-08:00 today)
        s3 = resolve_shift_schedule(specs, now=datetime(2026, 11, 27, 3, 20, tzinfo=tz))
        self.assertEqual(len(s3), 1)
        self.assertEqual(s3[0].pool, "claude")
        self.assertEqual((s3[0].start_dt.day, s3[0].start_dt.hour, s3[0].end_dt.hour), (27, 3, 8))

        # Auto-promote drafts test
        with tempfile.TemporaryDirectory() as td:
            root = _init_temp_git_repo(Path(td))
            cfg = AutoDevConfig.load(repo_root=root)
            store = TaskStore(cfg.tasks_file, cfg.board_file)
            store.add_task(title="Draft 1", description="d1", status="draft")
            store.add_task(title="Draft 2", description="d2", depends_on=["T001"], status="draft")
            orch = Orchestrator(cfg, GenericAdapter(cfg))
            orch._sync_pending_state_files()
            promoted = orch._try_promote_drafts(batch_size=2)
            self.assertEqual(promoted, ["T001", "T002"])
            self.assertEqual([t.status for t in store.load()], ["pending", "pending"])

    def test_balanced_pool_warm_start_and_proportional_selection(self) -> None:
        from auto_dev.core.config import AutoDevConfig
        from auto_dev.core.pool_manager import PoolManager

        with tempfile.TemporaryDirectory() as td:
            root = Path(td)
            cfg = AutoDevConfig.load(repo_root=root)
            pm = PoolManager(
                pools=cfg.model_pools,
                state_file=root / "pool_state.json",
                pool_budgets={"gemini": 2_500_000, "claude": 550_000},
            )
            counts = {"gemini": 0, "claude": 0}

            # Task 1: both at 100%, fast affinity selects gemini
            p1, _ = pm.select_balanced_pool(["gemini", "claude"], task_tier="fast", shift_pool_counts=counts)
            self.assertEqual(p1, "gemini")
            pm.record_usage("gemini", 150_000)  # gemini -> 94.0%
            counts["gemini"] += 1

            # Task 2: warm-start ignites claude so both 5h rolling timers start together!
            p2, _ = pm.select_balanced_pool(["gemini", "claude"], task_tier="fast", shift_pool_counts=counts)
            self.assertEqual(p2, "claude")
            pm.record_usage("claude", 150_000)  # claude -> 72.7%
            counts["claude"] += 1

            # Subsequent tasks pick highest remaining % (~4 Gemini : 1 Claude ratio)
            picks: list[str] = []
            for _ in range(5):
                p, _ = pm.select_balanced_pool(["gemini", "claude"], task_tier="fast", shift_pool_counts=counts)
                assert p is not None
                picks.append(p)
                pm.record_usage(p, 150_000)
                counts[p] += 1

            # First 4 picks are gemini (94->88->82->76->70%), then 5th switches back to claude (72.7% > 70% + 1.5%)
            self.assertEqual(picks[:4], ["gemini", "gemini", "gemini", "gemini"])
            self.assertEqual(picks[4], "claude")

    def test_cross_pool_escalation_and_cheap_visual_pool(self) -> None:
        from auto_dev.core.models import GateResult, TokenUsage

        with tempfile.TemporaryDirectory() as td:
            root = _init_temp_git_repo(Path(td))
            cfg = AutoDevConfig.load(repo_root=root)
            store = TaskStore(cfg.tasks_file, cfg.board_file)
            store.add_task(
                title="Cross-pool fix and cheap visual QA",
                description="Add mul() with UI check",
                target_files=["src/app.py"],
                visual_check=True,
                status="pending",
            )

            adapter = GenericAdapter(cfg)
            fake_png = root / ".autodev" / "screen.png"
            fake_png.parent.mkdir(parents=True, exist_ok=True)
            fake_png.write_bytes(b"PNG")
            adapter.capture_visual = lambda task, run_dir: GateResult(  # type: ignore[method-assign]
                name="visual_capture", passed=True, summary="ok", artifacts=[str(fake_png)]
            )

            orch = Orchestrator(cfg, adapter)
            calls: list[tuple[str, str]] = []

            def fake_run_prompt(prompt, profile, schema=None, tag="task", run_dir=None, read_only=False):
                calls.append((tag, profile.model))
                app_py = root / "src" / "app.py"
                if tag == "T001_visual":
                    return AgentResult(
                        ok=True,
                        model=profile.model,
                        effort=profile.effort,
                        summary="Visual OK",
                        structured={"passed": True, "summary": "Theme matches", "issues": []},
                        usage=TokenUsage(total_tokens=17_000),
                    )
                if tag.endswith("_a1"):
                    # Attempt 1 on Gemini introduces a test failure
                    app_py.write_text("def add(a: int, b: int) -> int:\n    return a - b\n", encoding="utf-8")
                else:
                    # Attempt 2 cross-pool repair on Claude fixes it!
                    app_py.write_text(
                        "def add(a: int, b: int) -> int:\n    return a + b\n\ndef mul(a: int, b: int) -> int:\n    return a * b\n",
                        encoding="utf-8",
                    )
                return AgentResult(
                    ok=True,
                    model=profile.model,
                    effort=profile.effort,
                    summary=f"Edited in {tag}",
                    changed_files=["src/app.py"],
                    usage=TokenUsage(total_tokens=40_000),
                )

            orch.runner.list_available_models = lambda force_refresh=False: []  # type: ignore[method-assign]
            orch.runner.run_prompt = fake_run_prompt  # type: ignore[method-assign]
            res = orch.run(
                max_tasks=2,
                pool_name="gemini",
                pool_strategy="balanced",
                active_pools=["gemini", "claude"],
                cross_pool_escalation=True,
                prefer_gemini_for_visual=True,
                keep_awake=False,
            )

            self.assertEqual(
                calls,
                [
                    ("T001_gemini_a1", "gemini-3.8-flash-medium"),
                    ("T001_claude_a2", "claude-sonnet-4-6"),
                    ("T001_visual", "gemini-3.8-flash-low"),
                ],
            )
            self.assertEqual(store.load()[0].status, "done")
            self.assertEqual(res["stop_reason"], "queue_drained")

    def test_diff_line_count_excludes_unity_scene_and_meta(self) -> None:
        with tempfile.TemporaryDirectory() as td:
            root = _init_temp_git_repo(Path(td))
            cfg = AutoDevConfig.load(repo_root=root)
            git = GitOps(root, cfg.git)

            git.start_task_branch("T001")
            # Agent edits small source file, while Unity Editor writes huge .unity YAML + .meta file
            (root / "src" / "app.py").write_text(
                "def add(a: int, b: int) -> int:\n    return a + b\n\ndef sub(a: int, b: int) -> int:\n    return a - b\n",
                encoding="utf-8",
            )
            (root / "src" / "Demo.unity").write_text("line\n" * 3000, encoding="utf-8")
            (root / "src" / "app.py.meta").write_text("meta\n" * 80, encoding="utf-8")

            raw_lines = git.diff_line_count(base_ref="auto-dev")
            code_lines = git.diff_line_count(base_ref="auto-dev", exclude_extensions={".unity", ".asset", ".meta"})
            self.assertGreater(raw_lines, 3000)
            self.assertLess(code_lines, 20)

            gate = git.validate_agent_diff(
                allowed_prefixes=["src/"],
                forbidden_extensions=[".unity", ".asset"],
                max_files=10,
                max_lines=200,
                base_ref="auto-dev",
                forbidden_check_files=["src/app.py"],
            )
            self.assertTrue(gate.passed, gate.error_excerpt)

    def test_parse_shift_specs_balanced_and_single(self) -> None:
        from auto_dev.night_runner import parse_shift_specs

        s_bal = parse_shift_specs("00:00-05:15:balanced,05:15-10:15:balanced")
        self.assertEqual(len(s_bal), 2)
        self.assertEqual((s_bal[0].mode, s_bal[0].pools), ("balanced", ["gemini", "claude"]))
        self.assertEqual((s_bal[1].mode, s_bal[1].pools), ("balanced", ["gemini", "claude"]))

        s_plus = parse_shift_specs("00:00-05:15:gemini+claude")
        self.assertEqual((s_plus[0].mode, s_plus[0].pools), ("balanced", ["gemini", "claude"]))

        s_omit = parse_shift_specs("00:00-05:15,05:15-10:15")
        self.assertEqual((s_omit[0].mode, s_omit[0].pools), ("balanced", ["gemini", "claude"]))

        s_single = parse_shift_specs("22:00-03:00:gemini,03:00-08:00:claude")
        self.assertEqual((s_single[0].mode, s_single[0].pools), ("single", ["gemini"]))
        self.assertEqual((s_single[1].mode, s_single[1].pools), ("single", ["claude"]))

    def test_startup_model_resolution_remaps_obsolete_models(self) -> None:
        from auto_dev.core.agent_runner import detect_model_unavailable_error, parse_agy_models_output
        from auto_dev.core.config import ModelProfile
        from auto_dev.core.pool_manager import PoolManager

        raw = (
            "Fetching available models...\n"
            "gemini-4-hydrogen-high\tGemini 4 Hydrogen (High)\n"
            "gemini-4-hydrogen-xhigh\tGemini 4 Hydrogen (XHigh)\n"
            "gemini-4-hydrogen-medium\tGemini 4 Hydrogen (Medium)\n"
            "gemini-4-hydrogen-low\tGemini 4 Hydrogen (Low)\n"
            "gemini-3.8-flash-high\tGemini 3.8 Flash (High)\n"
            "gemini-3.8-flash-medium\tGemini 3.8 Flash (Medium)\n"
            "gemini-3.8-flash-low\tGemini 3.8 Flash (Low)\n"
            "claude-sonnet-4-6\tClaude Sonnet 4.6 (Thinking)\n"
            "claude-opus-4-6-thinking\tClaude Opus 4.6 (Thinking)\n"
            "gpt-oss-120b-medium\tGPT-OSS 120B (Medium)\n"
        )
        avail = parse_agy_models_output(raw)
        self.assertIn("gemini-4-hydrogen-xhigh", avail)
        self.assertIn("claude-opus-4-6-thinking", avail)
        self.assertTrue(
            detect_model_unavailable_error(
                'invalid model selection (--model "gemini-4-neon-xhigh"): model gemini-4-neon-xhigh is not recognized as a known model'
            )
        )

        with tempfile.TemporaryDirectory() as td:
            pools = {
                "gemini": {
                    "planner": ModelProfile("gemini-4-neon-medium", "medium", 300),
                    "fast": ModelProfile("gemini-3.8-flash-medium", "medium", 420),
                    "smart": ModelProfile("gemini-4-neon-xhigh", "xhigh", 420),
                    "visual": ModelProfile("gemini-3.8-flash-low", "low", 90),
                },
                "claude": {
                    "planner": ModelProfile("claude-opus-5-5-medium", "medium", 360),
                    "fast": ModelProfile("claude-sonnet-5-5-low", "low", 300),
                    "smart": ModelProfile("claude-sonnet-5-5-medium", "medium", 420),
                    "visual": ModelProfile("claude-sonnet-5-5-low", "low", 90),
                },
            }
            pm = PoolManager(pools=pools, state_file=Path(td) / "pool_state.json")
            remapped = pm.set_available_models(avail)
            self.assertTrue(len(remapped) >= 5)
            self.assertEqual(pm.get_profile("gemini", "smart").model, "gemini-4-hydrogen-xhigh")
            self.assertEqual(pm.get_profile("gemini", "planner").model, "gemini-4-hydrogen-medium")
            self.assertEqual(pm.get_profile("gemini", "fast").model, "gemini-3.8-flash-medium")
            self.assertEqual(pm.get_profile("claude", "planner").model, "claude-opus-4-6-thinking")
            self.assertEqual(pm.get_profile("claude", "fast").model, "claude-sonnet-4-6")

    def test_runtime_unavailable_model_falls_back_within_and_across_pools(self) -> None:
        from auto_dev.core.config import ModelProfile
        from auto_dev.core.models import TokenUsage

        with tempfile.TemporaryDirectory() as td:
            root = _init_temp_git_repo(Path(td))
            cfg = AutoDevConfig.load(repo_root=root)
            cfg.model_pools["gemini"]["smart"] = ModelProfile(
                "gemini-4-neon-xhigh",
                "xhigh",
                420,
                fallback_models=["gemini-4-hydrogen-high", "gemini-3.8-flash-high"],
            )
            store = TaskStore(cfg.tasks_file, cfg.board_file)
            store.add_task(
                title="Smart task with obsolete primary model",
                description="Add sub() cleanly",
                target_files=["src/app.py"],
                tier="smart",
                status="pending",
            )
            store.add_task(
                title="Fast task when entire Gemini pool models fail",
                description="Add div() safely",
                target_files=["src/app.py"],
                tier="fast",
                status="pending",
            )

            orch = Orchestrator(cfg, GenericAdapter(cfg))
            orch.runner.list_available_models = lambda force_refresh=False: []  # type: ignore[method-assign]
            calls: list[tuple[str, str]] = []

            def fake_run_prompt(prompt, profile, schema=None, tag="task", run_dir=None, read_only=False):
                calls.append((tag, profile.model))
                # T001: only obsolete neon-xhigh fails -> intra-pool fallback to gemini-4-hydrogen-high
                if tag.startswith("T001"):
                    if profile.model == "gemini-4-neon-xhigh":
                        return AgentResult(
                            ok=False,
                            model=profile.model,
                            effort=profile.effort,
                            summary="",
                            exit_code=1,
                            error="invalid model selection: model gemini-4-neon-xhigh is not recognized as a known model",
                            is_model_unavailable=True,
                        )
                    (root / "src" / "app.py").write_text(
                        "def add(a: int, b: int) -> int:\n    return a + b\n\ndef sub(a: int, b: int) -> int:\n    return a - b\n",
                        encoding="utf-8",
                    )
                    return AgentResult(
                        ok=True,
                        model=profile.model,
                        effort=profile.effort,
                        summary="T001 done via intra-pool fallback",
                        changed_files=["src/app.py"],
                        usage=TokenUsage(total_tokens=22_000),
                    )

                # T002: ALL gemini models report unavailable -> must cross over to claude pool!
                if profile.model.startswith("gemini"):
                    return AgentResult(
                        ok=False,
                        model=profile.model,
                        effort=profile.effort,
                        summary="",
                        exit_code=1,
                        error=f"model {profile.model} is not available right now",
                        is_model_unavailable=True,
                    )
                (root / "src" / "app.py").write_text(
                    "def add(a: int, b: int) -> int:\n    return a + b\n\n"
                    "def sub(a: int, b: int) -> int:\n    return a - b\n\n"
                    "def div(a: int, b: int) -> float:\n    return a / b\n",
                    encoding="utf-8",
                )
                return AgentResult(
                    ok=True,
                    model=profile.model,
                    effort=profile.effort,
                    summary="T002 done via cross-pool fallback",
                    changed_files=["src/app.py"],
                    usage=TokenUsage(total_tokens=20_000),
                )

            orch.runner.run_prompt = fake_run_prompt  # type: ignore[method-assign]
            res = orch.run(
                max_tasks=4,
                pool_name="gemini",
                pool_strategy="single",
                active_pools=["gemini", "claude"],
                keep_awake=False,
            )

            t1_models = [m for t, m in calls if t.startswith("T001")]
            t2_models = [m for t, m in calls if t.startswith("T002")]
            self.assertEqual(t1_models, ["gemini-4-neon-xhigh", "gemini-4-hydrogen-high"])
            self.assertTrue(any(m.startswith("gemini") for m in t2_models))
            self.assertEqual(t2_models[-1], "claude-sonnet-4-6")
            final_tasks = {t.id: t for t in store.load()}
            self.assertEqual(final_tasks["T001"].status, "done")
            self.assertEqual(final_tasks["T002"].status, "done")
            self.assertEqual((final_tasks["T001"].retries, final_tasks["T002"].retries), (0, 0))
            self.assertEqual(res["stop_reason"], "queue_drained")


if __name__ == "__main__":
    unittest.main()
