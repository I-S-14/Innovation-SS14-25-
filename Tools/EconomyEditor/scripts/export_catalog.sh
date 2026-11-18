#!/usr/bin/env sh
set -eu
EDITOR_ROOT=$(CDPATH= cd -- "$(dirname -- "$0")/.." && pwd)
REPO_ROOT=$(CDPATH= cd -- "$EDITOR_ROOT/../.." && pwd)
OUTPUT_DIR=${1:-"$EDITOR_ROOT/data/catalog"}
cd "$REPO_ROOT"
# Keep build-time tools in their normal output directories: XamlIL.targets
# loads Robust.Client.Injectors from a fixed configuration-specific path.
# PublishDir collects the finished exporter without overriding OutputPath.
dotnet publish Content.YAMLLinter/Content.YAMLLinter.csproj --configuration Debug --nologo --output "$REPO_ROOT/bin/EconomyCatalogExporter/"
dotnet "$REPO_ROOT/bin/EconomyCatalogExporter/Content.YAMLLinter.dll" --export-economy-catalog "$OUTPUT_DIR"
