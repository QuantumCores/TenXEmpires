import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest'
import { renderHook } from '@testing-library/react'

// Mock dependencies
const mockNavigate = vi.fn()
import type { ModalRouteState } from '../../router/query'
const mockModalState: ModalRouteState = { modal: undefined, confirm: undefined, tab: undefined }

vi.mock('react-router-dom', async (importOriginal) => {
  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  const actual = await importOriginal() as any
  return {
    ...actual,
    useNavigate: () => mockNavigate,
  }
})

vi.mock('../../router/query', () => ({
  useModalParam: () => ({ state: mockModalState }),
}))

import { useBackstackCloseBehavior } from '../../router/backstack'

describe('useBackstackCloseBehavior', () => {
  let originalLocation: Location
  let popstateHandlers: ((event: PopStateEvent) => void)[]

  beforeEach(() => {
    // Save original location
    originalLocation = window.location

    // Mock window.location
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    delete (window as any).location
    window.location = {
      href: 'http://localhost:3000/game/123',
      pathname: '/game/123',
      search: '',
      hash: '',
      origin: 'http://localhost:3000',
      // eslint-disable-next-line @typescript-eslint/no-explicit-any
    } as any

    // Track popstate event handlers
    popstateHandlers = []

    // Mock addEventListener to capture handlers
    const originalAddEventListener = window.addEventListener
    vi.spyOn(window, 'addEventListener').mockImplementation((event, handler, options) => {
      if (event === 'popstate') {
        popstateHandlers.push(handler as (event: PopStateEvent) => void)
      } else {
        originalAddEventListener.call(window, event, handler as EventListener, options)
      }
    })

    // Mock removeEventListener
    vi.spyOn(window, 'removeEventListener').mockImplementation((event, handler) => {
      if (event === 'popstate') {
        const index = popstateHandlers.indexOf(handler as (event: PopStateEvent) => void)
        if (index > -1) {
          popstateHandlers.splice(index, 1)
        }
      }
    })

    // Clear mocks
    mockNavigate.mockClear()
    mockModalState.modal = undefined
    mockModalState.confirm = undefined
  })

  afterEach(() => {
    // Restore original location
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    ;(window as any).location = originalLocation
    vi.restoreAllMocks()
    popstateHandlers = []
  })

  describe('event listener setup', () => {
    it('registers popstate event listener on mount', () => {
      renderHook(() => useBackstackCloseBehavior())

      expect(window.addEventListener).toHaveBeenCalledWith('popstate', expect.any(Function))
      expect(popstateHandlers).toHaveLength(1)
    })

    it('removes popstate event listener on unmount', () => {
      const { unmount } = renderHook(() => useBackstackCloseBehavior())

      const addedHandler = popstateHandlers[0]
      unmount()

      expect(window.removeEventListener).toHaveBeenCalledWith('popstate', addedHandler)
    })
  })

  describe('confirm step navigation', () => {
    it('keeps modal open and drops confirm when back is pressed from confirm step', () => {
      // Initial state: modal with confirm
      mockModalState.modal = 'saves'
      mockModalState.confirm = true
      window.location.search = '?modal=saves&confirm=true'

      renderHook(() => useBackstackCloseBehavior())

      // Simulate back navigation - URL changes to modal without confirm
      window.location.search = '?modal=saves'
      Object.defineProperty(window.location, 'href', {
        value: 'http://localhost:3000/game/123?modal=saves',
        writable: true,
      })

      // Trigger popstate
      const handler = popstateHandlers[0]
      handler(new PopStateEvent('popstate'))

      expect(mockNavigate).toHaveBeenCalledWith(
        '/game/123?modal=saves',
        { replace: true }
      )
    })

    it('keeps modal open when confirm is removed during popstate', () => {
      mockModalState.modal = 'settings'
      mockModalState.confirm = true
      window.location.search = '?modal=settings&confirm=true'

      renderHook(() => useBackstackCloseBehavior())

      // URL changes to no modal (back pressed)
      window.location.search = ''
      Object.defineProperty(window.location, 'href', {
        value: 'http://localhost:3000/game/123',
        writable: true,
      })

      const handler = popstateHandlers[0]
      handler(new PopStateEvent('popstate'))

      expect(mockNavigate).toHaveBeenCalledWith(
        '/game/123?modal=settings',
        { replace: true }
      )
    })

    it('keeps modal open when different modal appears during popstate from confirm', () => {
      mockModalState.modal = 'saves'
      mockModalState.confirm = true
      window.location.search = '?modal=saves&confirm=true'

      renderHook(() => useBackstackCloseBehavior())

      // URL changes to different modal
      window.location.search = '?modal=settings'
      Object.defineProperty(window.location, 'href', {
        value: 'http://localhost:3000/game/123?modal=settings',
        writable: true,
      })

      const handler = popstateHandlers[0]
      handler(new PopStateEvent('popstate'))

      // Should restore the original modal without confirm
      expect(mockNavigate).toHaveBeenCalledWith(
        '/game/123?modal=saves',
        { replace: true }
      )
    })
  })

  describe('normal back navigation', () => {
    it('allows normal back behavior when no confirm step', () => {
      mockModalState.modal = 'help'
      mockModalState.confirm = false
      window.location.search = '?modal=help'

      renderHook(() => useBackstackCloseBehavior())

      // Simulate back to no modal
      window.location.search = ''
      Object.defineProperty(window.location, 'href', {
        value: 'http://localhost:3000/game/123',
        writable: true,
      })

      const handler = popstateHandlers[0]
      handler(new PopStateEvent('popstate'))

      // Should not intercept
      expect(mockNavigate).not.toHaveBeenCalled()
    })

    it('allows back from modal to map without interception', () => {
      mockModalState.modal = 'settings'
      mockModalState.confirm = false
      window.location.search = '?modal=settings'

      renderHook(() => useBackstackCloseBehavior())

      // Back to no modal
      window.location.search = ''
      Object.defineProperty(window.location, 'href', {
        value: 'http://localhost:3000/game/123',
        writable: true,
      })

      const handler = popstateHandlers[0]
      handler(new PopStateEvent('popstate'))

      expect(mockNavigate).not.toHaveBeenCalled()
    })

    it('allows back navigation when no modal is open', () => {
      mockModalState.modal = undefined
      mockModalState.confirm = false
      window.location.search = ''

      renderHook(() => useBackstackCloseBehavior())

      const handler = popstateHandlers[0]
      handler(new PopStateEvent('popstate'))

      expect(mockNavigate).not.toHaveBeenCalled()
    })
  })

  describe('modal state tracking', () => {
    it('updates ref when modal state changes', () => {
      mockModalState.modal = 'help'
      mockModalState.confirm = false

      const { rerender } = renderHook(() => useBackstackCloseBehavior())

      // Change state
      mockModalState.modal = 'settings'
      rerender()

      // Previous state should be updated - we can't directly test the ref,
      // but we can verify the hook doesn't crash
      expect(popstateHandlers).toHaveLength(1)
    })

    it('updates ref when confirm state changes', () => {
      mockModalState.modal = 'saves'
      mockModalState.confirm = false

      const { rerender } = renderHook(() => useBackstackCloseBehavior())

      mockModalState.confirm = true
      rerender()

      expect(popstateHandlers).toHaveLength(1)
    })
  })

  describe('URL parsing', () => {
    it('handles URLs with only modal parameter', () => {
      mockModalState.modal = 'help'
      mockModalState.confirm = false
      window.location.search = '?modal=help'
      Object.defineProperty(window.location, 'href', {
        value: 'http://localhost:3000/game/123?modal=help',
        writable: true,
      })

      renderHook(() => useBackstackCloseBehavior())

      const handler = popstateHandlers[0]
      
      // Back to no modal
      window.location.search = ''
      Object.defineProperty(window.location, 'href', {
        value: 'http://localhost:3000/game/123',
        writable: true,
      })

      handler(new PopStateEvent('popstate'))

      expect(mockNavigate).not.toHaveBeenCalled()
    })

    it('handles URLs with multiple query parameters', () => {
      mockModalState.modal = 'saves'
      mockModalState.confirm = true
      window.location.search = '?modal=saves&confirm=true&tab=local'
      Object.defineProperty(window.location, 'href', {
        value: 'http://localhost:3000/game/123?modal=saves&confirm=true&tab=local',
        writable: true,
      })

      renderHook(() => useBackstackCloseBehavior())

      // Back removes confirm
      window.location.search = '?modal=saves&tab=local'
      Object.defineProperty(window.location, 'href', {
        value: 'http://localhost:3000/game/123?modal=saves&tab=local',
        writable: true,
      })

      const handler = popstateHandlers[0]
      handler(new PopStateEvent('popstate'))

      expect(mockNavigate).toHaveBeenCalledWith(
        expect.stringContaining('modal=saves'),
        { replace: true }
      )
    })

    it('handles invalid modal keys gracefully', () => {
      mockModalState.modal = undefined
      mockModalState.confirm = false
      window.location.search = '?modal=invalid-modal'
      Object.defineProperty(window.location, 'href', {
        value: 'http://localhost:3000/game/123?modal=invalid-modal',
        writable: true,
      })

      renderHook(() => useBackstackCloseBehavior())

      const handler = popstateHandlers[0]
      handler(new PopStateEvent('popstate'))

      expect(mockNavigate).not.toHaveBeenCalled()
    })
  })

  describe('edge cases', () => {
    it('handles rapid popstate events', () => {
      mockModalState.modal = 'saves'
      mockModalState.confirm = true
      window.location.search = '?modal=saves&confirm=true'

      renderHook(() => useBackstackCloseBehavior())

      const handler = popstateHandlers[0]

      // First popstate
      window.location.search = '?modal=saves'
      Object.defineProperty(window.location, 'href', {
        value: 'http://localhost:3000/game/123?modal=saves',
        writable: true,
      })
      handler(new PopStateEvent('popstate'))

      // Second popstate immediately after
      window.location.search = ''
      Object.defineProperty(window.location, 'href', {
        value: 'http://localhost:3000/game/123',
        writable: true,
      })
      handler(new PopStateEvent('popstate'))

      expect(mockNavigate).toHaveBeenCalled()
    })

    it('handles unmount during popstate handling', () => {
      mockModalState.modal = 'help'
      window.location.search = '?modal=help'

      const { unmount } = renderHook(() => useBackstackCloseBehavior())

      unmount()

      // Should have cleaned up listener
      expect(window.removeEventListener).toHaveBeenCalledWith('popstate', expect.any(Function))
      expect(popstateHandlers).toHaveLength(0)
    })

    it('handles session-expired modal', () => {
      mockModalState.modal = 'session-expired'
      mockModalState.confirm = false
      window.location.search = '?modal=session-expired'

      renderHook(() => useBackstackCloseBehavior())

      window.location.search = ''
      Object.defineProperty(window.location, 'href', {
        value: 'http://localhost:3000/game/123',
        writable: true,
      })

      const handler = popstateHandlers[0]
      handler(new PopStateEvent('popstate'))

      expect(mockNavigate).not.toHaveBeenCalled()
    })

    it('handles error-schema modal', () => {
      mockModalState.modal = 'error-schema'
      mockModalState.confirm = false
      window.location.search = '?modal=error-schema'

      renderHook(() => useBackstackCloseBehavior())

      window.location.search = ''
      Object.defineProperty(window.location, 'href', {
        value: 'http://localhost:3000/game/123',
        writable: true,
      })

      const handler = popstateHandlers[0]
      handler(new PopStateEvent('popstate'))

      expect(mockNavigate).not.toHaveBeenCalled()
    })
  })

  describe('pathname preservation', () => {
    it('preserves pathname when navigating from confirm', () => {
      mockModalState.modal = 'saves'
      mockModalState.confirm = true
      window.location.pathname = '/game/abc123'
      window.location.search = '?modal=saves&confirm=true'
      Object.defineProperty(window.location, 'href', {
        value: 'http://localhost:3000/game/abc123?modal=saves&confirm=true',
        writable: true,
      })

      renderHook(() => useBackstackCloseBehavior())

      window.location.search = '?modal=saves'
      Object.defineProperty(window.location, 'href', {
        value: 'http://localhost:3000/game/abc123?modal=saves',
        writable: true,
      })

      const handler = popstateHandlers[0]
      handler(new PopStateEvent('popstate'))

      expect(mockNavigate).toHaveBeenCalledWith(
        expect.stringContaining('/game/abc123'),
        { replace: true }
      )
    })
  })
})

