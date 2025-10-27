import { useState, useId } from 'react'
import type { FormEvent } from 'react'
import { z } from 'zod'
import { ModalContainer } from '../modals/ModalContainer'
// import { postJson } from '../../api/http'
// import type { VerifyEmailFormModel, ApiError } from '../../types/auth'

const verifyEmailSchema = z.object({
  email: z.string().email('Please enter a valid email address'),
})

interface VerifyEmailModalProps {
  onRequestClose: () => void
}

export function VerifyEmailModal({ onRequestClose }: VerifyEmailModalProps) {
  const titleId = useId()
  const [email, setEmail] = useState('')
  const [isSubmitting, setIsSubmitting] = useState(false)
  const [error, setError] = useState<string | undefined>()
  const [validationError, setValidationError] = useState<string | undefined>()

  async function handleSubmit(e: FormEvent) {
    e.preventDefault()
    setValidationError(undefined)
    setError(undefined)

    // Client-side validation
    const result = verifyEmailSchema.safeParse({ email })
    if (!result.success) {
      setValidationError(result.error.issues[0]?.message)
      return
    }

    setIsSubmitting(true)

    // TODO: Backend endpoint /v1/auth/resend-verification not yet implemented
    // For now, show a placeholder message
    await new Promise(resolve => setTimeout(resolve, 500)) // Simulate API call
    
    setIsSubmitting(false)
    setError('Email verification functionality is not yet available. All new accounts are automatically verified.')
    
    // TODO: Uncomment when backend endpoint is ready:
    // const { ok, data } = await postJson<VerifyEmailFormModel, ApiError>(
    //   '/v1/auth/resend-verification',
    //   { email },
    // )
    // if (ok) {
    //   onRequestClose()
    //   return
    // }
    // setError(data?.message || 'Unable to send verification email. Please try again.')
  }

  return (
    <ModalContainer titleId={titleId} onRequestClose={onRequestClose}>
      <div className="space-y-4">
        <div className="flex items-center justify-between">
          <h2 id={titleId} className="text-lg font-semibold">
            Verify Email Address
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
          Enter your email address to receive a new verification link.
        </p>
        <form onSubmit={handleSubmit} className="space-y-4">
          <div>
            <label htmlFor="verify-email" className="block text-sm font-medium text-slate-700">
              Email
            </label>
            <input
              id="verify-email"
              type="email"
              required
              autoComplete="email"
              aria-invalid={!!validationError}
              aria-describedby={validationError ? 'verify-email-error' : undefined}
              className="mt-1 w-full rounded-md border border-slate-300 px-3 py-2 shadow-sm focus:border-indigo-500 focus:outline-none focus:ring-1 focus:ring-indigo-500"
              value={email}
              onChange={(e) => setEmail(e.target.value)}
              disabled={isSubmitting}
            />
            {validationError && (
              <p id="verify-email-error" role="alert" className="mt-1 text-sm text-rose-600">
                {validationError}
              </p>
            )}
          </div>

          {error && (
            <div role="alert" className="rounded-md border border-rose-300 bg-rose-50 p-3 text-sm text-rose-700">
              {error}
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
              disabled={isSubmitting}
              className="rounded-md bg-indigo-600 px-4 py-2 text-sm font-medium text-white hover:bg-indigo-700 focus:outline-none focus-visible:ring-2 focus-visible:ring-indigo-500 focus-visible:ring-offset-2 disabled:opacity-50 disabled:cursor-not-allowed"
            >
              {isSubmitting ? 'Sending…' : 'Send Verification Email'}
            </button>
          </div>
        </form>
      </div>
    </ModalContainer>
  )
}

