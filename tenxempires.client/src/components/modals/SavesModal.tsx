import { useState, useId } from 'react'
import { ModalContainer } from './ModalContainer'
import type { SaveManualDto, SaveAutosaveDto, OverwriteConfirm } from '../../types/saves'
import { 
  useSavesQuery, 
  useSaveManualMutation, 
  useDeleteManualMutation, 
  useLoadSaveMutation 
} from '../../features/game/useSavesQueries'

export interface SavesModalProps {
  onRequestClose: () => void
  gameId: number
  turnInProgress: boolean
  initialTab?: 'manual' | 'autosaves'
}

type TabType = 'manual' | 'autosaves'

export function SavesModal({ 
  onRequestClose, 
  gameId, 
  turnInProgress,
  initialTab = 'manual'
}: SavesModalProps) {
  const titleId = useId()
  const [activeTab, setActiveTab] = useState<TabType>(initialTab)
  const [overwriteConfirm, setOverwriteConfirm] = useState<OverwriteConfirm | null>(null)
  const [actionError, setActionError] = useState<string | null>(null)

  // Fetch saves data
  const { data: savesData, isLoading, error: queryError } = useSavesQuery(gameId)

  // Mutations
  const saveManualMutation = useSaveManualMutation(gameId)
  const deleteManualMutation = useDeleteManualMutation(gameId)
  const loadSaveMutation = useLoadSaveMutation(gameId)

  const manualSaves = savesData?.manual || []
  const autosaves = savesData?.autosaves || []
  const error = queryError ? String(queryError) : actionError

  const handleSaveToSlot = async (slot: number, name: string) => {
    setActionError(null)

    // Check if slot is occupied and needs confirmation
    const existingSave = manualSaves.find((s) => s.slot === slot)
    if (existingSave && !overwriteConfirm) {
      setOverwriteConfirm({
        slot: slot as 1 | 2 | 3,
        oldName: existingSave.name,
        newName: name,
      })
      return
    }

    // Validate name (1-40 chars)
    const trimmedName = name.trim()
    if (trimmedName.length < 1 || trimmedName.length > 40) {
      setActionError('Save name must be between 1 and 40 characters.')
      return
    }

    try {
      await saveManualMutation.mutateAsync({ slot, name: trimmedName })
      setOverwriteConfirm(null)
    } catch (err) {
      setActionError(err instanceof Error ? err.message : 'Failed to save game.')
    }
  }

  const handleLoadSave = async (saveId: number) => {
    setActionError(null)
    try {
      await loadSaveMutation.mutateAsync(saveId)
      // On success, close modal (game state will be updated by the mutation)
      onRequestClose()
    } catch (err) {
      setActionError(err instanceof Error ? err.message : 'Failed to load save.')
    }
  }

  const handleDeleteSlot = async (slot: number) => {
    setActionError(null)
    try {
      await deleteManualMutation.mutateAsync(slot)
    } catch (err) {
      setActionError(err instanceof Error ? err.message : 'Failed to delete save.')
    }
  }

  const handleCancelOverwrite = () => {
    setOverwriteConfirm(null)
  }

  const handleConfirmOverwrite = () => {
    if (overwriteConfirm) {
      handleSaveToSlot(overwriteConfirm.slot, overwriteConfirm.newName)
    }
  }

  const isBusy = saveManualMutation.isPending || deleteManualMutation.isPending || loadSaveMutation.isPending

  // Show overwrite confirmation modal if needed
  if (overwriteConfirm) {
    return (
      <ModalContainer 
        titleId={titleId} 
        onRequestClose={handleCancelOverwrite}
        closeOnBackdrop={false}
      >
        <div className="flex flex-col gap-4">
          <div className="flex items-center justify-between">
            <h2 id={titleId} className="text-lg font-semibold">
              Overwrite Save?
            </h2>
            <button
              type="button"
              className="rounded px-2 py-1 hover:bg-slate-100 disabled:opacity-50"
              onClick={handleCancelOverwrite}
              disabled={isBusy}
              aria-label="Close"
            >
              ✕
            </button>
          </div>

          <div className="text-sm text-slate-700">
            <p className="mb-2">
              Slot {overwriteConfirm.slot} already contains a save: <strong>{overwriteConfirm.oldName}</strong>
            </p>
            <p>
              Do you want to overwrite it with <strong>{overwriteConfirm.newName}</strong>?
            </p>
          </div>

          <div className="flex items-center justify-end gap-3 border-t border-slate-200 pt-4">
            <button
              type="button"
              className="rounded px-4 py-2 text-sm font-medium text-slate-700 hover:bg-slate-100 disabled:opacity-50"
              onClick={handleCancelOverwrite}
              disabled={isBusy}
            >
              Cancel
            </button>
            <button
              type="button"
              className="rounded bg-blue-600 px-4 py-2 text-sm font-medium text-white hover:bg-blue-700 disabled:cursor-not-allowed disabled:opacity-50"
              onClick={handleConfirmOverwrite}
              disabled={isBusy}
            >
              {isBusy ? 'Saving...' : 'Overwrite'}
            </button>
          </div>
        </div>
      </ModalContainer>
    )
  }

  return (
    <ModalContainer 
      titleId={titleId} 
      onRequestClose={onRequestClose}
      closeOnBackdrop={!isBusy}
    >
      <div className="flex flex-col gap-4">
        {/* Header */}
        <div className="flex items-center justify-between">
          <h2 id={titleId} className="text-lg font-semibold">
            Saves
          </h2>
          <button
            type="button"
            className="rounded px-2 py-1 hover:bg-slate-100 disabled:opacity-50"
            onClick={onRequestClose}
            disabled={isBusy}
            aria-label="Close"
          >
            ✕
          </button>
        </div>

        {/* Tabs */}
        <div role="tablist" className="flex gap-2 border-b border-slate-200">
          <button
            role="tab"
            aria-selected={activeTab === 'manual'}
            aria-controls="manual-tab-panel"
            className={`px-4 py-2 text-sm font-medium transition-colors ${
              activeTab === 'manual'
                ? 'border-b-2 border-blue-600 text-blue-600'
                : 'text-slate-600 hover:text-slate-800'
            }`}
            onClick={() => setActiveTab('manual')}
          >
            Manual Saves
          </button>
          <button
            role="tab"
            aria-selected={activeTab === 'autosaves'}
            aria-controls="autosaves-tab-panel"
            className={`px-4 py-2 text-sm font-medium transition-colors ${
              activeTab === 'autosaves'
                ? 'border-b-2 border-blue-600 text-blue-600'
                : 'text-slate-600 hover:text-slate-800'
            }`}
            onClick={() => setActiveTab('autosaves')}
          >
            Autosaves
          </button>
        </div>

        {/* Error Display */}
        {error && (
          <div 
            role="alert" 
            className="rounded border border-red-300 bg-red-50 p-3 text-sm text-red-800"
          >
            {String(error)}
          </div>
        )}

        {/* Loading State */}
        {isLoading && (
          <div className="flex items-center justify-center py-8">
            <div className="text-sm text-slate-600">Loading saves...</div>
          </div>
        )}

        {/* Tab Content */}
        {!isLoading && (
          <>
            {/* Manual Saves Tab */}
            {activeTab === 'manual' && (
              <div
                id="manual-tab-panel"
                role="tabpanel"
                aria-labelledby="manual-tab"
                className="py-4"
              >
                <ManualSavesTab
                  saves={manualSaves}
                  disabled={turnInProgress || isBusy}
                  onSave={handleSaveToSlot}
                  onLoad={handleLoadSave}
                  onDelete={handleDeleteSlot}
                />
              </div>
            )}

            {/* Autosaves Tab */}
            {activeTab === 'autosaves' && (
              <div
                id="autosaves-tab-panel"
                role="tabpanel"
                aria-labelledby="autosaves-tab"
                className="py-4"
              >
                <AutosavesTab
                  autosaves={autosaves}
                  disabled={turnInProgress || isBusy}
                  onLoad={handleLoadSave}
                />
              </div>
            )}
          </>
        )}

        {/* Footer */}
        <div className="flex items-center justify-end border-t border-slate-200 pt-4">
          <button
            type="button"
            className="rounded px-4 py-2 text-sm font-medium text-slate-700 hover:bg-slate-100 disabled:opacity-50"
            onClick={onRequestClose}
          >
            Close
          </button>
        </div>
      </div>
    </ModalContainer>
  )
}

// ============================================================================
// Manual Saves Tab
// ============================================================================

interface ManualSavesTabProps {
  saves: SaveManualDto[]
  disabled: boolean
  onSave: (slot: number, name: string) => void
  onLoad: (saveId: number) => void
  onDelete: (slot: number) => void
}

function ManualSavesTab({ 
  saves, 
  disabled, 
  onSave, 
  onLoad, 
  onDelete 
}: ManualSavesTabProps) {
  const slots = [1, 2, 3]

  return (
    <div className="flex flex-col gap-3">
      {slots.map((slot) => {
        const save = saves.find((s) => s.slot === slot)
        return (
          <SaveSlotCard
            key={slot}
            slot={slot}
            save={save}
            disabled={disabled}
            onSave={onSave}
            onLoad={onLoad}
            onDelete={onDelete}
          />
        )
      })}
    </div>
  )
}

// ============================================================================
// Save Slot Card
// ============================================================================

interface SaveSlotCardProps {
  slot: number
  save?: SaveManualDto
  disabled: boolean
  onSave: (slot: number, name: string) => void
  onLoad: (saveId: number) => void
  onDelete: (slot: number) => void
}

function SaveSlotCard({ 
  slot, 
  save, 
  disabled, 
  onSave, 
  onLoad, 
  onDelete 
}: SaveSlotCardProps) {
  const [name, setName] = useState(save?.name || `Save ${slot}`)
  const [isEditing, setIsEditing] = useState(false)

  const handleSave = () => {
    onSave(slot, name)
    setIsEditing(false)
  }

  const handleLoad = () => {
    if (save) {
      onLoad(save.id)
    }
  }

  const handleDelete = () => {
    if (save) {
      onDelete(slot)
    }
  }

  const isEmpty = !save

  return (
    <div className="rounded border border-slate-200 p-4">
      <div className="flex items-start justify-between gap-4">
        <div className="flex-1">
          <div className="mb-2 flex items-center gap-2">
            <span className="text-xs font-medium text-slate-500">Slot {slot}</span>
            {!isEmpty && (
              <span className="text-xs text-slate-400">Turn {save.turnNo}</span>
            )}
          </div>

          {isEmpty ? (
            <div className="mb-3 text-sm text-slate-400">Empty slot</div>
          ) : (
            <div className="mb-3">
              {isEditing ? (
                <input
                  type="text"
                  value={name}
                  onChange={(e) => setName(e.target.value)}
                  className="w-full rounded border border-slate-300 px-2 py-1 text-sm"
                  maxLength={40}
                  disabled={disabled}
                />
              ) : (
                <div className="text-sm font-medium">{save.name}</div>
              )}
              <div className="mt-1 text-xs text-slate-500">
                {new Date(save.createdAt).toLocaleString()}
              </div>
            </div>
          )}

          <div className="flex flex-wrap gap-2">
            {isEmpty ? (
              <button
                type="button"
                className="rounded bg-blue-600 px-3 py-1.5 text-xs font-medium text-white hover:bg-blue-700 disabled:cursor-not-allowed disabled:opacity-50"
                onClick={handleSave}
                disabled={disabled}
              >
                Save Here
              </button>
            ) : (
              <>
                <button
                  type="button"
                  className="rounded bg-green-600 px-3 py-1.5 text-xs font-medium text-white hover:bg-green-700 disabled:cursor-not-allowed disabled:opacity-50"
                  onClick={handleLoad}
                  disabled={disabled}
                >
                  Load
                </button>
                <button
                  type="button"
                  className="rounded border border-slate-300 px-3 py-1.5 text-xs font-medium text-slate-700 hover:bg-slate-50 disabled:cursor-not-allowed disabled:opacity-50"
                  onClick={handleSave}
                  disabled={disabled}
                >
                  Overwrite
                </button>
                <button
                  type="button"
                  className="rounded border border-red-300 px-3 py-1.5 text-xs font-medium text-red-700 hover:bg-red-50 disabled:cursor-not-allowed disabled:opacity-50"
                  onClick={handleDelete}
                  disabled={disabled}
                >
                  Delete
                </button>
              </>
            )}
          </div>
        </div>
      </div>
    </div>
  )
}

// ============================================================================
// Autosaves Tab
// ============================================================================

interface AutosavesTabProps {
  autosaves: SaveAutosaveDto[]
  disabled: boolean
  onLoad: (saveId: number) => void
}

function AutosavesTab({ autosaves, disabled, onLoad }: AutosavesTabProps) {
  if (autosaves.length === 0) {
    return (
      <div className="py-8 text-center text-sm text-slate-500">
        No autosaves available yet. Autosaves are created at the end of each turn.
      </div>
    )
  }

  return (
    <div className="flex flex-col gap-2">
      {autosaves.map((autosave) => (
        <AutosaveItem
          key={autosave.id}
          autosave={autosave}
          disabled={disabled}
          onLoad={onLoad}
        />
      ))}
    </div>
  )
}

// ============================================================================
// Autosave Item
// ============================================================================

interface AutosaveItemProps {
  autosave: SaveAutosaveDto
  disabled: boolean
  onLoad: (saveId: number) => void
}

function AutosaveItem({ autosave, disabled, onLoad }: AutosaveItemProps) {
  const handleLoad = () => {
    onLoad(autosave.id)
  }

  return (
    <div className="flex items-center justify-between rounded border border-slate-200 p-3">
      <div>
        <div className="text-sm font-medium">Turn {autosave.turnNo}</div>
        <div className="text-xs text-slate-500">
          {new Date(autosave.createdAt).toLocaleString()}
        </div>
      </div>
      <button
        type="button"
        className="rounded bg-green-600 px-3 py-1.5 text-xs font-medium text-white hover:bg-green-700 disabled:cursor-not-allowed disabled:opacity-50"
        onClick={handleLoad}
        disabled={disabled}
      >
        Load
      </button>
    </div>
  )
}

