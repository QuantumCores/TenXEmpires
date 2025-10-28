import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import {
  fetchGameState,
  fetchUnitDefinitions,
  fetchMapTiles,
  moveUnit,
  attackUnit,
  endTurn,
} from '../../api/games'
import type {
  MoveUnitCommand,
  AttackUnitCommand,
  GameStateDto,
} from '../../types/game'
import { useRef } from 'react'
import { withCsrfRetry } from '../../api/csrf'
import { useGameErrorHandler } from './errorHandling'

// ============================================================================
// Query Keys
// ============================================================================

export const gameKeys = {
  all: ['games'] as const,
  state: (gameId: number) => ['game', gameId] as const,
  unitDefs: () => ['unit-defs'] as const,
  mapTiles: (mapCode: string) => ['map-tiles', mapCode] as const,
}

// ============================================================================
// Game State Query
// ============================================================================

interface UseGameStateOptions {
  enabled?: boolean
  refetchInterval?: number | false | ((data: GameStateDto | undefined) => number | false)
}

export function useGameState(gameId: number | undefined, options: UseGameStateOptions = {}) {
  const etagRef = useRef<string | undefined>(undefined)

  return useQuery({
    queryKey: gameId ? gameKeys.state(gameId) : ['game', 'none'],
    queryFn: async () => {
      if (!gameId) throw new Error('Game ID is required')
      
      const result = await fetchGameState(gameId, etagRef.current)
      
      // Handle 304 Not Modified
      if (result.status === 304) {
        return null // Query will keep previous data
      }
      
      // Store ETag for next request
      // Note: In a real implementation, you'd extract this from response headers
      // For now, we'll skip ETag caching on the client side
      
      if (!result.ok) {
        throw new Error(`Failed to fetch game state: ${result.status}`)
      }
      
      return result.data
    },
    staleTime: 0, // Always fresh
    enabled: !!gameId && (options.enabled !== false),
    refetchInterval: typeof options.refetchInterval === 'function'
      ? (query) => {
          const fn = options.refetchInterval as (data: GameStateDto | undefined) => number | false
          return fn(query.state.data as GameStateDto | undefined)
        }
      : options.refetchInterval,
  })
}

// ============================================================================
// Unit Definitions Query
// ============================================================================

export function useUnitDefinitions() {
  return useQuery({
    queryKey: gameKeys.unitDefs(),
    queryFn: async () => {
      const result = await fetchUnitDefinitions()
      if (!result.ok || !result.data) {
        throw new Error(`Failed to fetch unit definitions: ${result.status}`)
      }
      return result.data.items
    },
    staleTime: Infinity, // Never refetch
  })
}

// ============================================================================
// Map Tiles Query
// ============================================================================

export function useMapTiles(mapCode: string | undefined) {
  return useQuery({
    queryKey: mapCode ? gameKeys.mapTiles(mapCode) : ['map-tiles', 'none'],
    queryFn: async () => {
      if (!mapCode) throw new Error('Map code is required')
      
      const result = await fetchMapTiles(mapCode)
      if (!result.ok || !result.data) {
        throw new Error(`Failed to fetch map tiles: ${result.status}`)
      }
      return result.data.items
    },
    staleTime: Infinity, // Never refetch
    enabled: !!mapCode,
  })
}

// ============================================================================
// Action Mutations
// ============================================================================

function generateIdempotencyKey(): string {
  return `${Date.now()}-${Math.random().toString(36).slice(2, 11)}`
}

interface MutationContext {
  previousState?: GameStateDto
}

export function useMoveUnit(gameId: number) {
  const queryClient = useQueryClient()
  const { handleError } = useGameErrorHandler()

  return useMutation<
    unknown,
    Error,
    MoveUnitCommand,
    MutationContext
  >({
    mutationFn: async (command: MoveUnitCommand) => {
      const idempotencyKey = generateIdempotencyKey()
      
      // Wrap with CSRF retry logic
      const result = await withCsrfRetry(
        () => moveUnit(gameId, command, idempotencyKey),
        (res) => res.status === 403 && (res.data as any)?.code === 'CSRF_INVALID'
      )
      
      if (!result.ok || !result.data) {
        handleError(result)
        throw new Error(`Move failed: ${result.status}`)
      }
      
      return result.data
    },
    onMutate: async () => {
      // Cancel outgoing refetches
      await queryClient.cancelQueries({ queryKey: gameKeys.state(gameId) })
      
      // Snapshot previous state
      const previousState = queryClient.getQueryData<GameStateDto>(gameKeys.state(gameId))
      
      return { previousState }
    },
    onSuccess: (data: any) => {
      // Write-through: update cache with new state
      if (data?.state) {
        queryClient.setQueryData(gameKeys.state(gameId), data.state)
      }
    },
    onError: (_err, _variables, context) => {
      // Rollback on error
      if (context?.previousState) {
        queryClient.setQueryData(gameKeys.state(gameId), context.previousState)
      }
    },
  })
}

export function useAttackUnit(gameId: number) {
  const queryClient = useQueryClient()
  const { handleError } = useGameErrorHandler()

  return useMutation<
    unknown,
    Error,
    AttackUnitCommand,
    MutationContext
  >({
    mutationFn: async (command: AttackUnitCommand) => {
      const idempotencyKey = generateIdempotencyKey()
      
      // Wrap with CSRF retry logic
      const result = await withCsrfRetry(
        () => attackUnit(gameId, command, idempotencyKey),
        (res) => res.status === 403 && (res.data as any)?.code === 'CSRF_INVALID'
      )
      
      if (!result.ok || !result.data) {
        handleError(result)
        throw new Error(`Attack failed: ${result.status}`)
      }
      
      return result.data
    },
    onMutate: async () => {
      await queryClient.cancelQueries({ queryKey: gameKeys.state(gameId) })
      const previousState = queryClient.getQueryData<GameStateDto>(gameKeys.state(gameId))
      return { previousState }
    },
    onSuccess: (data: any) => {
      if (data?.state) {
        queryClient.setQueryData(gameKeys.state(gameId), data.state)
      }
    },
    onError: (_err, _variables, context) => {
      if (context?.previousState) {
        queryClient.setQueryData(gameKeys.state(gameId), context.previousState)
      }
    },
  })
}

export function useEndTurn(gameId: number) {
  const queryClient = useQueryClient()
  const { handleError } = useGameErrorHandler()

  return useMutation<
    unknown,
    Error,
    void,
    MutationContext
  >({
    mutationFn: async () => {
      const idempotencyKey = generateIdempotencyKey()
      
      // Wrap with CSRF retry logic
      const result = await withCsrfRetry(
        () => endTurn(gameId, idempotencyKey),
        (res) => res.status === 403 && (res.data as any)?.code === 'CSRF_INVALID'
      )
      
      if (!result.ok || !result.data) {
        handleError(result)
        throw new Error(`End turn failed: ${result.status}`)
      }
      
      return result.data
    },
    onMutate: async () => {
      await queryClient.cancelQueries({ queryKey: gameKeys.state(gameId) })
      const previousState = queryClient.getQueryData<GameStateDto>(gameKeys.state(gameId))
      return { previousState }
    },
    onSuccess: (data: any) => {
      if (data?.state) {
        queryClient.setQueryData(gameKeys.state(gameId), data.state)
      }
      // Note: turnSummary and autosaveId are also available in data
    },
    onError: (_err, _variables, context) => {
      if (context?.previousState) {
        queryClient.setQueryData(gameKeys.state(gameId), context.previousState)
      }
    },
  })
}

