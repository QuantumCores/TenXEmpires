import { useState, useId } from 'react'
import { useNavigate } from 'react-router-dom'
import { createGame, deleteGame } from '../../api/games'
import type { ApiErrorDto } from '../../types/errors'

export interface StartNewGameModalProps {
  onRequestClose: () => void
  hasActiveGame?: boolean
  currentGameId?: number
}

export function StartNewGameModal({ 
  onRequestClose, 
  hasActiveGame = false, 
  currentGameId 
}: StartNewGameModalProps) {
  const navigate = useNavigate()
  const titleId = useId()
  const checkboxId = useId()
  
  const [isAcknowledged, setIsAcknowledged] = useState(false)
  const [isCreating, setIsCreating] = useState(false)
  const [isDeleting, setIsDeleting] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [showDeleteFlow, setShowDeleteFlow] = useState(false)

  const handleStartNewGame = async () => {
    if (!isAcknowledged) return

    setError(null)
    setIsCreating(true)

    try {
      const idempotencyKey = `create-game-${Date.now()}-${Math.random().toString(36).slice(2)}`
      const result = await createGame({}, idempotencyKey)

      if (!result.ok) {
        if (result.status === 409) {
          // GAME_LIMIT_REACHED
          setShowDeleteFlow(true)
          setError('You have reached the maximum number of active games. Please delete your current game first.')
          setIsCreating(false)
          return
        }
        
        if (result.status === 422) {
          // MAP_SCHEMA_MISMATCH
          setError('Map schema mismatch. Unable to create game at this time.')
          setIsCreating(false)
          return
        }
        
        if (result.status === 401 || result.status === 403) {
          // Unauthorized
          const returnUrl = encodeURIComponent('/game/current?modal=start-new')
          navigate(`/login?returnUrl=${returnUrl}`)
          return
        }

        if (result.status === 429) {
          // Rate limited
          setError('Too many requests. Please try again in a moment.')
          setIsCreating(false)
          return
        }

        const errorData = result.data as unknown as { error?: ApiErrorDto }
        setError(errorData?.error?.message || 'Failed to create game. Please try again.')
        setIsCreating(false)
        return
      }

      // Success - navigate to the new game
      if (result.data) {
        navigate(`/game/${result.data.id}`)
      }
    } catch (err) {
      setError('An unexpected error occurred. Please try again.')
      setIsCreating(false)
    }
  }

  const handleDeleteCurrentGame = async () => {
    if (!currentGameId) return

    setError(null)
    setIsDeleting(true)

    try {
      const result = await deleteGame(currentGameId)

      if (!result.ok) {
        if (result.status === 401 || result.status === 403) {
          const returnUrl = encodeURIComponent('/game/current?modal=start-new')
          navigate(`/login?returnUrl=${returnUrl}`)
          return
        }

        if (result.status === 404) {
          // Game not found, proceed to create anyway
          setShowDeleteFlow(false)
          setIsDeleting(false)
          handleStartNewGame()
          return
        }

        const errorData = result.data as unknown as { error?: ApiErrorDto }
        setError(errorData?.error?.message || 'Failed to delete game. Please try again.')
        setIsDeleting(false)
        return
      }

      // Successfully deleted, now create new game
      setShowDeleteFlow(false)
      setIsDeleting(false)
      handleStartNewGame()
    } catch (err) {
      setError('An unexpected error occurred while deleting the game.')
      setIsDeleting(false)
    }
  }

  const isBusy = isCreating || isDeleting

  return (
    <div className="flex flex-col gap-4">
      <div className="flex items-center justify-between">
        <h2 id={titleId} className="text-lg font-semibold">
          Start New Game
        </h2>
        <button
          type="button"
          className="rounded px-2 py-1 hover:bg-slate-100 disabled:opacity-50"
          onClick={onRequestClose}
          disabled={isBusy}
          aria-label="Close"
        >
          ✕
        </button>
      </div>

      {error && (
        <div 
          role="alert" 
          className="rounded border border-red-300 bg-red-50 p-3 text-sm text-red-800"
        >
          {error}
        </div>
      )}

      {showDeleteFlow ? (
        <>
          <div className="text-sm text-slate-700">
            <p className="mb-2">You have reached the maximum number of active games.</p>
            <p>Delete your current game to create a new one, or cancel and continue with your existing game.</p>
          </div>

          <div className="flex items-center justify-end gap-3 border-t border-slate-200 pt-4">
            <button
              type="button"
              className="rounded px-4 py-2 text-sm font-medium text-slate-700 hover:bg-slate-100 disabled:opacity-50"
              onClick={onRequestClose}
              disabled={isBusy}
            >
              Cancel
            </button>
            <button
              type="button"
              className="rounded bg-red-600 px-4 py-2 text-sm font-medium text-white hover:bg-red-700 disabled:opacity-50"
              onClick={handleDeleteCurrentGame}
              disabled={isBusy}
            >
              {isDeleting ? 'Deleting...' : 'Delete Current Game'}
            </button>
          </div>
        </>
      ) : (
        <>
          <div className="text-sm text-slate-700">
            {hasActiveGame ? (
              <p>Starting a new game will replace your current active game. Your progress will be saved, but only one game can be active at a time.</p>
            ) : (
              <p>You&apos;re about to start a new game. This will create a fresh game with default settings.</p>
            )}
          </div>

          <label className="flex items-start gap-2 text-sm">
            <input
              id={checkboxId}
              type="checkbox"
              checked={isAcknowledged}
              onChange={(e) => setIsAcknowledged(e.target.checked)}
              disabled={isBusy}
              className="mt-0.5"
            />
            <span>I understand and want to proceed</span>
          </label>

          <div className="flex items-center justify-between border-t border-slate-200 pt-4">
            {hasActiveGame && currentGameId && (
              <button
                type="button"
                className="text-sm text-slate-600 underline hover:text-slate-800 disabled:opacity-50"
                onClick={() => setShowDeleteFlow(true)}
                disabled={isBusy}
              >
                Delete current game instead
              </button>
            )}
            
            <div className="ml-auto flex items-center gap-3">
              <button
                type="button"
                className="rounded px-4 py-2 text-sm font-medium text-slate-700 hover:bg-slate-100 disabled:opacity-50"
                onClick={onRequestClose}
                disabled={isBusy}
              >
                Cancel
              </button>
              <button
                type="button"
                className="rounded bg-blue-600 px-4 py-2 text-sm font-medium text-white hover:bg-blue-700 disabled:cursor-not-allowed disabled:opacity-50"
                onClick={handleStartNewGame}
                disabled={!isAcknowledged || isBusy}
              >
                {isCreating ? 'Creating...' : 'Start New Game'}
              </button>
            </div>
          </div>
        </>
      )}
    </div>
  )
}

