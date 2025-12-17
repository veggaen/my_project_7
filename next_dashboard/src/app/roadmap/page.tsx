"use client";

import { useState } from "react";
import { motion, AnimatePresence } from "framer-motion";
import { Reveal } from "@/app/components/motion/Reveal";
import { SplitText } from "@/app/components/motion/SplitText";
import { SiteNavbar } from "@/app/components/SiteNavbar";

type Phase = {
  id: string;
  title: string;
  status: "completed" | "in-progress" | "planned";
  items: {
    name: string;
    status: "done" | "wip" | "todo";
    description: string;
  }[];
};

const phases: Phase[] = [
  {
    id: "foundation",
    title: "Phase 1: Foundation",
    status: "completed",
    items: [
      { name: "Player Movement", status: "done", description: "WASD, crouch, sprint, jump with networked animations" },
      { name: "Stats System", status: "done", description: "HP, Armor, Money, Job with [Sync] networking" },
      { name: "Skills System", status: "done", description: "23-skill progression system with XP tracking" },
      { name: "Combat Level", status: "done", description: "Auto-calculated using a consistent leveling formula" },
      { name: "Basic HUD", status: "done", description: "Health, armor, prayer, stamina bars" },
      { name: "XP Bar & Drops", status: "done", description: "Visual XP feedback system" },
      { name: "Data Persistence", status: "done", description: "JSON save/load with backups" },
    ]
  },
  {
    id: "core",
    title: "Phase 2: Core Gameplay",
    status: "in-progress",
    items: [
      { name: "Death & Respawn", status: "done", description: "Ragdoll physics, death screen, spawn points" },
      { name: "Multiplayer Sync", status: "done", description: "Full networking for all player actions" },
      { name: "Crosshair System", status: "done", description: "Customizable crosshair with menu" },
      { name: "First-Time Setup", status: "done", description: "Username, avatar, rules wizard" },
      { name: "Inventory System", status: "wip", description: "96-slot inventory with drag & drop" },
      { name: "Basic Combat", status: "todo", description: "Melee weapons, hit detection" },
      { name: "Item Pickups", status: "todo", description: "World items with E to pick up" },
    ]
  },
  {
    id: "content",
    title: "Phase 3: Content",
    status: "planned",
    items: [
      { name: "Mining", status: "todo", description: "Rocks, ores, pickaxes" },
      { name: "Woodcutting", status: "todo", description: "Trees, logs, axes" },
      { name: "Fishing", status: "todo", description: "Fish spots, fishing rods" },
      { name: "Cooking", status: "todo", description: "Campfires, food items" },
      { name: "Crafting", status: "todo", description: "Workbenches, recipes" },
      { name: "NPCs", status: "todo", description: "Basic AI, dialogue, shops" },
      { name: "Enemies", status: "todo", description: "Combat AI, loot drops" },
    ]
  },
  {
    id: "ui",
    title: "Phase 4: UI/UX",
    status: "planned",
    items: [
      { name: "HUD Customization", status: "todo", description: "Drag & drop panels, grid mode" },
      { name: "Action Bars", status: "todo", description: "6 bars, 72 slots, keybinds" },
      { name: "Minimap", status: "todo", description: "Player position, markers" },
      { name: "Quest Tracker", status: "todo", description: "Active quests, objectives" },
      { name: "Chat System", status: "todo", description: "Global, local, party chat" },
      { name: "Profile System", status: "todo", description: "Save/load UI layouts" },
    ]
  },
  {
    id: "sandbox",
    title: "Phase 5: Sandbox Tools",
    status: "planned",
    items: [
      { name: "Physgun", status: "todo", description: "Grab and move objects" },
      { name: "Tool Gun", status: "todo", description: "Weld, rope, thruster" },
      { name: "Spawn Menu", status: "todo", description: "Props, NPCs, weapons" },
      { name: "Building System", status: "todo", description: "Grid snap, save builds" },
      { name: "Undo/Redo", status: "todo", description: "Action history" },
    ]
  },
  {
    id: "social",
    title: "Phase 6: Social",
    status: "planned",
    items: [
      { name: "Friends List", status: "todo", description: "Add friends, online status" },
      { name: "Party System", status: "todo", description: "Invite, shared XP" },
      { name: "Trading", status: "todo", description: "Player to player trades" },
      { name: "Scoreboard", status: "done", description: "Player list, stats display" },
      { name: "Admin Tools", status: "todo", description: "Ban, kick, moderation" },
    ]
  }
];

const StatusBadge = ({ status }: { status: string }) => {
  const colors = {
    "completed": "bg-green-500/10 text-green-400 border-green-500/20",
    "in-progress": "bg-yellow-500/10 text-yellow-400 border-yellow-500/20",
    "planned": "bg-gray-500/10 text-gray-400 border-gray-500/20",
    "done": "bg-green-500",
    "wip": "bg-yellow-500",
    "todo": "bg-gray-700"
  };

  const labels = {
    "completed": "Completed",
    "in-progress": "In Progress",
    "planned": "Planned",
    "done": "✓",
    "wip": "◐",
    "todo": "○"
  };

  if (status === "done" || status === "wip" || status === "todo") {
    return (
      <span className={`w-6 h-6 rounded-full ${colors[status]} flex items-center justify-center text-xs text-white shadow-sm`}>
        {status === "done" ? "✓" : status === "wip" ? "◐" : ""}
      </span>
    );
  }

  return (
    <span className={`px-3 py-1 rounded-full text-xs font-medium border ${colors[status as keyof typeof colors]} uppercase tracking-wider`}>
      {labels[status as keyof typeof labels]}
    </span>
  );
};

export default function RoadmapPage() {
  const [expandedPhase, setExpandedPhase] = useState<string | null>("core");

  const totalItems = phases.reduce((acc, p) => acc + p.items.length, 0);
  const doneItems = phases.reduce((acc, p) => acc + p.items.filter(i => i.status === "done").length, 0);
  const progress = Math.round((doneItems / totalItems) * 100);

  return (
    <div className="min-h-screen bg-[#0a0a0f]">
      <SiteNavbar />

      <div className="nav-spacer page-section">
        <div className="page-container">
          
          <div className="grid lg:grid-cols-12 gap-16 xl:gap-24 items-start">
            {/* Left Column - Sticky Info */}
            <div className="lg:col-span-5 xl:col-span-4 lg:sticky lg:top-32 space-y-10">
              <Reveal>
                <SplitText
                  as="h1"
                  text="Development Roadmap"
                  className="text-5xl md:text-6xl font-bold mb-6 gradient-text leading-tight"
                  mode="words"
                  stagger={0.08}
                />
              </Reveal>
              
              <Reveal delay={0.1}>
                <p className="text-gray-400 text-lg leading-relaxed">
                  Track the progress of Vegga Roleplay as we build the ultimate s&box MMORPG experience.
                </p>
              </Reveal>

              <Reveal delay={0.2}>
                <div className="card p-8 bg-linear-to-br from-[#111118] to-[#16161f] shadow-xl shadow-purple-900/5 border-purple-500/20">
                  <div className="flex items-center justify-between mb-6">
                    <h3 className="text-xl font-bold text-gray-200">Overall Progress</h3>
                    <span className="text-4xl font-bold gradient-text">{progress}%</span>
                  </div>
                  <div className="h-4 bg-gray-800/50 rounded-full overflow-hidden mb-6 ring-1 ring-white/5">
                    <motion.div 
                      initial={{ width: 0 }}
                      animate={{ width: `${progress}%` }}
                      transition={{ duration: 1.5, ease: "easeOut" }}
                      className="h-full bg-linear-to-r from-purple-600 via-pink-600 to-purple-600 bg-size-[200%_100%] animate-shimmer rounded-full"
                    />
                  </div>
                  <div className="flex justify-between text-sm text-gray-400 font-medium">
                    <span className="flex items-center gap-2">
                      <span className="w-2 h-2 rounded-full bg-green-500"></span>
                      {doneItems} completed
                    </span>
                    <span className="flex items-center gap-2">
                      {totalItems - doneItems} remaining
                      <span className="w-2 h-2 rounded-full bg-gray-600"></span>
                    </span>
                  </div>
                </div>
              </Reveal>

              <Reveal delay={0.3}>
                <div className="flex flex-wrap gap-4">
                  <div className="flex items-center gap-2 bg-gray-800/30 px-3 py-1.5 rounded-lg border border-gray-700/30">
                    <StatusBadge status="done" />
                    <span className="text-sm text-gray-400">Done</span>
                  </div>
                  <div className="flex items-center gap-2 bg-gray-800/30 px-3 py-1.5 rounded-lg border border-gray-700/30">
                    <StatusBadge status="wip" />
                    <span className="text-sm text-gray-400">In Progress</span>
                  </div>
                  <div className="flex items-center gap-2 bg-gray-800/30 px-3 py-1.5 rounded-lg border border-gray-700/30">
                    <StatusBadge status="todo" />
                    <span className="text-sm text-gray-400">Planned</span>
                  </div>
                </div>
              </Reveal>
            </div>

            {/* Right Column - Phases List */}
            <div className="lg:col-span-7 xl:col-span-8 space-y-8">
              {phases.map((phase, index) => (
                <Reveal key={phase.id} delay={0.1 + index * 0.05} width="100%">
                  <motion.div 
                    className={`card overflow-hidden border transition-all duration-300 ${
                      expandedPhase === phase.id 
                        ? "border-purple-500/30 bg-[#13131a] shadow-lg shadow-purple-900/10" 
                        : "border-gray-800/50 hover:border-gray-700 bg-[#111118]"
                    }`}
                    layout
                  >
                    <motion.button
                      onClick={() => setExpandedPhase(expandedPhase === phase.id ? null : phase.id)}
                      className="w-full flex items-center justify-between p-6 text-left"
                    >
                      <div className="flex flex-col md:flex-row md:items-center gap-2 md:gap-4">
                        <h3 className={`text-xl font-bold transition-colors ${
                          expandedPhase === phase.id ? "text-white" : "text-gray-300"
                        }`}>
                          {phase.title}
                        </h3>
                        <div className="scale-90 origin-left">
                          <StatusBadge status={phase.status} />
                        </div>
                      </div>
                      <div className="flex items-center gap-4">
                        <span className="text-gray-500 text-sm font-mono hidden sm:block">
                          {phase.items.filter(i => i.status === "done").length}/{phase.items.length}
                        </span>
                        <motion.span 
                          animate={{ rotate: expandedPhase === phase.id ? 180 : 0 }}
                          className="text-gray-400"
                        >
                          ▼
                        </motion.span>
                      </div>
                    </motion.button>

                    <AnimatePresence>
                      {expandedPhase === phase.id && (
                        <motion.div
                          initial={{ height: 0, opacity: 0 }}
                          animate={{ height: "auto", opacity: 1 }}
                          exit={{ height: 0, opacity: 0 }}
                          transition={{ duration: 0.3, ease: "easeInOut" }}
                        >
                          <div className="px-6 pb-8 space-y-4 border-t border-gray-800/50 pt-6">
                            {phase.items.map((item, i) => (
                              <motion.div
                                key={i}
                                initial={{ x: -10, opacity: 0 }}
                                animate={{ x: 0, opacity: 1 }}
                                transition={{ delay: i * 0.05 }}
                                className={`flex items-start gap-4 p-4 rounded-xl transition-all duration-300 hover:translate-x-1 ${
                                  item.status === "done" ? "bg-green-500/5 border border-green-500/10 hover:bg-green-500/10" :
                                  item.status === "wip" ? "bg-yellow-500/5 border border-yellow-500/10 hover:bg-yellow-500/10" :
                                  "bg-gray-800/20 border border-transparent hover:bg-gray-800/30"
                                }`}
                              >
                                <div className="mt-0.5">
                                  <StatusBadge status={item.status} />
                                </div>
                                <div className="flex-1">
                                  <div className={`font-medium ${
                                    item.status === "done" ? "text-gray-200" : "text-gray-400"
                                  }`}>
                                    {item.name}
                                  </div>
                                  <div className="text-sm text-gray-500 mt-0.5 leading-relaxed">
                                    {item.description}
                                  </div>
                                </div>
                              </motion.div>
                            ))}
                          </div>
                        </motion.div>
                      )}
                    </AnimatePresence>
                  </motion.div>
                </Reveal>
              ))}
            </div>
          </div>
        </div>
      </div>
    </div>
  );
}

