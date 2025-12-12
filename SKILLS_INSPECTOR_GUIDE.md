# 🎯 Skills System Inspector Guide

## 📊 What You'll See in Inspector

When you select a player GameObject with **PlayerVeggaSkills** component, you'll now see ALL 23 skills organized by category!

### ⚔️ Combat Info (Read-Only)
```
┌─ Combat Info ─────────────────┐
│ Combat Level: 3 🔒            │ ← Auto-calculated
│ Combat Type: Warrior 🔒       │ ← Warrior/Ranger/Mage/Hybrid
└───────────────────────────────┘
```

### ⚙️ Config (Editable)
```
┌─ Config ──────────────────────┐
│ Stats: PlayerVeggaStats       │ ← Link to stats component
└───────────────────────────────┘
```

### ⚔️ Combat Skills (Read-Only)
```
┌─ Combat Skills ───────────────┐
│ Attack Level: 1 🔒            │
│ Attack XP: 0 🔒               │
│ Strength Level: 1 🔒          │
│ Strength XP: 0 🔒             │
│ Defence Level: 1 🔒           │
│ Defence XP: 0 🔒              │
│ Ranged Level: 1 🔒            │
│ Ranged XP: 0 🔒               │
│ Prayer Level: 1 🔒            │
│ Prayer XP: 0 🔒               │
│ Magic Level: 1 🔒             │
│ Magic XP: 0 🔒                │
│ Hitpoints Level: 10 🔒        │ ← Starts at 10 (OSRS-style)
│ Hitpoints XP: 1154 🔒         │
└───────────────────────────────┘
```

### 🌾 Gathering Skills (Read-Only)
```
┌─ Gathering Skills ────────────┐
│ Farming Level: 1 🔒           │
│ Farming XP: 0 🔒              │
│ Fishing Level: 1 🔒           │
│ Fishing XP: 0 🔒              │
│ Hunter Level: 1 🔒            │
│ Hunter XP: 0 🔒               │
│ Mining Level: 1 🔒            │
│ Mining XP: 0 🔒               │
│ Woodcutting Level: 1 🔒       │
│ Woodcutting XP: 0 🔒          │
└───────────────────────────────┘
```

### 🔨 Production Skills (Read-Only)
```
┌─ Production Skills ───────────┐
│ Cooking Level: 1 🔒           │
│ Cooking XP: 0 🔒              │
│ Crafting Level: 1 🔒          │
│ Crafting XP: 0 🔒             │
│ Fletching Level: 1 🔒         │
│ Fletching XP: 0 🔒            │
│ Herblore Level: 1 🔒          │
│ Herblore XP: 0 🔒             │
│ Runecraft Level: 1 🔒         │
│ Runecraft XP: 0 🔒            │
│ Smithing Level: 1 🔒          │
│ Smithing XP: 0 🔒             │
│ Construction Level: 1 🔒      │
│ Construction XP: 0 🔒         │
│ Firemaking Level: 1 🔒        │
│ Firemaking XP: 0 🔒           │
└───────────────────────────────┘
```

### 🏃 Utility Skills (Read-Only)
```
┌─ Utility Skills ──────────────┐
│ Agility Level: 1 🔒           │
│ Agility XP: 0 🔒              │
│ Slayer Level: 1 🔒            │
│ Slayer XP: 0 🔒               │
│ Thieving Level: 1 🔒          │
│ Thieving XP: 0 🔒             │
└───────────────────────────────┘
```

### 🧪 Testing (Buttons)
```
┌─ Testing ─────────────────────┐
│ [Add 100 Attack XP]           │
│ [Add 100 Strength XP]         │
│ [Add 100 Defence XP]          │
│ [Add 100 Hitpoints XP]        │
│ [Add 1000 Mining XP]          │
│ [Add 1000 Woodcutting XP]     │
│ [Set Attack to 99]            │
│ [Set All Combat to 99]        │
│ [Reset All Skills to 1]       │
└───────────────────────────────┘
```

---

## 🎮 How to Use

### During Gameplay:

1. **Press F5** to start the game
2. **Press F1** to open console
3. Type: `find PlayerVeggaSkills`
4. **Click on the component**
5. **Watch all skills update in real-time!**

### Example Testing:

```
1. Start game (F5)
2. Find PlayerVeggaSkills
3. Click [Add 100 Attack XP]
   - Attack XP: 100
   - Still level 1 (need 83 XP for level 2)

4. Click [Add 100 Attack XP] again
   - Attack XP: 200
   - Attack Level: 3! (leveled up!)
   - Combat Level: 3 (updated!)

5. Click [Set All Combat to 99]
   - All combat skills: 99
   - Combat Level: 126 (max!)
   - Hitpoints Level: 99
   - MaxHealth: 99 (auto-updated!)

6. Click [Reset All Skills to 1]
   - Everything back to 1
   - Hitpoints: 10 (OSRS starting)
```

---

## 📈 XP System (OSRS-Style)

Your game uses the **Old School RuneScape** XP formula!

### Level Requirements:
```
Level 1:  0 XP
Level 2:  83 XP
Level 3:  174 XP
Level 10: 1,154 XP
Level 50: 101,333 XP
Level 99: 13,034,431 XP (max!)
```

### How It Works:
- Add XP with `AddXp(skill, amount)`
- Level automatically increases when XP threshold reached
- Max level: **99**
- Hitpoints starts at **level 10** (like OSRS)

---

## 🎯 Special Features

### Hitpoints → Health Link:
- Hitpoints level = MaxHealth
- Level 10 Hitpoints = 10 HP
- Level 99 Hitpoints = 99 HP
- **Auto-updates** when Hitpoints levels up!

### Combat Level Calculation:
Uses OSRS formula:
```
Base = 0.25 × (Defence + Hitpoints + Prayer/2)
Melee = 0.325 × (Attack + Strength)
Range = 0.325 × (Ranged × 1.5)
Mage = 0.325 × (Magic × 1.5)

Combat Level = Base + Max(Melee, Range, Mage)
```

### Combat Type:
- **Warrior**: Melee is highest
- **Ranger**: Ranged is highest
- **Mage**: Magic is highest
- **Hybrid**: All equal

---

## 💡 Pro Tips

1. **Watch XP in real-time** - Updates every frame!
2. **Test leveling** - Click buttons to add XP
3. **Max out skills** - Use "Set to 99" buttons
4. **Reset for testing** - Use "Reset All Skills" button
5. **Check combat level** - Auto-calculates from combat skills

---

## 🔧 Code Usage

### Add XP from your code:
```csharp
var skills = player.Components.Get<PlayerVeggaSkills>();
skills.AddXp( SkillId.Attack, 100 );
skills.AddXp( SkillId.Mining, 500 );
```

### Set level directly:
```csharp
skills.SetSkillLevel( SkillId.Attack, 50 );
```

### Get current level/XP:
```csharp
int attackLevel = skills.GetLevel( SkillId.Attack );
int attackXP = skills.GetXp( SkillId.Attack );
```

### Listen for level ups:
```csharp
skills.OnSkillLevelUp += ( skill, newLevel ) =>
{
    Log.Info( $"Leveled {skill} to {newLevel}!" );
};
```

---

Enjoy your OSRS-style skill system! 🎮

