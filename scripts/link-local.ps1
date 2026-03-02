# link-local.ps1 — build, pack, and install unheft from the local source tree
# so you can run and test the tool as if it were installed from NuGet.
#
# Usage: scripts\link-local.ps1
$ErrorActionPreference = 'Stop'

$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$RepoRoot  = Split-Path -Parent $ScriptDir
$OutDir    = Join-Path $RepoRoot (Join-Path 'artifacts' 'nupkg')

Write-Host "Packing unheft from local source..."
dotnet pack (Join-Path $RepoRoot (Join-Path 'src' 'Unheft')) -o $OutDir --nologo

Write-Host "Uninstalling existing global tool (if any)..."
dotnet tool uninstall -g unheft 2>$null
if ($LASTEXITCODE -ne 0) { $LASTEXITCODE = 0 }

Write-Host "Installing unheft from local build..."
dotnet tool install -g unheft --add-source $OutDir

Write-Host ""
Write-Host "Done. Run 'unheft --help' to verify."
