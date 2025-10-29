import { useEffect, useId, useRef } from 'react'
import { useNavigate } from 'react-router-dom'
import { useUiStore } from '../ui/uiStore'

// ============================================================================
// Types
// ============================================================================

export interface ResultOverlayProps {
  status: 'victory' | 'defeat'
  turns: number
  citiesCaptured?: number
  gameId: number
}

// ============================================================================
// ResultOverlay Component
// ============================================================================

export function ResultOverlay({ status, turns, citiesCaptured, gameId }: ResultOverlayProps) {
  const titleId = useId()
  const overlayRef = useRef<HTMLDivElement>(null)
  const navigate = useNavigate()
  const setModalState = useUiStore((state) => state.setModalState)

  // Focus trap
  useEffect(() => {
    const overlay = overlayRef.current
    if (!overlay) return

    const focusableElements = overlay.querySelectorAll<HTMLElement>(
      'button, [href], input, select, textarea, [tabindex]:not([tabindex="-1"])'
    )
    const firstElement = focusableElements[0]
    const lastElement = focusableElements[focusableElements.length - 1]

    const trapFocus = (e: KeyboardEvent) => {
      if (e.key !== 'Tab') return

      if (e.shiftKey) {
        if (document.activeElement === firstElement) {
          e.preventDefault()
          lastElement?.focus()
        }
      } else {
        if (document.activeElement === lastElement) {
          e.preventDefault()
          firstElement?.focus()
        }
      }
    }

    overlay.addEventListener('keydown', trapFocus)
    firstElement?.focus()

    return () => {
      overlay.removeEventListener('keydown', trapFocus)
    }
  }, [])

  // Prevent map inputs
  useEffect(() => {
    const preventMapInputs = (e: KeyboardEvent) => {
      // Prevent hotkeys from being processed
      e.stopPropagation()
    }

    document.addEventListener('keydown', preventMapInputs, true)

    return () => {
      document.removeEventListener('keydown', preventMapInputs, true)
    }
  }, [])

  const handleStartNewGame = () => {
    setModalState('start-new')
  }

  const handleViewSaves = () => {
    setModalState('saves')
  }

  const handleAbout = () => {
    navigate('/about')
  }

  const backgroundImage = status === 'victory' 
    ? '/images/game/modals/victory.png'
    : '/images/game/modals/defeat.png'

  const title = status === 'victory' ? 'Victory!' : 'Defeat'
  const subtitle = status === 'victory'
    ? 'Congratulations! You have conquered the map.'
    : 'Your empire has fallen. Better luck next time!'

  return (
    <div
      ref={overlayRef}
      className="fixed inset-0 z-50 flex items-center justify-center bg-black/80"
      role="dialog"
      aria-modal="true"
      aria-labelledby={titleId}
    >
      {/* Background Image */}
      <div
        className="absolute inset-0 bg-cover bg-center opacity-30"
        style={{ backgroundImage: `url(${backgroundImage})` }}
        aria-hidden="true"
      />

      {/* Content */}
      <div className="relative z-10 flex max-w-2xl flex-col items-center gap-6 rounded-lg bg-white/95 px-8 py-12 text-center shadow-2xl backdrop-blur-sm">
        <h1 id={titleId} className="text-4xl font-bold text-slate-900">
          {title}
        </h1>

        <p className="text-lg text-slate-700">{subtitle}</p>

        {/* Summary */}
        <div className="flex gap-8 text-center">
          <div className="flex flex-col">
            <span className="text-3xl font-bold text-blue-600">{turns}</span>
            <span className="text-sm text-slate-600">Turns Taken</span>
          </div>
          {citiesCaptured !== undefined && (
            <div className="flex flex-col">
              <span className="text-3xl font-bold text-blue-600">{citiesCaptured}</span>
              <span className="text-sm text-slate-600">Cities Captured</span>
            </div>
          )}
        </div>

        {/* Actions */}
        <div className="flex flex-col gap-3 w-full mt-4">
          <button
            type="button"
            className="rounded-lg bg-blue-600 px-6 py-3 text-base font-semibold text-white hover:bg-blue-700 focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-blue-600 transition-colors"
            onClick={handleStartNewGame}
          >
            Start New Game
          </button>

          <button
            type="button"
            className="rounded-lg border-2 border-blue-600 px-6 py-3 text-base font-medium text-blue-600 hover:bg-blue-50 focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-blue-600 transition-colors"
            onClick={handleViewSaves}
          >
            View Saves
          </button>

          <button
            type="button"
            className="rounded-lg px-6 py-2 text-sm font-medium text-slate-600 hover:bg-slate-100 focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-slate-600 transition-colors"
            onClick={handleAbout}
          >
            About
          </button>
        </div>
      </div>
    </div>
  )
}

