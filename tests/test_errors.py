import os
import subprocess
import sqlite3
import pytest
from music_sync import SyncConfig, DRMPluginConfig, init_db
from music_sync.hashutils import get_music_md5
from music_sync.plugins import PluginManager, DrmPlugin
from music_sync.db_model import DBModel
from music_sync.processor import FileProcessor


def test_get_music_md5_errors(monkeypatch, tmp_path):
    file = tmp_path / "x.mp3"
    file.write_bytes(b"\x00\x01")
    def fail_run(*args, **kwargs):
        raise subprocess.CalledProcessError(1, args[0], stderr="boom")
    monkeypatch.setattr(subprocess, "run", fail_run)
    assert get_music_md5(str(file)) is None
    def missing_run(*args, **kwargs):
        raise FileNotFoundError
    monkeypatch.setattr(subprocess, "run", missing_run)
    assert get_music_md5(str(file)) is None


def test_handle_drm_file_plugin_error(tmp_path):
    src = tmp_path / "f.ncm"
    src.write_text("dummy")
    cfg = SyncConfig(music_sources=[str(tmp_path)], music_incoming_dir=str(tmp_path), database_file=str(tmp_path/"db.sqlite"), drm_plugins=[DRMPluginConfig(name="bad", enabled=True, extensions=[".ncm"])], music_extensions=[".mp3"])
    plugin = tmp_path / "bad.sh"
    plugin.write_text("#!/bin/bash\nexit 1\n")
    plugin.chmod(0o755)
    init_db(cfg.database_file)
    with DBModel(cfg.database_file) as db:
        db.init_tables()
        mgr = PluginManager(cfg.drm_plugins)
        mgr.load()
        # override resolved plugin with our failing script
        mgr._registry[".ncm"] = DrmPlugin("bad", str(plugin), [".ncm"])
        processor = FileProcessor(db, mgr, cfg)
        assert not processor._handle_drm(str(src), 0, "f.ncm", mgr.resolve(".ncm"))

