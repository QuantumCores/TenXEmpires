let csrfRefreshPromise: Promise<boolean> | null = null

/**
 * Refreshes the CSRF token by calling the server endpoint.
 * Multiple simultaneous calls will share the same promise.
 */
export async function refreshCsrfToken(): Promise<boolean> {
  // If refresh is already in progress, return the existing promise
  if (csrfRefreshPromise) {
    return csrfRefreshPromise
  }

  csrfRefreshPromise = (async () => {
    try {
      // Call server endpoint that sets new CSRF cookie
      // Uses the same endpoint as CsrfProvider for consistency
      const response = await fetch('/v1/auth/csrf', {
        method: 'GET',
        credentials: 'include',
        headers: { 'Accept': 'application/json' },
      })
      return response.ok
    } catch {
      return false
    } finally {
      // Clear the promise after completion
      csrfRefreshPromise = null
    }
  })()

  return csrfRefreshPromise
}

/**
 * Wraps an API call with automatic CSRF refresh on 403 CSRF_INVALID errors.
 * Retries the original request once after refreshing the token.
 */
export async function withCsrfRetry<T>(
  apiCall: () => Promise<T>,
  shouldRetry: (result: T) => boolean
): Promise<T> {
  const result = await apiCall()

  // Check if we should retry due to CSRF error
  if (shouldRetry(result)) {
    const refreshed = await refreshCsrfToken()

    if (refreshed) {
      // Retry the original call once
      return apiCall()
    }
  }

  return result
}

