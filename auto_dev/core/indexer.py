from __future__ import annotations

import ast
import os
import re
from pathlib import Path

from .config import ScopeConfig
from .models import Task, utc_now_iso

_CS_TYPE_RE = re.compile(
    r"^\s*(?:public|internal|protected|private)?\s*(?:static\s+|abstract\s+|sealed\s+|partial\s+)*"
    r"(class|interface|struct|enum)\s+([A-Za-z_][A-Za-z0-9_]*)(?:\s*:\s*([^\r\n{]+))?",
    re.MULTILINE,
)
_CS_METHOD_RE = re.compile(
    r"^\s*(?:public|protected)\s+(?:static\s+|virtual\s+|override\s+|async\s+)*"
    r"(?!class\b|struct\b|interface\b|enum\b)([A-Za-z0-9_<>\[\],\.\?]+)\s+([A-Za-z_][A-Za-z0-9_]*)\s*\(([^)]*)\)",
    re.MULTILINE,
)
_CS_EVENT_RE = re.compile(
    r"^\s*public\s+(?:static\s+)?event\s+([A-Za-z0-9_<>,\.\s]+?)\s+([A-Za-z_][A-Za-z0-9_]*)\s*;",
    re.MULTILINE,
)
_CS_SERIALIZE_RE = re.compile(
    r"\[SerializeField\]\s*(?:private|protected|public)?\s*([A-Za-z0-9_<>\[\]\.\?]+)\s+([A-Za-z_][A-Za-z0-9_]*)",
    re.MULTILINE,
)
_JS_TS_EXPORT_RE = re.compile(
    r"^\s*export\s+(?:default\s+)?(?:async\s+)?(class|interface|type|function|const)\s+([A-Za-z_][A-Za-z0-9_]*)",
    re.MULTILINE,
)
_GO_FUNC_RE = re.compile(
    r"^\s*(?:func\s+(?:\([^)]+\)\s+)?([A-Za-z_][A-Za-z0-9_]*)\s*\(|type\s+([A-Za-z_][A-Za-z0-9_]*)\s+(?:struct|interface))",
    re.MULTILINE,
)


def _summarize_cs(text: str) -> list[str]:
    summary: list[str] = []
    types = [
        f"{m.group(1)} {m.group(2)}" + (f": {m.group(3).strip()}" if m.group(3) else "")
        for m in _CS_TYPE_RE.finditer(text)
    ]
    if types:
        summary.append("types: " + ", ".join(types[:6]))

    events = [m.group(2) for m in _CS_EVENT_RE.finditer(text)]
    if events:
        summary.append("events: " + ", ".join(events[:8]))

    methods: list[str] = []
    for m in _CS_METHOD_RE.finditer(text):
        name = m.group(2)
        if name in ("if", "for", "while", "switch", "catch"):
            continue
        params = re.sub(r"\s+", " ", m.group(3).strip())
        if len(params) > 40:
            params = params[:37] + "..."
        methods.append(f"{name}({params})")
    if methods:
        summary.append("api: " + ", ".join(methods[:10]))

    fields = [m.group(2) for m in _CS_SERIALIZE_RE.finditer(text)]
    if fields:
        summary.append("fields: " + ", ".join(fields[:10]))
    return summary


def _summarize_py(text: str) -> list[str]:
    try:
        tree = ast.parse(text)
    except SyntaxError:
        return []
    classes: list[str] = []
    funcs: list[str] = []
    for node in tree.body:
        if isinstance(node, ast.ClassDef):
            methods = [
                n.name
                for n in node.body
                if isinstance(n, (ast.FunctionDef, ast.AsyncFunctionDef))
                and not n.name.startswith("_")
            ][:6]
            suffix = f"({', '.join(methods)})" if methods else ""
            classes.append(f"{node.name}{suffix}")
        elif isinstance(node, (ast.FunctionDef, ast.AsyncFunctionDef)):
            if not node.name.startswith("_"):
                funcs.append(f"{node.name}()")
    out: list[str] = []
    if classes:
        out.append("classes: " + ", ".join(classes[:8]))
    if funcs:
        out.append("funcs: " + ", ".join(funcs[:10]))
    return out


def _summarize_generic(ext: str, text: str) -> list[str]:
    if ext == ".cs":
        return _summarize_cs(text)
    if ext == ".py":
        return _summarize_py(text)
    if ext in (".ts", ".tsx", ".js", ".jsx"):
        items = [f"{m.group(1)} {m.group(2)}" for m in _JS_TS_EXPORT_RE.finditer(text)]
        return ["exports: " + ", ".join(items[:10])] if items else []
    if ext == ".go":
        items = [m.group(1) or m.group(2) for m in _GO_FUNC_RE.finditer(text)]
        return ["symbols: " + ", ".join(items[:10])] if items else []
    return []


class CodeIndexer:
    """Zero-token compact codebase symbol indexer to minimize LLM exploration tokens."""

    def __init__(self, repo_root: Path, scope: ScopeConfig, max_index_chars: int = 14000) -> None:
        self.repo_root = repo_root
        self.scope = scope
        self.max_index_chars = max_index_chars

    def discover_files(self) -> list[Path]:
        excluded = {d.lower() for d in self.scope.index_exclude_dirs}
        exts = {e.lower() for e in self.scope.index_extensions}
        found: list[Path] = []
        seen: set[Path] = set()

        for rel_root in self.scope.index_include_roots:
            base = (self.repo_root / rel_root).resolve()
            if not base.exists():
                continue
            for dirpath, dirnames, filenames in os.walk(base):
                dirnames[:] = sorted(
                    d for d in dirnames if d.lower() not in excluded and not d.startswith(".")
                )
                for fname in sorted(filenames):
                    if os.path.splitext(fname)[1].lower() not in exts:
                        continue
                    full = Path(dirpath) / fname
                    if full not in seen:
                        seen.add(full)
                        found.append(full)
        return sorted(found)

    def build_index(self, output_path: Path) -> str:
        files = self.discover_files()
        body_lines = [
            "# Compact Project Code Index (Auto-Generated, 0 Tokens)",
            f"Indexed files: `{len(files)}`",
            "",
        ]
        lines = list(body_lines)

        char_count = sum(len(l) + 1 for l in lines)
        truncated = False

        for fpath in files:
            rel = fpath.relative_to(self.repo_root).as_posix()
            ext = fpath.suffix.lower()
            try:
                text = fpath.read_text(encoding="utf-8", errors="ignore")
            except OSError:
                continue
            line_cnt = text.count("\n") + 1
            parts = _summarize_generic(ext, text)
            detail = " | ".join(parts) if parts else f"{line_cnt} lines"
            entry = f"- `{rel}` ({line_cnt}L): {detail}"
            if char_count + len(entry) + 1 > self.max_index_chars:
                truncated = True
                break
            lines.append(entry)
            char_count += len(entry) + 1

        if truncated:
            lines.append("- _... (index truncated by max_index_chars limit)_")

        content = "\n".join(lines) + "\n"
        output_path.parent.mkdir(parents=True, exist_ok=True)
        if not output_path.exists() or output_path.read_text(encoding="utf-8", errors="ignore") != content:
            output_path.write_text(content, encoding="utf-8")
        return content

    def build_context_pack(
        self,
        task: Task,
        max_chars_per_file: int = 8000,
        forbidden_extensions: list[str] | None = None,
    ) -> str:
        forbidden = {e.lower() for e in (forbidden_extensions or [])}
        sections: list[str] = []
        requested = list(dict.fromkeys([*task.context_files, *task.target_files]))
        for rel in requested:
            norm = rel.replace("\\", "/").lstrip("./")
            if os.path.splitext(norm)[1].lower() in forbidden:
                continue
            path = (self.repo_root / norm).resolve()
            if not path.is_file():
                continue
            try:
                text = path.read_text(encoding="utf-8", errors="ignore")
            except OSError:
                continue
            if len(text) > max_chars_per_file:
                text = text[:max_chars_per_file] + "\n... [truncated] ..."
            sections.append(f"### File: `{norm}`\n```\n{text}\n```")
        return "\n\n".join(sections)
