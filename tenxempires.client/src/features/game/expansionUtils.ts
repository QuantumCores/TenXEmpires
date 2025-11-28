import type { GameStateDto, MapTileDto } from '../../types/game'
import { hexDistance } from './hexGeometry'

// Configuration constants (should match server)
export const EXPANSION_BASE_COST = 20
export const EXPANSION_COST_PER_TILE = 10
export const INITIAL_CITY_TILES = 7
export const MAX_EXPANSION_DISTANCE = 2

/**
 * Calculates the cost to expand territory based on current size.
 * Formula: BaseCost + ((ControlledTilesCount - InitialTilesCount) * 10)
 */
export function calculateExpansionCost(controlledTilesCount: number): number {
  const extraTiles = Math.max(0, controlledTilesCount - INITIAL_CITY_TILES)
  return EXPANSION_BASE_COST + (extraTiles * EXPANSION_COST_PER_TILE)
}

/**
 * Returns tile IDs that are valid expansion targets.
 * Valid targets are:
 * 1. Adjacent to currently owned tiles
 * 2. Within 2 hex distance from city center
 * 3. Not already owned by this city (or any city)
 * 4. Not occupied by enemy units
 * 5. Not water/ocean
 * 6. Not owned by enemy city
 */
export function getValidExpansionTiles(
  cityId: number,
  gameState: GameStateDto,
  mapTiles: MapTileDto[]
): number[] {
  const city = gameState.cities.find(c => c.id === cityId)
  if (!city) return []

  // Get currently owned tiles
  const ownedTileIds = new Set(
    gameState.cityTiles
      .filter(ct => ct.cityId === cityId)
      .map(ct => ct.tileId)
  )

  // Get all tiles owned by ANY city (to exclude them)
  const allOwnedTileIds = new Set(
    gameState.cityTiles.map(ct => ct.tileId)
  )

  // Get enemy unit positions
  const enemyUnitTileIds = new Set(
    gameState.units
      .filter(u => u.participantId !== city.participantId)
      .map(u => u.tileId)
  )

  // Map tiles lookup
  const tilesById = new Map(mapTiles.map(t => [t.id, t]))
  
  // Find potential candidates (adjacent to owned AND within 2 hex of city center)
  const candidateTileIds = new Set<number>()

  ownedTileIds.forEach(tileId => {
    const tile = tilesById.get(tileId)
    if (!tile) return

    // Iterate all map tiles to find neighbors (simple distance check)
    mapTiles.forEach(candidate => {
      if (ownedTileIds.has(candidate.id)) return // Already owned
      if (candidateTileIds.has(candidate.id)) return // Already added
      
      // Check if adjacent to this owned tile
      const distToOwned = hexDistance(
        tile.col, tile.row,
        candidate.col, candidate.row
      )
      
      if (distToOwned !== 1) return // Not adjacent
      
      // Check distance from city center (must be within MAX_EXPANSION_DISTANCE)
      const distToCity = hexDistance(
        city.col, city.row,
        candidate.col, candidate.row
      )
      
      if (distToCity <= MAX_EXPANSION_DISTANCE) {
        candidateTileIds.add(candidate.id)
      }
    })
  })

  // Filter candidates
  return Array.from(candidateTileIds).filter(tileId => {
    const tile = tilesById.get(tileId)
    if (!tile) return false

    // Exclude water
    if (tile.terrain === 'water' || tile.terrain === 'ocean') return false

    // Exclude tiles owned by anyone
    if (allOwnedTileIds.has(tileId)) return false

    // Exclude tiles with enemy units
    if (enemyUnitTileIds.has(tileId)) return false

    return true
  })
}
