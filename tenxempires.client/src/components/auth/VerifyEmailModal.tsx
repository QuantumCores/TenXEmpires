import { useState, useId } from 'react'
import { ModalContainer } from '../modals/ModalContainer'
import { postJson } from '../../api/http'
import type { ResendVerificationRequest, ApiError } from '../../types/auth'

interface VerifyEmailModalProps {
  email?: string
  onRequestClose: () => void
}

export function VerifyEmailModal({ email, onRequestClose }: VerifyEmailModalProps) {
  const titleId = useId()
  const [isResending, setIsResending] = useState(false)
  const [successMessage, setSuccessMessage] = useState<string | undefined>()
  const [errorMessage, setErrorMessage] = useState<string | undefined>()

  async function handleResend() {
    setSuccessMessage(undefined)
    setErrorMessage(undefined)
    setIsResending(true)

    const { ok, status, data } = await postJson<ResendVerificationRequest, ApiError>(
      '/api/auth/resend-verification',
      { email },
    )

    setIsResending(false)

    if (ok || status === 204) {
      // setSuccessMessage('Verification email sent! Please check your inbox.')
      setSuccessMessage('You can now login.')
      return
    }

    // Handle network errors
    if (status === 0) {
      setErrorMessage('Network error. Please check your connection and try again.')
      return
    }

    // Handle rate limiting (429)
    if (status === 429) {
      setErrorMessage('Too many attempts. Please wait a moment before trying again.')
      return
    }

    // Handle other errors
    // setErrorMessage(data?.message || 'Unable to send verification email. Please try again.')
    setErrorMessage(data?.message || 'Something went wrong. Please try again.')
  }

  return (
    <ModalContainer titleId={titleId} onRequestClose={onRequestClose}>
      <div className="space-y-4">
        <div className="flex items-center justify-between">
          <h2 id={titleId} className="text-lg font-semibold">
            Verify Your Email
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

        {/* Instructions */}
        <div className="space-y-3">
          <p className="text-sm text-slate-700">
            We've sent a verification email to your account. Please check your inbox and click the verification link to activate your account.
          </p>

          {email && (
            <div className="rounded-md bg-slate-50 border border-slate-200 p-3">
              <p className="text-sm text-slate-600">
                Email sent to: <strong className="text-slate-900">{email}</strong>
              </p>
            </div>
          )}

          <p className="text-sm text-slate-600">
            Didn't receive the email? Check your spam folder or request a new verification link.
          </p>
        </div>

        {/* Success Message */}
        {successMessage && (
          <div role="alert" className="rounded-md border border-green-300 bg-green-50 p-3 text-sm text-green-700">
            {successMessage}
          </div>
        )}

        {/* Error Message */}
        {errorMessage && (
          <div role="alert" className="rounded-md border border-rose-300 bg-rose-50 p-3 text-sm text-rose-700">
            {errorMessage}
          </div>
        )}

        {/* Resend Section */}
        <div className="flex justify-between items-center pt-2 border-t border-slate-200">
          <button
            type="button"
            onClick={handleResend}
            disabled={isResending}
            className="text-sm font-medium text-indigo-600 hover:text-indigo-700 focus:outline-none focus-visible:ring-2 focus-visible:ring-indigo-500 focus-visible:ring-offset-2 rounded disabled:cursor-not-allowed disabled:opacity-50"
          >
            {isResending ? 'Sending…' : 'Resend Verification Email'}
          </button>

          {/* Footer Actions */}
          <button
            type="button"
            onClick={onRequestClose}
            className="rounded-md border border-slate-300 px-4 py-2 text-sm font-medium text-slate-700 hover:bg-slate-50 focus:outline-none focus-visible:ring-2 focus-visible:ring-indigo-500 focus-visible:ring-offset-2"
          >
            Back to Login
          </button>
        </div>
      </div>
    </ModalContainer>
  )
}

