import os
import sys
sys.path.insert(0, os.path.abspath(os.path.join(os.path.dirname(__file__), '..')))
from music_sync import load_drm_plugins, DRMPluginConfig


def test_load_drm_plugins(tmp_path, monkeypatch):
    script = tmp_path / "testplugin.sh"
    script.write_text("#!/bin/bash\necho hi\n")
    script.chmod(0o755)
    config = [DRMPluginConfig(name="testplugin", enabled=True, extensions=[".abc"])]
    monkeypatch.chdir(tmp_path)
    registry = load_drm_plugins(config)
    assert ".abc" in registry
    assert registry[".abc"]["script_path"] == str(script)

