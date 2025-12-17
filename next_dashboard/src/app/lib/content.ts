export type Skill = {
  slug: string;
  name: string;
  icon: string;
  color: string;
  tagline: string;
};

export type CoreFeature = {
  slug: string;
  title: string;
  icon: string;
  color: string;
  description: string;
};

export const SKILLS: Skill[] = [
  { slug: "attack", name: "Attack", icon: "⚔️", color: "#ef4444", tagline: "Accuracy and weapon mastery." },
  { slug: "strength", name: "Strength", icon: "💪", color: "#22c55e", tagline: "Raw damage and power." },
  { slug: "defence", name: "Defence", icon: "🛡️", color: "#3b82f6", tagline: "Mitigation and survivability." },
  { slug: "hitpoints", name: "Hitpoints", icon: "❤️", color: "#dc2626", tagline: "Maximum health and resilience." },
  { slug: "ranged", name: "Ranged", icon: "🏹", color: "#22c55e", tagline: "Distance combat and precision." },
  { slug: "prayer", name: "Prayer", icon: "✨", color: "#06b6d4", tagline: "Auras and protective effects." },
  { slug: "magic", name: "Magic", icon: "🔮", color: "#8b5cf6", tagline: "Spells and utility." },
  { slug: "mining", name: "Mining", icon: "⛏️", color: "#78716c", tagline: "Ores, stones, and resources." },
  { slug: "woodcutting", name: "Woodcutting", icon: "🪓", color: "#84cc16", tagline: "Logs, tools, and gathering." },
  { slug: "fishing", name: "Fishing", icon: "🎣", color: "#0ea5e9", tagline: "Food, trade goods, and profit." },
  { slug: "cooking", name: "Cooking", icon: "🍳", color: "#f59e0b", tagline: "Meals, buffs, and healing." },
  { slug: "crafting", name: "Crafting", icon: "🔨", color: "#a855f7", tagline: "Tools, gear, and components." },
  { slug: "smithing", name: "Smithing", icon: "🔥", color: "#f97316", tagline: "Metalwork and equipment." },
  { slug: "firemaking", name: "Firemaking", icon: "🔥", color: "#ef4444", tagline: "Campfires and utilities." },
  { slug: "herblore", name: "Herblore", icon: "🌿", color: "#22c55e", tagline: "Potions and remedies." },
  { slug: "agility", name: "Agility", icon: "🏃", color: "#3b82f6", tagline: "Movement tech and traversal." },
  { slug: "thieving", name: "Thieving", icon: "🗝️", color: "#a855f7", tagline: "Stealth, locks, and crime." },
  { slug: "slayer", name: "Slayer", icon: "💀", color: "#1f2937", tagline: "Contracts and monster hunting." },
  { slug: "farming", name: "Farming", icon: "🌾", color: "#84cc16", tagline: "Crops, yields, and growth." },
  { slug: "runecraft", name: "Runecraft", icon: "🔮", color: "#fbbf24", tagline: "Runes and magical fuel." },
  { slug: "hunter", name: "Hunter", icon: "🎯", color: "#78716c", tagline: "Traps, tracking, and loot." },
  { slug: "construction", name: "Construction", icon: "🏠", color: "#78716c", tagline: "Building, upgrades, and housing." },
  { slug: "fletching", name: "Fletching", icon: "🪶", color: "#84cc16", tagline: "Bows, arrows, and ranged gear." },
];

export const CORE_FEATURES: CoreFeature[] = [
  {
    slug: "skills",
    icon: "⚔️",
    title: "23-Skill Progression",
    description: "Complete skill system with XP tracking, level-ups, and a unified combat level.",
    color: "from-red-500 to-orange-500",
  },
  {
    slug: "multiplayer",
    icon: "🎮",
    title: "Multiplayer Ready",
    description: "Full networking with synced stats, animations, and player interactions.",
    color: "from-purple-500 to-pink-500",
  },
  {
    slug: "save-system",
    icon: "💾",
    title: "Smart Save System",
    description: "Event-based saving with automatic backups and rollback support.",
    color: "from-blue-500 to-cyan-500",
  },
  {
    slug: "hud",
    icon: "🎨",
    title: "Modular HUD System",
    description: "A clean, customizable UI manager with bars, XP drops, and modern layouts.",
    color: "from-green-500 to-emerald-500",
  },
  {
    slug: "death",
    icon: "💀",
    title: "Death & Respawn",
    description: "AAA-quality death system with ragdoll physics and spawn points.",
    color: "from-gray-500 to-slate-500",
  },
  {
    slug: "inventory",
    icon: "📦",
    title: "Inventory System",
    description: "96-slot inventory with drag & drop and item management.",
    color: "from-amber-500 to-yellow-500",
  },
];

export function getSkillBySlug(slug: string) {
  return SKILLS.find((s) => s.slug === slug);
}

export function getFeatureBySlug(slug: string) {
  return CORE_FEATURES.find((f) => f.slug === slug);
}
