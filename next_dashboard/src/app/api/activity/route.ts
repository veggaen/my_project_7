import { NextRequest, NextResponse } from 'next/server';
import fs from 'fs';
import path from 'path';

const LOG_FILE = path.join(process.cwd(), 'data', 'activity.log');

// Ensure data directory exists
function ensureDataDir() {
  const dir = path.dirname(LOG_FILE);
  if (!fs.existsSync(dir)) {
    fs.mkdirSync(dir, { recursive: true });
  }
}

// GET /api/activity - Get activity log
export async function GET(request: NextRequest) {
  try {
    const limit = parseInt(request.nextUrl.searchParams.get('limit') || '100');
    
    if (!fs.existsSync(LOG_FILE)) {
      return NextResponse.json({ activities: [], count: 0 });
    }
    
    const content = fs.readFileSync(LOG_FILE, 'utf-8');
    const lines = content.split('\n').filter(Boolean).reverse().slice(0, limit);
    
    const activities = lines.map(line => {
      // Parse format: [timestamp] TYPE | details
      const match = line.match(/^\[(.+?)\] (\w+) \| (.+)$/);
      if (match) {
        return {
          timestamp: match[1],
          type: match[2],
          details: match[3],
          raw: line,
        };
      }
      return { raw: line };
    });
    
    return NextResponse.json({ activities, count: activities.length });
  } catch (error) {
    console.error('GET /api/activity error:', error);
    return NextResponse.json({ error: 'Internal server error' }, { status: 500 });
  }
}

// POST /api/activity - Log an activity (called by s&box)
export async function POST(request: NextRequest) {
  try {
    ensureDataDir();
    
    const { type, message, steamId, playerName } = await request.json();
    
    if (!type || !message) {
      return NextResponse.json({ error: 'type and message are required' }, { status: 400 });
    }
    
    const timestamp = new Date().toISOString().replace('T', ' ').substring(0, 19);
    const playerInfo = playerName ? `${playerName} (${steamId})` : steamId || '';
    const logEntry = `[${timestamp}] ${type.toUpperCase()} | ${playerInfo ? playerInfo + ' - ' : ''}${message}\n`;
    
    fs.appendFileSync(LOG_FILE, logEntry);
    
    return NextResponse.json({ success: true });
  } catch (error) {
    console.error('POST /api/activity error:', error);
    return NextResponse.json({ error: 'Internal server error' }, { status: 500 });
  }
}

