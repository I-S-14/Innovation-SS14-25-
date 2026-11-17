#!/usr/bin/env sh
set -eu
EDITOR_ROOT=$(CDPATH= cd -- "$(dirname -- "$0")/.." && pwd)
REPO_ROOT=$(CDPATH= cd -- "$EDITOR_ROOT/../.." && pwd)
OUTPUT_DIR=${1:-"$EDITOR_ROOT/data/catalog"}
cd "$REPO_ROOT"
dotnet build Content.YAMLLinter/Content.YAMLLinter.csproj --nologo -p:OutputPath="$REPO_ROOT/bin/EconomyCatalogExporter/"
dotnet "$REPO_ROOT/bin/EconomyCatalogExporter/Content.YAMLLinter.dll" --export-economy-catalog "$OUTPUT_DIR"
