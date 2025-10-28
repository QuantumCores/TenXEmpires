// Games-specific types aligned with TenXEmpires.Server.Domain.DataContracts

import type { GameStateDto } from './game'

export interface CreateGameCommand {
  mapCode?: string
  settings?: Record<string, unknown>
}

export interface GameCreatedResponse {
  id: number
  state: GameStateDto
}

