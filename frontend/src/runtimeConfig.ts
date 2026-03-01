const rawApiBaseUrl = import.meta.env.VITE_API_BASE_URL?.trim()

function stripTrailingSlashes(value: string): string {
  return value.replace(/\/+$/, '')
}

export const apiBaseUrl = stripTrailingSlashes(rawApiBaseUrl)
export const gameHubUrl = `${apiBaseUrl}/hubs/game`
