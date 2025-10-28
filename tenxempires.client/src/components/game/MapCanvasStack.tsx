import { useEffect, useRef, useState, useCallback, useMemo } from 'react'
import type {
  GameStateDto,
  UnitDefinitionDto,
  MapTileDto,
  CameraState,
  SelectionState,
  GridPosition,
} from '../../types/game'
import {
  oddrToPixel,
  pixelToOddr,
  drawHexPath,
  HEX_SIZE,
} from '../../features/game/hexGeometry'
import { findPath, getReachableTiles, getAttackableTiles } from '../../features/game/pathfinding'
import { useGameMapStore } from '../../features/game/useGameMapStore'
import { useMoveUnit, useAttackUnit } from '../../features/game/useGameQueries'
import {
  getGlobalSpriteCache,
  generateTileSprite,
  drawTileSprite,
  generateUnitSprite,
  drawUnitSprite,
  generateCitySprite,
  drawCitySprite,
  generateGridSprite,
  drawGridSprite,
} from '../../features/game/spriteCache'
import { getGlobalImageLoader, DEFAULT_MANIFEST } from '../../features/game/imageLoader'

interface MapCanvasStackProps {
  gameState: GameStateDto
  unitDefs: UnitDefinitionDto[]
  mapTiles: MapTileDto[]
  camera: CameraState
  selection: SelectionState
  gridOn: boolean
}

interface PreviewState {
  kind: 'move' | 'attack' | null
  path?: GridPosition[]
  reachable?: GridPosition[]
  attackable?: GridPosition[]
  targetTile?: GridPosition
  targetUnitId?: number
}

/**
 * MapCanvasStack renders the game map using five stacked canvases with hexagonal geometry.
 */
export function MapCanvasStack({
  gameState,
  unitDefs,
  mapTiles,
  camera,
  selection,
  gridOn,
}: MapCanvasStackProps) {
  const containerRef = useRef<HTMLDivElement>(null)
  const tileCanvasRef = useRef<HTMLCanvasElement>(null)
  const gridCanvasRef = useRef<HTMLCanvasElement>(null)
  const featureCanvasRef = useRef<HTMLCanvasElement>(null)
  const unitCanvasRef = useRef<HTMLCanvasElement>(null)
  const overlayCanvasRef = useRef<HTMLCanvasElement>(null)

  const [dimensions, setDimensions] = useState({ width: 800, height: 600 })
  const [hoverTile, setHoverTile] = useState<GridPosition | null>(null)
  const [preview, setPreview] = useState<PreviewState>({ kind: null })

  const { setSelection, clearSelection } = useGameMapStore()
  const moveUnitMutation = useMoveUnit(gameState.game.id)
  const attackUnitMutation = useAttackUnit(gameState.game.id)

  // Sprite cache for performance
  const spriteCache = useMemo(() => getGlobalSpriteCache(), [])
  
  // Image loader for PNG assets
  const imageLoader = useMemo(() => getGlobalImageLoader(), [])
  const [imagesLoaded, setImagesLoaded] = useState(false)

  // Preload images on mount
  useEffect(() => {
    imageLoader.preloadManifest(DEFAULT_MANIFEST).then(() => {
      setImagesLoaded(true)
    })
  }, [imageLoader])

  // Handle container resize
  useEffect(() => {
    const container = containerRef.current
    if (!container) return

    const observer = new ResizeObserver((entries) => {
      for (const entry of entries) {
        const { width, height } = entry.contentRect
        setDimensions({ width, height })
      }
    })

    observer.observe(container)
    return () => observer.disconnect()
  }, [])

  // Setup canvases with DPR sizing
  useEffect(() => {
    const canvases = [
      tileCanvasRef.current,
      gridCanvasRef.current,
      featureCanvasRef.current,
      unitCanvasRef.current,
      overlayCanvasRef.current,
    ]

    const dpr = window.devicePixelRatio || 1

    // Update sprite cache DPR
    spriteCache.updateDpr(dpr)

    canvases.forEach((canvas) => {
      if (!canvas) return

      canvas.style.width = `${dimensions.width}px`
      canvas.style.height = `${dimensions.height}px`
      canvas.width = dimensions.width * dpr
      canvas.height = dimensions.height * dpr

      const ctx = canvas.getContext('2d')
      if (ctx) {
        ctx.scale(dpr, dpr)
        // Enable image smoothing for better quality
        ctx.imageSmoothingEnabled = true
        ctx.imageSmoothingQuality = 'high'
      }
    })
  }, [dimensions, spriteCache])

  // Update preview when selection changes
  useEffect(() => {
    if (!selection.kind || !selection.id) {
      setPreview({ kind: null })
      return
    }

    if (selection.kind === 'unit') {
      const unit = gameState.units.find((u) => u.id === selection.id)
      if (!unit || unit.hasActed) {
        setPreview({ kind: null })
        return
      }

      const unitDef = unitDefs.find((d) => d.code === unit.typeCode)
      if (!unitDef) {
        setPreview({ kind: null })
        return
      }

      // Calculate reachable tiles for movement
      const reachable = getReachableTiles(
        { row: unit.row, col: unit.col },
        unitDef.movePoints,
        gameState.map.width,
        gameState.map.height,
        (pos) => gameState.units.some((u) => u.row === pos.row && u.col === pos.col)
      )

      // Calculate attackable tiles
      const attackable = unitDef.isRanged
        ? getAttackableTiles(
            { row: unit.row, col: unit.col },
            unitDef.rangeMin,
            unitDef.rangeMax,
            gameState.map.width,
            gameState.map.height
          )
        : []

      setPreview({ kind: null, reachable, attackable })
    } else {
      setPreview({ kind: null })
    }
  }, [selection, gameState, unitDefs])

  // Render tiles layer
  useEffect(() => {
    const canvas = tileCanvasRef.current
    if (!canvas) return
    const ctx = canvas.getContext('2d')
    if (!ctx) return
    renderTiles(ctx, mapTiles, camera, dimensions, spriteCache, imageLoader)
  }, [mapTiles, camera, dimensions, spriteCache, imageLoader, imagesLoaded])

  // Render grid layer
  useEffect(() => {
    const canvas = gridCanvasRef.current
    if (!canvas) return
    const ctx = canvas.getContext('2d')
    if (!ctx) return
    if (gridOn) {
      renderGrid(ctx, gameState.map, camera, dimensions, spriteCache)
    } else {
      ctx.clearRect(0, 0, dimensions.width, dimensions.height)
    }
  }, [gameState.map, camera, dimensions, gridOn, spriteCache])

  // Render features layer
  useEffect(() => {
    const canvas = featureCanvasRef.current
    if (!canvas) return
    const ctx = canvas.getContext('2d')
    if (!ctx) return
    renderFeatures(ctx, gameState, camera, dimensions, spriteCache, imageLoader)
  }, [gameState, camera, dimensions, spriteCache, imageLoader, imagesLoaded])

  // Render units layer
  useEffect(() => {
    const canvas = unitCanvasRef.current
    if (!canvas) return
    const ctx = canvas.getContext('2d')
    if (!ctx) return
    renderUnits(ctx, gameState.units, gameState.participants, unitDefs, camera, dimensions, spriteCache)
  }, [gameState.units, gameState.participants, unitDefs, camera, dimensions, spriteCache])

  // Render overlay layer
  useEffect(() => {
    const canvas = overlayCanvasRef.current
    if (!canvas) return
    const ctx = canvas.getContext('2d')
    if (!ctx) return
    renderOverlay(ctx, selection, gameState, preview, hoverTile, camera, dimensions)
  }, [selection, gameState, preview, hoverTile, camera, dimensions])

  // Handle pointer move
  const handlePointerMove = useCallback(
    (e: React.PointerEvent) => {
      const rect = containerRef.current?.getBoundingClientRect()
      if (!rect) return

      const x = (e.clientX - rect.left - dimensions.width / 2 - camera.offsetX) / camera.scale
      const y = (e.clientY - rect.top - dimensions.height / 2 - camera.offsetY) / camera.scale

      const tile = pixelToOddr(x, y)
      setHoverTile({ row: tile.y, col: tile.x })
    },
    [camera, dimensions]
  )

  // Handle click
  const handleClick = useCallback(
    (e: React.MouseEvent<HTMLDivElement>) => {
      if (e.button !== 0) return // Left click only

      const rect = containerRef.current?.getBoundingClientRect()
      if (!rect) return

      const x = (e.clientX - rect.left - dimensions.width / 2 - camera.offsetX) / camera.scale
      const y = (e.clientY - rect.top - dimensions.height / 2 - camera.offsetY) / camera.scale

      const clickedTile = pixelToOddr(x, y)
      const clickedPos: GridPosition = { row: clickedTile.y, col: clickedTile.x }

      // Picking priority: Unit > City > Tile
      const clickedUnit = gameState.units.find((u) => u.row === clickedPos.row && u.col === clickedPos.col)
      const clickedCity = gameState.cities.find((c) => c.row === clickedPos.row && c.col === clickedPos.col)

      // If clicking on a unit
      if (clickedUnit) {
        // If same unit, deselect
        if (selection.kind === 'unit' && selection.id === clickedUnit.id) {
          clearSelection()
          return
        }

        // If different unit and it's an attack preview, execute attack
        if (selection.kind === 'unit' && preview.kind === 'attack' && preview.targetUnitId === clickedUnit.id) {
          attackUnitMutation.mutate({
            attackerUnitId: selection.id!,
            targetUnitId: clickedUnit.id,
          })
          clearSelection()
          return
        }

        // Otherwise select the clicked unit
        setSelection({ kind: 'unit', id: clickedUnit.id })
        return
      }

      // If clicking on a city
      if (clickedCity) {
        setSelection({ kind: 'city', id: clickedCity.id })
        return
      }

      // If clicking on empty tile with unit selected
      if (selection.kind === 'unit') {
        const selectedUnit = gameState.units.find((u) => u.id === selection.id)
        if (!selectedUnit || selectedUnit.hasActed) {
          clearSelection()
          return
        }

        const unitDef = unitDefs.find((d) => d.code === selectedUnit.typeCode)
        if (!unitDef) {
          clearSelection()
          return
        }

        // Check if clicking within movement range
        const isReachable = preview.reachable?.some((t) => t.row === clickedPos.row && t.col === clickedPos.col)

        if (isReachable) {
          // Calculate path
          const path = findPath(
            { row: selectedUnit.row, col: selectedUnit.col },
            clickedPos,
            unitDef.movePoints,
            gameState.map.width,
            gameState.map.height,
            (pos) => gameState.units.some((u) => u.row === pos.row && u.col === pos.col && u.id !== selectedUnit.id)
          )

          if (path) {
            // Second click on same tile: commit move
            if (preview.kind === 'move' && preview.targetTile?.row === clickedPos.row && preview.targetTile?.col === clickedPos.col) {
              moveUnitMutation.mutate({
                unitId: selectedUnit.id,
                to: clickedPos,
              })
              clearSelection()
            } else {
              // First click: show preview
              setPreview({ ...preview, kind: 'move', path, targetTile: clickedPos })
            }
            return
          }
        }

        // Check if clicking within attack range (for ranged units)
        if (unitDef.isRanged) {
          const isAttackable = preview.attackable?.some((t) => t.row === clickedPos.row && t.col === clickedPos.col)
          const targetUnit = gameState.units.find((u) => u.row === clickedPos.row && u.col === clickedPos.col)

          if (isAttackable && targetUnit && targetUnit.participantId !== selectedUnit.participantId) {
            // Second click: commit attack
            if (preview.kind === 'attack' && preview.targetUnitId === targetUnit.id) {
              attackUnitMutation.mutate({
                attackerUnitId: selectedUnit.id,
                targetUnitId: targetUnit.id,
              })
              clearSelection()
            } else {
              // First click: show preview
              setPreview({ ...preview, kind: 'attack', targetTile: clickedPos, targetUnitId: targetUnit.id })
            }
            return
          }
        }
      }

      // Otherwise clear selection
      clearSelection()
    },
    [gameState, unitDefs, selection, preview, camera, dimensions, setSelection, clearSelection, moveUnitMutation, attackUnitMutation]
  )

  // Handle right-click to cancel
  const handleContextMenu = useCallback(
    (e: React.MouseEvent<HTMLDivElement>) => {
      e.preventDefault()
      clearSelection()
      setPreview({ kind: null })
    },
    [clearSelection]
  )

  return (
    <div
      ref={containerRef}
      className="relative h-full w-full bg-slate-900"
      onPointerMove={handlePointerMove}
      onClick={handleClick}
      onContextMenu={handleContextMenu}
    >
      <canvas ref={tileCanvasRef} className="absolute inset-0" />
      <canvas ref={gridCanvasRef} className="absolute inset-0" />
      <canvas ref={featureCanvasRef} className="absolute inset-0" />
      <canvas ref={unitCanvasRef} className="absolute inset-0" />
      <canvas ref={overlayCanvasRef} className="absolute inset-0" />
    </div>
  )
}

// ============================================================================
// Rendering Functions
// ============================================================================

function toScreenCoords(
  worldX: number,
  worldY: number,
  camera: CameraState,
  viewport: { width: number; height: number }
): { x: number; y: number } {
  return {
    x: worldX * camera.scale + camera.offsetX + viewport.width / 2,
    y: worldY * camera.scale + camera.offsetY + viewport.height / 2,
  }
}

function renderTiles(
  ctx: CanvasRenderingContext2D,
  tiles: MapTileDto[],
  camera: CameraState,
  viewport: { width: number; height: number },
  spriteCache: ReturnType<typeof getGlobalSpriteCache>,
  imageLoader: ReturnType<typeof getGlobalImageLoader>
) {
  ctx.clearRect(0, 0, viewport.width, viewport.height)

  tiles.forEach((tile) => {
    const pos = oddrToPixel(tile.col, tile.row)
    const screen = toScreenCoords(pos.x, pos.y, camera, viewport)

    ctx.save()
    ctx.translate(screen.x, screen.y)
    ctx.scale(camera.scale, camera.scale)

    // Try to use PNG image first
    const terrainImage = imageLoader.getImage('terrain', tile.terrain.toLowerCase())
    
    if (terrainImage) {
      // Draw PNG image
      // Assuming PNG is sized to fit hex (adjust offsets as needed based on your image dimensions)
      const imageSize = HEX_SIZE * 2
      ctx.drawImage(terrainImage, -imageSize, -imageSize, imageSize * 2, imageSize * 2)
    } else {
      // Fallback to drawn sprite
      const spriteKey = generateTileSprite(tile.terrain, !!tile.resourceType)
      const sprite = spriteCache.get(
        spriteKey,
        HEX_SIZE * 4,
        HEX_SIZE * 4,
        (spriteCtx) => drawTileSprite(spriteCtx, tile.terrain, !!tile.resourceType, getTerrainColor(tile.terrain))
      )
      ctx.drawImage(sprite, -HEX_SIZE * 2, -HEX_SIZE * 2)
    }

    // Draw resource indicator if present (always drawn on top)
    if (tile.resourceType) {
      ctx.fillStyle = 'rgba(255, 215, 0, 0.6)'
      ctx.beginPath()
      ctx.arc(0, 0, 8, 0, Math.PI * 2)
      ctx.fill()
    }

    ctx.restore()
  })
}

function renderGrid(
  ctx: CanvasRenderingContext2D,
  map: { width: number; height: number },
  camera: CameraState,
  viewport: { width: number; height: number },
  spriteCache: ReturnType<typeof getGlobalSpriteCache>
) {
  ctx.clearRect(0, 0, viewport.width, viewport.height)

  // Get cached grid sprite
  const gridSprite = spriteCache.get(
    generateGridSprite(),
    HEX_SIZE * 4,
    HEX_SIZE * 4,
    (spriteCtx) => drawGridSprite(spriteCtx)
  )

  for (let row = 0; row < map.height; row++) {
    for (let col = 0; col < map.width; col++) {
      const pos = oddrToPixel(col, row)
      const screen = toScreenCoords(pos.x, pos.y, camera, viewport)

      ctx.save()
      ctx.translate(screen.x, screen.y)
      ctx.scale(camera.scale, camera.scale)
      ctx.drawImage(gridSprite, -HEX_SIZE * 2, -HEX_SIZE * 2)
      ctx.restore()
    }
  }
}

function renderFeatures(
  ctx: CanvasRenderingContext2D,
  gameState: GameStateDto,
  camera: CameraState,
  viewport: { width: number; height: number },
  spriteCache: ReturnType<typeof getGlobalSpriteCache>,
  imageLoader: ReturnType<typeof getGlobalImageLoader>
) {
  ctx.clearRect(0, 0, viewport.width, viewport.height)

  gameState.cities.forEach((city) => {
    const pos = oddrToPixel(city.col, city.row)
    const screen = toScreenCoords(pos.x, pos.y, camera, viewport)

    ctx.save()
    ctx.translate(screen.x, screen.y)
    ctx.scale(camera.scale, camera.scale)

    // Try to use PNG image for city
    const cityImage = imageLoader.getImage('city', 'city')
    
    if (cityImage) {
      // Draw PNG image
      const size = 40
      ctx.drawImage(cityImage, -size / 2, -size / 2, size, size)
    } else {
      // Fallback to drawn sprite
      const citySprite = spriteCache.get(generateCitySprite(), 40, 40, (spriteCtx) => drawCitySprite(spriteCtx))
      ctx.drawImage(citySprite, -20, -20)
    }

    // HP bar (always drawn on top)
    const hpPercent = city.hp / city.maxHp
    ctx.fillStyle = hpPercent > 0.5 ? '#22c55e' : hpPercent > 0.25 ? '#f59e0b' : '#ef4444'
    ctx.fillRect(-16, -22, 32 * hpPercent, 4)

    ctx.restore()
  })
}

function renderUnits(
  ctx: CanvasRenderingContext2D,
  units: GameStateDto['units'],
  participants: GameStateDto['participants'],
  _unitDefs: UnitDefinitionDto[],
  camera: CameraState,
  viewport: { width: number; height: number },
  spriteCache: ReturnType<typeof getGlobalSpriteCache>
) {
  ctx.clearRect(0, 0, viewport.width, viewport.height)

  units.forEach((unit) => {
    const pos = oddrToPixel(unit.col, unit.row)
    const screen = toScreenCoords(pos.x, pos.y, camera, viewport)

    const participant = participants.find((p) => p.id === unit.participantId)
    const isPlayerUnit = participant?.kind === 'human'

    // Get cached unit sprite
    const spriteKey = generateUnitSprite(isPlayerUnit, unit.hasActed)
    const sprite = spriteCache.get(spriteKey, 40, 40, (spriteCtx) => drawUnitSprite(spriteCtx, isPlayerUnit, unit.hasActed))

    ctx.save()
    ctx.translate(screen.x, screen.y)
    ctx.scale(camera.scale, camera.scale)

    // Draw unit sprite
    ctx.drawImage(sprite, -20, -20)

    // Draw unit type
    ctx.fillStyle = '#ffffff'
    ctx.font = 'bold 10px sans-serif'
    ctx.textAlign = 'center'
    ctx.textBaseline = 'middle'
    ctx.fillText(unit.typeCode.slice(0, 2).toUpperCase(), 0, 0)

    ctx.restore()
  })
}

function renderOverlay(
  ctx: CanvasRenderingContext2D,
  selection: SelectionState,
  gameState: GameStateDto,
  preview: PreviewState,
  _hoverTile: GridPosition | null,
  camera: CameraState,
  viewport: { width: number; height: number }
) {
  ctx.clearRect(0, 0, viewport.width, viewport.height)

  // Draw reachable tiles
  if (preview.reachable) {
    preview.reachable.forEach((tile) => {
      const pos = oddrToPixel(tile.col, tile.row)
      const screen = toScreenCoords(pos.x, pos.y, camera, viewport)

      ctx.save()
      ctx.translate(screen.x, screen.y)
      ctx.scale(camera.scale, camera.scale)

      drawHexPath(ctx, 0, 0)
      ctx.fillStyle = 'rgba(59, 130, 246, 0.2)'
      ctx.fill()

      ctx.restore()
    })
  }

  // Draw attackable tiles
  if (preview.attackable) {
    preview.attackable.forEach((tile) => {
      const pos = oddrToPixel(tile.col, tile.row)
      const screen = toScreenCoords(pos.x, pos.y, camera, viewport)

      ctx.save()
      ctx.translate(screen.x, screen.y)
      ctx.scale(camera.scale, camera.scale)

      drawHexPath(ctx, 0, 0)
      ctx.fillStyle = 'rgba(239, 68, 68, 0.15)'
      ctx.fill()

      ctx.restore()
    })
  }

  // Draw path preview
  if (preview.kind === 'move' && preview.path) {
    ctx.save()
    ctx.strokeStyle = '#fbbf24'
    ctx.lineWidth = 3 * camera.scale
    ctx.setLineDash([5 * camera.scale, 3 * camera.scale])

    ctx.beginPath()
    preview.path.forEach((tile, i) => {
      const pos = oddrToPixel(tile.col, tile.row)
      const screen = toScreenCoords(pos.x, pos.y, camera, viewport)
      if (i === 0) {
        ctx.moveTo(screen.x, screen.y)
      } else {
        ctx.lineTo(screen.x, screen.y)
      }
    })
    ctx.stroke()
    ctx.restore()
  }

  // Highlight selected hex
  if (selection.kind && selection.id) {
    let targetRow: number | undefined
    let targetCol: number | undefined

    if (selection.kind === 'unit') {
      const unit = gameState.units.find((u) => u.id === selection.id)
      if (unit) {
        targetRow = unit.row
        targetCol = unit.col
      }
    } else if (selection.kind === 'city') {
      const city = gameState.cities.find((c) => c.id === selection.id)
      if (city) {
        targetRow = city.row
        targetCol = city.col
      }
    }

    if (targetRow !== undefined && targetCol !== undefined) {
      const pos = oddrToPixel(targetCol, targetRow)
      const screen = toScreenCoords(pos.x, pos.y, camera, viewport)

      ctx.save()
      ctx.translate(screen.x, screen.y)
      ctx.scale(camera.scale, camera.scale)

      drawHexPath(ctx, 0, 0)
      ctx.strokeStyle = '#fbbf24'
      ctx.lineWidth = 3
      ctx.stroke()

      ctx.restore()
    }
  }

  // Highlight target tile
  if (preview.targetTile) {
    const pos = oddrToPixel(preview.targetTile.col, preview.targetTile.row)
    const screen = toScreenCoords(pos.x, pos.y, camera, viewport)

    ctx.save()
    ctx.translate(screen.x, screen.y)
    ctx.scale(camera.scale, camera.scale)

    drawHexPath(ctx, 0, 0)
    ctx.strokeStyle = preview.kind === 'attack' ? '#ef4444' : '#22c55e'
    ctx.lineWidth = 2
    ctx.stroke()

    ctx.restore()
  }
}

function getTerrainColor(terrain: string): string {
  const colors: Record<string, string> = {
    plains: '#9ca38f',
    grassland: '#7fb069',
    desert: '#e8c468',
    tundra: '#d4e4f7',
    ocean: '#4a90e2',
    coast: '#6db3f2',
    mountain: '#8b7355',
    hill: '#a8926f',
  }
  return colors[terrain.toLowerCase()] || '#666666'
}
