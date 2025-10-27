import { create } from 'zustand'

// Cookie name for analytics consent
const CONSENT_COOKIE = 'tenx.consent'
type ConsentValue = 'accepted' | 'declined'

function readConsentCookie(): ConsentValue | undefined {
  const m = document.cookie.match(/(?:^|; )tenx\.consent=([^;]*)/)
  return m ? (decodeURIComponent(m[1]) as ConsentValue) : undefined
}

function writeConsentCookie(value: ConsentValue) {
  const days = 365 * 2 // 2 years
  const expires = new Date(Date.now() + days * 24 * 60 * 60 * 1000).toUTCString()
  document.cookie = `${CONSENT_COOKIE}=${encodeURIComponent(value)}; Path=/; SameSite=Lax; Secure; Expires=${expires}`
}

interface ConsentState {
  decided: boolean
  accepted: boolean
  accept: () => void
  decline: () => void
  hydrateFromCookie: () => void
}

export const useConsent = create<ConsentState>((set) => ({
  decided: false,
  accepted: false,
  accept: () => {
    writeConsentCookie('accepted')
    set({ decided: true, accepted: true })
  },
  decline: () => {
    writeConsentCookie('declined')
    set({ decided: true, accepted: false })
  },
  hydrateFromCookie: () => {
    const v = readConsentCookie()
    if (!v) return
    set({ decided: true, accepted: v === 'accepted' })
  },
}))
