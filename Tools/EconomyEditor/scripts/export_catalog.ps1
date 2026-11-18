param([string]$OutputDirectory = "")
$ErrorActionPreference = "Stop"
$editorRoot = Split-Path $PSScriptRoot -Parent
$repoRoot = [IO.Path]::GetFullPath((Join-Path $editorRoot "../.."))
if (!$OutputDirectory) { $OutputDirectory = Join-Path $editorRoot "data/catalog" }
$OutputDirectory = [IO.Path]::GetFullPath($OutputDirectory)
# Publish the finished exporter separately, without redirecting build-time tools.
$exportBin = Join-Path $repoRoot "bin/EconomyCatalogExporter"
Push-Location $repoRoot
try {
    dotnet publish Content.YAMLLinter/Content.YAMLLinter.csproj --configuration Debug --nologo --output "$exportBin/"
    if ($LASTEXITCODE -ne 0) { throw "Catalog exporter build failed" }
    dotnet (Join-Path $exportBin "Content.YAMLLinter.dll") --export-economy-catalog $OutputDirectory
    if ($LASTEXITCODE -ne 0) { throw "Catalog export failed" }
} finally { Pop-Location }
