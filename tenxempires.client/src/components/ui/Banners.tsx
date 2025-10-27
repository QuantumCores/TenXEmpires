import { useEffect } from 'react'
import { useNotifications } from './notifications'

export function Banners() {
  const { banners, add, remove } = useNotifications()

  useEffect(() => {
    function handleOffline() {
      add({ id: 'offline', kind: 'warning', message: 'Offline. Some actions may not work.' })
    }
    function handleOnline() {
      remove('offline')
    }
    window.addEventListener('offline', handleOffline)
    window.addEventListener('online', handleOnline)
    if (!navigator.onLine) handleOffline()
    return () => {
      window.removeEventListener('offline', handleOffline)
      window.removeEventListener('online', handleOnline)
    }
  }, [add, remove])

  if (banners.length === 0) return null

  return (
    <div className="fixed inset-x-0 bottom-20 z-40 px-4" role="region" aria-label="Status banners">
      <div className="mx-auto flex max-w-5xl flex-col items-stretch gap-2" aria-live="polite">
        {banners.map((b) => (
          <div
            key={b.id}
            className={[
              'rounded-md px-3 py-2 text-sm shadow',
              b.kind === 'info' && 'bg-sky-50 text-sky-900 border border-sky-200',
              b.kind === 'warning' && 'bg-amber-50 text-amber-900 border border-amber-200',
              b.kind === 'error' && 'bg-rose-50 text-rose-900 border border-rose-200',
            ].filter(Boolean).join(' ')}
          >
            {b.message}
          </div>
        ))}
      </div>
    </div>
  )
}

