# Music Sync

Music Sync is a simple tool for synchronizing and de-DRMing your music library. It provides both a legacy Python script and a modern C# implementation. The C# project is fully unit tested and mirrors the behaviour of the original Python version.

## Project Layout

```
/                   - root of the repository
├── MusicSync/      - C# application source
│   ├── Models/     - configuration models
│   ├── Plugins/    - DRM plugin abstraction
│   ├── Services/   - core services (processing, database, configuration)
│   ├── Utils/      - helper utilities
│   └── Program.cs  - application entry point
├── MusicSync.Tests/ - xUnit test project
├── drm_plugins/    - example DRM bash scripts
├── config.yaml     - example configuration file
├── music_sync.py   - original Python script
└── scripts/        - helper scripts for build/test/run
```

## Requirements

- .NET 8 SDK
- `ffmpeg` available on your `PATH` for audio hashing
- Optional: external DRM utilities (e.g. `ncmdump`) for the provided plugins

## Building

From the repository root run:

```bash
./scripts/build.sh
```

This invokes `dotnet build` on the solution in Release mode.

## Running

The application reads `config.yaml` by default. You can specify a different path with `-c` or `--config`.

```bash
./scripts/run.sh -c path/to/config.yaml
```

## Testing

Unit tests are located in `MusicSync.Tests`. Code coverage can be collected using the helper script:

```bash
./scripts/test.sh
```

Coverage results will be placed under the `TestResults` directory.

## Configuration

See `config.yaml` for an annotated example of all available options, including:

- `music_sources` – directories to scan for music files
- `music_dest_dir` – destination directory for processed files
- `database_file` – SQLite database used for tracking
- `drm_plugins` – list of DRM plugins and the extensions they handle
- `music_extensions` – regular audio extensions that are copied directly

## Plugins

DRM plugins are simple executable scripts that take the source file and a temporary output directory. Example plugin `ncmdump.sh` decrypts NCM files using the external `ncmdump` tool. Enable or disable plugins in `config.yaml`.

## Python Script

The original `music_sync.py` script remains for reference. The C# project mirrors its behaviour and is the recommended entry point going forward.
