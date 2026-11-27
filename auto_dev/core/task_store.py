from __future__ import annotations

import json
import os
from pathlib import Path

from .models import Task, utc_now_iso


class TaskStore:
    """Atomic JSON store for tasks.json with DAG validation and BOARD.md rendering."""

    def __init__(self, tasks_file: Path, board_file: Path) -> None:
        self.tasks_file = tasks_file
        self.board_file = board_file

    def load(self) -> list[Task]:
        if not self.tasks_file.exists():
            return []
        raw = json.loads(self.tasks_file.read_text(encoding="utf-8"))
        items = raw.get("tasks", []) if isinstance(raw, dict) else raw
        return [Task.from_dict(item) for item in items]

    def save(self, tasks: list[Task]) -> None:
        self.tasks_file.parent.mkdir(parents=True, exist_ok=True)
        payload = {
            "schema_version": 1,
            "updated_at": utc_now_iso(),
            "tasks": [t.to_dict() for t in tasks],
        }
        tmp = self.tasks_file.with_suffix(".json.tmp")
        tmp.write_text(json.dumps(payload, indent=2, ensure_ascii=False) + "\n", encoding="utf-8")
        os.replace(str(tmp), str(self.tasks_file))
        self.render_board(tasks)

    def next_task_id(self, tasks: list[Task]) -> str:
        max_num = 0
        for t in tasks:
            if t.id.upper().startswith("T") and t.id[1:].isdigit():
                max_num = max(max_num, int(t.id[1:]))
        return f"T{max_num + 1:03d}"

    def validate_dag(self, tasks: list[Task]) -> list[str]:
        errors: list[str] = []
        by_id = {t.id: t for t in tasks}
        for t in tasks:
            for dep in t.depends_on:
                if dep not in by_id:
                    errors.append(f"{t.id}: tanimsiz bagimlilik '{dep}'")
                elif dep == t.id:
                    errors.append(f"{t.id}: kendisine bagimli olamaz")

        state: dict[str, int] = {}  # 0=unvisited, 1=visiting, 2=done

        def dfs(tid: str) -> None:
            state[tid] = 1
            for dep in by_id[tid].depends_on:
                if dep not in by_id:
                    continue
                st = state.get(dep, 0)
                if st == 1:
                    errors.append(f"Dongusel bagimlilik tespit edildi: {tid} -> {dep}")
                elif st == 0:
                    dfs(dep)
            state[tid] = 2

        for t in tasks:
            if state.get(t.id, 0) == 0:
                dfs(t.id)
        return errors

    def next_ready_task(self, tasks: list[Task]) -> Task | None:
        status_by_id = {t.id: t.status for t in tasks}
        for t in tasks:
            if t.status != "pending":
                continue
            if all(status_by_id.get(dep) == "done" for dep in t.depends_on):
                return t
        return None

    def ready_tasks(self, tasks: list[Task]) -> list[Task]:
        status_by_id = {t.id: t.status for t in tasks}
        return [
            t
            for t in tasks
            if t.status == "pending"
            and all(status_by_id.get(dep) == "done" for dep in t.depends_on)
        ]

    def add_task(
        self,
        title: str,
        description: str,
        acceptance_criteria: list[str] | None = None,
        depends_on: list[str] | None = None,
        target_files: list[str] | None = None,
        context_files: list[str] | None = None,
        tier: str = "fast",
        visual_check: bool = False,
        verify_check: str = "",
        status: str = "draft",
        task_id: str | None = None,
    ) -> Task:
        tasks = self.load()
        tid = task_id or self.next_task_id(tasks)
        task = Task(
            id=tid,
            title=title.strip(),
            description=description.strip(),
            acceptance_criteria=list(acceptance_criteria or []),
            status=status,
            depends_on=list(depends_on or []),
            target_files=list(target_files or []),
            context_files=list(context_files or []),
            tier=tier if tier in ("fast", "smart") else "fast",
            visual_check=visual_check,
            verify_check=verify_check,
        )
        tasks.append(task)
        self.save(tasks)
        return task

    def approve(self, task_ids: list[str] | None = None, approve_all: bool = False) -> list[str]:
        tasks = self.load()
        changed: list[str] = []
        id_set = set(task_ids or [])
        for t in tasks:
            if t.status == "draft" and (approve_all or t.id in id_set):
                t.status = "pending"
                t.updated_at = utc_now_iso()
                changed.append(t.id)
        if changed:
            self.save(tasks)
        return changed

    def unblock(self, task_ids: list[str] | None = None, unblock_all: bool = False) -> list[str]:
        tasks = self.load()
        changed: list[str] = []
        id_set = set(task_ids or [])
        for t in tasks:
            if t.status == "blocked" and (unblock_all or t.id in id_set):
                t.status = "pending"
                t.retries = 0
                t.last_error = ""
                t.updated_at = utc_now_iso()
                changed.append(t.id)
        if changed:
            self.save(tasks)
        return changed

    def update_task(self, updated: Task) -> None:
        tasks = self.load()
        updated.updated_at = utc_now_iso()
        replaced = False
        for idx, existing in enumerate(tasks):
            if existing.id == updated.id:
                tasks[idx] = updated
                replaced = True
                break
        if not replaced:
            tasks.append(updated)
        self.save(tasks)

    def render_board(self, tasks: list[Task]) -> None:
        self.board_file.parent.mkdir(parents=True, exist_ok=True)
        counts = {
            "draft": sum(1 for t in tasks if t.status == "draft"),
            "pending": sum(1 for t in tasks if t.status == "pending"),
            "in_progress": sum(1 for t in tasks if t.status == "in_progress"),
            "done": sum(1 for t in tasks if t.status == "done"),
            "blocked": sum(1 for t in tasks if t.status == "blocked"),
            "skipped": sum(1 for t in tasks if t.status == "skipped"),
        }
        ready = {t.id for t in self.ready_tasks(tasks)}

        lines = [
            "# AutoDev Gorev Panosu (BOARD)",
            "",
            (
                f"- Ozet: **Toplam {len(tasks)}** | "
                f"Taslak (Onay Bekleyen): `{counts['draft']}` | "
                f"Sirada: `{counts['pending']}` (Calismaya Hazir: `{len(ready)}`) | "
                f"Tamamlanan: `{counts['done']}` | "
                f"Bloklanan: `{counts['blocked']}`"
            ),
            "",
        ]

        sections = [
            ("Onay Bekleyen Taslaklar (`draft`)", [t for t in tasks if t.status == "draft"]),
            ("Siradaki Gorevler (`pending`)", [t for t in tasks if t.status == "pending"]),
            ("Bloklanan Gorevler (`blocked`)", [t for t in tasks if t.status == "blocked"]),
            ("Tamamlanan Gorevler (`done`)", [t for t in tasks if t.status == "done"]),
        ]

        for heading, items in sections:
            lines.append(f"## {heading}")
            if not items:
                lines.append("_Yok._")
                lines.append("")
                continue
            lines.append("| ID | Baslik | Bagimlilik | Model | Gorsel | Durum / Not |")
            lines.append("| --- | --- | --- | --- | --- | --- |")
            for t in items:
                deps = ", ".join(t.depends_on) if t.depends_on else "-"
                vis = "Evet" if t.visual_check else "-"
                if t.status == "done":
                    note = f"Commit `{t.commit_sha}`" if t.commit_sha else "Tamamlandi"
                elif t.status == "blocked":
                    err_line = (t.last_error.splitlines()[0] if t.last_error else "Hata")[:90]
                    note = f"Deneme: {t.retries} — {err_line.replace('|', '/')}"
                elif t.status == "pending":
                    note = "Hazir" if t.id in ready else "Bagimlilik bekliyor"
                else:
                    note = "`approve` bekliyor"
                safe_title = t.title.replace("|", "/")
                lines.append(
                    f"| `{t.id}` | {safe_title} | {deps} | `{t.tier}` | {vis} | {note} |"
                )
            lines.append("")

        content = "\n".join(lines)
        if not self.board_file.exists() or self.board_file.read_text(encoding="utf-8", errors="ignore") != content:
            self.board_file.write_text(content, encoding="utf-8")
