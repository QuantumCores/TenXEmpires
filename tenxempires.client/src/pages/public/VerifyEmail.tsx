import { useEffect, useMemo, useState } from 'react'
import { Link, useSearchParams } from 'react-router-dom'
import { postJson } from '../../api/http'
import type {
  ApiError,
  ConfirmEmailRequest,
  ResendVerificationRequest,
} from '../../types/auth'

type VerificationStatus = 'idle' | 'verifying' | 'success' | 'error'

export default function VerifyEmail() {
  const [searchParams] = useSearchParams()
  const emailParam = searchParams.get('email') ?? ''
  const token = searchParams.get('token')

  const [email, setEmail] = useState(emailParam)
  const [status, setStatus] = useState<VerificationStatus>(token ? 'verifying' : 'idle')
  const [statusMessage, setStatusMessage] = useState<string | undefined>()
  const [isResending, setIsResending] = useState(false)
  const [resendMessage, setResendMessage] = useState<string | undefined>()

  useEffect(() => {
    setEmail(emailParam)
  }, [emailParam])

  useEffect(() => {
    if (!token || !emailParam) {
      return
    }

    let cancelled = false
    async function confirmEmail() {
      setStatus('verifying')
      setStatusMessage(undefined)
      const { ok, status, data } = await postJson<ConfirmEmailRequest, ApiError>(
        '/api/auth/confirm-email',
        { email: emailParam, token },
      )
      if (cancelled) {
        return
      }
      if (ok || status === 204) {
        setStatus('success')
      } else {
        setStatus('error')
        setStatusMessage(data?.message ?? 'Unable to verify your email. The link may have expired.')
      }
    }
    confirmEmail()
    return () => {
      cancelled = true
    }
  }, [emailParam, token])

  const heading = useMemo(() => {
    if (token) {
      if (status === 'success') {
        return 'Email verified'
      }
      if (status === 'error') {
        return 'Verification failed'
      }
      return 'Verifying your email…'
    }
    return 'Check your inbox'
  }, [status, token])

  async function handleResend(e: React.FormEvent) {
    e.preventDefault()
    setResendMessage(undefined)

    if (!email) {
      setResendMessage('Please enter your email address.')
      return
    }

    setIsResending(true)
    const { ok, status, data } = await postJson<ResendVerificationRequest, ApiError>(
      '/api/auth/resend-verification',
      { email },
    )
    setIsResending(false)

    if (ok || status === 204) {
      setResendMessage('If an account exists, a new verification email is on its way.')
      return
    }

    if (status === 0) {
      setResendMessage('Network error. Please check your connection and try again.')
      return
    }

    setResendMessage(data?.message ?? 'Unable to resend verification email. Please try again.')
  }

  return (
    <main className="mx-auto max-w-lg px-6 py-10">
      <div className="rounded-2xl border border-slate-200 bg-white p-8 shadow-sm">
        <div className="space-y-4 text-center">
          <h1 className="text-2xl font-semibold text-slate-900">{heading}</h1>
          {token ? (
            <VerificationState status={status} message={statusMessage} />
          ) : (
            <p className="text-slate-600">
              We sent a verification link to <strong>{email || 'your email address'}</strong>. Please
              check your inbox and click the link to finish creating your account.
            </p>
          )}
          <div className="space-y-2 text-sm text-slate-600">
            <p>Didn&rsquo;t receive the email? Check Spam/Junk or resend below.</p>
            <form onSubmit={handleResend} className="space-y-3 text-left">
              <label className="block text-sm font-medium text-slate-700">
                Email address
                <input
                  type="email"
                  className="mt-1 w-full rounded-md border border-slate-300 px-3 py-2 shadow-sm focus:border-indigo-500 focus:outline-none focus:ring-1 focus:ring-indigo-500"
                  value={email}
                  onChange={(e) => setEmail(e.target.value)}
                  required
                />
              </label>
              <button
                type="submit"
                disabled={isResending}
                className="w-full rounded-md bg-indigo-600 px-4 py-2 text-sm font-medium text-white hover:bg-indigo-700 focus:outline-none focus-visible:ring-2 focus-visible:ring-indigo-500 focus-visible:ring-offset-2 disabled:cursor-not-allowed disabled:opacity-60"
              >
                {isResending ? 'Sending…' : 'Resend verification email'}
              </button>
            </form>
            {resendMessage && <p className="text-sm text-slate-700">{resendMessage}</p>}
          </div>
        </div>

        <div className="mt-8 flex flex-wrap justify-center gap-4 text-sm font-medium text-indigo-600">
          <Link className="hover:underline" to="/login">
            Back to sign in
          </Link>
          <span className="text-slate-300" aria-hidden="true">
            |
          </span>
          <Link className="hover:underline" to="/register">
            Create another account
          </Link>
        </div>
      </div>
    </main>
  )
}

function VerificationState({ status, message }: { status: VerificationStatus; message?: string }) {
  if (status === 'verifying') {
    return (
      <p className="text-slate-600">
        Hang tight—this only takes a moment. You can close this tab once we confirm your email.
      </p>
    )
  }

  if (status === 'success') {
    return (
      <p className="text-green-600">
        Your email is confirmed. You can now{' '}
        <Link to="/login" className="font-semibold text-indigo-600 hover:underline">
          sign in
        </Link>
        .
      </p>
    )
  }

  if (status === 'error') {
    return (
      <p className="text-rose-600">
        {message ?? 'Unable to verify your email. Please request a new link and try again.'}
      </p>
    )
  }

  return null
}
