import { useQuery } from '@tanstack/react-query'
import { getJson } from '../../api/http'
import type { CurrentUser } from '../../types/api'
import type { AuthStatus } from '../../types/view'
import { useNotifications } from '../../components/ui/notifications'

function nowIsoSeconds(): string {
  const iso = new Date().toISOString()
  return iso.replace(/\.\d{3}Z$/, 'Z')
}

export interface AuthStatusResult {
  status: number
  auth: AuthStatus
}

async function rawFetchAuthStatus(): Promise<AuthStatusResult> {
  // getJson already calls getApiUrl internally, so pass the path directly
  const { ok, status } = await getJson<CurrentUser>('/api/auth/me')

  // Default unauthenticated state
  const base: AuthStatus = {
    isAuthenticated: false,
    checkedAt: nowIsoSeconds(),
  }

  if (!ok) {
    if (status === 401 || status === 403) {
      // Unauthenticated visitor
      return { status, auth: base }
    }
    // Network/5xx/429: treat as visitor for CTA purposes but surface status for banners
    return { status, auth: base }
  }

  // Authenticated; do not fetch games here (guard will handle that)
  return {
    status: 200,
    auth: {
      isAuthenticated: true,
      checkedAt: nowIsoSeconds(),
    },
  }
}

export function useAuthStatusQuery() {
  const notify = useNotifications()
  return useQuery({
    queryKey: ['auth-status'],
    queryFn: async () => {
      const res = await rawFetchAuthStatus()
      const s = res.status
      if (s === 429) {
        notify.add({ id: 'rate-limit', kind: 'warning', message: 'Limited connectivity. Please try again shortly.', ttlMs: 5000 })
      } else if (s === 0 || (s >= 500 && s < 600)) {
        notify.add({ id: 'auth-status-fail', kind: 'info', message: "Couldn't check session. You can still log in or register.", ttlMs: 7000 })
      }
      return res
    },
  })
}
