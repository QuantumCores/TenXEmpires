import { useState, useId, useEffect, useRef } from 'react'
import type { FormEvent } from 'react'
import { z } from 'zod'
import { ModalContainer } from '../modals/ModalContainer'
import { postJson } from '../../api/http'
import type { ForgotPasswordFormModel, ApiError } from '../../types/auth'

const forgotPasswordSchema = z.object({
  email: z.string().email('Please enter a valid email address'),
})

interface ForgotPasswordModalProps {
  onRequestClose: () => void
}

export function ForgotPasswordModal({ onRequestClose }: ForgotPasswordModalProps) {
  const titleId = useId()
  const [email, setEmail] = useState('')
  const [isSubmitting, setIsSubmitting] = useState(false)
  const [isSuccess, setIsSuccess] = useState(false)
  const [error, setError] = useState<string | undefined>(undefined)
  const [validationError, setValidationError] = useState<string | undefined>(undefined)
  const [retryAfter, setRetryAfter] = useState<number | undefined>(undefined)
  const retryTimerRef = useRef<number | undefined>(undefined)

  // Cleanup retry timer on unmount
  useEffect(() => {
    return () => {
      if (retryTimerRef.current !== undefined) {
        window.clearInterval(retryTimerRef.current)
      }
    }
  }, [])

  async function handleSubmit(e: FormEvent) {
    e.preventDefault()
    setValidationError(undefined)
    setError(undefined)

    // Client-side validation
    const result = forgotPasswordSchema.safeParse({ email })
    if (!result.success) {
      setValidationError(result.error.issues[0]?.message)
      return
    }

    setIsSubmitting(true)

    const { ok, status, data } = await postJson<ForgotPasswordFormModel, ApiError>(
      '/api/auth/forgot-password',
      { email },
    )

    setIsSubmitting(false)

    // Handle success - show generic message to avoid account enumeration
    if (ok || status === 204) {
      setIsSuccess(true)
      return
    }

    // Handle rate limiting (429)
    if (status === 429) {
      // Try to parse Retry-After header from response
      // Note: we can't access headers directly from postJson, so we'll show a generic message
      setError('Too many attempts. Please wait a moment before trying again.')
      setRetryAfter(60) // Default to 60 seconds
      
      // Start countdown timer
      retryTimerRef.current = window.setInterval(() => {
        setRetryAfter((prev) => {
          if (!prev || prev <= 1) {
            if (retryTimerRef.current) {
              window.clearInterval(retryTimerRef.current)
              retryTimerRef.current = undefined
            }
            setError(undefined)
            return undefined
          }
          return prev - 1
        })
      }, 1000)
      return
    }

    // Handle network errors
    if (status === 0) {
      setError('Network error. Please check your connection and try again.')
      return
    }

    // Handle other errors (keep generic to avoid enumeration)
    if (data?.message) {
      setError('Unable to process your request. Please try again.')
    } else {
      setError('Unable to send reset email. Please try again.')
    }
  }

  // Success state view
  if (isSuccess) {
    return (
      <ModalContainer titleId={titleId} onRequestClose={onRequestClose}>
        <div className="space-y-4">
          <div className="flex items-center justify-between">
            <h2 id={titleId} className="text-lg font-semibold">
              Check Your Email
            </h2>
            <button
              type="button"
              className="rounded px-2 py-1 hover:bg-slate-100"
              onClick={onRequestClose}
              aria-label="Close"
            >
              ✕
            </button>
          </div>
          
          <div className="space-y-3">
            <div className="flex items-center justify-center text-green-600">
              <svg
                className="h-16 w-16"
                fill="none"
                stroke="currentColor"
                viewBox="0 0 24 24"
                aria-hidden="true"
              >
                <path
                  strokeLinecap="round"
                  strokeLinejoin="round"
                  strokeWidth={2}
                  d="M9 12l2 2 4-4m6 2a9 9 0 11-18 0 9 9 0 0118 0z"
                />
              </svg>
            </div>
            
            <p className="text-center text-slate-700">
              If an account exists with <strong>{email}</strong>, you will receive password reset instructions shortly.
            </p>
            
            <p className="text-center text-sm text-slate-600">
              Please check your email inbox and follow the instructions to reset your password.
            </p>
          </div>

          <div className="flex justify-center gap-2 pt-2">
            <button
              type="button"
              onClick={onRequestClose}
              className="rounded-md bg-indigo-600 px-4 py-2 text-sm font-medium text-white hover:bg-indigo-700 focus:outline-none focus-visible:ring-2 focus-visible:ring-indigo-500 focus-visible:ring-offset-2"
            >
              Close
            </button>
          </div>
        </div>
      </ModalContainer>
    )
  }

  // Form state view
  return (
    <ModalContainer titleId={titleId} onRequestClose={onRequestClose}>
      <div className="space-y-4">
        <div className="flex items-center justify-between">
          <h2 id={titleId} className="text-lg font-semibold">
            Reset Your Password
          </h2>
          <button
            type="button"
            className="rounded px-2 py-1 hover:bg-slate-100"
            onClick={onRequestClose}
            aria-label="Close"
          >
            ✕
          </button>
        </div>
        
        <p className="text-sm text-slate-600">
          Enter your email address and we'll send you instructions to reset your password.
        </p>
        
        <form onSubmit={handleSubmit} className="space-y-4">
          <div>
            <label htmlFor="forgot-email" className="block text-sm font-medium text-slate-700">
              Email
            </label>
            <input
              id="forgot-email"
              type="email"
              required
              autoComplete="email"
              autoFocus
              aria-invalid={!!validationError}
              aria-describedby={validationError ? 'forgot-email-error' : undefined}
              className="mt-1 w-full rounded-md border border-slate-300 px-3 py-2 shadow-sm focus:border-indigo-500 focus:outline-none focus:ring-1 focus:ring-indigo-500 disabled:cursor-not-allowed disabled:bg-slate-50 disabled:text-slate-500"
              value={email}
              onChange={(e) => setEmail(e.target.value)}
              disabled={isSubmitting || !!retryAfter}
            />
            {validationError && (
              <p id="forgot-email-error" role="alert" className="mt-1 text-sm text-rose-600">
                {validationError}
              </p>
            )}
          </div>

          {error && (
            <div role="alert" className="rounded-md border border-rose-300 bg-rose-50 p-3 text-sm text-rose-700">
              {error}
              {retryAfter && retryAfter > 0 && (
                <span className="ml-1">
                  ({retryAfter}s)
                </span>
              )}
            </div>
          )}

          <div className="flex justify-end gap-2">
            <button
              type="button"
              onClick={onRequestClose}
              className="rounded-md border border-slate-300 px-4 py-2 text-sm font-medium text-slate-700 hover:bg-slate-50 focus:outline-none focus-visible:ring-2 focus-visible:ring-indigo-500 focus-visible:ring-offset-2"
              disabled={isSubmitting}
            >
              Cancel
            </button>
            <button
              type="submit"
              disabled={isSubmitting || !!retryAfter}
              className="rounded-md bg-indigo-600 px-4 py-2 text-sm font-medium text-white hover:bg-indigo-700 focus:outline-none focus-visible:ring-2 focus-visible:ring-indigo-500 focus-visible:ring-offset-2 disabled:cursor-not-allowed disabled:opacity-50"
            >
              {isSubmitting ? 'Sending…' : 'Send Reset Instructions'}
            </button>
          </div>
        </form>
      </div>
    </ModalContainer>
  )
}
