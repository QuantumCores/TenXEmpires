import { useMemo, useEffect, useState } from 'react'
import { useNavigate } from 'react-router-dom'
import type React from 'react'
import { ModalContainer } from './ModalContainer'
import { StartNewGameModal } from './StartNewGameModal'
import { SavesModal } from './SavesModal'
import { HelpModal } from './HelpModal'
import { SettingsModal } from './SettingsModal'
import { ErrorSchemaModal } from './ErrorSchemaModal'
import { SessionExpiredModal } from './SessionExpiredModal'
import { CityModal } from './CityModal'
import { useModalParam } from '../../router/query'
import { useBackstackCloseBehavior } from '../../router/backstack'
import { fetchGames } from '../../api/games'
import { useGameState } from '../../features/game/useGameQueries'
import { useUiStore } from '../ui/uiStore'

export type ModalKey =
  | 'saves'
  | 'settings'
  | 'help'
  | 'account-delete'
  | 'start-new'
  | 'session-expired'
  | 'error-schema'
  | 'error-ai'
  | 'city'

type ModalProps = { onRequestClose: () => void }

function PlaceholderModal({ title, onRequestClose }: { title: string } & ModalProps) {
  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center">
      <div className="absolute inset-0 bg-black/50" onClick={onRequestClose} />
      <div role="dialog" aria-modal className="relative z-10 w-[32rem] rounded bg-white p-4 shadow-xl">
        <div className="flex items-center justify-between">
          <h2 className="text-lg font-semibold">{title}</h2>
          <button className="rounded px-2 py-1 hover:bg-slate-100" onClick={onRequestClose} aria-label="Close">
            ✕
          </button>
        </div>
        <div className="mt-3 text-sm text-slate-600">Placeholder content</div>
      </div>
    </div>
  )
}

// Modal components that use standard props (no special context needed)
const ModalComponents: Partial<Record<ModalKey, (p: ModalProps) => React.ReactElement>> = {
  'settings': (p) => <SettingsModal {...p} />,
  'help': (p) => <HelpModal {...p} />,
  'account-delete': (p) => <PlaceholderModal title="Delete Account" {...p} />,
  'session-expired': (p) => <SessionExpiredModal {...p} />,
  'error-schema': (p) => <ErrorSchemaModal {...p} />,
  'error-ai': (p) => <PlaceholderModal title="AI Timeout" {...p} />,
}

export function ModalManager({
  gameId: gameIdParam,
}: {
  gameId: string | number
  status?: 'online' | 'offline' | 'limited'
}) {
  useNavigate() // ensure hook context; not needed directly here
  const { state, closeModal } = useModalParam()
  useBackstackCloseBehavior()

  const [activeGameId, setActiveGameId] = useState<number | null>(null)
  const [hasCheckedActiveGame, setHasCheckedActiveGame] = useState(false)
  
  // City modal context
  const selectedCityId = useUiStore((s) => s.selectedCityId)
  const setSelectedCityId = useUiStore((s) => s.setSelectedCityId)

  // Determine if we're in "new game" context or have an actual game
  const isNewGameContext = gameIdParam === 'new' || gameIdParam === 'undefined'
  const currentGameId = !isNewGameContext && typeof gameIdParam === 'string' 
    ? parseInt(gameIdParam, 10) 
    : typeof gameIdParam === 'number'
    ? gameIdParam
    : undefined

  // Fetch game state for modals that need it (e.g., saves modal)
  const { data: gameState } = useGameState(currentGameId, { enabled: !!currentGameId })

  // Fetch active game when opening start-new modal to determine if user has one
  useEffect(() => {
    if (state.modal !== 'start-new') return
    if (hasCheckedActiveGame) return

    let cancelled = false
    ;(async () => {
      const result = await fetchGames({
        status: 'active',
        sort: 'lastTurnAt',
        order: 'desc',
        pageSize: 1,
      })

      if (cancelled) return
      
      if (result.ok && result.data?.items && result.data.items.length > 0) {
        setActiveGameId(result.data.items[0].id)
      } else {
        setActiveGameId(null)
      }
      setHasCheckedActiveGame(true)
    })()

    return () => {
      cancelled = true
    }
  }, [state.modal, hasCheckedActiveGame])

  const modalKey = useMemo(() => state.modal, [state.modal])
  if (!modalKey) return null

  // For start-new modal, pass active game context
  if (modalKey === 'start-new') {
    const hasActiveGame = activeGameId !== null
    const gameId = activeGameId ?? currentGameId

    return (
      <ModalContainer onRequestClose={() => closeModal('replace')}>
        <StartNewGameModal 
          onRequestClose={() => closeModal('replace')}
          hasActiveGame={hasActiveGame}
          currentGameId={gameId}
        />
      </ModalContainer>
    )
  }

  // For saves modal, pass game-specific context
  if (modalKey === 'saves') {
    if (!currentGameId) {
      // Can't show saves modal without a valid game ID
      return null
    }

    const turnInProgress = gameState?.game?.turnInProgress ?? false
    const initialTab = state.tab === 'autosaves' ? 'autosaves' : 'manual'

    return (
      <SavesModal
        onRequestClose={() => closeModal('replace')}
        gameId={currentGameId}
        turnInProgress={turnInProgress}
        initialTab={initialTab}
      />
    )
  }

  // For city modal, pass game state and city ID
  if (modalKey === 'city') {
    if (!gameState || !selectedCityId) {
      // Can't show city modal without game state and city ID
      return null
    }

    const handleClose = () => {
      setSelectedCityId(undefined)
      closeModal('replace')
    }

    return (
      <ModalContainer onRequestClose={handleClose}>
        <CityModal
          onRequestClose={handleClose}
          gameState={gameState}
          cityId={selectedCityId}
        />
      </ModalContainer>
    )
  }

  // For other modals, use the standard component map
  const Comp = ModalComponents[modalKey]
  if (!Comp) return null
  
  const isBlocking = modalKey === 'session-expired' || modalKey === 'error-schema'
  return (
    <ModalContainer onRequestClose={() => closeModal('replace')} closeOnBackdrop={!isBlocking}>
      <Comp onRequestClose={() => closeModal('replace')} />
    </ModalContainer>
  )
}
