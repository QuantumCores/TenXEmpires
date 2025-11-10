import { useEffect, useState } from 'react'
import { useLocation, useNavigate } from 'react-router-dom'
import { getJson, getApiUrl } from '../../api/http'
import type { GameSummary, PagedResult } from '../../types/api'

export function GameAuthGuard({ children }: { children: React.ReactNode }) {
  const navigate = useNavigate()
  const location = useLocation()
  const [allowed, setAllowed] = useState<boolean | undefined>(undefined)

  useEffect(() => {
    let cancelled = false
    ;(async () => {
      const baseUrl = getApiUrl('/api/games')
      const url = new URL(baseUrl)
      url.searchParams.set('status', 'active')
      url.searchParams.set('sort', 'lastTurnAt')
      url.searchParams.set('order', 'desc')
      url.searchParams.set('pageSize', '1')

      const { ok, status } = await getJson<PagedResult<GameSummary>>(url.toString())
      if (cancelled) return

      if (!ok && (status === 401 || status === 403)) {
        const returnUrl = encodeURIComponent(location.pathname + location.search)
        navigate(`/login?returnUrl=${returnUrl}`, { replace: true })
        setAllowed(false)
        return
      }
      // Network/5xx: do not block entry; soft banners handled elsewhere
      setAllowed(true)
    })()
    return () => {
      cancelled = true
    }
  }, [location.pathname, location.search, navigate])

  if (allowed === undefined) return null
  if (allowed === false) return null
  return <>{children}</>
}

