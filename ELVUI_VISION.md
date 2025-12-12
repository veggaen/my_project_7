# 🎨 ElvUI-Style UI Customization System - Vision Document

## 🎯 The Big Idea:

Create a **fully customizable UI system** like ElvUI from WoW, where players can:
- **Drag and drop** UI elements anywhere
- **Resize** panels and bars
- **Toggle grid mode** to align elements perfectly
- **Save/load** UI layouts
- **Customize** action bars, bags, chat, cooldowns, etc.

---

## 📋 Phase 1: Foundation (Start Here)

### **1.1 Create Base UI Manager**
- Component that manages all UI elements
- Tracks positions, sizes, visibility
- Saves to JSON file

### **1.2 Make UI Elements Draggable**
- Add drag handles to each panel
- Lock/unlock mode (like ElvUI's `/elvui` command)
- Grid overlay for alignment

### **1.3 Basic Panels:**
- ✅ Player HUD (HP, Prayer, Stamina, Special, Job, Money pulse on change)
- ✅ XP Bar (OSRS combat level, precise value + next level hint)
- ✅ XP Drops
- 🔲 Action Bars (1-6 bars, 12 slots each = 72 slots)
- 🔲 Inventory/Bags (1024 max items)
- 🔲 Chat Window
- 🔲 Minimap/Compass
- 🔲 Cooldown Tracker
- 🔲 Buff/Debuff Display

---

## 📋 Phase 2: Action Bars

### **2.1 Action Bar System**
```
[1] [2] [3] [4] [5] [6] [7] [8] [9] [10] [11] [12]  ← Bar 1
[1] [2] [3] [4] [5] [6] [7] [8] [9] [10] [11] [12]  ← Bar 2
...
```

**Features:**
- Drag skills/items to slots
- Keybinds (1-9, 0, -, =)
- Cooldown overlays
- Hotkey text on buttons
- Resize slots (16x16 to 64x64)
- Change slots per row (6, 8, 10, 12)

### **2.2 Action Bar Customization**
- Number of bars (1-6)
- Slots per bar (6, 8, 10, 12)
- Horizontal or vertical layout
- Spacing between slots
- Show/hide keybind text
- Show/hide macro names

---

## 📋 Phase 3: Inventory System

### **3.1 Bag System**
```
┌─────────────────────────────────┐
│ Inventory (1024 slots)          │
├─────────────────────────────────┤
│ [🗡️] [🛡️] [🍖] [⚗️] [💎] [📜] ... │
│ [🗡️] [🛡️] [🍖] [⚗️] [💎] [📜] ... │
│ ...                             │
└─────────────────────────────────┘
```

**Features:**
- 1024 max slots (like you wanted!)
- Grid layout (8x128, 16x64, 32x32, etc.)
- Search/filter
- Sort by type, name, value
- Stack items
- Drag to action bars
- Tooltips on hover

### **3.2 Bag Customization**
- Slots per row (8, 16, 24, 32)
- Icon size (16x16 to 48x48)
- Show/hide item names
- Show/hide stack counts
- Color code by rarity

---

## 📋 Phase 4: Advanced Features

### **4.1 Grid Mode**
```
┌─┬─┬─┬─┬─┬─┬─┬─┬─┬─┐
├─┼─┼─┼─┼─┼─┼─┼─┼─┼─┤
├─┼─┼─┼─┼─┼─┼─┼─┼─┼─┤  ← Grid overlay
├─┼─┼─┼─┼─┼─┼─┼─┼─┼─┤
└─┴─┴─┴─┴─┴─┴─┴─┴─┴─┘
```

**Features:**
- Toggle with `/grid` command
- Snap to grid when dragging
- Adjustable grid size (8px, 16px, 32px)
- Show coordinates

### **4.2 Profile System**
- Save layouts to profiles
- Load profiles
- Share profiles (export/import JSON)
- Default profiles (DPS, Tank, Healer, OSRS-style, WoW-style)

### **4.3 Anchoring System**
- Anchor panels to screen edges
- Anchor panels to each other
- Relative positioning
- Auto-resize with screen

---

## 🎨 UI Customization Menu

### **Main Menu:**
```
┌─────────────────────────────────┐
│ UI Customization                │
├─────────────────────────────────┤
│ ☐ Lock UI                       │
│ ☐ Show Grid                     │
│                                 │
│ Panels:                         │
│   ▶ Player HUD                  │
│   ▶ Action Bars                 │
│   ▶ Inventory                   │
│   ▶ Chat                        │
│   ▶ Minimap                     │
│                                 │
│ [Reset to Default]              │
│ [Save Profile]                  │
│ [Load Profile]                  │
└─────────────────────────────────┘
```

### **Action Bar Settings:**
```
┌─────────────────────────────────┐
│ Action Bars                     │
├─────────────────────────────────┤
│ Number of Bars: [1-6] ▶ 3       │
│ Slots per Bar: [6-12] ▶ 12      │
│ Button Size: [16-64] ▶ 32       │
│ Spacing: [0-16] ▶ 4             │
│                                 │
│ ☑ Show Keybinds                 │
│ ☑ Show Cooldowns                │
│ ☐ Show Macro Names              │
│                                 │
│ Layout:                         │
│   ◉ Horizontal                  │
│   ○ Vertical                    │
└─────────────────────────────────┘
```

---

## 🛠️ Implementation Plan

### **Week 1: Foundation**
- [ ] Create UIManager component
- [ ] Add drag/drop system
- [ ] Add grid overlay
- [ ] Save/load positions to JSON

### **Week 2: Action Bars**
- [ ] Create ActionBar component
- [ ] Add slot system (12 slots per bar)
- [ ] Add keybind system
- [ ] Add cooldown overlays
- [ ] Make customizable (slots per row, size, etc.)

### **Week 3: Inventory**
- [ ] Create Inventory component
- [ ] Add item system (1024 slots)
- [ ] Add drag/drop items
- [ ] Add search/filter
- [ ] Add sorting

### **Week 4: Polish**
- [ ] Add profile system
- [ ] Add customization menu
- [ ] Add default layouts
- [ ] Add tooltips
- [ ] Add animations

---

## 🎯 Immediate Next Steps:

1. **Add XP Bar and XP Drops** (DONE! ✅)
2. **Test the new HUD** (PlayerHudNew)
3. **Create UIManager foundation**
4. **Make panels draggable**
5. **Add grid mode**

---

## 💡 Inspiration Sources:

### **ElvUI (WoW):**
- Drag/drop customization
- Grid mode
- Profile system
- Anchoring

### **OSRS:**
- XP drops
- XP bar
- Skill icons
- Clean, simple design

### **WoW Classic + Addons:**
- Action bars (Bartender)
- Bags (Bagnon, AdiBags)
- Cooldowns (OmniCC)
- Chat (Prat)

---

## 🚀 Let's Start!

**Right now, let's focus on:**
1. ✅ Get XP Bar working
2. ✅ Get XP Drops working
3. ✅ Get clean HUD working
4. 🔲 Create basic UIManager
5. 🔲 Make panels draggable

**This is a BIG project, but we'll build it step by step!** 🎮✨

