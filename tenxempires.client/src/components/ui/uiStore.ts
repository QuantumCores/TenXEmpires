import { create } from 'zustand'
import type { ModalKey } from '../modals/ModalManager'

interface UiState {
  isModalOpen: boolean
  modalKey?: ModalKey
  setModalState: (key?: ModalKey) => void
  sessionLocked: boolean
  setSessionLocked: (locked: boolean) => void
}

export const useUiStore = create<UiState>((set) => ({
  isModalOpen: false,
  modalKey: undefined,
  setModalState: (key) => set({ isModalOpen: Boolean(key), modalKey: key, sessionLocked: key === 'session-expired' }),
  sessionLocked: false,
  setSessionLocked: (locked) => set({ sessionLocked: locked }),
}))
