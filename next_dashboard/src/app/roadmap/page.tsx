"use client";

import { useState } from "react";
import { Reveal } from "../components/motion/Reveal";
import { SplitText } from "../components/motion/SplitText";
import { SiteNavbar } from "../components/SiteNavbar";

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
    "completed": "bg-green-500/20 text-green-400 border-green-500/30",
    "in-progress": "bg-yellow-500/20 text-yellow-400 border-yellow-500/30",
    "planned": "bg-gray-500/20 text-gray-400 border-gray-500/30",
    "done": "bg-green-500",
    "wip": "bg-yellow-500",
    "todo": "bg-gray-600"
  };

  const labels = {
    "completed": "✅ Completed",
    "in-progress": "🔄 In Progress",
    "planned": "📋 Planned",
    "done": "✓",
    "wip": "◐",
    "todo": "○"
  };

  if (status === "done" || status === "wip" || status === "todo") {
    return (
      <span className={`w-5 h-5 rounded-full ${colors[status]} flex items-center justify-center text-xs text-white`}>
        {status === "done" ? "✓" : status === "wip" ? "◐" : ""}
      </span>
    );
  }

  return (
    <span className={`px-3 py-1 rounded-full text-sm border ${colors[status as keyof typeof colors]}`}>
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
        <div className="page-container max-w-6xl">
          {/* Header */}
          <div className="text-center mb-12">
            <Reveal>
              <SplitText
                as="h1"
                text="Development Roadmap"
                className="text-5xl font-bold mb-4 gradient-text"
                mode="words"
                stagger={0.08}
              />
            </Reveal>
            <Reveal delay={0.08}>
              <p className="text-gray-400 text-xl">Track the progress of Vegga Roleplay</p>
            </Reveal>
          </div>

          {/* Overall Progress */}
          <div className="card mb-12">
            <div className="flex items-center justify-between mb-4">
              <h3 className="text-xl font-semibold">Overall Progress</h3>
              <span className="text-2xl font-bold gradient-text">{progress}%</span>
            </div>
            <div className="h-4 bg-gray-800 rounded-full overflow-hidden">
              <div 
                className="h-full bg-linear-to-r from-purple-600 to-pink-600 rounded-full transition-all duration-500"
                style={{ width: `${progress}%` }}
              />
            </div>
            <div className="flex justify-between mt-2 text-sm text-gray-500">
              <span>{doneItems} completed</span>
              <span>{totalItems - doneItems} remaining</span>
            </div>
          </div>

          {/* Legend */}
          <div className="flex items-center justify-center gap-8 mb-8">
            <div className="flex items-center gap-2">
              <StatusBadge status="done" />
              <span className="text-gray-400">Done</span>
            </div>
            <div className="flex items-center gap-2">
              <StatusBadge status="wip" />
              <span className="text-gray-400">In Progress</span>
            </div>
            <div className="flex items-center gap-2">
              <StatusBadge status="todo" />
              <span className="text-gray-400">Planned</span>
            </div>
          </div>

          {/* Phases */}
          <div className="space-y-4">
            {phases.map((phase) => (
              <div key={phase.id} className="card">
                <button
                  onClick={() => setExpandedPhase(expandedPhase === phase.id ? null : phase.id)}
                  className="w-full flex items-center justify-between"
                >
                  <div className="flex items-center gap-4">
                    <h3 className="text-xl font-semibold">{phase.title}</h3>
                    <StatusBadge status={phase.status} />
                  </div>
                  <div className="flex items-center gap-4">
                    <span className="text-gray-500 text-sm">
                      {phase.items.filter(i => i.status === "done").length}/{phase.items.length}
                    </span>
                    <span className={`transition-transform ${expandedPhase === phase.id ? "rotate-180" : ""}`}>
                      ▼
                    </span>
                  </div>
                </button>

                {expandedPhase === phase.id && (
                  <div className="mt-6 space-y-3">
                    {phase.items.map((item, i) => (
                      <div
                        key={i}
                        className={`flex items-center gap-4 p-3 rounded-lg transition-colors ${
                          item.status === "done" ? "bg-green-500/5" :
                          item.status === "wip" ? "bg-yellow-500/5" :
                          "bg-gray-500/5"
                        }`}
                      >
                        <StatusBadge status={item.status} />
                        <div className="flex-1">
                          <div className="font-medium">{item.name}</div>
                          <div className="text-sm text-gray-500">{item.description}</div>
                        </div>
                      </div>
                    ))}
                  </div>
                )}
              </div>
            ))}
          </div>

          {/* Timeline Visual */}
          <div className="mt-16">
            <h3 className="text-2xl font-bold text-center mb-8">Timeline</h3>
            <div className="relative">
              <div className="absolute left-1/2 top-0 bottom-0 w-0.5 bg-purple-500/30" />
              {phases.map((phase, i) => (
                <div key={phase.id} className={`relative flex items-center gap-8 mb-8 ${i % 2 === 0 ? "flex-row" : "flex-row-reverse"}`}>
                  <div className={`flex-1 ${i % 2 === 0 ? "text-right" : "text-left"}`}>
                    <div className="card inline-block">
                      <h4 className="font-semibold">{phase.title}</h4>
                      <StatusBadge status={phase.status} />
                    </div>
                  </div>
                  <div className={`w-4 h-4 rounded-full ${
                    phase.status === "completed" ? "bg-green-500" :
                    phase.status === "in-progress" ? "bg-yellow-500" :
                    "bg-gray-600"
                  } ring-4 ring-[#0a0a0f] z-10`} />
                  <div className="flex-1" />
                </div>
              ))}
            </div>
          </div>
        </div>
      </div>
    </div>
  );
}

