import { useMemo, useState } from 'react'
import type { FormEvent } from 'react'
import { useNavigate } from 'react-router-dom'
import { postJson } from '../../api/http'
import { useQueryClient } from '@tanstack/react-query'

export default function Login() {
  const navigate = useNavigate()
  const qc = useQueryClient()
  const returnUrl = useMemo(() => {
    const params = new URLSearchParams(window.location.search)
    return params.get('returnUrl') ?? '/'
  }, [])
  const [email, setEmail] = useState('')
  const [password, setPassword] = useState('')
  const [remember, setRemember] = useState(true)
  const [error, setError] = useState<string | undefined>()
  const [submitting, setSubmitting] = useState(false)

  async function onSubmit(e: FormEvent) {
    e.preventDefault()
    setSubmitting(true)
    setError(undefined)
    const { ok, status, data } = await postJson<{ email: string; password: string; rememberMe: boolean }, { code?: string; message?: string }>(
      '/v1/auth/login',
      { email, password, rememberMe: remember },
    )
    setSubmitting(false)
    if (ok) {
      qc.invalidateQueries({ queryKey: ['auth-status'] })
      navigate(returnUrl, { replace: true })
      return
    }
    const code = (data as any)?.code || status
    const message = (data as any)?.message || 'Unable to sign in.'
    setError(`${code}: ${message}`)
  }

  return (
    <main className="mx-auto max-w-md p-6">
      <h1 className="text-2xl font-semibold">Sign in</h1>
      <p className="mt-2 text-slate-600">You must sign in to continue.</p>
      <form onSubmit={onSubmit} className="mt-6 space-y-4">
        <div>
          <label className="block text-sm font-medium text-slate-700">Email</label>
          <input
            type="email"
            required
            autoComplete="email"
            className="mt-1 w-full rounded-md border border-slate-300 px-3 py-2 shadow-sm focus:border-indigo-500 focus:outline-none"
            value={email}
            onChange={(e) => setEmail(e.target.value)}
          />
        </div>
        <div>
          <label className="block text-sm font-medium text-slate-700">Password</label>
          <input
            type="password"
            required
            autoComplete="current-password"
            className="mt-1 w-full rounded-md border border-slate-300 px-3 py-2 shadow-sm focus:border-indigo-500 focus:outline-none"
            value={password}
            onChange={(e) => setPassword(e.target.value)}
          />
        </div>
        <div className="flex items-center gap-2">
          <input id="remember" type="checkbox" className="h-4 w-4" checked={remember} onChange={(e) => setRemember(e.target.checked)} />
          <label htmlFor="remember" className="text-sm text-slate-700">Remember me</label>
        </div>
        {error && <div role="alert" className="rounded-md border border-rose-300 bg-rose-50 p-2 text-sm text-rose-700">{error}</div>}
        <button
          type="submit"
          disabled={submitting}
          className="inline-flex w-full items-center justify-center rounded-md bg-indigo-600 px-4 py-2 text-sm font-medium text-white hover:bg-indigo-700 focus:outline-none focus-visible:ring-2 focus-visible:ring-indigo-500 disabled:opacity-50"
        >
          {submitting ? 'Signing in…' : 'Sign in'}
        </button>
      </form>
      <div className="mt-4 text-xs text-slate-500">After sign-in you will be sent to: <code>{returnUrl}</code></div>
    </main>
  )
}
