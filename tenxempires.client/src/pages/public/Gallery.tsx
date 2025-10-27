import { PublicLayout } from '../../components/layouts/PublicLayout'

export default function Gallery() {
  return (
    <PublicLayout maxWidth="medium">
      <section className="py-12">
        <div className="rounded-3xl border border-slate-700/60 bg-slate-900/70 px-8 py-12 backdrop-blur supports-[backdrop-filter]:bg-slate-900/50">
          <h1 className="text-4xl font-bold text-white">Gallery</h1>
          <p className="mt-4 text-lg text-slate-300">
            Screenshots and media from TenX Empires.
          </p>
          
          <div className="mt-8 grid gap-6 sm:grid-cols-2">
            <div className="rounded-xl border border-slate-700/60 bg-slate-800/50 p-4">
              <div className="aspect-video rounded-lg bg-slate-700/30 flex items-center justify-center">
                <span className="text-sm text-slate-400">Screenshot placeholder</span>
              </div>
              <p className="mt-3 text-sm text-slate-400">Game map view</p>
            </div>
            <div className="rounded-xl border border-slate-700/60 bg-slate-800/50 p-4">
              <div className="aspect-video rounded-lg bg-slate-700/30 flex items-center justify-center">
                <span className="text-sm text-slate-400">Screenshot placeholder</span>
              </div>
              <p className="mt-3 text-sm text-slate-400">Unit combat</p>
            </div>
          </div>
        </div>
      </section>
    </PublicLayout>
  )
}

