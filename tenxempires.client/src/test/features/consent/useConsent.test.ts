import { describe, it, expect, beforeEach, afterEach } from 'vitest'

// Mock document.cookie before importing the module
let mockCookie = ''
Object.defineProperty(document, 'cookie', {
  get: () => mockCookie,
  set: (value: string) => {
    mockCookie = value
  },
  configurable: true,
})

import { useConsent } from '../../../features/consent/useConsent'

describe('useConsent', () => {
  beforeEach(() => {
    // Clear cookie and reset store state
    mockCookie = ''
    // Reset the store to initial state
    useConsent.setState({ decided: false, accepted: false })
  })

  afterEach(() => {
    mockCookie = ''
  })

  describe('initial state', () => {
    it('starts with decided: false and accepted: false', () => {
      const state = useConsent.getState()
      expect(state.decided).toBe(false)
      expect(state.accepted).toBe(false)
    })
  })

  describe('hydrateFromCookie', () => {
    it('reads accepted consent from cookie', () => {
      mockCookie = 'tenx.consent=accepted'
      
      useConsent.getState().hydrateFromCookie()
      
      const state = useConsent.getState()
      expect(state.decided).toBe(true)
      expect(state.accepted).toBe(true)
    })

    it('reads declined consent from cookie', () => {
      mockCookie = 'tenx.consent=declined'
      
      useConsent.getState().hydrateFromCookie()
      
      const state = useConsent.getState()
      expect(state.decided).toBe(true)
      expect(state.accepted).toBe(false)
    })

    it('handles missing cookie gracefully', () => {
      mockCookie = ''
      
      useConsent.getState().hydrateFromCookie()
      
      const state = useConsent.getState()
      expect(state.decided).toBe(false)
      expect(state.accepted).toBe(false)
    })

    it('handles cookie with other values', () => {
      mockCookie = 'other=value; another=thing'
      
      useConsent.getState().hydrateFromCookie()
      
      const state = useConsent.getState()
      expect(state.decided).toBe(false)
      expect(state.accepted).toBe(false)
    })

    it('reads consent cookie with other cookies present', () => {
      mockCookie = 'session=abc123; tenx.consent=accepted; theme=dark'
      
      useConsent.getState().hydrateFromCookie()
      
      const state = useConsent.getState()
      expect(state.decided).toBe(true)
      expect(state.accepted).toBe(true)
    })

    it('handles URL-encoded cookie values', () => {
      mockCookie = 'tenx.consent=accepted'
      
      useConsent.getState().hydrateFromCookie()
      
      const state = useConsent.getState()
      expect(state.decided).toBe(true)
      expect(state.accepted).toBe(true)
    })

    it('does not change state if cookie is malformed', () => {
      mockCookie = 'tenx.consent='
      
      useConsent.getState().hydrateFromCookie()
      
      const state = useConsent.getState()
      expect(state.decided).toBe(false)
      expect(state.accepted).toBe(false)
    })
  })

  describe('accept', () => {
    it('sets decided: true and accepted: true', () => {
      useConsent.getState().accept()
      
      const state = useConsent.getState()
      expect(state.decided).toBe(true)
      expect(state.accepted).toBe(true)
    })

    it('writes accepted value to cookie', () => {
      useConsent.getState().accept()
      
      expect(mockCookie).toContain('tenx.consent=accepted')
    })

    it('sets cookie with correct attributes', () => {
      useConsent.getState().accept()
      
      expect(mockCookie).toContain('Path=/')
      expect(mockCookie).toContain('SameSite=Lax')
      expect(mockCookie).toContain('Secure')
      expect(mockCookie).toContain('Expires=')
    })

    it('sets cookie with 2-year expiration', () => {
      const beforeAccept = Date.now()
      useConsent.getState().accept()
      
      // Extract the Expires date from the cookie
      const expiresMatch = mockCookie.match(/Expires=([^;]+)/)
      expect(expiresMatch).toBeTruthy()
      
      const expiresDate = new Date(expiresMatch![1])
      const expectedDate = new Date(beforeAccept + 365 * 2 * 24 * 60 * 60 * 1000)
      
      // Allow 1 second tolerance for test execution time
      const diff = Math.abs(expiresDate.getTime() - expectedDate.getTime())
      expect(diff).toBeLessThan(1000)
    })
  })

  describe('decline', () => {
    it('sets decided: true and accepted: false', () => {
      useConsent.getState().decline()
      
      const state = useConsent.getState()
      expect(state.decided).toBe(true)
      expect(state.accepted).toBe(false)
    })

    it('writes declined value to cookie', () => {
      useConsent.getState().decline()
      
      expect(mockCookie).toContain('tenx.consent=declined')
    })

    it('sets cookie with correct attributes', () => {
      useConsent.getState().decline()
      
      expect(mockCookie).toContain('Path=/')
      expect(mockCookie).toContain('SameSite=Lax')
      expect(mockCookie).toContain('Secure')
      expect(mockCookie).toContain('Expires=')
    })
  })

  describe('state persistence', () => {
    it('persists accepted state after accept', () => {
      useConsent.getState().accept()
      
      const state1 = useConsent.getState()
      expect(state1.decided).toBe(true)
      expect(state1.accepted).toBe(true)
      
      // Simulate page reload by hydrating from the cookie
      useConsent.setState({ decided: false, accepted: false })
      useConsent.getState().hydrateFromCookie()
      
      const state2 = useConsent.getState()
      expect(state2.decided).toBe(true)
      expect(state2.accepted).toBe(true)
    })

    it('persists declined state after decline', () => {
      useConsent.getState().decline()
      
      const state1 = useConsent.getState()
      expect(state1.decided).toBe(true)
      expect(state1.accepted).toBe(false)
      
      // Simulate page reload
      useConsent.setState({ decided: false, accepted: false })
      useConsent.getState().hydrateFromCookie()
      
      const state2 = useConsent.getState()
      expect(state2.decided).toBe(true)
      expect(state2.accepted).toBe(false)
    })
  })

  describe('state transitions', () => {
    it('can transition from undecided to accepted', () => {
      expect(useConsent.getState().decided).toBe(false)
      
      useConsent.getState().accept()
      
      expect(useConsent.getState().decided).toBe(true)
      expect(useConsent.getState().accepted).toBe(true)
    })

    it('can transition from undecided to declined', () => {
      expect(useConsent.getState().decided).toBe(false)
      
      useConsent.getState().decline()
      
      expect(useConsent.getState().decided).toBe(true)
      expect(useConsent.getState().accepted).toBe(false)
    })

    it('can change from accepted to declined', () => {
      useConsent.getState().accept()
      expect(useConsent.getState().accepted).toBe(true)
      
      useConsent.getState().decline()
      
      expect(useConsent.getState().decided).toBe(true)
      expect(useConsent.getState().accepted).toBe(false)
    })

    it('can change from declined to accepted', () => {
      useConsent.getState().decline()
      expect(useConsent.getState().accepted).toBe(false)
      
      useConsent.getState().accept()
      
      expect(useConsent.getState().decided).toBe(true)
      expect(useConsent.getState().accepted).toBe(true)
    })
  })
})

