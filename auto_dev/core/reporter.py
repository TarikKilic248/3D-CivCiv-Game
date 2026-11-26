from __future__ import annotations

import json
from pathlib import Path
from typing import Any

from .models import TaskRunRecord


class RunReporter:
    """Writes per-run JSON telemetry and maintains rolling RUN_REPORT.md."""

    def __init__(self, runtime_dir: Path, report_file: Path, project_name: str) -> None:
        self.runs_dir = runtime_dir / "runs"
        self.report_file = report_file
        self.project_name = project_name

    def save_run(
        self,
        run_id: str,
        records: list[TaskRunRecord],
        stop_reason: str,
        started_at: str,
        finished_at: str,
    ) -> Path:
        self.runs_dir.mkdir(parents=True, exist_ok=True)
        payload: dict[str, Any] = {
            "run_id": run_id,
            "project_name": self.project_name,
            "started_at": started_at,
            "finished_at": finished_at,
            "stop_reason": stop_reason,
            "summary": {
                "total": len(records),
                "done": sum(1 for r in records if r.outcome == "done"),
                "blocked": sum(1 for r in records if r.outcome == "blocked"),
                "total_duration_sec": round(sum(r.duration_sec for r in records), 2),
            },
            "tasks": [r.to_dict() for r in records],
        }
        out_path = self.runs_dir / f"{run_id}.json"
        out_path.write_text(json.dumps(payload, indent=2, ensure_ascii=False) + "\n", encoding="utf-8")
        self._render_markdown()
        return out_path

    def _render_markdown(self, max_runs: int = 20) -> None:
        run_files = sorted(self.runs_dir.glob("run_*.json"), reverse=True)[:max_runs]
        lines = [
            f"# AutoDev Kosu Raporu — {self.project_name}",
            "",
            "| Kosu ID | Tarih (UTC) | Tamamlanan | Bloklanan | Sure (sn) | Bitis Nedeni |",
            "| --- | --- | --- | --- | --- | --- |",
        ]

        recent_details: list[str] = []
        for idx, rfile in enumerate(run_files):
            try:
                data = json.loads(rfile.read_text(encoding="utf-8"))
            except Exception:
                continue
            s = data.get("summary", {})
            lines.append(
                f"| `{data.get('run_id')}` | `{data.get('started_at')}` | "
                f"`{s.get('done', 0)}` | `{s.get('blocked', 0)}` | "
                f"`{s.get('total_duration_sec', 0)}` | {data.get('stop_reason', '')} |"
            )

            if idx < 5:
                recent_details.append(f"### Kosu `{data.get('run_id')}` ({data.get('started_at')})")
                tasks = data.get("tasks", [])
                if not tasks:
                    recent_details.append("- Bu kosuda calistirilacak uygun gorev yoktu.")
                for t in tasks:
                    outcome = t.get("outcome", "unknown").upper()
                    sha = f" (`{t['commit_sha']}`)" if t.get("commit_sha") else ""
                    models_used = ", ".join(a.get("model", "?") for a in t.get("attempts", []))
                    recent_details.append(
                        f"- **{t.get('task_id')}** — {t.get('title')}: **{outcome}**{sha} "
                        f"[{t.get('duration_sec', 0)}s | modeller: `{models_used}`]"
                    )
                    if outcome != "DONE" and t.get("attempts"):
                        last_att = t["attempts"][-1]
                        err = last_att.get("error", "")
                        if not err:
                            for g in last_att.get("gates", []):
                                if not g.get("passed"):
                                    err = g.get("summary", "")
                                    break
                        if err:
                            recent_details.append(f"  - Hata ozeti: `{err[:180]}`")
                recent_details.append("")

        lines.append("")
        if recent_details:
            lines.append("## Son Kosu Detaylari")
            lines.append("")
            lines.extend(recent_details)

        self.report_file.parent.mkdir(parents=True, exist_ok=True)
        self.report_file.write_text("\n".join(lines), encoding="utf-8")
