import { useEffect, useMemo, useRef, useState } from 'react'
import { useNavigate, useSearchParams } from 'react-router-dom'
import { postJson } from '../../api/http'
import { useQueryClient } from '@tanstack/react-query'
import { LoginForm } from '../../components/auth/LoginForm'
import { LoginSupportLinks } from '../../components/auth/LoginSupportLinks'
import type { LoginFormModel, ApiError } from '../../types/auth'

export default function Login() {
  const navigate = useNavigate()
  const qc = useQueryClient()
  const [searchParams] = useSearchParams()
  
  const returnUrl = useMemo(() => {
    return searchParams.get('returnUrl') ?? '/game/current'
  }, [searchParams])
  
  const [error, setError] = useState<string | undefined>()
  const [isSubmitting, setIsSubmitting] = useState(false)
  const [retryAfter, setRetryAfter] = useState<number | undefined>()
  const retryTimerRef = useRef<ReturnType<typeof setInterval> | null>(null)

  // Cleanup retry timer on unmount
  useEffect(() => {
    return () => {
      if (retryTimerRef.current) {
        clearInterval(retryTimerRef.current)
      }
    }
  }, [])

  // Handle retry countdown ticks
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

  async function handleSubmit(model: LoginFormModel) {
    if (retryAfter !== undefined && retryAfter > 0) {
      return
    }

    setIsSubmitting(true)
    setError(undefined)
    
    const { ok, status, data } = await postJson<LoginFormModel, ApiError>(
      '/api/auth/login',
      model,
    )
    
    setIsSubmitting(false)
    
    if (ok) {
      setRetryAfter(undefined)
      // Login successful - invalidate auth cache, wait for refetch, then redirect
      await qc.invalidateQueries({ queryKey: ['auth-status'] })
      await qc.refetchQueries({ queryKey: ['auth-status'] })
      navigate(returnUrl, { replace: true })
      return
    }
    
    // Handle errors
    if (status === 0) {
      setError('Network error. Please check your connection and try again.')
      return
    }

    if (status === 429) {
      const retrySeconds = typeof data?.retryAfterSeconds === 'number'
        ? Math.max(0, Math.ceil(data.retryAfterSeconds))
        : 15 * 60
      setRetryAfter(retrySeconds)
      setError('Too many failed login attempts. Account temporarily locked. Try again in 15 minutes.')
      return
    }
    
    if (data?.code && data?.message) {
      // Backend returned structured error
      setError(data.message)
    } else {
      // Fallback for unexpected errors
      setError('Unable to sign in. Please try again.')
    }

    setRetryAfter(undefined)
  }

  return (
    <>
      <main data-testid="login-page" className="mx-auto max-w-md p-6">
        <h1 id="login-heading" data-testid="login-heading" className="text-2xl font-semibold">Sign in</h1>
        <p className="mt-2 text-slate-600" data-testid="login-subheading">You must sign in to continue.</p>
        
        <LoginForm 
          onSubmit={handleSubmit}
          isSubmitting={isSubmitting}
          error={error}
          retryAfter={retryAfter}
        />
        
        <LoginSupportLinks />
        
        {import.meta.env.DEV && (
          <div className="mt-4 text-xs text-slate-500">
            After sign-in you will be sent to: <code>{returnUrl}</code>
          </div>
        )}
      </main>

    </>
  )
}
