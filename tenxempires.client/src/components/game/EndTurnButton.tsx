import { useEndTurn } from '../../features/game/useGameQueries'
import { useNotifications } from '../ui/notifications'
import type { GameStateDto } from '../../types/game'

interface EndTurnButtonProps {
  gameId: number
  gameState: GameStateDto
  disabled: boolean
  turnNo: number
  status: string
}

export function EndTurnButton({ gameId, gameState, disabled, turnNo, status }: EndTurnButtonProps) {
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

  const isActive = status === 'active'
  const outerBorderColor = isActive ? 'ring-green-400' : 'ring-slate-400'

  return (
    <button
      type="button"
      className={`
        absolute bottom-4 right-4 
        h-32 w-32 rounded-full 
        flex flex-col items-center justify-center
        ring-2 ${outerBorderColor}
        bg-gradient-to-tl from-amber-300 via-yellow-400 to-amber-500
        p-3
        shadow-2xl
        hover:shadow-3xl hover:scale-105
        transition-all duration-200
        disabled:cursor-not-allowed disabled:opacity-60 disabled:hover:scale-100
      `}
      onClick={handleEndTurn}
      disabled={disabled || endTurnMutation.isPending}
      aria-label={`End turn ${turnNo}`}
    >
      <div className="h-full w-full rounded-full bg-gradient-to-br from-yellow-200 via-amber-300 to-yellow-400 flex flex-col items-center justify-center p-1 ring-2 ring-amber-600">
        {endTurnMutation.isPending ? (
          <svg
            className="h-8 w-8 animate-spin text-amber-800"
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
        ) : (
          <>
            <div className="text-4xl font-black text-amber-950 leading-none tracking-tight">
              {turnNo}
            </div>
            <div className="text-xs font-bold text-amber-900 uppercase tracking-wide mt-1">
              End Turn
            </div>
          </>
        )}
      </div>
    </button>
  )
}
