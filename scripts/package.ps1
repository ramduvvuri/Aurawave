$ErrorActionPreference = "Stop"
$Root = Split-Path -Parent $PSScriptRoot
$DistDir = Join-Path $Root "dist"

& (Join-Path $PSScriptRoot "build.ps1")
& (Join-Path $PSScriptRoot "test.ps1")
& (Join-Path $PSScriptRoot "publish.ps1")

$PortableZip = Join-Path $DistDir "AuraWave-Portable-Windows-x64.zip"
$SourceZip   = Join-Path $DistDir "AuraWave-Source.zip"
$BundleZip   = Join-Path $DistDir "AuraWave-Complete-Bundle.zip"

foreach ($z in @($PortableZip, $SourceZip, $BundleZip)) {
    if (Test-Path $z) { Remove-Item -Force $z }
}

Write-Host "==> Creating AuraWave-Portable-Windows-x64.zip..." -ForegroundColor Cyan
Compress-Archive -Path (Join-Path $DistDir "publish\AuraWave\*") -DestinationPath $PortableZip -Force

Write-Host "==> Creating AuraWave-Source.zip..." -ForegroundColor Cyan
$SourceStaging = Join-Path $DistDir "staging\AuraWave-Source"
if (Test-Path $SourceStaging) { Remove-Item -Recurse -Force $SourceStaging }
New-Item -ItemType Directory -Path $SourceStaging -Force | Out-Null

$ItemsToCopy = @("AuraWave.sln", "Directory.Build.props", ".gitignore", "README.md", "LICENSE", "THIRD-PARTY-NOTICES.txt", "CHANGELOG.md", "Launch-AuraWave.bat", "src", "tests", "scripts", "docs")
foreach ($item in $ItemsToCopy) {
    $srcPath = Join-Path $Root $item
    if (Test-Path $srcPath) {
        Copy-Item -Path $srcPath -Destination $SourceStaging -Recurse -Force
    }
}
Get-ChildItem -Path $SourceStaging -Include "bin","obj" -Directory -Recurse | Remove-Item -Recurse -Force
Compress-Archive -Path "$SourceStaging\*" -DestinationPath $SourceZip -Force

Write-Host "==> Creating AuraWave-Complete-Bundle.zip..." -ForegroundColor Cyan
$BundleStaging = Join-Path $DistDir "staging\AuraWave-Bundle"
if (Test-Path $BundleStaging) { Remove-Item -Recurse -Force $BundleStaging }
New-Item -ItemType Directory -Path $BundleStaging -Force | Out-Null

Copy-Item $PortableZip $BundleStaging -Force
Copy-Item $SourceZip $BundleStaging -Force
Copy-Item (Join-Path $DistDir "AuraWave-STEP-BY-STEP-GUIDE.md") $BundleStaging -Force
Copy-Item (Join-Path $Root "README.md") $BundleStaging -Force
Copy-Item (Join-Path $Root "LICENSE") $BundleStaging -Force
Compress-Archive -Path "$BundleStaging\*" -DestinationPath $BundleZip -Force

Remove-Item -Recurse -Force (Join-Path $DistDir "staging")

$SumsFile = Join-Path $DistDir "SHA256SUMS.txt"
$Lines = @()
foreach ($z in @($PortableZip, $SourceZip, $BundleZip)) {
    $hash = (Get-FileHash -Path $z -Algorithm SHA256).Hash
    $size = (Get-Item $z).Length
    $name = Split-Path -Leaf $z
    $Lines += "$hash  $name ($size bytes)"
}
$Lines | Set-Content -Path $SumsFile -Encoding UTF8
Write-Host "==> Packaging Complete! Checksums:" -ForegroundColor Green
$Lines | ForEach-Object { Write-Host "    $_" -ForegroundColor Green }