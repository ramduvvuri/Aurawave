$ErrorActionPreference = "Stop"
$Root = Split-Path -Parent $PSScriptRoot
Push-Location $Root
try {
    Write-Host "==> Running Automated xUnit Tests..." -ForegroundColor Cyan
    dotnet test .\tests\AuraWave.Tests\AuraWave.Tests.csproj -c Release --no-build --logger "console;verbosity=normal"
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
} finally {
    Pop-Location
}