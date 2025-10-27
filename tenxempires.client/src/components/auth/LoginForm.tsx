import { useState, useRef, useEffect } from 'react'
import type { FormEvent } from 'react'
import { z } from 'zod'
import type { LoginFormModel } from '../../types/auth'

const loginSchema = z.object({
  email: z.string().email('Please enter a valid email address'),
  password: z.string().min(1, 'Password is required'),
  rememberMe: z.boolean().optional(),
})

interface LoginFormProps {
  onSubmit: (model: LoginFormModel) => Promise<void>
  isSubmitting?: boolean
  error?: string
}

export function LoginForm({ onSubmit, isSubmitting = false, error }: LoginFormProps) {
  const [email, setEmail] = useState('')
  const [password, setPassword] = useState('')
  const [rememberMe, setRememberMe] = useState(true)
  const [validationErrors, setValidationErrors] = useState<Record<string, string>>({})
  
  const emailInputRef = useRef<HTMLInputElement>(null)
  const passwordInputRef = useRef<HTMLInputElement>(null)
  
  // Auto-focus first error field when validation fails
  useEffect(() => {
    if (validationErrors.email && emailInputRef.current) {
      emailInputRef.current.focus()
    } else if (validationErrors.password && passwordInputRef.current) {
      passwordInputRef.current.focus()
    }
  }, [validationErrors])

  async function handleSubmit(e: FormEvent) {
    e.preventDefault()
    setValidationErrors({})

    // Client-side validation
    const result = loginSchema.safeParse({ email, password, rememberMe })
    if (!result.success) {
      const errors: Record<string, string> = {}
      result.error.issues.forEach((issue) => {
        if (issue.path[0]) {
          errors[issue.path[0].toString()] = issue.message
        }
      })
      setValidationErrors(errors)
      return
    }

    await onSubmit(result.data)
  }

  return (
    <form onSubmit={handleSubmit} className="mt-6 space-y-4">
      <div>
        <label htmlFor="email" className="block text-sm font-medium text-slate-700">
          Email
        </label>
        <input
          ref={emailInputRef}
          id="email"
          type="email"
          required
          autoComplete="email"
          autoFocus
          aria-invalid={!!validationErrors.email}
          aria-describedby={validationErrors.email ? 'email-error' : undefined}
          className="mt-1 w-full rounded-md border border-slate-300 px-3 py-2 shadow-sm focus:border-indigo-500 focus:outline-none focus:ring-1 focus:ring-indigo-500 disabled:cursor-not-allowed disabled:bg-slate-50"
          value={email}
          onChange={(e) => setEmail(e.target.value)}
          disabled={isSubmitting}
        />
        {validationErrors.email && (
          <p id="email-error" role="alert" className="mt-1 text-sm text-rose-600">
            {validationErrors.email}
          </p>
        )}
      </div>

      <div>
        <label htmlFor="password" className="block text-sm font-medium text-slate-700">
          Password
        </label>
        <input
          ref={passwordInputRef}
          id="password"
          type="password"
          required
          autoComplete="current-password"
          aria-invalid={!!validationErrors.password}
          aria-describedby={validationErrors.password ? 'password-error' : undefined}
          className="mt-1 w-full rounded-md border border-slate-300 px-3 py-2 shadow-sm focus:border-indigo-500 focus:outline-none focus:ring-1 focus:ring-indigo-500 disabled:cursor-not-allowed disabled:bg-slate-50"
          value={password}
          onChange={(e) => setPassword(e.target.value)}
          disabled={isSubmitting}
        />
        {validationErrors.password && (
          <p id="password-error" role="alert" className="mt-1 text-sm text-rose-600">
            {validationErrors.password}
          </p>
        )}
      </div>

      <div className="flex items-center gap-2">
        <input
          id="remember"
          type="checkbox"
          className="h-4 w-4 rounded border-slate-300 text-indigo-600 focus:ring-indigo-500"
          checked={rememberMe}
          onChange={(e) => setRememberMe(e.target.checked)}
          disabled={isSubmitting}
        />
        <label htmlFor="remember" className="text-sm text-slate-700">
          Remember me
        </label>
      </div>

      {error && (
        <div role="alert" className="rounded-md border border-rose-300 bg-rose-50 p-3 text-sm text-rose-700">
          {error}
        </div>
      )}

      <button
        type="submit"
        disabled={isSubmitting}
        className="inline-flex w-full items-center justify-center rounded-md bg-indigo-600 px-4 py-2 text-sm font-medium text-white hover:bg-indigo-700 focus:outline-none focus-visible:ring-2 focus-visible:ring-indigo-500 focus-visible:ring-offset-2 disabled:opacity-50 disabled:cursor-not-allowed"
      >
        {isSubmitting ? 'Signing in…' : 'Sign in'}
      </button>
    </form>
  )
}

