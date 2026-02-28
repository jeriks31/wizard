import { Navigate, Route, Routes } from 'react-router-dom'
import { WizardClientProvider } from './WizardClientContext'
import { GamePage } from './pages/GamePage'
import { HomePage } from './pages/HomePage'
import { LobbyPage } from './pages/LobbyPage'

export default function App() {
  return (
    <WizardClientProvider>
      <Routes>
        <Route path="/" element={<HomePage />} />
        <Route path="/lobby/:lobbyCode" element={<LobbyPage />} />
        <Route path="/game/:lobbyCode" element={<GamePage />} />
        <Route path="*" element={<Navigate to="/" replace />} />
      </Routes>
    </WizardClientProvider>
  )
}
