"use client";

import Link from "next/link";
import { useState } from "react";

// Mock data - in real app, this would come from an API
const mockPlayerData = {
  steamId: "76561198012345678",
  steamName: "V3gga",
  preferredUsername: "V3gga",
  rank: "Owner",
  money: 15750,
  bankBalance: 50000,
  totalKills: 127,
  totalDeaths: 43,
  totalArrests: 12,
  totalQuestsCompleted: 8,
  totalQuestPoints: 24,
  totalPlaytime: 86400, // seconds
  totalConnects: 156,
  totalPropsSpawned: 892,
  firstSeen: "2025-12-01T12:00:00Z",
  lastSeen: "2025-12-10T18:30:00Z",
  skills: {
    attack: { level: 45, xp: 61512 },
    strength: { level: 42, xp: 48886 },
    defence: { level: 38, xp: 35228 },
    hitpoints: { level: 50, xp: 101333 },
    ranged: { level: 35, xp: 25461 },
    prayer: { level: 28, xp: 12031 },
    magic: { level: 52, xp: 125440 },
    mining: { level: 61, xp: 309146 },
    woodcutting: { level: 55, xp: 166636 },
    fishing: { level: 48, xp: 83014 },
    cooking: { level: 44, xp: 55649 },
    crafting: { level: 32, xp: 18247 },
    smithing: { level: 29, xp: 13363 },
    firemaking: { level: 67, xp: 547953 },
    herblore: { level: 25, xp: 8740 },
    agility: { level: 41, xp: 41171 },
    thieving: { level: 38, xp: 35228 },
    slayer: { level: 22, xp: 5902 },
    farming: { level: 18, xp: 3206 },
    runecraft: { level: 15, xp: 2411 },
    hunter: { level: 12, xp: 1154 },
    construction: { level: 20, xp: 4470 },
    fletching: { level: 35, xp: 25461 },
  }
};

const formatPlaytime = (seconds: number) => {
  const hours = Math.floor(seconds / 3600);
  const minutes = Math.floor((seconds % 3600) / 60);
  if (hours > 24) {
    const days = Math.floor(hours / 24);
    return `${days}d ${hours % 24}h`;
  }
  return `${hours}h ${minutes}m`;
};

const formatNumber = (n: number) => n.toLocaleString();

const formatDate = (date: string) => {
  return new Date(date).toLocaleDateString('en-US', {
    year: 'numeric',
    month: 'short',
    day: 'numeric'
  });
};

const skillIcons: Record<string, string> = {
  attack: "⚔️", strength: "💪", defence: "🛡️", hitpoints: "❤️",
  ranged: "🏹", prayer: "✨", magic: "🔮", mining: "⛏️",
  woodcutting: "🪓", fishing: "🎣", cooking: "🍳", crafting: "🔨",
  smithing: "🔥", firemaking: "🔥", herblore: "🌿", agility: "🏃",
  thieving: "🗝️", slayer: "💀", farming: "🌾", runecraft: "🔮",
  hunter: "🎯", construction: "🏠", fletching: "🏹"
};

const skillColors: Record<string, string> = {
  attack: "#ef4444", strength: "#22c55e", defence: "#3b82f6", hitpoints: "#dc2626",
  ranged: "#22c55e", prayer: "#06b6d4", magic: "#8b5cf6", mining: "#78716c",
  woodcutting: "#84cc16", fishing: "#0ea5e9", cooking: "#f59e0b", crafting: "#a855f7",
  smithing: "#f97316", firemaking: "#ef4444", herblore: "#22c55e", agility: "#3b82f6",
  thieving: "#a855f7", slayer: "#1f2937", farming: "#84cc16", runecraft: "#fbbf24",
  hunter: "#78716c", construction: "#78716c", fletching: "#84cc16"
};

export default function StatsPage() {
  const [searchId, setSearchId] = useState("");
  const [player] = useState(mockPlayerData);
  
  const totalLevel = Object.values(player.skills).reduce((acc, s) => acc + s.level, 0);
  const totalXp = Object.values(player.skills).reduce((acc, s) => acc + s.xp, 0);
  const kdRatio = player.totalDeaths > 0 ? (player.totalKills / player.totalDeaths).toFixed(2) : player.totalKills;

  // Calculate combat level
  const att = player.skills.attack.level;
  const str = player.skills.strength.level;
  const def = player.skills.defence.level;
  const hp = player.skills.hitpoints.level;
  const pray = player.skills.prayer.level;
  const rng = player.skills.ranged.level;
  const mag = player.skills.magic.level;
  
  const basePart = 0.25 * (def + hp + Math.floor(pray / 2));
  const melee = 0.325 * (att + str);
  const range = 0.325 * Math.floor(rng * 1.5);
  const mage = 0.325 * Math.floor(mag * 1.5);
  const combatLevel = Math.floor(basePart + Math.max(melee, range, mage));

  return (
    <div className="min-h-screen bg-[#0a0a0f]">
      {/* Navigation */}
      <nav className="fixed top-0 w-full z-50 bg-[#0a0a0f]/80 backdrop-blur-lg border-b border-purple-500/20">
        <div className="max-w-7xl mx-auto px-6 py-4 flex items-center justify-between">
          <Link href="/" className="text-xl font-bold gradient-text">
            ← Back to Home
          </Link>
          <div className="flex items-center gap-6">
            <Link href="/whitepaper" className="text-gray-400 hover:text-white transition-colors">
              Whitepaper
            </Link>
            <Link href="/roadmap" className="text-gray-400 hover:text-white transition-colors">
              Roadmap
            </Link>
          </div>
        </div>
      </nav>

      <div className="pt-24 pb-20 px-6">
        <div className="max-w-6xl mx-auto">
          {/* Header */}
          <div className="text-center mb-12">
            <h1 className="text-5xl font-bold mb-4">
              <span className="gradient-text">Player Stats</span>
            </h1>
            <p className="text-gray-400 text-xl">View detailed player statistics and progression</p>
          </div>

          {/* Search */}
          <div className="card mb-8">
            <div className="flex gap-4">
              <input
                type="text"
                placeholder="Enter Steam ID (e.g., 76561198012345678)"
                value={searchId}
                onChange={(e) => setSearchId(e.target.value)}
                className="flex-1 px-4 py-3 bg-gray-800 border border-gray-700 rounded-lg focus:border-purple-500 focus:outline-none transition-colors"
              />
              <button className="px-6 py-3 bg-purple-600 rounded-lg font-semibold hover:bg-purple-700 transition-colors">
                Search
              </button>
            </div>
            <p className="text-gray-500 text-sm mt-2">
              Currently showing demo data. Connect to s&box server for real stats.
            </p>
          </div>

          {/* Player Header */}
          <div className="card mb-8">
            <div className="flex items-center gap-6">
              <div className="w-24 h-24 rounded-full bg-gradient-to-r from-purple-600 to-pink-600 flex items-center justify-center text-4xl">
                👤
              </div>
              <div className="flex-1">
                <div className="flex items-center gap-3">
                  <h2 className="text-3xl font-bold">{player.preferredUsername}</h2>
                  <span className="px-3 py-1 bg-purple-500/20 text-purple-400 rounded-full text-sm border border-purple-500/30">
                    {player.rank}
                  </span>
                </div>
                <p className="text-gray-500">Steam: {player.steamName}</p>
                <p className="text-gray-600 text-sm">ID: {player.steamId}</p>
              </div>
              <div className="text-right">
                <div className="text-4xl font-bold gradient-text">{combatLevel}</div>
                <div className="text-gray-500">Combat Level</div>
              </div>
            </div>
          </div>

          {/* Quick Stats Grid */}
          <div className="grid grid-cols-2 md:grid-cols-4 gap-4 mb-8">
            <div className="card text-center">
              <div className="text-3xl font-bold text-green-400">${formatNumber(player.money)}</div>
              <div className="text-gray-500">Cash</div>
            </div>
            <div className="card text-center">
              <div className="text-3xl font-bold text-blue-400">${formatNumber(player.bankBalance)}</div>
              <div className="text-gray-500">Bank</div>
            </div>
            <div className="card text-center">
              <div className="text-3xl font-bold text-purple-400">{totalLevel}</div>
              <div className="text-gray-500">Total Level</div>
            </div>
            <div className="card text-center">
              <div className="text-3xl font-bold text-pink-400">{formatNumber(totalXp)}</div>
              <div className="text-gray-500">Total XP</div>
            </div>
          </div>

          {/* Combat Stats */}
          <div className="grid md:grid-cols-2 gap-8 mb-8">
            <div className="card">
              <h3 className="text-xl font-semibold mb-4">⚔️ Combat Stats</h3>
              <div className="grid grid-cols-2 gap-4">
                <div>
                  <div className="text-2xl font-bold text-red-400">{player.totalKills}</div>
                  <div className="text-gray-500">Kills</div>
                </div>
                <div>
                  <div className="text-2xl font-bold text-gray-400">{player.totalDeaths}</div>
                  <div className="text-gray-500">Deaths</div>
                </div>
                <div>
                  <div className="text-2xl font-bold text-yellow-400">{kdRatio}</div>
                  <div className="text-gray-500">K/D Ratio</div>
                </div>
                <div>
                  <div className="text-2xl font-bold text-blue-400">{player.totalArrests}</div>
                  <div className="text-gray-500">Arrests</div>
                </div>
              </div>
            </div>

            <div className="card">
              <h3 className="text-xl font-semibold mb-4">📊 Activity Stats</h3>
              <div className="grid grid-cols-2 gap-4">
                <div>
                  <div className="text-2xl font-bold text-green-400">{formatPlaytime(player.totalPlaytime)}</div>
                  <div className="text-gray-500">Playtime</div>
                </div>
                <div>
                  <div className="text-2xl font-bold text-purple-400">{player.totalConnects}</div>
                  <div className="text-gray-500">Sessions</div>
                </div>
                <div>
                  <div className="text-2xl font-bold text-amber-400">{player.totalQuestsCompleted}</div>
                  <div className="text-gray-500">Quests Done</div>
                </div>
                <div>
                  <div className="text-2xl font-bold text-cyan-400">{player.totalPropsSpawned}</div>
                  <div className="text-gray-500">Props Spawned</div>
                </div>
              </div>
            </div>
          </div>

          {/* Skills Grid */}
          <div className="card">
            <h3 className="text-xl font-semibold mb-6">🎯 Skills Overview</h3>
            <div className="grid grid-cols-3 md:grid-cols-4 lg:grid-cols-6 gap-4">
              {Object.entries(player.skills).map(([skill, data]) => (
                <div
                  key={skill}
                  className="p-4 rounded-lg bg-gray-800/50 border border-gray-700/50 hover:border-purple-500/50 transition-colors cursor-pointer"
                  style={{ borderLeftColor: skillColors[skill], borderLeftWidth: 3 }}
                >
                  <div className="flex items-center gap-2 mb-2">
                    <span className="text-xl">{skillIcons[skill]}</span>
                    <span className="text-xs text-gray-500 capitalize">{skill}</span>
                  </div>
                  <div className="text-2xl font-bold">{data.level}</div>
                  <div className="text-xs text-gray-600">{formatNumber(data.xp)} XP</div>
                  <div className="mt-2 h-1 bg-gray-700 rounded-full overflow-hidden">
                    <div
                      className="h-full rounded-full"
                      style={{ 
                        width: `${(data.level / 99) * 100}%`,
                        background: skillColors[skill]
                      }}
                    />
                  </div>
                </div>
              ))}
            </div>
          </div>

          {/* Timeline */}
          <div className="card mt-8">
            <h3 className="text-xl font-semibold mb-4">📅 Timeline</h3>
            <div className="flex items-center justify-between text-gray-500">
              <div>
                <div className="text-sm">First Seen</div>
                <div className="text-white">{formatDate(player.firstSeen)}</div>
              </div>
              <div className="flex-1 mx-8 h-0.5 bg-gradient-to-r from-purple-500 to-pink-500 rounded" />
              <div className="text-right">
                <div className="text-sm">Last Seen</div>
                <div className="text-white">{formatDate(player.lastSeen)}</div>
              </div>
            </div>
          </div>
        </div>
      </div>
    </div>
  );
}

