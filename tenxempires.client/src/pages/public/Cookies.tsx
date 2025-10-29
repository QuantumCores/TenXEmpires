import { PublicLayout } from '../../components/layouts/PublicLayout'

export default function Cookies(): React.JSX.Element {
  return (
    <PublicLayout maxWidth="full">
      <article className="py-12">
        <div className="rounded-3xl border border-slate-700/60 bg-slate-900/70 px-8 py-12 backdrop-blur supports-[backdrop-filter]:bg-slate-900/50">
          <h1 className="text-4xl font-bold text-white">Cookie Policy</h1>
          <div className="mt-6 space-y-4 text-slate-300">
            <p className="text-lg leading-7">
              We use cookies to enhance your experience.
            </p>
            <p className="leading-7">
              Essential cookies are required for authentication and session management. 
              Analytics cookies are optional and help us understand how you use the game.
            </p>
            <p className="leading-7">
              You can manage your cookie preferences through the consent banner or your browser settings.
            </p>
          </div>
        </div>
      </article>
    </PublicLayout>
  )
}

