# 🔷 HEX ADMIN SYSTEM v1.0

**VEGGA ROLEPLAY's Next-Generation Admin, Anti-Cheat & Anti-Exploit Suite**

---

## 🎯 WHAT IS HEX?

HEX is the definitive all-in-one administration, anti-cheat, and security system for VEGGA ROLEPLAY.

**HEX** = **H**exagonal **E**xecution **X**-System

Built to last 10 years. Built to be remembered.

---

## 🔥 CORE PILLARS:

### 1. **Admin Commands**
- 100% of ULX command list + custom VEGGA commands
- Prefix: `!hex` (e.g., `!hex kick`, `!hex ban`, `!hex god`)
- Shortcuts: `!kick`, `!ban`, etc. still work
- Full command: `!hex help` or `!menu`

### 2. **Anti-Cheat & Anti-Exploit**
- Speedhack / flyhack / noclip detection
- Teleport distance validation
- Invalid packet / movement checks
- Duplicate entity / prop spam protection
- Lua execution sandboxing
- Auto-spectate + log + kick/ban on detection

### 3. **HEX UI**
- Gorgeous dark hexagonal theme
- Opens with `!hex` or `!menu`
- Tabs: Players | Commands | Bans | Anti-Cheat | Settings | Talent Tree
- Live player list with stats
- One-click punish buttons
- Real-time anti-cheat alert feed

### 4. **Context Menu**
- Press C → Right-click player → HEX Context Menu
- Quick actions: Kick, Ban, Freeze, Slay, Goto, Bring, etc.

### 5. **Logging & Watchlist**
- Every action logged to JSON
- Discord webhook support
- Anti-cheat trigger history
- Player watchlist system

### 6. **Permissions**
- 6 Ranks: Guest, VIP, Moderator, Admin, Superadmin, Owner
- Stored in PlayerProfile
- Loaded from `data/hex/ranks.json`

---

## 📁 FOLDER STRUCTURE:

```
Assets/HEX/
├── Core/
│   ├── HEXCore.cs              - Main system controller
│   ├── HEXPermissions.cs       - Rank & permission system
│   └── HEXLogger.cs            - Logging & activity tracking
├── Commands/
│   ├── HEXCommandRegistry.cs   - Command registration
│   ├── PlayerCommands.cs       - Player management commands
│   ├── AdminCommands.cs        - Admin utility commands
│   └── FunCommands.cs          - Fun/misc commands
├── AntiCheat/
│   ├── HEXAntiCheat.cs         - Main anti-cheat controller
│   ├── SpeedHackDetector.cs    - Speed/fly/noclip detection
│   ├── TeleportValidator.cs    - Teleport distance checks
│   └── PropSpamDetector.cs     - Entity spam protection
├── UI/
│   ├── HEXPanel.razor          - Main admin panel
│   ├── HEXPanel.razor.scss     - Hexagonal theme
│   ├── HEXContextMenu.razor    - Right-click context menu
│   └── HEXAlertFeed.razor      - Real-time anti-cheat alerts
├── Data/
│   ├── ranks.json              - Rank definitions
│   ├── bans.json               - Ban database
│   └── anticheat_logs.json     - Anti-cheat history
└── Editor/
    └── HEXEditorTools.cs       - In-editor admin tools
```

---

## 🎮 COMMANDS:

### **Main Command:**
- `!hex` or `!menu` - Opens HEX Admin Panel

### **Player Management:**
- `!hex kick <player> [reason]`
- `!hex ban <player> <duration> [reason]`
- `!hex unban <steamid>`
- `!hex freeze <player>`
- `!hex unfreeze <player>`
- `!hex slay <player>`
- `!hex respawn <player>`
- `!hex goto <player>`
- `!hex bring <player>`
- `!hex teleport <player> <target>`
- `!hex spectate <player>`

### **Admin Utilities:**
- `!hex god [player]` - Toggle god mode
- `!hex noclip [player]` - Toggle noclip
- `!hex cloak [player]` - Toggle invisibility
- `!hex ragdoll <player>` - Ragdoll player
- `!hex ignite <player>` - Set on fire
- `!hex heal <player>` - Heal to full
- `!hex armor <player> <amount>` - Set armor

### **Server Management:**
- `!hex map <mapname>` - Change map
- `!hex restart` - Restart server
- `!hex cleanup` - Remove all props
- `!hex announce <message>` - Server announcement

### **Anti-Cheat:**
- `!hex ac status` - Anti-cheat status
- `!hex ac logs` - View recent detections
- `!hex ac whitelist <player>` - Whitelist from AC
- `!hex watchlist <player>` - Add to watchlist

---

## 🎨 UI THEME:

**Hexagonal Dark Theme:**
- Primary: `#6496FF` (Blue)
- Secondary: `#4ADE80` (Green)
- Background: `rgba(10, 10, 20, 0.98)`
- Accent: `#FBBF24` (Gold)
- Danger: `#EF4444` (Red)

**Hexagon Pattern:**
- Border radius: 4px with hexagonal clip-path
- Glow effects on hover
- Animated transitions

---

## 🔒 PERMISSIONS:

| Rank | Level | Can Use |
|------|-------|---------|
| Guest | 0 | None |
| VIP | 1 | Fun commands |
| Moderator | 2 | Kick, freeze, slay |
| Admin | 3 | Ban, teleport, god |
| Superadmin | 4 | All commands, AC access |
| Owner | 5 | Everything + system config |

---

## 📊 ANTI-CHEAT DETECTIONS:

1. **Speed Hack** - Movement speed > 1.5x normal
2. **Fly Hack** - Vertical movement without ground contact
3. **Noclip** - Collision bypass detection
4. **Teleport Hack** - Distance > 1000 units in 1 frame
5. **Prop Spam** - > 10 props spawned in 1 second
6. **Invalid Packets** - Malformed network data

**Actions:**
- Warning (1st offense)
- Kick (2nd offense)
- Ban 1 day (3rd offense)
- Ban permanent (4th offense)

---

## 🚀 INSTALLATION:

1. Add `HEXCore` component to scene
2. Configure ranks in `data/hex/ranks.json`
3. Set owner Steam ID in HEXCore inspector
4. Done!

---

**Built with ❤️ for VEGGA ROLEPLAY**
**HEX v1.0 - The Future of Admin Systems**

