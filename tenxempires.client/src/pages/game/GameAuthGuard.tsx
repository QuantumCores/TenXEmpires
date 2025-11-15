import { useEffect, useState } from 'react'
import { useLocation, useNavigate } from 'react-router-dom'
import { useAuthStatusQuery } from '../../features/auth/useAuthStatusQuery'

export function GameAuthGuard({ children }: { children: React.ReactNode }) {
  const navigate = useNavigate()
  const location = useLocation()
  const [allowed, setAllowed] = useState<boolean | undefined>(undefined)
  const { data: authResult } = useAuthStatusQuery()
  const authStatus = authResult?.status

  useEffect(() => {
    if (authStatus === undefined) return

    if (authStatus === 401 || authStatus === 403) {
      const returnUrl = encodeURIComponent(location.pathname + location.search)
      navigate(`/login?returnUrl=${returnUrl}`, { replace: true })
      setAllowed(false)
      return
    }

    setAllowed(true)
  }, [authStatus, location.pathname, location.search, navigate])

  if (allowed === undefined) return null
  if (allowed === false) return null
  return <>{children}</>
}
