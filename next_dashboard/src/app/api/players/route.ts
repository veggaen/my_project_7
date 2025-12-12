import { NextRequest, NextResponse } from 'next/server';
import fs from 'fs';
import path from 'path';

// Data directory for player JSON files
const DATA_DIR = path.join(process.cwd(), 'data', 'players');

// Ensure data directory exists
function ensureDataDir() {
  if (!fs.existsSync(DATA_DIR)) {
    fs.mkdirSync(DATA_DIR, { recursive: true });
  }
}

// GET /api/players - List all players
// GET /api/players?steamId=xxx - Get specific player
export async function GET(request: NextRequest) {
  try {
    ensureDataDir();
    
    const steamId = request.nextUrl.searchParams.get('steamId');
    
    if (steamId) {
      // Get specific player
      const filePath = path.join(DATA_DIR, `${steamId}.json`);
      
      if (!fs.existsSync(filePath)) {
        return NextResponse.json({ error: 'Player not found' }, { status: 404 });
      }
      
      const data = JSON.parse(fs.readFileSync(filePath, 'utf-8'));
      return NextResponse.json(data);
    }
    
    // List all players
    const files = fs.readdirSync(DATA_DIR).filter(f => f.endsWith('.json'));
    const players = files.map(f => {
      const data = JSON.parse(fs.readFileSync(path.join(DATA_DIR, f), 'utf-8'));
      return {
        steamId: data.SteamId || f.replace('.json', ''),
        steamName: data.SteamName || 'Unknown',
        preferredUsername: data.PreferredUsername || data.SteamName || 'Unknown',
        rank: data.Rank || 'Guest',
        money: data.Money || 0,
        lastSeen: data.LastSeen || null,
      };
    });
    
    return NextResponse.json({ players, count: players.length });
  } catch (error) {
    console.error('GET /api/players error:', error);
    return NextResponse.json({ error: 'Internal server error' }, { status: 500 });
  }
}

// POST /api/players - Create or update player data (called by s&box)
export async function POST(request: NextRequest) {
  try {
    ensureDataDir();
    
    const data = await request.json();
    
    if (!data.SteamId) {
      return NextResponse.json({ error: 'SteamId is required' }, { status: 400 });
    }
    
    const filePath = path.join(DATA_DIR, `${data.SteamId}.json`);
    
    // Merge with existing data if it exists
    let existingData = {};
    if (fs.existsSync(filePath)) {
      existingData = JSON.parse(fs.readFileSync(filePath, 'utf-8'));
    }
    
    const mergedData = {
      ...existingData,
      ...data,
      LastSeen: new Date().toISOString(),
    };
    
    fs.writeFileSync(filePath, JSON.stringify(mergedData, null, 2));
    
    console.log(`✅ Saved player data for ${data.SteamId}`);
    
    return NextResponse.json({ success: true, data: mergedData });
  } catch (error) {
    console.error('POST /api/players error:', error);
    return NextResponse.json({ error: 'Internal server error' }, { status: 500 });
  }
}

// DELETE /api/players?steamId=xxx - Delete player data (admin only)
export async function DELETE(request: NextRequest) {
  try {
    const steamId = request.nextUrl.searchParams.get('steamId');
    
    if (!steamId) {
      return NextResponse.json({ error: 'steamId is required' }, { status: 400 });
    }
    
    const filePath = path.join(DATA_DIR, `${steamId}.json`);
    
    if (!fs.existsSync(filePath)) {
      return NextResponse.json({ error: 'Player not found' }, { status: 404 });
    }
    
    // Create backup before deleting
    const backupDir = path.join(DATA_DIR, 'deleted');
    if (!fs.existsSync(backupDir)) {
      fs.mkdirSync(backupDir, { recursive: true });
    }
    
    const timestamp = new Date().toISOString().replace(/[:.]/g, '-');
    fs.copyFileSync(filePath, path.join(backupDir, `${steamId}_${timestamp}.json`));
    fs.unlinkSync(filePath);
    
    console.log(`🗑️ Deleted player data for ${steamId}`);
    
    return NextResponse.json({ success: true, message: `Player ${steamId} deleted` });
  } catch (error) {
    console.error('DELETE /api/players error:', error);
    return NextResponse.json({ error: 'Internal server error' }, { status: 500 });
  }
}

