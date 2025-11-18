import { useParams } from 'react-router-dom'
import { useCallback, useState, useEffect } from 'react'
import { useGameState, useUnitDefinitions, useMapTiles } from '../../features/game/useGameQueries'
import { MapCanvasStack } from '../../components/game/MapCanvasStack'
import { BottomPanel } from '../../components/game/BottomPanel'
import { ActionRail } from '../../components/game/ActionRail'
import { EndTurnButton } from '../../components/game/EndTurnButton'
import { AIOverlay } from '../../components/game/AIOverlay'
import { TurnLogPanel } from '../../components/game/TurnLogPanel'
import { ToastsCenter } from '../../components/game/ToastsCenter'
import { Banners } from '../../components/ui/Banners'
import { ModalManager } from '../../components/modals/ModalManager'
import { ResultOverlay } from '../../components/game/ResultOverlay'
import { useGameMapStore } from '../../features/game/useGameMapStore'
import { useGameHotkeys } from '../../features/game/useGameHotkeys'
import { useEndTurn } from '../../features/game/useGameQueries'
import { useUiStore } from '../../components/ui/uiStore'
import './GameMapPage.css'
import { IdleSessionProvider } from '../../providers/IdleSessionProvider'

export function GameMapPage() {
  const { id } = useParams<{ id: string }>()
  const gameId = id ? parseInt(id, 10) : undefined
  const isNewGameContext = id === 'new' || !id
  const [isRateLimited, setIsRateLimited] = useState(false)
  const [isOnline, setIsOnline] = useState(navigator.onLine)

  // Handle online/offline
  useEffect(() => {
    const handleOnline = () => setIsOnline(true)
    const handleOffline = () => setIsOnline(false)
    
    window.addEventListener('online', handleOnline)
    window.addEventListener('offline', handleOffline)
    
    return () => {
      window.removeEventListener('online', handleOnline)
      window.removeEventListener('offline', handleOffline)
    }
  }, [])

  // Fetch game data with polling during AI turns (skip if in new game context)
  // Slow down polling if rate limited
  const { data: gameState, isLoading: isLoadingState, error } = useGameState(
    isNewGameContext ? undefined : gameId, 
    {
      refetchInterval: (state) => {
        if (!state?.game.turnInProgress) return false
        return isRateLimited ? 2500 : 1000
      },
      enabled: !isNewGameContext,
    }
  )

  // Detect rate limiting from query errors
  useEffect(() => {
    if (error && 'status' in error && (error as { status: number }).status === 429) {
      setIsRateLimited(true)
      const timer = setTimeout(() => setIsRateLimited(false), 10000)
      return () => clearTimeout(timer)
    }
  }, [error])
  const { data: unitDefs, isLoading: isLoadingDefs } = useUnitDefinitions()
  const { data: mapTiles, isLoading: isLoadingTiles } = useMapTiles(gameState?.map.code)

  // UI state
  const { camera, selection, gridOn, setCamera, setSelection } = useGameMapStore()
  const sessionLocked = useUiStore((s) => s.sessionLocked)

  // Mutations
  const endTurnMutation = useEndTurn(gameId || 0)

  // Camera controls - defined before early returns to satisfy Rules of Hooks
  const handleZoomIn = useCallback(() => {
    setCamera({ scale: Math.min(camera.scale * 1.2, 3) })
  }, [camera.scale, setCamera])

  const handleZoomOut = useCallback(() => {
    setCamera({ scale: Math.max(camera.scale / 1.2, 0.5) })
  }, [camera.scale, setCamera])

  const handlePan = useCallback(
    (dx: number, dy: number) => {
      setCamera({
        offsetX: camera.offsetX + dx,
        offsetY: camera.offsetY + dy,
      })
    },
    [camera, setCamera]
  )

  // Find next unit - defined before early returns to satisfy Rules of Hooks
  const findNextUnit = useCallback(() => {
    if (!gameState) return
    const playerParticipant = gameState.participants.find((p) => p.kind === 'human')
    if (!playerParticipant) return

    const unactedUnits = gameState.units.filter(
      (u) => u.participantId === playerParticipant.id && !u.hasActed
    )

    if (unactedUnits.length > 0) {
      setSelection({ kind: 'unit', id: unactedUnits[0].id })
    }
  }, [gameState, setSelection])

  // Hotkeys - must be called unconditionally
  const isActionsDisabled = !isOnline || gameState?.game.turnInProgress || sessionLocked
  useGameHotkeys({
    onEndTurn: () => {
      if (!isActionsDisabled && !endTurnMutation.isPending) {
        endTurnMutation.mutate()
      }
    },
    onNextUnit: findNextUnit,
    onZoomIn: handleZoomIn,
    onZoomOut: handleZoomOut,
    onPan: handlePan,
    isEndTurnDisabled: isActionsDisabled || endTurnMutation.isPending,
  })

  // If we're in the new game context, show minimal shell for modal
  if (isNewGameContext) {
    const status = !isOnline ? 'offline' : isRateLimited ? 'limited' : 'online'
    return (
      <IdleSessionProvider>
        <div className="game-map-page relative flex min-h-dvh flex-col">
          <Banners />
          <div className="flex flex-1 items-center justify-center">
            <div className="text-slate-600">Starting new game...</div>
          </div>
          <ModalManager gameId={id || 'new'} status={status} />
        </div>
      </IdleSessionProvider>
    )
  }

  const isLoading = isLoadingState || isLoadingDefs || isLoadingTiles

  if (isLoading) {
    return (
      <div className="flex min-h-dvh items-center justify-center">
        <div className="text-slate-600">Loading game...</div>
      </div>
    )
  }

  if (!gameState || !unitDefs || !mapTiles) {
    return (
      <div className="flex min-h-dvh items-center justify-center">
        <div className="text-slate-600">Game not found</div>
      </div>
    )
  }

  const turnInProgress = gameState.game.turnInProgress
  const status = !isOnline ? 'offline' : isRateLimited ? 'limited' : 'online'
  const isGameFinished = gameState.game.status === 'finished'
  
  // Determine result status - if player is eliminated, it's defeat; otherwise victory
  const playerParticipant = gameState.participants.find((p) => p.kind === 'human')
  const resultStatus = playerParticipant?.isEliminated ? 'defeat' : 'victory'
  
  // Count cities captured by player
  const citiesCaptured = playerParticipant 
    ? gameState.cities.filter((c) => c.participantId === playerParticipant.id).length
    : 0

  return (
    <IdleSessionProvider>
    <div className="game-map-page relative">
      {/* Map fills entire viewport */}
      <main className="game-map-main">
        <MapCanvasStack
          gameState={gameState}
          unitDefs={unitDefs}
          mapTiles={mapTiles}
          camera={camera}
          selection={selection}
          gridOn={gridOn}
        />
      </main>

      {/* UI overlays positioned absolutely */}

      <BottomPanel
        gameState={gameState}
        mapTiles={mapTiles}
        selection={selection}
      />

      <ActionRail />

      <EndTurnButton
        gameId={gameId!}
        gameState={gameState}
        disabled={isActionsDisabled}
        turnNo={gameState.game.turnNo}
        status={gameState.game.status}
      />

      <AIOverlay isVisible={turnInProgress} />

      <TurnLogPanel gameId={gameId!} />

      <ToastsCenter />

      <Banners />

      <ModalManager gameId={gameId!} status={status} />

      {/* Result Overlay - shown when game is finished */}
      {isGameFinished && (
        <ResultOverlay
          status={resultStatus}
          turns={gameState.game.turnNo}
          citiesCaptured={citiesCaptured}
        />
      )}
    </div>
    </IdleSessionProvider>
  )
}

