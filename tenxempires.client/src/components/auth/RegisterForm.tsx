import { useState, useRef, useEffect, useMemo } from 'react'
import type { FormEvent } from 'react'
import { z } from 'zod'
import type { RegisterFormModel } from '../../types/auth'

// Password validation rules matching backend requirements
const PASSWORD_MIN_LENGTH = 8
const PASSWORD_RULES = {
  minLength: (val: string) => val.length >= PASSWORD_MIN_LENGTH,
  hasDigit: (val: string) => /\d/.test(val),
  hasUppercase: (val: string) => /[A-Z]/.test(val),
  hasLowercase: (val: string) => /[a-z]/.test(val),
  hasNonAlphanumeric: (val: string) => /[^a-zA-Z0-9]/.test(val),
}

const registerSchema = z.object({
  email: z.string().email('Please enter a valid email address'),
  password: z
    .string()
    .min(PASSWORD_MIN_LENGTH, `Password must be at least ${PASSWORD_MIN_LENGTH} characters`)
    .refine(PASSWORD_RULES.hasDigit, 'Password must contain at least one digit')
    .refine(PASSWORD_RULES.hasUppercase, 'Password must contain at least one uppercase letter')
    .refine(PASSWORD_RULES.hasLowercase, 'Password must contain at least one lowercase letter')
    .refine(PASSWORD_RULES.hasNonAlphanumeric, 'Password must contain at least one symbol'),
  confirm: z.string().optional(),
})

interface RegisterFormProps {
  onSubmit: (model: RegisterFormModel) => Promise<void>
  isSubmitting?: boolean
  error?: string
  retryAfter?: number
}

interface PasswordRuleStatus {
  minLength: boolean
  hasDigit: boolean
  hasUppercase: boolean
  hasLowercase: boolean
  hasNonAlphanumeric: boolean
}

export function RegisterForm({ onSubmit, isSubmitting = false, error, retryAfter }: RegisterFormProps) {
  const [email, setEmail] = useState('')
  const [password, setPassword] = useState('')
  const [confirm, setConfirm] = useState('')
  const [validationErrors, setValidationErrors] = useState<Record<string, string>>({})
  const [showPasswordRules, setShowPasswordRules] = useState(false)

  const emailInputRef = useRef<HTMLInputElement>(null)
  const passwordInputRef = useRef<HTMLInputElement>(null)
  const confirmInputRef = useRef<HTMLInputElement>(null)

  // Calculate password rule statuses for live feedback
  const passwordRuleStatus = useMemo<PasswordRuleStatus>(() => ({
    minLength: PASSWORD_RULES.minLength(password),
    hasDigit: PASSWORD_RULES.hasDigit(password),
    hasUppercase: PASSWORD_RULES.hasUppercase(password),
    hasLowercase: PASSWORD_RULES.hasLowercase(password),
    hasNonAlphanumeric: PASSWORD_RULES.hasNonAlphanumeric(password),
  }), [password])

  const allRulesMet = useMemo(
    () => Object.values(passwordRuleStatus).every(Boolean),
    [passwordRuleStatus]
  )

  // Auto-focus first error field when validation fails
  useEffect(() => {
    if (validationErrors.email && emailInputRef.current) {
      emailInputRef.current.focus()
    } else if (validationErrors.password && passwordInputRef.current) {
      passwordInputRef.current.focus()
    } else if (validationErrors.confirm && confirmInputRef.current) {
      confirmInputRef.current.focus()
    }
  }, [validationErrors])

  async function handleSubmit(e: FormEvent) {
    e.preventDefault()
    setValidationErrors({})

    // Check password confirmation match first
    if (confirm && password !== confirm) {
      setValidationErrors({ confirm: 'Passwords do not match' })
      return
    }

    // Client-side validation
    const result = registerSchema.safeParse({ email, password, confirm })
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

    await onSubmit({ email, password, confirm })
  }

  const isDisabled = isSubmitting || (retryAfter !== undefined && retryAfter > 0)

  // Check if there are any validation errors for a11y summary
  const hasErrors = Object.keys(validationErrors).length > 0 || error

  return (
    <form data-testid="register-form" onSubmit={handleSubmit} className="mt-6 space-y-4" aria-labelledby="register-heading">
      {/* Error summary for screen readers */}
      {hasErrors && (
        <div 
          role="alert" 
          aria-live="assertive"
          className="sr-only"
        >
          Registration form has errors. Please correct the following:
          {validationErrors.email && ` Email: ${validationErrors.email}.`}
          {validationErrors.password && ` Password: ${validationErrors.password}.`}
          {validationErrors.confirm && ` Confirm password: ${validationErrors.confirm}.`}
          {error && ` ${error}`}
        </div>
      )}

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
          data-testid="register-email-input"
          aria-invalid={!!validationErrors.email}
          aria-describedby={validationErrors.email ? 'email-error' : undefined}
          className="mt-1 w-full rounded-md border border-slate-300 px-3 py-2 shadow-sm focus:border-indigo-500 focus:outline-none focus:ring-1 focus:ring-indigo-500 disabled:cursor-not-allowed disabled:bg-slate-50"
          value={email}
          onChange={(e) => setEmail(e.target.value)}
          disabled={isDisabled}
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
          autoComplete="new-password"
          data-testid="register-password-input"
          aria-invalid={!!validationErrors.password}
          aria-describedby={showPasswordRules ? 'password-rules' : validationErrors.password ? 'password-error' : undefined}
          className="mt-1 w-full rounded-md border border-slate-300 px-3 py-2 shadow-sm focus:border-indigo-500 focus:outline-none focus:ring-1 focus:ring-indigo-500 disabled:cursor-not-allowed disabled:bg-slate-50"
          value={password}
          onChange={(e) => setPassword(e.target.value)}
          onFocus={() => setShowPasswordRules(true)}
          onBlur={() => setShowPasswordRules(false)}
          disabled={isDisabled}
        />
        {validationErrors.password && (
          <p id="password-error" role="alert" className="mt-1 text-sm text-rose-600">
            {validationErrors.password}
          </p>
        )}
        
        {/* Live password rules checklist */}
        {(showPasswordRules || password.length > 0) && (
          <div
            id="password-rules"
            role="status"
            aria-live="polite"
            className="mt-2 space-y-1 text-xs"
          >
            <p className="font-medium text-slate-700">Password must contain:</p>
            <ul className="ml-4 space-y-0.5">
              <li className={passwordRuleStatus.minLength ? 'text-green-600' : 'text-slate-600'}>
                <span aria-hidden="true">{passwordRuleStatus.minLength ? '✓' : '○'}</span>{' '}
                <span className={passwordRuleStatus.minLength ? 'sr-only' : undefined}>
                  {passwordRuleStatus.minLength ? 'Completed: ' : 'Required: '}
                </span>
                At least {PASSWORD_MIN_LENGTH} characters
              </li>
              <li className={passwordRuleStatus.hasDigit ? 'text-green-600' : 'text-slate-600'}>
                <span aria-hidden="true">{passwordRuleStatus.hasDigit ? '✓' : '○'}</span>{' '}
                <span className={passwordRuleStatus.hasDigit ? 'sr-only' : undefined}>
                  {passwordRuleStatus.hasDigit ? 'Completed: ' : 'Required: '}
                </span>
                At least one digit (0-9)
              </li>
              <li className={passwordRuleStatus.hasUppercase ? 'text-green-600' : 'text-slate-600'}>
                <span aria-hidden="true">{passwordRuleStatus.hasUppercase ? '✓' : '○'}</span>{' '}
                <span className={passwordRuleStatus.hasUppercase ? 'sr-only' : undefined}>
                  {passwordRuleStatus.hasUppercase ? 'Completed: ' : 'Required: '}
                </span>
                At least one uppercase letter (A-Z)
              </li>
              <li className={passwordRuleStatus.hasLowercase ? 'text-green-600' : 'text-slate-600'}>
                <span aria-hidden="true">{passwordRuleStatus.hasLowercase ? '✓' : '○'}</span>{' '}
                <span className={passwordRuleStatus.hasLowercase ? 'sr-only' : undefined}>
                  {passwordRuleStatus.hasLowercase ? 'Completed: ' : 'Required: '}
                </span>
                At least one lowercase letter (a-z)
              </li>
              <li className={passwordRuleStatus.hasNonAlphanumeric ? 'text-green-600' : 'text-slate-600'}>
                <span aria-hidden="true">{passwordRuleStatus.hasNonAlphanumeric ? '✓' : '○'}</span>{' '}
                <span className={passwordRuleStatus.hasNonAlphanumeric ? 'sr-only' : undefined}>
                  {passwordRuleStatus.hasNonAlphanumeric ? 'Completed: ' : 'Required: '}
                </span>
                At least one symbol (!@#$%^&*, etc.)
              </li>
            </ul>
          </div>
        )}
      </div>

      <div>
        <label htmlFor="confirm" className="block text-sm font-medium text-slate-700">
          Confirm Password
        </label>
        <input
          ref={confirmInputRef}
          id="confirm"
          type="password"
          required
          autoComplete="new-password"
          data-testid="register-confirm-password-input"
          aria-invalid={!!validationErrors.confirm}
          aria-describedby={validationErrors.confirm ? 'confirm-error' : undefined}
          className="mt-1 w-full rounded-md border border-slate-300 px-3 py-2 shadow-sm focus:border-indigo-500 focus:outline-none focus:ring-1 focus:ring-indigo-500 disabled:cursor-not-allowed disabled:bg-slate-50"
          value={confirm}
          onChange={(e) => setConfirm(e.target.value)}
          disabled={isDisabled}
        />
        {validationErrors.confirm && (
          <p id="confirm-error" role="alert" className="mt-1 text-sm text-rose-600">
            {validationErrors.confirm}
          </p>
        )}
      </div>

      {error && (
        <div role="alert" className="rounded-md border border-rose-300 bg-rose-50 p-3 text-sm text-rose-700">
          {error}
        </div>
      )}

      <button
        type="submit"
        data-testid="create-account-button"
        disabled={isDisabled || !allRulesMet}
        className="inline-flex w-full items-center justify-center rounded-md bg-indigo-600 px-4 py-2 text-sm font-medium text-white hover:bg-indigo-700 focus:outline-none focus-visible:ring-2 focus-visible:ring-indigo-500 focus-visible:ring-offset-2 disabled:opacity-50 disabled:cursor-not-allowed"
      >
        {retryAfter && retryAfter > 0
          ? `Wait ${retryAfter}s...`
          : isSubmitting
            ? 'Creating account…'
            : 'Create account'}
      </button>
    </form>
  )
}

