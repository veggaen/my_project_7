# 🧪 VEGGA ROLEPLAY - COMPLETE TESTING GUIDE

## 📋 PRE-TESTING CHECKLIST

### ✅ Step 1: Verify Files Exist

Check that these files were created:
- `Code/Data/PlayerDataManager.cs` (enhanced with backups)
- `Code/Data/AutoSaveSystem.cs` (new)
- `Code/UI/Setup/FirstTimeSetup.razor` (multi-step wizard)
- `Code/UI/Setup/FirstTimeSetup.razor.scss`
- `Code/UI/Scoreboard/VeggaScoreboard.razor`
- `Code/UI/Scoreboard/VeggaScoreboard.razor.scss`
- `IMPLEMENTATION_SUMMARY.txt` (this summary)

---

### ✅ Step 2: Add Components in Editor

#### 2.1 Open Scene
1. Open `testscenevegga.scene`

#### 2.2 Add FirstTimeSetup
1. In **Hierarchy**, select **"UI Root"**
2. In **Inspector** (right side), scroll to bottom
3. Click **"Add Component"**
4. Type: `FirstTimeSetup`
5. Click it to add
6. ✅ Should see `FirstTimeSetup` in component list

#### 2.3 Add VeggaScoreboard (if not already added)
1. Still on **"UI Root"**
2. Click **"Add Component"**
3. Type: `VeggaScoreboard`
4. Click it to add
5. ✅ Should see `VeggaScoreboard` in component list

#### 2.4 Add AutoSaveSystem
1. In **Hierarchy**, select **scene root** (top level)
2. Right-click → **Create Empty GameObject**
3. Name it: `Systems`
4. Select `Systems`
5. Click **"Add Component"**
6. Type: `AutoSaveSystem`
7. Click it to add
8. In Inspector, set:
   - **Save Interval:** 300 (5 minutes)
   - **Weekly Backup Interval:** 604800 (7 days)

#### 2.5 Save Scene
1. Press **Ctrl + S**
2. ✅ Scene saved!

---

## 🎮 TESTING PHASE 1: First-Time Setup

### Test 1.1: Setup Wizard Appears
1. **Press F5** to start game
2. **Expected:** Welcome modal appears immediately
3. **Should see:**
   ```
   ┌────────────────────────────────────┐
   │ WELCOME TO VEGGA ROLEPLAY          │
   │ Step 1: Choose Your Name           │
   │ ① — ②                              │
   ├────────────────────────────────────┤
   │ Choose Your Username: *            │
   │ [_________________________]        │
   │ This will be displayed...          │
   │                                    │
   │              [NEXT →]              │
   └────────────────────────────────────┘
   ```

### Test 1.2: Username Validation
1. **Click "NEXT →"** without entering username
2. **Expected:** Red error appears: "⚠️ Username is required!"
3. **Type:** `V3gga` (or your preferred name)
4. **Click "NEXT →"**
5. **Expected:** Moves to Step 2

### Test 1.3: Step 2 - Optional Details
1. **Should see:**
   ```
   ┌────────────────────────────────────┐
   │ WELCOME TO VEGGA ROLEPLAY          │
   │ Step 2: Optional Details           │
   │ ① — ②                              │
   ├────────────────────────────────────┤
   │ 👋 Welcome, V3gga!                 │
   │ Would you like to help us...       │
   │                                    │
   │ Display Steam Name:                │
   │ [✓] Show your Steam name...        │
   │                                    │
   │ 📊 Data Collection & Analytics     │
   │ [✓] I consent to anonymous...     │
   │                                    │
   │ [← BACK] [SKIP] [COMPLETE SETUP]  │
   └────────────────────────────────────┘
   ```

2. **Test "BACK" button:**
   - Click "← BACK"
   - Should return to Step 1
   - Click "NEXT →" again

3. **Test "SKIP" button:**
   - Click "SKIP (Limited Mode)"
   - Modal closes
   - Data saved with minimal consent

4. **Test "COMPLETE SETUP":**
   - Check/uncheck consent box
   - Click "COMPLETE SETUP"
   - Modal closes
   - ✅ Setup complete!

---

## 🎮 TESTING PHASE 2: Scoreboard

### Test 2.1: Open Scoreboard
1. **Press Tab** key
2. **Expected:** Scoreboard opens
3. **Should see:**
   - Your username: "V3gga"
   - Rank: Guest
   - Job: Citizen
   - Money: $500
   - Stats: 0/0 K/D, 0 QP, 0 Props, 0ms ping

### Test 2.2: Sorting
1. **Click "RANK"** column header
   - Should sort by rank
   - See ▼ or ▲ arrow
2. **Click "$"** column header
   - Should sort by money
3. **Click "QP"** column header
   - Should sort by quest points

### Test 2.3: Lock/Unlock
1. **Click any column header**
   - Footer changes to: "Press TAB to unlock and close"
   - Scoreboard is "locked" (stays open)
2. **Press Tab**
   - Scoreboard closes
   - ✅ Works!

---

## 🎮 TESTING PHASE 3: Data Persistence

### Test 3.1: Modify Player Data
1. **Press F1** to open console
2. Type: `find PlayerVeggaStats`
3. Click on the result
4. In Inspector, click **"Add $100"** button 5 times
5. **Expected:** Money shows $1000 (500 + 500)

### Test 3.2: Check Console for Save
1. **Wait 5 minutes** OR **stop the game**
2. **Check console** for:
   ```
   💾 Auto-save: Saved 1 player(s)
   ```
   OR
   ```
   💾 AutoSaveSystem: Final save complete
   ```

### Test 3.3: Verify Files Created
1. **Navigate to:**
   ```
   C:\Program Files (x86)\Steam\steamapps\common\sbox\data\{org}\{game}\PlayerData\
   ```
   (Replace `{org}` and `{game}` with your actual org/game name)

2. **Should see:**
   - `TODO_STEAM_ID.json` (main save file)
   - `Backups/Daily/TODO_STEAM_ID_2025-12-09_XX-XX-XX.json`
   - `activity.log`

3. **Open `TODO_STEAM_ID.json`:**
   ```json
   {
     "SteamId": "TODO_STEAM_ID",
     "PreferredUsername": "V3gga",
     "Money": 1000,
     "TotalPlaytime": 300.5,
     ...
   }
   ```

4. **Open `activity.log`:**
   ```
   [2025-12-09 10:30:00] LOAD | New player: TODO_STEAM_ID
   [2025-12-09 10:35:00] SAVE | V3gga (TODO_STEAM_ID) | Money: $1000 | Level: 300s
   [2025-12-09 10:35:00] SYSTEM | AutoSave system stopped (final save complete)
   ```

### Test 3.4: Test Data Loading
1. **Start game again** (F5)
2. **Expected:** Money should still be $1000
3. **Check console:**
   ```
   📂 Loaded: V3gga (TODO_STEAM_ID) | Money: $1000
   ```
4. ✅ Data persisted!

---

## 🎮 TESTING PHASE 4: Backup System

### Test 4.1: Daily Backups
1. **Play for a few minutes**
2. **Stop game**
3. **Check:** `PlayerData/Backups/Daily/`
4. **Should see:** Multiple backup files with timestamps

### Test 4.2: Weekly Backups (Manual Trigger)
1. **In console, type:**
   ```
   find AutoSaveSystem
   ```
2. Click on it
3. In Inspector, find `CreateWeeklyBackups()` method
4. Click it to trigger
5. **Check:** `PlayerData/Backups/Weekly/`
6. **Should see:** Weekly backup file

---

## 📸 SEND ME SCREENSHOTS OF:

1. **Step 1 screen** (username input)
2. **Step 2 screen** (optional details)
3. **Scoreboard open** (with your player visible)
4. **File explorer** showing PlayerData folder with files
5. **activity.log** contents

---

## ✅ SUCCESS CRITERIA:

- [ ] First-time setup wizard works (both steps)
- [ ] Username is required (validation works)
- [ ] Scoreboard shows player data
- [ ] Money persists between sessions
- [ ] JSON files are created
- [ ] Daily backups are created
- [ ] Activity log records events
- [ ] Console is clean (no spam)

---

## 🐛 IF SOMETHING DOESN'T WORK:

1. **Check console for errors** (F1)
2. **Verify components are added** to scene
3. **Check file paths** in File Explorer
4. **Send me screenshots** of the issue
5. **Copy error messages** from console

---

**Ready to test! Start with Phase 1 and work through each phase.** 🚀

