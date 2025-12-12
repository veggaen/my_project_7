# 💾 Player Data Save Location

## 📁 Where Your Data is Saved

Your player data is saved using **s&box FileSystem.Data**, which stores files at:

```
C:\Program Files (x86)\Steam\steamapps\common\sbox\data\original\my_project_7\
```

### File Structure:

```
C:\Program Files (x86)\Steam\steamapps\common\sbox\data\original\my_project_7\
├── PlayerData/
│   ├── {YourSteamId64}.json          ← Your player data
│   ├── {OtherPlayerSteamId}.json     ← Other players
│   └── Backups/
│       ├── {SteamId}_last.json       ← Last save
│       ├── {SteamId}_hourly.json     ← 1 hour ago
│       ├── {SteamId}_daily.json      ← 1 day ago
│       └── {SteamId}_weekly.json     ← 1 week ago
├── BannedNames.json                   ← List of banned usernames
└── PlayerData/activity.log            ← Activity log
```

---

## 🔍 How to Find Your Data

### Method 1: Check Console Logs

When you join/disconnect, the console will show:

```
📖 Loaded from: C:\Program Files (x86)\Steam\steamapps\common\sbox\data\original\my_project_7\PlayerData\76561198012345678.json
💾 Saved to: C:\Program Files (x86)\Steam\steamapps\common\sbox\data\original\my_project_7\PlayerData\76561198012345678.json
```

### Method 2: Navigate Manually

1. Open File Explorer
2. Paste this path: `C:\Program Files (x86)\Steam\steamapps\common\sbox\data\original\my_project_7\PlayerData\`
3. Look for files named with your SteamId64

### Method 3: Find Your SteamId64

1. Go to https://steamid.io/
2. Enter your Steam profile URL
3. Copy your **SteamId64** (e.g., `76561198012345678`)
4. Look for `{YourSteamId64}.json` in the PlayerData folder

---

## 📊 What's Saved

Your player file contains:

```json
{
  "SteamId": "76561198012345678",
  "SteamName": "YourSteamName",
  "PreferredUsername": "YourCustomName",
  "Rank": "Guest",
  
  "Money": 650,
  "BankBalance": 0,
  
  "TotalKills": 5,
  "TotalDeaths": 2,
  "TotalPlaytime": 1800.5,
  "TotalConnects": 10,
  
  "FirstSeen": "2025-12-10T12:00:00Z",
  "LastSeen": "2025-12-10T15:30:00Z"
}
```

---

## 💰 Starting Money System

### New Players:
- **First join** → Get **$500** starting money
- Data file created with `Money: 500`

### Returning Players:
- **Rejoin** → Money loaded from saved file
- Example: If you had $650, you'll have $650 again

### How It Works:

1. **Player joins** → System checks for `{SteamId}.json`
2. **File exists?**
   - ✅ YES → Load saved money (e.g., $650)
   - ❌ NO → Create new file with $500
3. **Player plays** → Money changes (earn/spend)
4. **Player disconnects** → Save current money to file
5. **Player rejoins** → Load saved money again

---

## 🔄 When Data is Saved

### Automatic Saves:

1. **On Disconnect** (always)
   - Instant save when you leave the server
   - No data loss!

2. **On Money Change** (event-based)
   - When you earn money
   - When you spend money
   - Triggers auto-save after 5 minutes

3. **Safety Auto-Save** (every 5 minutes)
   - Only if data changed
   - Prevents data loss from crashes

---

## 🛠️ Troubleshooting

### "I don't see my data file!"

1. Make sure you've **disconnected** from the server (data saves on disconnect)
2. Check the console for the save path
3. Look for your SteamId64 (not your username)

### "My money reset to $500!"

This means:
- Your data file was deleted/corrupted
- You're using a different Steam account
- The save system isn't running (check for `PlayerDataPersistence` in scene)

### "Where is the JsonDataBase folder?"

The `JsonDataBase` folder in your project is just for documentation. **Actual data** is saved to:
```
C:\Program Files (x86)\Steam\steamapps\common\sbox\data\original\my_project_7\
```

This is s&box's standard data location (safe, backed up, persistent).

---

## 📝 Activity Log

Check `activity.log` for all save/load events:

```
[2025-12-10 12:00:00] LOAD | V3gga (76561198012345678) | Money: $500
[2025-12-10 12:30:00] SAVE | V3gga (76561198012345678) | Money: $650 | Playtime: 1800s
```

---

## ✅ Summary

**Your data is saved at:**
```
C:\Program Files (x86)\Steam\steamapps\common\sbox\data\original\my_project_7\PlayerData\{YourSteamId64}.json
```

**Check console logs** to see the exact path when you join/disconnect!

**Starting money:** $500 for new players, loaded from file for returning players.

**Your progress is safe!** 🎉

