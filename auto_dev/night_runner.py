from __future__ import annotations

import argparse
import sys
import time
from dataclasses import dataclass
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


def parse_shift_specs(spec_str: str) -> list[ShiftWindowConfig]:
    """Parse CLI `--shifts` string like '22:00-03:00:gemini,03:00-08:00:claude'."""
    shifts: list[ShiftWindowConfig] = []
    for idx, chunk in enumerate(spec_str.split(","), start=1):
        chunk = chunk.strip()
        if not chunk:
            continue
        if "-" not in chunk:
            raise ValueError(f"Gecersiz vardiya tanimi: '{chunk}' (ornek: 22:00-03:00:gemini)")
        start_part, rest = chunk.split("-", 1)
        # rest is either "03:00:gemini" or "03:00"
        rest_parts = rest.split(":")
        if len(rest_parts) == 3:
            end_str = f"{rest_parts[0]}:{rest_parts[1]}"
            pool = rest_parts[2].strip()
        elif len(rest_parts) == 2:
            end_str = rest
            pool = "gemini" if idx % 2 == 1 else "claude"
        else:
            raise ValueError(f"Gecersiz vardiya bitis/havuz tanimi: '{chunk}'")
        _parse_hhmm(start_part)
        _parse_hhmm(end_str)
        shifts.append(
            ShiftWindowConfig(
                name=f"Shift{idx}_{pool.capitalize()}",
                start=start_part.strip(),
                end=end_str.strip(),
                pool=pool,
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
            cycle.append(ResolvedShift(name=spec.name, pool=spec.pool, start_dt=start_dt, end_dt=end_dt))
            prev_end = end_dt
            cursor_day = end_dt.date()
        return cycle

    # Try yesterday's overnight cycle first (handles e.g. 03:20 AM inside yesterday's 22:00->08:00 schedule!)
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

    if start_now:
        end_str = until_hhmm or (ns.shifts[-1].end if ns.shifts else "08:00")
        eh, em = _parse_hhmm(end_str)
        end_dt = now_local.replace(hour=eh, minute=em, second=0, microsecond=0)
        if end_dt <= now_local:
            end_dt += timedelta(days=1)
        pool0 = initial_pool or (ns.shifts[0].pool if ns.shifts else "gemini")
        schedule = [
            ResolvedShift(
                name=f"Immediate_{pool0.capitalize()}",
                pool=pool0,
                start_dt=now_local,
                end_dt=end_dt,
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
    print(f" Failover     : {'Acik (kota dolunca diger havuza gec)' if (ns.failover_on_quota and not no_failover) else 'Kapali'}")
    print(f" Auto-Push    : {'Acik (origin/' + cfg.git.dev_branch + ')' if cfg.git.auto_push else 'Kapali'}")
    print(f" Auto-Draft   : {'Acik' if (ns.auto_promote_drafts and not no_auto_promote) else 'Kapali'}")
    print(f" Auto-Plan    : {'Acik (max ' + str(ns.max_auto_plan_batches_per_night) + ' parti)' if (ns.auto_plan_when_empty and not no_auto_plan) else 'Kapali'}")
    print("\nPlanlanan Vardiyalar:")
    for idx, sh in enumerate(schedule, start=1):
        profs = cfg.model_pools.get(sh.pool, {})
        fast_m = profs.get("fast").model if "fast" in profs else "?"
        smart_m = profs.get("smart").model if "smart" in profs else "?"
        rem_pct = pool_mgr.get_remaining_pct(sh.pool)
        print(
            f"  {idx}. {sh.name}: {sh.start_dt.strftime('%d.%m %H:%M')} -> {sh.end_dt.strftime('%d.%m %H:%M')} "
            f"| Havuz: {sh.pool} (Kalan: %{rem_pct}) | fast={fast_m}, smart={smart_m}"
        )

    adapter = load_adapter(cfg)
    orch = Orchestrator(cfg, adapter, pool_manager=pool_mgr)

    if dry_run:
        preview = orch.run(max_tasks=0, dry_run=True, pool_name=schedule[0].pool)
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
                        _sleep_until(sh.start_dt, f"{sh.name} ({sh.pool}) baslangici")

                    print(
                        f"\n[NightRunner] === {sh.name} BASLADI "
                        f"({sh.start_dt.strftime('%H:%M')} -> {sh.end_dt.strftime('%H:%M')} | Havuz: {sh.pool}) ===",
                        flush=True,
                    )

                    # Loop within shift if all pools cool down before shift end
                    while True:
                        now_loop = datetime.now(sh.end_dt.tzinfo)
                        if (sh.end_dt - now_loop).total_seconds() < ns.cutoff_buffer_minutes * 60:
                            print(f"[NightRunner] {sh.name} bitis tamponuna ulasildi.", flush=True)
                            break

                        res = orch.run(
                            max_tasks=max_tasks_per_shift,
                            dry_run=False,
                            pool_name=sh.pool,
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
                            keep_awake=False,  # Outer KeepAwake holds execution state across entire night
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
        description="AutoDev saf Python gece vardiya yoneticisi (cift kota havuzlu, zaman ayarli).",
    )
    p.add_argument("--config", type=Path, default=None, help="Ozel config.json yolu")
    p.add_argument(
        "--shifts",
        default="",
        help="Vardiya listesi (or. '22:00-03:00:gemini,03:00-08:00:claude'). Bos ise config.json kullanilir.",
    )
    p.add_argument("--start-now", action="store_true", help="Beklemeden hemen simdi basla")
    p.add_argument("--until", default="", help="--start-now ile bitis saati (HH:MM, varsayilan 08:00)")
    p.add_argument("--pool", default="", help="--start-now ile baslangic havuzu (gemini|claude)")
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


if __name__ == "__main__":
    raise SystemExit(main())
