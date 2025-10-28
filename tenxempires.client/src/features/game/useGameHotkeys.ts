import { useEffect } from 'react'
import { useGameMapStore } from './useGameMapStore'
import { useUiStore } from '../../components/ui/uiStore'

interface UseGameHotkeysOptions {
  onEndTurn: () => void
  onNextUnit: () => void
  onZoomIn: () => void
  onZoomOut: () => void
  onPan: (dx: number, dy: number) => void
  isEndTurnDisabled: boolean
}

/**
 * Handles keyboard shortcuts for the game map.
 * Suspends hotkeys when a modal is open.
 */
export function useGameHotkeys({
  onEndTurn,
  onNextUnit,
  onZoomIn,
  onZoomOut,
  onPan,
  isEndTurnDisabled,
}: UseGameHotkeysOptions) {
  const { toggleGrid, clearSelection } = useGameMapStore()
  const { isModalOpen } = useUiStore()

  useEffect(() => {
    function handleKeyDown(e: KeyboardEvent) {
      // Suspend hotkeys when modal is open
      if (isModalOpen) return

      // Ignore if typing in an input/textarea
      const target = e.target as HTMLElement
      if (target.tagName === 'INPUT' || target.tagName === 'TEXTAREA') return

      const key = e.key.toLowerCase()

      // E - End Turn
      if (key === 'e') {
        e.preventDefault()
        if (!isEndTurnDisabled) {
          onEndTurn()
        }
        return
      }

      // N - Next Unit
      if (key === 'n') {
        e.preventDefault()
        onNextUnit()
        return
      }

      // G - Toggle Grid
      if (key === 'g') {
        e.preventDefault()
        toggleGrid()
        return
      }

      // ESC - Cancel Selection
      if (key === 'escape') {
        e.preventDefault()
        clearSelection()
        return
      }

      // +/= - Zoom In
      if (key === '+' || key === '=') {
        e.preventDefault()
        onZoomIn()
        return
      }

      // -/_ - Zoom Out
      if (key === '-' || key === '_') {
        e.preventDefault()
        onZoomOut()
        return
      }

      // WASD / Arrow Keys - Pan
      const PAN_AMOUNT = 50
      let dx = 0
      let dy = 0

      if (key === 'w' || key === 'arrowup') {
        dy = PAN_AMOUNT
        e.preventDefault()
      } else if (key === 's' || key === 'arrowdown') {
        dy = -PAN_AMOUNT
        e.preventDefault()
      } else if (key === 'a' || key === 'arrowleft') {
        dx = PAN_AMOUNT
        e.preventDefault()
      } else if (key === 'd' || key === 'arrowright') {
        dx = -PAN_AMOUNT
        e.preventDefault()
      }

      if (dx !== 0 || dy !== 0) {
        onPan(dx, dy)
      }
    }

    window.addEventListener('keydown', handleKeyDown)
    return () => window.removeEventListener('keydown', handleKeyDown)
  }, [
    isModalOpen,
    isEndTurnDisabled,
    onEndTurn,
    onNextUnit,
    onZoomIn,
    onZoomOut,
    onPan,
    toggleGrid,
    clearSelection,
  ])
}

