param([string]$OutputDirectory = "")
$ErrorActionPreference = "Stop"
$editorRoot = Split-Path $PSScriptRoot -Parent
$repoRoot = [IO.Path]::GetFullPath((Join-Path $editorRoot "../.."))
if (!$OutputDirectory) { $OutputDirectory = Join-Path $editorRoot "data/catalog" }
$OutputDirectory = [IO.Path]::GetFullPath($OutputDirectory)
# Separate output keeps a developer's running game binaries untouched.
$exportBin = Join-Path $repoRoot "bin/EconomyCatalogExporter"
Push-Location $repoRoot
try {
    dotnet build Content.YAMLLinter/Content.YAMLLinter.csproj --nologo "-p:OutputPath=$exportBin/"
    if ($LASTEXITCODE -ne 0) { throw "Catalog exporter build failed" }
    dotnet (Join-Path $exportBin "Content.YAMLLinter.dll") --export-economy-catalog $OutputDirectory
    if ($LASTEXITCODE -ne 0) { throw "Catalog export failed" }
} finally { Pop-Location }
