interface WizardRuntimeConfig {
  backendUrl?: string
}

interface Window {
  __WIZARD_CONFIG__?: WizardRuntimeConfig
}
