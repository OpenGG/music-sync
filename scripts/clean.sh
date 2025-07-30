#!/bin/bash
set -e
SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
PROJECT_ROOT="$(dirname "$SCRIPT_DIR")"
cd "$PROJECT_ROOT"

rm -rf MusicSync/bin
rm -rf MusicSync/obj
rm -rf MusicSync.Tests/bin
rm -rf MusicSync.Tests/obj
