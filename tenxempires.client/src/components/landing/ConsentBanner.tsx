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
    <div className="fixed inset-x-0 bottom-0 z-50 border-t border-slate-200 bg-white/95 px-4 py-3 shadow backdrop-blur-sm" role="region" aria-label="Analytics consent">
      <div className="mx-auto flex max-w-5xl flex-col items-start gap-3 sm:flex-row sm:items-center sm:justify-between">
        <p className="text-sm text-slate-700">
          We use minimal analytics to improve gameplay. See <Link className="underline" to="/privacy">Privacy</Link> and <Link className="underline" to="/cookies">Cookies</Link>.
        </p>
        <div className="flex gap-2">
          <button
            type="button"
            className="inline-flex items-center justify-center rounded-md border border-slate-300 bg-white px-3 py-1.5 text-sm font-medium text-slate-900 shadow-sm hover:bg-slate-50 focus:outline-none focus-visible:ring-2 focus-visible:ring-indigo-500"
            onClick={decline}
            aria-label="Decline analytics"
          >
            Decline
          </button>
          <button
            type="button"
            className="inline-flex items-center justify-center rounded-md bg-indigo-600 px-3 py-1.5 text-sm font-medium text-white shadow hover:bg-indigo-700 focus:outline-none focus-visible:ring-2 focus-visible:ring-indigo-500"
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
