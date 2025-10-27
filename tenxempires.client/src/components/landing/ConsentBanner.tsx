import { useEffect } from 'react'
import { Link } from 'react-router-dom'
import { useConsent } from '../../features/consent/useConsent'

export function ConsentBanner() {
  const { decided, accept, decline, hydrateFromCookie } = useConsent()

  useEffect(() => {
    hydrateFromCookie()
  }, [hydrateFromCookie])

  if (decided) return null

  return (
    <div className="fixed inset-x-0 bottom-0 z-50 border-t border-slate-700/60 bg-slate-900/95 px-4 py-3 shadow-lg backdrop-blur-sm" role="region" aria-label="Analytics consent">
      <div className="mx-auto flex max-w-5xl flex-col items-start gap-3 sm:flex-row sm:items-center sm:justify-between">
        <p className="text-sm text-slate-300">
          We use minimal analytics to improve gameplay. See <Link className="underline hover:text-slate-100" to="/privacy">Privacy</Link> and <Link className="underline hover:text-slate-100" to="/cookies">Cookies</Link>.
        </p>
        <div className="flex gap-2">
          <button
            type="button"
            className="inline-flex items-center justify-center rounded-md border border-slate-600 bg-slate-800 px-3 py-1.5 text-sm font-medium text-slate-100 shadow-sm hover:bg-slate-700 focus:outline-none focus-visible:ring-2 focus-visible:ring-indigo-400"
            onClick={decline}
            aria-label="Decline analytics"
          >
            Decline
          </button>
          <button
            type="button"
            className="inline-flex items-center justify-center rounded-md bg-indigo-600 px-3 py-1.5 text-sm font-medium text-white shadow hover:bg-indigo-700 focus:outline-none focus-visible:ring-2 focus-visible:ring-indigo-400"
            onClick={accept}
            aria-label="Accept analytics"
          >
            Accept
          </button>
        </div>
      </div>
    </div>
  )
}
