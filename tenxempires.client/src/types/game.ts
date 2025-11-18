// Game Map types aligned with TenXEmpires.Server.Domain.DataContracts

// ============================================================================
// Game State DTOs
// ============================================================================

export interface GameStateDto {
  game: GameStateGameDto
  map: GameStateMapDto
  participants: ParticipantDto[]
  units: UnitInStateDto[]
  cities: CityInStateDto[]
  cityTiles: CityTileLinkDto[]
  cityResources: CityResourceDto[]
  unitDefinitions: UnitDefinitionDto[]
  turnSummary?: Record<string, unknown> | null
}

export interface GameStateGameDto {
  id: number
  turnNo: number
  activeParticipantId: number | null
  turnInProgress: boolean
  status: string
}

export interface GameStateMapDto {
  id: number
  code: string
  schemaVersion: number
  width: number
  height: number
}

export interface ParticipantDto {
  id: number
  gameId: number
  kind: string
  userId: string | null
  displayName: string
  isEliminated: boolean
}

export interface UnitInStateDto {
  id: number
  participantId: number
  typeCode: string
  hp: number
  hasActed: boolean
  tileId: number
  row: number
  col: number
}

export interface CityInStateDto {
  id: number
  participantId: number
  hp: number
  maxHp: number
  tileId: number
  row: number
  col: number
}

export interface CityTileLinkDto {
  cityId: number
  tileId: number
}

export interface CityResourceDto {
  cityId: number
  resourceType: string
  amount: number
}

// ============================================================================
// Lookup DTOs
// ============================================================================

export interface UnitDefinitionDto {
  id: number
  code: string
  isRanged: boolean
  attack: number
  defence: number
  rangeMin: number
  rangeMax: number
  movePoints: number
  health: number
}

export interface MapTileDto {
  id: number
  row: number
  col: number
  terrain: string
  resourceType: string | null
  resourceAmount: number
}

export interface MapDto {
  id: number
  code: string
  schemaVersion: number
  width: number
  height: number
}

// ============================================================================
// Command DTOs
// ============================================================================

export interface GridPosition {
  row: number
  col: number
}

export interface MoveUnitCommand {
  unitId: number
  to: GridPosition
}

export interface AttackUnitCommand {
  attackerUnitId: number
  targetUnitId: number
}

export interface AttackCityCommand {
  attackerUnitId: number
  targetCityId: number
}

// Empty command type for end turn action
// eslint-disable-next-line @typescript-eslint/no-empty-object-type
export interface EndTurnCommand {}

// ============================================================================
// Response DTOs
// ============================================================================

export interface ActionStateResponse {
  state: GameStateDto
}

export interface EndTurnResponse {
  state: GameStateDto
  turnSummary: Record<string, unknown>
  autosaveId: number
}

// ============================================================================
// View Models (Client-only)
// ============================================================================

export interface CameraState {
  scale: number
  offsetX: number
  offsetY: number
}

export interface SelectionState {
  kind: 'unit' | 'city' | 'tile' | null
  id?: number
}

export interface InteractionConfig {
  radii: {
    unit: number
    city: number
    feature: number
  }
  thresholds: {
    pan: number
  }
}

export interface TurnLogEntry {
  id: string
  at: string
  kind: 'move' | 'attack' | 'city' | 'save' | 'system'
  text: string
}

export interface UnitView extends UnitInStateDto {
  definition: UnitDefinitionDto
  movePointsLeft: number
}

export interface CityView extends CityInStateDto {
  workedTilesCount: number
  resources: CityResourceDto[]
  isUnderSiege: boolean
}

// ============================================================================
// API Response Wrappers
// ============================================================================

export interface UnitDefinitionsResponse {
  items: UnitDefinitionDto[]
}

export interface MapTilesResponse {
  items: MapTileDto[]
}

export interface GamesListResponse {
  items: GameSummary[]
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

