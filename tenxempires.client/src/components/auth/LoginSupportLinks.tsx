import { Link, useNavigate, useSearchParams } from 'react-router-dom'

export function LoginSupportLinks() {
  const navigate = useNavigate()
  const [searchParams] = useSearchParams()

  // Preserve returnUrl when navigating
  const returnUrl = searchParams.get('returnUrl')
  const returnUrlParam = returnUrl ? `?returnUrl=${encodeURIComponent(returnUrl)}` : ''

  // Build modal URLs
  const forgotPasswordUrl = `/login?modal=forgot${returnUrl ? `&returnUrl=${encodeURIComponent(returnUrl)}` : ''}`
  const registerUrl = `/register${returnUrlParam}`

  return (
    <div className="mt-6 flex items-center justify-between text-sm">
      <button
        type="button"
        onClick={() => {
          // Use React Router navigate for SPA navigation
          navigate(forgotPasswordUrl, { replace: true })
        }}
        className="text-indigo-600 hover:text-indigo-500 hover:underline focus:outline-none focus:underline"
      >
        Forgot password?
      </button>
      <Link
        to={registerUrl}
        className="text-indigo-600 hover:text-indigo-500 hover:underline focus:outline-none focus:underline"
      >
        Create account
      </Link>
    </div>
  )
}

