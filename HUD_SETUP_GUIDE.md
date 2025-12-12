# 🎮 HUD & HP System Setup Guide - my_project_7

## ✅ Current Setup Status

Your project already has all the necessary code:
- ✅ `PlayerVeggaStats.cs` - Health, Armor, Money tracking with networking
- ✅ `PlayerHud.razor` - UI component with health/armor bars
- ✅ `PlayerHud.razor.scss` - Styling for the HUD
- ✅ `player_vegga.prefab` - Player prefab with all components

## 🎯 The Problem

The HUD is currently **inside the player prefab** (ScreenLayout GameObject), which means:
- It only shows for the player who owns that specific GameObject
- In multiplayer, each client sees their own HUD from their own player instance

## 🔧 Solution: Two Options

### **Option 1: Scene-Based HUD (Recommended)**

Move the HUD to the scene so it's always visible and reads from `PlayerVeggaStats.Local`.

#### Steps in s&box Editor:

1. **Open Scene:** `Assets/scenes/testscenevegga.scene`

2. **Create UI Root GameObject:**
   - Right-click in Hierarchy → "Create Empty"
   - Name: `UI Root`
   - Position: `0, 0, 0`

3. **Add ScreenPanel Component:**
   - Select "UI Root"
   - Click "Add Component" → Search `ScreenPanel`
   - **Settings:**
     - Auto Screen Scale: ✅ `true`
     - Scale Strategy: `ConsistentHeight`
     - Scale: `1`
     - ZIndex: `100`
     - Opacity: `1`

4. **Add PlayerHud Component:**
   - With "UI Root" selected
   - Click "Add Component" → Search `PlayerHud`
   - Add it

5. **Remove HUD from Player Prefab:**
   - Open `Assets/player_vegga.prefab`
   - Find "ScreenLayout" child GameObject
   - **Delete it** (or disable it)
   - Save prefab

6. **Save Scene**

---

### **Option 2: Keep HUD in Prefab**

The code has been updated to hide the HUD for non-owner players. This should work now.

Just make sure the HUD is enabled in the player prefab.

---

## 🧪 Testing the HUD

### In-Game Console Commands:

Press `~` (tilde) to open console, then try:

```
# Test damage (should reduce HP)
find PlayerVeggaStats
# Click on the component in the list, then click "TestDamage10" button in inspector

# Or use the skill debug commands:
skill_set_hp 50
skill_add_hp 10
```

### What to Look For:

1. **HUD appears** in bottom-left corner
2. **Health bar** shows current HP (should be 10/10 initially based on prefab settings)
3. **Armor bar** shows current armor (should be 10/100 initially)
4. **Money** shows $500
5. **Job** shows "Citizen"

### Debug Output:

Check the console (F1) for log messages:
```
HUD: HP=10/10 (100%) | Armor=10/100 (10%)
```

---

## 🐛 Troubleshooting

### HUD Not Showing:

1. **Check if PlayerVeggaStats.Local is null:**
   - Open console (F1)
   - Look for "NO STATS" in the job field
   - If you see this, the stats component isn't being found

2. **Check Network Ownership:**
   - The player must be owned by the local client
   - In multiplayer, make sure you're testing as the host or a connected client

3. **Check ScreenPanel:**
   - Make sure ScreenPanel is enabled
   - Check that ZIndex is high enough (100+)

### Health Not Updating:

1. **Check PlayerVeggaStats component:**
   - Open player prefab
   - Verify PlayerVeggaStats is enabled
   - Check InitHealth and MaxHealth values

2. **Network Sync:**
   - Health is marked with `[Sync]`
   - Changes must happen on the authority (host/owner)
   - Use the `TestDamage10()` button in inspector

### Bars Not Filling:

1. **Check SCSS is loaded:**
   - The bars use CSS gradients
   - Make sure `PlayerHud.razor.scss` is in the same folder

2. **Check @ref bindings:**
   - `@ref="_healthBar"` must match the Panel variable name
   - Same for `_armorBar`

---

## 📊 Component Hierarchy

### Scene Hierarchy (Option 1):
```
testscenevegga.scene
├── Directional Light
├── Map
├── The Little RootUI Helper
│   ├── TheLittleHelper (component)
│   └── NetworkHelper (component) ← Spawns player_vegga.prefab
└── UI Root ← ADD THIS
    ├── ScreenPanel (component)
    └── PlayerHud (component)
```

### Player Prefab Hierarchy (Option 2):
```
player_vegga.prefab
├── CharacterController
├── PlayerVeggaMovement
├── PlayerVeggaStats ← Health/Armor/Money data
├── PlayerVeggaSkills
├── CitizenAnimationHelper
├── Body (child GameObject)
├── Head (child GameObject)
├── Camera (child GameObject)
└── ScreenLayout (child GameObject) ← HUD is here
    ├── ScreenPanel (component)
    └── PlayerHud (component)
```

---

## 🎨 Customizing the HUD

### Change Colors:

Edit `PlayerHud.razor.scss`:

```scss
.bar-health .bar-fill {
    background: linear-gradient(90deg, #ff5252, #ffb347); // Red to orange
}

.bar-armor .bar-fill {
    background: linear-gradient(90deg, #4fc3f7, #81d4fa); // Blue
}
```

### Change Position:

Edit `PlayerHud.razor.scss`:

```scss
PlayerHud {
    left: 32px;   // Distance from left
    bottom: 32px; // Distance from bottom
}
```

### Change Starting Values:

Edit player prefab → PlayerVeggaStats component:
- MaxHealth: `100`
- InitHealth: `100`
- MaxArmor: `100`
- InitArmor: `50`
- StartMoney: `500`

---

## 🚀 Next Steps

1. ✅ Choose Option 1 or Option 2
2. ✅ Test in-game
3. ✅ Use console commands to test damage
4. ✅ Verify HUD updates in real-time
5. ✅ Test in multiplayer (if applicable)

Good luck! 🎮

