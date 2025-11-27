import { useNotifications } from '../../components/ui/notifications'
import { useModalParam } from '../../router/query'
import { useUiStore } from '../../components/ui/uiStore'
import type { HttpResult } from '../../api/http'

// Error codes from the backend
export const ErrorCodes = {
  // Authentication
  UNAUTHORIZED: 'UNAUTHORIZED',
  SESSION_EXPIRED: 'SESSION_EXPIRED',

  // Game errors
  GAME_NOT_FOUND: 'GAME_NOT_FOUND',
  TURN_IN_PROGRESS: 'TURN_IN_PROGRESS',
  NOT_YOUR_TURN: 'NOT_YOUR_TURN',

  // Action errors
  ILLEGAL_MOVE: 'ILLEGAL_MOVE',
  ONE_UNIT_PER_TILE: 'ONE_UNIT_PER_TILE',
  NO_ACTIONS_LEFT: 'NO_ACTIONS_LEFT',
  OUT_OF_RANGE: 'OUT_OF_RANGE',
  INVALID_TARGET: 'INVALID_TARGET',
  CITY_ALREADY_ACTED: 'CITY_ALREADY_ACTED',
  INSUFFICIENT_RESOURCES: 'INSUFFICIENT_RESOURCES',
  SPAWN_BLOCKED: 'SPAWN_BLOCKED',
  INVALID_UNIT: 'INVALID_UNIT',

  // AI errors
  AI_TIMEOUT: 'AI_TIMEOUT',

  // Schema errors
  SCHEMA_MISMATCH: 'SCHEMA_MISMATCH',
  MAP_SCHEMA_MISMATCH: 'MAP_SCHEMA_MISMATCH',

  // Save errors
  SAVE_NOT_FOUND: 'SAVE_NOT_FOUND',
  SAVE_CONFLICT: 'SAVE_CONFLICT',
  INVALID_SLOT: 'INVALID_SLOT',

  // Rate limiting
  RATE_LIMIT: 'RATE_LIMIT',
} as const

interface ErrorResponse {
  code?: string
  message?: string
  details?: Record<string, unknown>
}

export interface GameError {
  code: string
  message: string
  status: number
  shouldRetry: boolean
  shouldRedirect?: string
}

/**
 * Parses an HTTP error response into a structured GameError
 */
export function parseGameError(result: HttpResult<unknown>): GameError {
  const { status, data } = result

  // Network error
  if (status === 0) {
    return {
      code: 'NETWORK_ERROR',
      message: 'Unable to connect. Check your internet connection.',
      status: 0,
      shouldRetry: false,
    }
  }

  // Parse error response
  const errorData = data as ErrorResponse | undefined
  const code = errorData?.code || 'UNKNOWN_ERROR'
  const message = errorData?.message || 'An unexpected error occurred'

  // Unauthorized / Session expired
  if (status === 401 || status === 403) {
    return {
      code: ErrorCodes.UNAUTHORIZED,
      message: 'Your session has expired. Please log in again.',
      status,
      shouldRetry: false,
      shouldRedirect: `/login?returnUrl=${encodeURIComponent(window.location.pathname)}`,
    }
  }

  // Rate limiting
  if (status === 429) {
    return {
      code: ErrorCodes.RATE_LIMIT,
      message: 'Too many requests. Please slow down.',
      status,
      shouldRetry: false,
    }
  }

  // Conflict errors (turn in progress, save conflict, etc.)
  if (status === 409) {
    if (code === ErrorCodes.TURN_IN_PROGRESS) {
      return {
        code,
        message: 'AI is still processing. Please wait.',
        status,
        shouldRetry: true,
      }
    }
    return {
      code,
      message,
      status,
      shouldRetry: false,
    }
  }

  // Validation errors
  if (status === 422) {
    return {
      code,
      message,
      status,
      shouldRetry: false,
    }
  }

  // Server errors
  if (status >= 500) {
    return {
      code: 'SERVER_ERROR',
      message: 'Server error. Please try again later.',
      status,
      shouldRetry: false,
    }
  }

  return {
    code,
    message,
    status,
    shouldRetry: false,
  }
}

/**
 * Determines the appropriate user-facing notification for an error
 */
export function getErrorNotification(error: GameError): {
  kind: 'info' | 'warning' | 'error'
  message: string
  ttlMs?: number
} {
  // Turn in progress - informational
  if (error.code === ErrorCodes.TURN_IN_PROGRESS) {
    return {
      kind: 'info',
      message: error.message,
      ttlMs: 2000,
    }
  }

  // Rate limiting - warning
  if (error.code === ErrorCodes.RATE_LIMIT) {
    return {
      kind: 'warning',
      message: error.message,
      ttlMs: 5000,
    }
  }

  // Action errors - warning (user mistake)
  const actionErrors: readonly string[] = [
    ErrorCodes.ILLEGAL_MOVE,
    ErrorCodes.ONE_UNIT_PER_TILE,
    ErrorCodes.NO_ACTIONS_LEFT,
    ErrorCodes.OUT_OF_RANGE,
    ErrorCodes.INVALID_TARGET,
    ErrorCodes.CITY_ALREADY_ACTED,
    ErrorCodes.INSUFFICIENT_RESOURCES,
    ErrorCodes.SPAWN_BLOCKED,
    ErrorCodes.INVALID_UNIT,
  ]
  if (actionErrors.includes(error.code)) {
    return {
      kind: 'warning',
      message: error.message,
      ttlMs: 4000,
    }
  }

  // Server/network errors - error
  if (error.status >= 500 || error.status === 0) {
    return {
      kind: 'error',
      message: error.message,
      ttlMs: 6000,
    }
  }

  // Default - error
  return {
    kind: 'error',
    message: error.message,
    ttlMs: 5000,
  }
}

interface GameErrorHandler {
  handleError: (result: HttpResult<unknown>) => GameError
  handleRateLimit: () => void
}

/**
 * Hook to handle game errors with notifications and routing
 */
export function useGameErrorHandler(): GameErrorHandler {
  const notifications = useNotifications()
  const { state, openModal } = useModalParam()
  const setSchemaError = useUiStore((s) => s.setSchemaError)

  return {
    handleError: (result: HttpResult<unknown>): GameError => {
      const error = parseGameError(result)
      const notification = getErrorNotification(error)

      // Show notification
      notifications.add({
        id: `error-${error.code}-${Date.now()}`,
        kind: notification.kind,
        message: notification.message,
        ttlMs: notification.ttlMs,
      })

      // Open session-expired modal on unauthorized
      if ((error.status === 401 || error.status === 403) && state.modal !== 'session-expired') {
        openModal('session-expired', undefined, 'replace')
      }

      // Schema mismatch handling -> open blocking error-schema modal
      if (result.status === 422) {
        const errorResponse = result.data as ErrorResponse | undefined
        const code = errorResponse?.code
        if (code === ErrorCodes.SCHEMA_MISMATCH || code === ErrorCodes.MAP_SCHEMA_MISMATCH) {
          // Persist last schema error for the modal
          setSchemaError({
            code,
            message: errorResponse?.message || error.message,
            details: errorResponse?.details,
          })
          if (state.modal !== 'error-schema') {
            openModal('error-schema', undefined, 'replace')
          }
        }
      }

      return error
    },

    handleRateLimit: (): void => {
      notifications.add({
        id: 'rate-limit-global',
        kind: 'warning',
        message: 'Rate limited. Slowing down requests...',
        ttlMs: 5000,
      })
    },
  }
}

