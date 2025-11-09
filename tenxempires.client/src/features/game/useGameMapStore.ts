import { create } from 'zustand'
import { persist } from 'zustand/middleware'
import type { CameraState, SelectionState, TurnLogEntry } from '../../types/game'

// ============================================================================
// Banner Types
// ============================================================================

interface Banner {
  id: string
  kind: 'idle' | 'offline' | 'multi-tab' | 'rate-limit'
  message: string
  action?: {
    label: string
    onClick: () => void
  }
}

// ============================================================================
// Game Map Store State
// ============================================================================

interface GameMapState {
  // Camera
  camera: CameraState
  setCamera: (camera: Partial<CameraState>) => void
  resetCamera: () => void

  // Selection
  selection: SelectionState
  setSelection: (selection: SelectionState) => void
  clearSelection: () => void

  // Grid toggle
  gridOn: boolean
  toggleGrid: () => void

  // Zoom invert
  invertZoom: boolean
  toggleInvertZoom: () => void

  // Debug (dev-only surfaces)
  debug: boolean
  toggleDebug: () => void

  // AI Overlay
  isAiOverlayVisible: boolean
  setAiOverlayVisible: (visible: boolean) => void

  // Banners
  banners: Banner[]
  addBanner: (banner: Banner) => void
  removeBanner: (id: string) => void
  clearBanners: () => void
}

// ============================================================================
// Persisted Turn Log Store
// ============================================================================

interface TurnLogState {
  logs: Record<number, TurnLogEntry[]>
  isOpen: boolean
  addEntry: (gameId: number, entry: TurnLogEntry) => void
  clearLog: (gameId: number) => void
  toggleOpen: () => void
}

// ============================================================================
// Default Values
// ============================================================================

const DEFAULT_CAMERA: CameraState = {
  scale: 1,
  offsetX: 0,
  offsetY: 0,
}

const DEFAULT_SELECTION: SelectionState = {
  kind: null,
}

// ============================================================================
// Stores
// ============================================================================

export const useGameMapStore = create<GameMapState>()(
  persist(
    (set) => ({
  // Camera
  camera: DEFAULT_CAMERA,
  setCamera: (camera) =>
    set((state) => ({
      camera: { ...state.camera, ...camera },
    })),
  resetCamera: () => set({ camera: DEFAULT_CAMERA }),

  // Selection
  selection: DEFAULT_SELECTION,
  setSelection: (selection) => set({ selection }),
  clearSelection: () => set({ selection: DEFAULT_SELECTION }),

  // Grid toggle
  gridOn: false,
  toggleGrid: () => set((state) => ({ gridOn: !state.gridOn })),

  // Zoom invert
  invertZoom: false,
  toggleInvertZoom: () => set((state) => ({ invertZoom: !state.invertZoom })),

  // Debug
  debug: false,
  toggleDebug: () => set((state) => ({ debug: !state.debug })),

  // AI Overlay
  isAiOverlayVisible: false,
  setAiOverlayVisible: (visible) => set({ isAiOverlayVisible: visible }),

  // Banners
  banners: [],
  addBanner: (banner) =>
    set((state) => ({
      banners: [...state.banners.filter((b) => b.id !== banner.id), banner],
    })),
  removeBanner: (id) =>
    set((state) => ({
      banners: state.banners.filter((b) => b.id !== id),
    })),
  clearBanners: () => set({ banners: [] }),
    }),
    {
      name: 'ui-settings',
      partialize: (state) => ({ gridOn: state.gridOn, invertZoom: state.invertZoom, debug: state.debug }),
    }
  )
)

export const useTurnLogStore = create<TurnLogState>()(
  persist(
    (set) => ({
      logs: {},
      isOpen: false,
      addEntry: (gameId, entry) =>
        set((state) => {
          const currentLog = state.logs[gameId] || []
          // Keep last 20 entries
          const newLog = [...currentLog, entry].slice(-20)
          return {
            logs: {
              ...state.logs,
              [gameId]: newLog,
            },
          }
        }),
      clearLog: (gameId) =>
        set((state) => {
          // eslint-disable-next-line @typescript-eslint/no-unused-vars
          const { [gameId]: _, ...rest } = state.logs
          return { logs: rest }
        }),
      toggleOpen: () => set((state) => ({ isOpen: !state.isOpen })),
    }),
    {
      name: 'turn-log-storage',
      partialize: (state) => ({ logs: state.logs }), // Only persist logs, not isOpen
    }
  )
)

