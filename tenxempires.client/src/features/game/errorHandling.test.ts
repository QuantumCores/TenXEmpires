import { describe, it, expect, vi, beforeEach } from 'vitest'

let notifyAdd = vi.fn()
let openModal = vi.fn()
let setSchemaError = vi.fn()

vi.mock('../../components/ui/notifications', () => ({
  useNotifications: () => ({ add: notifyAdd }),
}))

vi.mock('../../router/query', () => ({
  useModalParam: () => ({ state: { modal: undefined }, openModal }),
}))

vi.mock('../../components/ui/uiStore', () => ({
  // Simulate zustand selector usage
  useUiStore: <T,>(selector: (state: { setSchemaError: typeof setSchemaError }) => T) => 
    selector({ setSchemaError }),
}))

import { useGameErrorHandler } from './errorHandling'

describe('useGameErrorHandler schema mismatch', () => {
  beforeEach(() => {
    notifyAdd = vi.fn()
    openModal = vi.fn()
    setSchemaError = vi.fn()
  })

  it('opens error-schema modal and stores error on SCHEMA_MISMATCH (422)', () => {
    const { handleError } = useGameErrorHandler()

    handleError({
      ok: false,
      status: 422,
      data: { code: 'SCHEMA_MISMATCH', message: 'Save schema mismatch' },
    })

    expect(setSchemaError).toHaveBeenCalled()
    expect(openModal).toHaveBeenCalledWith('error-schema', undefined, 'replace')
  })

  it('opens error-schema modal and stores error on MAP_SCHEMA_MISMATCH (422)', () => {
    const { handleError } = useGameErrorHandler()

    handleError({
      ok: false,
      status: 422,
      data: { code: 'MAP_SCHEMA_MISMATCH', message: 'Map schema mismatch' },
    })

    expect(setSchemaError).toHaveBeenCalled()
    expect(openModal).toHaveBeenCalledWith('error-schema', undefined, 'replace')
  })
})

