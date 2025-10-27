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
        className="inline-flex items-center justify-center rounded-md bg-indigo-600 px-5 py-2.5 text-sm font-medium text-white shadow hover:bg-indigo-700 focus:outline-none focus-visible:ring-2 focus-visible:ring-indigo-500"
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
        className="inline-flex items-center justify-center rounded-md bg-indigo-600 px-5 py-2.5 text-sm font-medium text-white shadow hover:bg-indigo-700 focus:outline-none focus-visible:ring-2 focus-visible:ring-indigo-500"
      >
        Login
      </Link>
      <Link
        to={`/register?returnUrl=${returnUrl}`}
        className="inline-flex items-center justify-center rounded-md border border-slate-300 bg-white px-5 py-2.5 text-sm font-medium text-slate-900 shadow-sm hover:bg-slate-50 focus:outline-none focus-visible:ring-2 focus-visible:ring-indigo-500"
      >
        Register
      </Link>
    </>
  )
}

