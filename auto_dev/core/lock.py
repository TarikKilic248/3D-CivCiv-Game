from __future__ import annotations

import ctypes
import json
import os
import sys
import time
from datetime import datetime, timezone
from pathlib import Path


class LockError(RuntimeError):
    """Raised when another AutoDev instance already holds the runner lock."""


def _is_pid_running(pid: int) -> bool:
    if pid <= 0:
        return False
    if sys.platform == "win32":
        PROCESS_QUERY_LIMITED_INFORMATION = 0x1000
        STILL_ACTIVE = 259
        kernel32 = ctypes.windll.kernel32
        handle = kernel32.OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION, False, pid)
        if not handle:
            return False
        try:
            exit_code = ctypes.c_ulong()
            if kernel32.GetExitCodeProcess(handle, ctypes.byref(exit_code)):
                return exit_code.value == STILL_ACTIVE
            return False
        finally:
            kernel32.CloseHandle(handle)
    try:
        os.kill(pid, 0)
        return True
    except OSError:
        return False


class RunnerLock:
    """Single-instance PID lockfile with stale-lock recovery and optional handover wait."""

    def __init__(self, lock_path: Path, wait_timeout_sec: int = 0) -> None:
        self.lock_path = lock_path
        self.wait_timeout_sec = max(0, int(wait_timeout_sec))
        self._acquired = False

    def acquire(self, wait_timeout_sec: int | None = None) -> None:
        wait_sec = self.wait_timeout_sec if wait_timeout_sec is None else max(0, int(wait_timeout_sec))
        deadline = time.monotonic() + wait_sec
        self.lock_path.parent.mkdir(parents=True, exist_ok=True)

        while True:
            try:
                fd = os.open(
                    str(self.lock_path),
                    os.O_CREAT | os.O_EXCL | os.O_WRONLY,
                )
                payload = json.dumps(
                    {
                        "pid": os.getpid(),
                        "started_at": datetime.now(timezone.utc).isoformat(),
                    }
                )
                with os.fdopen(fd, "w", encoding="utf-8") as handle:
                    handle.write(payload)
                self._acquired = True
                return
            except FileExistsError:
                if self._is_stale():
                    try:
                        self.lock_path.unlink(missing_ok=True)
                    except OSError as exc:
                        raise LockError(f"Eski kilit dosyasi silinemedi: {exc}") from exc
                    continue
                if time.monotonic() < deadline:
                    time.sleep(1.0)
                    continue
                owner = self._read_owner()
                raise LockError(
                    f"Baska bir AutoDev kosusu zaten aktif ({owner}). Kilit: {self.lock_path}"
                )

    def release(self) -> None:
        if self._acquired:
            try:
                self.lock_path.unlink(missing_ok=True)
            finally:
                self._acquired = False

    def _read_owner(self) -> str:
        try:
            data = json.loads(self.lock_path.read_text(encoding="utf-8"))
            return f"pid={data.get('pid')}, started_at={data.get('started_at')}"
        except Exception:
            return "bilinmeyen surec"

    def _is_stale(self) -> bool:
        try:
            data = json.loads(self.lock_path.read_text(encoding="utf-8"))
            pid = int(data.get("pid", -1))
            return not _is_pid_running(pid)
        except Exception:
            return True

    def __enter__(self) -> "RunnerLock":
        self.acquire()
        return self

    def __exit__(self, exc_type, exc, tb) -> None:
        self.release()
