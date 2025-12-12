# 🖱️ Editor Click-by-Click Guide

## 📍 Exact Steps to Set Up HUD (Option 1 - Recommended)

### Part 1: Create UI Root in Scene

1. **Open Scene:**
   - In Project panel (bottom), navigate to: `Assets/scenes/`
   - Double-click: `testscenevegga.scene`

2. **Create UI Root GameObject:**
   - In Hierarchy panel (left), right-click in empty space
   - Click: `Create Empty`
   - A new GameObject appears named "GameObject"
   - With it selected, look at Inspector (right)
   - At the top, change name from "GameObject" to: `UI Root`
   - Press Enter

3. **Add ScreenPanel Component:**
   - With "UI Root" still selected in Hierarchy
   - In Inspector panel (right), scroll to bottom
   - Click the button: `Add Component`
   - In the search box, type: `ScreenPanel`
   - Click on `ScreenPanel` in the results
   - Component is now added!

4. **Configure ScreenPanel:**
   - Still in Inspector, find the ScreenPanel component
   - Look for these settings and set them:
   
   ```
   ┌─ ScreenPanel ─────────────────────┐
   │ ✅ Auto Screen Scale              │
   │ Scale Strategy: ConsistentHeight  │ ← Click dropdown
   │ Scale: 1                          │
   │ ZIndex: 100                       │ ← Type this number
   │ Opacity: 1                        │
   │ Target Camera: (empty)            │
   └───────────────────────────────────┘
   ```

5. **Add PlayerHud Component:**
   - With "UI Root" still selected
   - In Inspector, click: `Add Component`
   - Type: `PlayerHud`
   - Click on `PlayerHud` in results
   - Component is now added!

6. **Save Scene:**
   - Press: `Ctrl + S` (or `Cmd + S` on Mac)
   - Or: File → Save

---

### Part 2: Remove HUD from Player Prefab

1. **Open Player Prefab:**
   - In Project panel, navigate to: `Assets/`
   - Double-click: `player_vegga.prefab`
   - Prefab opens in Hierarchy

2. **Find ScreenLayout:**
   - In Hierarchy, expand "player_vegga" (click the arrow)
   - You should see children:
     - Body
     - Head
     - Camera
     - **ScreenLayout** ← This one!

3. **Delete ScreenLayout:**
   - Click on "ScreenLayout" to select it
   - Press: `Delete` key
   - Or right-click → Delete

4. **Save Prefab:**
   - Press: `Ctrl + S`
   - Or: File → Save

5. **Close Prefab:**
   - In Hierarchy, click on the scene tab to go back to scene view

---

### Part 3: Verify Setup

1. **Check Scene Hierarchy:**
   ```
   testscenevegga.scene
   ├── Directional Light
   ├── Map
   ├── Cube (trigger)
   ├── Cube (gold bar)
   ├── The Little RootUI Helper
   │   ├── TheLittleHelper
   │   └── NetworkHelper
   ├── The Little RootConfig Helper
   └── UI Root ← YOU JUST ADDED THIS
       ├── ScreenPanel
       └── PlayerHud
   ```

2. **Check Player Prefab:**
   ```
   player_vegga.prefab
   ├── CharacterController
   ├── PlayerVeggaMovement
   ├── PlayerVeggaStats
   ├── PlayerVeggaSkills
   ├── CitizenAnimationHelper
   ├── Body (child)
   ├── Head (child)
   └── Camera (child)
   (NO ScreenLayout anymore!)
   ```

---

## 🎮 Testing

1. **Launch Game:**
   - Click the Play button (▶️) at top
   - Or press: `F5`

2. **Wait for Spawn:**
   - Game loads
   - Player spawns automatically (NetworkHelper does this)

3. **Look for HUD:**
   - Bottom-left corner of screen
   - Should show:
     - JOB: Citizen
     - Money: $500
     - HP: 10 / 10 (with red/orange bar)
     - ARMOR: 10 / 100 (with blue bar)

4. **Test Damage:**
   - Press: `~` (tilde key) to open console
   - Type: `find PlayerVeggaStats`
   - Press Enter
   - Click on the component in the list
   - In Inspector (right), find the button: `TestDamage10`
   - Click it
   - Watch HP bar decrease to 0 / 10

---

## 🔍 Inspector Settings Reference

### When "UI Root" is Selected:

```
┌─ Inspector ────────────────────────────┐
│ Name: UI Root                          │
│ Position: 0, 0, 0                      │
│ Rotation: 0, 0, 0, 1                   │
│ Scale: 1, 1, 1                         │
│ Tags: (empty)                          │
│ ✅ Enabled                             │
│ Network Mode: 2                        │
│                                        │
│ ┌─ ScreenPanel ──────────────────┐    │
│ │ ✅ Auto Screen Scale           │    │
│ │ Scale Strategy: ConsistentHeight│   │
│ │ Scale: 1                       │    │
│ │ ZIndex: 100                    │    │
│ │ Opacity: 1                     │    │
│ │ Target Camera: (empty)         │    │
│ └────────────────────────────────┘    │
│                                        │
│ ┌─ PlayerHud ────────────────────┐    │
│ │ (no settings to configure)     │    │
│ └────────────────────────────────┘    │
└────────────────────────────────────────┘
```

### When "player_vegga" Prefab Root is Selected:

```
┌─ Inspector ────────────────────────────┐
│ Name: player_vegga                     │
│ Tags: player                           │
│ ✅ Enabled                             │
│                                        │
│ ┌─ CharacterController ──────────┐    │
│ │ Height: 64                     │    │
│ │ Radius: 10                     │    │
│ │ ...                            │    │
│ └────────────────────────────────┘    │
│                                        │
│ ┌─ PlayerVeggaMovement ──────────┐    │
│ │ BaseSpeed: 125                 │    │
│ │ ...                            │    │
│ └────────────────────────────────┘    │
│                                        │
│ ┌─ PlayerVeggaStats ─────────────┐    │
│ │ MaxHealth: 10                  │    │
│ │ InitHealth: 10                 │    │
│ │ MaxArmor: 100                  │    │
│ │ InitArmor: 10                  │    │
│ │ StartMoney: 500                │    │
│ │ StartJobName: Citizen          │    │
│ │                                │    │
│ │ [TestDamage10] ← Click to test │    │
│ └────────────────────────────────┘    │
└────────────────────────────────────────┘
```

---

## 🎯 What Each Component Does

### ScreenPanel
- Renders UI on screen
- Handles scaling for different resolutions
- ZIndex controls layering (higher = on top)

### PlayerHud
- Your custom Razor component
- Reads from PlayerVeggaStats.Local
- Updates health/armor bars every frame
- Shows job and money

### PlayerVeggaStats
- Stores player's health, armor, money
- Synced across network with [Sync]
- Has TestDamage10() button for testing

### NetworkHelper
- Spawns player_vegga.prefab when player joins
- Handles multiplayer networking
- Already in your scene!

---

## ✅ Success Indicators

You know it's working when:
- ✅ HUD appears in bottom-left corner
- ✅ Shows "JOB: Citizen" and "$500"
- ✅ Health bar is full (red/orange gradient)
- ✅ Armor bar is 10% full (blue gradient)
- ✅ Clicking TestDamage10 reduces health
- ✅ Bars animate smoothly when values change
- ✅ Console shows: `HUD: HP=10/10 (100%) | Armor=10/100 (10%)`

---

## 🆘 Troubleshooting

### HUD doesn't appear:
1. Check "UI Root" is in scene (not prefab)
2. Check ScreenPanel ZIndex is 100
3. Check PlayerHud component is enabled
4. Press F1 to see console errors

### "NO STATS" shows:
1. Check player has PlayerVeggaStats component
2. Check player is spawned (look for player model)
3. Check NetworkHelper is spawning player_vegga.prefab

### Bars don't fill:
1. Check PlayerHud.razor.scss file exists
2. Restart editor to reload styles
3. Check console for CSS errors

---

Done! 🎉

