$ErrorActionPreference = "Stop"
$Root = Split-Path -Parent $PSScriptRoot
$PublishDir = Join-Path $Root "dist\publish\AuraWave"
Push-Location $Root
try {
    if (Test-Path $PublishDir) { Remove-Item -Recurse -Force $PublishDir }
    New-Item -ItemType Directory -Path $PublishDir -Force | Out-Null

    Write-Host "==> Publishing Self-Contained Windows x64 Release..." -ForegroundColor Cyan
    dotnet publish .\src\AuraWave\AuraWave.csproj `
        -c Release `
        -r win-x64 `
        --self-contained true `
        -p:PublishSingleFile=false `
        -o $PublishDir
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

    Copy-Item (Join-Path $Root "Launch-AuraWave.bat") $PublishDir -Force
    Copy-Item (Join-Path $Root "README.md") $PublishDir -Force
    Copy-Item (Join-Path $Root "LICENSE") $PublishDir -Force
    Copy-Item (Join-Path $Root "THIRD-PARTY-NOTICES.txt") $PublishDir -Force
    Copy-Item (Join-Path $Root "dist\AuraWave-STEP-BY-STEP-GUIDE.md") $PublishDir -Force
} finally {
    Pop-Location
}