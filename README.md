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


## Game Rules

Wizard Online is a trick-taking game for 3-6 players.  
Each round, every player predicts how many tricks they will win, then plays to hit that prediction exactly.

### Cards
- 60-card deck total:
- 52 standard cards (4 suits, values 1-13)
- 4 Wizards
- 4 Jesters

### Round Flow
1. Round number starts at 1 and increases by 1 each round.
2. In round `R`, each player is dealt `R` cards.
3. One extra card is turned face up to set trump for the round.
4. Players bid how many tricks they expect to win (from `0` to `R`).
5. Players play `R` tricks.
6. Scores are calculated, then the next round begins.

### Turn Order
- Player order is fixed for the game.
- The starting player rotates each round.
- Within a round, the winner of a trick leads the next trick.

### Bidding Rules
- Each player bids once per round.
- The last player to bid cannot choose a bid that makes the total of all bids equal to the round number.

### Card Play Rules
- Players must follow the lead suit if they can.
- If they cannot follow suit, they may play any card.
- Wizards and Jesters may be played at any time.

### Trick Winner Rules
1. If one or more Wizards are played, the last Wizard played wins the trick.
2. If all cards played are Jesters, the player who led the trick wins.
3. Otherwise:
- Trump suit beats non-trump suits.
- If no trump card wins, the highest card of the winning suit wins.
- Jesters lose to any non-Jester card.

### Scoring
- Exact bid: `+10 + (10 x bid)`
- Missed bid: `-10 x |bid - tricks won|`

### End of Game
- The game continues while the deck can support the next larger round for all players. (i.e. for 6 players, the game ends after round 10 since 6 x 11 > 60)
- Final winner is the player with the highest total score.
