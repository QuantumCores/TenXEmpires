import { useEffect } from 'react'
import { useNavigate } from 'react-router-dom'
import { getJson } from '../../api/http'
import type { GameSummary, PagedResult } from '../../types/api'

export function GameCurrentGuardRoute() {
  const navigate = useNavigate()

  useEffect(() => {
    let cancelled = false
    ;(async () => {
      const url = new URL('/v1/games', window.location.origin)
      url.searchParams.set('status', 'active')
      url.searchParams.set('sort', 'lastTurnAt')
      url.searchParams.set('order', 'desc')
      url.searchParams.set('pageSize', '1')

      const { ok, status, data } = await getJson<PagedResult<GameSummary>>(url.toString())

      if (cancelled) return

      if (!ok) {
        if (status === 401 || status === 403) {
          const returnUrl = encodeURIComponent('/game/current')
          navigate(`/login?returnUrl=${returnUrl}`, { replace: true })
          return
        }
        // Network/5xx: soft-fail to landing page for now (banner TBD)
        navigate('/', { replace: true })
        return
      }

      const items = data?.items ?? []
      if (items.length > 0) {
        const latest = items[0]
        navigate(`/game/${latest.id}`, { replace: true })
        return
      }

      // No active game: open Start New modal in game shell context
      navigate(`/game/new?modal=start-new`)
    })()
    return () => {
      cancelled = true
    }
  }, [navigate])

  return null
}

