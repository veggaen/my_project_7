@echo off
REM Vegga Server Manager Launcher
REM Handles paths with special characters like & properly

setlocal
set "SCRIPT_DIR=%~dp0"
powershell -NoProfile -ExecutionPolicy Bypass -File "%SCRIPT_DIR%server_manager.ps1"
endlocal
