import { PublicLayout } from '../../components/layouts/PublicLayout'

export default function Privacy(): React.JSX.Element {
  return (
    <PublicLayout maxWidth="full">
      <article className="py-12">
        <div className="rounded-3xl border border-slate-700/60 bg-slate-900/70 px-8 py-12 backdrop-blur supports-[backdrop-filter]:bg-slate-900/50">
          <h1 className="text-4xl font-bold text-white">Privacy Policy</h1>
          <div className="mt-6 space-y-4 text-slate-300">
            <p className="text-lg leading-7">
              Your privacy matters to us.
            </p>
            <p className="leading-7">
              We collect minimal data necessary to provide gameplay services. This includes your email 
              for account management and optional analytics to improve the game experience.
            </p>
            <p className="leading-7">
              You can opt out of analytics at any time. Your game data is stored securely and will be 
              deleted upon account deletion.
            </p>
          </div>
        </div>
      </article>
    </PublicLayout>
  )
}

