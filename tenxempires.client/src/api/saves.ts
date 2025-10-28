import { getJson, postJson, deleteJson } from './http'
import type {
  GameSavesListDto,
  CreateManualSaveCommand,
  SaveCreatedDto,
  LoadSaveResponse,
} from '../types/saves'

const API_BASE = '/api'

// ============================================================================
// Saves Endpoints
// ============================================================================

/**
 * GET /games/{id}/saves
 * List all saves (manual + autosaves) for a game
 */
export async function fetchGameSaves(gameId: number) {
  return getJson<GameSavesListDto>(`${API_BASE}/games/${gameId}/saves`)
}

/**
 * POST /games/{id}/saves/manual
 * Create or overwrite manual save in specified slot
 */
export async function createManualSave(
  gameId: number,
  command: CreateManualSaveCommand,
  idempotencyKey: string
) {
  return postJson<CreateManualSaveCommand, SaveCreatedDto>(
    `${API_BASE}/games/${gameId}/saves/manual`,
    command,
    { headers: { 'Idempotency-Key': idempotencyKey } }
  )
}

/**
 * DELETE /games/{id}/saves/manual/{slot}
 * Delete manual save in specified slot
 */
export async function deleteManualSave(gameId: number, slot: number) {
  return deleteJson<void>(`${API_BASE}/games/${gameId}/saves/manual/${slot}`)
}

/**
 * POST /saves/{saveId}/load
 * Load save and replace current game state
 */
export async function loadSave(saveId: number, idempotencyKey: string) {
  return postJson<object, LoadSaveResponse>(
    `${API_BASE}/saves/${saveId}/load`,
    {},
    { headers: { 'Idempotency-Key': idempotencyKey } }
  )
}

