================================================
  VEGGA PROJECT 7 - DEDICATED SERVER SETUP
================================================

PASSWORD: vegga123

================================================
  QUICK START
================================================

1. Double-click "Manager.bat" for a menu-driven interface
   OR
2. Double-click "start_server.bat" to start directly

================================================
  FILES
================================================

Manager.bat           - Easy menu interface (recommended!)
start_server.bat      - Direct server launcher
install_steamcmd.bat  - Downloads & installs SteamCMD automatically
install_server.bat    - Install dedicated server via SteamCMD
server_manager.ps1    - PowerShell manager script
server.cfg            - Server configuration
users/config.json     - Admin permissions (V3gga is Owner)
README.txt            - This file

================================================
  FIRST TIME SETUP (Automated!)
================================================

For a REAL dedicated server (not just using your client):

1. Run Manager.bat (double-click)

2. Select [2] Install SteamCMD
   - Downloads automatically from Valve
   - Installs to: dedicated_server_folder\steamcmd\

3. Select [3] Install/Update s&box Server
   - Downloads the official s&box dedicated server (App ID 1892930)
   - Installs to: dedicated_server_folder\server_files\

4. Select [1] Start Server
   - Your server is now running!

That's it! Everything is automated.

================================================
  ALTERNATIVE: Use Existing s&box Client
================================================

If you just want to test locally, the start_server.bat will
automatically fall back to your regular s&box installation at:
C:\Program Files (x86)\Steam\steamapps\common\sbox\sbox.exe

================================================
  CONNECTING TO YOUR SERVER
================================================

From s&box console (~):

  Local (same PC):     connect localhost
  LAN (same network):  connect 192.168.x.x:27015
  Internet:            connect YOUR_PUBLIC_IP:27015

Password: vegga123

================================================
  PORT FORWARDING (for internet access)
================================================

Forward these ports on your router:
  - 27015 UDP (game traffic)
  - 27015 TCP (RCON, optional)

================================================
  ADMIN PERMISSIONS
================================================

Your SteamID (76561198050516440) has full owner permissions.

To add more admins, edit users/config.json:

[
    {
        "SteamId": "76561198050516440",
        "Claims": ["kick", "ban", "restart", "admin", "owner"],
        "Name": "V3gga (Owner)"
    },
    {
        "SteamId": "OTHER_STEAMID_HERE",
        "Claims": ["kick", "ban"],
        "Name": "Moderator Name"
    }
]

================================================
  EXTERNAL TOOL (OPTIONAL)
================================================

For a fancier GUI server manager, check out:
https://github.com/timmybo5/sbox-server-manager

Features:
- Visual console
- Player list with kicking
- Multi-server support
- Map/gamemode browser

================================================
  CONSOLE COMMANDS (IN-GAME)
================================================

Money Testing:
  vegga_money_info        - Show money & save file info
  vegga_add_money 100     - Add money and save
  vegga_save_now          - Force save to disk
  vegga_check_save_file   - Print raw JSON save

Skills:
  vegga_set_skill_level Attack 30
  vegga_add_skill_xp Mining 5000
  vegga_print_combat
  vegga_print_skill Fishing

Server (admin only):
  status                  - Show connected players
  kick <name>             - Kick a player
  changelevel <map>       - Change map

================================================
