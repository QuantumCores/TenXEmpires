import { createContext, useCallback, useContext, useEffect, useMemo, useState } from 'react'
import { refreshCsrfToken } from '../api/csrf'

type CsrfStatus = 'idle' | 'initializing' | 'ready' | 'error'

interface CsrfContextValue {
  status: CsrfStatus
  refresh: () => Promise<void>
  lastError?: string
}

const CsrfContext = createContext<CsrfContextValue | undefined>(undefined)

export function CsrfProvider({ children }: { children: React.ReactNode }) {
  const [status, setStatus] = useState<CsrfStatus>('idle')
  const [lastError, setLastError] = useState<string | undefined>(undefined)

  const refresh = useCallback(async () => {
    setStatus('initializing')
    setLastError(undefined)
    const res = await refreshCsrfToken()
    if (res) {
      setStatus('ready')
    } else {
      // Retry once after brief delay
      await new Promise((r) => setTimeout(r, 300))
      const retry = await refreshCsrfToken()
      if (retry) setStatus('ready')
      else {
        setStatus('error')
        setLastError('Failed to init CSRF')
      }
    }
  }, [])

  useEffect(() => {
    // Initialize on mount
    void refresh()
  }, [refresh])

  const value = useMemo<CsrfContextValue>(() => ({ status, refresh, lastError }), [status, refresh, lastError])
  return <CsrfContext.Provider value={value}>{children}</CsrfContext.Provider>
}

// eslint-disable-next-line react-refresh/only-export-components
export function useCsrf() {
  const ctx = useContext(CsrfContext)
  if (!ctx) throw new Error('useCsrf must be used within CsrfProvider')
  return ctx
}
