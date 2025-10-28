import { useEffect } from 'react'
import { useGameMapStore } from './useGameMapStore'
import { useUiStore } from '../../components/ui/uiStore'
import { useModalParam } from '../../router/query'

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
  const { state: modalState, openModal, closeModal } = useModalParam()

  useEffect(() => {
    function handleKeyDown(e: KeyboardEvent) {
      const rawKey = e.key
      const key = rawKey.toLowerCase()

      // Help toggle (H or ?), allowed even when help modal is open
      if (key === 'h' || rawKey === '?') {
        e.preventDefault()
        if (modalState.modal === 'help') {
          closeModal('replace')
        } else {
          openModal('help', undefined, 'replace')
        }
        return
      }

      // Suspend other hotkeys when any non-help modal is open
      if (isModalOpen) return

      // Ignore if typing in an input/textarea
      const target = e.target as HTMLElement
      if (target.tagName === 'INPUT' || target.tagName === 'TEXTAREA') return

      // key already computed above

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
    modalState.modal,
    openModal,
    closeModal,
  ])
}

