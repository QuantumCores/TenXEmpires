import { useSearchParams, useNavigate } from 'react-router-dom'
import { useState } from 'react'
import { postJson } from '../../api/http'
import { useQueryClient } from '@tanstack/react-query'

export function ActionRail() {
  const [, setSearchParams] = useSearchParams()
  const navigate = useNavigate()
  const queryClient = useQueryClient()
  const [isLoggingOut, setIsLoggingOut] = useState(false)

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
    <div className="absolute right-4 top-4 flex flex-col gap-2">
      <button
        type="button"
        className="rounded-lg border border-slate-300 bg-white px-3 py-2 text-sm shadow hover:bg-slate-50"
        onClick={handleGoHome}
        aria-label="Go to home page"
      >
        Home
      </button>
      <button
        type="button"
        className="rounded-lg border border-slate-300 bg-white px-3 py-2 text-sm shadow hover:bg-slate-50"
        onClick={() => openModal('saves')}
        aria-label="Open saves"
      >
        Saves
      </button>
      <button
        type="button"
        className="rounded-lg border border-slate-300 bg-white px-3 py-2 text-sm shadow hover:bg-slate-50"
        onClick={() => openModal('settings')}
        aria-label="Open settings"
      >
        Settings
      </button>
      <button
        type="button"
        className="rounded-lg border border-slate-300 bg-white px-3 py-2 text-sm shadow hover:bg-slate-50"
        onClick={() => openModal('help')}
        aria-label="Open help"
      >
        Help
      </button>
      <button
        type="button"
        className="rounded-lg border border-slate-300 bg-white px-3 py-2 text-sm shadow hover:bg-slate-50 disabled:opacity-50 disabled:cursor-not-allowed"
        onClick={handleLogout}
        disabled={isLoggingOut}
        aria-label="Logout"
      >
        {isLoggingOut ? 'Logging out...' : 'Logout'}
      </button>
    </div>
  )
}

