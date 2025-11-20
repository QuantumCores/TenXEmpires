import { useSearchParams, useNavigate } from 'react-router-dom'
import { useState } from 'react'
import { postJson } from '../../api/http'
import { useQueryClient } from '@tanstack/react-query'

export function ActionRail() {
  const [, setSearchParams] = useSearchParams()
  const navigate = useNavigate()
  const queryClient = useQueryClient()
  const [isLoggingOut, setIsLoggingOut] = useState(false)

  const baseButtonClasses =
    'flex w-28 justify-center rounded-lg border border-slate-300 bg-white px-3 py-2 text-sm text-center shadow hover:bg-slate-50'

  const openModal = (modal: string) => {
    setSearchParams({ modal })
  }

  const handleLogout = async () => {
    if (isLoggingOut) return
    
    setIsLoggingOut(true)
    const { ok } = await postJson('/api/auth/logout', {})
    
    if (ok) {
      // Clear all cached queries
      queryClient.clear()
      // Redirect to home page
      navigate('/', { replace: true })
    } else {
      // If logout fails, still redirect but show error
      setIsLoggingOut(false)
      alert('Logout failed. Please try again.')
    }
  }

  const handleGoHome = () => {
    navigate('/')
  }

  return (
    <div className="absolute right-4 top-4 flex flex-col gap-2" data-testid="action-rail">
      <button
        type="button"
        className={baseButtonClasses}
        onClick={handleGoHome}
        aria-label="Go to home page"
        data-testid="action-rail-home"
      >
        Home
      </button>
      <button
        type="button"
        className={baseButtonClasses}
        onClick={() => openModal('saves')}
        aria-label="Open saves"
        data-testid="action-rail-saves"
      >
        Saves
      </button>
      <button
        type="button"
        className={baseButtonClasses}
        onClick={() => openModal('settings')}
        aria-label="Open settings"
        data-testid="action-rail-settings"
      >
        Settings
      </button>
      <button
        type="button"
        className={baseButtonClasses}
        onClick={() => openModal('help')}
        aria-label="Open help"
        data-testid="action-rail-help"
      >
        Help
      </button>
      <button
        type="button"
        className={`${baseButtonClasses} disabled:cursor-not-allowed disabled:opacity-50`}
        onClick={handleLogout}
        disabled={isLoggingOut}
        aria-label="Logout"
        data-testid="action-rail-logout"
      >
        {isLoggingOut ? 'Logging out...' : 'Logout'}
      </button>
    </div>
  )
}

