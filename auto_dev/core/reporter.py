from __future__ import annotations

import json
from datetime import datetime, timedelta, timezone
from pathlib import Path
from typing import Any

from .models import ModelCallMetric, TaskRunRecord, TokenUsage


def _fmt_dur(sec: float) -> str:
    sec = max(0.0, float(sec))
    if sec < 60:
        return f"{sec:.1f}s"
    mins = int(sec // 60)
    rem = int(round(sec % 60))
    return f"{mins}dk {rem:02d}s"


def _fmt_tokens(n: int) -> str:
    if n >= 1_000_000:
        return f"{n / 1_000_000:.2f}M"
    if n >= 1_000:
        return f"{n / 1_000:.1f}K"
    return str(n)


def _aggregate_models_from_records(
    records: list[dict[str, Any]],
    extra_calls: list[dict[str, Any]] | None = None,
) -> list[dict[str, Any]]:
    agg: dict[str, dict[str, Any]] = {}
    total_dur = 0.0
    total_tok = 0

    def add_call(pool: str, model: str, dur: float, tok: int) -> None:
        nonlocal total_dur, total_tok
        key = f"{pool}::{model}"
        if key not in agg:
            agg[key] = {
                "pool": pool,
                "model": model,
                "calls": 0,
                "duration_sec": 0.0,
                "tokens": 0,
            }
        agg[key]["calls"] += 1
        agg[key]["duration_sec"] += float(dur)
        agg[key]["tokens"] += int(tok)
        total_dur += float(dur)
        total_tok += int(tok)

    for rec in records:
        for att in rec.get("attempts", []):
            calls = att.get("model_calls", [])
            if calls:
                for c in calls:
                    u = c.get("usage", {})
                    add_call(
                        str(c.get("pool", att.get("pool", "gemini"))),
                        str(c.get("model", att.get("model", "?"))),
                        float(c.get("duration_sec", 0.0)),
                        int(u.get("total_tokens", 0) or 0),
                    )
            else:
                u = att.get("usage", {})
                add_call(
                    str(att.get("pool", rec.get("pool", "gemini"))),
                    str(att.get("model", "?")),
                    float(att.get("duration_sec", 0.0)),
                    int(u.get("total_tokens", 0) or 0),
                )

    for c in extra_calls or []:
        u = c.get("usage", {})
        add_call(
            str(c.get("pool", "gemini")),
            str(c.get("model", "?")),
            float(c.get("duration_sec", 0.0)),
            int(u.get("total_tokens", 0) or 0),
        )

    out: list[dict[str, Any]] = []
    for item in sorted(agg.values(), key=lambda x: x["duration_sec"], reverse=True):
        dur_pct = (item["duration_sec"] / total_dur * 100.0) if total_dur > 0 else 0.0
        tok_pct = (item["tokens"] / total_tok * 100.0) if total_tok > 0 else dur_pct
        out.append(
            {
                "pool": item["pool"],
                "model": item["model"],
                "calls": item["calls"],
                "duration_sec": round(item["duration_sec"], 1),
                "duration_pct": round(dur_pct, 1),
                "tokens": item["tokens"],
                "token_pct": round(tok_pct, 1),
            }
        )
    return out


class RunReporter:
    """Writes per-run JSON telemetry, rolling RUN_REPORT.md, and detailed MORNING_BRIEF.md."""

    def __init__(
        self,
        runtime_dir: Path,
        report_file: Path,
        project_name: str,
        brief_file: Path | None = None,
    ) -> None:
        self.runs_dir = runtime_dir / "runs"
        self.report_file = report_file
        self.brief_file = brief_file or (report_file.parent / "MORNING_BRIEF.md")
        self.project_name = project_name

    def save_run(
        self,
        run_id: str,
        records: list[TaskRunRecord],
        stop_reason: str,
        started_at: str,
        finished_at: str,
        shift_name: str = "",
        initial_pool: str = "gemini",
        pool_status_after: dict[str, Any] | None = None,
        extra_calls: list[ModelCallMetric] | None = None,
        auto_planned_ids: list[str] | None = None,
        pushed_to_remote: bool = False,
    ) -> Path:
        self.runs_dir.mkdir(parents=True, exist_ok=True)
        total_usage = TokenUsage()
        for r in records:
            total_usage = total_usage.add(r.total_usage)
        for c in extra_calls or []:
            total_usage = total_usage.add(c.usage)

        rec_dicts = [r.to_dict() for r in records]
        extra_dicts = [c.to_dict() for c in (extra_calls or [])]
        session_models = _aggregate_models_from_records(rec_dicts, extra_dicts)

        payload: dict[str, Any] = {
            "run_id": run_id,
            "project_name": self.project_name,
            "shift_name": shift_name or f"Run ({initial_pool})",
            "initial_pool": initial_pool,
            "started_at": started_at,
            "finished_at": finished_at,
            "stop_reason": stop_reason,
            "pushed_to_remote": pushed_to_remote,
            "auto_planned_ids": list(auto_planned_ids or []),
            "summary": {
                "total": len(records),
                "done": sum(1 for r in records if r.outcome == "done"),
                "blocked": sum(1 for r in records if r.outcome == "blocked"),
                "deferred_quota": sum(1 for r in records if r.outcome == "deferred_quota"),
                "total_duration_sec": round(sum(r.duration_sec for r in records), 2),
                "usage": total_usage.to_dict(),
            },
            "session_model_breakdown": session_models,
            "pool_status_after": pool_status_after or {},
            "extra_calls": extra_dicts,
            "tasks": rec_dicts,
        }
        out_path = self.runs_dir / f"{run_id}.json"
        out_path.write_text(json.dumps(payload, indent=2, ensure_ascii=False) + "\n", encoding="utf-8")
        self._render_markdown()
        self._render_morning_brief(pool_status_after or {})
        return out_path

    def _render_markdown(self, max_runs: int = 20) -> None:
        run_files = sorted(self.runs_dir.glob("run_*.json"), reverse=True)[:max_runs]
        lines = [
            f"# AutoDev Kosu Raporu — {self.project_name}",
            "",
            "| Kosu ID | Vardiya | Tarih (UTC) | Biten | Ertelenen (Kota) | Blok | Sure | Token | Bitis Nedeni |",
            "| --- | --- | --- | --- | --- | --- | --- | --- | --- |",
        ]

        recent_details: list[str] = []
        for idx, rfile in enumerate(run_files):
            try:
                data = json.loads(rfile.read_text(encoding="utf-8"))
            except Exception:
                continue
            s = data.get("summary", {})
            u = s.get("usage", {})
            tok_total = int(u.get("total_tokens", 0) or 0)
            lines.append(
                f"| `{data.get('run_id')}` | `{data.get('shift_name', '-')}` | "
                f"`{data.get('started_at')}` | `{s.get('done', 0)}` | "
                f"`{s.get('deferred_quota', 0)}` | `{s.get('blocked', 0)}` | "
                f"`{_fmt_dur(s.get('total_duration_sec', 0))}` | `{_fmt_tokens(tok_total)}` | "
                f"{data.get('stop_reason', '')} |"
            )

            if idx < 6:
                recent_details.append(
                    f"### `{data.get('run_id')}` — {data.get('shift_name', '')} ({data.get('started_at')})"
                )
                s_models = data.get("session_model_breakdown", [])
                if s_models:
                    parts = [
                        f"`{m['model']}` ({m['duration_pct']}% sure / {_fmt_dur(m['duration_sec'])}, "
                        f"{m['token_pct']}% tok / {_fmt_tokens(m['tokens'])})"
                        for m in s_models
                    ]
                    recent_details.append("- **Oturum Model Dagilimi:** " + " + ".join(parts))

                pool_after = data.get("pool_status_after", {})
                if pool_after:
                    p_parts = [
                        f"`{pname}`: **%{info.get('remaining_pct', '?')}** kalan"
                        for pname, info in pool_after.items()
                    ]
                    recent_details.append("- **Oturum Sonu Havuz Kalan Kota (Tahmini):** " + " | ".join(p_parts))

                tasks = data.get("tasks", [])
                if not tasks:
                    recent_details.append("- Bu kosuda calistirilacak uygun gorev yoktu.")
                for t in tasks:
                    outcome = str(t.get("outcome", "unknown")).upper()
                    sha = f" (`{t['commit_sha']}`)" if t.get("commit_sha") else ""
                    mb = t.get("model_breakdown", [])
                    if mb:
                        m_str = " + ".join(
                            f"`{m['model']}` %{m['duration_pct']} ({_fmt_dur(m['duration_sec'])})"
                            for m in mb
                        )
                    else:
                        m_str = ", ".join(a.get("model", "?") for a in t.get("attempts", []))
                    pct_after = t.get("pool_remaining_pct_after", {})
                    pct_str = (
                        " | Havuz kalan: "
                        + ", ".join(f"`{k}` %{v}" for k, v in pct_after.items())
                        if pct_after
                        else ""
                    )
                    recent_details.append(
                        f"- **{t.get('task_id')}** — {t.get('title')}: **{outcome}**{sha} "
                        f"[{_fmt_dur(t.get('duration_sec', 0))} | {m_str}{pct_str}]"
                    )
                recent_details.append("")

        lines.append("")
        if recent_details:
            lines.append("## Son Kosu Detaylari")
            lines.append("")
            lines.extend(recent_details)

        self.report_file.parent.mkdir(parents=True, exist_ok=True)
        self.report_file.write_text("\n".join(lines), encoding="utf-8")

    def _render_morning_brief(self, latest_pool_status: dict[str, Any]) -> None:
        """Generates a crisp morning briefing covering all shifts from the last 16 hours."""
        now_utc = datetime.now(timezone.utc)
        cutoff = now_utc - timedelta(hours=16)

        run_files = sorted(self.runs_dir.glob("run_*.json"))
        recent_runs: list[dict[str, Any]] = []
        for rfile in run_files:
            try:
                data = json.loads(rfile.read_text(encoding="utf-8"))
                st = datetime.fromisoformat(str(data.get("started_at", "")))
                if st.tzinfo is None:
                    st = st.replace(tzinfo=timezone.utc)
                if st >= cutoff:
                    recent_runs.append(data)
            except Exception:
                continue

        if not recent_runs and run_files:
            try:
                recent_runs.append(json.loads(run_files[-1].read_text(encoding="utf-8")))
            except Exception:
                pass

        all_tasks: list[tuple[str, dict[str, Any]]] = []
        all_extra: list[dict[str, Any]] = []
        auto_planned: list[str] = []
        pushed_any = False

        for r in recent_runs:
            sname = str(r.get("shift_name", r.get("run_id", "")))
            for t in r.get("tasks", []):
                all_tasks.append((sname, t))
            all_extra.extend(r.get("extra_calls", []))
            auto_planned.extend(r.get("auto_planned_ids", []))
            if r.get("pushed_to_remote"):
                pushed_any = True

        done_tasks = [t for _, t in all_tasks if t.get("outcome") == "done"]
        deferred_tasks = [t for _, t in all_tasks if t.get("outcome") == "deferred_quota"]
        blocked_tasks = [t for _, t in all_tasks if t.get("outcome") == "blocked"]

        lines = [
            f"# AutoDev Sabah Ozet Raporu (MORNING_BRIEF) — {self.project_name}",
            "",
            f"- Guncelleme (UTC): `{now_utc.replace(microsecond=0).isoformat()}`",
            (
                f"- Son 16 Saat Ozeti: **Tamamlanan `{len(done_tasks)}`** | "
                f"Kota Ertelenen (`pending` korundu): `{len(deferred_tasks)}` | "
                f"Bloklanan: `{len(blocked_tasks)}` | "
                f"Otomatik Push (`origin/auto-dev`): `{'Evet' if pushed_any else 'Hayir'}`"
            ),
        ]
        if auto_planned:
            lines.append(f"- Otomatik Uretilen Yeni Gorevler: `{', '.join(auto_planned)}`")
        lines.append("")

        # 1. Havuz Kalan Kota Ozeti
        lines.append("## 1. Kota Havuzlari Son Durumu (5 Saatlik Pencere — Tahmini)")
        lines.append("| Havuz | Durum | Kalan Kota (%) | Bu Pencerede Harcanan Token | Soguma / Yenilenme |")
        lines.append("| --- | --- | --- | --- | --- |")
        pool_info = latest_pool_status
        if not pool_info and recent_runs:
            pool_info = recent_runs[-1].get("pool_status_after", {})
        if pool_info:
            for pname, info in pool_info.items():
                avail = "Musait" if info.get("available", True) else "Kota Doldu (Sogumada)"
                rem = info.get("remaining_pct", 100.0)
                used = int(info.get("window_tokens_used", 0) or 0)
                cd = info.get("cooldown_until") or "-"
                lines.append(
                    f"| `{pname}` | {avail} | **%{rem}** | `{_fmt_tokens(used)}` | `{cd}` |"
                )
        else:
            lines.append("| - | - | - | - | - |")
        lines.append("")

        # 2. Oturum (Vardiya) Bazli Model Dagilimi
        lines.append("## 2. Oturum / Vardiya Bazli Model Kullanim Yuzdeleri ve Sureleri")
        for r in recent_runs:
            sname = r.get("shift_name", r.get("run_id"))
            s = r.get("summary", {})
            lines.append(
                f"### {sname} (`{r.get('run_id')}` — Bitis: `{r.get('stop_reason', '')}`)"
            )
            models = r.get("session_model_breakdown", [])
            if not models:
                lines.append("_Bu oturumda model cagrisi yapilmadi._")
                lines.append("")
                continue
            lines.append("| Havuz | Model | Cagri | Sure | Sure Payi (%) | Token | Token Payi (%) |")
            lines.append("| --- | --- | --- | --- | --- | --- | --- |")
            for m in models:
                lines.append(
                    f"| `{m['pool']}` | `{m['model']}` | `{m['calls']}` | "
                    f"`{_fmt_dur(m['duration_sec'])}` | **%{m['duration_pct']}** | "
                    f"`{_fmt_tokens(m['tokens'])}` | **%{m['token_pct']}** |"
                )
            p_after = r.get("pool_status_after", {})
            if p_after:
                rem_txt = " | ".join(f"`{k}`: **%{v.get('remaining_pct', '?')}**" for k, v in p_after.items())
                lines.append(f"\n> Oturum sonu havuz kalan tahmini kota: {rem_txt}")
            lines.append("")

        # 3. Gorev Bazli Detay Tablosu
        lines.append("## 3. Gorev Bazli Model Dagilimi, Sure ve Gorev Sonu Kalan Kota")
        if not all_tasks:
            lines.append("_Son 16 saatte calistirilan gorev yok._")
        else:
            lines.append("| Gorev | Vardiya | Sonuc | Commit | Kullanilan Modeller (% Sure & Sure) | Toplam Sure | Token | Gorev Sonu Havuz Kalan (%) |")
            lines.append("| --- | --- | --- | --- | --- | --- | --- | --- |")
            for sname, t in all_tasks:
                mb = t.get("model_breakdown", [])
                m_desc = "<br>".join(
                    f"`{m['model']}`: **%{m['duration_pct']}** ({_fmt_dur(m['duration_sec'])}, {_fmt_tokens(m['tokens'])})"
                    for m in mb
                ) or "-"
                rem_after = t.get("pool_remaining_pct_after", {})
                rem_desc = " / ".join(f"`{k}`: %{v}" for k, v in rem_after.items()) or "-"
                tok_tot = int(t.get("total_usage", {}).get("total_tokens", 0) or 0)
                sha = f"`{t['commit_sha']}`" if t.get("commit_sha") else "-"
                safe_title = str(t.get("title", "")).replace("|", "/")[:42]
                lines.append(
                    f"| `{t.get('task_id')}` {safe_title} | `{sname}` | **{str(t.get('outcome','')).upper()}** | "
                    f"{sha} | {m_desc} | `{_fmt_dur(t.get('duration_sec', 0))}` | "
                    f"`{_fmt_tokens(tok_tot)}` | {rem_desc} |"
                )
        lines.append("")

        self.brief_file.parent.mkdir(parents=True, exist_ok=True)
        self.brief_file.write_text("\n".join(lines), encoding="utf-8")
