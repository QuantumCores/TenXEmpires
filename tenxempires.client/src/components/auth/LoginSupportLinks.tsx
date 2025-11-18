import { Link, useSearchParams } from 'react-router-dom'

export function LoginSupportLinks() {
  const [searchParams] = useSearchParams()

  // Preserve returnUrl when navigating
  const returnUrl = searchParams.get('returnUrl')
  const returnUrlParam = returnUrl ? `?returnUrl=${encodeURIComponent(returnUrl)}` : ''

  const resetUrl = `/reset-password${returnUrlParam}`
  const registerUrl = `/register${returnUrlParam}`

  return (
    <div data-testid="login-support-links" className="mt-6 flex items-center justify-between text-sm">
      <Link
        to={resetUrl}
        data-testid="login-forgot-password-link"
        className="text-indigo-600 hover:text-indigo-500 hover:underline focus:outline-none focus:underline"
      >
        Forgot password?
      </Link>
      <Link
        to={registerUrl}
        data-testid="login-create-account-link"
        className="text-indigo-600 hover:text-indigo-500 hover:underline focus:outline-none focus:underline"
      >
        Create account
      </Link>
    </div>
  )
}

