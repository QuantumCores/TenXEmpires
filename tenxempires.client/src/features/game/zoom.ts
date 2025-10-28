import type { CameraState } from '../../types/game'

export interface ZoomUpdate {
  scale: number
  offsetX: number
  offsetY: number
}

/**
 * Computes a pointer-anchored zoom update for the map camera.
 * Returns new camera fields or undefined if no change required.
 */
export function computeZoomUpdate(
  camera: CameraState,
  pointer: { x: number; y: number },
  viewport: { width: number; height: number },
  invert: boolean,
  deltaY: number,
  options?: { minScale?: number; maxScale?: number; step?: number }
): ZoomUpdate | undefined {
  const { minScale = 0.5, maxScale = 3, step = 1.1 } = options || {}

  const dir = deltaY > 0 ? 1 : -1
  const sign = invert ? -dir : dir
  const factor = sign < 0 ? step : 1 / step

  const newScale = clamp(camera.scale * factor, minScale, maxScale)
  if (newScale === camera.scale) return undefined

  const worldX = (pointer.x - viewport.width / 2 - camera.offsetX) / camera.scale
  const worldY = (pointer.y - viewport.height / 2 - camera.offsetY) / camera.scale

  const offsetX = pointer.x - worldX * newScale - viewport.width / 2
  const offsetY = pointer.y - worldY * newScale - viewport.height / 2

  return { scale: newScale, offsetX, offsetY }
}

function clamp(v: number, min: number, max: number) {
  return Math.max(min, Math.min(max, v))
}

