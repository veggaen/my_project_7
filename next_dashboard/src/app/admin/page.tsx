"use client";

import Link from "next/link";
import { useState, useEffect } from "react";

type Player = {
  steamId: string;
  steamName: string;
  preferredUsername: string;
  rank: string;
  money: number;
  lastSeen: string | null;
};

type ActivityEntry = {
  timestamp: string;
  type: string;
  details: string;
  raw: string;
};

export default function AdminPage() {
  const [players, setPlayers] = useState<Player[]>([]);
  const [activities, setActivities] = useState<ActivityEntry[]>([]);
  const [selectedPlayer, setSelectedPlayer] = useState<Player | null>(null);
  const [editMode, setEditMode] = useState(false);
  const [editData, setEditData] = useState<Record<string, unknown>>({});
  const [loading, setLoading] = useState(true);
  const [tab, setTab] = useState<"players" | "activity" | "stats">("players");

  // Fetch players
  const fetchPlayers = async () => {
    try {
      const res = await fetch('/api/players');
      const data = await res.json();
      setPlayers(data.players || []);
    } catch (error) {
      console.error('Failed to fetch players:', error);
    }
  };

  // Fetch activity log
  const fetchActivity = async () => {
    try {
      const res = await fetch('/api/activity?limit=50');
      const data = await res.json();
      setActivities(data.activities || []);
    } catch (error) {
      console.error('Failed to fetch activity:', error);
    }
  };

  // Fetch player details
  const fetchPlayerDetails = async (steamId: string) => {
    try {
      const res = await fetch(`/api/players/${steamId}`);
      const data = await res.json();
      setSelectedPlayer({ ...data, steamId });
      setEditData(data);
    } catch (error) {
      console.error('Failed to fetch player:', error);
    }
  };

  // Update player
  const updatePlayer = async () => {
    if (!selectedPlayer) return;
    try {
      const res = await fetch(`/api/players/${selectedPlayer.steamId}`, {
        method: 'PUT',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(editData),
      });
      if (res.ok) {
        alert('Player updated!');
        setEditMode(false);
        fetchPlayers();
      }
    } catch (error) {
      console.error('Failed to update player:', error);
    }
  };

  // Delete player
  const deletePlayer = async (steamId: string) => {
    if (!confirm(`Are you sure you want to delete player ${steamId}?`)) return;
    try {
      const res = await fetch(`/api/players/${steamId}`, { method: 'DELETE' });
      if (res.ok) {
        alert('Player deleted!');
        setSelectedPlayer(null);
        fetchPlayers();
      }
    } catch (error) {
      console.error('Failed to delete player:', error);
    }
  };

  useEffect(() => {
    Promise.all([fetchPlayers(), fetchActivity()]).finally(() => setLoading(false));
    
    // Auto-refresh every 30 seconds
    const interval = setInterval(() => {
      fetchPlayers();
      fetchActivity();
    }, 30000);
    
    return () => clearInterval(interval);
  }, []);

  if (loading) {
    return (
      <div className="min-h-screen bg-[#0a0a0f] flex items-center justify-center">
        <div className="text-2xl text-purple-400">Loading...</div>
      </div>
    );
  }

  return (
    <div className="min-h-screen bg-[#0a0a0f]">
      {/* Navigation */}
      <nav className="fixed top-0 w-full z-50 bg-[#0a0a0f]/80 backdrop-blur-lg border-b border-purple-500/20">
        <div className="max-w-7xl mx-auto px-6 py-4 flex items-center justify-between">
          <Link href="/" className="text-xl font-bold gradient-text">
            ← Back to Home
          </Link>
          <div className="flex items-center gap-2">
            <span className="w-2 h-2 bg-green-500 rounded-full animate-pulse" />
            <span className="text-green-400 text-sm">Admin Panel</span>
          </div>
        </div>
      </nav>

      <div className="pt-24 pb-20 px-6">
        <div className="max-w-7xl mx-auto">
          {/* Header */}
          <div className="mb-8">
            <h1 className="text-4xl font-bold gradient-text">Admin Dashboard</h1>
            <p className="text-gray-400">Manage players, view activity, and monitor server stats</p>
          </div>

          {/* Tabs */}
          <div className="flex gap-2 mb-6">
            {["players", "activity", "stats"].map((t) => (
              <button
                key={t}
                onClick={() => setTab(t as typeof tab)}
                className={`px-4 py-2 rounded-lg font-medium transition-colors ${
                  tab === t
                    ? "bg-purple-600 text-white"
                    : "bg-gray-800 text-gray-400 hover:bg-gray-700"
                }`}
              >
                {t === "players" && "👥 Players"}
                {t === "activity" && "📜 Activity"}
                {t === "stats" && "📊 Stats"}
              </button>
            ))}
          </div>

          {/* Players Tab */}
          {tab === "players" && (
            <div className="grid lg:grid-cols-3 gap-6">
              {/* Player List */}
              <div className="lg:col-span-1 card max-h-[600px] overflow-y-auto">
                <h3 className="text-xl font-semibold mb-4">
                  Players ({players.length})
                </h3>
                <div className="space-y-2">
                  {players.map((player) => (
                    <div
                      key={player.steamId}
                      onClick={() => fetchPlayerDetails(player.steamId)}
                      className={`p-3 rounded-lg cursor-pointer transition-colors ${
                        selectedPlayer?.steamId === player.steamId
                          ? "bg-purple-600/20 border border-purple-500"
                          : "bg-gray-800/50 hover:bg-gray-800"
                      }`}
                    >
                      <div className="font-medium">{player.preferredUsername}</div>
                      <div className="text-sm text-gray-500 flex justify-between">
                        <span>{player.rank}</span>
                        <span className="text-green-400">${player.money?.toLocaleString()}</span>
                      </div>
                    </div>
                  ))}
                  {players.length === 0 && (
                    <div className="text-gray-500 text-center py-8">
                      No players found
                    </div>
                  )}
                </div>
              </div>

              {/* Player Details */}
              <div className="lg:col-span-2 card">
                {selectedPlayer ? (
                  <>
                    <div className="flex items-center justify-between mb-6">
                      <h3 className="text-xl font-semibold">Player Details</h3>
                      <div className="flex gap-2">
                        <button
                          onClick={() => setEditMode(!editMode)}
                          className="px-4 py-2 bg-blue-600 rounded-lg hover:bg-blue-700 transition-colors"
                        >
                          {editMode ? "Cancel" : "✏️ Edit"}
                        </button>
                        <button
                          onClick={() => deletePlayer(selectedPlayer.steamId)}
                          className="px-4 py-2 bg-red-600 rounded-lg hover:bg-red-700 transition-colors"
                        >
                          🗑️ Delete
                        </button>
                      </div>
                    </div>

                    {editMode ? (
                      <div className="space-y-4">
                        <div className="grid grid-cols-2 gap-4">
                          <div>
                            <label className="block text-sm text-gray-400 mb-1">Username</label>
                            <input
                              type="text"
                              value={(editData.PreferredUsername as string) || ''}
                              onChange={(e) => setEditData({ ...editData, PreferredUsername: e.target.value })}
                              className="w-full px-3 py-2 bg-gray-800 border border-gray-700 rounded-lg"
                            />
                          </div>
                          <div>
                            <label className="block text-sm text-gray-400 mb-1">Rank</label>
                            <select
                              value={(editData.Rank as string) || 'Guest'}
                              onChange={(e) => setEditData({ ...editData, Rank: e.target.value })}
                              className="w-full px-3 py-2 bg-gray-800 border border-gray-700 rounded-lg"
                            >
                              <option value="Guest">Guest</option>
                              <option value="VIP">VIP</option>
                              <option value="Moderator">Moderator</option>
                              <option value="Admin">Admin</option>
                              <option value="Superadmin">Superadmin</option>
                              <option value="Owner">Owner</option>
                            </select>
                          </div>
                          <div>
                            <label className="block text-sm text-gray-400 mb-1">Money</label>
                            <input
                              type="number"
                              value={(editData.Money as number) || 0}
                              onChange={(e) => setEditData({ ...editData, Money: parseInt(e.target.value) })}
                              className="w-full px-3 py-2 bg-gray-800 border border-gray-700 rounded-lg"
                            />
                          </div>
                          <div>
                            <label className="block text-sm text-gray-400 mb-1">Bank</label>
                            <input
                              type="number"
                              value={(editData.BankBalance as number) || 0}
                              onChange={(e) => setEditData({ ...editData, BankBalance: parseInt(e.target.value) })}
                              className="w-full px-3 py-2 bg-gray-800 border border-gray-700 rounded-lg"
                            />
                          </div>
                        </div>
                        <button
                          onClick={updatePlayer}
                          className="px-6 py-2 bg-green-600 rounded-lg hover:bg-green-700 transition-colors"
                        >
                          💾 Save Changes
                        </button>
                      </div>
                    ) : (
                      <div className="grid grid-cols-2 gap-4">
                        <div><span className="text-gray-500">Steam ID:</span> <span className="font-mono text-sm">{selectedPlayer.steamId}</span></div>
                        <div><span className="text-gray-500">Steam Name:</span> {selectedPlayer.steamName}</div>
                        <div><span className="text-gray-500">Username:</span> {selectedPlayer.preferredUsername}</div>
                        <div><span className="text-gray-500">Rank:</span> <span className="text-purple-400">{selectedPlayer.rank}</span></div>
                        <div><span className="text-gray-500">Money:</span> <span className="text-green-400">${selectedPlayer.money?.toLocaleString()}</span></div>
                        <div><span className="text-gray-500">Last Seen:</span> {selectedPlayer.lastSeen ? new Date(selectedPlayer.lastSeen).toLocaleString() : 'Never'}</div>
                      </div>
                    )}
                  </>
                ) : (
                  <div className="text-gray-500 text-center py-12">
                    Select a player to view details
                  </div>
                )}
              </div>
            </div>
          )}

          {/* Activity Tab */}
          {tab === "activity" && (
            <div className="card">
              <h3 className="text-xl font-semibold mb-4">Activity Log</h3>
              <div className="space-y-2 max-h-[600px] overflow-y-auto">
                {activities.map((activity, i) => (
                  <div
                    key={i}
                    className={`p-3 rounded-lg font-mono text-sm ${
                      activity.type === "ERROR" ? "bg-red-500/10 text-red-400" :
                      activity.type === "SAVE" ? "bg-green-500/10 text-green-400" :
                      activity.type === "LOAD" ? "bg-blue-500/10 text-blue-400" :
                      "bg-gray-800/50 text-gray-400"
                    }`}
                  >
                    <span className="text-gray-600">[{activity.timestamp}]</span>{" "}
                    <span className="font-bold">{activity.type}</span>{" "}
                    {activity.details}
                  </div>
                ))}
                {activities.length === 0 && (
                  <div className="text-gray-500 text-center py-8">
                    No activity recorded yet
                  </div>
                )}
              </div>
            </div>
          )}

          {/* Stats Tab */}
          {tab === "stats" && (
            <div className="grid md:grid-cols-2 lg:grid-cols-4 gap-4">
              <div className="card text-center">
                <div className="text-4xl font-bold text-purple-400">{players.length}</div>
                <div className="text-gray-500">Total Players</div>
              </div>
              <div className="card text-center">
                <div className="text-4xl font-bold text-green-400">
                  ${players.reduce((acc, p) => acc + (p.money || 0), 0).toLocaleString()}
                </div>
                <div className="text-gray-500">Total Economy</div>
              </div>
              <div className="card text-center">
                <div className="text-4xl font-bold text-blue-400">{activities.length}</div>
                <div className="text-gray-500">Activities Logged</div>
              </div>
              <div className="card text-center">
                <div className="text-4xl font-bold text-amber-400">
                  {players.filter(p => {
                    if (!p.lastSeen) return false;
                    const lastSeen = new Date(p.lastSeen);
                    const hourAgo = new Date(Date.now() - 60 * 60 * 1000);
                    return lastSeen > hourAgo;
                  }).length}
                </div>
                <div className="text-gray-500">Online (last hour)</div>
              </div>
            </div>
          )}
        </div>
      </div>
    </div>
  );
}

