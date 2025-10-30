import { describe, it, expect, vi, beforeEach } from 'vitest'
import type { HttpResult } from '../../../api/http'

let notifyAdd = vi.fn()
let openModal = vi.fn()
let setSchemaError = vi.fn()

vi.mock('../../../components/ui/notifications', () => ({
  useNotifications: () => ({ add: notifyAdd }),
}))

vi.mock('../../../router/query', () => ({
  useModalParam: () => ({ state: { modal: undefined }, openModal }),
}))

vi.mock('../../../components/ui/uiStore', () => ({
  // Simulate zustand selector usage
  useUiStore: <T,>(selector: (state: { setSchemaError: typeof setSchemaError }) => T) =>
    selector({ setSchemaError }),
}))

import {
  useGameErrorHandler,
  parseGameError,
  getErrorNotification,
  ErrorCodes,
} from '../../../features/game/errorHandling'

describe('errorHandling - parseGameError', () => {
  describe('network errors', () => {
    it('handles network error (status 0)', () => {
      const result: HttpResult<unknown> = { ok: false, status: 0 }
      const error = parseGameError(result)

      expect(error.code).toBe('NETWORK_ERROR')
      expect(error.message).toContain('internet connection')
      expect(error.status).toBe(0)
      expect(error.shouldRetry).toBe(false)
    })
  })

  describe('authentication errors', () => {
    it('handles 401 Unauthorized', () => {
      const result: HttpResult<unknown> = {
        ok: false,
        status: 401,
        data: { code: 'UNAUTHORIZED', message: 'Not authenticated' },
      }
      const error = parseGameError(result)

      expect(error.code).toBe(ErrorCodes.UNAUTHORIZED)
      expect(error.status).toBe(401)
      expect(error.shouldRetry).toBe(false)
      expect(error.shouldRedirect).toContain('/login')
    })

    it('handles 403 CSRF_INVALID with retry', () => {
      const result: HttpResult<unknown> = {
        ok: false,
        status: 403,
        data: { code: 'CSRF_INVALID', message: 'Invalid token' },
      }
      const error = parseGameError(result)

      expect(error.code).toBe(ErrorCodes.CSRF_INVALID)
      expect(error.status).toBe(403)
      expect(error.shouldRetry).toBe(true)
    })

    it('handles 403 without CSRF as unauthorized', () => {
      const result: HttpResult<unknown> = {
        ok: false,
        status: 403,
        data: { code: 'FORBIDDEN', message: 'Access denied' },
      }
      const error = parseGameError(result)

      expect(error.code).toBe(ErrorCodes.UNAUTHORIZED)
      expect(error.shouldRedirect).toContain('/login')
    })
  })

  describe('rate limiting', () => {
    it('handles 429 rate limit error', () => {
      const result: HttpResult<unknown> = { ok: false, status: 429 }
      const error = parseGameError(result)

      expect(error.code).toBe(ErrorCodes.RATE_LIMIT)
      expect(error.message).toContain('Too many requests')
      expect(error.status).toBe(429)
      expect(error.shouldRetry).toBe(false)
    })
  })

  describe('conflict errors', () => {
    it('handles 409 TURN_IN_PROGRESS with retry', () => {
      const result: HttpResult<unknown> = {
        ok: false,
        status: 409,
        data: { code: 'TURN_IN_PROGRESS', message: 'AI is processing' },
      }
      const error = parseGameError(result)

      expect(error.code).toBe(ErrorCodes.TURN_IN_PROGRESS)
      expect(error.shouldRetry).toBe(true)
    })

    it('handles 409 SAVE_CONFLICT without retry', () => {
      const result: HttpResult<unknown> = {
        ok: false,
        status: 409,
        data: { code: 'SAVE_CONFLICT', message: 'Save conflict' },
      }
      const error = parseGameError(result)

      expect(error.code).toBe('SAVE_CONFLICT')
      expect(error.shouldRetry).toBe(false)
    })
  })

  describe('validation errors', () => {
    it('handles 422 validation error', () => {
      const result: HttpResult<unknown> = {
        ok: false,
        status: 422,
        data: { code: 'ILLEGAL_MOVE', message: 'Invalid move' },
      }
      const error = parseGameError(result)

      expect(error.code).toBe('ILLEGAL_MOVE')
      expect(error.status).toBe(422)
      expect(error.shouldRetry).toBe(false)
    })

    it('handles 422 SCHEMA_MISMATCH', () => {
      const result: HttpResult<unknown> = {
        ok: false,
        status: 422,
        data: { code: 'SCHEMA_MISMATCH', message: 'Save schema mismatch' },
      }
      const error = parseGameError(result)

      expect(error.code).toBe('SCHEMA_MISMATCH')
      expect(error.message).toContain('schema mismatch')
    })
  })

  describe('server errors', () => {
    it('handles 500 server error', () => {
      const result: HttpResult<unknown> = {
        ok: false,
        status: 500,
        data: { code: 'INTERNAL_ERROR', message: 'Server error' },
      }
      const error = parseGameError(result)

      expect(error.code).toBe('SERVER_ERROR')
      expect(error.message).toContain('Server error')
      expect(error.status).toBe(500)
      expect(error.shouldRetry).toBe(false)
    })

    it('handles 503 service unavailable', () => {
      const result: HttpResult<unknown> = { ok: false, status: 503 }
      const error = parseGameError(result)

      expect(error.code).toBe('SERVER_ERROR')
      expect(error.status).toBe(503)
    })
  })

  describe('fallback errors', () => {
    it('handles unknown error without data', () => {
      const result: HttpResult<unknown> = { ok: false, status: 418 }
      const error = parseGameError(result)

      expect(error.code).toBe('UNKNOWN_ERROR')
      expect(error.message).toContain('unexpected error')
      expect(error.shouldRetry).toBe(false)
    })

    it('uses error message from response when available', () => {
      const result: HttpResult<unknown> = {
        ok: false,
        status: 400,
        data: { code: 'CUSTOM_ERROR', message: 'Custom message' },
      }
      const error = parseGameError(result)

      expect(error.code).toBe('CUSTOM_ERROR')
      expect(error.message).toBe('Custom message')
    })
  })
})

describe('errorHandling - getErrorNotification', () => {
  describe('notification types', () => {
    it('returns info notification for CSRF_INVALID', () => {
      const error = {
        code: ErrorCodes.CSRF_INVALID,
        message: 'Token expired',
        status: 403,
        shouldRetry: true,
      }
      const notification = getErrorNotification(error)

      expect(notification.kind).toBe('info')
      expect(notification.ttlMs).toBe(3000)
    })

    it('returns info notification for TURN_IN_PROGRESS', () => {
      const error = {
        code: ErrorCodes.TURN_IN_PROGRESS,
        message: 'AI processing',
        status: 409,
        shouldRetry: true,
      }
      const notification = getErrorNotification(error)

      expect(notification.kind).toBe('info')
      expect(notification.ttlMs).toBe(2000)
    })

    it('returns warning notification for RATE_LIMIT', () => {
      const error = {
        code: ErrorCodes.RATE_LIMIT,
        message: 'Too many requests',
        status: 429,
        shouldRetry: false,
      }
      const notification = getErrorNotification(error)

      expect(notification.kind).toBe('warning')
      expect(notification.ttlMs).toBe(5000)
    })

    it('returns warning notification for action errors', () => {
      const actionErrors = [
        ErrorCodes.ILLEGAL_MOVE,
        ErrorCodes.ONE_UNIT_PER_TILE,
        ErrorCodes.NO_ACTIONS_LEFT,
        ErrorCodes.OUT_OF_RANGE,
        ErrorCodes.INVALID_TARGET,
      ]

      actionErrors.forEach((code) => {
        const error = {
          code,
          message: 'Action error',
          status: 422,
          shouldRetry: false,
        }
        const notification = getErrorNotification(error)

        expect(notification.kind).toBe('warning')
        expect(notification.ttlMs).toBe(4000)
      })
    })

    it('returns error notification for network errors', () => {
      const error = {
        code: 'NETWORK_ERROR',
        message: 'Connection failed',
        status: 0,
        shouldRetry: false,
      }
      const notification = getErrorNotification(error)

      expect(notification.kind).toBe('error')
      expect(notification.ttlMs).toBe(6000)
    })

    it('returns error notification for server errors', () => {
      const error = {
        code: 'SERVER_ERROR',
        message: 'Server error',
        status: 500,
        shouldRetry: false,
      }
      const notification = getErrorNotification(error)

      expect(notification.kind).toBe('error')
      expect(notification.ttlMs).toBe(6000)
    })

    it('returns error notification for unknown errors', () => {
      const error = {
        code: 'UNKNOWN',
        message: 'Something went wrong',
        status: 400,
        shouldRetry: false,
      }
      const notification = getErrorNotification(error)

      expect(notification.kind).toBe('error')
      expect(notification.ttlMs).toBe(5000)
    })
  })

  describe('notification messages', () => {
    it('includes error message in notification', () => {
      const error = {
        code: 'CUSTOM_ERROR',
        message: 'Custom error message',
        status: 400,
        shouldRetry: false,
      }
      const notification = getErrorNotification(error)

      expect(notification.message).toBe('Custom error message')
    })
  })
})

describe('errorHandling - useGameErrorHandler', () => {
  beforeEach(() => {
    notifyAdd = vi.fn()
    openModal = vi.fn()
    setSchemaError = vi.fn()
  })

  describe('notification behavior', () => {
    it('shows notification for all errors', () => {
      const { handleError } = useGameErrorHandler()

      handleError({
        ok: false,
        status: 404,
        data: { code: 'NOT_FOUND', message: 'Resource not found' },
      })

      expect(notifyAdd).toHaveBeenCalledWith(
        expect.objectContaining({
          kind: expect.any(String),
          message: expect.any(String),
        })
      )
    })

    it('generates unique notification IDs with timestamp', () => {
      const { handleError } = useGameErrorHandler()

      handleError({
        ok: false,
        status: 400,
        data: { code: 'ERROR', message: 'Error' },
      })

      expect(notifyAdd).toHaveBeenCalledWith(
        expect.objectContaining({
          id: expect.stringMatching(/^error-/),
        })
      )
    })
  })

  describe('modal behavior', () => {
    it('opens session-expired modal on 401', () => {
      const { handleError } = useGameErrorHandler()

      handleError({
        ok: false,
        status: 401,
        data: { code: 'UNAUTHORIZED' },
      })

      expect(openModal).toHaveBeenCalledWith('session-expired', undefined, 'replace')
    })

    it('opens session-expired modal on 403 (non-CSRF)', () => {
      const { handleError } = useGameErrorHandler()

      handleError({
        ok: false,
        status: 403,
        data: { code: 'FORBIDDEN' },
      })

      expect(openModal).toHaveBeenCalledWith('session-expired', undefined, 'replace')
    })

    it('opens error-schema modal and stores error on SCHEMA_MISMATCH', () => {
      const { handleError } = useGameErrorHandler()

      handleError({
        ok: false,
        status: 422,
        data: {
          code: 'SCHEMA_MISMATCH',
          message: 'Save schema mismatch',
          details: { version: 2 },
        },
      })

      expect(setSchemaError).toHaveBeenCalledWith({
        code: 'SCHEMA_MISMATCH',
        message: 'Save schema mismatch',
        details: { version: 2 },
      })
      expect(openModal).toHaveBeenCalledWith('error-schema', undefined, 'replace')
    })

    it('opens error-schema modal on MAP_SCHEMA_MISMATCH', () => {
      const { handleError } = useGameErrorHandler()

      handleError({
        ok: false,
        status: 422,
        data: { code: 'MAP_SCHEMA_MISMATCH', message: 'Map schema mismatch' },
      })

      expect(setSchemaError).toHaveBeenCalled()
      expect(openModal).toHaveBeenCalledWith('error-schema', undefined, 'replace')
    })

    it('does not open modal for non-auth/schema errors', () => {
      const { handleError } = useGameErrorHandler()

      handleError({
        ok: false,
        status: 400,
        data: { code: 'BAD_REQUEST', message: 'Bad request' },
      })

      expect(openModal).not.toHaveBeenCalled()
    })
  })

  describe('rate limit handler', () => {
    it('shows rate limit notification', () => {
      const { handleRateLimit } = useGameErrorHandler()

      handleRateLimit()

      expect(notifyAdd).toHaveBeenCalledWith({
        id: 'rate-limit-global',
        kind: 'warning',
        message: expect.stringContaining('Rate limited'),
        ttlMs: 5000,
      })
    })
  })

  describe('return values', () => {
    it('returns parsed error object', () => {
      const { handleError } = useGameErrorHandler()

      const result = handleError({
        ok: false,
        status: 404,
        data: { code: 'NOT_FOUND', message: 'Not found' },
      })

      expect(result).toEqual(
        expect.objectContaining({
          code: 'NOT_FOUND',
          message: 'Not found',
          status: 404,
          shouldRetry: expect.any(Boolean),
        })
      )
    })
  })
})

