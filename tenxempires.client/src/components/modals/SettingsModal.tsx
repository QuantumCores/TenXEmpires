import { useId } from 'react'
import { useGameMapStore } from '../../features/game/useGameMapStore'

export interface SettingsModalProps {
  onRequestClose: () => void
}

export function SettingsModal({ onRequestClose }: SettingsModalProps) {
  const titleId = useId()
  const gridOn = useGameMapStore((s) => s.gridOn)
  const invertZoom = useGameMapStore((s) => s.invertZoom)
  const debug = useGameMapStore((s) => s.debug)
  const toggleGrid = useGameMapStore((s) => s.toggleGrid)
  const toggleInvertZoom = useGameMapStore((s) => s.toggleInvertZoom)
  const toggleDebug = useGameMapStore((s) => s.toggleDebug)

  return (
    <div className="flex flex-col gap-4">
      <div className="flex items-center justify-between">
        <h2 id={titleId} className="text-lg font-semibold">Settings</h2>
        <button
          type="button"
          className="rounded px-2 py-1 hover:bg-slate-100"
          onClick={onRequestClose}
          aria-label="Close"
        >
          ?
        </button>
      </div>

      <p className="text-sm text-slate-600">
        Player preferences. These settings only affect your current device and session. Tip: Press <kbd className="rounded bg-slate-100 px-1">S</kbd> to open Settings.
      </p>

      <SettingsList>
        <ToggleRow
          id="setting-grid"
          label="Show Grid Overlay"
          description="Display thin hex lines over the map."
          checked={gridOn}
          onToggle={toggleGrid}
        />
        <ToggleRow
          id="setting-invert-zoom"
          label="Invert Scroll Zoom"
          description="Reverse the direction of scroll wheel zoom."
          checked={invertZoom}
          onToggle={toggleInvertZoom}
        />
        {import.meta.env.DEV && (
          <ToggleRow
            id="setting-debug"
            label="Debug Mode"
            description="Enable extra debug visuals and logs (development only)."
            checked={!!debug}
            onToggle={toggleDebug}
          />
        )}
      </SettingsList>

      <FooterActions onClose={onRequestClose} />
    </div>
  )
}

function SettingsList({ children }: { children: React.ReactNode }) {
  return (
    <div className="flex flex-col divide-y divide-slate-200 rounded border border-slate-200 bg-white">
      {children}
    </div>
  )
}

function ToggleRow({
  id,
  label,
  description,
  checked,
  onToggle,
}: {
  id: string
  label: string
  description?: string
  checked: boolean
  onToggle: () => void
}) {
  const handleKey = (e: React.KeyboardEvent<HTMLButtonElement>) => {
    if (e.key === 'Enter' || e.key === ' ') {
      e.preventDefault()
      onToggle()
    }
  }

  return (
    <div className="flex items-center justify-between p-3">
      <div>
        <label htmlFor={id} className="block text-sm font-medium text-slate-800">
          {label}
        </label>
        {description && (
          <div className="mt-0.5 text-xs text-slate-500">{description}</div>
        )}
      </div>
      <button
        id={id}
        type="button"
        role="switch"
        aria-checked={checked}
        aria-label={label}
        onClick={onToggle}
        onKeyDown={handleKey}
        className={[
          'relative inline-flex h-6 w-11 items-center rounded-full transition-colors',
          checked ? 'bg-indigo-600' : 'bg-slate-300',
        ].join(' ')}
      >
        <span
          className={[
            'inline-block h-5 w-5 transform rounded-full bg-white shadow transition-transform',
            checked ? 'translate-x-5' : 'translate-x-1',
          ].join(' ')}
        />
      </button>
    </div>
  )
}

function FooterActions({ onClose }: { onClose: () => void }) {
  return (
    <div className="flex items-center justify-end gap-3 border-t border-slate-200 pt-4">
      <button
        type="button"
        className="rounded px-4 py-2 text-sm font-medium text-slate-700 hover:bg-slate-100"
        onClick={onClose}
      >
        Close
      </button>
    </div>
  )
}
