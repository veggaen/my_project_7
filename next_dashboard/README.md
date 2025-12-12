# Vegga Roleplay Dashboard

A modern Next.js dashboard for managing your s&box Vegga Roleplay server.

## Features

- 📊 **Player Stats** - View detailed player statistics, skills, and progression
- 🗺️ **Roadmap** - Track development progress across all phases
- 📄 **Whitepaper** - Complete technical documentation
- 👥 **Admin Panel** - Manage players, view activity logs, edit data
- 🔗 **s&box Integration** - Real-time sync with your game server

## Setup

### 1. Install Dependencies

```bash
cd next_dashboard
npm install
```

### 2. Run Development Server

```bash
npm run dev
```

Open [http://localhost:3000](http://localhost:3000)

### 3. Configure s&box Integration

In your s&box game, the `VeggaApiClient` will automatically sync player data to your dashboard.

**Set the API URL (if not localhost):**
```
vegga_api_url "http://your-server:3000"
```

**Test the connection:**
```
vegga_api_test
```

## API Endpoints

| Endpoint | Method | Description |
|----------|--------|-------------|
| `/api/players` | GET | List all players |
| `/api/players` | POST | Create/update player (from s&box) |
| `/api/players/[steamId]` | GET | Get player details |
| `/api/players/[steamId]` | PUT | Update player (admin) |
| `/api/players/[steamId]` | DELETE | Delete player |
| `/api/activity` | GET | Get activity log |
| `/api/activity` | POST | Log activity (from s&box) |
| `/api/stats` | GET | Get server statistics |

## Data Storage

Player data is stored in JSON files:
```
next_dashboard/
  data/
    players/
      76561198012345678.json
      76561198087654321.json
    activity.log
```

## Pages

- `/` - Landing page with features overview
- `/whitepaper` - Technical documentation
- `/roadmap` - Development phases and progress
- `/stats` - Player stats lookup
- `/admin` - Admin panel for data management

## s&box Integration

The game syncs data to the dashboard via HTTP:

```csharp
// Automatically called on player disconnect, money change, etc.
await VeggaApiClient.SavePlayerAsync(stats, skills, sessionStats);

// Log activities
await VeggaApiClient.LogActivityAsync("KILL", "Player killed enemy", steamId, playerName);
```

## Deployment

For production, deploy to Vercel:

```bash
npm run build
npx vercel
```

Then update `VeggaApiClient.BaseUrl` in s&box to your production URL.

---

Built for [Vegga Roleplay](https://sbox.game) - An OSRS-inspired MMORPG on s&box.
