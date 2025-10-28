import { useNotifications } from '../ui/notifications'

export function ToastsCenter() {
  const { banners } = useNotifications()

  if (banners.length === 0) return null

  return (
    <div
      className="fixed right-4 top-20 z-40 flex max-w-sm flex-col gap-2"
      role="region"
      aria-live="polite"
      aria-label="Notifications"
    >
      {banners.map((banner) => (
        <div
          key={banner.id}
          className={[
            'animate-slideInRight rounded-lg px-4 py-3 text-sm shadow-lg',
            banner.kind === 'info' && 'bg-blue-50 text-blue-900 border border-blue-200',
            banner.kind === 'warning' && 'bg-amber-50 text-amber-900 border border-amber-200',
            banner.kind === 'error' && 'bg-rose-50 text-rose-900 border border-rose-200',
          ]
            .filter(Boolean)
            .join(' ')}
        >
          {banner.message}
        </div>
      ))}
    </div>
  )
}

