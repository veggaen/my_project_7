# VEGGA ROLEPLAY - Player Data System

## 📁 File Structure

```
JsonDataBase/
├── PlayerData/
│   ├── {SteamId64}.json     # Individual player save files
│   └── Backups/
│       ├── Daily/           # Daily backups (24h retention)
│       └── Weekly/          # Weekly backups (7 weeks retention)
├── BannedNames.json         # List of banned usernames
└── activity.log             # Server activity log
```

## 💾 Player Data Format

Each player file (`{SteamId64}.json`) contains:

```json
{
  "SteamId": "76561198012345678",
  "SteamName": "PlayerSteamName",
  "PreferredUsername": "CustomName",
  "Rank": "Guest",
  
  "Money": 500,
  "BankBalance": 0,
  
  "TotalKills": 10,
  "TotalDeaths": 5,
  "TotalArrests": 2,
  "TotalQuestsCompleted": 15,
  "TotalPlaytime": 3600.5,
  "TotalConnects": 25,
  "TotalDisconnects": 24,
  "TotalPropsSpawned": 100,
  
  "FirstSeen": "2025-12-10T12:00:00Z",
  "LastSeen": "2025-12-10T15:30:00Z",
  
  "InventorySlots": 96,
  "ItemIds": [],
  "ItemCounts": [],
  
  "IsStealthMode": false,
  "StealthName": "",
  
  "ShowSteamName": true,
  "ScoreboardSortMode": "money",
  "ScoreboardSortDescending": true,
  "AvatarType": "steam",
  
  "HasCompletedSetup": true,
  "ConsentToDataCollection": true,
  "ConsentDate": "2025-12-10T12:00:00Z"
}
```

## 🚫 Banned Names

`BannedNames.json` contains a list of forbidden usernames:

```json
[
  "admin",
  "moderator",
  "owner",
  "server",
  "system",
  "bot",
  "vegga",
  "staff"
]
```

Usernames are checked case-insensitively and partial matches are blocked.

## 🔄 Auto-Save System

- **On Join**: Player data is automatically loaded from JSON
- **On Disconnect**: Player data is automatically saved to JSON
- **Daily Backups**: Created every time data is saved (24h retention)
- **Weekly Backups**: Created manually (7 weeks retention)

## 📊 Activity Log

`activity.log` tracks all important events:

```
[2025-12-10 12:00:00] LOAD | V3gga (76561198012345678) | Money: $500
[2025-12-10 12:30:00] SAVE | V3gga (76561198012345678) | Money: $650 | Level: 1800s
[2025-12-10 13:00:00] CONNECT | NewPlayer (76561198087654321) | Rank: Guest
[2025-12-10 13:30:00] DISCONNECT | NewPlayer (76561198087654321) | Duration: 30.0m
```

## 🛠️ Setup Instructions

1. **Add PlayerDataPersistence Component**:
   - Open your main scene
   - Create a new GameObject called "DataManager"
   - Add the `PlayerDataPersistence` component to it
   - This will automatically handle save/load on player join/disconnect

2. **Files are saved to**:
   - Windows: `C:\Users\{YourName}\AppData\LocalLow\Facepunch\sbox\data\{GameIdent}\`
   - The `FileSystem.Data` API handles the path automatically

3. **Banned Names**:
   - Edit `BannedNames.json` to add/remove banned usernames
   - Changes take effect immediately (no restart needed)

## 🎮 In-Game Usage

Players will:
1. Join server → Data loads automatically
2. Play game → Stats tracked in real-time
3. Disconnect → Data saves automatically
4. Rejoin → All progress restored!

## 🔧 Manual Save/Load

You can manually save/load player data:

```csharp
// Save
var data = PlayerDataManager.LoadPlayerData( steamId );
data.Money += 100;
PlayerDataManager.SavePlayerData( steamId, data );

// Load
var data = PlayerDataManager.LoadPlayerData( steamId );
Log.Info( $"Player has ${data.Money}" );
```

## 📝 Notes

- All data is stored locally on the server
- SteamId64 is used as the unique identifier
- Data persists across server restarts
- Backups protect against data corruption

