import { getJson, postJson, deleteJson } from './http'
import type {
  GameStateDto,
  UnitDefinitionsResponse,
  MapTilesResponse,
  MoveUnitCommand,
  AttackUnitCommand,
  EndTurnCommand,
  ActionStateResponse,
  EndTurnResponse,
  GamesListResponse,
} from '../types/game'
import type { CreateGameCommand, GameCreatedResponse } from '../types/games'

const API_BASE = '/api'

// ============================================================================
// Game State
// ============================================================================

export async function fetchGameState(gameId: number, etag?: string) {
  const headers: HeadersInit = {}
  if (etag) {
    headers['If-None-Match'] = etag
  }
  return getJson<GameStateDto>(`${API_BASE}/games/${gameId}/state`, { headers })
}

// ============================================================================
// Lookups
// ============================================================================

export async function fetchUnitDefinitions() {
  return getJson<UnitDefinitionsResponse>(`${API_BASE}/unit-definitions`)
}

export async function fetchMapTiles(mapCode: string, etag?: string) {
  const headers: HeadersInit = {}
  if (etag) {
    headers['If-None-Match'] = etag
  }
  // Backend defaults to pageSize=500, which is enough for standard maps (20x15 = 300 tiles)
  return getJson<MapTilesResponse>(`${API_BASE}/maps/${mapCode}/tiles`, { headers })
}

// ============================================================================
// Game Actions
// ============================================================================

export async function moveUnit(gameId: number, command: MoveUnitCommand, idempotencyKey: string) {
  return postJson<MoveUnitCommand, ActionStateResponse>(
    `${API_BASE}/games/${gameId}/actions/move`,
    command,
    { headers: { 'Idempotency-Key': idempotencyKey } }
  )
}

export async function attackUnit(gameId: number, command: AttackUnitCommand, idempotencyKey: string) {
  return postJson<AttackUnitCommand, ActionStateResponse>(
    `${API_BASE}/games/${gameId}/actions/attack`,
    command,
    { headers: { 'Idempotency-Key': idempotencyKey } }
  )
}

export async function endTurn(gameId: number, idempotencyKey: string) {
  return postJson<EndTurnCommand, EndTurnResponse>(
    `${API_BASE}/games/${gameId}/end-turn`,
    {},
    { headers: { 'Idempotency-Key': idempotencyKey } }
  )
}

// ============================================================================
// Games List
// ============================================================================

export interface ListGamesParams {
  status?: string
  page?: number
  pageSize?: number
  sort?: string
  order?: string
}

export async function fetchGames(params?: ListGamesParams) {
  const query = new URLSearchParams()
  if (params?.status) query.append('status', params.status)
  if (params?.page) query.append('page', String(params.page))
  if (params?.pageSize) query.append('pageSize', String(params.pageSize))
  if (params?.sort) query.append('sort', params.sort)
  if (params?.order) query.append('order', params.order)
  
  const queryString = query.toString()
  const url = queryString ? `${API_BASE}/games?${queryString}` : `${API_BASE}/games`
  
  return getJson<GamesListResponse>(url)
}

// ============================================================================
// Game Management
// ============================================================================

export async function createGame(command: CreateGameCommand, idempotencyKey: string) {
  return postJson<CreateGameCommand, GameCreatedResponse>(
    `${API_BASE}/games`,
    command,
    { headers: { 'Idempotency-Key': idempotencyKey } }
  )
}

export async function deleteGame(gameId: number) {
  return deleteJson<void>(`${API_BASE}/games/${gameId}`)
}

