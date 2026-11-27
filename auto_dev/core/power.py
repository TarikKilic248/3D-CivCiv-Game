from __future__ import annotations

import ctypes
import sys

_ES_CONTINUOUS = 0x80000000
_ES_SYSTEM_REQUIRED = 0x00000001


class KeepAwake:
    """Prevents Windows idle sleep while an overnight shift is running (no admin needed)."""

    def __init__(self, enabled: bool = True) -> None:
        self.enabled = enabled and sys.platform == "win32"
        self._active = False

    def start(self) -> None:
        if not self.enabled or self._active:
            return
        try:
            ctypes.windll.kernel32.SetThreadExecutionState(
                _ES_CONTINUOUS | _ES_SYSTEM_REQUIRED
            )
            self._active = True
        except Exception:
            self._active = False

    def stop(self) -> None:
        if not self._active:
            return
        try:
            ctypes.windll.kernel32.SetThreadExecutionState(_ES_CONTINUOUS)
        except Exception:
            pass
        finally:
            self._active = False

    def __enter__(self) -> "KeepAwake":
        self.start()
        return self

    def __exit__(self, exc_type, exc, tb) -> None:
        self.stop()
