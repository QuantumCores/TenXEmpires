import { Link } from 'react-router-dom'

export interface HelpModalProps {
  onRequestClose: () => void
}

export function HelpModal({ onRequestClose }: HelpModalProps) {
  return (
    <div className="flex flex-col gap-4">
      <div className="flex items-center justify-between">
        <h2 className="text-lg font-semibold">Help</h2>
        <button
          type="button"
          className="rounded px-2 py-1 hover:bg-slate-100"
          onClick={onRequestClose}
          aria-label="Close"
        >
          ✕
        </button>
      </div>

      <p className="text-sm text-slate-600">
        Quick reference for controls and map colors. Colors are chosen for clarity and contrast; overlays use transparency to avoid obscuring the map.
      </p>

      <HotkeysSection />
      <LegendSection />
      <LinksSection />
    </div>
  )
}

function HotkeysSection() {
  const Item = ({ k, label }: { k: string; label: string }) => (
    <li className="flex items-center justify-between rounded border border-slate-200 px-3 py-2 text-sm">
      <span className="text-slate-700">{label}</span>
      <kbd className="rounded bg-slate-100 px-2 py-1 font-mono text-[12px] text-slate-700">{k}</kbd>
    </li>
  )

  return (
    <section aria-labelledby="help-hotkeys">
      <h3 id="help-hotkeys" className="mb-2 text-sm font-semibold uppercase tracking-wide text-slate-500">
        Hotkeys
      </h3>
      <ul className="grid grid-cols-1 gap-2 sm:grid-cols-2">
        <Item k="H / ?" label="Toggle Help" />
        <Item k="E" label="End Turn" />
        <Item k="N" label="Next Unit" />
        <Item k="G" label="Toggle Grid" />
        <Item k="ESC" label="Cancel / Deselect" />
        <Item k="+ / =" label="Zoom In" />
        <Item k="-" label="Zoom Out" />
      </ul>
      <p className="mt-2 text-xs text-slate-500">
        Pan by dragging with the mouse. Scroll wheel or trackpad to zoom.
      </p>
    </section>
  )
}

function LegendSection() {
  const Rect = ({ className }: { className: string }) => (
    <div className={["h-6 w-12 rounded-sm", className].join(" ")} aria-hidden />
  )

  const DashedLine = () => (
    <div className="flex h-6 w-12 items-center" aria-hidden>
      <div className="w-full border-t-2 border-amber-400 border-dashed" />
    </div>
  )

  const Hex = ({
    stroke,
    strokeOpacity = 1,
  }: {
    stroke: string
    strokeOpacity?: number
  }) => (
    <svg width="36" height="24" viewBox="0 0 32 22" aria-hidden>
      <polygon
        points="26,11 21,19.66 11,19.66 6,11 11,2.34 21,2.34"
        fill="none"
        stroke={stroke}
        strokeOpacity={strokeOpacity}
        strokeWidth="2"
      />
    </svg>
  )

  const TargetPair = () => (
    <div className="flex items-center gap-1" aria-hidden>
      <Hex stroke="#22c55e" />
      <Hex stroke="#ef4444" />
    </div>
  )

  const Item = ({ icon, label, desc }: { icon: React.ReactNode; label: string; desc: string }) => (
    <li className="flex items-center gap-3 text-sm">
      <div className="flex items-center justify-center rounded border border-slate-200 bg-white p-1">
        {icon}
      </div>
      <div>
        <div className="font-medium text-slate-800">{label}</div>
        <div className="text-xs text-slate-500">{desc}</div>
      </div>
    </li>
  )

  return (
    <section aria-labelledby="help-legend">
      <h3 id="help-legend" className="mb-2 text-sm font-semibold uppercase tracking-wide text-slate-500">
        Map Color Legend
      </h3>
      <ul className="grid grid-cols-1 gap-3 sm:grid-cols-2">
        <Item
          icon={<Rect className="bg-blue-500/20 ring-1 ring-inset ring-blue-500/40" />}
          label="Reachable (Move Range)"
          desc="Blue translucent overlay on tiles you can move to."
        />
        <Item
          icon={<DashedLine />}
          label="Path (Preview)"
          desc="Amber dashed line showing the movement path."
        />
        <Item
          icon={<Rect className="bg-red-500/15 ring-1 ring-inset ring-red-500/40" />}
          label="Targets (Attack Range)"
          desc="Red translucent overlay on tiles you can attack."
        />
        <Item
          icon={<Hex stroke="#fbbf24" />}
          label="Selection"
          desc="Amber outline highlighting the selected unit or city."
        />
        <Item
          icon={<TargetPair />}
          label="Target Tile"
          desc="Green outline for move target; Red outline for attack target."
        />
        <Item
          icon={<Rect className="bg-slate-400/30 ring-1 ring-inset ring-slate-400/40" />}
          label="Blocked"
          desc="Occupied or impassable; not highlighted as reachable."
        />
        <Item
          icon={
            <div className="flex items-center justify-center bg-slate-800 px-1">
              <Hex stroke="#ffffff" strokeOpacity={0.15} />
            </div>
          }
          label="Grid"
          desc="Thin white hex lines at 15% opacity (toggle with G)."
        />
        <Item
          icon={<Rect className="bg-purple-500/15 ring-1 ring-inset ring-purple-500/40" />}
          label="Siege (Planned)"
          desc="Planned purple overlay for siege radius in future updates."
        />
      </ul>
      <p className="mt-2 text-xs text-slate-500">
        Note: Overlays are semi-transparent to maintain readability. Aim for 4.5:1 text contrast where applicable.
      </p>
    </section>
  )
}

function LinksSection() {
  return (
    <section aria-labelledby="help-links">
      <h3 id="help-links" className="mb-2 text-sm font-semibold uppercase tracking-wide text-slate-500">
        Links
      </h3>
      <div className="flex flex-wrap gap-3 text-sm">
        <Link to="/privacy" className="text-indigo-600 hover:text-indigo-700 hover:underline">
          Privacy Policy
        </Link>
        <span aria-hidden className="text-slate-300">•</span>
        <Link to="/cookies" className="text-indigo-600 hover:text-indigo-700 hover:underline">
          Cookies
        </Link>
      </div>
    </section>
  )
}
