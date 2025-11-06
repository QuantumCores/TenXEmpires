import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest'
import { renderHook, waitFor } from '@testing-library/react'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import React, { ReactNode } from 'react'
import { useAttackUnit, useAttackCity } from '../../../features/game/useGameQueries'
import type { AttackUnitCommand, AttackCityCommand, ActionStateResponse, GameStateDto } from '../../../types/game'

// Mock the API functions
vi.mock('../../../api/games', () => ({
  attackUnit: vi.fn(),
  attackCity: vi.fn(),
}))

// Mock the CSRF retry logic
vi.mock('../../../api/csrf', () => ({
  withCsrfRetry: vi.fn((fn) => fn()),
}))

// Mock the error handler
const mockHandleError = vi.fn()
vi.mock('../../../features/game/errorHandling', () => ({
  useGameErrorHandler: () => ({
    handleError: mockHandleError,
  }),
}))

// Import mocked modules after mocking
import { attackUnit, attackCity } from '../../../api/games'
import { withCsrfRetry } from '../../../api/csrf'

// Note: useGameErrorHandler is mocked above, so we don't import it here

// Mock useModalParam hook (used by useGameErrorHandler)
vi.mock('../../../router/query', () => ({
  useModalParam: () => ({
    state: { modal: null },
    openModal: vi.fn(),
  }),
}))

// Helper to create a QueryClient wrapper
function createWrapper() {
  const queryClient = new QueryClient({
    defaultOptions: {
      queries: { retry: false },
      mutations: { retry: false },
    },
  })

  return ({ children }: { children: ReactNode }) =>
    React.createElement(QueryClientProvider, { client: queryClient }, children)
}

describe('useAttackUnit', () => {
  const gameId = 42
  const mockCommand: AttackUnitCommand = {
    attackerUnitId: 101,
    targetUnitId: 201,
  }

  const mockResponse: ActionStateResponse = {
    state: {
      game: {
        id: gameId,
        turnNo: 1,
        activeParticipantId: 1,
        turnInProgress: false,
        status: 'active',
      },
      map: {
        id: 1,
        code: 'test-map',
        schemaVersion: 1,
        width: 10,
        height: 10,
      },
      participants: [],
      units: [],
      cities: [],
      cityTiles: [],
      cityResources: [],
      unitDefinitions: [],
      turnSummary: null,
    },
  }

  beforeEach(() => {
    vi.clearAllMocks()
    mockHandleError.mockClear()
    // Default mock implementation
    vi.mocked(attackUnit).mockResolvedValue({
      ok: true,
      status: 200,
      data: mockResponse,
    } as any)
    vi.mocked(withCsrfRetry).mockImplementation((fn) => fn())
  })

  afterEach(() => {
    vi.clearAllMocks()
  })

  it('calls attackUnit API with correct parameters', async () => {
    const { result } = renderHook(() => useAttackUnit(gameId), {
      wrapper: createWrapper(),
    })

    await result.current.mutateAsync(mockCommand)

    expect(attackUnit).toHaveBeenCalledTimes(1)
    expect(attackUnit).toHaveBeenCalledWith(
      gameId,
      mockCommand,
      expect.any(String) // idempotency key
    )
  })

  it('generates unique idempotency keys for each call', async () => {
    const { result } = renderHook(() => useAttackUnit(gameId), {
      wrapper: createWrapper(),
    })

    await result.current.mutateAsync(mockCommand)
    const firstCall = vi.mocked(attackUnit).mock.calls[0][2]

    vi.mocked(attackUnit).mockClear()
    await result.current.mutateAsync(mockCommand)
    const secondCall = vi.mocked(attackUnit).mock.calls[0][2]

    expect(firstCall).not.toBe(secondCall)
    expect(typeof firstCall).toBe('string')
    expect(typeof secondCall).toBe('string')
  })

  it('wraps API call with CSRF retry logic', async () => {
    const { result } = renderHook(() => useAttackUnit(gameId), {
      wrapper: createWrapper(),
    })

    await result.current.mutateAsync(mockCommand)

    expect(withCsrfRetry).toHaveBeenCalledTimes(1)
    expect(withCsrfRetry).toHaveBeenCalledWith(
      expect.any(Function),
      expect.any(Function)
    )
  })

  it('returns ActionStateResponse on success', async () => {
    const { result } = renderHook(() => useAttackUnit(gameId), {
      wrapper: createWrapper(),
    })

    const response = await result.current.mutateAsync(mockCommand)

    expect(response).toEqual(mockResponse)
  })

  it('updates query cache with new game state on success', async () => {
    const queryClient = new QueryClient({
      defaultOptions: {
        queries: { retry: false },
        mutations: { retry: false },
      },
    })

    const Wrapper = ({ children }: { children: ReactNode }) =>
      React.createElement(QueryClientProvider, { client: queryClient }, children)

    const { result } = renderHook(() => useAttackUnit(gameId), {
      wrapper: Wrapper,
    })

    await result.current.mutateAsync(mockCommand)

    // Wait for the mutation to complete and cache to update
    await waitFor(() => {
      const cachedState = queryClient.getQueryData(['game', gameId])
      expect(cachedState).toEqual(mockResponse.state)
    })
  })

  it('handles API errors and calls error handler', async () => {
    const mockErrorResponse = {
      ok: false,
      status: 422,
      data: { code: 'OUT_OF_RANGE', message: 'Target out of range' },
    }

    vi.mocked(attackUnit).mockResolvedValue(mockErrorResponse as any)
    mockHandleError.mockClear()

    const { result } = renderHook(() => useAttackUnit(gameId), {
      wrapper: createWrapper(),
    })

    await expect(result.current.mutateAsync(mockCommand)).rejects.toThrow()

    expect(mockHandleError).toHaveBeenCalledWith(mockErrorResponse)
  })

  it('rolls back optimistic update on error', async () => {
    const wrapper = createWrapper()
    const queryClient = new QueryClient({
      defaultOptions: {
        queries: { retry: false },
        mutations: { retry: false },
      },
    })

    const previousState: GameStateDto = {
      game: {
        id: gameId,
        turnNo: 1,
        activeParticipantId: 1,
        turnInProgress: false,
        status: 'active',
      },
      map: {
        id: 1,
        code: 'test-map',
        schemaVersion: 1,
        width: 10,
        height: 10,
      },
      participants: [],
      units: [],
      cities: [],
      cityTiles: [],
      cityResources: [],
      unitDefinitions: [],
      turnSummary: null,
    }

    // Set initial state
    queryClient.setQueryData(['game', gameId], previousState)

    const Wrapper = ({ children }: { children: ReactNode }) =>
      React.createElement(QueryClientProvider, { client: queryClient }, children)

    const mockErrorResponse = {
      ok: false,
      status: 422,
      data: { code: 'OUT_OF_RANGE', message: 'Target out of range' },
    }

    vi.mocked(attackUnit).mockResolvedValue(mockErrorResponse as any)

    const { result } = renderHook(() => useAttackUnit(gameId), {
      wrapper: Wrapper,
    })

    try {
      await result.current.mutateAsync(mockCommand)
    } catch {
      // Expected to throw
    }

    // Wait for error handling to complete
    await waitFor(() => {
      const cachedState = queryClient.getQueryData(['game', gameId])
      expect(cachedState).toEqual(previousState)
    })
  })

  it('handles network errors', async () => {
    const networkError = new Error('Network error')
    vi.mocked(attackUnit).mockRejectedValue(networkError)

    const { result } = renderHook(() => useAttackUnit(gameId), {
      wrapper: createWrapper(),
    })

    await expect(result.current.mutateAsync(mockCommand)).rejects.toThrow('Network error')
  })

  it('cancels in-flight queries on mutate', async () => {
    const queryClient = new QueryClient({
      defaultOptions: {
        queries: { retry: false },
        mutations: { retry: false },
      },
    })

    const cancelQueriesSpy = vi.spyOn(queryClient, 'cancelQueries')

    const Wrapper = ({ children }: { children: ReactNode }) =>
      React.createElement(QueryClientProvider, { client: queryClient }, children)

    const { result } = renderHook(() => useAttackUnit(gameId), {
      wrapper: Wrapper,
    })

    await result.current.mutateAsync(mockCommand)

    expect(cancelQueriesSpy).toHaveBeenCalledWith({
      queryKey: ['game', gameId],
    })
  })
})

describe('useAttackCity', () => {
  const gameId = 42
  const mockCommand: AttackCityCommand = {
    attackerUnitId: 101,
    targetCityId: 501,
  }

  const mockResponse: ActionStateResponse = {
    state: {
      game: {
        id: gameId,
        turnNo: 1,
        activeParticipantId: 1,
        turnInProgress: false,
        status: 'active',
      },
      map: {
        id: 1,
        code: 'test-map',
        schemaVersion: 1,
        width: 10,
        height: 10,
      },
      participants: [],
      units: [],
      cities: [],
      cityTiles: [],
      cityResources: [],
      unitDefinitions: [],
      turnSummary: null,
    },
  }

  beforeEach(() => {
    vi.clearAllMocks()
    mockHandleError.mockClear()
    // Default mock implementation
    vi.mocked(attackCity).mockResolvedValue({
      ok: true,
      status: 200,
      data: mockResponse,
    } as any)
    vi.mocked(withCsrfRetry).mockImplementation((fn) => fn())
  })

  afterEach(() => {
    vi.clearAllMocks()
  })

  it('calls attackCity API with correct parameters', async () => {
    const { result } = renderHook(() => useAttackCity(gameId), {
      wrapper: createWrapper(),
    })

    await result.current.mutateAsync(mockCommand)

    expect(attackCity).toHaveBeenCalledTimes(1)
    expect(attackCity).toHaveBeenCalledWith(
      gameId,
      mockCommand,
      expect.any(String) // idempotency key
    )
  })

  it('generates unique idempotency keys for each call', async () => {
    const { result } = renderHook(() => useAttackCity(gameId), {
      wrapper: createWrapper(),
    })

    await result.current.mutateAsync(mockCommand)
    const firstCall = vi.mocked(attackCity).mock.calls[0][2]

    vi.mocked(attackCity).mockClear()
    await result.current.mutateAsync(mockCommand)
    const secondCall = vi.mocked(attackCity).mock.calls[0][2]

    expect(firstCall).not.toBe(secondCall)
    expect(typeof firstCall).toBe('string')
    expect(typeof secondCall).toBe('string')
  })

  it('wraps API call with CSRF retry logic', async () => {
    const { result } = renderHook(() => useAttackCity(gameId), {
      wrapper: createWrapper(),
    })

    await result.current.mutateAsync(mockCommand)

    expect(withCsrfRetry).toHaveBeenCalledTimes(1)
    expect(withCsrfRetry).toHaveBeenCalledWith(
      expect.any(Function),
      expect.any(Function)
    )
  })

  it('returns ActionStateResponse on success', async () => {
    const { result } = renderHook(() => useAttackCity(gameId), {
      wrapper: createWrapper(),
    })

    const response = await result.current.mutateAsync(mockCommand)

    expect(response).toEqual(mockResponse)
  })

  it('updates query cache with new game state on success', async () => {
    const queryClient = new QueryClient({
      defaultOptions: {
        queries: { retry: false },
        mutations: { retry: false },
      },
    })

    const Wrapper = ({ children }: { children: ReactNode }) =>
      React.createElement(QueryClientProvider, { client: queryClient }, children)

    const { result } = renderHook(() => useAttackCity(gameId), {
      wrapper: Wrapper,
    })

    await result.current.mutateAsync(mockCommand)

    // Wait for the mutation to complete and cache to update
    await waitFor(() => {
      const cachedState = queryClient.getQueryData(['game', gameId])
      expect(cachedState).toEqual(mockResponse.state)
    })
  })

  it('handles API errors and calls error handler', async () => {
    const mockErrorResponse = {
      ok: false,
      status: 422,
      data: { code: 'OUT_OF_RANGE', message: 'City out of range' },
    }

    vi.mocked(attackCity).mockResolvedValue(mockErrorResponse as any)
    mockHandleError.mockClear()

    const { result } = renderHook(() => useAttackCity(gameId), {
      wrapper: createWrapper(),
    })

    await expect(result.current.mutateAsync(mockCommand)).rejects.toThrow()

    expect(mockHandleError).toHaveBeenCalledWith(mockErrorResponse)
  })

  it('rolls back optimistic update on error', async () => {
    const wrapper = createWrapper()
    const queryClient = new QueryClient({
      defaultOptions: {
        queries: { retry: false },
        mutations: { retry: false },
      },
    })

    const previousState: GameStateDto = {
      game: {
        id: gameId,
        turnNo: 1,
        activeParticipantId: 1,
        turnInProgress: false,
        status: 'active',
      },
      map: {
        id: 1,
        code: 'test-map',
        schemaVersion: 1,
        width: 10,
        height: 10,
      },
      participants: [],
      units: [],
      cities: [],
      cityTiles: [],
      cityResources: [],
      unitDefinitions: [],
      turnSummary: null,
    }

    // Set initial state
    queryClient.setQueryData(['game', gameId], previousState)

    const Wrapper = ({ children }: { children: ReactNode }) =>
      React.createElement(QueryClientProvider, { client: queryClient }, children)

    const mockErrorResponse = {
      ok: false,
      status: 422,
      data: { code: 'OUT_OF_RANGE', message: 'City out of range' },
    }

    vi.mocked(attackCity).mockResolvedValue(mockErrorResponse as any)

    const { result } = renderHook(() => useAttackCity(gameId), {
      wrapper: Wrapper,
    })

    try {
      await result.current.mutateAsync(mockCommand)
    } catch {
      // Expected to throw
    }

    // Wait for error handling to complete
    await waitFor(() => {
      const cachedState = queryClient.getQueryData(['game', gameId])
      expect(cachedState).toEqual(previousState)
    })
  })

  it('handles network errors', async () => {
    const networkError = new Error('Network error')
    vi.mocked(attackCity).mockRejectedValue(networkError)

    const { result } = renderHook(() => useAttackCity(gameId), {
      wrapper: createWrapper(),
    })

    await expect(result.current.mutateAsync(mockCommand)).rejects.toThrow('Network error')
  })

  it('cancels in-flight queries on mutate', async () => {
    const queryClient = new QueryClient({
      defaultOptions: {
        queries: { retry: false },
        mutations: { retry: false },
      },
    })

    const cancelQueriesSpy = vi.spyOn(queryClient, 'cancelQueries')

    const Wrapper = ({ children }: { children: ReactNode }) =>
      React.createElement(QueryClientProvider, { client: queryClient }, children)

    const { result } = renderHook(() => useAttackCity(gameId), {
      wrapper: Wrapper,
    })

    await result.current.mutateAsync(mockCommand)

    expect(cancelQueriesSpy).toHaveBeenCalledWith({
      queryKey: ['game', gameId],
    })
  })

  it('handles missing response data gracefully', async () => {
    vi.mocked(attackCity).mockResolvedValue({
      ok: true,
      status: 200,
      data: null,
    } as any)

    const { result } = renderHook(() => useAttackCity(gameId), {
      wrapper: createWrapper(),
    })

    await expect(result.current.mutateAsync(mockCommand)).rejects.toThrow()
  })
})

