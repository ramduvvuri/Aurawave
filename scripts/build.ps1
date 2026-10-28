$ErrorActionPreference = "Stop"
$Root = Split-Path -Parent $PSScriptRoot
Push-Location $Root
try {
    Write-Host "==> Restoring NuGet packages..." -ForegroundColor Cyan
    dotnet restore .\AuraWave.sln
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

    Write-Host "==> Building AuraWave Solution (Release)..." -ForegroundColor Cyan
    dotnet build .\AuraWave.sln -c Release --no-restore
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
} finally {
    Pop-Location
}