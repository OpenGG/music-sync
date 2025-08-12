#!/bin/bash
set -e
SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
PROJECT_ROOT="$(dirname "$SCRIPT_DIR")"
cd "$PROJECT_ROOT"

dirs=(
    "CoverageReport"
    "TestResults"
    "music_sync.db"
    "dist"
    "MusicSync/bin"
    "MusicSync/obj"
    "MusicSync.Tests/bin"
    "MusicSync.Tests/obj"
)

for dir in "${dirs[@]}"; do
    rm -rf "$dir"
done
