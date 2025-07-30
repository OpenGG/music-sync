from __future__ import annotations
import logging
import os
import subprocess
import shutil
from dataclasses import dataclass
from typing import List, Optional, Tuple, Iterator
import tempfile
from contextlib import contextmanager

from typing import TYPE_CHECKING
if TYPE_CHECKING:
    from .core import DRMPluginConfig

logger = logging.getLogger(__name__)


@dataclass
class DrmPlugin:
    name: str
    script_path: str
    extensions: List[str]

    def handles(self, ext: str) -> bool:
        return ext.lower() in (e.lower() for e in self.extensions)

    @contextmanager
    def process(self, source: str, music_exts: List[str]) -> Iterator[Optional[str]]:
        """Yield decrypted file path produced by the DRM plugin."""
        logger.debug("Running DRM plugin %s on %s", self.name, source)
        with tempfile.TemporaryDirectory(prefix="drm_output_") as tmp_dir:
            try:
                result = subprocess.run(
                    [self.script_path, source, tmp_dir],
                    check=True,
                    capture_output=True,
                    text=True,
                    timeout=120,
                )
                if result.stdout:
                    logger.debug("  Plugin stdout: %s", result.stdout.strip())  # pragma: no cover
                if result.stderr:
                    logger.debug("  Plugin stderr: %s", result.stderr.strip())  # pragma: no cover
                candidates: List[str] = []
                for root, _, files in os.walk(tmp_dir):
                    for fn in files:
                        ext = os.path.splitext(fn)[1].lower()
                        if ext in music_exts:
                            candidates.append(os.path.join(root, fn))
                if not candidates:
                    yield None
                else:
                    candidates.sort(
                        key=lambda x: music_exts.index(os.path.splitext(x)[1].lower())
                        if os.path.splitext(x)[1].lower() in music_exts
                        else len(music_exts)
                    )
                    yield candidates[0]
            except Exception:
                logger.error("Plugin %s failed", self.name, exc_info=True)
                yield None


class PluginManager:
    def __init__(self, plugins_cfg: List[DRMPluginConfig]):
        self._configs = plugins_cfg
        self._registry: dict[str, DrmPlugin] = {}
        self._loaded = False

    def _find_script(self, name: str) -> Optional[str]:
        cwd = os.getcwd()
        script_dir = os.path.join(os.path.dirname(os.path.abspath(__file__)), "drm_plugins")
        candidates = [name, f"{name}.sh", f"{name}.bash"]
        for cand in candidates:
            p = os.path.join(cwd, cand)
            if os.path.isfile(p) and os.access(p, os.X_OK):
                return p
        for cand in candidates:
            p = os.path.join(script_dir, cand)
            if os.path.isfile(p) and os.access(p, os.X_OK):
                return p
        return None

    def load(self) -> None:
        if self._loaded:
            return
        for cfg in self._configs:
            if not cfg.enabled:
                continue
            script = self._find_script(cfg.name)
            if not script:
                logger.warning("DRM plugin script for '%s' not found", cfg.name)
                continue
            plugin = DrmPlugin(cfg.name, script, cfg.extensions)
            for ext in plugin.extensions:
                self._registry[ext.lower()] = plugin
            logger.info("Loaded DRM plugin: %s (%s)", cfg.name, os.path.basename(script))
        self._loaded = True

    def resolve(self, ext: str) -> Optional[DrmPlugin]:
        if not self._loaded:
            self.load()
        return self._registry.get(ext.lower())


# Backwards compatible helper
def load_drm_plugins(plugins_cfg: List[DRMPluginConfig]):
    manager = PluginManager(plugins_cfg)
    manager.load()
    return {ext: {"script_path": plugin.script_path} for ext, plugin in manager._registry.items()}
