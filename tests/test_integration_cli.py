import os
import subprocess
import sqlite3
import sys
from pathlib import Path
from music_sync import load_config, music_sync, SyncConfig, DRMPluginConfig
from music_sync.cli import main


def create_audio(path: Path, freq: int = 440):
    subprocess.run([
        "ffmpeg", "-f", "lavfi", "-i", f"sine=frequency={freq}",
        "-t", "1", "-q:a", "9", "-acodec", "libmp3lame", str(path), "-y",
    ], check=True, capture_output=True)


def test_load_config(tmp_path):
    config_file = tmp_path / "cfg.yaml"
    config_file.write_text(
        """
        music_sources: [src]
        music_incoming_dir: incoming
        database_file: test.db
        drm_plugins:
          - name: dummy
            enabled: true
            extensions: [.ncm]
        music_extensions: [.mp3]
        """
    )
    (tmp_path / "src").mkdir()
    cfg = load_config(str(config_file))
    assert cfg.music_sources == ["src"]
    assert cfg.music_incoming_dir == "incoming"
    assert cfg.database_file == "test.db"
    assert cfg.drm_plugins[0].name == "dummy"
    assert cfg.music_extensions == [".mp3"]


def test_music_sync_end_to_end(tmp_path, monkeypatch):
    src = tmp_path / "src"
    incoming = tmp_path / "incoming"
    src.mkdir()
    incoming.mkdir()
    audio = src / "file.mp3"
    create_audio(audio)
    drm_source = src / "file.ncm"
    create_audio(tmp_path / "tmp.mp3", freq=880)
    (tmp_path / "tmp.mp3").rename(drm_source)
    plugin = tmp_path / "dummy.sh"
    plugin.write_text("#!/bin/bash\ncp $1 $2/out.mp3\n")
    plugin.chmod(0o755)
    cfg = SyncConfig(
        music_sources=[str(src)],
        music_incoming_dir=str(incoming),
        database_file=str(tmp_path / "db.sqlite"),
        drm_plugins=[DRMPluginConfig(name=plugin.name, enabled=True, extensions=[".ncm"])],
        music_extensions=[".mp3"],
    )
    monkeypatch.chdir(tmp_path)
    music_sync(cfg)
    files = sorted(p.name for p in incoming.iterdir())
    assert len(files) >= 1


def test_cli_main(tmp_path, monkeypatch):
    cfg_file = tmp_path / "c.yaml"
    src = tmp_path / "src"; src.mkdir()
    incoming = tmp_path / "dest"
    plugin = tmp_path / "d.sh"
    plugin.write_text("#!/bin/bash\ncp $1 $2/out.mp3\n")
    plugin.chmod(0o755)
    create_audio(src / "a.mp3")
    cfg_file.write_text(
        f"music_sources: [{src}]\nmusic_incoming_dir: {incoming}\ndatabase_file: {tmp_path / 'db.sqlite'}\ndrm_plugins:\n  - name: {plugin.name}\n    enabled: true\n    extensions: [.ncm]\nmusic_extensions: [.mp3]\n"
    )
    monkeypatch.chdir(tmp_path)
    monkeypatch.setattr(sys, "argv", ["music-sync", "-c", str(cfg_file)])
    main()
    assert any(incoming.iterdir())

