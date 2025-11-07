import { Link } from 'react-router-dom'
import type { AuthStatus } from '../../types/view'

interface Props {
  auth?: AuthStatus
}

export function AuthAwareCTA({ auth }: Props) {
  const isAuthed = auth?.isAuthenticated === true

  if (isAuthed) {
    return (
      <Link
        to="/game/current"
        className="inline-flex items-center justify-center rounded-md bg-indigo-600 px-6 py-3 text-base font-semibold text-white shadow-lg hover:bg-indigo-700 focus:outline-none focus-visible:ring-2 focus-visible:ring-indigo-400"
      >
        Play
      </Link>
    )
  }

  const returnUrl = encodeURIComponent('/game/current')
  return (
    <>
      <Link
        to={`/login?returnUrl=${returnUrl}`}
        className="inline-flex items-center justify-center rounded-md bg-indigo-600 px-6 py-3 text-base font-semibold text-white shadow-lg hover:bg-indigo-700 focus:outline-none focus-visible:ring-2 focus-visible:ring-indigo-400"
      >
        Login
      </Link>
      <Link
        to={`/register?returnUrl=${returnUrl}`}
        data-testid="register-button"
        className="inline-flex items-center justify-center rounded-md border border-slate-600 bg-slate-800 px-6 py-3 text-base font-semibold text-slate-100 shadow-lg hover:bg-slate-700 focus:outline-none focus-visible:ring-2 focus-visible:ring-indigo-400"
      >
        Register
      </Link>
    </>
  )
}
