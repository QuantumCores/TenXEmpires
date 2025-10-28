import { useEffect, useState } from 'react'

interface AIOverlayProps {
  isVisible: boolean
}

export function AIOverlay({ isVisible }: AIOverlayProps) {
  const [elapsedSeconds, setElapsedSeconds] = useState(0)

  useEffect(() => {
    if (!isVisible) {
      setElapsedSeconds(0)
      return
    }

    const startTime = Date.now()
    const interval = setInterval(() => {
      const elapsed = Math.floor((Date.now() - startTime) / 1000)
      setElapsedSeconds(elapsed)
    }, 100)

    return () => clearInterval(interval)
  }, [isVisible])

  if (!isVisible) return null

  const getMessage = () => {
    if (elapsedSeconds < 2) {
      return 'AI is thinking...'
    }
    if (elapsedSeconds < 5) {
      return 'AI is still processing...'
    }
    return 'AI is taking longer than expected...'
  }

  const getIntensity = () => {
    if (elapsedSeconds < 2) return 'bg-blue-900/80'
    if (elapsedSeconds < 5) return 'bg-blue-900/85'
    return 'bg-blue-900/90'
  }

  return (
    <div
      className={`fixed inset-0 z-50 flex items-center justify-center ${getIntensity()}`}
      role="alert"
      aria-live="polite"
      aria-busy="true"
    >
      <div className="rounded-lg bg-white p-8 shadow-2xl">
        <div className="flex flex-col items-center gap-4">
          <svg
            className="h-12 w-12 animate-spin text-blue-600"
            xmlns="http://www.w3.org/2000/svg"
            fill="none"
            viewBox="0 0 24 24"
          >
            <circle
              className="opacity-25"
              cx="12"
              cy="12"
              r="10"
              stroke="currentColor"
              strokeWidth="4"
            />
            <path
              className="opacity-75"
              fill="currentColor"
              d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4zm2 5.291A7.962 7.962 0 014 12H0c0 3.042 1.135 5.824 3 7.938l3-2.647z"
            />
          </svg>
          <div className="text-center">
            <div className="text-lg font-semibold text-slate-900">{getMessage()}</div>
            <div className="mt-1 text-sm text-slate-600">{elapsedSeconds}s elapsed</div>
          </div>
        </div>
      </div>
    </div>
  )
}

