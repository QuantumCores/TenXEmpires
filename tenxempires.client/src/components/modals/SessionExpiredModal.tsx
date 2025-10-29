import { useMemo } from 'react'
import { Link, useLocation } from 'react-router-dom'

export interface SessionExpiredModalProps {
  onRequestClose: () => void
  returnUrl?: string
}

function buildReturnUrl(currentPath: string, currentSearch: string): string {
  const url = new URL(window.location.origin + currentPath + currentSearch)
  url.searchParams.delete('modal')
  url.searchParams.delete('confirm')
  // Keep other params like tab if present
  return url.pathname + (url.search ? `?${url.searchParams.toString()}` : '')
}

export function SessionExpiredModal({ onRequestClose, returnUrl }: SessionExpiredModalProps) {
  const location = useLocation()

  const computedReturnUrl = useMemo(() => {
    if (returnUrl) return returnUrl
    const sanitized = buildReturnUrl(location.pathname, location.search)
    // If not in game route, fallback to game/current
    return sanitized.startsWith('/game/') || sanitized === '/game/current'
      ? sanitized
      : '/game/current'
  }, [location.pathname, location.search, returnUrl])

  const loginHref = useMemo(() => {
    const params = new URLSearchParams()
    params.set('returnUrl', computedReturnUrl)
    return `/login?${params.toString()}`
  }, [computedReturnUrl])

  return (
    <div className="flex flex-col gap-4">
      <div className="flex items-center justify-between">
        <h2 className="text-lg font-semibold" id="session-expired-title">Session expired</h2>
        <button
          type="button"
          className="rounded px-2 py-1 hover:bg-slate-100"
          onClick={onRequestClose}
          aria-label="Dismiss"
        >
          ?
        </button>
      </div>

      <p className="text-sm text-slate-600">
        Your session has expired due to inactivity or a security check. Log in again to continue your game.
      </p>

      <div className="flex items-center justify-end gap-2">
        <button
          type="button"
          className="rounded border border-slate-300 bg-white px-3 py-1.5 text-sm text-slate-700 hover:bg-slate-50"
          onClick={onRequestClose}
        >
          Dismiss
        </button>
        <Link
          to={loginHref}
          className="rounded bg-indigo-600 px-3 py-1.5 text-sm font-medium text-white hover:bg-indigo-700"
        >
          Login
        </Link>
      </div>
    </div>
  )
}

