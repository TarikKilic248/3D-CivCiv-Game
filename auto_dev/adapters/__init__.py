from __future__ import annotations

from auto_dev.core.config import AutoDevConfig

from .base import ProjectAdapter
from .generic import GenericAdapter
from .unity import UnityAdapter


def load_adapter(cfg: AutoDevConfig) -> ProjectAdapter:
    adapter_type = str(cfg.adapter.get("type", "generic")).strip().lower()
    if adapter_type == "unity":
        return UnityAdapter(cfg)
    return GenericAdapter(cfg)
