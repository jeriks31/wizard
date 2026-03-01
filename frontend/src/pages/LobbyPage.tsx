import { useEffect } from 'react'
import { useNavigate, useParams } from 'react-router-dom'
import { useWizardClient } from '../WizardClientContext'

export function LobbyPage() {
  const { lobbyCode } = useParams()
  const navigate = useNavigate()
  const { session, envelope, connectionState, lastError, startGame, setSession } = useWizardClient()

  useEffect(() => {
    if (!session || !lobbyCode || session.lobbyCode !== lobbyCode) {
      navigate('/')
    }
  }, [session, lobbyCode, navigate])

  useEffect(() => {
    if (!envelope) {
      return
    }
    if (envelope.state.status !== 'Lobby') {
      navigate(`/game/${envelope.state.lobbyCode}`)
    }
  }, [envelope, navigate])

  if (!session || !lobbyCode || session.lobbyCode !== lobbyCode) {
    return null
  }

  const players = envelope?.state.players ?? []

  return (
    <main className="layout">
      <section className="panel">
        <header className="row-between">
          <div>
            <h1>Lobby {session.lobbyCode}</h1>
            <p>Connection: {connectionState}</p>
          </div>
          <button
            type="button"
            onClick={() => {
              setSession(null)
              navigate('/')
            }}
          >
            Leave
          </button>
        </header>

        {lastError ? <p className="error">{lastError}</p> : null}

        <h2>Players ({players.length})</h2>
        <ul className="list">
          {players.map(player => (
            <li key={player.playerId} className="row-between card">
              <span>
                {player.name}
                {player.isHost ? ' (Host)' : ''}
                {player.isYou ? ' (You)' : ''}
              </span>
              <span>{player.connected ? 'online' : 'offline'}</span>
            </li>
          ))}
        </ul>

        <div className="row">
          <button
            type="button"
            disabled={!envelope?.state.canStartGame}
            onClick={() => {
              void startGame()
            }}
          >
            Start Game
          </button>
        </div>
      </section>
    </main>
  )
}
