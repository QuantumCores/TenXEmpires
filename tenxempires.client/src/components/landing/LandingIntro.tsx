import type { ReactNode } from 'react'

interface LandingIntroProps {
  cta: ReactNode
}

export function LandingIntro({ cta }: LandingIntroProps) {
  return (
    <section className="relative isolate overflow-hidden rounded-3xl border border-slate-700/60 bg-slate-900/70 px-6 py-20 text-center shadow-2xl backdrop-blur supports-[backdrop-filter]:bg-slate-900/50 sm:py-28">
      <div className="pointer-events-none absolute -inset-x-10 -top-24 -z-10 h-64 bg-gradient-to-b from-indigo-500/30 to-transparent blur-3xl"></div>
      <div className="mx-auto max-w-3xl">
        <span className="inline-flex items-center gap-2 rounded-full bg-indigo-500/20 px-3 py-1 text-xs font-medium text-indigo-300 ring-1 ring-inset ring-indigo-400/40">
          New
          <span className="text-indigo-200">Deterministic strategy</span>
        </span>
        <h1 className="mt-6 text-pretty text-5xl font-extrabold tracking-tight text-white sm:text-6xl">
          Conquer smarter with
          {' '}<span className="bg-gradient-to-r from-indigo-400 to-violet-400 bg-clip-text text-transparent">TenX Empires</span>
        </h1>
        <p className="mx-auto mt-6 max-w-2xl text-balance text-base leading-7 text-slate-300 sm:text-lg">
          A crisp, fast turn-based 4X on a compact map. Every move is predictable—outplay the AI with pure strategy.
        </p>
        <div className="mt-10 flex flex-col items-center justify-center gap-3 sm:flex-row sm:gap-4">
          {cta}
          <a href="/about" className="inline-flex items-center justify-center rounded-md border border-slate-600 bg-slate-800 px-5 py-2.5 text-sm font-medium text-slate-100 shadow-sm hover:bg-slate-700 focus:outline-none focus-visible:ring-2 focus-visible:ring-indigo-400">
            Learn more
          </a>
        </div>
      </div>
    </section>
  )
}
