import { postJson } from '../../api/http'
import { useConsent } from '../consent/useConsent'

export interface AnalyticsEvent {
  type: string
  ts: string
  props?: Record<string, unknown>
}

export async function sendAnalyticsBatch(events: AnalyticsEvent[]) {
  // Honor consent: do not send when declined or not decided
  const { decided, accepted } = useConsent.getState()
  if (!decided || !accepted) return { skipped: true }
  const body = { events }
  const res = await postJson<typeof body, { ok?: boolean; message?: string }>('/api/analytics/batch', body)
  return res
}
