# 🎮 Inspector Guide - PlayerVeggaStats

## 📊 What You'll See in Inspector

When you select a player GameObject with PlayerVeggaStats component, you'll now see:

### ⚙️ Config Section (Editable)
These are the **starting values** when the player spawns:

```
┌─ Config ──────────────────────┐
│ Max Health: 100               │ ← Maximum HP
│ Init Health: 100              │ ← Starting HP
│ Max Armor: 100                │ ← Maximum Armor
│ Init Armor: 50                │ ← Starting Armor
│ Start Money: 500              │ ← Starting Money
│ Start Job Name: Citizen       │ ← Starting Job
└───────────────────────────────┘
```

**You can edit these!** They affect the next spawn.

---

### 📈 Runtime Stats Section (Read-Only)
These show the **current live values** during gameplay:

```
┌─ Runtime Stats ───────────────┐
│ Health: 100 (read-only)       │ ← Current HP
│ Armor: 50 (read-only)         │ ← Current Armor
│ Money: 500 (read-only)        │ ← Current Money
│ Job Name: Citizen (read-only) │ ← Current Job
└───────────────────────────────┘
```

**These update in real-time!** Watch them change as you play.

---

### 🧪 Testing Section (Buttons)
Click these buttons to test the HUD:

```
┌─ Testing ─────────────────────┐
│ [Damage 10 HP]                │ ← Remove 10 HP
│ [Damage 50 HP]                │ ← Remove 50 HP
│ [Heal to Full]                │ ← Restore to max HP
│ [Add $100]                    │ ← Add $100
│ [Add $1000]                   │ ← Add $1000
│ [Set Full Armor]              │ ← Set armor to max
│ [Remove All Armor]            │ ← Set armor to 0
│ [Kill Player]                 │ ← Set HP and Armor to 0
└───────────────────────────────┘
```

---

## 🎯 How to Use It

### During Gameplay:

1. **Press F5** to start the game
2. **Press F1** to open the console
3. Type: `find PlayerVeggaStats`
4. **Click on the component** in the list
5. Inspector shows the component on the right
6. **Watch "Runtime Stats"** update in real-time!
7. **Click buttons** in "Testing" section to test

### Example Testing Flow:

```
1. Start game (F5)
2. Find PlayerVeggaStats (F1 → find PlayerVeggaStats)
3. Click on it
4. Watch Runtime Stats:
   - Health: 100
   - Armor: 50
   - Money: 500

5. Click [Damage 10 HP]
   - Health: 90 ← Changed!
   - HUD updates instantly!

6. Click [Add $100]
   - Money: 600 ← Changed!
   - HUD updates instantly!

7. Click [Damage 50 HP]
   - Health: 40 ← Changed!
   - Armor: 50 (armor absorbs damage first)

8. Click [Heal to Full]
   - Health: 100 ← Back to max!
```

---

## 🔧 Changing Starting Values

### Before Starting Game:

1. Open `player_vegga.prefab`
2. Select the root "player_vegga" GameObject
3. Find PlayerVeggaStats component
4. Edit **Config** section:
   - Max Health: `100` (change to whatever you want)
   - Init Health: `100` (starting HP)
   - Max Armor: `100`
   - Init Armor: `50`
   - Start Money: `500`

5. **Save prefab** (Ctrl + S)
6. **Test in game** (F5)

### Example: Make a Tank Character

```
Config:
- Max Health: 200  ← Double HP!
- Init Health: 200
- Max Armor: 150   ← More armor!
- Init Armor: 100
- Start Money: 1000 ← Rich!
```

### Example: Make a Glass Cannon

```
Config:
- Max Health: 50   ← Low HP!
- Init Health: 50
- Max Armor: 25    ← Low armor!
- Init Armor: 0
- Start Money: 2000 ← But rich!
```

---

## 📝 What Changed

### Before:
- Health/Armor/Money were hidden
- Had to use console commands to see values
- Only one test button

### After:
- ✅ **Runtime Stats** visible in Inspector
- ✅ **8 test buttons** for easy testing
- ✅ **Grouped** for better organization
- ✅ **Read-only** indicators on runtime values
- ✅ **Better default values** (100 HP instead of 10)

---

## 🎨 Inspector Layout

```
PlayerVeggaStats Component
├── Config (editable)
│   ├── Max Health: 100
│   ├── Init Health: 100
│   ├── Max Armor: 100
│   ├── Init Armor: 50
│   ├── Start Money: 500
│   └── Start Job Name: Citizen
│
├── Runtime Stats (read-only)
│   ├── Health: 100
│   ├── Armor: 50
│   ├── Money: 500
│   └── Job Name: Citizen
│
└── Testing (buttons)
    ├── [Damage 10 HP]
    ├── [Damage 50 HP]
    ├── [Heal to Full]
    ├── [Add $100]
    ├── [Add $1000]
    ├── [Set Full Armor]
    ├── [Remove All Armor]
    └── [Kill Player]
```

---

## 💡 Pro Tips

1. **Watch Runtime Stats while playing** - They update every frame!
2. **Use buttons during gameplay** - Test damage/healing instantly
3. **Edit Config before starting** - Set up your character
4. **Use [Kill Player] button** - Test death/respawn systems
5. **Use [Add $1000] button** - Test shop systems

---

## 🐛 Troubleshooting

### Runtime Stats show 0:
- Player hasn't spawned yet
- Wait for NetworkHelper to spawn player
- Check console for spawn messages

### Buttons don't work:
- Make sure you're clicking during gameplay (F5)
- Check you found the right component (find PlayerVeggaStats)
- Make sure it's the local player's component

### Values don't update:
- Make sure you're looking at Runtime Stats, not Config
- Runtime Stats update automatically
- If frozen, restart the game

---

Enjoy testing! 🎮

