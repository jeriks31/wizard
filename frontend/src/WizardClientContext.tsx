import {
  HubConnection,
  HubConnectionBuilder,
  HubConnectionState,
  LogLevel,
} from '@microsoft/signalr'
import {
  createContext,
  useCallback,
  useContext,
  useEffect,
  useMemo,
  useRef,
  useState,
  type ReactNode,
} from 'react'
import type { SessionState, StateUpdatedEnvelope, Suit } from './types'

const SESSION_KEY = 'wizard_session_v1'

interface WizardClientContextValue {
  session: SessionState | null
  envelope: StateUpdatedEnvelope | null
  connectionState: HubConnectionState
  lastError: string | null
  setSession: (session: SessionState | null) => void
  startGame: () => Promise<void>
  chooseTrump: (suit: Suit) => Promise<void>
  submitBid: (roundNumber: number, bid: number) => Promise<void>
  playCard: (roundNumber: number, trickNumber: number, cardId: string) => Promise<void>
  requestState: () => Promise<void>
}

const WizardClientContext = createContext<WizardClientContextValue | null>(null)

const hubUrl = import.meta.env.VITE_HUB_URL?.trim() || '/hubs/game'

export function WizardClientProvider({ children }: { children: ReactNode }) {
  const [session, setSessionInternal] = useState<SessionState | null>(() => {
    const raw = localStorage.getItem(SESSION_KEY)
    if (!raw) {
      return null
    }
    try {
      return JSON.parse(raw) as SessionState
    } catch {
      return null
    }
  })
  const [envelope, setEnvelope] = useState<StateUpdatedEnvelope | null>(null)
  const [connectionState, setConnectionState] = useState(HubConnectionState.Disconnected)
  const [lastError, setLastError] = useState<string | null>(null)
  const connectionRef = useRef<HubConnection | null>(null)

  const setSession = useCallback((nextSession: SessionState | null) => {
    setSessionInternal(nextSession)
    if (!nextSession) {
      localStorage.removeItem(SESSION_KEY)
      setEnvelope(null)
      return
    }
    localStorage.setItem(SESSION_KEY, JSON.stringify(nextSession))
  }, [])

  useEffect(() => {
    if (!session) {
      const conn = connectionRef.current
      if (conn) {
        void conn.stop()
      }
      connectionRef.current = null
      setConnectionState(HubConnectionState.Disconnected)
      return
    }

    let disposed = false

    const connection = new HubConnectionBuilder()
      .withUrl(hubUrl)
      .configureLogging(LogLevel.Warning)
      .withAutomaticReconnect()
      .build()

    connection.onclose(() => {
      setConnectionState(connection.state)
    })
    connection.onreconnecting(() => {
      setConnectionState(connection.state)
    })
    connection.onreconnected(async () => {
      setConnectionState(connection.state)
      if (!session) {
        return
      }
      await connection.invoke('ConnectToLobby', session.lobbyCode, session.playerId, session.seatToken)
      await connection.invoke('GetState')
    })

    connection.on('ServerError', (code: string, message: string) => {
      setLastError(`${code}: ${message}`)
    })
    connection.on('StateUpdated', (incoming: StateUpdatedEnvelope) => {
      setEnvelope(prev => {
        if (!prev) {
          return incoming
        }
        return incoming.revision >= prev.revision ? incoming : prev
      })
      setLastError(null)
    })

    const startConnection = async () => {
      await connection.start()
      if (disposed) {
        return
      }
      setConnectionState(connection.state)
      await connection.invoke('ConnectToLobby', session.lobbyCode, session.playerId, session.seatToken)
      await connection.invoke('GetState')
    }

    void startConnection().catch(err => {
      setLastError(String(err))
      setConnectionState(connection.state)
    })
    connectionRef.current = connection

    return () => {
      disposed = true
      void connection.stop()
      if (connectionRef.current === connection) {
        connectionRef.current = null
      }
    }
  }, [session])

  const invoke = useCallback(async (method: string, ...args: unknown[]) => {
    const connection = connectionRef.current
    if (!connection || connection.state !== HubConnectionState.Connected) {
      throw new Error('Not connected to game hub.')
    }
    await connection.invoke(method, ...args)
  }, [])

  const value = useMemo<WizardClientContextValue>(
    () => ({
      session,
      envelope,
      connectionState,
      lastError,
      setSession,
      startGame: async () => await invoke('StartGame'),
      chooseTrump: async (suit: Suit) => await invoke('ChooseTrump', suit),
      submitBid: async (roundNumber: number, bid: number) => await invoke('SubmitBid', roundNumber, bid),
      playCard: async (roundNumber: number, trickNumber: number, cardId: string) =>
        await invoke('PlayCard', roundNumber, trickNumber, cardId),
      requestState: async () => await invoke('GetState'),
    }),
    [session, envelope, connectionState, lastError, setSession, invoke],
  )

  return <WizardClientContext.Provider value={value}>{children}</WizardClientContext.Provider>
}

export function useWizardClient() {
  const ctx = useContext(WizardClientContext)
  if (!ctx) {
    throw new Error('useWizardClient must be used inside WizardClientProvider')
  }
  return ctx
}
