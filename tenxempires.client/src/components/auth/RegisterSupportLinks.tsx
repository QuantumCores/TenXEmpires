import { Link, useSearchParams } from 'react-router-dom'

export function RegisterSupportLinks() {
  const [searchParams] = useSearchParams()

  // Preserve returnUrl when navigating back to login
  const returnUrl = searchParams.get('returnUrl')
  const loginUrl = `/login${returnUrl ? `?returnUrl=${encodeURIComponent(returnUrl)}` : ''}`

  return (
    <div className="mt-6 text-center text-sm">
      <span className="text-slate-600">Already have an account? </span>
      <Link
        to={loginUrl}
        className="text-indigo-600 hover:text-indigo-500 hover:underline focus:outline-none focus:underline"
      >
        Sign in
      </Link>
    </div>
  )
}

