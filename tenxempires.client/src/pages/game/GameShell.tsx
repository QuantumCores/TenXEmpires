import { useParams } from 'react-router-dom'
import { ModalManager } from '../../components/modals/ModalManager'
import { useModalParam } from '../../router/query'

export function GameShell() {
  const { id } = useParams<{ id: string }>()
  const { openModal } = useModalParam()

  return (
    <div className="relative min-h-dvh">
      <header className="flex items-center justify-between border-b px-4 py-2">
        <h1 className="text-lg font-semibold">Game {id}</h1>
        {/* Placeholder actions */}
        <div className="flex gap-2 text-sm">
          <button
            onClick={() => openModal('saves')}
            className="rounded bg-blue-600 px-3 py-1.5 text-sm font-medium text-white hover:bg-blue-700"
          >
            Saves
          </button>
          <button
            onClick={() => openModal('start-new')}
            className="rounded bg-slate-600 px-3 py-1.5 text-sm font-medium text-white hover:bg-slate-700"
          >
            Start New
          </button>
        </div>
      </header>

      <main className="p-4">
        <div className="h-[60vh] w-full rounded border bg-slate-50/60" aria-label="Map placeholder" />
      </main>

      <ModalManager gameId={id ?? ''} />
    </div>
  )
}
