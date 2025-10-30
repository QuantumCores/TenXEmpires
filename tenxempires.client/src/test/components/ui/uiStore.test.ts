import { describe, it, expect, beforeEach } from 'vitest'
import { useUiStore } from '../../../components/ui/uiStore'
import type { ModalKey } from '../../../components/modals/ModalManager'

describe('useUiStore', () => {
  beforeEach(() => {
    // Reset store to initial state before each test
    useUiStore.setState({
      isModalOpen: false,
      modalKey: undefined,
      sessionLocked: false,
      schemaError: undefined,
    })
  })

  describe('initial state', () => {
    it('starts with isModalOpen: false', () => {
      const state = useUiStore.getState()
      expect(state.isModalOpen).toBe(false)
    })

    it('starts with modalKey: undefined', () => {
      const state = useUiStore.getState()
      expect(state.modalKey).toBeUndefined()
    })

    it('starts with sessionLocked: false', () => {
      const state = useUiStore.getState()
      expect(state.sessionLocked).toBe(false)
    })

    it('starts with schemaError: undefined', () => {
      const state = useUiStore.getState()
      expect(state.schemaError).toBeUndefined()
    })
  })

  describe('setModalState', () => {
    describe('modal opening', () => {
      it('opens modal when key is provided', () => {
        useUiStore.getState().setModalState('help')
        
        const state = useUiStore.getState()
        expect(state.isModalOpen).toBe(true)
        expect(state.modalKey).toBe('help')
      })

      it('opens different modals', () => {
        const modalKeys: ModalKey[] = ['help', 'settings', 'session-expired', 'error-schema', 'saves']
        
        modalKeys.forEach((key) => {
          useUiStore.getState().setModalState(key)
          
          const state = useUiStore.getState()
          expect(state.isModalOpen).toBe(true)
          expect(state.modalKey).toBe(key)
        })
      })

      it('updates isModalOpen to true for any truthy key', () => {
        useUiStore.getState().setModalState('help')
        expect(useUiStore.getState().isModalOpen).toBe(true)
        
        useUiStore.getState().setModalState('settings')
        expect(useUiStore.getState().isModalOpen).toBe(true)
      })
    })

    describe('modal closing', () => {
      it('closes modal when key is undefined', () => {
        // First open a modal
        useUiStore.getState().setModalState('help')
        expect(useUiStore.getState().isModalOpen).toBe(true)
        
        // Then close it
        useUiStore.getState().setModalState(undefined)
        
        const state = useUiStore.getState()
        expect(state.isModalOpen).toBe(false)
        expect(state.modalKey).toBeUndefined()
      })

      it('keeps modal closed when undefined is set on closed modal', () => {
        expect(useUiStore.getState().isModalOpen).toBe(false)
        
        useUiStore.getState().setModalState(undefined)
        
        expect(useUiStore.getState().isModalOpen).toBe(false)
        expect(useUiStore.getState().modalKey).toBeUndefined()
      })
    })

    describe('modal switching', () => {
      it('switches from one modal to another', () => {
        useUiStore.getState().setModalState('help')
        expect(useUiStore.getState().modalKey).toBe('help')
        
        useUiStore.getState().setModalState('settings')
        
        const state = useUiStore.getState()
        expect(state.isModalOpen).toBe(true)
        expect(state.modalKey).toBe('settings')
      })

      it('maintains isModalOpen: true when switching modals', () => {
        useUiStore.getState().setModalState('help')
        expect(useUiStore.getState().isModalOpen).toBe(true)
        
        useUiStore.getState().setModalState('settings')
        
        expect(useUiStore.getState().isModalOpen).toBe(true)
      })
    })

    describe('session lock behavior', () => {
      it('sets sessionLocked: true when opening session-expired modal', () => {
        useUiStore.getState().setModalState('session-expired')
        
        const state = useUiStore.getState()
        expect(state.sessionLocked).toBe(true)
        expect(state.isModalOpen).toBe(true)
        expect(state.modalKey).toBe('session-expired')
      })

      it('does not set sessionLocked for other modals', () => {
        const otherModals: ModalKey[] = ['help', 'settings', 'error-schema', 'saves']
        
        otherModals.forEach((key) => {
          // Reset state
          useUiStore.setState({ isModalOpen: false, modalKey: undefined, sessionLocked: false })
          
          useUiStore.getState().setModalState(key)
          
          expect(useUiStore.getState().sessionLocked).toBe(false)
        })
      })

      it('unlocks session when closing session-expired modal', () => {
        useUiStore.getState().setModalState('session-expired')
        expect(useUiStore.getState().sessionLocked).toBe(true)
        
        useUiStore.getState().setModalState(undefined)
        
        expect(useUiStore.getState().sessionLocked).toBe(false)
      })

      it('maintains sessionLocked when switching from session-expired to another modal', () => {
        useUiStore.getState().setModalState('session-expired')
        expect(useUiStore.getState().sessionLocked).toBe(true)
        
        useUiStore.getState().setModalState('help')
        
        expect(useUiStore.getState().sessionLocked).toBe(false)
      })
    })
  })

  describe('setSessionLocked', () => {
    it('locks session when set to true', () => {
      useUiStore.getState().setSessionLocked(true)
      
      expect(useUiStore.getState().sessionLocked).toBe(true)
    })

    it('unlocks session when set to false', () => {
      useUiStore.getState().setSessionLocked(true)
      expect(useUiStore.getState().sessionLocked).toBe(true)
      
      useUiStore.getState().setSessionLocked(false)
      
      expect(useUiStore.getState().sessionLocked).toBe(false)
    })

    it('can be called independently of setModalState', () => {
      expect(useUiStore.getState().sessionLocked).toBe(false)
      
      useUiStore.getState().setSessionLocked(true)
      
      expect(useUiStore.getState().sessionLocked).toBe(true)
      expect(useUiStore.getState().isModalOpen).toBe(false)
      expect(useUiStore.getState().modalKey).toBeUndefined()
    })
  })

  describe('setSchemaError', () => {
    it('stores schema error with code, message, and details', () => {
      const error = {
        code: 'SCHEMA_MISMATCH',
        message: 'Save schema mismatch',
        details: { version: 2, expectedVersion: 1 },
      }
      
      useUiStore.getState().setSchemaError(error)
      
      const state = useUiStore.getState()
      expect(state.schemaError).toEqual(error)
    })

    it('stores schema error without details', () => {
      const error = {
        code: 'MAP_SCHEMA_MISMATCH',
        message: 'Map schema version mismatch',
      }
      
      useUiStore.getState().setSchemaError(error)
      
      const state = useUiStore.getState()
      expect(state.schemaError).toEqual(error)
    })

    it('clears schema error when set to undefined', () => {
      // First set an error
      useUiStore.getState().setSchemaError({
        code: 'SCHEMA_MISMATCH',
        message: 'Error',
      })
      expect(useUiStore.getState().schemaError).toBeDefined()
      
      // Then clear it
      useUiStore.getState().setSchemaError(undefined)
      
      expect(useUiStore.getState().schemaError).toBeUndefined()
    })

    it('overwrites previous schema error', () => {
      const error1 = {
        code: 'SCHEMA_MISMATCH',
        message: 'First error',
        details: { version: 1 },
      }
      const error2 = {
        code: 'MAP_SCHEMA_MISMATCH',
        message: 'Second error',
        details: { version: 2 },
      }
      
      useUiStore.getState().setSchemaError(error1)
      expect(useUiStore.getState().schemaError).toEqual(error1)
      
      useUiStore.getState().setSchemaError(error2)
      
      expect(useUiStore.getState().schemaError).toEqual(error2)
    })

    it('stores error with complex details object', () => {
      const error = {
        code: 'SCHEMA_MISMATCH',
        message: 'Complex error',
        details: {
          version: 3,
          expectedVersion: 2,
          fields: ['player', 'map', 'units'],
          timestamp: '2024-10-30T12:00:00Z',
          nested: {
            deep: {
              value: 42,
            },
          },
        },
      }
      
      useUiStore.getState().setSchemaError(error)
      
      expect(useUiStore.getState().schemaError).toEqual(error)
    })
  })

  describe('state independence', () => {
    it('modal state does not affect schema error', () => {
      const error = {
        code: 'SCHEMA_MISMATCH',
        message: 'Error',
      }
      
      useUiStore.getState().setSchemaError(error)
      useUiStore.getState().setModalState('help')
      
      const state = useUiStore.getState()
      expect(state.schemaError).toEqual(error)
      expect(state.modalKey).toBe('help')
    })

    it('schema error does not affect modal state', () => {
      useUiStore.getState().setModalState('settings')
      
      const error = {
        code: 'SCHEMA_MISMATCH',
        message: 'Error',
      }
      useUiStore.getState().setSchemaError(error)
      
      const state = useUiStore.getState()
      expect(state.isModalOpen).toBe(true)
      expect(state.modalKey).toBe('settings')
    })

    it('session lock does not affect schema error', () => {
      const error = {
        code: 'SCHEMA_MISMATCH',
        message: 'Error',
      }
      
      useUiStore.getState().setSchemaError(error)
      useUiStore.getState().setSessionLocked(true)
      
      expect(useUiStore.getState().schemaError).toEqual(error)
    })
  })

  describe('complex workflows', () => {
    it('handles typical error-schema modal flow', () => {
      // Schema error occurs
      const error = {
        code: 'SCHEMA_MISMATCH',
        message: 'Version mismatch',
        details: { version: 2 },
      }
      useUiStore.getState().setSchemaError(error)
      useUiStore.getState().setModalState('error-schema')
      
      let state = useUiStore.getState()
      expect(state.schemaError).toEqual(error)
      expect(state.isModalOpen).toBe(true)
      expect(state.modalKey).toBe('error-schema')
      
      // User dismisses modal
      useUiStore.getState().setModalState(undefined)
      useUiStore.getState().setSchemaError(undefined)
      
      state = useUiStore.getState()
      expect(state.schemaError).toBeUndefined()
      expect(state.isModalOpen).toBe(false)
      expect(state.modalKey).toBeUndefined()
    })

    it('handles typical session-expired flow', () => {
      // Session expires
      useUiStore.getState().setModalState('session-expired')
      
      let state = useUiStore.getState()
      expect(state.sessionLocked).toBe(true)
      expect(state.isModalOpen).toBe(true)
      expect(state.modalKey).toBe('session-expired')
      
      // User logs back in (simulated by closing modal)
      useUiStore.getState().setModalState(undefined)
      
      state = useUiStore.getState()
      expect(state.sessionLocked).toBe(false)
      expect(state.isModalOpen).toBe(false)
      expect(state.modalKey).toBeUndefined()
    })

    it('handles opening multiple modals sequentially', () => {
      useUiStore.getState().setModalState('help')
      expect(useUiStore.getState().modalKey).toBe('help')
      
      useUiStore.getState().setModalState(undefined)
      expect(useUiStore.getState().modalKey).toBeUndefined()
      
      useUiStore.getState().setModalState('settings')
      expect(useUiStore.getState().modalKey).toBe('settings')
      
      useUiStore.getState().setModalState(undefined)
      expect(useUiStore.getState().modalKey).toBeUndefined()
      
      useUiStore.getState().setModalState('session-expired')
      expect(useUiStore.getState().modalKey).toBe('session-expired')
      expect(useUiStore.getState().sessionLocked).toBe(true)
    })
  })
})

