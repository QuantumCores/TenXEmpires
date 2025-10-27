import { useState, useId } from 'react'
import type { FormEvent } from 'react'
import { z } from 'zod'
import { ModalContainer } from '../modals/ModalContainer'
// import { postJson } from '../../api/http'
// import type { ForgotPasswordFormModel, ApiError } from '../../types/auth'

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
  const [error, setError] = useState<string | undefined>()
  const [validationError, setValidationError] = useState<string | undefined>()

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

    // TODO: Backend endpoint /v1/auth/forgot-password not yet implemented
    // For now, show a placeholder message
    await new Promise(resolve => setTimeout(resolve, 500)) // Simulate API call
    
    setIsSubmitting(false)
    setError('Password reset functionality is not yet available. Please contact support for assistance.')
    
    // TODO: Uncomment when backend endpoint is ready:
    // const { ok, data } = await postJson<ForgotPasswordFormModel, ApiError>(
    //   '/v1/auth/forgot-password',
    //   { email },
    // )
    // if (ok) {
    //   onRequestClose()
    //   return
    // }
    // setError(data?.message || 'Unable to process request. Please try again.')
  }

  return (
    <ModalContainer titleId={titleId} onRequestClose={onRequestClose}>
      <div className="space-y-4">
        <div className="flex items-center justify-between">
          <h2 id={titleId} className="text-lg font-semibold">
            Forgot Password
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
              aria-invalid={!!validationError}
              aria-describedby={validationError ? 'forgot-email-error' : undefined}
              className="mt-1 w-full rounded-md border border-slate-300 px-3 py-2 shadow-sm focus:border-indigo-500 focus:outline-none focus:ring-1 focus:ring-indigo-500"
              value={email}
              onChange={(e) => setEmail(e.target.value)}
              disabled={isSubmitting}
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
              {isSubmitting ? 'Sending…' : 'Send Reset Link'}
            </button>
          </div>
        </form>
      </div>
    </ModalContainer>
  )
}

