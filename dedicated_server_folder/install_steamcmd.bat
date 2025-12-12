@echo off
setlocal EnableDelayedExpansion

echo ================================================
echo   STEAMCMD INSTALLER FOR WINDOWS 10
echo ================================================
echo.

REM Get the script directory (handles special characters like &)
set "SCRIPT_DIR=%~dp0"
set "STEAMCMD_DIR=%SCRIPT_DIR%steamcmd"
set "STEAMCMD_ZIP=%SCRIPT_DIR%steamcmd.zip"
set "DOWNLOAD_URL=https://steamcdn-a.akamaihd.net/client/installer/steamcmd.zip"

REM Check if already installed
if exist "%STEAMCMD_DIR%\steamcmd.exe" (
    echo SteamCMD already installed at:
    echo %STEAMCMD_DIR%
    echo.
    choice /C YN /M "Re-download and reinstall"
    if errorlevel 2 goto :done
)

REM Create steamcmd folder
if not exist "%STEAMCMD_DIR%" mkdir "%STEAMCMD_DIR%"

echo Downloading SteamCMD...
echo URL: %DOWNLOAD_URL%
echo Destination: %STEAMCMD_ZIP%
echo.

REM Use PowerShell with properly escaped paths
powershell -NoProfile -ExecutionPolicy Bypass -Command ^
    "$ProgressPreference = 'SilentlyContinue'; ^
    Invoke-WebRequest -Uri '%DOWNLOAD_URL%' -OutFile '%STEAMCMD_ZIP%'"

if not exist "%STEAMCMD_ZIP%" (
    echo.
    echo ERROR: Download failed!
    echo.
    echo Please download manually from:
    echo https://developer.valvesoftware.com/wiki/SteamCMD
    echo.
    echo Save steamcmd.zip to: %SCRIPT_DIR%
    echo Then run this script again.
    echo.
    pause
    exit /b 1
)

echo Download complete!
echo.
echo Extracting to: %STEAMCMD_DIR%

REM Use PowerShell to extract with escaped paths
powershell -NoProfile -ExecutionPolicy Bypass -Command ^
    "Expand-Archive -LiteralPath '%STEAMCMD_ZIP%' -DestinationPath '%STEAMCMD_DIR%' -Force"

REM Cleanup zip
if exist "%STEAMCMD_ZIP%" del "%STEAMCMD_ZIP%"

if exist "%STEAMCMD_DIR%\steamcmd.exe" (
    echo.
    echo ================================================
    echo   SUCCESS! SteamCMD installed!
    echo ================================================
    echo Location: %STEAMCMD_DIR%
    echo.
    echo Running first-time update...
    echo.
    
    REM Run SteamCMD once to update itself
    pushd "%STEAMCMD_DIR%"
    steamcmd.exe +quit
    popd
    
    echo.
    echo ================================================
    echo   SteamCMD is ready!
    echo ================================================
    echo.
    echo Next step: Run install_server.bat to install
    echo the s&box dedicated server.
    echo.
) else (
    echo.
    echo ERROR: Extraction failed!
    echo.
    echo Try manual installation:
    echo 1. Download: https://steamcdn-a.akamaihd.net/client/installer/steamcmd.zip
    echo 2. Extract to: %STEAMCMD_DIR%
    echo.
)

:done
endlocal
pause
