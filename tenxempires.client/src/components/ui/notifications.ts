import { create } from 'zustand'

export type BannerKind = 'info' | 'warning' | 'error'

export interface Banner {
  id: string
  kind: BannerKind
  message: string
}

interface NotificationsState {
  banners: Banner[]
  add: (banner: { id?: string; kind: BannerKind; message: string; ttlMs?: number }) => string
  remove: (id: string) => void
}

export const useNotifications = create<NotificationsState>((set, get) => ({
  banners: [],
  add: ({ id, kind, message, ttlMs }) => {
    const newId = id ?? `${Date.now()}-${Math.random().toString(36).slice(2)}`
    const exists = get().banners.some((b) => b.id === newId)
    const next = exists
      ? get().banners.map((b) => (b.id === newId ? { id: newId, kind, message } : b))
      : [...get().banners, { id: newId, kind, message }]
    set({ banners: next })
    if (ttlMs && ttlMs > 0) {
      const timeout = window.setTimeout(() => {
        get().remove(newId)
      }, ttlMs)
      // Fire-and-forget; timers are not tracked to keep store simple
      void timeout
    }
    return newId
  },
  remove: (id: string) => set({ banners: get().banners.filter((b) => b.id !== id) }),
}))

