# Vegga Project 7 - Simple Server Manager
# Run with: powershell -ExecutionPolicy Bypass -File server_manager.ps1

$Host.UI.RawUI.WindowTitle = "Vegga Server Manager"

# Get script directory properly (handles special characters like &)
$ScriptPath = $PSScriptRoot
if (-not $ScriptPath) {
    $ScriptPath = Split-Path -Parent $MyInvocation.MyCommand.Definition
}

# Use a simple path for server files (avoids & character issues with SteamCMD)
$ServerInstallPath = "C:\SboxServer"

function Find-SteamCMD {
    # Check multiple possible locations
    $locations = @(
        (Join-Path $ScriptPath "steamcmd\steamcmd.exe"),
        (Join-Path $ScriptPath "steamcmd.exe"),
        (Join-Path $ScriptPath "SteamCMD\steamcmd.exe")
    )
    
    foreach ($loc in $locations) {
        if (Test-Path -LiteralPath $loc) {
            return $loc
        }
    }
    return $null
}

function Show-Menu {
    Clear-Host
    Write-Host "================================================" -ForegroundColor Cyan
    Write-Host "   VEGGA PROJECT 7 - SERVER MANAGER" -ForegroundColor Yellow
    Write-Host "================================================" -ForegroundColor Cyan
    Write-Host ""
    Write-Host "  [1] Start Server" -ForegroundColor Green
    Write-Host "  [2] Install/Update s&box Server" -ForegroundColor Green
    Write-Host "  [3] Edit Server Config" -ForegroundColor Green
    Write-Host "  [4] Edit Admin Users" -ForegroundColor Green
    Write-Host "  [5] Check Server Status" -ForegroundColor Green
    Write-Host "  [6] Open Server Folder" -ForegroundColor Green
    Write-Host ""
    Write-Host "  [Q] Quit" -ForegroundColor Red
    Write-Host ""
    Write-Host "================================================" -ForegroundColor Cyan
    
    # Show installation status
    Write-Host ""
    $steamcmdPath = Find-SteamCMD
    $serverPath = Join-Path $ServerInstallPath "sbox-server.exe"
    
    if ($steamcmdPath) {
        Write-Host "  SteamCMD: " -NoNewline; Write-Host "Installed" -ForegroundColor Green
    } else {
        Write-Host "  SteamCMD: " -NoNewline; Write-Host "Not found" -ForegroundColor Red
    }
    
    if (Test-Path -LiteralPath $serverPath) {
        Write-Host "  Server:   " -NoNewline; Write-Host "Installed at $ServerInstallPath" -ForegroundColor Green
    } else {
        Write-Host "  Server:   " -NoNewline; Write-Host "Not installed" -ForegroundColor Red
    }
    Write-Host ""
}

function Start-GameServer {
    Clear-Host
    Write-Host "================================================" -ForegroundColor Cyan
    Write-Host "   STARTING SERVER" -ForegroundColor Yellow
    Write-Host "================================================" -ForegroundColor Cyan
    Write-Host ""
    
    $serverExe = Join-Path $ServerInstallPath "sbox-server.exe"
    
    if (-not (Test-Path -LiteralPath $serverExe)) {
        Write-Host "ERROR: Server not installed!" -ForegroundColor Red
        Write-Host "Press [2] to install the server first." -ForegroundColor Yellow
        Write-Host ""
        Pause
        return
    }
    
    Write-Host "Server: $serverExe" -ForegroundColor Gray
    Write-Host "Password: vegga123" -ForegroundColor Gray
    Write-Host ""
    Write-Host "Starting server in new window..." -ForegroundColor Yellow
    Write-Host ""
    
    # Start the server directly
    Start-Process -FilePath $serverExe -ArgumentList "-dedicated +hostname `"Vegga Project 7 - Dev Server`" +sv_password vegga123 +maxplayers 16 +map testscenevegga +gamemode original.my_project_7" -WorkingDirectory $ServerInstallPath
    
    Write-Host "Server window opened!" -ForegroundColor Green
    Write-Host ""
    Write-Host "To connect, open s&box and type in console:" -ForegroundColor Yellow
    Write-Host "  connect localhost" -ForegroundColor White
    Write-Host "  (password: vegga123)" -ForegroundColor Gray
    Write-Host ""
    Pause
}

function Install-Server {
    Clear-Host
    Write-Host "================================================" -ForegroundColor Cyan
    Write-Host "   INSTALLING S&BOX DEDICATED SERVER" -ForegroundColor Yellow
    Write-Host "================================================" -ForegroundColor Cyan
    Write-Host ""
    
    $steamcmdPath = Find-SteamCMD
    if (-not $steamcmdPath) {
        Write-Host "ERROR: SteamCMD not found!" -ForegroundColor Red
        Write-Host ""
        Write-Host "Please place steamcmd.exe in:" -ForegroundColor Yellow
        Write-Host "  $ScriptPath" -ForegroundColor White
        Write-Host ""
        Pause
        return
    }
    
    $steamcmdDir = Split-Path -Parent $steamcmdPath
    
    Write-Host "SteamCMD: $steamcmdPath" -ForegroundColor Gray
    Write-Host "Server will install to: $ServerInstallPath" -ForegroundColor Gray
    Write-Host ""
    Write-Host "NOTE: Installing to C:\SboxServer to avoid path issues" -ForegroundColor Yellow
    Write-Host "      (Your project folder has '&' which breaks SteamCMD)" -ForegroundColor Yellow
    Write-Host ""
    Write-Host "App ID: 1892930 (s&box Dedicated Server)" -ForegroundColor Gray
    Write-Host ""
    Write-Host "================================================" -ForegroundColor Cyan
    Write-Host "  DOWNLOADING... This may take 10-30 minutes" -ForegroundColor Yellow
    Write-Host "  (Server is about 2-3 GB)" -ForegroundColor Yellow
    Write-Host "================================================" -ForegroundColor Cyan
    Write-Host ""
    
    # Create server directory
    if (-not (Test-Path -LiteralPath $ServerInstallPath)) {
        New-Item -ItemType Directory -Path $ServerInstallPath -Force | Out-Null
        Write-Host "Created directory: $ServerInstallPath" -ForegroundColor Gray
    }
    
    # Run SteamCMD directly in this window so user sees progress
    Push-Location $steamcmdDir
    
    Write-Host ""
    Write-Host "Running SteamCMD..." -ForegroundColor Cyan
    Write-Host ""
    
    # Execute SteamCMD and show output
    & "$steamcmdPath" +force_install_dir $ServerInstallPath +login anonymous +app_update 1892930 validate +quit
    
    Pop-Location
    
    Write-Host ""
    Write-Host "================================================" -ForegroundColor Cyan
    
    $serverExe = Join-Path $ServerInstallPath "sbox-server.exe"
    if (Test-Path -LiteralPath $serverExe) {
        Write-Host "  SUCCESS! Server installed!" -ForegroundColor Green
        Write-Host "  Location: $ServerInstallPath" -ForegroundColor White
        Write-Host ""
        Write-Host "  Press [1] in the menu to start your server." -ForegroundColor Yellow
    } else {
        Write-Host "  Installation may have failed." -ForegroundColor Red
        Write-Host "  Check the output above for errors." -ForegroundColor Yellow
    }
    
    Write-Host "================================================" -ForegroundColor Cyan
    Write-Host ""
    Pause
}

function Edit-Config {
    $configPath = Join-Path $ScriptPath "server.cfg"
    if (Test-Path -LiteralPath $configPath) {
        Start-Process "notepad.exe" -ArgumentList "`"$configPath`""
    } else {
        Write-Host "Config file not found: $configPath" -ForegroundColor Red
        Pause
    }
}

function Edit-Users {
    $usersPath = Join-Path $ScriptPath "users\config.json"
    if (Test-Path -LiteralPath $usersPath) {
        Start-Process "notepad.exe" -ArgumentList "`"$usersPath`""
    } else {
        Write-Host "Users file not found: $usersPath" -ForegroundColor Red
        Pause
    }
}

function Check-Status {
    Clear-Host
    Write-Host "================================================" -ForegroundColor Cyan
    Write-Host "   SERVER STATUS CHECK" -ForegroundColor Yellow
    Write-Host "================================================" -ForegroundColor Cyan
    Write-Host ""
    
    Write-Host "Checking for running s&box processes..." -ForegroundColor Gray
    $processes = Get-Process -Name "sbox*" -ErrorAction SilentlyContinue
    if ($processes) {
        Write-Host ""
        Write-Host "Running s&box processes:" -ForegroundColor Green
        $processes | Format-Table Name, Id, CPU, @{N='Memory (MB)';E={[math]::Round($_.WorkingSet64/1MB,2)}} -AutoSize
    } else {
        Write-Host "  No s&box processes running." -ForegroundColor Yellow
    }
    
    Write-Host ""
    Write-Host "Checking port 27015..." -ForegroundColor Gray
    $portCheck = netstat -an | Select-String ":27015"
    if ($portCheck) {
        Write-Host "  Port 27015 is IN USE:" -ForegroundColor Green
        $portCheck | ForEach-Object { Write-Host "    $($_.Line)" -ForegroundColor White }
    } else {
        Write-Host "  Port 27015 is free (server not running)." -ForegroundColor Yellow
    }
    
    Write-Host ""
    Write-Host "Server install path: $ServerInstallPath" -ForegroundColor Gray
    Write-Host ""
    Write-Host "================================================" -ForegroundColor Cyan
    Write-Host ""
    Pause
}

function Open-Folder {
    if (Test-Path -LiteralPath $ServerInstallPath) {
        Start-Process "explorer.exe" -ArgumentList "`"$ServerInstallPath`""
    } else {
        Start-Process "explorer.exe" -ArgumentList "`"$ScriptPath`""
    }
}

# Main loop
do {
    Show-Menu
    $choice = Read-Host "Select option"
    
    switch ($choice.ToUpper()) {
        "1" { Start-GameServer }
        "2" { Install-Server }
        "3" { Edit-Config }
        "4" { Edit-Users }
        "5" { Check-Status }
        "6" { Open-Folder }
        "Q" { break }
        default { Write-Host "Invalid option!" -ForegroundColor Red; Start-Sleep -Seconds 1 }
    }
} while ($choice.ToUpper() -ne "Q")

Write-Host "Goodbye!" -ForegroundColor Cyan
