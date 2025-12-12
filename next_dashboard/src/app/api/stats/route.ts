import { NextResponse } from 'next/server';
import fs from 'fs';
import path from 'path';

const DATA_DIR = path.join(process.cwd(), 'data', 'players');

// GET /api/stats - Get server statistics
export async function GET() {
  try {
    if (!fs.existsSync(DATA_DIR)) {
      return NextResponse.json({
        totalPlayers: 0,
        totalMoney: 0,
        totalPlaytime: 0,
        totalKills: 0,
        totalDeaths: 0,
        topPlayers: [],
        recentPlayers: [],
      });
    }
    
    const files = fs.readdirSync(DATA_DIR).filter(f => f.endsWith('.json'));
    
    let totalMoney = 0;
    let totalPlaytime = 0;
    let totalKills = 0;
    let totalDeaths = 0;
    
    const players = files.map(f => {
      try {
        const data = JSON.parse(fs.readFileSync(path.join(DATA_DIR, f), 'utf-8'));
        
        totalMoney += data.Money || 0;
        totalPlaytime += data.TotalPlaytime || 0;
        totalKills += data.TotalKills || 0;
        totalDeaths += data.TotalDeaths || 0;
        
        return {
          steamId: data.SteamId || f.replace('.json', ''),
          name: data.PreferredUsername || data.SteamName || 'Unknown',
          money: data.Money || 0,
          kills: data.TotalKills || 0,
          deaths: data.TotalDeaths || 0,
          playtime: data.TotalPlaytime || 0,
          lastSeen: data.LastSeen || null,
        };
      } catch {
        return null;
      }
    }).filter(Boolean);
    
    // Sort by money for top players
    const topByMoney = [...players].sort((a, b) => (b?.money || 0) - (a?.money || 0)).slice(0, 10);
    
    // Sort by last seen for recent players
    const recentPlayers = [...players]
      .filter(p => p?.lastSeen)
      .sort((a, b) => new Date(b?.lastSeen || 0).getTime() - new Date(a?.lastSeen || 0).getTime())
      .slice(0, 10);
    
    // Sort by kills for top killers
    const topKillers = [...players].sort((a, b) => (b?.kills || 0) - (a?.kills || 0)).slice(0, 10);
    
    return NextResponse.json({
      totalPlayers: players.length,
      totalMoney,
      totalPlaytime,
      totalKills,
      totalDeaths,
      avgMoney: players.length > 0 ? Math.round(totalMoney / players.length) : 0,
      avgPlaytime: players.length > 0 ? Math.round(totalPlaytime / players.length) : 0,
      topByMoney,
      topKillers,
      recentPlayers,
    });
  } catch (error) {
    console.error('GET /api/stats error:', error);
    return NextResponse.json({ error: 'Internal server error' }, { status: 500 });
  }
}

