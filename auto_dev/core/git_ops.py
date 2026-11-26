from __future__ import annotations

import os
import subprocess
from pathlib import Path
from typing import Iterable

from .config import GitConfig
from .models import GateResult, Task


class GitError(RuntimeError):
    """Raised when a Git command or safety invariant fails."""


class GitOps:
    """Deterministic Git controller with protected-branch safety and task-branch isolation."""

    def __init__(self, repo_root: Path, cfg: GitConfig) -> None:
        self.repo_root = repo_root
        self.cfg = cfg

    def run(self, args: list[str], check: bool = True) -> subprocess.CompletedProcess[str]:
        proc = subprocess.run(
            ["git", *args],
            cwd=str(self.repo_root),
            capture_output=True,
            text=True,
            encoding="utf-8",
            errors="replace",
        )
        if check and proc.returncode != 0:
            msg = (proc.stderr or proc.stdout).strip()
            raise GitError(f"git {' '.join(args)} basarisiz (kod {proc.returncode}): {msg}")
        return proc

    def current_branch(self) -> str:
        return self.run(["rev-parse", "--abbrev-ref", "HEAD"]).stdout.strip()

    def head_sha(self, short: bool = True) -> str:
        args = ["rev-parse", "--short", "HEAD"] if short else ["rev-parse", "HEAD"]
        return self.run(args).stdout.strip()

    def branch_exists(self, branch: str) -> bool:
        proc = self.run(["show-ref", "--verify", "--quiet", f"refs/heads/{branch}"], check=False)
        return proc.returncode == 0

    def status_porcelain(self) -> list[str]:
        out = self.run(["status", "--porcelain"]).stdout
        return [line for line in out.splitlines() if line.strip()]

    def is_clean(self) -> bool:
        return len(self.status_porcelain()) == 0

    def ensure_safe_dev_branch(self) -> None:
        if self.cfg.dev_branch in self.cfg.protected_branches:
            raise GitError(
                f"Guvenlik ihlali: dev_branch ('{self.cfg.dev_branch}') korumali dallar listesinde!"
            )
        cur = self.current_branch()
        if cur != self.cfg.dev_branch:
            if not self.is_clean():
                raise GitError(
                    f"Calisma dizini kirli ({cur}). '{self.cfg.dev_branch}' dalina gecmeden once temizleyin."
                )
            if self.branch_exists(self.cfg.dev_branch):
                self.run(["checkout", self.cfg.dev_branch])
            else:
                self.run(["checkout", "-b", self.cfg.dev_branch])

        if not self.is_clean():
            dirty = "\n".join(self.status_porcelain()[:15])
            raise GitError(
                f"'{self.cfg.dev_branch}' dalinda kaydedilmemis degisiklikler var:\n{dirty}"
            )

    def task_branch_name(self, task_id: str) -> str:
        safe = "".join(ch if ch.isalnum() or ch in ("-", "_") else "-" for ch in task_id.lower())
        return f"{self.cfg.task_branch_prefix}{safe}"

    def start_task_branch(self, task_id: str) -> str:
        self.ensure_safe_dev_branch()
        branch = self.task_branch_name(task_id)
        if self.branch_exists(branch):
            self.run(["branch", "-D", branch])
        self.run(["checkout", "-b", branch, self.cfg.dev_branch])
        return branch

    def changed_files(self, base_ref: str | None = None) -> list[str]:
        files: set[str] = set()
        if base_ref:
            diff_out = self.run(["diff", "--name-only", base_ref], check=False).stdout
            for line in diff_out.splitlines():
                if line.strip():
                    files.add(line.strip().replace("\\", "/"))
        else:
            for args in (["diff", "--name-only"], ["diff", "--cached", "--name-only"]):
                out = self.run(args, check=False).stdout
                for line in out.splitlines():
                    if line.strip():
                        files.add(line.strip().replace("\\", "/"))

        untracked = self.run(["ls-files", "--others", "--exclude-standard"], check=False).stdout
        for line in untracked.splitlines():
            if line.strip():
                files.add(line.strip().replace("\\", "/"))
        return sorted(files)

    def diff_line_count(self, base_ref: str | None = None) -> int:
        args = ["diff", "--numstat"]
        if base_ref:
            args.append(base_ref)
        out = self.run(args, check=False).stdout
        total = 0
        for line in out.splitlines():
            parts = line.split("\t")
            if len(parts) >= 2:
                for val in parts[:2]:
                    if val.isdigit():
                        total += int(val)
        # Include untracked text files roughly
        untracked = self.run(["ls-files", "--others", "--exclude-standard"], check=False).stdout
        for rel in untracked.splitlines():
            rel = rel.strip()
            if not rel:
                continue
            path = self.repo_root / rel
            if path.is_file() and path.stat().st_size < 512_000:
                try:
                    total += len(path.read_text(encoding="utf-8", errors="ignore").splitlines())
                except OSError:
                    pass
        return total

    def diff_stat_summary(self, base_ref: str | None = None) -> str:
        args = ["diff", "--stat"]
        if base_ref:
            args.append(base_ref)
        stat = self.run(args, check=False).stdout.strip()
        untracked = self.run(["ls-files", "--others", "--exclude-standard"], check=False).stdout.strip()
        parts = [p for p in (stat, f"Untracked:\n{untracked}" if untracked else "") if p]
        return "\n".join(parts)

    def validate_agent_diff(
        self,
        allowed_prefixes: Iterable[str],
        forbidden_extensions: Iterable[str],
        max_files: int,
        max_lines: int,
        base_ref: str | None = None,
        forbidden_check_files: Iterable[str] | None = None,
    ) -> GateResult:
        files = self.changed_files(base_ref=base_ref)
        if not files:
            return GateResult(
                name="scope_diff_guard",
                passed=False,
                summary="Ajan hicbir dosya degistirmedi.",
                error_excerpt="No files were modified or created for this task.",
            )

        violations: list[str] = []
        allowed = [p.replace("\\", "/").lstrip("./") for p in allowed_prefixes if p]
        forbidden = {e.lower() for e in forbidden_extensions if e}
        check_forbidden_set = (
            {f.replace("\\", "/") for f in forbidden_check_files}
            if forbidden_check_files is not None
            else set(files)
        )

        for rel in files:
            norm = rel.replace("\\", "/")
            ext = os.path.splitext(norm)[1].lower()
            if norm in check_forbidden_set and ext in forbidden:
                violations.append(
                    f"Yasakli dosya uzantisi dogrudan duzenlendi: {norm} (YAML/binary dosyalar yalnizca arac/API ile degistirilebilir)"
                )
            if allowed and not any(norm.startswith(pref) for pref in allowed):
                violations.append(f"Izin verilen kapsam disinda dosya degistirildi: {norm}")

        if len(files) > max_files:
            violations.append(
                f"Degisen dosya sayisi limiti asildi: {len(files)} > {max_files}"
            )

        line_count = self.diff_line_count(base_ref=base_ref)
        if line_count > max_lines:
            violations.append(
                f"Degisen satir sayisi limiti asildi: {line_count} > {max_lines}"
            )

        if violations:
            return GateResult(
                name="scope_diff_guard",
                passed=False,
                summary=f"Kapsam/diff korumasi basarisiz ({len(violations)} ihlal).",
                error_excerpt="\n".join(violations[:12]),
            )

        return GateResult(
            name="scope_diff_guard",
            passed=True,
            summary=f"Diff uygun ({len(files)} dosya, ~{line_count} satir).",
        )

    def commit_and_merge_task(self, task: Task, detail_summary: str = "") -> str:
        branch = self.task_branch_name(task.id)
        if self.current_branch() != branch:
            raise GitError(f"Beklenen gorev dali '{branch}', mevcut dal '{self.current_branch()}'")

        self.run(["add", "-A"])
        if self.is_clean():
            raise GitError(f"Gorev {task.id} icin commitlenecek degisiklik yok.")

        commit_title = f"{self.cfg.commit_prefix}({task.id}): {task.title.strip()}"
        commit_args = ["commit", "-m", commit_title]
        if detail_summary.strip():
            commit_args.extend(["-m", detail_summary.strip()[:500]])
        self.run(commit_args)

        self.run(["checkout", self.cfg.dev_branch])
        self.run(["merge", "--ff-only", branch])
        sha = self.head_sha(short=True)
        self.run(["branch", "-d", branch])
        return sha

    def rollback_task_branch(self, task_id: str) -> None:
        branch = self.task_branch_name(task_id)
        # Discard all tracked and untracked (non-ignored) edits; keeps ignored .autodev/ intact!
        self.run(["reset", "--hard"], check=False)
        self.run(["clean", "-fd"], check=False)
        if self.current_branch() != self.cfg.dev_branch:
            self.run(["checkout", self.cfg.dev_branch])
        if self.branch_exists(branch):
            self.run(["branch", "-D", branch], check=False)

    def commit_state_files(self, paths: list[Path], message: str) -> str | None:
        if not self.cfg.state_commit_enabled:
            return None
        if self.current_branch() != self.cfg.dev_branch:
            raise GitError(
                f"Durum dosyalari yalnizca '{self.cfg.dev_branch}' dalinda commitlenebilir."
            )
        rel_paths: list[str] = []
        for path in paths:
            if path.exists():
                rel_paths.append(str(path.resolve().relative_to(self.repo_root.resolve())))
        if not rel_paths:
            return None
        self.run(["add", "--", *rel_paths])
        staged = self.run(["diff", "--cached", "--name-only"]).stdout.strip()
        if not staged:
            return None
        self.run(["commit", "-m", message])
        return self.head_sha(short=True)

    def push_dev_branch(self) -> None:
        if not self.cfg.auto_push:
            return
        if self.cfg.dev_branch in self.cfg.protected_branches:
            raise GitError("Korumali dala otomatik push yasaktir.")
        self.run(["push", "-u", "origin", self.cfg.dev_branch])
