import { useEffect } from 'react'
import { useNavigate } from 'react-router-dom'
import { fetchGames } from '../../api/games'

export function GameCurrentGuardRoute() {
  const navigate = useNavigate()

  useEffect(() => {
    let cancelled = false
    ;(async () => {
      const { ok, status, data } = await fetchGames({
        status: 'active',
        sort: 'lastTurnAt',
        order: 'desc',
        pageSize: 1,
      })

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

