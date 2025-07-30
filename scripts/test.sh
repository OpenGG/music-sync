#!/bin/bash
set -e
SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
PROJECT_ROOT="$(dirname "$SCRIPT_DIR")"
cd "$PROJECT_ROOT"

rm -rf "$PROJECT_ROOT/TestResults"

dotnet test MusicSync.Tests/MusicSync.Tests.csproj --collect:"XPlat Code Coverage" --results-directory TestResults "$@"

if ! command -v reportgenerator &> /dev/null; then
  echo "The command 'reportgenerator' not exists."
  echo "Installing reportgenerator"

  dotnet tool install --global dotnet-reportgenerator-globaltool
fi
reportgenerator -reports:**/TestResults/**/coverage.cobertura.xml -targetdir:CoverageReport -filefilters:"-*Migrations*;-*Generated.cs;-*TypeFactoryGenerator*;-*RegexGenerator*"
