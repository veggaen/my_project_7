"use client";

import { useState } from "react";
import Link from "next/link";

// Icons as simple SVG components
const HexIcon = () => (
  <svg viewBox="0 0 100 100" className="w-16 h-16">
    <polygon 
      points="50,5 95,27.5 95,72.5 50,95 5,72.5 5,27.5" 
      fill="none" 
      stroke="url(#hexGradient)" 
      strokeWidth="3"
    />
    <defs>
      <linearGradient id="hexGradient" x1="0%" y1="0%" x2="100%" y2="100%">
        <stop offset="0%" stopColor="#8b5cf6" />
        <stop offset="50%" stopColor="#ec4899" />
        <stop offset="100%" stopColor="#fbbf24" />
      </linearGradient>
    </defs>
  </svg>
);

const features = [
  {
    icon: "⚔️",
    title: "23 OSRS-Style Skills",
    description: "Complete skill system with XP tracking, level-ups, and combat level calculation",
    color: "from-red-500 to-orange-500"
  },
  {
    icon: "🎮",
    title: "Multiplayer Ready",
    description: "Full networking with synced stats, animations, and player interactions",
    color: "from-purple-500 to-pink-500"
  },
  {
    icon: "💾",
    title: "Smart Save System",
    description: "Event-based saving with automatic backups and rollback support",
    color: "from-blue-500 to-cyan-500"
  },
  {
    icon: "🎨",
    title: "ElvUI-Style HUD",
    description: "Customizable UI with health bars, XP drops, and modern design",
    color: "from-green-500 to-emerald-500"
  },
  {
    icon: "💀",
    title: "Death & Respawn",
    description: "AAA-quality death system with ragdoll physics and spawn points",
    color: "from-gray-500 to-slate-500"
  },
  {
    icon: "📦",
    title: "Inventory System",
    description: "96-slot inventory with drag & drop and item management",
    color: "from-amber-500 to-yellow-500"
  }
];

const stats = [
  { label: "Skills", value: "23", suffix: "" },
  { label: "Max Level", value: "99", suffix: "" },
  { label: "Total Levels", value: "2,277", suffix: "" },
  { label: "Save Backups", value: "4", suffix: "/player" }
];

export default function Home() {
  const [hoveredFeature, setHoveredFeature] = useState<number | null>(null);

  return (
    <div className="min-h-screen bg-[#0a0a0f]">
      {/* Navigation */}
      <nav className="fixed top-0 w-full z-50 bg-[#0a0a0f]/80 backdrop-blur-lg border-b border-purple-500/20">
        <div className="max-w-7xl mx-auto px-6 py-4 flex items-center justify-between">
          <div className="flex items-center gap-3">
            <HexIcon />
            <span className="text-xl font-bold gradient-text">Vegga Roleplay</span>
          </div>
          <div className="flex items-center gap-6">
            <Link href="/whitepaper" className="text-gray-400 hover:text-white transition-colors">
              Whitepaper
            </Link>
            <Link href="/roadmap" className="text-gray-400 hover:text-white transition-colors">
              Roadmap
            </Link>
            <Link href="/stats" className="text-gray-400 hover:text-white transition-colors">
              Stats
            </Link>
            <Link href="/admin" className="text-gray-400 hover:text-white transition-colors">
              Admin
            </Link>
            <a 
              href="https://sbox.game" 
              target="_blank"
              className="px-4 py-2 bg-gradient-to-r from-purple-600 to-pink-600 rounded-lg font-medium hover:opacity-90 transition-opacity"
            >
              Play on s&box
            </a>
          </div>
        </div>
      </nav>

      {/* Hero Section */}
      <section className="pt-32 pb-20 px-6">
        <div className="max-w-7xl mx-auto text-center">
          <div className="inline-flex items-center gap-2 px-4 py-2 bg-purple-500/10 border border-purple-500/30 rounded-full mb-8">
            <span className="w-2 h-2 bg-green-500 rounded-full animate-pulse"></span>
            <span className="text-sm text-purple-300">Built on s&box</span>
          </div>
          
          <h1 className="text-6xl md:text-7xl font-bold mb-6">
            <span className="gradient-text">Vegga Roleplay</span>
          </h1>
          
          <p className="text-xl text-gray-400 max-w-2xl mx-auto mb-12">
            An OSRS-inspired MMORPG experience built on s&box. 
            Featuring 23 skills, custom UI, multiplayer networking, and endless possibilities.
          </p>

          <div className="flex items-center justify-center gap-4 mb-16">
            <Link 
              href="/whitepaper"
              className="px-8 py-4 bg-gradient-to-r from-purple-600 to-pink-600 rounded-xl font-semibold text-lg hover:scale-105 transition-transform glow-purple"
            >
              Read Whitepaper
            </Link>
            <Link 
              href="/roadmap"
              className="px-8 py-4 bg-white/5 border border-white/10 rounded-xl font-semibold text-lg hover:bg-white/10 transition-colors"
            >
              View Roadmap
            </Link>
          </div>

          {/* Stats */}
          <div className="grid grid-cols-2 md:grid-cols-4 gap-6 max-w-3xl mx-auto">
            {stats.map((stat, i) => (
              <div key={i} className="card text-center">
                <div className="text-3xl font-bold gradient-text">{stat.value}</div>
                <div className="text-sm text-gray-500">{stat.label}{stat.suffix}</div>
              </div>
            ))}
          </div>
        </div>
      </section>

      {/* Features Grid */}
      <section className="py-20 px-6 bg-gradient-to-b from-transparent to-purple-900/10">
        <div className="max-w-7xl mx-auto">
          <h2 className="text-4xl font-bold text-center mb-4">Core Features</h2>
          <p className="text-gray-400 text-center mb-12 max-w-2xl mx-auto">
            Everything you need for an immersive roleplay experience
          </p>

          <div className="grid md:grid-cols-2 lg:grid-cols-3 gap-6">
            {features.map((feature, i) => (
              <div
                key={i}
                className="card cursor-pointer group"
                onMouseEnter={() => setHoveredFeature(i)}
                onMouseLeave={() => setHoveredFeature(null)}
              >
                <div className={`w-12 h-12 rounded-xl bg-gradient-to-r ${feature.color} flex items-center justify-center text-2xl mb-4 group-hover:scale-110 transition-transform`}>
                  {feature.icon}
                </div>
                <h3 className="text-xl font-semibold mb-2">{feature.title}</h3>
                <p className="text-gray-400">{feature.description}</p>
              </div>
            ))}
          </div>
        </div>
      </section>

      {/* Skills Preview */}
      <section className="py-20 px-6">
        <div className="max-w-7xl mx-auto">
          <h2 className="text-4xl font-bold text-center mb-4">23 Skills to Master</h2>
          <p className="text-gray-400 text-center mb-12">OSRS-style progression with authentic XP tables</p>

          <div className="grid grid-cols-4 md:grid-cols-6 lg:grid-cols-8 gap-4">
            {[
              { name: "Attack", icon: "⚔️", color: "#ef4444" },
              { name: "Strength", icon: "💪", color: "#22c55e" },
              { name: "Defence", icon: "🛡️", color: "#3b82f6" },
              { name: "Hitpoints", icon: "❤️", color: "#dc2626" },
              { name: "Ranged", icon: "🏹", color: "#22c55e" },
              { name: "Prayer", icon: "✨", color: "#06b6d4" },
              { name: "Magic", icon: "🔮", color: "#8b5cf6" },
              { name: "Mining", icon: "⛏️", color: "#78716c" },
              { name: "Woodcutting", icon: "🪓", color: "#84cc16" },
              { name: "Fishing", icon: "🎣", color: "#0ea5e9" },
              { name: "Cooking", icon: "🍳", color: "#f59e0b" },
              { name: "Crafting", icon: "🔨", color: "#a855f7" },
              { name: "Smithing", icon: "🔥", color: "#f97316" },
              { name: "Firemaking", icon: "🔥", color: "#ef4444" },
              { name: "Herblore", icon: "🌿", color: "#22c55e" },
              { name: "Agility", icon: "🏃", color: "#3b82f6" },
              { name: "Thieving", icon: "🗝️", color: "#a855f7" },
              { name: "Slayer", icon: "💀", color: "#1f2937" },
              { name: "Farming", icon: "🌾", color: "#84cc16" },
              { name: "Runecraft", icon: "🔮", color: "#fbbf24" },
              { name: "Hunter", icon: "🎯", color: "#78716c" },
              { name: "Construction", icon: "🏠", color: "#78716c" },
              { name: "Fletching", icon: "🏹", color: "#84cc16" },
            ].map((skill, i) => (
              <div
                key={i}
                className="card text-center p-4 hover:scale-105 transition-transform cursor-pointer"
                style={{ borderColor: skill.color + "40" }}
              >
                <div className="text-2xl mb-2">{skill.icon}</div>
                <div className="text-xs text-gray-400 truncate">{skill.name}</div>
              </div>
            ))}
          </div>
        </div>
      </section>

      {/* CTA Section */}
      <section className="py-20 px-6">
        <div className="max-w-4xl mx-auto text-center">
          <div className="card p-12 bg-gradient-to-r from-purple-900/30 to-pink-900/30 border-purple-500/30">
            <h2 className="text-4xl font-bold mb-4">Ready to Begin?</h2>
            <p className="text-gray-400 mb-8 max-w-xl mx-auto">
              Dive into the whitepaper to learn about the complete system architecture, 
              or check the roadmap to see what&apos;s coming next.
            </p>
            <div className="flex items-center justify-center gap-4">
              <Link 
                href="/whitepaper"
                className="px-6 py-3 bg-white text-black rounded-lg font-semibold hover:bg-gray-200 transition-colors"
              >
                📄 Whitepaper
              </Link>
              <Link 
                href="/roadmap"
                className="px-6 py-3 bg-purple-600 rounded-lg font-semibold hover:bg-purple-700 transition-colors"
              >
                🗺️ Roadmap
              </Link>
              <Link 
                href="/stats"
                className="px-6 py-3 bg-pink-600 rounded-lg font-semibold hover:bg-pink-700 transition-colors"
              >
                📊 Stats
              </Link>
            </div>
          </div>
        </div>
      </section>

      {/* Footer */}
      <footer className="py-8 px-6 border-t border-white/10">
        <div className="max-w-7xl mx-auto flex items-center justify-between">
          <div className="flex items-center gap-2">
            <HexIcon />
            <span className="text-gray-500">Vegga Roleplay © 2025</span>
          </div>
          <div className="text-gray-500 text-sm">
            Built with ❤️ on s&box
          </div>
        </div>
      </footer>
    </div>
  );
}
