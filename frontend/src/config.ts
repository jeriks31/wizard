const runtimeConfig =
  typeof window !== 'undefined' ? window.__WIZARD_CONFIG__ : undefined

const backendUrl = runtimeConfig?.backendUrl?.trim() || ''

export const apiBaseUrl = backendUrl.replace(/\/+$/, '')

export const hubUrl = apiBaseUrl ? `${apiBaseUrl}/hubs/game` : '/hubs/game'
