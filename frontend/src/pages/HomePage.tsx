import { useState, type FormEvent } from 'react'
import { useNavigate } from 'react-router-dom'
import { createLobby, joinLobby } from '../api'
import { useWizardClient } from '../WizardClientContext'

export function HomePage() {
  const navigate = useNavigate()
  const { setSession } = useWizardClient()
  const [playerName, setPlayerName] = useState('')
  const [joinCode, setJoinCode] = useState('')
  const [error, setError] = useState<string | null>(null)
  const [loading, setLoading] = useState(false)

  const create = async (event: FormEvent) => {
    event.preventDefault()
    if (!playerName.trim()) {
      setError('Player name is required.')
      return
    }
    setLoading(true)
    setError(null)
    try {
      const join = await createLobby(playerName.trim())
      setSession({
        playerName: playerName.trim(),
        lobbyCode: join.lobbyCode,
        playerId: join.playerId,
        seatToken: join.seatToken,
      })
      navigate(`/lobby/${join.lobbyCode}`)
    } catch (err) {
      setError(String(err))
    } finally {
      setLoading(false)
    }
  }

  const join = async (event: FormEvent) => {
    event.preventDefault()
    if (!playerName.trim() || !joinCode.trim()) {
      setError('Player name and lobby code are required.')
      return
    }
    setLoading(true)
    setError(null)
    try {
      const joinResponse = await joinLobby(joinCode.trim().toUpperCase(), playerName.trim())
      setSession({
        playerName: playerName.trim(),
        lobbyCode: joinResponse.lobbyCode,
        playerId: joinResponse.playerId,
        seatToken: joinResponse.seatToken,
      })
      navigate(`/lobby/${joinResponse.lobbyCode}`)
    } catch (err) {
      setError(String(err))
    } finally {
      setLoading(false)
    }
  }

  return (
    <main className="layout">
      <section className="panel">
        <h1>Wizard Card Game</h1>
        <p>Server-authoritative multiplayer Wizard for 3-6 players.</p>

        <form className="stack" onSubmit={create}>
          <label htmlFor="name">Player name</label>
          <input
            id="name"
            value={playerName}
            onChange={e => setPlayerName(e.target.value)}
            maxLength={32}
            placeholder="Jan"
          />
          <button type="submit" disabled={loading}>
            Create Lobby
          </button>
        </form>

        <form className="stack" onSubmit={join}>
          <label htmlFor="joinCode">Join by code</label>
          <input
            id="joinCode"
            value={joinCode}
            onChange={e => setJoinCode(e.target.value)}
            maxLength={6}
            placeholder="ABC123"
          />
          <button type="submit" disabled={loading}>
            Join Lobby
          </button>
        </form>

        {error ? <p className="error">{error}</p> : null}
      </section>
    </main>
  )
}
