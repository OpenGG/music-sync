#!/usr/bin/env python3
from __future__ import annotations

import logging
from dataclasses import dataclass, field
from pathlib import Path
from typing import List, Optional
import os
import yaml

from .db_model import DBModel
from .plugins import PluginManager
from .processor import FileProcessor


logger = logging.getLogger(__name__)


@dataclass
class DRMPluginConfig:
    name: str
    enabled: bool = True
    extensions: List[str] = field(default_factory=list)


@dataclass
class SyncConfig:
    music_sources: List[str] = field(default_factory=list)
    music_incoming_dir: str = "music_incoming"
    database_file: str = "music_sync.db"
    drm_plugins: List[DRMPluginConfig] = field(default_factory=list)
    music_extensions: List[str] = field(default_factory=list)


# --- Configuration Loading -------------------------------------------------

def _find_config_path(config_path: Optional[str]) -> Path:
    possible = []
    if config_path:
        possible.append(Path(config_path))
    possible.append(Path.cwd() / "config.yaml")
    possible.append(Path(__file__).resolve().parent / "config.yaml")

    for path in possible:
        if path.exists():
            return path
    raise FileNotFoundError("config.yaml not found. Tried: " + ", ".join(str(p) for p in possible))


def load_config(config_path: Optional[str] = None) -> SyncConfig:
    path = _find_config_path(config_path)
    logger.info("Loading configuration from: %s", path)
    with open(path, "r", encoding="utf-8") as f:
        try:
            raw = yaml.safe_load(f) or {}
        except yaml.YAMLError as e:  # pragma: no cover - invalid YAML
            raise RuntimeError(f"Error parsing config file '{path}': {e}")

    plugins = [
        DRMPluginConfig(
            name=p.get("name", ""),
            enabled=p.get("enabled", True),
            extensions=p.get("extensions", []),
        )
        for p in raw.get("drm_plugins", [])
    ]

    return SyncConfig(
        music_sources=raw.get("music_sources", []),
        music_incoming_dir=raw.get("music_incoming_dir", "music_incoming"),
        database_file=raw.get("database_file", "music_sync.db"),
        drm_plugins=plugins,
        music_extensions=[ext.lower() for ext in raw.get("music_extensions", [])],
    )


# Backwards compatible wrappers --------------------------------------------



def init_db(db_file: str) -> None:
    with DBModel(db_file) as db:
        db.init_tables()


# --- Main orchestration ----------------------------------------------------

def music_sync(cfg: SyncConfig) -> None:
    plugin_mgr = PluginManager(cfg.drm_plugins)
    plugin_mgr.load()

    os.makedirs(cfg.music_incoming_dir, exist_ok=True)
    init_db(cfg.database_file)

    with DBModel(cfg.database_file) as db:
        db.init_tables()
        processor = FileProcessor(db, plugin_mgr, cfg)
        for src in cfg.music_sources:
            if not os.path.exists(src):
                logger.warning("Source directory not found: %s. Skipping.", src)
                continue
            logger.info("\n--- Processing files from: %s ---", src)
            for root, _, files in os.walk(src):
                for fname in files:
                    processor.process_file(os.path.join(root, fname), src)
    logger.info("\n--- Music synchronization complete ---")


