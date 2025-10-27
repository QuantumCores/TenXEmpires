import { useMemo } from 'react'
import { useNavigate } from 'react-router-dom'
import type React from 'react'
import { ModalContainer } from './ModalContainer'
import { useModalParam } from '../../router/query'
import { useBackstackCloseBehavior } from '../../router/backstack'

export type ModalKey =
  | 'saves'
  | 'settings'
  | 'help'
  | 'account-delete'
  | 'start-new'
  | 'session-expired'
  | 'error-schema'
  | 'error-ai'

type ModalProps = { onRequestClose: () => void }

function PlaceholderModal({ title, onRequestClose }: { title: string } & ModalProps) {
  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center">
      <div className="absolute inset-0 bg-black/50" onClick={onRequestClose} />
      <div role="dialog" aria-modal className="relative z-10 w-[32rem] rounded bg-white p-4 shadow-xl">
        <div className="flex items-center justify-between">
          <h2 className="text-lg font-semibold">{title}</h2>
          <button className="rounded px-2 py-1 hover:bg-slate-100" onClick={onRequestClose} aria-label="Close">
            ✕
          </button>
        </div>
        <div className="mt-3 text-sm text-slate-600">Placeholder content</div>
      </div>
    </div>
  )
}

const ModalComponents: Record<ModalKey, (p: ModalProps) => React.ReactElement> = {
  'saves': (p) => <PlaceholderModal title="Saves" {...p} />,
  'settings': (p) => <PlaceholderModal title="Settings" {...p} />,
  'help': (p) => <PlaceholderModal title="Help" {...p} />,
  'account-delete': (p) => <PlaceholderModal title="Delete Account" {...p} />,
  'start-new': (p) => <PlaceholderModal title="Start New Game" {...p} />,
  'session-expired': (p) => <PlaceholderModal title="Session Expired" {...p} />,
  'error-schema': (p) => <PlaceholderModal title="Schema Error" {...p} />,
  'error-ai': (p) => <PlaceholderModal title="AI Timeout" {...p} />,
}

export function ModalManager({
  gameId: _gameId,
  status: _status,
  searchParams: _searchParams,
}: {
  gameId: string
  status?: 'online' | 'offline' | 'limited'
  searchParams?: URLSearchParams
}) {
  useNavigate() // ensure hook context; not needed directly here
  const { state, closeModal } = useModalParam()
  useBackstackCloseBehavior()

  const modalKey = useMemo(() => state.modal, [state.modal])
  if (!modalKey) return null

  const Comp = ModalComponents[modalKey]
  return (
    <ModalContainer onRequestClose={() => closeModal('replace')}>
      <Comp onRequestClose={() => closeModal('replace')} />
    </ModalContainer>
  )
}
