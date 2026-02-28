export type LobbyStatus = 'Lobby' | 'ChoosingTrump' | 'Bidding' | 'Playing' | 'Completed'

export type CardKind = 'Standard' | 'Wizard' | 'Jester'
export type Suit = 'Clubs' | 'Diamonds' | 'Hearts' | 'Spades'

export interface JoinLobbyResponse {
  lobbyCode: string
  playerId: string
  seatToken: string
  isHost: boolean
}

export interface CardView {
  id: string
  kind: CardKind
  suit: Suit
  value: number | null
  isPlayable: boolean
}

export interface TrickPlayView {
  playerId: string
  seatIndex: number
  card: CardView
}

export interface PlayerView {
  playerId: string
  name: string
  seatIndex: number
  isHost: boolean
  isYou: boolean
  connected: boolean
  score: number
  bid: number | null
  tricksWon: number
  handCount: number
  hand: CardView[] | null
}

export interface RoundView {
  roundNumber: number
  dealerSeatIndex: number
  startingSeatIndex: number
  completedTricks: number
  trumpSuit: Suit | null
  upCard: CardView | null
  requiresDealerTrumpSelection: boolean
  currentTrickNumber: number
  currentTrickLeaderPlayerId: string
  currentTrickPlays: TrickPlayView[]
}

export interface PlayerScopedState {
  lobbyCode: string
  status: LobbyStatus
  maxRounds: number
  currentRoundNumber: number | null
  currentTurnPlayerId: string | null
  round: RoundView | null
  players: PlayerView[]
  youPlayerId: string
  canStartGame: boolean
  allowedBids: number[]
  winnerPlayerIds: string[]
}

export interface StateUpdatedEnvelope {
  revision: number
  schemaVersion: string
  reason: string
  state: PlayerScopedState
}

export interface SessionState {
  playerName: string
  lobbyCode: string
  playerId: string
  seatToken: string
}
