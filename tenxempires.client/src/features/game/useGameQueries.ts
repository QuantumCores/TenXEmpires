import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import {
  fetchGameState,
  fetchUnitDefinitions,
  fetchMapTiles,
  moveUnit,
  attackUnit,
  attackCity,
  spawnUnit,
  expandTerritory,
  endTurn,
} from '../../api/games'
import type {
  MoveUnitCommand,
  AttackUnitCommand,
  AttackCityCommand,
  SpawnUnitCommand,
  ExpandTerritoryCommand,
  GameStateDto,
  ActionStateResponse,
  EndTurnResponse,
} from '../../types/game'
import { useRef } from 'react'
import { useModalParam } from '../../router/query'
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
  const { state, openModal } = useModalParam()

  return useQuery({
    queryKey: gameId ? gameKeys.state(gameId) : ['game', 'none'],
    queryFn: async () => {
      if (!gameId) throw new Error('Game ID is required')
      
      const result = await fetchGameState(gameId, etagRef.current)
      
      // Handle 304 Not Modified
      if (result.status === 304) {
        return null // Query will keep previous data
      }
      
      // Unauthorized: open session expired modal
      if (!result.ok && (result.status === 401 || result.status === 403)) {
        if (state.modal !== 'session-expired') {
          openModal('session-expired', undefined, 'replace')
        }
        throw new Error(`Unauthorized: ${result.status}`)
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
    staleTime: 45_000,
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
    ActionStateResponse,
    Error,
    MoveUnitCommand,
    MutationContext
  >({
    mutationFn: async (command: MoveUnitCommand) => {
      const idempotencyKey = generateIdempotencyKey()
      
      const result = await moveUnit(gameId, command, idempotencyKey)
      
      if (!result.ok || !result.data) {
        handleError(result)
        throw new Error(`Move failed: ${result.status}`)
      }
      
      return result.data as ActionStateResponse
    },
    onMutate: async () => {
      // Cancel outgoing refetches
      await queryClient.cancelQueries({ queryKey: gameKeys.state(gameId) })
      
      // Snapshot previous state
      const previousState = queryClient.getQueryData<GameStateDto>(gameKeys.state(gameId))
      
      return { previousState }
    },
    onSuccess: (data: ActionStateResponse) => {
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
    ActionStateResponse,
    Error,
    AttackUnitCommand,
    MutationContext
  >({
    mutationFn: async (command: AttackUnitCommand) => {
      const idempotencyKey = generateIdempotencyKey()
      
      const result = await attackUnit(gameId, command, idempotencyKey)
      
      if (!result.ok || !result.data) {
        handleError(result)
        throw new Error(`Attack failed: ${result.status}`)
      }
      
      return result.data as ActionStateResponse
    },
    onMutate: async () => {
      await queryClient.cancelQueries({ queryKey: gameKeys.state(gameId) })
      const previousState = queryClient.getQueryData<GameStateDto>(gameKeys.state(gameId))
      return { previousState }
    },
    onSuccess: (data: ActionStateResponse) => {
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

export function useAttackCity(gameId: number) {
  const queryClient = useQueryClient()
  const { handleError } = useGameErrorHandler()

  return useMutation<
    ActionStateResponse,
    Error,
    AttackCityCommand,
    MutationContext
  >({
    mutationFn: async (command: AttackCityCommand) => {
      const idempotencyKey = generateIdempotencyKey()
      
      const result = await attackCity(gameId, command, idempotencyKey)
      
      if (!result.ok || !result.data) {
        handleError(result)
        throw new Error(`City attack failed: ${result.status}`)
      }
      
      return result.data as ActionStateResponse
    },
    onMutate: async () => {
      await queryClient.cancelQueries({ queryKey: gameKeys.state(gameId) })
      const previousState = queryClient.getQueryData<GameStateDto>(gameKeys.state(gameId))
      return { previousState }
    },
    onSuccess: (data: ActionStateResponse) => {
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

export function useSpawnUnit(gameId: number) {
  const queryClient = useQueryClient()
  const { handleError } = useGameErrorHandler()

  return useMutation<
    ActionStateResponse,
    Error,
    SpawnUnitCommand,
    MutationContext
  >({
    mutationFn: async (command: SpawnUnitCommand) => {
      const idempotencyKey = generateIdempotencyKey()

      const result = await spawnUnit(gameId, command, idempotencyKey)

      if (!result.ok || !result.data) {
        handleError(result)
        throw new Error(`Spawn failed: ${result.status}`)
      }

      return result.data as ActionStateResponse
    },
    onMutate: async () => {
      await queryClient.cancelQueries({ queryKey: gameKeys.state(gameId) })
      const previousState = queryClient.getQueryData<GameStateDto>(gameKeys.state(gameId))
      return { previousState }
    },
    onSuccess: (data: ActionStateResponse) => {
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

export function useExpandTerritory(gameId: number) {
  const queryClient = useQueryClient()
  const { handleError } = useGameErrorHandler()

  return useMutation<
    ActionStateResponse,
    Error,
    ExpandTerritoryCommand,
    MutationContext
  >({
    mutationFn: async (command: ExpandTerritoryCommand) => {
      const idempotencyKey = generateIdempotencyKey()

      const result = await expandTerritory(gameId, command, idempotencyKey)

      if (!result.ok || !result.data) {
        handleError(result)
        throw new Error(`Expand territory failed: ${result.status}`)
      }

      return result.data as ActionStateResponse
    },
    onMutate: async () => {
      await queryClient.cancelQueries({ queryKey: gameKeys.state(gameId) })
      const previousState = queryClient.getQueryData<GameStateDto>(gameKeys.state(gameId))
      return { previousState }
    },
    onSuccess: (data: ActionStateResponse) => {
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
    EndTurnResponse,
    Error,
    void,
    MutationContext
  >({
    mutationFn: async () => {
      const idempotencyKey = generateIdempotencyKey()
      
      const result = await endTurn(gameId, idempotencyKey)
      
      if (!result.ok || !result.data) {
        handleError(result)
        throw new Error(`End turn failed: ${result.status}`)
      }
      
      return result.data as EndTurnResponse
    },
    onMutate: async () => {
      await queryClient.cancelQueries({ queryKey: gameKeys.state(gameId) })
      const previousState = queryClient.getQueryData<GameStateDto>(gameKeys.state(gameId))
      return { previousState }
    },
    onSuccess: (data: EndTurnResponse) => {
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

