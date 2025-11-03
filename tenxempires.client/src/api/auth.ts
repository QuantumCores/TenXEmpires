import { getJson } from './http'

/**
 * Calls the keepalive endpoint to extend the authenticated session.
 * Returns 204 when successful. 401 when unauthenticated.
 */
export async function keepAlive() {
  return getJson<void>('/api/auth/keepalive')
}

