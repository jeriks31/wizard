import { useEffect, useMemo } from 'react'
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
  const playersBySeat = [...state.players].sort((a, b) => a.seatIndex - b.seatIndex)
  const historyRows = useMemo(() => {
    const previousScoresByPlayer = new Map<string, number>()

    return state.roundHistory.map(row => ({
      ...row,
      cells: row.cells.map(cell => {
        if (cell.score === null) {
          return { ...cell, outcome: 'pending' as const }
        }

        const previousScore = previousScoresByPlayer.get(cell.playerId) ?? 0
        const delta = cell.score - previousScore
        previousScoresByPlayer.set(cell.playerId, cell.score)

        return {
          ...cell,
          outcome: delta > 0 ? ('win' as const) : delta < 0 ? ('loss' as const) : ('pending' as const),
        }
      }),
    }))
  }, [state.roundHistory])
  const round = state.round
  const trickOrderPlayers = round
    ? (() => {
        const leaderIndex = playersBySeat.findIndex(player => player.playerId === round.currentTrickLeaderPlayerId)
        if (leaderIndex < 0) {
          return playersBySeat
        }
        return playersBySeat.map((_, offset) => playersBySeat[(leaderIndex + offset) % playersBySeat.length])
      })()
    : []
  const trickSlots = round
    ? trickOrderPlayers.map(player => ({
        player,
        play: round.currentTrickPlays.find(play => play.playerId === player.playerId) ?? null,
      }))
    : []
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
              {trickSlots.map(({ player, play }) => (
                <li key={player.playerId} className="trick-play">
                  <span className="trick-play-name">
                    {player.name}
                  </span>
                  {play ? (
                    <PlayingCard card={play.card} size="small" />
                  ) : (
                    <div className="trick-play-placeholder" aria-hidden="true" />
                  )}
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
                  {player.isYou ? 'You' : player.name}
                  {activePlayer?.playerId === player.playerId && activePlayerTag ? ` (${activePlayerTag})` : ''}
                </span>
                <span>
                  tricks: {player.tricksWon} / {player.bid ?? '-'}
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

        <section className="card history-card">
          <h2>Round History</h2>
          <div className="history-table-wrap">
            <table className="history-table">
              <thead>
                <tr>
                  <th scope="col" aria-label="Round" />
                  {playersBySeat.map(player => (
                    <th key={player.playerId} scope="col">
                      {player.name}
                    </th>
                  ))}
                </tr>
              </thead>
              <tbody>
                {historyRows.map(row => (
                  <tr key={row.roundNumber}>
                    <th scope="row">{row.roundNumber}</th>
                    {row.cells.map(cell => (
                      <td key={`${row.roundNumber}-${cell.playerId}`} className={`history-cell-${cell.outcome}`}>
                        <div className="history-cell">
                          <span>B: {cell.bid ?? '-'}</span>
                          <span>S: {cell.score ?? '-'}</span>
                        </div>
                      </td>
                    ))}
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </section>
      </section>
    </main>
  )
}
