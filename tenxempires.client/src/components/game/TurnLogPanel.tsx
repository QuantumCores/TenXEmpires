import { useTurnLogStore } from '../../features/game/useGameMapStore'

interface TurnLogPanelProps {
  gameId: number
}

export function TurnLogPanel({ gameId }: TurnLogPanelProps) {
  const { logs, isOpen, toggleOpen } = useTurnLogStore()
  const entries = logs[gameId] || []

  return (
    <div className="absolute left-4 top-20 z-30">
      <button
        type="button"
        className="rounded-lg border border-slate-300 bg-white px-3 py-2 text-sm shadow hover:bg-slate-50"
        onClick={toggleOpen}
        aria-expanded={isOpen}
        aria-controls="turn-log-content"
      >
        Turn Log {entries.length > 0 && `(${entries.length})`}
      </button>

      {isOpen && (
        <div
          id="turn-log-content"
          className="mt-2 max-h-96 w-80 overflow-y-auto rounded-lg border border-slate-300 bg-white shadow-lg"
          role="region"
          aria-label="Turn log"
        >
          {entries.length === 0 ? (
            <div className="p-4 text-center text-sm text-slate-500">No events yet</div>
          ) : (
            <ul className="divide-y divide-slate-200">
              {entries.map((entry) => (
                <li key={entry.id} className="px-4 py-2 text-sm">
                  <div className="flex items-start gap-2">
                    <span className="text-xs text-slate-500">{getIcon(entry.kind)}</span>
                    <span className="flex-1">{entry.text}</span>
                  </div>
                </li>
              ))}
            </ul>
          )}
        </div>
      )}
    </div>
  )
}

function getIcon(kind: string): string {
  switch (kind) {
    case 'move':
      return '→'
    case 'attack':
      return '⚔'
    case 'city':
      return '🏛'
    case 'save':
      return '💾'
    case 'system':
      return 'ℹ'
    default:
      return '•'
  }
}

