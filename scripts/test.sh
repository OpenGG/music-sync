#!/bin/bash
set -e
SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
PROJECT_ROOT="$(dirname "$SCRIPT_DIR")"
cd "$PROJECT_ROOT"

dotnet test MusicSync.Tests/MusicSync.Tests.csproj --no-build --collect:"XPlat Code Coverage" --results-directory TestResults "$@"
