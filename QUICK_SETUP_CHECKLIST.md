# ⚡ Quick Setup Checklist - HUD System

## 🎯 Choose Your Approach

### ✅ Option 1: Scene-Based HUD (Recommended)

**Pros:** Always visible, simpler networking, easier to debug
**Cons:** One extra GameObject in scene

#### Steps:

1. **In Scene Hierarchy:**
   ```
   Right-click → Create Empty → Name: "UI Root"
   ```

2. **Add Components to "UI Root":**
   - Add Component → `ScreenPanel`
     - ✅ Auto Screen Scale: `true`
     - Scale Strategy: `ConsistentHeight`
     - Scale: `1`
     - ZIndex: `100`
   
   - Add Component → `PlayerHud`

3. **In Player Prefab:**
   - Open `player_vegga.prefab`
   - Find "ScreenLayout" GameObject
   - **Delete it** or disable it

4. **Save Everything**

---

### ✅ Option 2: Prefab-Based HUD

**Pros:** HUD moves with player, no scene changes needed
**Cons:** More complex, requires ownership checks

#### Steps:

1. **Already Done!** 
   - The code has been updated with ownership checks
   - HUD will only show for the local player

2. **Verify in Player Prefab:**
   - Open `player_vegga.prefab`
   - Check "ScreenLayout" GameObject exists
   - Check it has:
     - ✅ ScreenPanel component
     - ✅ PlayerHud component

3. **Save Prefab**

---

## 🧪 Testing Checklist

### Launch Game:

1. ✅ Press F5 or click Play
2. ✅ Wait for player to spawn
3. ✅ Look at bottom-left corner

### Expected Result:

```
┌─────────────────────────┐
│ JOB: Citizen    $500    │
│ HP: 10 / 10             │
│ ████████████████████    │ ← Red/Orange bar
│ ARMOR: 10 / 100         │
│ ██                      │ ← Blue bar
└─────────────────────────┘
```

### Test Damage:

1. Open Console (`~` key)
2. Type: `find PlayerVeggaStats`
3. Click on the component in the list
4. In Inspector, click **"TestDamage10"** button
5. Watch HP bar decrease

---

## 🐛 Common Issues & Fixes

### Issue: "NO STATS" shows in HUD

**Cause:** PlayerVeggaStats.Local is returning null

**Fix:**
1. Check player prefab has PlayerVeggaStats component
2. Check Network.IsOwner is true for your player
3. Check console for errors

---

### Issue: HUD not visible

**Cause:** ScreenPanel not configured or HUD hidden

**Fix:**
1. Check ScreenPanel ZIndex is 100+
2. Check PlayerHud component is enabled
3. Check Panel.Style.Display is not None

---

### Issue: Bars not filling

**Cause:** SCSS not loaded or @ref not working

**Fix:**
1. Check `PlayerHud.razor.scss` exists in same folder
2. Restart s&box editor to reload styles
3. Check console for CSS errors

---

### Issue: Health not syncing in multiplayer

**Cause:** Network authority issue

**Fix:**
1. Damage must be applied on the authority (host/owner)
2. Check `[Sync]` attribute is on Health property
3. Use `Network.IsProxy` checks in PlayerVeggaStats

---

## 📝 Component Settings Reference

### ScreenPanel Settings:
```
Auto Screen Scale: ✅ true
Scale Strategy: ConsistentHeight
Scale: 1
ZIndex: 100
Opacity: 1
Target Camera: (leave empty for main camera)
```

### PlayerVeggaStats Settings (in prefab):
```
MaxHealth: 10
InitHealth: 10
MaxArmor: 100
InitArmor: 10
StartMoney: 500
StartJobName: "Citizen"
```

### NetworkHelper Settings (in scene):
```
PlayerPrefab: player_vegga.prefab
StartServer: ✅ true
SpawnPoints: (empty - uses default spawn)
```

---

## 🎨 Quick Customization

### Change HUD Position:

Edit `PlayerHud.razor.scss` line 3-5:
```scss
left: 32px;   // Distance from left edge
bottom: 32px; // Distance from bottom edge
```

### Change Starting Health:

Edit `player_vegga.prefab` → PlayerVeggaStats:
```
MaxHealth: 100  // Change from 10 to 100
InitHealth: 100 // Change from 10 to 100
```

### Change Bar Colors:

Edit `PlayerHud.razor.scss` line 93-99:
```scss
.bar-health .bar-fill {
    background: linear-gradient(90deg, #YOUR_COLOR_1, #YOUR_COLOR_2);
}
```

---

## ✅ Final Checklist

- [ ] Chose Option 1 or Option 2
- [ ] Created/verified UI components
- [ ] Tested in-game
- [ ] HUD appears in bottom-left
- [ ] Health/Armor bars visible
- [ ] Tested damage with TestDamage10 button
- [ ] Bars update correctly
- [ ] No console errors

---

## 🆘 Still Not Working?

1. Check console (F1) for errors
2. Verify all files are saved
3. Restart s&box editor
4. Try rebuilding the project
5. Check the full guide: `HUD_SETUP_GUIDE.md`

Good luck! 🚀

