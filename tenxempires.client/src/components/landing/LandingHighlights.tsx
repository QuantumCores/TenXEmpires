export function LandingHighlights() {
  return (
    <section className="mx-auto mt-8 max-w-6xl px-2 sm:px-0">
      <ul className="grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
        <li className="group rounded-2xl border border-slate-700/60 bg-slate-900/70 p-5 shadow-lg backdrop-blur supports-[backdrop-filter]:bg-slate-900/50 transition hover:-translate-y-0.5 hover:shadow-xl">
          <div className="flex items-center gap-3">
            <span aria-hidden className="inline-flex h-9 w-9 items-center justify-center rounded-lg bg-indigo-500/20 text-indigo-300">⏱️</span>
            <h3 className="text-sm font-semibold text-white">Fast, compact turns</h3>
          </div>
          <p className="mt-2 text-sm leading-6 text-slate-300">A fixed map keeps decisions tight and turns snappy.</p>
        </li>
        <li className="group rounded-2xl border border-slate-700/60 bg-slate-900/70 p-5 shadow-lg backdrop-blur supports-[backdrop-filter]:bg-slate-900/50 transition hover:-translate-y-0.5 hover:shadow-xl">
          <div className="flex items-center gap-3">
            <span aria-hidden className="inline-flex h-9 w-9 items-center justify-center rounded-lg bg-indigo-500/20 text-indigo-300">♟️</span>
            <h3 className="text-sm font-semibold text-white">Deterministic AI</h3>
          </div>
          <p className="mt-2 text-sm leading-6 text-slate-300">Predictable outcomes—win by out-thinking, not dice rolls.</p>
        </li>
        <li className="group rounded-2xl border border-slate-700/60 bg-slate-900/70 p-5 shadow-lg backdrop-blur supports-[backdrop-filter]:bg-slate-900/50 transition hover:-translate-y-0.5 hover:shadow-xl">
          <div className="flex items-center gap-3">
            <span aria-hidden className="inline-flex h-9 w-9 items-center justify-center rounded-lg bg-indigo-500/20 text-indigo-300">💾</span>
            <h3 className="text-sm font-semibold text-white">Saves built‑in</h3>
          </div>
          <p className="mt-2 text-sm leading-6 text-slate-300">Autosave and manual slots so progress is always safe.</p>
        </li>
      </ul>
    </section>
  )
}
