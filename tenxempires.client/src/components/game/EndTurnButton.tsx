import { useEndTurn } from '../../features/game/useGameQueries'
import { useNotifications } from '../ui/notifications'
import type { GameStateDto } from '../../types/game'

interface EndTurnButtonProps {
  gameId: number
  gameState: GameStateDto
  disabled: boolean
}

export function EndTurnButton({ gameId, gameState, disabled }: EndTurnButtonProps) {
  const endTurnMutation = useEndTurn(gameId)
  const notifications = useNotifications()

  const handleEndTurn = () => {
    if (disabled || endTurnMutation.isPending) return

    // Check for units that haven't acted
    const playerParticipant = gameState.participants.find((p) => p.kind === 'human')
    if (playerParticipant) {
      const unactedUnits = gameState.units.filter(
        (u) => u.participantId === playerParticipant.id && !u.hasActed
      )

      if (unactedUnits.length > 0) {
        notifications.add({
          id: 'pending-actions',
          kind: 'warning',
          message: `${unactedUnits.length} unit${unactedUnits.length > 1 ? 's have' : ' has'} not moved this turn`,
          ttlMs: 3000,
        })
      }
    }

    endTurnMutation.mutate()
  }

  return (
    <button
      type="button"
      className="absolute bottom-4 right-4 rounded-lg bg-blue-600 px-6 py-3 font-semibold text-white shadow-lg hover:bg-blue-700 disabled:cursor-not-allowed disabled:opacity-50"
      onClick={handleEndTurn}
      disabled={disabled || endTurnMutation.isPending}
      aria-label="End turn"
    >
      {endTurnMutation.isPending ? (
        <span className="flex items-center gap-2">
          <svg
            className="h-5 w-5 animate-spin"
            xmlns="http://www.w3.org/2000/svg"
            fill="none"
            viewBox="0 0 24 24"
          >
            <circle
              className="opacity-25"
              cx="12"
              cy="12"
              r="10"
              stroke="currentColor"
              strokeWidth="4"
            />
            <path
              className="opacity-75"
              fill="currentColor"
              d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4zm2 5.291A7.962 7.962 0 014 12H0c0 3.042 1.135 5.824 3 7.938l3-2.647z"
            />
          </svg>
          Ending Turn...
        </span>
      ) : (
        'End Turn'
      )}
    </button>
  )
}
