import { describe, it, expect, beforeEach, afterEach, vi } from 'vitest'
import { useNotifications } from '../../../components/ui/notifications'

describe('useNotifications', () => {
  beforeEach(() => {
    // Reset store state before each test
    useNotifications.setState({ banners: [] })
    // Use fake timers for TTL testing
    vi.useFakeTimers()
  })

  afterEach(() => {
    vi.restoreAllMocks()
    vi.useRealTimers()
  })

  describe('initial state', () => {
    it('starts with empty banners array', () => {
      const state = useNotifications.getState()
      expect(state.banners).toEqual([])
    })
  })

  describe('add', () => {
    describe('ID generation', () => {
      it('generates unique ID when not provided', () => {
        const id1 = useNotifications.getState().add({
          kind: 'info',
          message: 'Test 1',
        })
        const id2 = useNotifications.getState().add({
          kind: 'info',
          message: 'Test 2',
        })
        
        expect(id1).toBeTruthy()
        expect(id2).toBeTruthy()
        expect(id1).not.toBe(id2)
      })

      it('uses provided ID when given', () => {
        const id = useNotifications.getState().add({
          id: 'custom-id',
          kind: 'info',
          message: 'Test',
        })
        
        expect(id).toBe('custom-id')
        const state = useNotifications.getState()
        expect(state.banners[0].id).toBe('custom-id')
      })

      it('returns the ID of the added banner', () => {
        const id = useNotifications.getState().add({
          kind: 'warning',
          message: 'Warning message',
        })
        
        const state = useNotifications.getState()
        expect(state.banners[0].id).toBe(id)
      })
    })

    describe('adding banners', () => {
      it('adds a new banner to empty list', () => {
        useNotifications.getState().add({
          id: 'test-1',
          kind: 'info',
          message: 'Info message',
        })
        
        const state = useNotifications.getState()
        expect(state.banners).toHaveLength(1)
        expect(state.banners[0]).toEqual({
          id: 'test-1',
          kind: 'info',
          message: 'Info message',
        })
      })

      it('adds multiple banners', () => {
        useNotifications.getState().add({
          id: 'test-1',
          kind: 'info',
          message: 'Info message',
        })
        useNotifications.getState().add({
          id: 'test-2',
          kind: 'warning',
          message: 'Warning message',
        })
        useNotifications.getState().add({
          id: 'test-3',
          kind: 'error',
          message: 'Error message',
        })
        
        const state = useNotifications.getState()
        expect(state.banners).toHaveLength(3)
        expect(state.banners[0].id).toBe('test-1')
        expect(state.banners[1].id).toBe('test-2')
        expect(state.banners[2].id).toBe('test-3')
      })

      it('supports all banner kinds: info, warning, error', () => {
        useNotifications.getState().add({
          id: 'info',
          kind: 'info',
          message: 'Info',
        })
        useNotifications.getState().add({
          id: 'warning',
          kind: 'warning',
          message: 'Warning',
        })
        useNotifications.getState().add({
          id: 'error',
          kind: 'error',
          message: 'Error',
        })
        
        const state = useNotifications.getState()
        expect(state.banners[0].kind).toBe('info')
        expect(state.banners[1].kind).toBe('warning')
        expect(state.banners[2].kind).toBe('error')
      })
    })

    describe('updating existing banners', () => {
      it('updates existing banner with same ID', () => {
        useNotifications.getState().add({
          id: 'test-1',
          kind: 'info',
          message: 'Original message',
        })
        
        useNotifications.getState().add({
          id: 'test-1',
          kind: 'warning',
          message: 'Updated message',
        })
        
        const state = useNotifications.getState()
        expect(state.banners).toHaveLength(1)
        expect(state.banners[0]).toEqual({
          id: 'test-1',
          kind: 'warning',
          message: 'Updated message',
        })
      })

      it('updates only the matching banner', () => {
        useNotifications.getState().add({
          id: 'test-1',
          kind: 'info',
          message: 'Message 1',
        })
        useNotifications.getState().add({
          id: 'test-2',
          kind: 'warning',
          message: 'Message 2',
        })
        useNotifications.getState().add({
          id: 'test-3',
          kind: 'error',
          message: 'Message 3',
        })
        
        useNotifications.getState().add({
          id: 'test-2',
          kind: 'error',
          message: 'Updated Message 2',
        })
        
        const state = useNotifications.getState()
        expect(state.banners).toHaveLength(3)
        expect(state.banners[0].message).toBe('Message 1')
        expect(state.banners[1].message).toBe('Updated Message 2')
        expect(state.banners[1].kind).toBe('error')
        expect(state.banners[2].message).toBe('Message 3')
      })

      it('preserves banner order when updating', () => {
        useNotifications.getState().add({ id: 'a', kind: 'info', message: 'A' })
        useNotifications.getState().add({ id: 'b', kind: 'info', message: 'B' })
        useNotifications.getState().add({ id: 'c', kind: 'info', message: 'C' })
        
        useNotifications.getState().add({ id: 'b', kind: 'warning', message: 'B Updated' })
        
        const state = useNotifications.getState()
        expect(state.banners.map(b => b.id)).toEqual(['a', 'b', 'c'])
        expect(state.banners[1].message).toBe('B Updated')
      })
    })

    describe('TTL (Time To Live)', () => {
      it('removes banner after TTL expires', () => {
        useNotifications.getState().add({
          id: 'test-1',
          kind: 'info',
          message: 'Temporary message',
          ttlMs: 3000,
        })
        
        expect(useNotifications.getState().banners).toHaveLength(1)
        
        vi.advanceTimersByTime(3000)
        
        expect(useNotifications.getState().banners).toHaveLength(0)
      })

      it('does not remove banner if TTL is not set', () => {
        useNotifications.getState().add({
          id: 'test-1',
          kind: 'info',
          message: 'Persistent message',
        })
        
        vi.advanceTimersByTime(10000)
        
        expect(useNotifications.getState().banners).toHaveLength(1)
      })

      it('does not remove banner if TTL is 0', () => {
        useNotifications.getState().add({
          id: 'test-1',
          kind: 'info',
          message: 'Persistent message',
          ttlMs: 0,
        })
        
        vi.advanceTimersByTime(10000)
        
        expect(useNotifications.getState().banners).toHaveLength(1)
      })

      it('does not remove banner if TTL is negative', () => {
        useNotifications.getState().add({
          id: 'test-1',
          kind: 'info',
          message: 'Persistent message',
          ttlMs: -1000,
        })
        
        vi.advanceTimersByTime(10000)
        
        expect(useNotifications.getState().banners).toHaveLength(1)
      })

      it('removes multiple banners with different TTLs', () => {
        useNotifications.getState().add({
          id: 'test-1',
          kind: 'info',
          message: 'Short TTL',
          ttlMs: 1000,
        })
        useNotifications.getState().add({
          id: 'test-2',
          kind: 'info',
          message: 'Medium TTL',
          ttlMs: 3000,
        })
        useNotifications.getState().add({
          id: 'test-3',
          kind: 'info',
          message: 'Long TTL',
          ttlMs: 5000,
        })
        
        expect(useNotifications.getState().banners).toHaveLength(3)
        
        vi.advanceTimersByTime(1000)
        expect(useNotifications.getState().banners).toHaveLength(2)
        expect(useNotifications.getState().banners.map(b => b.id)).toEqual(['test-2', 'test-3'])
        
        vi.advanceTimersByTime(2000)
        expect(useNotifications.getState().banners).toHaveLength(1)
        expect(useNotifications.getState().banners[0].id).toBe('test-3')
        
        vi.advanceTimersByTime(2000)
        expect(useNotifications.getState().banners).toHaveLength(0)
      })

      it('updates TTL when banner is updated with same ID', () => {
        // Add banner with 5s TTL
        useNotifications.getState().add({
          id: 'test-1',
          kind: 'info',
          message: 'Original',
          ttlMs: 5000,
        })
        
        // Advance 2 seconds
        vi.advanceTimersByTime(2000)
        
        // Update banner with new 3s TTL
        useNotifications.getState().add({
          id: 'test-1',
          kind: 'warning',
          message: 'Updated',
          ttlMs: 3000,
        })
        
        // Advance 2 more seconds (4s total from original, but 2s from update)
        vi.advanceTimersByTime(2000)
        expect(useNotifications.getState().banners).toHaveLength(1)
        
        // Advance 1 more second (3s from update)
        vi.advanceTimersByTime(1000)
        expect(useNotifications.getState().banners).toHaveLength(0)
      })
    })
  })

  describe('remove', () => {
    it('removes banner by ID', () => {
      useNotifications.getState().add({ id: 'test-1', kind: 'info', message: 'Test 1' })
      useNotifications.getState().add({ id: 'test-2', kind: 'info', message: 'Test 2' })
      
      useNotifications.getState().remove('test-1')
      
      const state = useNotifications.getState()
      expect(state.banners).toHaveLength(1)
      expect(state.banners[0].id).toBe('test-2')
    })

    it('removes multiple banners', () => {
      useNotifications.getState().add({ id: 'test-1', kind: 'info', message: 'Test 1' })
      useNotifications.getState().add({ id: 'test-2', kind: 'info', message: 'Test 2' })
      useNotifications.getState().add({ id: 'test-3', kind: 'info', message: 'Test 3' })
      
      useNotifications.getState().remove('test-1')
      useNotifications.getState().remove('test-3')
      
      const state = useNotifications.getState()
      expect(state.banners).toHaveLength(1)
      expect(state.banners[0].id).toBe('test-2')
    })

    it('handles removing non-existent ID gracefully', () => {
      useNotifications.getState().add({ id: 'test-1', kind: 'info', message: 'Test 1' })
      
      useNotifications.getState().remove('non-existent')
      
      const state = useNotifications.getState()
      expect(state.banners).toHaveLength(1)
      expect(state.banners[0].id).toBe('test-1')
    })

    it('handles removing from empty list', () => {
      expect(useNotifications.getState().banners).toHaveLength(0)
      
      useNotifications.getState().remove('test-1')
      
      expect(useNotifications.getState().banners).toHaveLength(0)
    })

    it('removes all banners when called for each', () => {
      useNotifications.getState().add({ id: 'test-1', kind: 'info', message: 'Test 1' })
      useNotifications.getState().add({ id: 'test-2', kind: 'info', message: 'Test 2' })
      useNotifications.getState().add({ id: 'test-3', kind: 'info', message: 'Test 3' })
      
      useNotifications.getState().remove('test-1')
      useNotifications.getState().remove('test-2')
      useNotifications.getState().remove('test-3')
      
      expect(useNotifications.getState().banners).toHaveLength(0)
    })
  })

  describe('state immutability', () => {
    it('creates new array reference when adding', () => {
      const state1 = useNotifications.getState()
      const banners1 = state1.banners
      
      useNotifications.getState().add({ id: 'test-1', kind: 'info', message: 'Test' })
      
      const state2 = useNotifications.getState()
      const banners2 = state2.banners
      
      expect(banners1).not.toBe(banners2)
    })

    it('creates new array reference when removing', () => {
      useNotifications.getState().add({ id: 'test-1', kind: 'info', message: 'Test 1' })
      useNotifications.getState().add({ id: 'test-2', kind: 'info', message: 'Test 2' })
      
      const state1 = useNotifications.getState()
      const banners1 = state1.banners
      
      useNotifications.getState().remove('test-1')
      
      const state2 = useNotifications.getState()
      const banners2 = state2.banners
      
      expect(banners1).not.toBe(banners2)
    })

    it('creates new array reference when updating', () => {
      useNotifications.getState().add({ id: 'test-1', kind: 'info', message: 'Original' })
      
      const state1 = useNotifications.getState()
      const banners1 = state1.banners
      
      useNotifications.getState().add({ id: 'test-1', kind: 'warning', message: 'Updated' })
      
      const state2 = useNotifications.getState()
      const banners2 = state2.banners
      
      expect(banners1).not.toBe(banners2)
    })
  })

  describe('complex scenarios', () => {
    it('handles rapid add/remove operations', () => {
      useNotifications.getState().add({ id: 'test-1', kind: 'info', message: 'Test 1' })
      useNotifications.getState().add({ id: 'test-2', kind: 'info', message: 'Test 2' })
      useNotifications.getState().remove('test-1')
      useNotifications.getState().add({ id: 'test-3', kind: 'info', message: 'Test 3' })
      useNotifications.getState().remove('test-2')
      
      const state = useNotifications.getState()
      expect(state.banners).toHaveLength(1)
      expect(state.banners[0].id).toBe('test-3')
    })

    it('handles adding banner with TTL then manually removing before expiry', () => {
      useNotifications.getState().add({
        id: 'test-1',
        kind: 'info',
        message: 'Test',
        ttlMs: 5000,
      })
      
      vi.advanceTimersByTime(2000)
      useNotifications.getState().remove('test-1')
      
      expect(useNotifications.getState().banners).toHaveLength(0)
      
      // Ensure no error when TTL expires after manual removal
      vi.advanceTimersByTime(3000)
      expect(useNotifications.getState().banners).toHaveLength(0)
    })

    it('handles multiple banners with same message but different IDs', () => {
      useNotifications.getState().add({ id: 'id-1', kind: 'info', message: 'Same message' })
      useNotifications.getState().add({ id: 'id-2', kind: 'info', message: 'Same message' })
      
      const state = useNotifications.getState()
      expect(state.banners).toHaveLength(2)
      expect(state.banners[0].message).toBe('Same message')
      expect(state.banners[1].message).toBe('Same message')
      expect(state.banners[0].id).not.toBe(state.banners[1].id)
    })
  })
})

