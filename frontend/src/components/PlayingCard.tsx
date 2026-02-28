import type { CardView } from '../types'

type PlayingCardSize = 'small' | 'normal'

interface PlayingCardProps {
  card: CardView
  size?: PlayingCardSize
}

const suitSymbol: Record<CardView['suit'], string> = {
  Clubs: '♣',
  Diamonds: '♦',
  Hearts: '♥',
  Spades: '♠',
}

export function PlayingCard({ card, size = 'normal' }: PlayingCardProps) {
  const rank = card.kind === 'Standard' ? String(card.value ?? '?') : card.kind === 'Wizard' ? 'W' : 'J'
  const symbol = suitSymbol[card.suit]
  const suitClass = card.kind === 'Standard' ? card.suit.toLowerCase() : ''

  return (
    <div className={`playing-card ${size === 'small' ? 'small' : ''} ${suitClass}`}>
      <div className="playing-card-corner top-left">
        <span>{rank}</span>
        <span>{symbol}</span>
      </div>
      <div className="playing-card-corner bottom-right">
        <span>{rank}</span>
        <span>{symbol}</span>
      </div>
    </div>
  )
}
