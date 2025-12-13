# Project 7 (Vegga Roleplay) — Master Pack

Session baseline: 2025-12-09 docs + current repo state.

This is a **single copy‑paste master document** that combines:
- Quick pitch (what it is)
- How it works (core loop)
- Datasheet (systems list)
- Whitepaper (architecture + persistence + UI direction)
- Roadmap (phases + recommended order)
- “What we learned” (s&box UI + editor realities)

It is grounded in these repo docs:
- `CONVERSATION_SUMMARY.md`
- `IMPLEMENTATION_SUMMARY.txt`
- `ROADMAP.md`
- `ELVUI_VISION.md`
- `XP_SYSTEM_GUIDE.md`
- `HEX_QUICK_START.md`
- `Assets/HEX/README.md`

---

## 1) Quick Pitch (10–30 seconds)

**Vegga Roleplay** is an OSRS‑inspired RPG progression layer (23 skills + XP + levels + quest points) fused with GMod/s&box sandbox roleplay: you build, fight, gather, craft, and progress over time — with **real persistence** and an **ElvUI‑style UI customization vision**.

Players log in, set a username, and then everything that matters (money, skills, inventory, session stats) is saved to JSON with backups, so the server feels like a real long‑running world.

---

## 2) One‑Page “How It Works”

### Core player loop
1. **Spawn / join** → First‑time setup wizard prompts for username.
2. **Play** → Do combat, gather resources, craft, explore, roleplay.
3. **Progress** → Gain XP in one of 23 skills, level up to 99.
4. **Earn quest points** → “Talent points” you spend on perks/abilities/cosmetics.
5. **Persist** → Autosave + backups keep progression safe.

### What makes it “Project 7”
- **OSRS progression** (skills, XP, levels, combat level)
- **GMod sandbox freedom** (tools/building/content expansion path)
- **UI as a product feature** (ElvUI‑like customization; drag panels, grid mode, profiles)
- **Persistence done seriously** (save/load + backups + corruption recovery + activity log)

---

## 3) Datasheet (Systems & Features)

### Player systems (implemented)
- **Stats:** Health, Armor, Money, Job, Prayer, Stamina, Special Attack (`PlayerVeggaStats`)
- **Session stats:** Kills, Deaths, Arrests, Quests, Quest Points, Props Spawned, Playtime (`PlayerSessionStats`)
- **Skills:** 23 RuneScape‑style skills with XP tracking (`PlayerVeggaSkills`)
- **Inventory:** 96 slots (12×8), stacking, drag & drop (`VeggaInventory`)

### UI systems (implemented)
- **PlayerHud:** Core stat bars
- **XPBar:** Bottom‑center OSRS‑style progress bar
- **XPDrops:** Floating XP numbers
- **InventoryHud:** Drag/drop inventory UI
- **VeggaScoreboard:** Tab scoreboard with sortable columns
- **FirstTimeSetup:** 2‑step wizard (username + optional consent/preferences)
- **DeathScreen:** Death/respawn UI (exists per docs)

### Persistence & safety (implemented)
- **Save/load:** `PlayerDataManager` JSON
- **Autosave:** every 5 minutes (`AutoSaveSystem`)
- **Backups:**
  - Daily backups (24h retention)
  - Weekly backups (7 weeks retention, max 7 files)
- **Corruption recovery:** restore from backups when needed
- **Activity log:** `activity.log` audit trail

### Performance optimizations (implemented)
- **Event‑driven UI updates:** `PlayerDataCache`
- **Console spam reduction:** scoreboard updates throttled to 1/sec

### HEX admin/security foundation (implemented + expanding)
- **Owner protection + bypass window:** `Sandbox.Admin.HexPermissions` (data file: `data/hex/permissions.json`)
- **Admin commands wired to HEX owner tooling:** `!setowner <player>` and `!ownerbypass <hours>` in `VeggaAdminManager`
- **Long-term vision docs exist for full HEX suite:** anti‑cheat, admin UI panel, context menu, logging/watchlist (`Assets/HEX/README.md`, `HEX_QUICK_START.md`)

### Planned/vision systems (not implemented yet)
- Steam integration (real SteamID, Steam name, avatar)
- Pickable world items (spawned items, pickup with E)
- Quest system (NPCs, objectives, quest rewards)
- Talent tree UI (spend quest points)
- UI customization foundation (UIManager, drag/drop, grid mode, profiles)
- Combat expansion (melee/ranged/magic weapons, animations, hit detection)
- Resource nodes + skilling content (mining, woodcutting, fishing, etc.)
- Admin anti‑cheat/data viewer

---

## 4) Whitepaper (Architecture)

### 4.1 Guiding principles
- **Persistence first:** progression must survive restarts and crashes.
- **Event driven, not polling:** UI should update only when data changes.
- **Modular UI:** each panel can be moved, resized, toggled, and eventually composed.
- **Performance guardrails:** throttle expensive UI refresh and avoid per‑frame logs.

### 4.1.1 Security/admin philosophy (HEX)
- **Owner protection is a hard rule:** Owners are protected from targeting by default.
- **Bypass is explicit and time-limited:** a global bypass window can be enabled and auto-expires.
- **Permissions are persisted:** stored in `data/hex/permissions.json` via `FileSystem.Data`.

### 4.2 Data flow (current)
- Runtime components hold authoritative state (`PlayerVeggaStats`, `PlayerVeggaSkills`, etc.)
- A cache/event layer (`PlayerDataCache`) notifies UI when values change
- Persistence layer (`PlayerDataManager`) writes/reads JSON with backup strategy

### 4.3 Persistence model (current)
- **Primary save file:** `{SteamID}.json`
- **Backup strategy:**
  - daily backups with 24h retention
  - weekly backups with 7 weeks retention, capped count
- **Audit trail:** `activity.log`

Assumption (from docs): current SteamID may still be placeholder (`TODO_STEAM_ID`) until Steam integration is done.

### 4.4 Progression model (current)
- **23 skills**, each intended to level to 99.
- Total level target: **2,277** (99 × 23).
- Quest points are treated as **talent points**:
  - Earned from quests/achievements/milestones
  - Spent on perks/abilities/cosmetics
  - Vision target: ~600 points total → forces meaningful choices

### 4.5 UI customization direction (vision)
From `ELVUI_VISION.md`:
- A `UIManager` tracks positions/sizes/visibility and saves to JSON
- Draggable panels, lock/unlock mode, and grid overlay
- Action bars, bags, chat, cooldowns, minimap as modular panels
- Profiles: save/load/share UI layouts

This is framed as a “big project” and should be built incrementally.

---

## 5) Roadmap (Phased)

This is the merged version of `ROADMAP.md` + the ElvUI vision.

### Phase 1 — Core gameplay (highest priority)
- Death & respawn system (essential)
- Basic combat loop (melee first; expand later)
- Simple inventory workflow (pickup/drop/equip)

### Phase 2 — UI/UX improvements
- ElvUI‑style customization foundation:
  - UIManager
  - drag/drop panels
  - grid mode
  - save/load layouts
  - profiles
- Action bars
- Better HUD surface area (minimap, quest tracker, buffs, chat, cooldowns)

### Phase 3 — Content
- Skill training content (mining, woodcutting, fishing, cooking, crafting…)
- NPCs/enemies, dialogue, shops
- World building (dungeons, towns, zones)

### Phase 4 — GMod tools
- Physgun/toolgun
- building tools, duplication, undo/redo

### Phase 5 — Multiplayer polish
- Networking for items, NPCs, combat, inventory
- Social systems (chat, parties, trading)

### Recommended build order (from `ROADMAP.md`)
1) Death & respawn
2) Basic melee combat
3) Simple inventory
Then: weapons + enemies + resource nodes + skilling.

---

## 6) What We Learned (Practical Lessons)

### 6.1 Console spam is a product issue
- Polling UI at 60/sec + logging is not viable.
- Throttling scoreboard updates to 1/sec and switching to event‑driven updates is the right default.

### 6.2 Persistence needs “ops thinking”
- Backups + retention policies + corruption recovery are not optional if this is meant to feel like a real progression server.
- Activity logging is valuable for debugging and admin tools later.

### 6.3 UI customization is a *system*, not a component
- ElvUI‑style features require:
  - a layout/state manager
  - serialization
  - interaction rules (locking, snapping, anchors)
  - profile support

### 6.4 Known rough edges / technical debt (from docs)
- SteamID currently hardcoded as `TODO_STEAM_ID` → all players risk writing to the same save until fixed.
- Some APIs referenced are obsolete (mouse visibility, transform fields) → should be modernized when doing a cleanup pass.
- HEX currently exists in two forms in-repo (a working static permissions/owner-protection layer + a larger “HEX suite” vision under `Assets/HEX/`). Aligning these into one authoritative implementation is a future cleanup task.

---

## 7) “North Star” Vision Summary

If you had to describe the endgame in one sentence:

**A persistent OSRS‑style progression RPG inside a sandbox roleplay world, with MMO‑grade UI customization and systems that are stable enough to run for months without wiping.**

---

## 8) Next Decisions (Pick One)

To keep momentum, pick the next workstream:
1) **Gameplay first:** Death/respawn + basic melee combat.
2) **Persistence correctness:** Steam integration so saves are per‑player.
3) **UI platform:** Begin `UIManager` + drag/drop + grid mode foundation.
4) **Admin/security:** build out HEX from the current owner-protection base (commands, logging, UI panel, anti-cheat).

