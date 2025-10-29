import { useMemo, useState } from 'react'
import { useModalParam } from '../../router/query'
import { useUiStore } from '../ui/uiStore'

export interface ErrorSchemaModalProps {
  onRequestClose: () => void
}

export function ErrorSchemaModal({ onRequestClose }: ErrorSchemaModalProps) {
  const { openModal } = useModalParam()
  const schemaError = useUiStore((s) => s.schemaError)
  const clearSchemaError = useUiStore((s) => s.setSchemaError)
  const [showDetails, setShowDetails] = useState(false)
  const [copied, setCopied] = useState(false)

  const title = 'Schema Version Mismatch'
  const description = useMemo(() => {
    return (
      schemaError?.message ||
      'The save or map schema is incompatible with this version.'
    )
  }, [schemaError?.message])

  const jsonDetails = useMemo(() => {
    const payload = schemaError ?? { code: 'SCHEMA_MISMATCH', message: description }
    try {
      return JSON.stringify(payload, null, 2)
    } catch {
      return JSON.stringify({ message: description })
    }
  }, [schemaError, description])

  const onCopy = async () => {
    try {
      await navigator.clipboard.writeText(jsonDetails)
      setCopied(true)
      window.setTimeout(() => setCopied(false), 1500)
    } catch {
      // ignore clipboard errors
    }
  }

  return (
    <div className="flex flex-col gap-4">
      <div className="flex items-center justify-between">
        <h2 className="text-lg font-semibold">{title}</h2>
        {/* Intentionally no close X to keep this blocking */}
      </div>

      <div className="rounded border border-amber-300 bg-amber-50 p-3 text-sm text-amber-900">
        <div className="font-medium">{schemaError?.code ?? 'SCHEMA_MISMATCH'}</div>
        <div className="mt-1 text-slate-700">{description}</div>
      </div>

      <div className="flex items-center justify-between">
        <button
          type="button"
          className="rounded bg-blue-600 px-4 py-2 text-sm font-medium text-white hover:bg-blue-700"
          onClick={() => openModal('start-new', undefined, 'replace')}
        >
          Start New Game
        </button>
        <button
          type="button"
          className="rounded px-3 py-1.5 text-sm font-medium text-slate-700 hover:bg-slate-100"
          aria-expanded={showDetails}
          aria-controls="error-schema-details"
          onClick={() => setShowDetails((v) => !v)}
        >
          {showDetails ? 'Hide Details' : 'View Details'}
        </button>
      </div>

      {showDetails && (
        <div id="error-schema-details" className="rounded border border-slate-200 bg-slate-50 p-3">
          <div className="mb-2 flex items-center justify-between">
            <div className="text-sm font-medium text-slate-700">Raw Details</div>
            <button
              type="button"
              onClick={onCopy}
              className="rounded px-2 py-1 text-xs font-medium text-slate-700 hover:bg-slate-200"
            >
              {copied ? 'Copied' : 'Copy JSON'}
            </button>
          </div>
          <pre className="max-h-64 overflow-auto rounded bg-white p-3 text-xs text-slate-800 shadow-inner">
            {jsonDetails}
          </pre>
        </div>
      )}

      <div className="flex items-center justify-end gap-2 border-t border-slate-200 pt-4">
        <button
          type="button"
          className="rounded px-3 py-1.5 text-sm text-slate-600 hover:bg-slate-100"
          onClick={() => { clearSchemaError(undefined); onRequestClose() }}
        >
          Dismiss
        </button>
      </div>
    </div>
  )
}
