// Shared API result types aligned with server defaults (camelCase JSON)

export interface PagedResult<T> {
  items: T[]
  page: number
  pageSize: number
  total?: number
}

export interface GameSummary {
  id: number
  status: string
  turnNo: number
  mapId: number
  mapSchemaVersion: number
  startedAt: string
  finishedAt?: string | null
  lastTurnAt?: string | null
}

export interface CurrentUser {
  id: string
  email: string | null
}
