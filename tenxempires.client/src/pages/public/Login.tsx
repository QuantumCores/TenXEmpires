import { useMemo } from 'react'

export default function Login() {
  const returnUrl = useMemo(() => {
    const params = new URLSearchParams(window.location.search)
    return params.get('returnUrl') ?? '/'
  }, [])

  return (
    <main className="mx-auto max-w-3xl p-6">
      <h1 className="text-2xl font-semibold">Sign in</h1>
      <p className="mt-4 text-slate-600">You must sign in to continue.</p>
      <div className="mt-6 text-sm text-slate-500">After sign-in you will be sent to: <code>{returnUrl}</code></div>
      {/* Actual auth flow is server-driven; this is a placeholder. */}
    </main>
  )
}

