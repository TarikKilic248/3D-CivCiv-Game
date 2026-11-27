from __future__ import annotations

import argparse
import sys
import time
from dataclasses import dataclass, field
from datetime import datetime, timedelta
from pathlib import Path
from typing import TextIO

# Allow running directly via `python auto_dev/night_runner.py ...`
REPO_ROOT = Path(__file__).resolve().parent.parent
if str(REPO_ROOT) not in sys.path:
    sys.path.insert(0, str(REPO_ROOT))

from auto_dev.adapters import load_adapter
from auto_dev.core.config import AutoDevConfig, ShiftWindowConfig
from auto_dev.core.orchestrator import Orchestrator
from auto_dev.core.pool_manager import PoolManager
from auto_dev.core.power import KeepAwake


@dataclass
class ResolvedShift:
    name: str
    pool: str
    start_dt: datetime
    end_dt: datetime
    pools: list[str] = field(default_factory=lambda: ["gemini", "claude"])
    mode: str = "balanced"


class _TeeStream:
    def __init__(self, primary: TextIO, log_handle: TextIO) -> None:
        self._primary = primary
        self._log = log_handle

    def write(self, data: str) -> int:
        self._primary.write(data)
        self._log.write(data)
        self._log.flush()
        return len(data)

    def flush(self) -> None:
        self._primary.flush()
        self._log.flush()


def _parse_hhmm(hhmm: str) -> tuple[int, int]:
    parts = hhmm.strip().split(":")
    if len(parts) != 2:
        raise ValueError(f"Gecersiz saat formati (HH:MM beklenir): '{hhmm}'")
    h, m = int(parts[0]), int(parts[1])
    if not (0 <= h <= 23 and 0 <= m <= 59):
        raise ValueError(f"Saat araligi gecersiz: '{hhmm}'")
    return h, m


def parse_shift_specs(spec_str: str, default_pools: list[str] | None = None) -> list[ShiftWindowConfig]:
    """Parse CLI `--shifts` string.
    Examples:
      - '00:00-05:15:balanced,05:15-10:15:balanced'
      - '00:00-05:15:gemini+claude,05:15-10:15:gemini+claude'
      - '22:00-03:00:gemini,03:00-08:00:claude'
      - '00:00-05:15,05:15-10:15'  (defaults to balanced across both pools!)
    """
    all_pools = list(default_pools or ["gemini", "claude"])
    shifts: list[ShiftWindowConfig] = []
    for idx, chunk in enumerate(spec_str.split(","), start=1):
        chunk = chunk.strip()
        if not chunk:
            continue
        if "-" not in chunk:
            raise ValueError(f"Gecersiz vardiya tanimi: '{chunk}' (ornek: 00:00-05:15:balanced)")
        start_part, rest = chunk.split("-", 1)
        rest_parts = rest.split(":")
        if len(rest_parts) == 3:
            end_str = f"{rest_parts[0]}:{rest_parts[1]}"
            pool_token = rest_parts[2].strip()
        elif len(rest_parts) == 2:
            end_str = rest
            pool_token = "balanced"
        else:
            raise ValueError(f"Gecersiz vardiya bitis/havuz tanimi: '{chunk}'")
        _parse_hhmm(start_part)
        _parse_hhmm(end_str)

        if pool_token.lower() == "balanced":
            sh_pools = list(all_pools)
            sh_mode = "balanced"
            label = "Balanced"
        elif "+" in pool_token:
            sh_pools = [p.strip() for p in pool_token.split("+") if p.strip()]
            sh_mode = "balanced"
            label = "Balanced"
        else:
            sh_pools = [pool_token]
            sh_mode = "single"
            label = pool_token.capitalize()

        shifts.append(
            ShiftWindowConfig(
                name=f"Shift{idx}_{label}",
                start=start_part.strip(),
                end=end_str.strip(),
                pool=pool_token,
                pools=sh_pools,
                mode=sh_mode,
            )
        )
    if not shifts:
        raise ValueError("En az bir vardiya tanimlanmalidir.")
    return shifts


def resolve_shift_schedule(
    specs: list[ShiftWindowConfig],
    now: datetime | None = None,
) -> list[ResolvedShift]:
    """Resolve sequential wall-clock shift windows cleanly across midnight."""
    local_now = now or datetime.now().astimezone()
    tz = local_now.tzinfo

    def build_cycle(base_date: datetime) -> list[ResolvedShift]:
        cycle: list[ResolvedShift] = []
        cursor_day = base_date.date()
        prev_end: datetime | None = None
        for spec in specs:
            sh, sm = _parse_hhmm(spec.start)
            eh, em = _parse_hhmm(spec.end)
            start_dt = datetime(cursor_day.year, cursor_day.month, cursor_day.day, sh, sm, tzinfo=tz)
            if prev_end is not None and start_dt < prev_end:
                start_dt += timedelta(days=1)
                cursor_day = start_dt.date()
            end_dt = datetime(start_dt.year, start_dt.month, start_dt.day, eh, em, tzinfo=tz)
            if end_dt <= start_dt:
                end_dt += timedelta(days=1)
            cycle.append(
                ResolvedShift(
                    name=spec.name,
                    pool=spec.pool,
                    start_dt=start_dt,
                    end_dt=end_dt,
                    pools=list(spec.pools),
                    mode=spec.mode,
                )
            )
            prev_end = end_dt
            cursor_day = end_dt.date()
        return cycle

    for day_offset in (-1, 0, 1):
        base = local_now + timedelta(days=day_offset)
        cycle = build_cycle(base)
        remaining = [s for s in cycle if s.end_dt > local_now + timedelta(minutes=1)]
        if remaining:
            return remaining
    return build_cycle(local_now + timedelta(days=1))


def _sleep_until(target: datetime, label: str) -> None:
    while True:
        now = datetime.now(target.tzinfo)
        rem_sec = (target - now).total_seconds()
        if rem_sec <= 0:
            return
        rem_min = int(rem_sec // 60)
        rem_s = int(rem_sec % 60)
        print(
            f"[NightRunner] {label} bekleniyor — hedef: {target.strftime('%H:%M')} "
            f"(kalan: {rem_min}dk {rem_s:02d}s, 0 token)",
            flush=True,
        )
        time.sleep(min(300.0, rem_sec))


def _rotate_logs(log_dir: Path, keep: int = 60) -> None:
    files = sorted(log_dir.glob("night_*.log"), key=lambda p: p.stat().st_mtime, reverse=True)
    for old in files[keep:]:
        try:
            old.unlink(missing_ok=True)
        except OSError:
            pass


def run_night_session(
    cfg: AutoDevConfig,
    shifts_spec: list[ShiftWindowConfig] | None = None,
    start_now: bool = False,
    until_hhmm: str | None = None,
    initial_pool: str | None = None,
    max_tasks_per_shift: int = 0,
    no_failover: bool = False,
    no_auto_promote: bool = False,
    no_auto_plan: bool = False,
    gemini_pct: float | None = None,
    gemini_refresh_min: int | None = None,
    claude_pct: float | None = None,
    claude_refresh_min: int | None = None,
    dry_run: bool = False,
) -> int:
    ns = cfg.night_shift
    now_local = datetime.now().astimezone()
    all_pools = list(cfg.model_pools.keys())

    if start_now:
        end_str = until_hhmm or (ns.shifts[-1].end if ns.shifts else "08:00")
        eh, em = _parse_hhmm(end_str)
        end_dt = now_local.replace(hour=eh, minute=em, second=0, microsecond=0)
        if end_dt <= now_local:
            end_dt += timedelta(days=1)
        if initial_pool and initial_pool != "balanced" and "+" not in initial_pool:
            sh_mode = "single"
            sh_pools = [initial_pool]
            sh_name = f"Immediate_{initial_pool.capitalize()}"
        else:
            sh_mode = "balanced"
            sh_pools = all_pools
            sh_name = "Immediate_Balanced"
        schedule = [
            ResolvedShift(
                name=sh_name,
                pool=initial_pool or "balanced",
                start_dt=now_local,
                end_dt=end_dt,
                pools=sh_pools,
                mode=sh_mode,
            )
        ]
    else:
        specs = shifts_spec or ns.shifts
        schedule = resolve_shift_schedule(specs, now=now_local)

    pool_mgr = PoolManager(
        pools=cfg.model_pools,
        state_file=cfg.runtime_dir / "pool_state.json",
        default_cooldown_min=ns.quota_cooldown_minutes,
        pool_budgets=ns.pool_5h_budgets,
    )
    if gemini_pct is not None:
        pool_mgr.set_initial_remaining_pct("gemini", gemini_pct, refresh_in_minutes=gemini_refresh_min)
    if claude_pct is not None:
        pool_mgr.set_initial_remaining_pct("claude", claude_pct, refresh_in_minutes=claude_refresh_min)

    print("============================================================")
    print(f" AutoDev Gece Vardiya Yoneticisi — {cfg.project_name}")
    print("============================================================")
    print(f" Simdiki Saat : {now_local.strftime('%Y-%m-%d %H:%M:%S %z')}")
    print(f" Strateji     : {'Senkronize Cift Havuz Dengeleme (balanced)' if ns.pool_strategy == 'balanced' else 'Tek Havuz'}")
    print(f" Capraz Onarim: {'Acik (Gemini <-> Claude)' if ns.cross_pool_escalation else 'Kapali'}")
    print(f" Failover     : {'Acik (kota dolunca diger havuza gec)' if (ns.failover_on_quota and not no_failover) else 'Kapali'}")
    print(f" Blok Limiti  : Ust uste max {ns.max_consecutive_blocks_night} blok")
    print(f" Auto-Push    : {'Acik (origin/' + cfg.git.dev_branch + ')' if cfg.git.auto_push else 'Kapali'}")
    print(f" Auto-Draft   : {'Acik' if (ns.auto_promote_drafts and not no_auto_promote) else 'Kapali'}")
    print(f" Auto-Plan    : {'Acik (max ' + str(ns.max_auto_plan_batches_per_night) + ' parti)' if (ns.auto_plan_when_empty and not no_auto_plan) else 'Kapali'}")
    print("\nPlanlanan Vardiyalar:")
    for idx, sh in enumerate(schedule, start=1):
        pool_desc = " + ".join(
            f"{p} (%{pool_mgr.get_remaining_pct(p)})" for p in sh.pools
        )
        print(
            f"  {idx}. {sh.name} [{sh.mode}]: {sh.start_dt.strftime('%d.%m %H:%M')} -> {sh.end_dt.strftime('%d.%m %H:%M')} "
            f"| Havuzlar: {pool_desc}"
        )
        for p in sh.pools:
            profs = cfg.model_pools.get(p, {})
            fast_m = profs.get("fast").model if "fast" in profs else "?"
            smart_m = profs.get("smart").model if "smart" in profs else "?"
            plan_m = profs.get("planner").model if "planner" in profs else "?"
            print(f"       - {p}: fast={fast_m}, smart={smart_m}, planner={plan_m}")

    adapter = load_adapter(cfg)
    orch = Orchestrator(cfg, adapter, pool_manager=pool_mgr)

    if dry_run:
        preview = orch.run(
            max_tasks=0,
            dry_run=True,
            pool_name=schedule[0].pools[0],
            pool_strategy=schedule[0].mode,
            active_pools=schedule[0].pools,
        )
        ready = preview.get("ready_tasks", [])
        print(f"\n[DRY-RUN] Su an calismaya hazir {len(ready)} onayli gorev:")
        for t in ready[:10]:
            print(f"  - {t['id']} [{t['tier']}]: {t['title']}")
        print("\nHavuz Durumu:", pool_mgr.status_summary())
        return 0

    log_dir = cfg.runtime_dir / "logs"
    log_dir.mkdir(parents=True, exist_ok=True)
    log_path = log_dir / f"night_{now_local.strftime('%Y%m%d_%H%M%S')}.log"
    _rotate_logs(log_dir, keep=60)

    orig_out, orig_err = sys.stdout, sys.stderr
    with log_path.open("w", encoding="utf-8") as log_handle:
        sys.stdout = _TeeStream(orig_out, log_handle)  # type: ignore[assignment]
        sys.stderr = _TeeStream(orig_err, log_handle)  # type: ignore[assignment]
        try:
            print(f"[NightRunner] Log dosyasi: {log_path}", flush=True)
            with KeepAwake(enabled=ns.keep_awake):
                for sh in schedule:
                    now_check = datetime.now(sh.start_dt.tzinfo)
                    if now_check >= sh.end_dt - timedelta(minutes=ns.cutoff_buffer_minutes):
                        print(f"[NightRunner] {sh.name} suresi dolmus, atlaniyor.", flush=True)
                        continue

                    if now_check < sh.start_dt:
                        _sleep_until(sh.start_dt, f"{sh.name} ({sh.mode}) baslangici")

                    print(
                        f"\n[NightRunner] === {sh.name} BASLADI "
                        f"({sh.start_dt.strftime('%H:%M')} -> {sh.end_dt.strftime('%H:%M')} "
                        f"| Mod: {sh.mode} | Havuzlar: {'+'.join(sh.pools)}) ===",
                        flush=True,
                    )

                    while True:
                        now_loop = datetime.now(sh.end_dt.tzinfo)
                        if (sh.end_dt - now_loop).total_seconds() < ns.cutoff_buffer_minutes * 60:
                            print(f"[NightRunner] {sh.name} bitis tamponuna ulasildi.", flush=True)
                            break

                        res = orch.run(
                            max_tasks=max_tasks_per_shift,
                            dry_run=False,
                            pool_name=sh.pools[0],
                            pool_strategy=sh.mode,
                            active_pools=sh.pools,
                            cross_pool_escalation=ns.cross_pool_escalation,
                            prefer_gemini_for_visual=ns.prefer_gemini_for_visual,
                            max_consecutive_blocks=ns.max_consecutive_blocks_night,
                            shift_name=sh.name,
                            deadline=sh.end_dt,
                            cutoff_buffer_min=ns.cutoff_buffer_minutes,
                            failover_on_quota=(ns.failover_on_quota and not no_failover),
                            auto_promote_drafts=(ns.auto_promote_drafts and not no_auto_promote),
                            auto_promote_batch_size=ns.auto_promote_batch_size,
                            auto_plan_when_empty=(ns.auto_plan_when_empty and not no_auto_plan),
                            auto_plan_batch_size=ns.auto_plan_batch_size,
                            max_auto_plan_batches=ns.max_auto_plan_batches_per_night,
                            wait_lock_sec=300,
                            keep_awake=False,
                        )
                        stop = res.get("stop_reason", "")
                        print(f"[NightRunner] {sh.name} tur sonucu: {stop}", flush=True)

                        if stop == "all_pools_exhausted":
                            wake_iso = res.get("next_available_at")
                            if wake_iso:
                                wake_dt = datetime.fromisoformat(wake_iso).astimezone(sh.end_dt.tzinfo)
                                if wake_dt < sh.end_dt - timedelta(minutes=ns.cutoff_buffer_minutes + 5):
                                    _sleep_until(wake_dt, "Kota yenilenmesi")
                                    continue
                            break
                        break

                print(f"\n[NightRunner] Tum gece vardiyalari tamamlandi! Sabah ozeti: {cfg.brief_file}", flush=True)
        except KeyboardInterrupt:
            print("\n[NightRunner] Kullanici tarafindan guvenle sonlandirildi.", flush=True)
        finally:
            sys.stdout = orig_out
            sys.stderr = orig_err

    return 0


def build_parser() -> argparse.ArgumentParser:
    p = argparse.ArgumentParser(
        prog="night_runner",
        description="AutoDev saf Python gece vardiya yoneticisi (senkronize cift kota havuzlu).",
    )
    p.add_argument("--config", type=Path, default=None, help="Ozel config.json yolu")
    p.add_argument(
        "--shifts",
        default="",
        help="Vardiya listesi (or. '00:00-05:15:balanced,05:15-10:15:balanced'). Bos ise config.json kullanilir.",
    )
    p.add_argument("--start-now", action="store_true", help="Beklemeden hemen simdi basla")
    p.add_argument("--until", default="", help="--start-now ile bitis saati (HH:MM, varsayilan 08:00)")
    p.add_argument("--pool", default="", help="--start-now ile havuz modu (balanced|gemini|claude)")
    p.add_argument(
        "--max-tasks-per-shift",
        type=int,
        default=0,
        help="Vardiya basina max gorev (0 = zaman penceresi bitene kadar sinirsiz)",
    )
    p.add_argument("--no-failover", action="store_true", help="Kota dolunca diger havuza gecme")
    p.add_argument("--no-auto-promote", action="store_true", help="Draft gorevleri otomatik onaylama")
    p.add_argument("--no-auto-plan", action="store_true", help="Kuyruk bitince yeni gorev uretme")
    p.add_argument("--gemini-pct", type=float, default=None, help="Gemini 5h kalan kota % baslangic degeri")
    p.add_argument("--gemini-refresh-min", type=int, default=None, help="Gemini yenilenmeye kalan dakika")
    p.add_argument("--claude-pct", type=float, default=None, help="Claude 5h kalan kota % baslangic degeri")
    p.add_argument("--claude-refresh-min", type=int, default=None, help="Claude yenilenmeye kalan dakika")
    p.add_argument("--dry-run", action="store_true", help="Sadece vardiya takvimini ve hazir gorevleri goster")
    return p


def main(argv: list[str] | None = None) -> int:
    args = build_parser().parse_args(argv)
    cfg = AutoDevConfig.load(config_path=args.config, repo_root=REPO_ROOT)
    specs = parse_shift_specs(args.shifts, default_pools=list(cfg.model_pools.keys())) if args.shifts else None
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


if __name__ == "__main__":
    raise SystemExit(main())
