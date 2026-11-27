from __future__ import annotations

import json
import os
from datetime import datetime, timedelta, timezone
from pathlib import Path
from typing import Any

from .config import ModelProfile

FIVE_HOURS_SEC = 5 * 3600


def _now_utc() -> datetime:
    return datetime.now(timezone.utc)


def _parse_iso(val: str | None) -> datetime | None:
    if not val:
        return None
    try:
        dt = datetime.fromisoformat(val)
        if dt.tzinfo is None:
            dt = dt.replace(tzinfo=timezone.utc)
        return dt
    except ValueError:
        return None


class PoolManager:
    """Manages independent model quota pools (e.g. `gemini`, `claude`) with 5h rolling telemetry & failover."""

    def __init__(
        self,
        pools: dict[str, dict[str, ModelProfile]],
        state_file: Path,
        default_cooldown_min: int = 305,
        pool_budgets: dict[str, int] | None = None,
        available_models: list[str] | None = None,
    ) -> None:
        self.pools = pools
        self.state_file = state_file
        self.default_cooldown_sec = max(60, int(default_cooldown_min) * 60)
        self.default_budgets = {
            "gemini": 2_500_000,
            "claude": 1_500_000,
            **(pool_budgets or {}),
        }
        self._available_models: list[str] = list(available_models or [])
        self._unavailable_models: set[str] = set()
        self._state: dict[str, dict[str, Any]] = {}
        self._load()
        if self._available_models:
            self.set_available_models(self._available_models)

    @staticmethod
    def _belongs_to_pool(pool_name: str, model_id: str) -> bool:
        m = model_id.lower()
        p = pool_name.lower()
        if p.startswith("gemini"):
            return m.startswith("gemini")
        if p.startswith("claude"):
            return m.startswith("claude") or m.startswith("gpt-")
        return True

    @staticmethod
    def _extract_effort_hint(model_id: str, default_effort: str = "medium") -> str:
        low = model_id.lower()
        for eff in ("xhigh", "high", "medium", "low"):
            if low.endswith(f"-{eff}"):
                return eff
        return (default_effort or "medium").lower()

    @classmethod
    def _score_candidate(
        cls,
        pool_name: str,
        role: str,
        candidate: str,
        requested_model: str,
        effort_hint: str,
    ) -> float:
        """Score how well a live model matches the target pool, role, and effort."""
        c = candidate.lower()
        score = 0.0
        if not cls._belongs_to_pool(pool_name, c):
            return -10_000.0

        is_flash = "flash" in c
        is_gemini4 = c.startswith("gemini-4")
        is_opus = "opus" in c
        is_sonnet = "sonnet" in c
        is_haiku = "haiku" in c
        is_gpt = c.startswith("gpt-")

        if pool_name.lower().startswith("gemini"):
            if role in ("fast", "visual"):
                score += 100.0 if is_flash else 45.0
                if role == "visual" and c.endswith("-low"):
                    score += 30.0
                elif role == "fast" and c.endswith("-medium"):
                    score += 25.0
                elif c.endswith("-high"):
                    score += 15.0
            else:  # smart / planner
                score += 100.0 if is_gemini4 else (65.0 if not is_flash else 40.0)
                if "pro" in c or "ultra" in c or "hydrogen" in c or "neon" in c:
                    score += 15.0
        elif pool_name.lower().startswith("claude"):
            if role == "planner":
                score += 110.0 if is_opus else (85.0 if is_sonnet else 35.0)
            elif role == "smart":
                score += 105.0 if is_sonnet else (95.0 if is_opus else 35.0)
            else:  # fast / visual
                score += 105.0 if is_sonnet else (90.0 if is_haiku else (65.0 if is_opus else 40.0))
                if is_gpt and role == "visual":
                    score -= 30.0

        # Effort affinity bonus
        desired_eff = cls._extract_effort_hint(requested_model, effort_hint)
        cand_eff = cls._extract_effort_hint(c, "")
        if cand_eff == desired_eff:
            score += 22.0
        elif desired_eff == "xhigh" and cand_eff == "high":
            score += 18.0
        elif desired_eff == "high" and cand_eff in ("medium", "xhigh"):
            score += 12.0
        elif desired_eff == "medium" and cand_eff in ("high", "low"):
            score += 10.0
        elif desired_eff == "low" and cand_eff == "medium":
            score += 10.0
        elif not cand_eff and desired_eff in c:
            score += 8.0

        # Small bonus if candidate shares the family prefix (e.g., gemini-3.8-flash vs gemini-4)
        req_prefix = "-".join(requested_model.lower().split("-")[:2])
        if req_prefix and c.startswith(req_prefix):
            score += 15.0
        return score

    def _ordered_candidates(
        self,
        pool_name: str,
        role: str,
        profile: ModelProfile,
        excluded: set[str] | None = None,
    ) -> list[str]:
        blocked = self._unavailable_models | (excluded or set())
        pool = self.pools.get(pool_name, {})

        # Build deduplicated candidate universe: configured fallback_models + other roles in pool + live models
        raw_pool: list[str] = []
        if self._available_models:
            raw_pool.extend(self._available_models)
        raw_pool.extend(profile.fallback_models)
        for rp in pool.values():
            raw_pool.append(rp.model)
            raw_pool.extend(rp.fallback_models)

        # Deduplicate while preserving order, filtering pool compatibility & non-blocked
        seen: set[str] = set()
        candidates: list[str] = []
        avail_set = set(self._available_models)
        for m in raw_pool:
            if not m or m in seen or m in blocked:
                continue
            if not self._belongs_to_pool(pool_name, m):
                continue
            # When live model list is known, skip candidates absent from it
            if avail_set and m not in avail_set:
                continue
            seen.add(m)
            candidates.append(m)

        # Sort candidates strictly by pool/role/effort fitness; explicit `fallback_models` acts as a
        # small tie-breaker so weaker models like `gpt-oss` never outrank `claude-sonnet`/`claude-opus`.
        explicit_rank = {m: idx for idx, m in enumerate(profile.fallback_models)}

        def _rank_key(m: str) -> tuple[float, int]:
            base_score = self._score_candidate(pool_name, role, m, profile.model, profile.effort)
            if m in explicit_rank:
                # Only give preference boost within similar-tier models (e.g. skip outranking opus/sonnet with gpt-oss)
                if not m.lower().startswith("gpt-"):
                    base_score += max(1.0, 8.0 - explicit_rank[m])
                return (base_score, -explicit_rank[m])
            return (base_score, -999)

        candidates.sort(key=_rank_key, reverse=True)
        return candidates

    def set_available_models(self, available_models: list[str]) -> list[tuple[str, str, str, str]]:
        """Sync configured pool models against live `agy models` output (0 tokens).
        Returns [(pool, role, old_model, new_model), ...] for any remapped roles.
        """
        self._available_models = list(available_models or [])
        if not self._available_models:
            return []

        avail_set = set(self._available_models)
        remapped: list[tuple[str, str, str, str]] = []
        for pname, roles in self.pools.items():
            for role, prof in roles.items():
                if prof.model in avail_set and prof.model not in self._unavailable_models:
                    continue
                alts = self._ordered_candidates(pname, role, prof, excluded={prof.model})
                if alts and alts[0] != prof.model:
                    old_m = prof.model
                    prof.model = alts[0]
                    remapped.append((pname, role, old_m, alts[0]))
        return remapped

    def mark_model_unavailable(self, model_name: str) -> None:
        if model_name:
            self._unavailable_models.add(model_name)

    def next_fallback_profile(
        self,
        pool_name: str,
        role: str,
        failed_model: str,
        excluded: set[str] | None = None,
    ) -> ModelProfile | None:
        """Pick next healthy alternative ModelProfile in `pool_name` after `failed_model` failed."""
        base = self.get_profile(pool_name, role)
        skip = set(excluded or set()) | {failed_model}
        alts = self._ordered_candidates(pool_name, role, base, excluded=skip)
        if not alts:
            return None
        chosen = alts[0]
        return ModelProfile(
            model=chosen,
            effort=base.effort,
            timeout_sec=base.timeout_sec,
            fallback_models=[m for m in alts[1:]],
        )

    def pool_names(self) -> list[str]:
        return list(self.pools.keys())

    def get_profile(self, pool_name: str, role: str) -> ModelProfile:
        if pool_name not in self.pools:
            pool_name = next(iter(self.pools.keys()))
        pool = self.pools[pool_name]
        if role in pool:
            return pool[role]
        return next(iter(pool.values()))

    def _default_pool_state(self, name: str) -> dict[str, Any]:
        return {
            "window_started_at": None,
            "window_tokens_used": 0,
            "starting_remaining_pct": 100.0,
            "estimated_5h_budget": int(self.default_budgets.get(name, 2_000_000)),
            "cooldown_until": None,
            "exhausted_count": 0,
            "last_calibrated_at": None,
        }

    def _load(self) -> None:
        raw: dict[str, Any] = {}
        if self.state_file.exists():
            try:
                raw = json.loads(self.state_file.read_text(encoding="utf-8"))
            except Exception:
                raw = {}
        pools_raw = raw.get("pools", {}) if isinstance(raw, dict) else {}
        for name in self.pools:
            st = self._default_pool_state(name)
            if isinstance(pools_raw.get(name), dict):
                st.update(pools_raw[name])
            self._state[name] = st
        self._refresh_expired_windows()

    def _save(self) -> None:
        self.state_file.parent.mkdir(parents=True, exist_ok=True)
        payload = {
            "updated_at": _now_utc().replace(microsecond=0).isoformat(),
            "pools": self._state,
        }
        tmp = self.state_file.with_suffix(".json.tmp")
        tmp.write_text(json.dumps(payload, indent=2, ensure_ascii=False) + "\n", encoding="utf-8")
        os.replace(str(tmp), str(self.state_file))

    def _refresh_expired_windows(self, now: datetime | None = None) -> None:
        now = now or _now_utc()
        changed = False
        for name, st in self._state.items():
            cooldown_dt = _parse_iso(st.get("cooldown_until"))
            if cooldown_dt is not None and now >= cooldown_dt:
                st["cooldown_until"] = None
                st["window_started_at"] = None
                st["window_tokens_used"] = 0
                st["starting_remaining_pct"] = 100.0
                changed = True
                continue

            win_start = _parse_iso(st.get("window_started_at"))
            if win_start is not None and (now - win_start).total_seconds() >= FIVE_HOURS_SEC:
                st["window_started_at"] = None
                st["window_tokens_used"] = 0
                st["starting_remaining_pct"] = 100.0
                changed = True

        if changed:
            self._save()

    def set_initial_remaining_pct(
        self,
        pool_name: str,
        pct: float,
        refresh_in_minutes: int | None = None,
    ) -> None:
        if pool_name not in self._state:
            return
        now = _now_utc()
        st = self._state[pool_name]
        clamped = max(0.0, min(100.0, float(pct)))
        st["starting_remaining_pct"] = clamped
        st["window_tokens_used"] = 0
        if refresh_in_minutes is not None and refresh_in_minutes > 0:
            elapsed = max(0, FIVE_HOURS_SEC - refresh_in_minutes * 60)
            st["window_started_at"] = (now - timedelta(seconds=elapsed)).replace(microsecond=0).isoformat()
        elif clamped < 100.0:
            st["window_started_at"] = now.replace(microsecond=0).isoformat()
        else:
            st["window_started_at"] = None
        if clamped > 0.0:
            st["cooldown_until"] = None
        self._save()

    def is_available(self, pool_name: str, now: datetime | None = None) -> bool:
        now = now or _now_utc()
        self._refresh_expired_windows(now)
        if pool_name not in self._state:
            return False
        cd = _parse_iso(self._state[pool_name].get("cooldown_until"))
        return cd is None or now >= cd

    def cooldown_until(self, pool_name: str) -> datetime | None:
        self._refresh_expired_windows()
        if pool_name not in self._state:
            return None
        return _parse_iso(self._state[pool_name].get("cooldown_until"))

    def get_remaining_pct(self, pool_name: str, now: datetime | None = None) -> float:
        now = now or _now_utc()
        self._refresh_expired_windows(now)
        if pool_name not in self._state:
            return 0.0
        st = self._state[pool_name]
        cd = _parse_iso(st.get("cooldown_until"))
        if cd is not None and now < cd:
            return 0.0
        budget = max(100_000, int(st.get("estimated_5h_budget", 2_000_000)))
        start_pct = float(st.get("starting_remaining_pct", 100.0))
        used = int(st.get("window_tokens_used", 0))
        consumed_pct = (used / budget) * 100.0
        return round(max(0.0, min(100.0, start_pct - consumed_pct)), 1)

    def snapshot_remaining_pcts(self, now: datetime | None = None) -> dict[str, float]:
        now = now or _now_utc()
        return {name: self.get_remaining_pct(name, now) for name in self.pools}

    def record_usage(self, pool_name: str, total_tokens: int, now: datetime | None = None) -> float:
        now = now or _now_utc()
        self._refresh_expired_windows(now)
        if pool_name not in self._state:
            return 0.0
        st = self._state[pool_name]
        if total_tokens > 0:
            if not st.get("window_started_at"):
                st["window_started_at"] = now.replace(microsecond=0).isoformat()
            st["window_tokens_used"] = int(st.get("window_tokens_used", 0)) + int(total_tokens)
            self._save()
        return self.get_remaining_pct(pool_name, now)

    def mark_quota_exhausted(
        self,
        pool_name: str,
        hint_reset_sec: int | None = None,
        now: datetime | None = None,
    ) -> datetime:
        now = now or _now_utc()
        if pool_name not in self._state:
            return now + timedelta(seconds=self.default_cooldown_sec)
        st = self._state[pool_name]
        st["exhausted_count"] = int(st.get("exhausted_count", 0)) + 1

        # Self-calibrate estimated 5h budget when meaningful token volume was observed
        used = int(st.get("window_tokens_used", 0))
        start_pct = float(st.get("starting_remaining_pct", 100.0))
        if used >= 50_000 and start_pct >= 20.0:
            inferred_full_budget = int(used * (100.0 / start_pct))
            if inferred_full_budget >= 100_000:
                st["estimated_5h_budget"] = inferred_full_budget
                st["last_calibrated_at"] = now.replace(microsecond=0).isoformat()

        if hint_reset_sec is not None and hint_reset_sec > 0:
            wait_sec = hint_reset_sec + 120
        else:
            win_start = _parse_iso(st.get("window_started_at"))
            if win_start is not None:
                remaining_win = int(FIVE_HOURS_SEC - (now - win_start).total_seconds())
                wait_sec = max(300, remaining_win + 120) if remaining_win > 0 else self.default_cooldown_sec
            else:
                wait_sec = self.default_cooldown_sec

        until = (now + timedelta(seconds=wait_sec)).replace(microsecond=0)
        st["cooldown_until"] = until.isoformat()
        self._save()
        return until

    def select_available_pool(
        self,
        preferred: str,
        allow_failover: bool = True,
        now: datetime | None = None,
    ) -> tuple[str | None, datetime | None]:
        now = now or _now_utc()
        self._refresh_expired_windows(now)

        if self.is_available(preferred, now):
            return preferred, None

        if allow_failover:
            for name in self.pools:
                if name != preferred and self.is_available(name, now):
                    return name, None

        # None available -> find earliest wake time
        wake_times: list[datetime] = []
        candidates = list(self.pools.keys()) if allow_failover else [preferred]
        for name in candidates:
            cd = self.cooldown_until(name)
            if cd is not None:
                wake_times.append(cd)
        return None, (min(wake_times) if wake_times else None)

    def select_balanced_pool(
        self,
        active_pools: list[str],
        task_tier: str = "fast",
        shift_pool_counts: dict[str, int] | None = None,
        now: datetime | None = None,
    ) -> tuple[str | None, datetime | None]:
        """Select optimal pool for balanced dual-pool operation:
        1) Warm-start both pools within the first 2 tasks of a shift so both 5h clocks start & refresh together.
        2) After both clocks are running, pick the available pool with highest remaining quota % (auto-proportional to real budgets).
        """
        now = now or _now_utc()
        self._refresh_expired_windows(now)

        candidates = [p for p in active_pools if p in self.pools]
        if not candidates:
            candidates = list(self.pools.keys())

        available = [p for p in candidates if self.is_available(p, now)]
        if not available:
            wake_times = [cd for p in candidates if (cd := self.cooldown_until(p)) is not None]
            return None, (min(wake_times) if wake_times else None)

        if len(available) == 1:
            return available[0], None

        counts = shift_pool_counts or {}
        # 1. Warm-start: if some pool has already run in this shift and another available pool has not,
        # ignite the unstarted pool right away so both 5h rolling timers stay synchronized.
        started_any = any(counts.get(p, 0) > 0 for p in available)
        unstarted = [p for p in available if counts.get(p, 0) == 0]
        if started_any and unstarted:
            best_unstarted = max(unstarted, key=lambda p: self.get_remaining_pct(p, now))
            return best_unstarted, None

        # 2. Highest remaining quota % wins (naturally balances ~4 Gemini : 1 Claude from calibrated budgets)
        scored: list[tuple[float, str]] = []
        for p in available:
            pct = self.get_remaining_pct(p, now)
            # Gentle task-affinity tie-breaker (only breaks close ties within 4%)
            bonus = 0.0
            if task_tier == "smart" and p.lower().startswith("claude"):
                bonus = 4.0
            elif task_tier == "fast" and p.lower().startswith("gemini"):
                bonus = 1.5
            scored.append((pct + bonus, p))

        scored.sort(key=lambda item: item[0], reverse=True)
        return scored[0][1], None

    def select_escalation_pool(
        self,
        attempt1_pool: str,
        active_pools: list[str],
        cross_pool: bool = True,
        min_other_pct: float = 15.0,
        now: datetime | None = None,
    ) -> str:
        """Pick pool for Attempt 2 repair: switch to another healthy model family when cross_pool is enabled."""
        now = now or _now_utc()
        self._refresh_expired_windows(now)

        candidates = [p for p in active_pools if p in self.pools] or list(self.pools.keys())
        if cross_pool:
            others = [
                p
                for p in candidates
                if p != attempt1_pool
                and self.is_available(p, now)
                and self.get_remaining_pct(p, now) >= min_other_pct
            ]
            if others:
                return max(others, key=lambda p: self.get_remaining_pct(p, now))

        if self.is_available(attempt1_pool, now):
            return attempt1_pool
        avail = [p for p in candidates if self.is_available(p, now)]
        return avail[0] if avail else attempt1_pool

    def select_visual_pool(
        self,
        active_pools: list[str],
        fallback_pool: str,
        prefer_gemini: bool = True,
        now: datetime | None = None,
    ) -> str:
        """Prefer cheap `gemini-3.8-flash-low` for visual QA whenever Gemini pool is available."""
        now = now or _now_utc()
        self._refresh_expired_windows(now)

        if prefer_gemini and "gemini" in self.pools and self.is_available("gemini", now):
            return "gemini"
        if self.is_available(fallback_pool, now):
            return fallback_pool
        for p in active_pools:
            if self.is_available(p, now):
                return p
        return fallback_pool

    def status_summary(self, now: datetime | None = None) -> dict[str, dict[str, Any]]:
        now = now or _now_utc()
        self._refresh_expired_windows(now)
        out: dict[str, dict[str, Any]] = {}
        for name, st in self._state.items():
            out[name] = {
                "available": self.is_available(name, now),
                "remaining_pct": self.get_remaining_pct(name, now),
                "window_tokens_used": int(st.get("window_tokens_used", 0)),
                "estimated_5h_budget": int(st.get("estimated_5h_budget", 0)),
                "cooldown_until": st.get("cooldown_until"),
            }
        return out
