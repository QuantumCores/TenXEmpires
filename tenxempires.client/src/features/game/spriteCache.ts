import { drawHexPath, HEX_SIZE } from './hexGeometry'

/**
 * Manages offscreen canvases for sprite caching to improve rendering performance.
 */
export class SpriteCache {
  private cache = new Map<string, HTMLCanvasElement>()
  private dpr: number

  constructor(dpr: number = window.devicePixelRatio || 1) {
    this.dpr = dpr
  }

  /**
   * Gets or creates a cached sprite canvas
   */
  get(key: string, width: number, height: number, draw: (ctx: CanvasRenderingContext2D, logicalWidth: number, logicalHeight: number) => void): HTMLCanvasElement {
    const cacheKey = `${key}-${this.dpr}`

    if (this.cache.has(cacheKey)) {
      return this.cache.get(cacheKey)!
    }

    // Create offscreen canvas
    const canvas = document.createElement('canvas')
    canvas.width = width * this.dpr
    canvas.height = height * this.dpr
    
    const ctx = canvas.getContext('2d')!
    ctx.scale(this.dpr, this.dpr)
    
    // Draw sprite with logical dimensions
    draw(ctx, width, height)
    
    this.cache.set(cacheKey, canvas)
    return canvas
  }

  /**
   * Clears the entire cache
   */
  clear(): void {
    this.cache.clear()
  }

  /**
   * Updates DPR and clears cache if changed
   */
  updateDpr(newDpr: number): void {
    if (newDpr !== this.dpr) {
      this.dpr = newDpr
      this.clear()
    }
  }
}

// Global sprite cache instance
let globalCache: SpriteCache | null = null

export function getGlobalSpriteCache(): SpriteCache {
  if (!globalCache) {
    globalCache = new SpriteCache()
  }
  return globalCache
}

// ============================================================================
// Sprite Generators
// ============================================================================

/**
 * Generates a hexagonal tile sprite with terrain color
 */
export function generateTileSprite(terrain: string, hasResource: boolean): string {
  return `tile-${terrain}-${hasResource ? 'res' : 'nores'}`
}

/**
 * Draws a tile sprite
 */
export function drawTileSprite(
  ctx: CanvasRenderingContext2D,
  _terrain: string,
  hasResource: boolean,
  color: string,
  _logicalWidth?: number,
  _logicalHeight?: number
): void {
  const centerX = HEX_SIZE * 2
  const centerY = HEX_SIZE * 2

  // Draw hexagon
  drawHexPath(ctx, centerX, centerY)
  ctx.fillStyle = color
  ctx.fill()

  // Draw resource indicator
  if (hasResource) {
    ctx.fillStyle = 'rgba(255, 215, 0, 0.6)'
    ctx.beginPath()
    ctx.arc(centerX, centerY, 8, 0, Math.PI * 2)
    ctx.fill()
  }
}

/**
 * Generates a unit sprite key
 */
export function generateUnitSprite(isPlayerUnit: boolean, hasActed: boolean): string {
  return `unit-v4-${isPlayerUnit ? 'player' : 'ai'}-${hasActed ? 'acted' : 'ready'}`
}

/**
 * Draws a unit sprite
 */
export function drawUnitSprite(
  ctx: CanvasRenderingContext2D,
  isPlayerUnit: boolean,
  hasActed: boolean,
  logicalWidth?: number,
  logicalHeight?: number
): void {
  // Get dimensions - use passed logical dimensions
  const width = logicalWidth || 40
  const height = logicalHeight || 40
  
  // Center coordinates
  const centerX = width / 2
  const centerY = height / 2
  
  // Calculate radius - 80% of available space to ensure full visibility
  const radius = (Math.min(centerX, centerY) * 0.8)

  // Draw unit circle (full circle, smaller than city square)
  ctx.fillStyle = hasActed ? '#64748b' : isPlayerUnit ? '#3b82f6' : '#ef4444'
  ctx.beginPath()
  ctx.arc(centerX, centerY, radius, 0, Math.PI * 2)
  ctx.fill()

  // Draw border (thinner, inside the circle)
  ctx.strokeStyle = '#ffffff'
  ctx.lineWidth = 2
  ctx.stroke()
}

/**
 * Generates a city sprite key
 */
export function generateCitySprite(): string {
  return 'city-v4-base'
}

/**
 * Draws a city sprite
 */
export function drawCitySprite(
  ctx: CanvasRenderingContext2D,
  logicalWidth?: number,
  logicalHeight?: number
): void {
  // Get dimensions - use passed logical dimensions
  const width = logicalWidth || 40
  const height = logicalHeight || 40
  
  // Center coordinates
  const centerX = width / 2
  const centerY = height / 2
  
  // Calculate size - 80% of available space to ensure full visibility
  const size = Math.min(width, height) * 0.8

  // Draw city as a square
  ctx.fillStyle = '#fbbf24'
  ctx.fillRect(centerX - size / 2, centerY - size / 2, size, size)

  // Draw border (thinner, inside the square)
  ctx.strokeStyle = '#ffffff'
  ctx.lineWidth = 2
  ctx.strokeRect(centerX - size / 2, centerY - size / 2, size, size)
}

/**
 * Generates a hex grid sprite key
 */
export function generateGridSprite(): string {
  return 'grid-v2-hex'
}

/**
 * Draws a hex grid sprite
 */
export function drawGridSprite(
  ctx: CanvasRenderingContext2D,
  logicalWidth?: number,
  logicalHeight?: number
): void {
  // Use passed dimensions or fall back to defaults
  // The canvas is created as hexSize * 4, so center is at half that
  const centerX = (logicalWidth ?? HEX_SIZE * 4) / 2
  const centerY = (logicalHeight ?? HEX_SIZE * 4) / 2
  const hexSize = centerX / 2

  ctx.strokeStyle = 'rgba(255, 255, 255, 0.15)'
  ctx.lineWidth = 1
  drawHexPath(ctx, centerX, centerY, hexSize)
  ctx.stroke()
}

