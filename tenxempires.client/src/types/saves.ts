// Saves types aligned with TenXEmpires.Server.Domain.DataContracts.Saves

import type { GameStateDto } from './game'

// ============================================================================
// Saves DTOs
// ============================================================================

export interface SaveManualDto {
  id: number
  slot: number
  turnNo: number
  createdAt: string
  name: string
}

export interface SaveAutosaveDto {
  id: number
  turnNo: number
  createdAt: string
}

export interface GameSavesListDto {
  manual: SaveManualDto[]
  autosaves: SaveAutosaveDto[]
}

// ============================================================================
// Command DTOs
// ============================================================================

export interface CreateManualSaveCommand {
  slot: number
  name: string
}

// ============================================================================
// Response DTOs
// ============================================================================

export interface SaveCreatedDto {
  id: number
  slot: number
  turnNo: number
  createdAt: string
  name: string
}

export interface LoadSaveResponse {
  gameId: number
  state: GameStateDto
}

// ============================================================================
// View Models (Client-only)
// ============================================================================

export interface OverwriteConfirm {
  slot: 1 | 2 | 3
  oldName?: string
  newName: string
}

