import sqlite3
import logging
from typing import Optional

logger = logging.getLogger(__name__)


class DBModel:
    """SQLite helper with basic operations."""

    def __init__(self, path: str):
        self.path = path
        self.conn: Optional[sqlite3.Connection] = None

    def __enter__(self):
        self.conn = sqlite3.connect(self.path)
        return self

    def __exit__(self, exc_type, exc, tb):
        if self.conn:
            self.conn.commit()
            self.conn.close()

    # Table management
    def init_tables(self) -> None:
        cur = self.conn.cursor()
        cur.execute(
            """
            CREATE TABLE IF NOT EXISTS music_hash (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                md5_hash TEXT UNIQUE NOT NULL,
                first_processed_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
            )
            """
        )
        cur.execute(
            """
            CREATE TABLE IF NOT EXISTS operation_log (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                original_path TEXT NOT NULL,
                mtime INTEGER NOT NULL,
                music_md5_hash TEXT,
                result TEXT NOT NULL,
                log_time TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
                UNIQUE (original_path, mtime)
            )
            """
        )
        self.conn.commit()

    # Query helpers
    def is_music_hash_processed(self, md5_hash: str) -> bool:
        cur = self.conn.cursor()
        cur.execute("SELECT 1 FROM music_hash WHERE md5_hash = ?", (md5_hash,))
        return cur.fetchone() is not None

    def record_music_hash(self, md5_hash: str) -> None:
        cur = self.conn.cursor()
        try:
            cur.execute("INSERT INTO music_hash (md5_hash) VALUES (?)", (md5_hash,))
            logger.debug("Recorded new music hash: %s", md5_hash)
        except sqlite3.IntegrityError:
            logger.debug("music hash %s already recorded", md5_hash)

    def get_previous_result(self, path: str, mtime: int) -> Optional[str]:
        cur = self.conn.cursor()
        cur.execute(
            "SELECT result FROM operation_log WHERE original_path = ? AND mtime = ?",
            (path, mtime),
        )
        row = cur.fetchone()
        return row[0] if row else None

    def log_operation(self, path: str, mtime: int, md5: Optional[str], result: str, log_to_db: bool = True) -> None:
        cur = self.conn.cursor()
        if log_to_db:
            cur.execute(
                "INSERT INTO operation_log (original_path, mtime, music_md5_hash, result) VALUES (?, ?, ?, ?)",
                (path, mtime, md5, result),
            )
            logger.info("LOG (DB): %s -> %s", path, result)
        else:
            logger.info("LOG: %s -> %s", path, result)
