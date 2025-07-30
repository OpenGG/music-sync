# music-sync

`music-sync` collects music from one or more source folders, decrypts any DRM-protected files via shell plugins and places the results in a single destination directory. The script keeps a SQLite database of processed hashes so duplicates are skipped automatically.

## Requirements

- Python 3.8+
- [ffmpeg](https://ffmpeg.org/) command line tools (ffmpeg/ffprobe)
- Optional DRM utilities (e.g. [ncmdump](https://github.com/nondanee/ncmdump) for `.ncm` files)

Install Python dependencies and pytest for running the test suite:

```bash
pip install -e .
pip install pytest
```

## Configuration

Edit `config.yaml` to match your environment.

- `music_sources`: list of directories to scan
- `music_incoming_dir`: destination for processed files
- `database_file`: SQLite file used to track hashes (defaults to `music_sync.db`)
- `drm_plugins`: list of DRM plugin scripts. Each item has `name`, `enabled` and `extensions`.
- `music_extensions`: regular audio extensions that should be copied directly

## Usage

```bash
music-sync [-c CONFIG] [-v]
```

You can also run `python -m music_sync.cli` for the same behaviour.

- `-c /path/to/config.yaml` – use an alternative configuration file
- `-v` – enable verbose debug logging

DRM plugins are simple executable scripts. They receive the source file path and an output directory. Example plugin `drm_plugins/ncmdump.sh` decrypts `.ncm` files using `ncmdump`.

## Running Tests

Tests are written with `pytest` and require `ffmpeg` to be installed:

```bash
pytest
```

This will run unit tests under the `tests/` directory.
