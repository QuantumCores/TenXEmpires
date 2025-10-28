import { useParams } from 'react-router-dom'
import { ModalManager } from '../../components/modals/ModalManager'

export function GameShell() {
  const { id } = useParams<{ id: string }>()

  return (
    <div className="relative min-h-dvh">
      <header className="flex items-center justify-between border-b px-4 py-2">
        <h1 className="text-lg font-semibold">Game {id}</h1>
        {/* Placeholder actions */}
        <div className="flex gap-2 text-sm text-slate-600">
          <span>Turn</span>
        </div>
      </header>

      <main className="p-4">
        <div className="h-[60vh] w-full rounded border bg-slate-50/60" aria-label="Map placeholder" />
      </main>

      <ModalManager gameId={id ?? ''} />
    </div>
  )
}
