import { useEffect, useRef } from 'react'
import { useNavigate } from 'react-router-dom'
import type { ModalKey } from '../components/modals/ModalManager'
import { useModalParam } from './query'

type ModalRouteState = {
  modal?: ModalKey
  confirm?: boolean
}

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

function readModalStateFromUrl(): ModalRouteState {
  const url = new URL(window.location.href)
  const raw = url.searchParams.get('modal')
  const modal = raw && (allowed as string[]).includes(raw) ? (raw as ModalKey) : undefined
  const confirm = url.searchParams.get('confirm') === 'true'
  return { modal, confirm }
}

// Ensures Back unwinds confirm → modal → map.
export function useBackstackCloseBehavior() {
  const navigate = useNavigate()
  const { state } = useModalParam()
  const prevRef = useRef<ModalRouteState>(state)

  useEffect(() => {
    prevRef.current = { modal: state.modal, confirm: state.confirm }
  }, [state.modal, state.confirm])

  useEffect(() => {
    const onPop = () => {
      const prev = prevRef.current
      const next = readModalStateFromUrl()

      // If we were on a confirm step and Back is pressed, first drop confirm but keep modal open
      if (prev.confirm && prev.modal && (!next.modal || next.modal !== prev.modal || next.confirm === false)) {
        const url = new URL(window.location.href)
        url.searchParams.set('modal', prev.modal)
        url.searchParams.delete('confirm')
        navigate(url.pathname + `?${url.searchParams.toString()}`, { replace: true })
        return
      }
      // Else allow normal back behavior (modal → map or route → previous route)
    }
    window.addEventListener('popstate', onPop)
    return () => window.removeEventListener('popstate', onPop)
  }, [navigate])
}

