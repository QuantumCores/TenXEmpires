import { useMemo, useEffect } from 'react'
import { useNavigate, useSearchParams } from 'react-router-dom'
import type { ModalKey } from '../components/modals/ModalManager'
import { useUiStore } from '../components/ui/uiStore'

export interface ModalRouteState {
  modal?: ModalKey
  tab?: string
  confirm?: boolean
}

function sanitizeModalKey(value: string | null): ModalKey | undefined {
  const allowed: ModalKey[] = [
    'saves',
    'settings',
    'help',
    'account-delete',
    'start-new',
    'session-expired',
    'error-schema',
    'error-ai',
  ]
  return value && (allowed as string[]).includes(value) ? (value as ModalKey) : undefined
}

export function useModalParam() {
  const [sp] = useSearchParams()
  const navigate = useNavigate()
  const setUiModal = useUiStore((s) => s.setModalState)
  const setSessionLocked = useUiStore((s) => s.setSessionLocked)

  const state = useMemo<ModalRouteState>(() => {
    return {
      modal: sanitizeModalKey(sp.get('modal')),
      tab: sp.get('tab') ?? undefined,
      confirm: sp.get('confirm') === 'true',
    }
  }, [sp])

  // Sanitize unknown modal keys from URL and mirror to UI store
  useEffect(() => {
    const raw = sp.get('modal')
    if (raw && !state.modal) {
      const url = new URL(window.location.href)
      url.searchParams.delete('modal')
      url.searchParams.delete('tab')
      url.searchParams.delete('confirm')
      navigate(url.pathname + (url.search ? `?${url.searchParams.toString()}` : ''), { replace: true })
    }
    setUiModal(state.modal)
    // Update session lock globally based on session-expired modal visibility
    setSessionLocked(state.modal === 'session-expired')
  }, [sp, state.modal, navigate, setUiModal, setSessionLocked])

  const openModal = (key: ModalKey, opts?: { tab?: string }, action: 'push' | 'replace' = 'push') => {
    const url = new URL(window.location.href)
    url.searchParams.set('modal', key)
    if (opts?.tab) url.searchParams.set('tab', opts.tab)
    url.searchParams.delete('confirm')
    navigate(url.pathname + `?${url.searchParams.toString()}`, { replace: action === 'replace' })
  }

  const closeModal = (action: 'push' | 'replace' = 'replace') => {
    const url = new URL(window.location.href)
    url.searchParams.delete('modal')
    url.searchParams.delete('tab')
    url.searchParams.delete('confirm')
    navigate(url.pathname + (url.search ? `?${url.searchParams.toString()}` : ''), { replace: action === 'replace' })
  }

  const openConfirm = () => {
    const url = new URL(window.location.href)
    if (!url.searchParams.get('modal')) return
    url.searchParams.set('confirm', 'true')
    navigate(url.pathname + `?${url.searchParams.toString()}`, { replace: true })
  }

  return { state, openModal, closeModal, openConfirm }
}
