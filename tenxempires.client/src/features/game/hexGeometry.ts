/**
 * Hexagonal grid geometry utilities for pointy-top hexagons using odd-r offset coordinates.
 * Matches backend implementation in HexagonalGrid.cs
 */

// ============================================================================
// Constants
// ============================================================================

// Pointy-top hex dimensions
export const HEX_SIZE = 32 // Distance from center to vertex
export const HEX_WIDTH = HEX_SIZE * Math.sqrt(3) // ~55.4
export const HEX_HEIGHT = HEX_SIZE * 2 // 64
export const HEX_VERT_SPACING = HEX_SIZE * 1.5 // 48

// ============================================================================
// Coordinate Types
// ============================================================================

export interface CubeCoord {
  x: number
  y: number
  z: number
}

export interface SquareCoord {
  x: number
  y: number
}

export interface PixelCoord {
  x: number
  y: number
}

// ============================================================================
// Coordinate Conversion
// ============================================================================

/**
 * Converts odd-r offset coordinates to cube coordinates.
 * Matches HexagonalGrid.ConvertOddrToCube
 */
export function oddrToCube(col: number, row: number): CubeCoord {
  const x = col - (row - (row & 1)) / 2
  const z = row
  const y = -x - z
  return { x, y, z }
}

/**
 * Converts cube coordinates to odd-r offset coordinates.
 * Matches HexagonalGrid.ConvertCubeToOddr
 */
export function cubeToOddr(cube: CubeCoord): SquareCoord {
  const col = cube.x + (cube.z - (cube.z & 1)) / 2
  const row = cube.z
  return { x: col, y: row }
}

/**
 * Converts odd-r offset coordinates to pixel coordinates (pointy-top).
 */
export function oddrToPixel(col: number, row: number): PixelCoord {
  const x = HEX_WIDTH * (col + 0.5 * (row & 1))
  const y = HEX_VERT_SPACING * row
  return { x, y }
}

/**
 * Converts pixel coordinates to odd-r offset coordinates (pointy-top).
 * Returns the hex that contains the given pixel point.
 */
export function pixelToOddr(x: number, y: number): SquareCoord {
  // Fractional odd-r coordinates
  const row = y / HEX_VERT_SPACING
  const col = (x - HEX_WIDTH * 0.5 * (Math.floor(row) & 1)) / HEX_WIDTH

  // Convert to cube for rounding
  const cubeX = col - (Math.floor(row) - (Math.floor(row) & 1)) / 2
  const cubeZ = Math.floor(row)
  const cubeY = -cubeX - cubeZ

  // Round cube coordinates
  const rounded = roundCube({ x: cubeX, y: cubeY, z: cubeZ })

  // Convert back to odd-r
  return cubeToOddr(rounded)
}

/**
 * Rounds fractional cube coordinates to the nearest hex.
 */
function roundCube(cube: CubeCoord): CubeCoord {
  let rx = Math.round(cube.x)
  let ry = Math.round(cube.y)
  let rz = Math.round(cube.z)

  const xDiff = Math.abs(rx - cube.x)
  const yDiff = Math.abs(ry - cube.y)
  const zDiff = Math.abs(rz - cube.z)

  if (xDiff > yDiff && xDiff > zDiff) {
    rx = -ry - rz
  } else if (yDiff > zDiff) {
    ry = -rx - rz
  } else {
    rz = -rx - ry
  }

  return { x: rx, y: ry, z: rz }
}

// ============================================================================
// Distance and Pathfinding
// ============================================================================

/**
 * Calculates hexagonal distance between two odd-r coordinates.
 * Matches HexagonalGrid.GetCubeDistance
 */
export function hexDistance(col1: number, row1: number, col2: number, row2: number): number {
  const a = oddrToCube(col1, row1)
  const b = oddrToCube(col2, row2)
  return (Math.abs(a.x - b.x) + Math.abs(a.y - b.y) + Math.abs(a.z - b.z)) / 2
}

/**
 * Gets all six neighbor coordinates in odd-r offset system.
 */
export function getNeighbors(col: number, row: number): SquareCoord[] {
  const cube = oddrToCube(col, row)
  const cubeDirections: CubeCoord[] = [
    { x: +1, y: 0, z: -1 },
    { x: +1, y: -1, z: 0 },
    { x: 0, y: -1, z: +1 },
    { x: -1, y: 0, z: +1 },
    { x: -1, y: +1, z: 0 },
    { x: 0, y: +1, z: -1 },
  ]

  return cubeDirections.map((dir) => {
    const neighbor = { x: cube.x + dir.x, y: cube.y + dir.y, z: cube.z + dir.z }
    return cubeToOddr(neighbor)
  })
}

/**
 * Checks if a hex is within range of another hex.
 */
export function isInRange(
  fromCol: number,
  fromRow: number,
  toCol: number,
  toRow: number,
  range: number
): boolean {
  return hexDistance(fromCol, fromRow, toCol, toRow) <= range
}

// ============================================================================
// Rendering Helpers
// ============================================================================

/**
 * Gets the six vertices of a pointy-top hexagon centered at (x, y).
 */
export function getHexVertices(centerX: number, centerY: number): PixelCoord[] {
  const vertices: PixelCoord[] = []
  for (let i = 0; i < 6; i++) {
    const angleDeg = 60 * i - 30 // Start at -30° for pointy-top
    const angleRad = (Math.PI / 180) * angleDeg
    vertices.push({
      x: centerX + HEX_SIZE * Math.cos(angleRad),
      y: centerY + HEX_SIZE * Math.sin(angleRad),
    })
  }
  return vertices
}

/**
 * Draws a hexagon path on a canvas context.
 */
export function drawHexPath(
  ctx: CanvasRenderingContext2D,
  centerX: number,
  centerY: number
): void {
  const vertices = getHexVertices(centerX, centerY)
  ctx.beginPath()
  ctx.moveTo(vertices[0].x, vertices[0].y)
  for (let i = 1; i < 6; i++) {
    ctx.lineTo(vertices[i].x, vertices[i].y)
  }
  ctx.closePath()
}

/**
 * Checks if a pixel point is inside a hexagon.
 */
export function isPointInHex(
  pointX: number,
  pointY: number,
  hexCol: number,
  hexRow: number
): boolean {
  const center = oddrToPixel(hexCol, hexRow)
  const vertices = getHexVertices(center.x, center.y)

  // Use point-in-polygon algorithm
  let inside = false
  for (let i = 0, j = vertices.length - 1; i < vertices.length; j = i++) {
    const xi = vertices[i].x
    const yi = vertices[i].y
    const xj = vertices[j].x
    const yj = vertices[j].y

    const intersect = yi > pointY !== yj > pointY && pointX < ((xj - xi) * (pointY - yi)) / (yj - yi) + xi
    if (intersect) inside = !inside
  }
  return inside
}

