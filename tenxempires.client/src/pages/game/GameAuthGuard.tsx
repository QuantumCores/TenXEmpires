import { useEffect, useState } from 'react'
import { useLocation, useNavigate } from 'react-router-dom'
import { getApiUrl } from '../../api/http'

export function GameAuthGuard({ children }: { children: React.ReactNode }) {
  const navigate = useNavigate()
  const location = useLocation()
  const [allowed, setAllowed] = useState<boolean | undefined>(undefined)

  useEffect(() => {
    let cancelled = false
    ;(async () => {
      // Build query string first, then append to path
      const queryParams = new URLSearchParams({
        status: 'active',
        sort: 'lastTurnAt',
        order: 'desc',
        pageSize: '1',
      })
      // Use getApiUrl to handle dev mode (relative) vs Docker/CI (absolute URL)
      // getApiUrl preserves query strings, so we can include them in the path
      const url = getApiUrl(`/api/games?${queryParams.toString()}`)

      // Use fetch directly since we've already processed the URL via getApiUrl
      const res = await fetch(url, {
        method: 'GET',
        credentials: 'include',
        headers: { 'Accept': 'application/json' },
      })
      if (cancelled) return

      const status = res.status
      const ok = res.ok
      
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

