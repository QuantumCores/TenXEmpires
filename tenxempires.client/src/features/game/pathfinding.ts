import { oddrToCube, cubeToOddr, hexDistance, getNeighbors } from './hexGeometry'
import type { CubeCoord, SquareCoord } from './hexGeometry'
import type { GridPosition } from '../../types/game'

/**
 * A* pathfinding for hexagonal grids using odd-r coordinates.
 * Matches backend PathfindingHelper.cs implementation.
 */

interface PathNode {
  position: CubeCoord
  gScore: number
  hScore: number
  fScore: number
}

/**
 * Finds the shortest path from start to end using A* algorithm.
 * Returns the path as a list of grid positions, or null if no path exists.
 */
export function findPath(
  start: GridPosition,
  end: GridPosition,
  maxMovePoints: number,
  mapWidth: number,
  mapHeight: number,
  isBlocked: (pos: GridPosition) => boolean
): GridPosition[] | null {
  // Quick validation
  if (start.row === end.row && start.col === end.col) {
    return [start]
  }

  if (!isInBounds(end, mapWidth, mapHeight)) {
    return null
  }

  if (isBlocked(end)) {
    return null
  }

  // Convert to cube coordinates
  const startCube = oddrToCube(start.col, start.row)
  const endCube = oddrToCube(end.col, end.row)

  // Priority queue (min-heap by f-score)
  const openSet: PathNode[] = []
  const openSetLookup = new Set<string>()
  const closedSet = new Set<string>()
  const cameFrom = new Map<string, CubeCoord>()
  const gScore = new Map<string, number>()

  const startKey = cubeKey(startCube)
  gScore.set(startKey, 0)

  const startNode: PathNode = {
    position: startCube,
    gScore: 0,
    hScore: cubeDistance(startCube, endCube),
    fScore: cubeDistance(startCube, endCube),
  }

  pushToQueue(openSet, startNode)
  openSetLookup.add(startKey)

  while (openSet.length > 0) {
    const current = popFromQueue(openSet)
    const currentKey = cubeKey(current.position)
    openSetLookup.delete(currentKey)

    // Check if we've reached the destination
    if (cubeEquals(current.position, endCube)) {
      return reconstructPath(cameFrom, current.position)
    }

    closedSet.add(currentKey)

    // Explore neighbors
    const neighborsCube = getHexNeighborsCube(current.position)

    for (const neighborCube of neighborsCube) {
      const neighborKey = cubeKey(neighborCube)

      if (closedSet.has(neighborKey)) {
        continue
      }

      // Convert to offset for bounds check
      const neighborSquare = cubeToOddr(neighborCube)

      // Check bounds
      if (!isInBoundsSquare(neighborSquare, mapWidth, mapHeight)) {
        continue
      }

      const neighborGridPos: GridPosition = { row: neighborSquare.y, col: neighborSquare.x }

      // Check if blocked (allow destination even if occupied)
      if (isBlocked(neighborGridPos) && !cubeEquals(neighborCube, endCube)) {
        continue
      }

      // Calculate tentative g-score (uniform cost of 1 per tile)
      const tentativeGScore = current.gScore + 1

      // Check movement range
      if (tentativeGScore > maxMovePoints) {
        continue
      }

      // Check if this is a better path
      const currentGScore = gScore.get(neighborKey)
      if (currentGScore === undefined || tentativeGScore < currentGScore) {
        cameFrom.set(neighborKey, current.position)
        gScore.set(neighborKey, tentativeGScore)

        const hScore = cubeDistance(neighborCube, endCube)
        const fScore = tentativeGScore + hScore

        if (!openSetLookup.has(neighborKey)) {
          const neighborNode: PathNode = {
            position: neighborCube,
            gScore: tentativeGScore,
            hScore,
            fScore,
          }
          pushToQueue(openSet, neighborNode)
          openSetLookup.add(neighborKey)
        }
      }
    }
  }

  // No path found
  return null
}

/**
 * Gets all tiles reachable within a given range.
 */
export function getReachableTiles(
  start: GridPosition,
  maxMovePoints: number,
  mapWidth: number,
  mapHeight: number,
  isBlocked: (pos: GridPosition) => boolean
): GridPosition[] {
  const reachable: GridPosition[] = []
  const visited = new Set<string>()
  const queue: Array<{ pos: GridPosition; cost: number }> = []

  queue.push({ pos: start, cost: 0 })
  visited.add(gridKey(start))

  while (queue.length > 0) {
    const current = queue.shift()!

    reachable.push(current.pos)

    if (current.cost >= maxMovePoints) {
      continue
    }

    const neighbors = getNeighbors(current.pos.col, current.pos.row)

    for (const neighbor of neighbors) {
      const neighborGrid: GridPosition = { row: neighbor.y, col: neighbor.x }
      const key = gridKey(neighborGrid)

      if (visited.has(key)) {
        continue
      }

      if (!isInBounds(neighborGrid, mapWidth, mapHeight)) {
        continue
      }

      if (isBlocked(neighborGrid)) {
        continue
      }

      visited.add(key)
      queue.push({ pos: neighborGrid, cost: current.cost + 1 })
    }
  }

  return reachable
}

/**
 * Gets all tiles within attack range (including blocked tiles).
 */
export function getAttackableTiles(
  start: GridPosition,
  rangeMin: number,
  rangeMax: number,
  mapWidth: number,
  mapHeight: number
): GridPosition[] {
  const attackable: GridPosition[] = []

  // Simple flood fill within range
  for (let row = 0; row < mapHeight; row++) {
    for (let col = 0; col < mapWidth; col++) {
      const distance = hexDistance(start.col, start.row, col, row)
      if (distance >= rangeMin && distance <= rangeMax) {
        attackable.push({ row, col })
      }
    }
  }

  return attackable
}

// ============================================================================
// Helper Functions
// ============================================================================

function cubeKey(cube: CubeCoord): string {
  return `${cube.x},${cube.y},${cube.z}`
}

function gridKey(pos: GridPosition): string {
  return `${pos.row},${pos.col}`
}

function cubeEquals(a: CubeCoord, b: CubeCoord): boolean {
  return a.x === b.x && a.y === b.y && a.z === b.z
}

function cubeDistance(a: CubeCoord, b: CubeCoord): number {
  return (Math.abs(a.x - b.x) + Math.abs(a.y - b.y) + Math.abs(a.z - b.z)) / 2
}

function getHexNeighborsCube(cube: CubeCoord): CubeCoord[] {
  const directions: CubeCoord[] = [
    { x: +1, y: 0, z: -1 },
    { x: +1, y: -1, z: 0 },
    { x: 0, y: -1, z: +1 },
    { x: -1, y: 0, z: +1 },
    { x: -1, y: +1, z: 0 },
    { x: 0, y: +1, z: -1 },
  ]

  return directions.map((dir) => ({
    x: cube.x + dir.x,
    y: cube.y + dir.y,
    z: cube.z + dir.z,
  }))
}

function reconstructPath(
  cameFrom: Map<string, CubeCoord>,
  current: CubeCoord
): GridPosition[] {
  const path: GridPosition[] = []
  let currentCube = current

  // Build path from end to start
  let square = cubeToOddr(currentCube)
  path.push({ row: square.y, col: square.x })

  while (true) {
    const key = cubeKey(currentCube)
    const prev = cameFrom.get(key)
    if (!prev) break

    currentCube = prev
    square = cubeToOddr(currentCube)
    path.push({ row: square.y, col: square.x })
  }

  // Reverse to get path from start to end
  path.reverse()
  return path
}

function isInBounds(pos: GridPosition, mapWidth: number, mapHeight: number): boolean {
  return pos.row >= 0 && pos.row < mapHeight && pos.col >= 0 && pos.col < mapWidth
}

function isInBoundsSquare(pos: SquareCoord, mapWidth: number, mapHeight: number): boolean {
  return pos.y >= 0 && pos.y < mapHeight && pos.x >= 0 && pos.x < mapWidth
}

// Simple priority queue implementation
function pushToQueue(queue: PathNode[], node: PathNode): void {
  queue.push(node)
  queue.sort((a, b) => a.fScore - b.fScore)
}

function popFromQueue(queue: PathNode[]): PathNode {
  return queue.shift()!
}

