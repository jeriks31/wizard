import { useEffect } from 'react'
import { useNavigate, useParams } from 'react-router-dom'
import { PlayingCard } from '../components/PlayingCard'
import { useWizardClient } from '../WizardClientContext'
import type { Suit } from '../types'

const suits: Suit[] = ['Clubs', 'Diamonds', 'Hearts', 'Spades']

export function GamePage() {
  const { lobbyCode } = useParams()
  const navigate = useNavigate()
  const { session, envelope, connectionState, lastError, chooseTrump, submitBid, playCard } = useWizardClient()

  useEffect(() => {
    if (!session || !lobbyCode || session.lobbyCode !== lobbyCode) {
      navigate('/')
    }
  }, [session, lobbyCode, navigate])

  if (!session || !lobbyCode || session.lobbyCode !== lobbyCode) {
    return null
  }

  if (!envelope) {
    return (
      <main className="layout">
        <section className="panel">
          <h1>Connecting to {session.lobbyCode}</h1>
          <p>Connection: {connectionState}</p>
        </section>
      </main>
    )
  }

  const state = envelope.state
  const me = state.players.find(p => p.isYou)
  const round = state.round
  const isMyTurn = state.currentTurnPlayerId === state.youPlayerId
  const dealer = round ? state.players.find(p => p.seatIndex === round.dealerSeatIndex) : null
  const shouldChooseTrump =
    state.status === 'ChoosingTrump' && round?.requiresDealerTrumpSelection && dealer?.playerId === state.youPlayerId
  const currentTurnPlayer = state.currentTurnPlayerId
    ? state.players.find(p => p.playerId === state.currentTurnPlayerId)
    : null
  const activePlayer =
    state.status === 'ChoosingTrump' && round?.requiresDealerTrumpSelection ? dealer : currentTurnPlayer
  const activePlayerTag =
    state.status === 'ChoosingTrump'
      ? 'Choosing trump'
      : state.status === 'Bidding'
        ? 'Bidding'
        : state.status === 'Playing'
          ? 'Playing'
          : null

  return (
    <main className="layout">
      <section className="panel">
        <header className="row-between">
          <div>
            <p>
              Lobby {state.lobbyCode} | Status: {state.status} | Round {state.currentRoundNumber ?? '-'} /{' '}
              {state.maxRounds}
            </p>
            <p>Connection: {connectionState}</p>
          </div>
        </header>

        {lastError ? <p className="error">{lastError}</p> : null}

        {state.status === 'Completed' ? (
          <section className="card">
            <h2>Game Over</h2>
            <p>Winners: {state.winnerPlayerIds.join(', ')}</p>
          </section>
        ) : null}

        {round ? (
          <section className="card">
            <h2>Round {round.roundNumber}</h2>
            <p>
              Trump: {round.trumpSuit ?? 'No trump'}
            </p>
            <div className="row">
              <span>Upcard:</span>
              {round.upCard ? <PlayingCard card={round.upCard} size="small" /> : <span>-</span>}
            </div>
            <p>
              Trick {round.currentTrickNumber} | Completed tricks: {round.completedTricks}
            </p>
            <h3>Current trick</h3>
            <ul className="trick-plays">
              {round.currentTrickPlays.map(play => (
                <li key={`${play.playerId}-${play.card.id}`} className="trick-play">
                  <span className="trick-play-name">
                    {state.players.find(player => player.playerId === play.playerId)?.name ?? play.playerId}
                  </span>
                  <PlayingCard card={play.card} size="small" />
                </li>
              ))}
            </ul>
          </section>
        ) : null}

        {shouldChooseTrump ? (
          <section className="card">
            <h2>Choose Trump (Dealer)</h2>
            <div className="row">
              {suits.map(suit => (
                <button
                  key={suit}
                  type="button"
                  onClick={() => {
                    void chooseTrump(suit)
                  }}
                >
                  {suit}
                </button>
              ))}
            </div>
          </section>
        ) : null}

        {state.status === 'Bidding' && round && isMyTurn ? (
          <section className="card">
            <h2>Your Bid</h2>
            <div className="row">
              {Array.from({ length: round.roundNumber + 1 }).map((_, bid) => (
                <button
                  key={bid}
                  type="button"
                  disabled={!state.allowedBids.includes(bid)}
                  onClick={() => {
                    void submitBid(round.roundNumber, bid)
                  }}
                >
                  {bid}
                </button>
              ))}
            </div>
          </section>
        ) : null}

        <section className="card">
          <h2>Players</h2>
          <ul className="list">
            {state.players.map(player => (
              <li
                key={player.playerId}
                className={`row-between card ${activePlayer?.playerId === player.playerId ? 'current-turn' : ''}`}
              >
                <span>
                  {player.name}
                  {player.isYou ? ' (You)' : ''}
                  {activePlayer?.playerId === player.playerId && activePlayerTag ? ` (${activePlayerTag})` : ''}
                </span>
                <span>
                  score {player.score} | bid {player.bid ?? '-'} | tricks {player.tricksWon} | cards{' '}
                  {player.handCount}
                </span>
              </li>
            ))}
          </ul>
        </section>

        <section className="card">
          <h2>Your Hand</h2>
          <div className="row wrap">
            {(me?.hand ?? []).map(card => {
              return (
                <button
                  key={card.id}
                  type="button"
                  className="card-button"
                  disabled={!card.isPlayable}
                  onClick={() => {
                    if (!round) {
                      return
                    }
                    void playCard(round.roundNumber, round.currentTrickNumber, card.id)
                  }}
                >
                  <PlayingCard card={card} />
                </button>
              )
            })}
          </div>
        </section>
      </section>
    </main>
  )
}
