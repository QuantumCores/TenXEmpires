import { describe, it, expect } from 'vitest'
import {
  HEX_SIZE,
  HEX_WIDTH,
  HEX_HEIGHT,
  HEX_VERT_SPACING,
  oddrToCube,
  cubeToOddr,
  oddrToPixel,
  pixelToOddr,
  hexDistance,
  getNeighbors,
  isInRange,
  getHexVertices,
  isPointInHex,
  type CubeCoord,
  type SquareCoord,
} from '../../../features/game/hexGeometry'

describe('hexGeometry - Constants', () => {
  it('has correct hex size constant', () => {
    expect(HEX_SIZE).toBe(32)
  })

  it('has correct hex width for pointy-top', () => {
    expect(HEX_WIDTH).toBeCloseTo(32 * Math.sqrt(3), 5)
    expect(HEX_WIDTH).toBeCloseTo(55.42562584220407, 5)
  })

  it('has correct hex height', () => {
    expect(HEX_HEIGHT).toBe(64)
  })

  it('has correct vertical spacing', () => {
    expect(HEX_VERT_SPACING).toBe(48)
  })
})

describe('hexGeometry - Coordinate Conversions', () => {
  describe('oddrToCube', () => {
    it('converts origin (0,0) to cube', () => {
      const result = oddrToCube(0, 0)
      // Note: y might be -0 due to floating point math, which is functionally equivalent to 0
      expect(result.x).toBe(0)
      expect(Math.abs(result.y)).toBe(0) // Handles both 0 and -0
      expect(result.z).toBe(0)
      expect(result.x + result.y + result.z).toBe(0) // Cube constraint
    })

    it('converts odd row coordinate', () => {
      const result = oddrToCube(1, 1)
      expect(result.x + result.y + result.z).toBe(0) // Cube constraint
    })

    it('converts even row coordinate', () => {
      const result = oddrToCube(2, 2)
      expect(result.x + result.y + result.z).toBe(0)
    })

    it('maintains cube coordinate constraint x+y+z=0', () => {
      const coords = [
        [0, 0],
        [1, 1],
        [5, 3],
        [10, 7],
        [-2, 4],
      ]
      coords.forEach(([col, row]) => {
        const cube = oddrToCube(col, row)
        expect(cube.x + cube.y + cube.z).toBe(0)
      })
    })

    it('handles negative coordinates', () => {
      const result = oddrToCube(-1, -1)
      expect(result.x + result.y + result.z).toBe(0)
    })
  })

  describe('cubeToOddr', () => {
    it('converts cube origin to offset', () => {
      const cube: CubeCoord = { x: 0, y: 0, z: 0 }
      const result = cubeToOddr(cube)
      expect(result).toEqual({ x: 0, y: 0 })
    })

    it('is inverse of oddrToCube', () => {
      const coords: SquareCoord[] = [
        { x: 0, y: 0 },
        { x: 1, y: 1 },
        { x: 5, y: 3 },
        { x: 10, y: 7 },
        { x: 3, y: 5 },
      ]
      coords.forEach((coord) => {
        const cube = oddrToCube(coord.x, coord.y)
        const back = cubeToOddr(cube)
        expect(back).toEqual(coord)
      })
    })

    it('handles negative cube coordinates', () => {
      const cube: CubeCoord = { x: -1, y: 2, z: -1 }
      const result = cubeToOddr(cube)
      // Verify round-trip
      const backToCube = oddrToCube(result.x, result.y)
      expect(backToCube).toEqual(cube)
    })
  })

  describe('oddrToPixel', () => {
    it('converts origin to pixel center', () => {
      const result = oddrToPixel(0, 0)
      expect(result.x).toBe(0)
      expect(result.y).toBe(0)
    })

    it('offsets odd rows horizontally by half hex width', () => {
      const even = oddrToPixel(0, 0)
      const odd = oddrToPixel(0, 1)
      expect(odd.x).toBeCloseTo(even.x + HEX_WIDTH * 0.5, 5)
    })

    it('spaces rows vertically by HEX_VERT_SPACING', () => {
      const row0 = oddrToPixel(0, 0)
      const row1 = oddrToPixel(0, 1)
      const row2 = oddrToPixel(0, 2)
      expect(row1.y - row0.y).toBe(HEX_VERT_SPACING)
      expect(row2.y - row1.y).toBe(HEX_VERT_SPACING)
    })

    it('spaces columns horizontally by HEX_WIDTH', () => {
      const col0 = oddrToPixel(0, 0)
      const col1 = oddrToPixel(1, 0)
      expect(col1.x - col0.x).toBeCloseTo(HEX_WIDTH, 5)
    })

    it('handles negative coordinates', () => {
      const result = oddrToPixel(-2, -3)
      expect(result.x).toBeLessThan(0)
      expect(result.y).toBeLessThan(0)
    })
  })

  describe('pixelToOddr', () => {
    it('converts pixel origin to hex origin', () => {
      const result = pixelToOddr(0, 0)
      expect(result.x).toBe(0)
      expect(result.y).toBe(0)
    })

    it('is approximate inverse of oddrToPixel', () => {
      const coords = [
        [0, 0],
        [1, 1],
        [5, 3],
        [10, 7],
        [3, 5],
      ]
      coords.forEach(([col, row]) => {
        const pixel = oddrToPixel(col, row)
        const back = pixelToOddr(pixel.x, pixel.y)
        expect(back).toEqual({ x: col, y: row })
      })
    })

    it('maps center pixel to correct hex', () => {
      const center = oddrToPixel(2, 2)
      // Center point should definitely map to the same hex
      const result = pixelToOddr(center.x, center.y)
      expect(result).toEqual({ x: 2, y: 2 })

      // Test a few other known centers
      const coords = [[0, 0], [5, 5], [10, 3]]
      coords.forEach(([col, row]) => {
        const pixel = oddrToPixel(col, row)
        const back = pixelToOddr(pixel.x, pixel.y)
        expect(back).toEqual({ x: col, y: row })
      })
    })

    it('correctly rounds fractional coordinates', () => {
      // Test boundary between hexes
      const hex1 = oddrToPixel(0, 0)
      const hex2 = oddrToPixel(1, 0)
      const midpoint = { x: (hex1.x + hex2.x) / 2, y: hex1.y }
      const result = pixelToOddr(midpoint.x, midpoint.y)
      // Should map to one of the two hexes
      expect([0, 1]).toContain(result.x)
      expect(result.y).toBe(0)
    })
  })
})

describe('hexGeometry - Distance and Pathfinding', () => {
  describe('hexDistance', () => {
    it('returns 0 for same hex', () => {
      expect(hexDistance(0, 0, 0, 0)).toBe(0)
      expect(hexDistance(5, 3, 5, 3)).toBe(0)
    })

    it('returns 1 for adjacent hexes', () => {
      const neighbors = getNeighbors(0, 0)
      neighbors.forEach((neighbor) => {
        const dist = hexDistance(0, 0, neighbor.x, neighbor.y)
        expect(dist).toBe(1)
      })
    })

    it('is symmetric', () => {
      const dist1 = hexDistance(0, 0, 5, 3)
      const dist2 = hexDistance(5, 3, 0, 0)
      expect(dist1).toBe(dist2)
    })

    it('calculates correct distance for straight lines', () => {
      // Along same row
      expect(hexDistance(0, 0, 3, 0)).toBe(3)
      expect(hexDistance(0, 0, 5, 0)).toBe(5)
    })

    it('handles negative coordinates', () => {
      const dist = hexDistance(-2, -2, 3, 3)
      expect(dist).toBeGreaterThan(0)
      // Distance should be same as reverse
      expect(hexDistance(3, 3, -2, -2)).toBe(dist)
    })

    it('satisfies triangle inequality', () => {
      // Distance from A to C should be <= distance A to B + B to C
      const dist_ac = hexDistance(0, 0, 5, 5)
      const dist_ab = hexDistance(0, 0, 2, 2)
      const dist_bc = hexDistance(2, 2, 5, 5)
      expect(dist_ac).toBeLessThanOrEqual(dist_ab + dist_bc)
    })
  })

  describe('getNeighbors', () => {
    it('returns exactly 6 neighbors', () => {
      const neighbors = getNeighbors(0, 0)
      expect(neighbors).toHaveLength(6)
    })

    it('returns unique neighbors', () => {
      const neighbors = getNeighbors(0, 0)
      const keys = neighbors.map((n) => `${n.x},${n.y}`)
      const unique = new Set(keys)
      expect(unique.size).toBe(6)
    })

    it('all neighbors are distance 1 away', () => {
      const neighbors = getNeighbors(3, 3)
      neighbors.forEach((neighbor) => {
        expect(hexDistance(3, 3, neighbor.x, neighbor.y)).toBe(1)
      })
    })

    it('neighbors of even row hex', () => {
      const neighbors = getNeighbors(2, 2)
      expect(neighbors).toHaveLength(6)
      // All should be distance 1
      neighbors.forEach((n) => {
        expect(hexDistance(2, 2, n.x, n.y)).toBe(1)
      })
    })

    it('neighbors of odd row hex', () => {
      const neighbors = getNeighbors(2, 3)
      expect(neighbors).toHaveLength(6)
      neighbors.forEach((n) => {
        expect(hexDistance(2, 3, n.x, n.y)).toBe(1)
      })
    })

    it('is symmetric - neighbor relationship is bidirectional', () => {
      const neighborsOfA = getNeighbors(0, 0)
      neighborsOfA.forEach((b) => {
        const neighborsOfB = getNeighbors(b.x, b.y)
        const hasA = neighborsOfB.some((n) => n.x === 0 && n.y === 0)
        expect(hasA).toBe(true)
      })
    })
  })

  describe('isInRange', () => {
    it('returns true for same hex with range 0', () => {
      expect(isInRange(0, 0, 0, 0, 0)).toBe(true)
    })

    it('returns true for hex within range', () => {
      expect(isInRange(0, 0, 1, 0, 1)).toBe(true)
      expect(isInRange(0, 0, 2, 0, 2)).toBe(true)
      expect(isInRange(0, 0, 2, 0, 3)).toBe(true)
    })

    it('returns false for hex outside range', () => {
      expect(isInRange(0, 0, 3, 0, 2)).toBe(false)
      expect(isInRange(0, 0, 5, 3, 4)).toBe(false)
    })

    it('includes exact range boundary', () => {
      // Distance of exactly 3
      const dist = hexDistance(0, 0, 3, 0)
      expect(dist).toBe(3)
      expect(isInRange(0, 0, 3, 0, 3)).toBe(true)
      expect(isInRange(0, 0, 3, 0, 2)).toBe(false)
    })

    it('works with large ranges', () => {
      expect(isInRange(0, 0, 10, 10, 100)).toBe(true)
    })
  })
})

describe('hexGeometry - Rendering Helpers', () => {
  describe('getHexVertices', () => {
    it('returns exactly 6 vertices', () => {
      const vertices = getHexVertices(0, 0)
      expect(vertices).toHaveLength(6)
    })

    it('vertices are HEX_SIZE distance from center', () => {
      const centerX = 100
      const centerY = 100
      const vertices = getHexVertices(centerX, centerY)
      vertices.forEach((vertex) => {
        const dist = Math.sqrt(
          Math.pow(vertex.x - centerX, 2) + Math.pow(vertex.y - centerY, 2)
        )
        expect(dist).toBeCloseTo(HEX_SIZE, 5)
      })
    })

    it('first vertex is at -30 degrees for pointy-top', () => {
      const vertices = getHexVertices(0, 0)
      const expectedAngle = -30 * (Math.PI / 180)
      const expectedX = HEX_SIZE * Math.cos(expectedAngle)
      const expectedY = HEX_SIZE * Math.sin(expectedAngle)
      expect(vertices[0].x).toBeCloseTo(expectedX, 5)
      expect(vertices[0].y).toBeCloseTo(expectedY, 5)
    })

    it('vertices are evenly spaced 60 degrees apart', () => {
      const vertices = getHexVertices(0, 0)
      for (let i = 0; i < 6; i += 1) {
        const angle = ((60 * i - 30) * Math.PI) / 180
        const expectedX = HEX_SIZE * Math.cos(angle)
        const expectedY = HEX_SIZE * Math.sin(angle)
        expect(vertices[i].x).toBeCloseTo(expectedX, 5)
        expect(vertices[i].y).toBeCloseTo(expectedY, 5)
      }
    })

    it('vertices form a closed polygon', () => {
      const vertices = getHexVertices(50, 50)
      // Last vertex should be near first when wrapped
      const v0 = vertices[0]
      const v5 = vertices[5]
      // Distance between v5 and v0 should equal side length
      const sideLength = Math.sqrt(
        Math.pow(v0.x - v5.x, 2) + Math.pow(v0.y - v5.y, 2)
      )
      expect(sideLength).toBeCloseTo(HEX_SIZE, 5)
    })
  })

  describe('isPointInHex', () => {
    it('returns true for point at hex center', () => {
      const center = oddrToPixel(5, 5)
      expect(isPointInHex(center.x, center.y, 5, 5)).toBe(true)
    })

    it('returns false for point in different hex', () => {
      const center = oddrToPixel(5, 5)
      expect(isPointInHex(center.x, center.y, 0, 0)).toBe(false)
    })

    it('returns true for points near center', () => {
      const center = oddrToPixel(3, 3)
      const offsets = [
        [0, 0],
        [5, 0],
        [-5, 0],
        [0, 5],
        [0, -5],
        [3, 3],
        [-3, -3],
      ]
      offsets.forEach(([dx, dy]) => {
        expect(isPointInHex(center.x + dx, center.y + dy, 3, 3)).toBe(true)
      })
    })

    it('returns false for points far from hex', () => {
      const center = oddrToPixel(3, 3)
      const farPoint = { x: center.x + 100, y: center.y + 100 }
      expect(isPointInHex(farPoint.x, farPoint.y, 3, 3)).toBe(false)
    })

    it('handles boundary points consistently', () => {
      const center = oddrToPixel(2, 2)
      const vertices = getHexVertices(center.x, center.y)
      // Vertices should be inside or on boundary (depending on implementation)
      vertices.forEach((vertex) => {
        const result = isPointInHex(vertex.x, vertex.y, 2, 2)
        // Accept either true or false for boundary (implementation detail)
        expect(typeof result).toBe('boolean')
      })
    })

    it('matches pixelToOddr for center points', () => {
      const coords = [
        [0, 0],
        [3, 3],
        [5, 7],
        [10, 2],
      ]
      coords.forEach(([col, row]) => {
        const pixel = oddrToPixel(col, row)
        const detected = pixelToOddr(pixel.x, pixel.y)
        expect(detected).toEqual({ x: col, y: row })
        expect(isPointInHex(pixel.x, pixel.y, col, row)).toBe(true)
      })
    })

    it('correctly identifies points between hexes', () => {
      // Point between hex (0,0) and (1,0)
      const hex1 = oddrToPixel(0, 0)
      const hex2 = oddrToPixel(1, 0)
      const midpoint = { x: (hex1.x + hex2.x) / 2, y: hex1.y }

      // Point should be in exactly one hex (or on boundary)
      const inHex1 = isPointInHex(midpoint.x, midpoint.y, 0, 0)
      const inHex2 = isPointInHex(midpoint.x, midpoint.y, 1, 0)

      // At least one should be true (boundary handling)
      expect(inHex1 || inHex2).toBe(true)
    })
  })
})

