# Wizard Card Game

Wizard is a multiplayer trick-taking game for 3-6 players.

## Stack

- Backend: ASP.NET Core + SignalR (`backend/src/Wizard.Api`)
- Rules engine: pure domain library (`backend/src/Wizard.Game`)
- Frontend: React + Vite + TypeScript (`frontend`)
- Runtime state: in-memory only (no persistence)
- Deployment: Docker + docker-compose (local and Unraid-friendly)


## Implemented Features

- Lobby create and join by code
- Host-started game (3-6 players only)
- Realtime `StateUpdated(...)` snapshots over SignalR
- Server-side enforcement for:
  - bids and last-bid restriction
  - follow-suit rules
  - Wizard/Jester/trump trick resolution
  - per-round scoring and game end
- Reconnect by `playerId + seatToken`

## Local Development

### Backend

```sh
dotnet run --project backend/src/Wizard.Api
```

### Frontend

```sh
cd frontend && npm install && npm run dev
```

## Docker

Build and run:

```powershell
docker compose up --build
```

Services:

- Frontend: `http://localhost:3000`
- Backend: `http://localhost:5000`
