import { create } from 'zustand'
import type { ModalKey } from '../modals/ModalManager'

interface UiState {
  isModalOpen: boolean
  modalKey?: ModalKey
  setModalState: (key?: ModalKey) => void
}

export const useUiStore = create<UiState>((set) => ({
  isModalOpen: false,
  modalKey: undefined,
  setModalState: (key) => set({ isModalOpen: Boolean(key), modalKey: key }),
}))

