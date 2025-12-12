@echo off
setlocal EnableDelayedExpansion

echo ================================================
echo   S&BOX DEDICATED SERVER INSTALLER
echo ================================================
echo.

REM Get the script directory
set "SCRIPT_DIR=%~dp0"
set "SERVER_DIR=%SCRIPT_DIR%server_files"
set "STEAMCMD_EXE="

REM Look for SteamCMD in multiple locations
if exist "%SCRIPT_DIR%steamcmd\steamcmd.exe" (
    set "STEAMCMD_EXE=%SCRIPT_DIR%steamcmd\steamcmd.exe"
    echo Found SteamCMD at: steamcmd\steamcmd.exe
    goto :found
)

if exist "%SCRIPT_DIR%steamcmd.exe" (
    set "STEAMCMD_EXE=%SCRIPT_DIR%steamcmd.exe"
    echo Found SteamCMD at: steamcmd.exe
    goto :found
)

if exist "%SCRIPT_DIR%SteamCMD\steamcmd.exe" (
    set "STEAMCMD_EXE=%SCRIPT_DIR%SteamCMD\steamcmd.exe"
    echo Found SteamCMD at: SteamCMD\steamcmd.exe
    goto :found
)

echo ERROR: SteamCMD not found!
echo.
echo Please place steamcmd.exe in one of these locations:
echo   %SCRIPT_DIR%steamcmd.exe
echo   %SCRIPT_DIR%steamcmd\steamcmd.exe
echo.
echo Download from: https://steamcdn-a.akamaihd.net/client/installer/steamcmd.zip
echo.
pause
exit /b 1

:found
echo.
echo ================================================
echo   Installing s&box Dedicated Server
echo ================================================
echo App ID: 1892930
echo Install to: %SERVER_DIR%
echo.

REM Create server directory
if not exist "%SERVER_DIR%" mkdir "%SERVER_DIR%"

REM Get the directory where steamcmd.exe is located
for %%I in ("%STEAMCMD_EXE%") do set "STEAMCMD_DIR=%%~dpI"

REM Run SteamCMD to install/update s&box server
pushd "%STEAMCMD_DIR%"
steamcmd.exe +force_install_dir "%SERVER_DIR%" +login anonymous +app_update 1892930 validate +quit
popd

echo.
echo ================================================
if exist "%SERVER_DIR%\sbox-server.exe" (
    echo   SUCCESS! Server installed!
    echo   Location: %SERVER_DIR%
    echo.
    echo   Run start_server.bat to start your server.
) else (
    echo   Installation may have failed.
    echo   Check the output above for errors.
)
echo ================================================
echo.

endlocal
pause
