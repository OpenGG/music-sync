#!/usr/bin/env bash
set -e
SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
PROJECT_ROOT="$(dirname "$SCRIPT_DIR")"
cd "$PROJECT_ROOT"

./scripts/clean.sh

# dotnet build MusicSync.sln -c Release "$@"
dotnet publish \
  MusicSync/MusicSync.csproj \
  -c Release \
  "$@"
