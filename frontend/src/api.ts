import type { JoinLobbyResponse } from './types'

const apiBase = import.meta.env.VITE_API_BASE_URL?.trim() || ''

async function fetchJson<T>(url: string, init: RequestInit): Promise<T> {
  const response = await fetch(`${apiBase}${url}`, {
    ...init,
    headers: {
      'Content-Type': 'application/json',
      ...(init.headers || {}),
    },
  })

  if (!response.ok) {
    const errorText = await response.text()
    throw new Error(errorText || `Request failed with ${response.status}`)
  }

  return (await response.json()) as T
}

export async function createLobby(playerName: string): Promise<JoinLobbyResponse> {
  return await fetchJson<JoinLobbyResponse>('/api/lobbies', {
    method: 'POST',
    body: JSON.stringify({ playerName }),
  })
}

export async function joinLobby(lobbyCode: string, playerName: string): Promise<JoinLobbyResponse> {
  return await fetchJson<JoinLobbyResponse>(`/api/lobbies/${encodeURIComponent(lobbyCode)}/join`, {
    method: 'POST',
    body: JSON.stringify({ playerName }),
  })
}
