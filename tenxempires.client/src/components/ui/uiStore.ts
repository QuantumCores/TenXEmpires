import { create } from 'zustand'
import type { ModalKey } from '../modals/ModalManager'

interface UiState {
  isModalOpen: boolean
  modalKey?: ModalKey
  setModalState: (key?: ModalKey) => void
  sessionLocked: boolean
  setSessionLocked: (locked: boolean) => void
  schemaError?: { code: string; message: string; details?: unknown }
  setSchemaError: (err?: { code: string; message: string; details?: unknown }) => void
}

export const useUiStore = create<UiState>((set) => ({
  isModalOpen: false,
  modalKey: undefined,
  setModalState: (key) => set({ isModalOpen: Boolean(key), modalKey: key, sessionLocked: key === 'session-expired' }),
  sessionLocked: false,
  setSessionLocked: (locked) => set({ sessionLocked: locked }),
  schemaError: undefined,
  setSchemaError: (err) => set({ schemaError: err }),
}))
