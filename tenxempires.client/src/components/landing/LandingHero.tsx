import type { ReactNode } from 'react'

interface LandingHeroProps {
  cta: ReactNode
}

export function LandingHero({ cta }: LandingHeroProps) {
  return (
    <section className="mx-auto max-w-4xl px-6 py-16 text-center">
      <h1 className="text-4xl font-extrabold tracking-tight text-slate-900">TenX Empires</h1>
      <p className="mx-auto mt-4 max-w-2xl text-balance text-slate-600">
        Turn-based, deterministic strategy on a compact map. Make smart moves, outplay the AI, and expand your empire.
      </p>
      <div className="mt-8 flex items-center justify-center gap-4">{cta}</div>
    </section>
  )
}

