#!/usr/bin/env sh
set -eu
EDITOR_ROOT=$(CDPATH= cd -- "$(dirname -- "$0")/.." && pwd)
REPO_ROOT=$(CDPATH= cd -- "$EDITOR_ROOT/../.." && pwd)
: "${GITHUB_SHA:?GITHUB_SHA is required}"
: "${ECONOMY_EDITOR_IMAGE:?ECONOMY_EDITOR_IMAGE is required}"
BUILD_ROOT=$(mktemp -d "${RUNNER_TEMP:-${TMPDIR:-/tmp}}/is14-economy-editor.XXXXXX")
export BUILD_ROOT
cd "$REPO_ROOT"

# A new directory every time: no previously generated catalog is reused.
sh "$EDITOR_ROOT/scripts/export_catalog.sh" "$BUILD_ROOT/catalog"
dotnet publish "$EDITOR_ROOT/EconomyEditor.csproj" --configuration Release --nologo --output "$BUILD_ROOT/app"
python3 -m unittest discover -s "$EDITOR_ROOT/tests" -p 'test_apply_project.py' -v
python3 -m unittest discover -s "$EDITOR_ROOT/tests" -p 'test_deploy.py' -v
python3 "$EDITOR_ROOT/scripts/verify_web.py" --app "$BUILD_ROOT/app" --catalog "$BUILD_ROOT/catalog"

python3 - <<'PY'
import json, os, subprocess
from pathlib import Path
root = Path(os.environ['BUILD_ROOT'])
catalog = json.loads((root / 'catalog/catalog.json').read_text(encoding='utf-8'))
if catalog['commit'] != os.environ['GITHUB_SHA']:
    raise SystemExit('Catalog commit does not match the game publish commit')
manifest = dict(commit=catalog['commit'], catalogVersion=catalog['version'],
                generatedAt=catalog['generatedAt'], items=len(catalog['items']), vendors=len(catalog['vendors']),
                engineCommit=subprocess.check_output(['git', '-C', 'RobustToolbox', 'rev-parse', 'HEAD'], text=True).strip(),
                image=os.environ['ECONOMY_EDITOR_IMAGE'])
(root / 'deployment.json').write_text(json.dumps(manifest, indent=2) + '\n', encoding='utf-8')
PY
CATALOG_VERSION=$(python3 -c 'import json,os;from pathlib import Path;print(json.loads((Path(os.environ["BUILD_ROOT"])/"catalog/catalog.json").read_text())["version"])')
docker build --file "$EDITOR_ROOT/deploy/Dockerfile" --tag "$ECONOMY_EDITOR_IMAGE" \
  --label "org.opencontainers.image.revision=$GITHUB_SHA" \
  --label "io.innovation.economy-editor.catalog-version=$CATALOG_VERSION" "$BUILD_ROOT"

if [ -n "${GITHUB_OUTPUT:-}" ]; then
  printf 'image=%s\ncatalog-version=%s\nmanifest=%s\n' "$ECONOMY_EDITOR_IMAGE" "$CATALOG_VERSION" "$BUILD_ROOT/deployment.json" >> "$GITHUB_OUTPUT"
fi
printf 'Built %s with catalog %s for game commit %s\n' "$ECONOMY_EDITOR_IMAGE" "$CATALOG_VERSION" "$GITHUB_SHA"
