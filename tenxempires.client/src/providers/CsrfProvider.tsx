import { createContext, useCallback, useContext, useEffect, useMemo, useState } from 'react'

type CsrfStatus = 'idle' | 'initializing' | 'ready' | 'error'

interface CsrfContextValue {
  status: CsrfStatus
  refresh: () => Promise<void>
  lastError?: string
}

const CsrfContext = createContext<CsrfContextValue | undefined>(undefined)

async function requestCsrf(): Promise<{ ok: boolean; status: number }> {
  try {
    const res = await fetch('/v1/auth/csrf', {
      method: 'GET',
      credentials: 'include',
      headers: { 'Accept': 'application/json' },
    })
    return { ok: res.ok, status: res.status }
  } catch (err: unknown) {
    console.error('[CSRF] Request failed:', err)
    return { ok: false, status: 0 }
  }
}

export function CsrfProvider({ children }: { children: React.ReactNode }) {
  const [status, setStatus] = useState<CsrfStatus>('idle')
  const [lastError, setLastError] = useState<string | undefined>(undefined)

  const refresh = useCallback(async () => {
    setStatus('initializing')
    setLastError(undefined)
    const res = await requestCsrf()
    if (res.ok) {
      setStatus('ready')
    } else {
      // Retry once after brief delay
      await new Promise((r) => setTimeout(r, 300))
      const retry = await requestCsrf()
      if (retry.ok) setStatus('ready')
      else {
        setStatus('error')
        setLastError(`Failed to init CSRF (${retry.status || 'network'})`)
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

export function useCsrf() {
  const ctx = useContext(CsrfContext)
  if (!ctx) throw new Error('useCsrf must be used within CsrfProvider')
  return ctx
}

