import os
import subprocess
import sqlite3
import hashlib
import sys
sys.path.insert(0, os.path.abspath(os.path.join(os.path.dirname(__file__), '..')))

from music_sync import (
    get_music_md5,
    SyncConfig,
    DRMPluginConfig,
    init_db,
    load_drm_plugins,
)
from music_sync.db_model import DBModel
from music_sync.plugins import PluginManager
from music_sync.processor import FileProcessor


def create_audio(path: str, freq: int = 440):
    subprocess.run([
        "ffmpeg", "-f", "lavfi", "-i", f"sine=frequency={freq}",
        "-t", "1", "-q:a", "9", "-acodec", "libmp3lame", path, "-y"
    ], check=True, capture_output=True)


def create_video(path: str):
    subprocess.run([
        "ffmpeg", "-f", "lavfi", "-i", "testsrc=rate=1:size=10x10",
        "-f", "lavfi", "-i", "sine=frequency=1000",
        "-t", "1", path, "-y"
    ], check=True, capture_output=True)


def test_has_only_audio_and_video(tmp_path):
    audio = tmp_path / "a.mp3"
    video = tmp_path / "v.mp4"
    create_audio(str(audio))
    create_video(str(video))
    assert get_music_md5(str(audio)) == hashlib.md5(audio.read_bytes()).hexdigest()
    assert get_music_md5(str(video)) is not None


def test_handle_regular_and_drm(tmp_path, monkeypatch):
    src = tmp_path / "src"
    incoming = tmp_path / "incoming"
    src.mkdir()
    incoming.mkdir()
    normal = src / "song.mp3"
    create_audio(str(normal))
    drm_src = src / "enc.ncm"
    tmp_mp3 = src / "enc_tmp.mp3"
    create_audio(str(tmp_mp3), freq=880)
    tmp_mp3.rename(drm_src)
    drm_dst_script = tmp_path / "dummy_drm.sh"
    drm_dst_script.write_text("#!/bin/bash\ncp $1 $2/out.mp3\n")
    drm_dst_script.chmod(0o755)
    cfg = SyncConfig(
        music_sources=[str(src)],
        music_incoming_dir=str(incoming),
        database_file=str(tmp_path / "db.sqlite"),
        drm_plugins=[DRMPluginConfig(name=drm_dst_script.name, enabled=True, extensions=['.ncm'])],
        music_extensions=['.mp3']
    )
    monkeypatch.chdir(tmp_path)
    registry = load_drm_plugins(cfg.drm_plugins)
    assert ".ncm" in registry
    init_db(cfg.database_file)
    with DBModel(cfg.database_file) as db:
        db.init_tables()
        plugin_mgr = PluginManager(cfg.drm_plugins)
        plugin_mgr.load()
        processor = FileProcessor(db, plugin_mgr, cfg)
        processor.process_file(str(normal), str(src))
        processor.process_file(str(drm_src), str(src))
    assert len(list(incoming.iterdir())) == 2
