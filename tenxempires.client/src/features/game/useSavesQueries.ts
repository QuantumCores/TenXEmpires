import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import {
  fetchGameSaves,
  createManualSave,
  deleteManualSave,
  loadSave,
} from '../../api/saves'
import type {
  GameSavesListDto,
  CreateManualSaveCommand,
  LoadSaveResponse,
} from '../../types/saves'
import type { GameStateDto } from '../../types/game'
import { withCsrfRetry } from '../../api/csrf'
import { useGameErrorHandler } from './errorHandling'
import { gameKeys } from './useGameQueries'

// ============================================================================
// Query Keys
// ============================================================================

export const savesKeys = {
  all: ['saves'] as const,
  list: (gameId: number) => ['saves', gameId] as const,
}

// ============================================================================
// Saves List Query
// ============================================================================

export function useSavesQuery(gameId: number) {
  return useQuery({
    queryKey: savesKeys.list(gameId),
    queryFn: async () => {
      const result = await fetchGameSaves(gameId)
      
      if (!result.ok || !result.data) {
        throw new Error(`Failed to fetch saves: ${result.status}`)
      }
      
      return result.data
    },
    staleTime: 60_000, // 60 seconds
    enabled: !!gameId,
  })
}

// ============================================================================
// Mutations
// ============================================================================

function generateIdempotencyKey(): string {
  return `${Date.now()}-${Math.random().toString(36).slice(2, 11)}`
}

interface MutationContext {
  previousSaves?: GameSavesListDto
  previousState?: GameStateDto
}

// ============================================================================
// Save Manual Mutation
// ============================================================================

export function useSaveManualMutation(gameId: number) {
  const queryClient = useQueryClient()
  const { handleError } = useGameErrorHandler()

  return useMutation<
    unknown,
    Error,
    CreateManualSaveCommand,
    MutationContext
  >({
    mutationFn: async (command: CreateManualSaveCommand) => {
      const idempotencyKey = generateIdempotencyKey()
      
      // Wrap with CSRF retry logic
      const result = await withCsrfRetry(
        () => createManualSave(gameId, command, idempotencyKey),
        (res) => res.status === 403 && (res.data as any)?.code === 'CSRF_INVALID'
      )
      
      if (!result.ok || !result.data) {
        handleError(result)
        throw new Error(`Save failed: ${result.status}`)
      }
      
      return result.data
    },
    onMutate: async () => {
      // Cancel outgoing refetches
      await queryClient.cancelQueries({ queryKey: savesKeys.list(gameId) })
      
      // Snapshot previous saves
      const previousSaves = queryClient.getQueryData<GameSavesListDto>(savesKeys.list(gameId))
      
      return { previousSaves }
    },
    onSuccess: () => {
      // Invalidate and refetch saves list
      queryClient.invalidateQueries({ queryKey: savesKeys.list(gameId) })
    },
    onError: (_err, _variables, context) => {
      // Rollback on error
      if (context?.previousSaves) {
        queryClient.setQueryData(savesKeys.list(gameId), context.previousSaves)
      }
    },
  })
}

// ============================================================================
// Delete Manual Mutation
// ============================================================================

export function useDeleteManualMutation(gameId: number) {
  const queryClient = useQueryClient()
  const { handleError } = useGameErrorHandler()

  return useMutation<
    unknown,
    Error,
    number, // slot number
    MutationContext
  >({
    mutationFn: async (slot: number) => {
      // Wrap with CSRF retry logic
      const result = await withCsrfRetry(
        () => deleteManualSave(gameId, slot),
        (res) => res.status === 403 && (res.data as any)?.code === 'CSRF_INVALID'
      )
      
      if (!result.ok) {
        handleError(result)
        throw new Error(`Delete failed: ${result.status}`)
      }
      
      return result.data
    },
    onMutate: async () => {
      await queryClient.cancelQueries({ queryKey: savesKeys.list(gameId) })
      const previousSaves = queryClient.getQueryData<GameSavesListDto>(savesKeys.list(gameId))
      return { previousSaves }
    },
    onSuccess: () => {
      // Invalidate and refetch saves list
      queryClient.invalidateQueries({ queryKey: savesKeys.list(gameId) })
    },
    onError: (_err, _variables, context) => {
      if (context?.previousSaves) {
        queryClient.setQueryData(savesKeys.list(gameId), context.previousSaves)
      }
    },
  })
}

// ============================================================================
// Load Save Mutation
// ============================================================================

export function useLoadSaveMutation(gameId: number) {
  const queryClient = useQueryClient()
  const { handleError } = useGameErrorHandler()

  return useMutation<
    LoadSaveResponse,
    Error,
    number, // save ID
    MutationContext
  >({
    mutationFn: async (saveId: number) => {
      const idempotencyKey = generateIdempotencyKey()
      
      // Wrap with CSRF retry logic
      const result = await withCsrfRetry(
        () => loadSave(saveId, idempotencyKey),
        (res) => res.status === 403 && (res.data as any)?.code === 'CSRF_INVALID'
      )
      
      if (!result.ok || !result.data) {
        handleError(result)
        throw new Error(`Load save failed: ${result.status}`)
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
    onSuccess: (data: LoadSaveResponse) => {
      // Write-through: update game state cache with loaded state
      if (data?.state) {
        queryClient.setQueryData(gameKeys.state(gameId), data.state)
      }
      
      // Also refresh saves list to ensure it's up-to-date
      queryClient.invalidateQueries({ queryKey: savesKeys.list(gameId) })
    },
    onError: (_err, _variables, context) => {
      // Rollback on error
      if (context?.previousState) {
        queryClient.setQueryData(gameKeys.state(gameId), context.previousState)
      }
    },
  })
}

