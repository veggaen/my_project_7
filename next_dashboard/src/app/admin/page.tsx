"use client";

import { useState, useEffect } from "react";
import { Reveal } from "../components/motion/Reveal";
import { SplitText } from "../components/motion/SplitText";
import { SiteNavbar } from "../components/SiteNavbar";

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
  const [nowMs, setNowMs] = useState(0);

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
    let cancelled = false;

    (async () => {
      try {
        await Promise.all([fetchPlayers(), fetchActivity()]);
      } finally {
        if (!cancelled) setLoading(false);
      }
    })();

    // Auto-refresh every 30 seconds
    const interval = setInterval(() => {
      fetchPlayers();
      fetchActivity();
    }, 30000);

    return () => {
      cancelled = true;
      clearInterval(interval);
    };
  }, []);

  useEffect(() => {
    const updateNow = () => setNowMs(Date.now());
    updateNow();
    const interval = setInterval(updateNow, 30000);
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
      <SiteNavbar />

      <div className="nav-spacer page-section">
        <div className="page-container max-w-6xl">
          {/* Header */}
          <div className="mb-8">
            <Reveal>
              <SplitText
                as="h1"
                text="Admin Dashboard"
                className="text-4xl font-bold gradient-text"
                mode="words"
                stagger={0.08}
              />
            </Reveal>
            <Reveal delay={0.08}>
              <p className="text-gray-400">Manage players, view activity, and monitor server stats</p>
            </Reveal>
          </div>

          {/* Tabs */}
          <div className="flex gap-2 mb-6 flex-wrap">
            {["players", "activity", "stats"].map((t) => (
              <button
                key={t}
                onClick={() => setTab(t as typeof tab)}
                className={`px-5 py-2.5 rounded-xl font-semibold transition-colors whitespace-nowrap ${
                  tab === t
                    ? "bg-purple-600 text-white"
                    : "bg-gray-800 text-gray-300 hover:bg-gray-700"
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
              <div className="lg:col-span-1 card max-h-150 overflow-y-auto">
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
                      <div className="flex gap-2 flex-wrap justify-end">
                        <button
                          onClick={() => setEditMode(!editMode)}
                          className="btn btn-info"
                        >
                          {editMode ? "Cancel" : "✏️ Edit"}
                        </button>
                        <button
                          onClick={() => deletePlayer(selectedPlayer.steamId)}
                          className="btn btn-danger"
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
              <div className="space-y-2 max-h-150 overflow-y-auto">
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
                  {players.filter((p) => {
                    if (!p.lastSeen || nowMs === 0) return false;
                    const lastSeenMs = new Date(p.lastSeen).getTime();
                    return lastSeenMs > nowMs - 60 * 60 * 1000;
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

