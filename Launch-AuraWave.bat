@echo off
setlocal
set "APP_DIR=%~dp0"
if exist "%APP_DIR%AuraWave.exe" (
    start "" "%APP_DIR%AuraWave.exe"
    exit /b 0
) else (
    echo [ERROR] AuraWave.exe was not found in "%APP_DIR%".
    echo Please extract the entire ZIP archive before running Launch-AuraWave.bat.
    pause
    exit /b 1
)