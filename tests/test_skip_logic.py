import os
import sys
import sqlite3
import subprocess
sys.path.insert(0, os.path.abspath(os.path.join(os.path.dirname(__file__), '..')))
from music_sync import init_db, SyncConfig
from music_sync.db_model import DBModel
from music_sync.plugins import PluginManager
from music_sync.processor import FileProcessor


def test_skip_logic(tmp_path):
    audio = tmp_path / "tone.mp3"
    subprocess.run([
        "ffmpeg","-f","lavfi","-i","anullsrc=r=44100:cl=mono","-t","1","-q:a","9","-acodec","libmp3lame",str(audio),"-y"],
        check=True,
        capture_output=True,
    )
    db_file = tmp_path / "test.db"
    init_db(str(db_file))
    with DBModel(str(db_file)) as db:
        db.init_tables()
        mtime = int(os.path.getmtime(audio))
        db.conn.execute(
            "INSERT INTO operation_log (original_path, mtime, music_md5_hash, result) VALUES (?, ?, ?, ?)",
            (str(audio), mtime, "dummy", "copy_success"),
        )
        db.conn.commit()
        cfg = SyncConfig(music_sources=[str(tmp_path)], music_incoming_dir=str(tmp_path / "incoming"), database_file=str(db_file), music_extensions=[".mp3"], drm_plugins=[])
        os.makedirs(cfg.music_incoming_dir, exist_ok=True)
        processor = FileProcessor(db, PluginManager(cfg.drm_plugins), cfg)
        processor.process_file(str(audio), str(tmp_path))
        count = db.conn.execute("SELECT COUNT(*) FROM operation_log").fetchone()[0]
        assert count == 1
