import { NextRequest, NextResponse } from 'next/server';
import fs from 'fs';
import path from 'path';

const DATA_DIR = path.join(process.cwd(), 'data', 'players');

// GET /api/players/[steamId] - Get specific player
export async function GET(
  request: NextRequest,
  { params }: { params: Promise<{ steamId: string }> }
) {
  try {
    const { steamId } = await params;
    const filePath = path.join(DATA_DIR, `${steamId}.json`);
    
    if (!fs.existsSync(filePath)) {
      return NextResponse.json({ error: 'Player not found' }, { status: 404 });
    }
    
    const data = JSON.parse(fs.readFileSync(filePath, 'utf-8'));
    return NextResponse.json(data);
  } catch (error) {
    console.error('GET player error:', error);
    return NextResponse.json({ error: 'Internal server error' }, { status: 500 });
  }
}

// PUT /api/players/[steamId] - Update specific player (admin)
export async function PUT(
  request: NextRequest,
  { params }: { params: Promise<{ steamId: string }> }
) {
  try {
    const { steamId } = await params;
    const updates = await request.json();
    const filePath = path.join(DATA_DIR, `${steamId}.json`);
    
    if (!fs.existsSync(filePath)) {
      return NextResponse.json({ error: 'Player not found' }, { status: 404 });
    }
    
    const existingData = JSON.parse(fs.readFileSync(filePath, 'utf-8'));
    
    // Create backup before updating
    const backupDir = path.join(DATA_DIR, 'backups');
    if (!fs.existsSync(backupDir)) {
      fs.mkdirSync(backupDir, { recursive: true });
    }
    fs.writeFileSync(
      path.join(backupDir, `${steamId}_${Date.now()}.json`),
      JSON.stringify(existingData, null, 2)
    );
    
    const updatedData = {
      ...existingData,
      ...updates,
      SteamId: steamId, // Prevent changing SteamId
      LastModified: new Date().toISOString(),
    };
    
    fs.writeFileSync(filePath, JSON.stringify(updatedData, null, 2));
    
    console.log(`✏️ Updated player ${steamId}:`, Object.keys(updates));
    
    return NextResponse.json({ success: true, data: updatedData });
  } catch (error) {
    console.error('PUT player error:', error);
    return NextResponse.json({ error: 'Internal server error' }, { status: 500 });
  }
}

// DELETE /api/players/[steamId] - Delete specific player
export async function DELETE(
  request: NextRequest,
  { params }: { params: Promise<{ steamId: string }> }
) {
  try {
    const { steamId } = await params;
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
    
    console.log(`🗑️ Deleted player ${steamId}`);
    
    return NextResponse.json({ success: true });
  } catch (error) {
    console.error('DELETE player error:', error);
    return NextResponse.json({ error: 'Internal server error' }, { status: 500 });
  }
}

