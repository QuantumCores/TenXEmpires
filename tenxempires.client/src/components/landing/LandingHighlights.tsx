export function LandingHighlights() {
  return (
    <section className="mx-auto max-w-4xl px-6 py-10">
      <ul className="grid gap-4 sm:grid-cols-3">
        <li className="rounded-lg border border-slate-200 p-4 text-sm text-slate-700">
          Fixed map for fast turns
        </li>
        <li className="rounded-lg border border-slate-200 p-4 text-sm text-slate-700">
          Deterministic AI under 500 ms
        </li>
        <li className="rounded-lg border border-slate-200 p-4 text-sm text-slate-700">
          Saves and autosaves built-in
        </li>
      </ul>
    </section>
  )
}

