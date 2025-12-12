@echo off
setlocal EnableDelayedExpansion

echo ================================================
echo   VEGGA PROJECT 7 - DEDICATED SERVER
echo ================================================
echo   Password: vegga123
echo ================================================
echo.

REM Get the script directory (handles special characters like &)
set "SCRIPT_DIR=%~dp0"
set "SERVER_EXE="

REM Option 1: Dedicated server installed via SteamCMD (recommended)
if exist "%SCRIPT_DIR%server_files\sbox-server.exe" (
    set "SERVER_EXE=%SCRIPT_DIR%server_files\sbox-server.exe"
    echo Found: Dedicated server (SteamCMD install)
    goto :start
)

REM Option 2: Regular s&box install (for testing)
if exist "C:\Program Files (x86)\Steam\steamapps\common\sbox\sbox.exe" (
    set "SERVER_EXE=C:\Program Files (x86)\Steam\steamapps\common\sbox\sbox.exe"
    echo Found: Regular s&box client (fallback)
    goto :start
)

echo ERROR: No s&box installation found!
echo.
echo Please either:
echo 1. Run install_server.bat to install dedicated server via SteamCMD
echo 2. Or install s&box via Steam
echo.
pause
exit /b 1

:start
echo.
echo Starting server with: %SERVER_EXE%
echo.

REM Launch the server
"%SERVER_EXE%" -dedicated +hostname "Vegga Project 7 - Dev Server" +sv_password vegga123 +maxplayers 16 +map testscenevegga +gamemode original.my_project_7

echo.
echo Server stopped.
endlocal
pause
