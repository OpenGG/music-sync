from __future__ import annotations

import os
import logging
import shutil
import subprocess
from typing import TYPE_CHECKING

from .db_model import DBModel
from .hashutils import get_music_md5
from .plugins import PluginManager, DrmPlugin

if TYPE_CHECKING:
    from .core import SyncConfig

logger = logging.getLogger(__name__)


class FileProcessor:
    def __init__(self, db: DBModel, plugin_mgr: PluginManager, cfg: SyncConfig):
        self.db = db
        self.plugin_mgr = plugin_mgr
        self.cfg = cfg

    def _handle_drm(self, full_path: str, file_mtime: int, relative_path: str, plugin: DrmPlugin) -> bool:
        target_dir = os.path.join(self.cfg.music_incoming_dir, os.path.dirname(relative_path))
        os.makedirs(target_dir, exist_ok=True)
        logger.info("Detecting DRM file (%s): %s", os.path.splitext(full_path)[1], full_path)
        try:
            with plugin.process(full_path, self.cfg.music_extensions) as decrypted:
                if not decrypted:
                    self.db.log_operation(full_path, file_mtime, None, "dedrm_no_music_found")
                    logger.error("No supported music file produced for %s", full_path)
                    return False
                music_md5_hash = get_music_md5(decrypted)
                if music_md5_hash is None:
                    self.db.log_operation(full_path, file_mtime, None, "md5_fail_dedrm")
                    return False
                if self.db.is_music_hash_processed(music_md5_hash):
                    self.db.log_operation(full_path, file_mtime, music_md5_hash, "skip_music_hash_exists", log_to_db=False)
                    logger.info("Skipping already processed music (by hash): %s", os.path.basename(full_path))
                    return False
                base = os.path.splitext(os.path.basename(full_path))[0]
                final_ext = os.path.splitext(decrypted)[1]
                final_target = os.path.join(target_dir, base + final_ext)
                shutil.move(decrypted, final_target)

            self.db.record_music_hash(music_md5_hash)
            self.db.log_operation(full_path, file_mtime, music_md5_hash, "dedrm_success")
            logger.info("Successfully decrypted and moved %s to %s", os.path.basename(full_path), final_target)
            return True
        except subprocess.CalledProcessError as e:  # pragma: no cover - plugin error
            err = e.stderr.strip() if e.stderr else f"Exit code {e.returncode}"
            self.db.log_operation(full_path, file_mtime, None, f"dedrm_fail_plugin_error_{e.returncode}")
            logger.error("Error calling DRM plugin for %s: %s", os.path.basename(full_path), err)
        except FileNotFoundError:  # pragma: no cover - missing plugin
            self.db.log_operation(full_path, file_mtime, None, "plugin_script_not_found")
            logger.error("DRM plugin script '%s' not found or not executable", plugin.script_path)
        except subprocess.TimeoutExpired:  # pragma: no cover - plugin timeout
            self.db.log_operation(full_path, file_mtime, None, "dedrm_timeout")
            logger.error("DRM plugin timed out for %s", os.path.basename(full_path))
        except Exception as e:  # pragma: no cover - unexpected
            self.db.log_operation(full_path, file_mtime, None, f"dedrm_unexpected_error_{type(e).__name__}")
            logger.error("Unexpected error during DRM processing of %s: %s", os.path.basename(full_path), e)
        return False

    def _handle_regular(self, full_path: str, file_mtime: int, relative_path: str) -> bool:
        target_dir = os.path.join(self.cfg.music_incoming_dir, os.path.dirname(relative_path))
        os.makedirs(target_dir, exist_ok=True)
        final_target = os.path.join(target_dir, os.path.basename(full_path))
        logger.info("Detecting music file: %s", full_path)
        md5 = get_music_md5(full_path)
        if md5 is None:
            self.db.log_operation(full_path, file_mtime, None, "md5_fail_copy")
            return False
        if self.db.is_music_hash_processed(md5):
            self.db.log_operation(full_path, file_mtime, md5, "skip_music_hash_exists", log_to_db=False)
            logger.info("Skipping already processed music (by hash): %s", os.path.basename(full_path))
            return False
        try:
            shutil.copy2(full_path, final_target)
            self.db.record_music_hash(md5)
            self.db.log_operation(full_path, file_mtime, md5, "copy_success")
            logger.info("Successfully copied %s to %s", os.path.basename(full_path), final_target)
            return True
        except Exception as e:  # pragma: no cover - filesystem error
            self.db.log_operation(full_path, file_mtime, md5, f"copy_fail_{type(e).__name__}")
            logger.error("Error copying %s: %s", os.path.basename(full_path), e)
            return False

    def process_file(self, full_path: str, source_dir: str) -> None:
        file_mtime = int(os.path.getmtime(full_path))
        relative_path = os.path.relpath(full_path, source_dir)
        prev = self.db.get_previous_result(full_path, file_mtime)
        if prev in {"copy_success", "dedrm_success"}:
            self.db.log_operation(full_path, file_mtime, None, "skip_path_mtime_exists", log_to_db=False)
            return
        if prev:
            logger.info("File %s was previously processed with result '%s'. Retrying.", os.path.basename(full_path), prev)
        ext = os.path.splitext(full_path)[1].lower()
        plugin = self.plugin_mgr.resolve(ext)
        if plugin:
            self._handle_drm(full_path, file_mtime, relative_path, plugin)
        elif ext in self.cfg.music_extensions:
            self._handle_regular(full_path, file_mtime, relative_path)
        else:
            self.db.log_operation(full_path, file_mtime, None, "unsupported_type")  # pragma: no cover - unsupported type
            logger.info("Skipping unsupported file type: %s", full_path)
