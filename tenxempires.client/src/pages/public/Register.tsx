import { useMemo, useState, useEffect, useRef } from 'react'
import { useNavigate, useSearchParams } from 'react-router-dom'
import { postJson } from '../../api/http'
import { RegisterForm } from '../../components/auth/RegisterForm'
import { RegisterSupportLinks } from '../../components/auth/RegisterSupportLinks'
import { VerifyEmailModal } from '../../components/auth/VerifyEmailModal'
import type { RegisterFormModel, ApiError } from '../../types/auth'

export default function Register() {
  const navigate = useNavigate()
  const [searchParams] = useSearchParams()
  
  const returnUrl = useMemo(() => {
    return searchParams.get('returnUrl') ?? '/game/current'
  }, [searchParams])
  
  const modalType = searchParams.get('modal')
  
  const [error, setError] = useState<string | undefined>()
  const [isSubmitting, setIsSubmitting] = useState(false)
  const [retryAfter, setRetryAfter] = useState<number | undefined>()
  const [registeredEmail, setRegisteredEmail] = useState<string | undefined>()
  const retryTimerRef = useRef<ReturnType<typeof setInterval> | null>(null)

  // Cleanup retry timer on unmount
  useEffect(() => {
    return () => {
      if (retryTimerRef.current) {
        clearInterval(retryTimerRef.current)
      }
    }
  }, [])

  // Handle retry countdown
  useEffect(() => {
    if (retryAfter !== undefined && retryAfter > 0) {
      retryTimerRef.current = setInterval(() => {
        setRetryAfter((prev) => {
          if (prev === undefined || prev <= 1) {
            if (retryTimerRef.current) {
              clearInterval(retryTimerRef.current)
              retryTimerRef.current = null
            }
            return undefined
          }
          return prev - 1
        })
      }, 1000)

      return () => {
        if (retryTimerRef.current) {
          clearInterval(retryTimerRef.current)
          retryTimerRef.current = null
        }
      }
    }
  }, [retryAfter])

  async function handleSubmit(model: RegisterFormModel) {
    setIsSubmitting(true)
    setError(undefined)
    
    const { ok, status, data } = await postJson<RegisterFormModel, ApiError>(
      '/api/auth/register',
      model,
    )
    
    setIsSubmitting(false)
    
    if (ok) {
      // Registration successful - save email and show verify modal
      setRegisteredEmail(model.email)
      return
    }
    
    // Handle errors based on status code
    if (status === 0) {
      setError('Network error. Please check your connection and try again.')
      return
    }
    
    if (status === 400) {
      // Validation errors from backend
      if (data?.code && data?.message) {
        // Map common Identity errors to user-friendly messages
        const errorCode = data.code
        if (errorCode.includes('DuplicateEmail') || errorCode.includes('DuplicateUserName')) {
          setError('This email address is already registered. Please sign in or use a different email.')
        } else if (errorCode.includes('PasswordTooShort')) {
          setError('Password is too short. Please use at least 8 characters.')
        } else if (errorCode.includes('PasswordRequiresNonAlphanumeric')) {
          setError('Password must contain at least one symbol (!@#$%^&*, etc.).')
        } else if (errorCode.includes('PasswordRequiresDigit')) {
          setError('Password must contain at least one digit (0-9).')
        } else if (errorCode.includes('PasswordRequiresUpper')) {
          setError('Password must contain at least one uppercase letter (A-Z).')
        } else if (errorCode.includes('PasswordRequiresLower')) {
          setError('Password must contain at least one lowercase letter (a-z).')
        } else if (errorCode.includes('InvalidEmail')) {
          setError('Please enter a valid email address.')
        } else {
          setError(data.message)
        }
      } else {
        setError('Invalid input. Please check your information and try again.')
      }
      return
    }
    
    if (status === 409) {
      // Conflict - email already exists (avoid account enumeration in message)
      setError('An account with this email may already exist. Please try signing in or use the "Forgot password" option.')
      return
    }
    
    if (status === 429) {
      // Rate limit exceeded
      // Try to parse Retry-After header from response
      // For now, we'll default to 60 seconds if not provided
      const retrySeconds = 60 // TODO: Parse from headers when available
      setRetryAfter(retrySeconds)
      setError(`Too many registration attempts. Please wait ${retrySeconds} seconds before trying again.`)
      return
    }
    
    if (status >= 500) {
      // Server error
      setError('Server error. Please try again later.')
      return
    }
    
    // Fallback for any other errors
    if (data?.message) {
      setError(data.message)
    } else {
      setError('Unable to create account. Please try again.')
    }
  }

  function closeModal() {
    // If on verify modal after registration, navigate to login
    if (registeredEmail) {
      const loginUrl = `/login${returnUrl ? `?returnUrl=${encodeURIComponent(returnUrl)}` : ''}`
      navigate(loginUrl, { replace: true })
      return
    }
    
    const url = new URL(window.location.href)
    url.searchParams.delete('modal')
    navigate(url.pathname + (url.search ? `?${url.searchParams.toString()}` : ''), { replace: true })
  }

  return (
    <>
      <main data-testid="register-page" className="mx-auto max-w-md p-6">
        <h1 id="register-heading" className="text-2xl font-semibold">Create account</h1>
        <p className="mt-2 text-slate-600">Sign up to start playing TenX Empires.</p>
        
        <RegisterForm 
          onSubmit={handleSubmit}
          isSubmitting={isSubmitting}
          error={error}
          retryAfter={retryAfter}
        />
        
        <RegisterSupportLinks />
        
        {import.meta.env.DEV && (
          <div className="mt-4 text-xs text-slate-500">
            After registration you will be sent to: <code>{returnUrl}</code>
          </div>
        )}
      </main>

      {modalType === 'verify' && <VerifyEmailModal email={searchParams.get('email') ?? undefined} onRequestClose={closeModal} />}
      {registeredEmail && <VerifyEmailModal email={registeredEmail} onRequestClose={closeModal} />}
    </>
  )
}
