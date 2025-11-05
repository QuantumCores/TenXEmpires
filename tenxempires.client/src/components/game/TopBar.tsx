interface TopBarProps {
  turnNo: number
  status: string
  turnInProgress: boolean
}

export function TopBar({ turnNo, status, turnInProgress }: TopBarProps) {
  return (
    <header className="absolute left-0 right-0 top-0 z-10 flex items-center justify-between border-b border-slate-300 bg-white/90 px-4 py-2 backdrop-blur-sm">
      <div className="flex items-center gap-4">
        <h1 className="text-lg font-semibold">Turn {turnNo}</h1>
        <div className="flex items-center gap-2">
          <span
            className={`rounded-full px-2 py-1 text-xs font-medium ${
              status === 'active'
                ? 'bg-green-100 text-green-800'
                : 'bg-slate-100 text-slate-800'
            }`}
          >
            {status}
          </span>
          {turnInProgress && (
            <span className="text-xs text-slate-600">AI thinking...</span>
          )}
        </div>
      </div>
      <button
        type="button"
        className="text-sm text-blue-600 hover:text-blue-800"
        onClick={() => {
          // TODO: Open help modal
        }}
      >
        Help
      </button>
    </header>
  )
}

