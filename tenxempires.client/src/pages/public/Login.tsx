import { useMemo, useState } from 'react'
import { useNavigate, useSearchParams } from 'react-router-dom'
import { postJson } from '../../api/http'
import { useQueryClient } from '@tanstack/react-query'
import { LoginForm } from '../../components/auth/LoginForm'
import { LoginSupportLinks } from '../../components/auth/LoginSupportLinks'
import { ForgotPasswordModal } from '../../components/auth/ForgotPasswordModal'
import { VerifyEmailModal } from '../../components/auth/VerifyEmailModal'
import type { LoginFormModel, ApiError } from '../../types/auth'

export default function Login() {
  const navigate = useNavigate()
  const qc = useQueryClient()
  const [searchParams] = useSearchParams()
  
  const returnUrl = useMemo(() => {
    return searchParams.get('returnUrl') ?? '/game/current'
  }, [searchParams])
  
  const modalType = searchParams.get('modal')
  const emailParam = searchParams.get('email') ?? undefined
  
  const [error, setError] = useState<string | undefined>()
  const [isSubmitting, setIsSubmitting] = useState(false)

  async function handleSubmit(model: LoginFormModel) {
    setIsSubmitting(true)
    setError(undefined)
    
    const { ok, status, data } = await postJson<LoginFormModel, ApiError>(
      '/api/auth/login',
      model,
    )
    
    setIsSubmitting(false)
    
    if (ok) {
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
    
    if (data?.code && data?.message) {
      // Backend returned structured error
      setError(data.message)
    } else {
      // Fallback for unexpected errors
      setError('Unable to sign in. Please try again.')
    }
  }

  function closeModal() {
    const url = new URL(window.location.href)
    url.searchParams.delete('modal')
    navigate(url.pathname + (url.search ? `?${url.searchParams.toString()}` : ''), { replace: true })
  }

  return (
    <>
      <main className="mx-auto max-w-md p-6">
        <h1 className="text-2xl font-semibold">Sign in</h1>
        <p className="mt-2 text-slate-600">You must sign in to continue.</p>
        
        <LoginForm 
          onSubmit={handleSubmit}
          isSubmitting={isSubmitting}
          error={error}
        />
        
        <LoginSupportLinks />
        
        {import.meta.env.DEV && (
          <div className="mt-4 text-xs text-slate-500">
            After sign-in you will be sent to: <code>{returnUrl}</code>
          </div>
        )}
      </main>

      {modalType === 'forgot' && <ForgotPasswordModal onRequestClose={closeModal} />}
      {modalType === 'verify' && <VerifyEmailModal email={emailParam} onRequestClose={closeModal} />}
    </>
  )
}
