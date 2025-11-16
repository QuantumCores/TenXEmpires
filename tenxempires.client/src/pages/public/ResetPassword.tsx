import { useMemo, useState } from 'react'
import { Link, useSearchParams } from 'react-router-dom'
import { z } from 'zod'
import { postJson } from '../../api/http'
import type {
  ApiError,
  CompleteResetPasswordRequest,
  ForgotPasswordFormModel,
} from '../../types/auth'

const resetSchema = z
  .object({
    password: z.string().min(8, 'Password must be at least 8 characters long.'),
    confirm: z.string(),
  })
  .refine((value) => value.password === value.confirm, {
    message: 'Passwords do not match.',
    path: ['confirm'],
  })

export default function ResetPassword() {
  const [searchParams] = useSearchParams()
  const token = searchParams.get('token')
  const emailParam = searchParams.get('email') ?? ''
  const returnUrl = searchParams.get('returnUrl')

  const [email, setEmail] = useState(emailParam)
  const [password, setPassword] = useState('')
  const [confirm, setConfirm] = useState('')
  const [isSubmitting, setIsSubmitting] = useState(false)
  const [infoMessage, setInfoMessage] = useState<string | undefined>()
  const [error, setError] = useState<string | undefined>()
  const [isComplete, setIsComplete] = useState(false)

  const heading = useMemo(() => {
    if (token) {
      if (isComplete) {
        return 'Password updated'
      }
      return 'Choose a new password'
    }
    if (isComplete) {
      return 'Check your email'
    }
    return 'Reset your password'
  }, [isComplete, token])

  const description = useMemo(() => {
    if (token) {
      if (isComplete) {
        return 'Your password has been reset. You can now sign in with the new password.'
      }
      return 'Enter a new password for your account. Your reset link expires shortly.'
    }
    if (isComplete) {
      return 'If an account exists with the provided email, you will receive reset instructions.'
    }
    return 'Enter your email address and we will send you password reset instructions.'
  }, [isComplete, token])

  async function handleRequestSubmit(e: React.FormEvent) {
    e.preventDefault()
    setError(undefined)
    setInfoMessage(undefined)

    if (!email) {
      setError('Please provide your email address.')
      return
    }

    setIsSubmitting(true)
    const { ok, status } = await postJson<ForgotPasswordFormModel, ApiError>(
      '/api/auth/forgot-password',
      { email },
    )
    setIsSubmitting(false)

    if (ok || status === 204) {
      setIsComplete(true)
      setInfoMessage('If an account exists, we sent reset instructions to your inbox.')
    } else if (status === 0) {
      setError('Network error. Please check your connection and try again.')
    } else {
      setError('Unable to send reset instructions. Please try again later.')
    }
  }

  async function handleResetSubmit(e: React.FormEvent) {
    e.preventDefault()
    setError(undefined)

    const validation = resetSchema.safeParse({ password, confirm })
    if (!validation.success) {
      setError(validation.error.issues[0]?.message ?? 'Password requirements not met.')
      return
    }

    if (!email || !token) {
      setError('Email and token are required.')
      return
    }

    setIsSubmitting(true)
    const { ok, status, data } = await postJson<CompleteResetPasswordRequest, ApiError>(
      '/api/auth/reset-password',
      {
        email,
        token,
        password,
        confirm,
      },
    )
    setIsSubmitting(false)

    if (ok || status === 204) {
      setIsComplete(true)
      setInfoMessage('Password updated successfully. You can now sign in.')
      return
    }

    if (status === 0) {
      setError('Network error. Please check your connection and try again.')
      return
    }

    setError(data?.message ?? 'Unable to reset password. The link may have expired.')
  }

  return (
    <main className="mx-auto max-w-lg px-6 py-10">
      <div className="rounded-2xl border border-slate-200 bg-white p-8 shadow-sm">
        <div className="space-y-2 text-center">
          <h1 className="text-2xl font-semibold text-slate-900">{heading}</h1>
          <p className="text-slate-600">{description}</p>
          {infoMessage && <p className="text-sm text-green-600">{infoMessage}</p>}
          {error && <p className="text-sm text-rose-600">{error}</p>}
        </div>

        {token ? (
          isComplete ? (
            <div className="mt-8 text-center">
              <Link
                className="text-indigo-600 hover:underline"
                to={`/login${returnUrl ? `?returnUrl=${encodeURIComponent(returnUrl)}` : ''}`}
              >
                Continue to sign in
              </Link>
            </div>
          ) : (
            <form onSubmit={handleResetSubmit} className="mt-8 space-y-4">
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
              <label className="block text-sm font-medium text-slate-700">
                New password
                <input
                  type="password"
                  className="mt-1 w-full rounded-md border border-slate-300 px-3 py-2 shadow-sm focus:border-indigo-500 focus:outline-none focus:ring-1 focus:ring-indigo-500"
                  value={password}
                  onChange={(e) => setPassword(e.target.value)}
                  required
                  minLength={8}
                />
              </label>
              <label className="block text-sm font-medium text-slate-700">
                Confirm new password
                <input
                  type="password"
                  className="mt-1 w-full rounded-md border border-slate-300 px-3 py-2 shadow-sm focus:border-indigo-500 focus:outline-none focus:ring-1 focus:ring-indigo-500"
                  value={confirm}
                  onChange={(e) => setConfirm(e.target.value)}
                  required
                  minLength={8}
                />
              </label>
              <button
                type="submit"
                disabled={isSubmitting}
                className="w-full rounded-md bg-indigo-600 px-4 py-2 text-sm font-medium text-white hover:bg-indigo-700 focus:outline-none focus-visible:ring-2 focus-visible:ring-indigo-500 focus-visible:ring-offset-2 disabled:cursor-not-allowed disabled:opacity-60"
              >
                {isSubmitting ? 'Updating…' : 'Update password'}
              </button>
            </form>
          )
        ) : (
          <>
            {isComplete ? null : (
              <form onSubmit={handleRequestSubmit} className="mt-8 space-y-4">
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
                  disabled={isSubmitting}
                  className="w-full rounded-md bg-indigo-600 px-4 py-2 text-sm font-medium text-white hover:bg-indigo-700 focus:outline-none focus-visible:ring-2 focus-visible:ring-indigo-500 focus-visible:ring-offset-2 disabled:cursor-not-allowed disabled:opacity-60"
                >
                  {isSubmitting ? 'Sending…' : 'Send reset instructions'}
                </button>
              </form>
            )}
            <div className="mt-6 text-center text-sm text-slate-600">
              Remembered your password?{' '}
              <Link
                className="font-medium text-indigo-600 hover:underline"
                to={`/login${returnUrl ? `?returnUrl=${encodeURIComponent(returnUrl)}` : ''}`}
              >
                Return to sign in
              </Link>
            </div>
          </>
        )}
      </div>
    </main>
  )
}
