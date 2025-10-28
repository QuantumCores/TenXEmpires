import { useSearchParams } from 'react-router-dom'

export function ActionRail() {
  const [, setSearchParams] = useSearchParams()

  const openModal = (modal: string) => {
    setSearchParams({ modal })
  }

  return (
    <div className="absolute right-4 top-20 flex flex-col gap-2">
      <button
        type="button"
        className="rounded-lg border border-slate-300 bg-white px-3 py-2 text-sm shadow hover:bg-slate-50"
        onClick={() => openModal('saves')}
        aria-label="Open saves"
      >
        Saves
      </button>
      <button
        type="button"
        className="rounded-lg border border-slate-300 bg-white px-3 py-2 text-sm shadow hover:bg-slate-50"
        onClick={() => openModal('settings')}
        aria-label="Open settings"
      >
        Settings
      </button>
      <button
        type="button"
        className="rounded-lg border border-slate-300 bg-white px-3 py-2 text-sm shadow hover:bg-slate-50"
        onClick={() => openModal('help')}
        aria-label="Open help"
      >
        Help
      </button>
    </div>
  )
}

