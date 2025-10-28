import { describe, it, expect } from 'vitest'
import type { CameraState } from '../../types/game'
import { computeZoomUpdate } from './zoom'

const baseCamera: CameraState = { scale: 1, offsetX: 0, offsetY: 0 }
const viewport = { width: 1000, height: 800 }

describe('computeZoomUpdate', () => {
  it('zooms in towards pointer without invert', () => {
    const pointer = { x: 600, y: 400 }
    const res = computeZoomUpdate(baseCamera, pointer, viewport, false, -100)
    expect(res).toBeTruthy()
    expect(res!.scale).toBeGreaterThan(1)
    // Anchored: after zooming in, offset should shift so world under pointer stays under pointer
    // Verify by recomputing screen coords, within epsilon
    const eps = 0.0001
    const worldX = (pointer.x - viewport.width / 2 - baseCamera.offsetX) / baseCamera.scale
    const worldY = (pointer.y - viewport.height / 2 - baseCamera.offsetY) / baseCamera.scale
    const screenX = worldX * res!.scale + res!.offsetX + viewport.width / 2
    const screenY = worldY * res!.scale + res!.offsetY + viewport.height / 2
    expect(Math.abs(screenX - pointer.x)).toBeLessThan(eps)
    expect(Math.abs(screenY - pointer.y)).toBeLessThan(eps)
  })

  it('zooms out away from pointer without invert', () => {
    const pointer = { x: 200, y: 300 }
    const res = computeZoomUpdate(baseCamera, pointer, viewport, false, 100)
    expect(res).toBeTruthy()
    expect(res!.scale).toBeLessThan(1)
  })

  it('inverts direction when invert=true', () => {
    const pointer = { x: 500, y: 400 }
    const res1 = computeZoomUpdate(baseCamera, pointer, viewport, false, -100)
    const res2 = computeZoomUpdate(baseCamera, pointer, viewport, true, -100)
    expect(res1 && res2).toBeTruthy()
    // Without invert: deltaY negative => zoom in; with invert should be zoom out
    expect(res1!.scale).toBeGreaterThan(1)
    expect(res2!.scale).toBeLessThan(1)
  })

  it('clamps scale to bounds and returns undefined if unchanged', () => {
    const pointer = { x: 500, y: 400 }
    const camMax: CameraState = { scale: 3, offsetX: 0, offsetY: 0 }
    const camMin: CameraState = { scale: 0.5, offsetX: 0, offsetY: 0 }
    // Try to zoom in at max -> no change
    expect(computeZoomUpdate(camMax, pointer, viewport, false, -100)).toBeUndefined()
    // Try to zoom out at min -> no change
    expect(computeZoomUpdate(camMin, pointer, viewport, false, 100)).toBeUndefined()
  })
})
