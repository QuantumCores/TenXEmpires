import { useCallback, useEffect, useMemo, useRef, useState } from 'react'
import { keepAlive } from '../api/auth'
import { useNotifications } from '../components/ui/notifications'
import { useModalParam } from '../router/query'
import { useAuthStatusQuery } from '../features/auth/useAuthStatusQuery'

const SESSION_EXPIRY_MS = 30 * 60 * 1000 // 30 minutes (matches backend sliding expiration)
const PRE_EXPIRY_LEAD_MS = 60 * 1000 // Show banner 60s before expiry

export function IdleSessionProvider({ children }: { children: React.ReactNode }) {
  const [showBanner, setShowBanner] = useState(false)
  const [secondsLeft, setSecondsLeft] = useState<number>(60)
  const lastActiveRef = useRef<number>(Date.now())
  const tickTimerRef = useRef<number | null>(null)
  const showTimerRef = useRef<number | null>(null)
  const notifications = useNotifications()
  const { state, openModal } = useModalParam()
  const { data: auth } = useAuthStatusQuery()

  const scheduleTimers = useCallback(() => {
    // Clear existing timers
    if (tickTimerRef.current) window.clearInterval(tickTimerRef.current)
    if (showTimerRef.current) window.clearTimeout(showTimerRef.current)

    const now = Date.now()
    const elapsed = now - lastActiveRef.current
    const untilBanner = Math.max(0, SESSION_EXPIRY_MS - PRE_EXPIRY_LEAD_MS - elapsed)

    // Schedule showing the banner
    showTimerRef.current = window.setTimeout(() => {
      // Compute seconds left at the moment we show
      setShowBanner(true)
      const remainingMs = Math.max(0, SESSION_EXPIRY_MS - (Date.now() - lastActiveRef.current))
      setSecondsLeft(Math.ceil(remainingMs / 1000))

      // Start countdown tick
      tickTimerRef.current = window.setInterval(() => {
        const remaining = Math.max(0, SESSION_EXPIRY_MS - (Date.now() - lastActiveRef.current))
        setSecondsLeft(Math.ceil(remaining / 1000))
        if (remaining <= 0) {
          if (tickTimerRef.current) window.clearInterval(tickTimerRef.current)
          setShowBanner(false)
          // Open session expired modal if not already open
          if (state.modal !== 'session-expired') {
            openModal('session-expired', undefined, 'replace')
          }
        }
      }, 1000)
    }, untilBanner)
  }, [openModal, state.modal])

  // Handle user activity, gated by auth status
  useEffect(() => {
    const isAuthed = auth?.isAuthenticated === true
    if (!isAuthed) {
      setShowBanner(false)
      if (tickTimerRef.current) window.clearInterval(tickTimerRef.current)
      if (showTimerRef.current) window.clearTimeout(showTimerRef.current)
      return
    }
    const onActivity = () => {
      lastActiveRef.current = Date.now()
      setShowBanner(false)
      scheduleTimers()
    }
    const opts: AddEventListenerOptions = { passive: true }
    window.addEventListener('mousemove', onActivity, opts)
    window.addEventListener('keydown', onActivity, opts)
    window.addEventListener('click', onActivity, opts)
    window.addEventListener('scroll', onActivity, opts)
    window.addEventListener('touchstart', onActivity, opts)
    scheduleTimers()
    return () => {
      window.removeEventListener('mousemove', onActivity)
      window.removeEventListener('keydown', onActivity)
      window.removeEventListener('click', onActivity)
      window.removeEventListener('scroll', onActivity)
      window.removeEventListener('touchstart', onActivity)
      if (tickTimerRef.current) window.clearInterval(tickTimerRef.current)
      if (showTimerRef.current) window.clearTimeout(showTimerRef.current)
    }
  }, [scheduleTimers, auth?.isAuthenticated])

  const handleKeepAlive = useCallback(async () => {
    const res = await keepAlive()
    if (res.ok) {
      notifications.add({ id: 'keepalive-ok', kind: 'info', message: 'Session extended.', ttlMs: 2000 })
      // Reset idle timers
      lastActiveRef.current = Date.now()
      setShowBanner(false)
      scheduleTimers()
    } else if (res.status === 401) {
      setShowBanner(false)
      openModal('session-expired', undefined, 'replace')
    } else if (res.status === 429) {
      notifications.add({ id: 'keepalive-rl', kind: 'warning', message: 'Please try again shortly.', ttlMs: 4000 })
    } else {
      notifications.add({ id: 'keepalive-fail', kind: 'error', message: 'Unable to extend session.', ttlMs: 4000 })
    }
  }, [notifications, openModal, scheduleTimers])

  const banner = useMemo(() => {
    if (!showBanner) return null
    return (
      <div className="fixed inset-x-0 bottom-20 z-40 px-4" role="region" aria-label="Session status">
        <div className="mx-auto flex max-w-5xl items-center justify-between rounded-md border border-amber-200 bg-amber-50 px-3 py-2 text-sm text-amber-900 shadow">
          <div>
            Session expires in {secondsLeft}s. Stay signed in?
          </div>
          <div className="flex items-center gap-2">
            <button
              type="button"
              className="rounded border border-amber-300 bg-white px-3 py-1 text-sm text-amber-900 hover:bg-amber-100"
              onClick={() => setShowBanner(false)}
            >
              Dismiss
            </button>
            <button
              type="button"
              className="rounded bg-amber-600 px-3 py-1 text-sm font-medium text-white hover:bg-amber-700"
              onClick={handleKeepAlive}
            >
              Stay signed in
            </button>
          </div>
        </div>
      </div>
    )
  }, [showBanner, secondsLeft, handleKeepAlive])

  return (
    <>
      {children}
      {banner}
    </>
  )
}
