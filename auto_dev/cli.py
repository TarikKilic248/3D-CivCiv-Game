from __future__ import annotations

import argparse
import json
import sys
from pathlib import Path

# Allow running directly via `python auto_dev/cli.py ...`
REPO_ROOT = Path(__file__).resolve().parent.parent
if str(REPO_ROOT) not in sys.path:
    sys.path.insert(0, str(REPO_ROOT))

from auto_dev.adapters import load_adapter
from auto_dev.core.agent_runner import AgentRunner
from auto_dev.core.config import AutoDevConfig
from auto_dev.core.git_ops import GitOps
from auto_dev.core.indexer import CodeIndexer
from auto_dev.core.orchestrator import Orchestrator
from auto_dev.core.planner import TaskPlanner
from auto_dev.core.task_store import TaskStore


def _cmd_status(cfg: AutoDevConfig, args: argparse.Namespace) -> int:
    store = TaskStore(cfg.tasks_file, cfg.board_file)
    git = GitOps(cfg.repo_root, cfg.git)
    tasks = store.load()
    store.render_board(tasks)

    counts = {st: sum(1 for t in tasks if t.status == st) for st in ("draft", "pending", "done", "blocked")}
    ready = store.ready_tasks(tasks)

    print(f"Proje       : {cfg.project_name}")
    print(f"Aktif Dal   : {git.current_branch()} (Hedef dev dali: {cfg.git.dev_branch})")
    print(f"Calisma Alani: {'Temiz' if git.is_clean() else 'Kaydedilmemis degisiklik var'}")
    print(f"Adaptor     : {cfg.adapter.get('type', 'generic')}")
    print(
        f"Gorevler    : Toplam={len(tasks)} | Taslak={counts['draft']} | "
        f"Sirada={counts['pending']} (Hazir={len(ready)}) | "
        f"Bitti={counts['done']} | Bloklu={counts['blocked']}"
    )
    if ready:
        print("\nCalismaya hazir ilk gorevler:")
        for t in ready[:5]:
            print(f"  - {t.id} [{t.tier}{' +visual' if t.visual_check else ''}]: {t.title}")
    drafts = [t for t in tasks if t.status == "draft"]
    if drafts:
        print("\nOnay bekleyen taslaklar (`approve --all` veya `approve <ID>`):")
        for t in drafts[:10]:
            print(f"  - {t.id}: {t.title}")
    blocked = [t for t in tasks if t.status == "blocked"]
    if blocked:
        print("\nBloklanan gorevler:")
        for t in blocked:
            first_err = t.last_error.splitlines()[0] if t.last_error else ""
            print(f"  - {t.id}: {t.title} -> {first_err[:100]}")
    return 0


def _cmd_index(cfg: AutoDevConfig, args: argparse.Namespace) -> int:
    indexer = CodeIndexer(cfg.repo_root, cfg.scope, cfg.limits.max_index_chars)
    store = TaskStore(cfg.tasks_file, cfg.board_file)
    content = indexer.build_index(cfg.index_file)
    store.render_board(store.load())
    print(f"Indeks guncellendi: {cfg.index_file} ({len(content)} karakter, 0 token)")
    return 0


def _cmd_plan(cfg: AutoDevConfig, args: argparse.Namespace) -> int:
    adapter = load_adapter(cfg)
    store = TaskStore(cfg.tasks_file, cfg.board_file)
    indexer = CodeIndexer(cfg.repo_root, cfg.scope, cfg.limits.max_index_chars)
    runner = AgentRunner(cfg.repo_root, cfg.agent_cli, cfg.runtime_dir)
    planner = TaskPlanner(cfg, store, runner, indexer, adapter)

    print(f"[AutoDev] Planlayici baslatiliyor (Model: {cfg.models['planner'].model})...")
    created = planner.plan_goal(args.goal, max_tasks=args.max_tasks)
    print(f"\n[AutoDev] {len(created)} yeni taslak gorev olusturuldu (`draft`):")
    for t in created:
        deps = f" (bagimli: {', '.join(t.depends_on)})" if t.depends_on else ""
        print(f"  - {t.id} [{t.tier}]: {t.title}{deps}")
    print("\nIncelemek icin: auto_dev/BOARD.md veya auto_dev/tasks.json")
    print("Onaylamak icin: python auto_dev/cli.py approve --all")
    return 0


def _cmd_add(cfg: AutoDevConfig, args: argparse.Namespace) -> int:
    store = TaskStore(cfg.tasks_file, cfg.board_file)
    task = store.add_task(
        title=args.title,
        description=args.desc or args.title,
        acceptance_criteria=args.criteria or [],
        depends_on=args.depends or [],
        target_files=args.target or [],
        context_files=args.context or [],
        tier=args.tier,
        visual_check=args.visual,
        verify_check=args.verify_check or "",
        status="pending" if args.approve else "draft",
    )
    print(f"Gorev eklendi: {task.id} [{task.status}] — {task.title}")
    return 0


def _cmd_approve(cfg: AutoDevConfig, args: argparse.Namespace) -> int:
    store = TaskStore(cfg.tasks_file, cfg.board_file)
    if not args.all and not args.ids:
        print("Hata: --all veya en az bir gorev ID'si belirtin (or. T001).")
        return 1
    changed = store.approve(task_ids=args.ids, approve_all=args.all)
    if not changed:
        print("Onaylanacak taslak gorev bulunamadi.")
        return 0
    print(f"Onaylandi (pending): {', '.join(changed)}")
    return 0


def _cmd_unblock(cfg: AutoDevConfig, args: argparse.Namespace) -> int:
    store = TaskStore(cfg.tasks_file, cfg.board_file)
    if not args.all and not args.ids:
        print("Hata: --all veya en az bir gorev ID'si belirtin.")
        return 1
    changed = store.unblock(task_ids=args.ids, unblock_all=args.all)
    if not changed:
        print("Bloklu gorev bulunamadi.")
        return 0
    print(f"Blok kaldirildi (pending): {', '.join(changed)}")
    return 0


def _cmd_run(cfg: AutoDevConfig, args: argparse.Namespace) -> int:
    from datetime import datetime, timedelta

    adapter = load_adapter(cfg)
    orch = Orchestrator(cfg, adapter)
    deadline = None
    if args.until:
        parts = args.until.strip().split(":")
        h, m = int(parts[0]), int(parts[1])
        now = datetime.now().astimezone()
        deadline = now.replace(hour=h, minute=m, second=0, microsecond=0)
        if deadline <= now:
            deadline += timedelta(days=1)

    result = orch.run(
        max_tasks=args.max_tasks,
        dry_run=args.dry_run,
        pool_name=args.pool or None,
        deadline=deadline,
    )
    if args.dry_run:
        ready = result.get("ready_tasks", [])
        print(f"[DRY-RUN] Havuz: {result.get('pool', 'gemini')} | Calistirilacak {len(ready)} hazir gorev:")
        for t in ready:
            print(f"  - {t['id']} [{t['tier']}]: {t['title']}")
        return 0

    print("\n=== AutoDev Kosu Ozeti ===")
    print(f"Kosu ID     : {result['run_id']}")
    print(f"Bitis Nedeni: {result['stop_reason']}")
    print(f"Rapor       : {result['report_path']}")
    print(f"Sabah Ozeti : {result.get('brief_path', '')}")
    for r in result.get("records", []):
        sha = f" ({r['commit_sha']})" if r.get("commit_sha") else ""
        print(f"  - {r['task_id']}: {r['outcome'].upper()}{sha} [{r['duration_sec']}s]")
    return 0


def _cmd_night(cfg: AutoDevConfig, args: argparse.Namespace) -> int:
    from auto_dev.night_runner import parse_shift_specs, run_night_session

    specs = parse_shift_specs(args.shifts) if args.shifts else None
    return run_night_session(
        cfg=cfg,
        shifts_spec=specs,
        start_now=args.start_now,
        until_hhmm=args.until or None,
        initial_pool=args.pool or None,
        max_tasks_per_shift=args.max_tasks_per_shift,
        no_failover=args.no_failover,
        no_auto_promote=args.no_auto_promote,
        no_auto_plan=args.no_auto_plan,
        gemini_pct=args.gemini_pct,
        gemini_refresh_min=args.gemini_refresh_min,
        claude_pct=args.claude_pct,
        claude_refresh_min=args.claude_refresh_min,
        dry_run=args.dry_run,
    )


def _cmd_verify(cfg: AutoDevConfig, args: argparse.Namespace) -> int:
    from auto_dev.core.models import Task

    adapter = load_adapter(cfg)
    dummy = Task(id="VERIFY", title="Manual Verify", description="", verify_check=args.check)
    g_comp = adapter.verify_compile(dummy)
    print(f"[{'PASS' if g_comp.passed else 'FAIL'}] {g_comp.name}: {g_comp.summary}")
    if not g_comp.passed:
        if g_comp.error_excerpt:
            print(g_comp.error_excerpt)
        return 1

    g_beh = adapter.verify_behavior(dummy)
    print(f"[{'PASS' if g_beh.passed else 'FAIL'}] {g_beh.name}: {g_beh.summary}")
    if not g_beh.passed:
        if g_beh.error_excerpt:
            print(g_beh.error_excerpt)
        return 1
    return 0


def _cmd_init_generic(cfg: AutoDevConfig, args: argparse.Namespace) -> int:
    template = {
        "project_name": "MyProject",
        "git": {
            "protected_branches": ["main", "master"],
            "dev_branch": "auto-dev",
            "task_branch_prefix": "task/",
            "auto_push": False,
            "commit_prefix": "feat",
            "state_commit_enabled": True,
        },
        "models": {
            "planner": {"model": "gemini-4-high", "effort": "medium", "timeout_sec": 300},
            "fast": {"model": "gemini-3.8-flash-high", "effort": "medium", "timeout_sec": 300},
            "smart": {"model": "gemini-4-neon-xhigh", "effort": "medium", "timeout_sec": 420},
            "visual": {"model": "gemini-3.8-flash-medium", "effort": "low", "timeout_sec": 180},
        },
        "limits": {
            "max_tasks_per_run": 3,
            "max_retries_per_task": 2,
            "max_consecutive_blocks": 2,
            "max_changed_files": 20,
            "max_diff_lines": 1200,
        },
        "scope": {
            "allowed_write_prefixes": ["src/", "tests/"],
            "forbidden_agent_edit_extensions": [".lock", ".png", ".jpg"],
            "index_include_roots": ["src", "tests"],
            "index_extensions": [".py", ".ts", ".tsx", ".js", ".cs", ".go"],
        },
        "adapter": {
            "type": "generic",
            "compile_cmd": "python -m compileall src",
            "test_cmd": "pytest -q",
            "timeout_sec": 180,
        },
        "agent_cli": {
            "executable": "agy",
            "mode": "auto",
            "skip_permissions": False,
            "extra_args": [],
        },
    }
    text = json.dumps(template, indent=2, ensure_ascii=False)
    if args.out:
        out_path = Path(args.out)
        out_path.parent.mkdir(parents=True, exist_ok=True)
        out_path.write_text(text + "\n", encoding="utf-8")
        print(f"Genel sablon kaydedildi: {out_path}")
    else:
        print(text)
    return 0


def build_parser() -> argparse.ArgumentParser:
    p = argparse.ArgumentParser(
        prog="autodev",
        description="AutoDev: Dusuk token maliyetli, Git guvenli otonom gelistirme orkestratoru.",
    )
    p.add_argument("--config", type=Path, default=None, help="Ozel config.json yolu")
    sub = p.add_subparsers(dest="command", required=True)

    sub.add_parser("status", help="Dal, kuyruk ve pano durumunu goster").set_defaults(func=_cmd_status)
    sub.add_parser("index", help="Sifir tokenla PROJECT_INDEX.md ve BOARD.md uret").set_defaults(
        func=_cmd_index
    )

    p_plan = sub.add_parser("plan", help="Hedefi kucuk atomik taslak gorevlere bol")
    p_plan.add_argument("goal", type=str, help="Planlanacak ozellik veya kilometre tasi hedefi")
    p_plan.add_argument("--max-tasks", type=int, default=6, help="Maksimum gorev sayisi")
    p_plan.set_defaults(func=_cmd_plan)

    p_add = sub.add_parser("add", help="Kuyruga elle yeni gorev ekle (0 token)")
    p_add.add_argument("--title", required=True, help="Gorev basligi")
    p_add.add_argument("--desc", default="", help="Gorev aciklamasi")
    p_add.add_argument("--criteria", action="append", help="Kabul kriteri (tekrarlanabilir)")
    p_add.add_argument("--depends", action="append", help="Bagimli gorev ID'si (tekrarlanabilir)")
    p_add.add_argument("--target", action="append", help="Hedef dosya yolu (tekrarlanabilir)")
    p_add.add_argument("--context", action="append", help="Baglam dosyasi yolu (tekrarlanabilir)")
    p_add.add_argument("--tier", choices=("fast", "smart"), default="fast")
    p_add.add_argument("--visual", action="store_true", help="Gorsel ekran goruntusu dogrulamasi ac")
    p_add.add_argument("--verify-check", default="", help="Adaptor dogrulama kontrol ID'si")
    p_add.add_argument("--approve", action="store_true", help="Dogrudan pending olarak ekle")
    p_add.set_defaults(func=_cmd_add)

    p_app = sub.add_parser("approve", help="Taslak gorevleri calismaya onayla (draft -> pending)")
    p_app.add_argument("ids", nargs="*", help="Onaylanacak gorev ID'leri")
    p_app.add_argument("--all", action="store_true", help="Tum taslaklari onayla")
    p_app.set_defaults(func=_cmd_approve)

    p_unb = sub.add_parser("unblock", help="Bloklanan gorevleri tekrar siraya al")
    p_unb.add_argument("ids", nargs="*", help="Bloku kaldirilacak gorev ID'leri")
    p_unb.add_argument("--all", action="store_true", help="Tum bloklu gorevleri ac")
    p_unb.set_defaults(func=_cmd_unblock)

    p_run = sub.add_parser("run", help="Onayli gorevleri otonom olarak calistir")
    p_run.add_argument("--max-tasks", type=int, default=None, help="Bu kosudaki maksimum gorev (0=sinirsiz)")
    p_run.add_argument("--pool", default="", help="Kullanilacak kota havuzu (gemini|claude)")
    p_run.add_argument("--until", default="", help="Bitis saati (HH:MM)")
    p_run.add_argument("--dry-run", action="store_true", help="Sadece calisacak gorevleri listele")
    p_run.set_defaults(func=_cmd_run)

    p_night = sub.add_parser("night", help="Cift havuzlu zaman ayarli gece vardiyasini baslat")
    p_night.add_argument("--shifts", default="", help="Vardiya listesi (or. 22:00-03:00:gemini,03:00-08:00:claude)")
    p_night.add_argument("--start-now", action="store_true", help="Beklemeden hemen basla")
    p_night.add_argument("--until", default="", help="--start-now ile bitis saati (HH:MM)")
    p_night.add_argument("--pool", default="", help="--start-now ile baslangic havuzu")
    p_night.add_argument("--max-tasks-per-shift", type=int, default=0)
    p_night.add_argument("--no-failover", action="store_true")
    p_night.add_argument("--no-auto-promote", action="store_true")
    p_night.add_argument("--no-auto-plan", action="store_true")
    p_night.add_argument("--gemini-pct", type=float, default=None)
    p_night.add_argument("--gemini-refresh-min", type=int, default=None)
    p_night.add_argument("--claude-pct", type=float, default=None)
    p_night.add_argument("--claude-refresh-min", type=int, default=None)
    p_night.add_argument("--dry-run", action="store_true")
    p_night.set_defaults(func=_cmd_night)

    p_ver = sub.add_parser("verify", help="Adaptor derleme ve dogrulama koprusunu test et (0 token)")
    p_ver.add_argument("--check", default="SMOKE", help="Calistirilacak dogrulama ID'si (varsayilan: SMOKE)")
    p_ver.set_defaults(func=_cmd_verify)

    p_init = sub.add_parser("init-generic", help="Baska projeler icin genel config.json sablonu uret")
    p_init.add_argument("--out", default="", help="Cikti dosya yolu (bos ise ekrana yazar)")
    p_init.set_defaults(func=_cmd_init_generic)

    return p


def main(argv: list[str] | None = None) -> int:
    parser = build_parser()
    args = parser.parse_args(argv)
    cfg = AutoDevConfig.load(config_path=args.config, repo_root=REPO_ROOT)
    try:
        return int(args.func(cfg, args))
    except Exception as exc:
        print(f"[AutoDev HATA] {exc}", file=sys.stderr)
        return 1


if __name__ == "__main__":
    raise SystemExit(main())
