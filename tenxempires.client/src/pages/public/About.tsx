import { PublicLayout } from '../../components/layouts/PublicLayout'

export default function About() {
  return (
    <PublicLayout maxWidth="narrow">
      <article className="py-12">
        <div className="rounded-3xl border border-slate-700/60 bg-slate-900/70 px-8 py-12 backdrop-blur supports-[backdrop-filter]:bg-slate-900/50">
          <h1 className="text-4xl font-bold text-white">About TenX Empires</h1>
          <div className="mt-6 space-y-4 text-slate-300">
            <p className="text-lg leading-7">
              TenX Empires is a crisp, fast turn-based 4X strategy game built for deterministic gameplay. 
              Every move is predictable—outplay the AI with pure strategy.
            </p>
            <p className="leading-7">
              This is a prototype showcasing server-authoritative gameplay with a fixed 20×15 map, 
              minimal UI, and lightning-fast AI turns (under 500ms).
            </p>
            <p className="leading-7">
              The game features autosave and manual save slots, ensuring your progress is always safe. 
              Perfect for quick strategic sessions or longer campaigns.
            </p>
          </div>
        </div>
      </article>
    </PublicLayout>
  )
}

