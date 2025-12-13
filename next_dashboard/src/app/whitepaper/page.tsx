"use client";

import Link from "next/link";
import { Reveal } from "../components/motion/Reveal";
import { SplitText } from "../components/motion/SplitText";
import { SiteNavbar } from "../components/SiteNavbar";

const sections = [
  {
    id: "overview",
    title: "1. Project Overview",
    content: `
Vegga Roleplay is an ambitious MMORPG-style game built on s&box, combining the best elements of:

• **Old School RuneScape (OSRS)** - 23 skills with authentic XP tables, combat level calculation
• **Garry's Mod Sandbox** - Freedom, props, tools, creativity
• **ElvUI (World of Warcraft)** - Highly customizable, modular UI system
• **DarkRP** - Job system, economy, roleplay elements

The game features full multiplayer support with networked stats, persistent data storage, 
and a modern UI system built with Razor components.
    `
  },
  {
    id: "architecture",
    title: "2. System Architecture",
    content: `
### Core Components

**PlayerVeggaStats**
- Health, Armor, Money, Job tracking
- All values are [Sync] networked
- Event-driven updates for UI performance

**PlayerVeggaSkills**
- 23 OSRS-style skills
- XP system with authentic level tables
- Combat level calculation (OSRS formula)
- Events: OnSkillLevelUp, OnXPGained

**PlayerVeggaMovement**
- First/third person camera
- Crouch, sprint, jump
- Fully networked animations

**VeggaInventory**
- 96-slot inventory system
- NetList synced items
- Drag & drop support
    `
  },
  {
    id: "skills",
    title: "3. Skills System",
    content: `
### 23 Skills (OSRS-Style)

**Combat Skills (7)**
- Attack, Strength, Defence, Hitpoints
- Ranged, Prayer, Magic

**Gathering Skills (5)**
- Mining, Woodcutting, Fishing
- Farming, Hunter

**Production Skills (8)**
- Smithing, Crafting, Cooking, Firemaking
- Herblore, Construction, Fletching, Runecraft

**Utility Skills (3)**
- Agility, Thieving, Slayer

### XP Table
Level 1: 0 XP
Level 2: 83 XP
Level 10: 1,154 XP
Level 50: 101,333 XP
Level 99: 13,034,431 XP (max)

### Combat Level Formula
Base = 0.25 × (Defence + Hitpoints + Prayer/2)
Melee = 0.325 × (Attack + Strength)
Range = 0.325 × (Ranged × 1.5)
Mage = 0.325 × (Magic × 1.5)
Combat Level = Base + Max(Melee, Range, Mage)
    `
  },
  {
    id: "networking",
    title: "4. Networking",
    content: `
### Synced Variables ([Sync])

**Always Synced:**
- Health, Armor, Money, Job
- IsCrouching, IsSprinting
- TargetBodyAngle, TargetHeadAngle
- Inventory items (NetList)

**Never Synced (Local Only):**
- Camera settings
- Input states
- UI preferences
- Internal calculations

### Broadcast Events
- Jump animation: [Broadcast]
- Death events: [Broadcast]
- Respawn events: [Broadcast]

### Network Rules
1. Only OWNER sets [Sync] variables
2. PROXY clients read and apply visuals
3. Use IsProxy checks to separate logic
    `
  },
  {
    id: "data",
    title: "5. Data Persistence",
    content: `
### Smart Save System

**Event-Based Saving:**
- Saves on disconnect (immediate)
- Saves on money change
- Safety auto-save every 5 minutes (if changed)

**Backup System (4 files per player):**
- {steamid}_last.json - Every save
- {steamid}_hourly.json - Once per hour
- {steamid}_daily.json - Once per day
- {steamid}_weekly.json - Once per week

**Automatic Rollback:**
If main file corrupted, loads from:
1. _last.json (most recent)
2. _hourly.json (1 hour ago)
3. _daily.json (1 day ago)
4. _weekly.json (1 week ago)

**Save Location:**
C:\\Program Files (x86)\\Steam\\steamapps\\common\\sbox\\data\\original\\my_project_7\\PlayerData\\
    `
  },
  {
    id: "ui",
    title: "6. UI System",
    content: `
### Components (Razor)

**PlayerHud**
- Player name, combat level
- Job badge, money display
- HP, Armor, Prayer, Stamina, Special bars
- Modern dark theme with gradients

**XPBar**
- Bottom-center skill XP progress
- Shows current skill name and progress

**XPDrops**
- Floating XP numbers on right side
- Skill icons, fade animation

**DeathScreen**
- "YOU DIED" overlay
- Respawn countdown
- Dark Souls inspired

**FirstTimeSetup**
- 3-step wizard
- Username, Avatar, Rules consent
- Purple → Pink → Gold theme

### Performance Optimizations
- Event-driven updates (PlayerDataCache)
- Throttled scoreboard updates (1/sec)
- Minimal re-renders
    `
  },
  {
    id: "death",
    title: "7. Death & Respawn",
    content: `
### Death System

**Trigger:** Health reaches 0
**Flow:**
1. OnHealthChanged event fires
2. Die() broadcasts to all clients
3. Movement disabled
4. Ragdoll created with physics
5. Death screen shows
6. Camera orbits ragdoll
7. After delay, Respawn() called
8. Health restored
9. Teleport to spawn point

### Spawn Points
- SpawnPoint component
- Multiple per map
- Random selection on respawn
- Green sphere gizmos in editor

### Security
- Server authority (IsProxy checks)
- Can't bypass death
- Can't instant respawn
- Protected health setter
    `
  }
];

export default function WhitepaperPage() {
  return (
    <div className="min-h-screen bg-[#0a0a0f]">
      <SiteNavbar />

      <div className="nav-spacer page-section">
        <div className="page-container max-w-4xl">
          {/* Header */}
          <div className="text-center mb-16">
            <Reveal>
              <div className="inline-flex items-center gap-2 px-4 py-2 bg-purple-500/10 border border-purple-500/30 rounded-full mb-6">
                <span className="text-purple-300">📄 Technical Documentation</span>
              </div>
            </Reveal>
            <Reveal delay={0.05}>
              <SplitText
                as="h1"
                text="Vegga Roleplay"
                className="text-5xl font-bold mb-4 gradient-text"
                mode="chars"
                stagger={0.02}
              />
            </Reveal>
            <Reveal delay={0.12}>
              <h2 className="text-2xl text-gray-400">Whitepaper v1.0</h2>
              <p className="text-gray-500 mt-4">Last updated: December 2025</p>
            </Reveal>
          </div>

          {/* Table of Contents */}
          <div className="card mb-12">
            <h3 className="text-xl font-semibold mb-4">📑 Table of Contents</h3>
            <div className="grid md:grid-cols-2 gap-4">
              {sections.map((section, i) => (
                <a
                  key={i}
                  href={`#${section.id}`}
                  className="text-gray-300 hover:text-purple-400 transition-colors px-3 py-2 rounded-lg bg-white/0 hover:bg-white/5 border border-white/0 hover:border-white/10"
                >
                  {section.title}
                </a>
              ))}
            </div>
          </div>

          {/* Sections */}
          {sections.map((section, i) => (
            <section key={i} id={section.id} className="card mb-8">
              <h2 className="text-2xl font-bold mb-6 gradient-text">{section.title}</h2>
              <div className="prose prose-invert max-w-none prose-p:my-3 prose-li:my-2 prose-headings:mt-6 prose-headings:mb-3">
                {section.content.split('\n').map((line, j) => {
                  if (line.startsWith('###')) {
                    return <h4 key={j} className="text-lg font-semibold text-purple-400 mt-6 mb-3">{line.replace('### ', '')}</h4>;
                  }
                  if (line.startsWith('**') && line.endsWith('**')) {
                    return <h5 key={j} className="font-semibold text-pink-400 mt-4 mb-2">{line.replace(/\*\*/g, '')}</h5>;
                  }
                  if (line.startsWith('- ') || line.startsWith('• ')) {
                    return <li key={j} className="text-gray-300 ml-4 list-disc">{line.substring(2)}</li>;
                  }
                  if (line.trim() === '') {
                    return <br key={j} />;
                  }
                  return <p key={j} className="text-gray-300 leading-relaxed">{line}</p>;
                })}
              </div>
            </section>
          ))}

          {/* Download Section */}
          <div className="card bg-linear-to-r from-purple-900/30 to-pink-900/30 border-purple-500/30 text-center">
            <h3 className="text-2xl font-bold mb-4">Want the Full Source?</h3>
            <p className="text-gray-400 mb-6">
              The complete project is available in the my_project_7 folder
            </p>
            <div className="flex items-center justify-center gap-4">
              <Link 
                href="/roadmap"
                  className="btn btn-primary"
              >
                View Roadmap →
              </Link>
            </div>
          </div>
        </div>
      </div>
    </div>
  );
}

