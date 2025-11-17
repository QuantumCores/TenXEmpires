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
  calculateOptimalHexSize,
  hexDistance,
} from '../../features/game/hexGeometry'
import { findPath, getReachableTiles, getAttackableTiles } from '../../features/game/pathfinding'
import { useGameMapStore } from '../../features/game/useGameMapStore'
import { computeZoomUpdate } from '../../features/game/zoom'
import { useMoveUnit, useAttackUnit, useAttackCity } from '../../features/game/useGameQueries'
import {
  getGlobalSpriteCache,
  generateTileSprite,
  drawTileSprite,
  generateUnitSprite,
  drawUnitSprite,
  generateCitySprite,
  drawCitySprite,
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
  targetCityId?: number
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
  const tileLookup = useMemo(() => {
    const lookup = new Map<number, MapTileDto>()
    mapTiles.forEach((tile) => lookup.set(tile.id, tile))
    return lookup
  }, [mapTiles])

  // Calculate optimal hex size based on viewport and map dimensions
  const hexMetrics = useMemo(() => {
    return calculateOptimalHexSize(
      gameState.map.width,
      gameState.map.height,
      dimensions.width,
      dimensions.height,
      0.05 // 5% padding
    )
  }, [dimensions.width, dimensions.height, gameState.map.width, gameState.map.height])

  const { setSelection, clearSelection, setCamera } = useGameMapStore()
  const debug = useGameMapStore((s) => s.debug)
  const invertScrollZoom = useGameMapStore((s) => s.invertZoom)
  const moveUnitMutation = useMoveUnit(gameState.game.id)
  const attackUnitMutation = useAttackUnit(gameState.game.id)
  const attackCityMutation = useAttackCity(gameState.game.id)

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

    // Set initial dimensions immediately
    const rect = container.getBoundingClientRect()
    if (rect.width > 0 && rect.height > 0) {
      setDimensions({ width: rect.width, height: rect.height })
    }

    const observer = new ResizeObserver((entries) => {
      for (const entry of entries) {
        const { width, height } = entry.contentRect
        if (width > 0 && height > 0) {
          setDimensions({ width, height })
        }
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

  // Center camera on map when first loaded or when hex size changes
  useEffect(() => {
    // Calculate map center in world coordinates using current hex metrics
    const mapCenterRow = gameState.map.height / 2
    const mapCenterCol = gameState.map.width / 2
    const mapCenter = oddrToPixel(mapCenterCol, mapCenterRow, hexMetrics.hexWidth, hexMetrics.hexVertSpacing)
    
    // Set camera to center the map, shifted down by half a hex height to account for TopBar
    // Negative offset moves the world in that direction, centering it
    setCamera({
      offsetX: -mapCenter.x,
      offsetY: -mapCenter.y + hexMetrics.hexHeight * 0.5,
      scale: 1,
    })
  }, [gameState.map.width, gameState.map.height, setCamera, hexMetrics])

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
      const allReachable = getReachableTiles(
        { row: unit.row, col: unit.col },
        unitDef.movePoints,
        gameState.map.width,
        gameState.map.height,
        (pos) => {
          // Block if occupied by another unit
          if (gameState.units.some((u) => u.row === pos.row && u.col === pos.col)) {
            return true
          }
          // Block if tile is water or ocean
          const tile = mapTiles.find((t) => t.row === pos.row && t.col === pos.col)
          if (tile) {
            if (tile.terrain === 'water' || tile.terrain === 'ocean') {
              return true
            }
          }
          return false
        }
      )
      
      // Filter out any water/ocean tiles that might have slipped through (defensive check)
      const reachable = allReachable.filter((pos) => {
        const tile = mapTiles.find((t) => t.row === pos.row && t.col === pos.col)
        if (tile && (tile.terrain === 'water' || tile.terrain === 'ocean')) {
          return false
        }
        return true
      })

      // Calculate attackable tiles
      // Melee units can attack adjacent tiles (distance 1), ranged units use their range
      // Filter out water/ocean tiles but keep tiles with enemy units
      const allAttackableTiles = unitDef.isRanged
        ? getAttackableTiles(
            { row: unit.row, col: unit.col },
            unitDef.rangeMin,
            unitDef.rangeMax,
            gameState.map.width,
            gameState.map.height
          )
        : getAttackableTiles(
            { row: unit.row, col: unit.col },
            1,
            1,
            gameState.map.width,
            gameState.map.height
          )
      
      // Filter attackable tiles: exclude water/ocean, but include tiles with enemy units or cities
      const attackable = allAttackableTiles.filter((tile) => {
        // Always include tiles with enemy units (even if on water - though that shouldn't happen)
        const enemyUnit = gameState.units.find(
          (u) => u.row === tile.row && u.col === tile.col && u.participantId !== unit.participantId
        )
        if (enemyUnit) return true
        
        // Always include tiles with enemy cities
        const enemyCity = gameState.cities.find(
          (c) => c.row === tile.row && c.col === tile.col && c.participantId !== unit.participantId
        )
        if (enemyCity) return true
        
        // Exclude water/ocean tiles
        const mapTile = mapTiles.find((t) => t.row === tile.row && t.col === tile.col)
        if (mapTile && (mapTile.terrain === 'water' || mapTile.terrain === 'ocean')) {
          return false
        }
        
        return true
      })

      setPreview({ kind: null, reachable, attackable })
    } else {
      setPreview({ kind: null })
    }
  }, [selection, gameState, unitDefs, mapTiles])

  // Render tiles layer
  useEffect(() => {
    const canvas = tileCanvasRef.current
    if (!canvas) return
    const ctx = canvas.getContext('2d')
    if (!ctx) return
    renderTiles(ctx, mapTiles, camera, dimensions, spriteCache, imageLoader, hexMetrics)
  }, [mapTiles, camera, dimensions, spriteCache, imageLoader, imagesLoaded, hexMetrics])

  // Render grid layer
    useEffect(() => {
      const canvas = gridCanvasRef.current
      if (!canvas) return
      const ctx = canvas.getContext('2d')
      if (!ctx) return
      if (gridOn) {
        renderGrid(ctx, gameState.map, camera, dimensions, hexMetrics)
      } else {
        ctx.clearRect(0, 0, dimensions.width, dimensions.height)
      }
    }, [gameState.map, camera, dimensions, gridOn, hexMetrics])

  // Render features layer
  useEffect(() => {
    const canvas = featureCanvasRef.current
    if (!canvas) return
    const ctx = canvas.getContext('2d')
    if (!ctx) return
    renderFeatures(ctx, gameState, camera, dimensions, spriteCache, imageLoader, hexMetrics)
  }, [gameState, camera, dimensions, spriteCache, imageLoader, imagesLoaded, hexMetrics])

  // Render units layer
  useEffect(() => {
    const canvas = unitCanvasRef.current
    if (!canvas) return
    const ctx = canvas.getContext('2d')
    if (!ctx) return
    renderUnits(ctx, gameState.units, gameState.participants, unitDefs, camera, dimensions, spriteCache, imageLoader, imagesLoaded, hexMetrics)
  }, [gameState.units, gameState.participants, unitDefs, camera, dimensions, spriteCache, imageLoader, imagesLoaded, hexMetrics])

  // Render overlay layer
  useEffect(() => {
    const canvas = overlayCanvasRef.current
    if (!canvas) return
    const ctx = canvas.getContext('2d')
    if (!ctx) return
    renderOverlay(
      ctx,
      selection,
      gameState,
      mapTiles,
      tileLookup,
      preview,
      hoverTile,
      camera,
      dimensions,
      debug,
      hexMetrics,
      imageLoader
    )
  }, [selection, gameState, mapTiles, tileLookup, preview, hoverTile, camera, dimensions, debug, hexMetrics, imageLoader, imagesLoaded])

  // Handle pointer move
  const handlePointerMove = useCallback(
    (e: React.PointerEvent) => {
      const rect = containerRef.current?.getBoundingClientRect()
      if (!rect) return

      const x = (e.clientX - rect.left - dimensions.width / 2 - camera.offsetX) / camera.scale
      const y = (e.clientY - rect.top - dimensions.height / 2 - camera.offsetY) / camera.scale

      const tile = pixelToOddr(x, y, hexMetrics.hexWidth, hexMetrics.hexVertSpacing)
      setHoverTile({ row: tile.y, col: tile.x })
    },
    [camera, dimensions, hexMetrics]
  )

  // Handle click
  const handleClick = useCallback(
    (e: React.MouseEvent<HTMLDivElement>) => {
      if (e.button !== 0) return // Left click only

      const rect = containerRef.current?.getBoundingClientRect()
      if (!rect) return

      const x = (e.clientX - rect.left - dimensions.width / 2 - camera.offsetX) / camera.scale
      const y = (e.clientY - rect.top - dimensions.height / 2 - camera.offsetY) / camera.scale

      const clickedTile = pixelToOddr(x, y, hexMetrics.hexWidth, hexMetrics.hexVertSpacing)
      const clickedPos: GridPosition = { row: clickedTile.y, col: clickedTile.x }

      // Picking priority: Unit > City > Tile
      const clickedUnit = gameState.units.find((u) => u.row === clickedPos.row && u.col === clickedPos.col)
      const clickedCity = gameState.cities.find((c) => c.row === clickedPos.row && c.col === clickedPos.col)
      
      // Get player participant
      const playerParticipant = gameState.participants.find((p) => p.kind === 'human')

      // If clicking on a unit
      if (clickedUnit) {
        // Check if unit belongs to player
        const isPlayerUnit = clickedUnit.participantId === playerParticipant?.id
        
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

        // If clicking on enemy unit and we have a unit selected, check if it's attackable
        if (selection.kind === 'unit' && !isPlayerUnit && clickedUnit.participantId !== playerParticipant?.id) {
          const selectedUnit = gameState.units.find((u) => u.id === selection.id)
          if (selectedUnit && !selectedUnit.hasActed) {
            const unitDef = unitDefs.find((d) => d.code === selectedUnit.typeCode)
            if (unitDef) {
              const isAttackable = preview.attackable?.some((t) => t.row === clickedPos.row && t.col === clickedPos.col)
              if (isAttackable) {
                // Second click: commit attack
                if (preview.kind === 'attack' && preview.targetUnitId === clickedUnit.id) {
                  attackUnitMutation.mutate({
                    attackerUnitId: selectedUnit.id,
                    targetUnitId: clickedUnit.id,
                  })
                  clearSelection()
                } else {
                  // First click: show preview
                  setPreview({ ...preview, kind: 'attack', targetTile: clickedPos, targetUnitId: clickedUnit.id })
                }
                return
              }
            }
          }
        }

        // Only allow selecting player's own units
        if (isPlayerUnit) {
          setSelection({ kind: 'unit', id: clickedUnit.id })
        }
        return
      }

      // If clicking on a city
      if (clickedCity) {
        // Check if city belongs to player
        const isPlayerCity = clickedCity.participantId === playerParticipant?.id
        
        // If clicking on enemy city and we have a unit selected, check if it's attackable
        if (selection.kind === 'unit' && !isPlayerCity) {
          const selectedUnit = gameState.units.find((u) => u.id === selection.id)
          if (selectedUnit && !selectedUnit.hasActed) {
            const unitDef = unitDefs.find((d) => d.code === selectedUnit.typeCode)
            if (unitDef) {
              // Check if this tile is in the attackable list (already filtered by range)
              const isAttackable = preview.attackable?.some((t) => t.row === clickedPos.row && t.col === clickedPos.col)
              
              // Also check range directly as fallback (in case city tile isn't in attackable list)
              const distance = hexDistance(selectedUnit.col, selectedUnit.row, clickedCity.col, clickedCity.row)
              const isInRange = unitDef.isRanged
                ? distance >= unitDef.rangeMin && distance <= unitDef.rangeMax
                : distance === 1
              
              if (isAttackable || isInRange) {
                // Second click: commit attack
                if (preview.kind === 'attack' && preview.targetCityId === clickedCity.id) {
                  attackCityMutation.mutate({
                    attackerUnitId: selectedUnit.id,
                    targetCityId: clickedCity.id,
                  })
                  clearSelection()
                } else {
                  // First click: show preview
                  setPreview({ ...preview, kind: 'attack', targetTile: clickedPos, targetCityId: clickedCity.id })
                }
                return
              }
            }
          }
        }
        
        // Only allow selecting player's own cities
        if (isPlayerCity) {
          setSelection({ kind: 'city', id: clickedCity.id })
        }
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

        // Check for attackable targets FIRST (attacks take priority over movement)
        const isAttackable = preview.attackable?.some((t) => t.row === clickedPos.row && t.col === clickedPos.col)
        const targetUnit = gameState.units.find((u) => u.row === clickedPos.row && u.col === clickedPos.col)
        const targetCity = gameState.cities.find((c) => c.row === clickedPos.row && c.col === clickedPos.col)

        // Handle unit attacks
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

        // Handle city attacks
        if (isAttackable && targetCity && targetCity.participantId !== selectedUnit.participantId) {
          // Calculate hex distance between unit and city
          const distance = hexDistance(selectedUnit.col, selectedUnit.row, targetCity.col, targetCity.row)
          
          // Check if city is in attack range
          const isInRange = unitDef.isRanged
            ? distance >= unitDef.rangeMin && distance <= unitDef.rangeMax
            : distance === 1

          if (isInRange) {
            // Second click: commit attack (when API is ready)
            if (preview.kind === 'attack' && preview.targetCityId === targetCity.id) {
              // TODO: Implement city attack mutation when API endpoint is available
              // attackCityMutation.mutate({
              //   attackerUnitId: selectedUnit.id,
              //   targetCityId: targetCity.id,
              // })
              // clearSelection()
              // For now, just show preview
              setPreview({ ...preview, kind: 'attack', targetTile: clickedPos, targetCityId: targetCity.id })
            } else {
              // First click: show preview
              setPreview({ ...preview, kind: 'attack', targetTile: clickedPos, targetCityId: targetCity.id })
            }
            return
          }
        }

        // Check if clicking within movement range (only if not attacking)
        const isReachable = preview.reachable?.some((t) => t.row === clickedPos.row && t.col === clickedPos.col)

        if (isReachable) {
          // Don't allow movement to tiles with enemy cities (they should be attacked instead)
          const enemyCityOnTile = gameState.cities.find(
            (c) => c.row === clickedPos.row && c.col === clickedPos.col && c.participantId !== selectedUnit.participantId
          )
          if (enemyCityOnTile) {
            // Already handled above as attack target
            return
          }

          // Calculate path
          const path = findPath(
            { row: selectedUnit.row, col: selectedUnit.col },
            clickedPos,
            unitDef.movePoints,
            gameState.map.width,
            gameState.map.height,
            (pos) => {
              // Block if occupied by another unit
              if (gameState.units.some((u) => u.row === pos.row && u.col === pos.col && u.id !== selectedUnit.id)) {
                return true
              }
              // Block if tile is water or ocean
              const tile = mapTiles.find((t) => t.row === pos.row && t.col === pos.col)
              if (tile && (tile.terrain === 'water' || tile.terrain === 'ocean')) {
                return true
              }
              return false
            }
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
      }

      // Otherwise clear selection
      clearSelection()
    },
    [gameState, unitDefs, selection, preview, camera, dimensions, setSelection, clearSelection, moveUnitMutation, attackUnitMutation, attackCityMutation, hexMetrics, mapTiles]
  )

  // Handle right-click to cancel
  const handleContextMenu = useCallback(
    (e: React.MouseEvent<HTMLDivElement>) => {
      e.preventDefault()
      clearSelection()
      setPreview({ kind: null })
    },
    [clearSelection, setPreview]
  )

  return (
    <div
      ref={containerRef}
      className="relative h-full w-full bg-slate-900"
      onPointerMove={handlePointerMove}
      onClick={handleClick}
      onContextMenu={handleContextMenu}
      onWheel={(e) => {
        const container = containerRef.current
        if (!container) return
        e.preventDefault()

        const rect = container.getBoundingClientRect()
        const sX = e.clientX - rect.left
        const sY = e.clientY - rect.top

        const result = computeZoomUpdate(
          camera,
          { x: sX, y: sY },
          { width: dimensions.width, height: dimensions.height },
          invertScrollZoom,
          e.deltaY
        )
        if (!result) return
        setCamera(result)
      }}
    >
      <canvas ref={tileCanvasRef} className="absolute inset-0" />
      <canvas ref={gridCanvasRef} className="absolute inset-0" />
      <canvas ref={featureCanvasRef} className="absolute inset-0" />
      <canvas ref={unitCanvasRef} className="absolute inset-0" />
      <canvas ref={overlayCanvasRef} className="absolute inset-0" />
      {debug && (
        <div className="pointer-events-none absolute left-2 top-16 rounded bg-black/60 p-2 text-[11px] text-white z-50">
          <div>Scale: {camera.scale.toFixed(3)}</div>
          <div>Offset: ({Math.round(camera.offsetX)}, {Math.round(camera.offsetY)})</div>
          <div>Hover: {hoverTile ? `${hoverTile.row}, ${hoverTile.col}` : '—'}</div>
          <div>Selection: {selection.kind ? `${selection.kind}:${selection.id ?? '—'}` : 'none'}</div>
        </div>
      )}
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
  imageLoader: ReturnType<typeof getGlobalImageLoader>,
  hexMetrics: ReturnType<typeof calculateOptimalHexSize>
) {
  ctx.clearRect(0, 0, viewport.width, viewport.height)

  tiles.forEach((tile) => {
    const pos = oddrToPixel(tile.col, tile.row, hexMetrics.hexWidth, hexMetrics.hexVertSpacing)
    const screen = toScreenCoords(pos.x, pos.y, camera, viewport)

    ctx.save()
    ctx.translate(screen.x, screen.y)
    ctx.scale(camera.scale, camera.scale)

    // Try to use PNG image first
    const terrainImage = imageLoader.getImage('terrain', tile.terrain.toLowerCase())
    
    if (terrainImage) {
      // Draw PNG image, cropping transparent padding
      // Image: 274x274 with transparent padding (52px horizontal, 39px vertical on each side)
      // Actual hex content: 170x196 (after removing padding)
      const IMG_WIDTH = 274
      const IMG_HEIGHT = 274
      const IMG_PADDING_H = 53
      const IMG_PADDING_V = 40
      
      // Source rectangle (crop to hex content only, no padding)
      const srcX = IMG_PADDING_H
      const srcY = IMG_PADDING_V
      const srcWidth = IMG_WIDTH - (IMG_PADDING_H * 2)   // 170
      const srcHeight = IMG_HEIGHT - (IMG_PADDING_V * 2) // 196
      
      // Destination dimensions (scale hex content to fit current hex size)
      const destWidth = hexMetrics.hexWidth
      const destHeight = hexMetrics.hexHeight
      
      // Draw only the hex content, centered
      ctx.drawImage(
        terrainImage,
        srcX, srcY, srcWidth, srcHeight,  // source rectangle (crop padding)
        -destWidth / 2, -destHeight / 2, destWidth, destHeight  // destination
      )
    } else {
      // Fallback to drawn sprite
      const spriteKey = generateTileSprite(tile.terrain, !!tile.resourceType)
      const sprite = spriteCache.get(
        spriteKey,
        hexMetrics.hexSize * 4,
        hexMetrics.hexSize * 4,
        (spriteCtx) => drawTileSprite(spriteCtx, tile.terrain, !!tile.resourceType, getTerrainColor(tile.terrain))
      )
      ctx.drawImage(sprite, -hexMetrics.hexSize * 2, -hexMetrics.hexSize * 2)
    }

    ctx.restore()
  })
}

  function renderGrid(
    ctx: CanvasRenderingContext2D,
    map: { width: number; height: number },
    camera: CameraState,
    viewport: { width: number; height: number },
    hexMetrics: ReturnType<typeof calculateOptimalHexSize>
  ) {
    ctx.clearRect(0, 0, viewport.width, viewport.height)
    ctx.strokeStyle = 'rgba(255, 255, 255, 0.22)'
    const strokeWidth = 2 / camera.scale

    for (let row = 0; row < map.height; row++) {
      for (let col = 0; col < map.width; col++) {
        const pos = oddrToPixel(col, row, hexMetrics.hexWidth, hexMetrics.hexVertSpacing)
        const screen = toScreenCoords(pos.x, pos.y, camera, viewport)

        ctx.save()
        ctx.translate(screen.x, screen.y)
        ctx.scale(camera.scale, camera.scale)
        ctx.lineWidth = strokeWidth
        drawHexPath(ctx, 0, 0, hexMetrics.hexSize)
        ctx.stroke()
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
  imageLoader: ReturnType<typeof getGlobalImageLoader>,
  hexMetrics: ReturnType<typeof calculateOptimalHexSize>
) {
  ctx.clearRect(0, 0, viewport.width, viewport.height)

  // Get player participant for color coding
  const playerParticipant = gameState.participants.find((p) => p.kind === 'human')

  gameState.cities.forEach((city) => {
    const pos = oddrToPixel(city.col, city.row, hexMetrics.hexWidth, hexMetrics.hexVertSpacing)
    const screen = toScreenCoords(pos.x, pos.y, camera, viewport)
    const isPlayerCity = city.participantId === playerParticipant?.id
    const cityImageKey = isPlayerCity ? 'human' : 'enemy'

    ctx.save()
    ctx.translate(screen.x, screen.y)
    ctx.scale(camera.scale, camera.scale)

    // Try to use PNG image for city
    const cityImage = imageLoader.getImage('city', cityImageKey)
    
    if (cityImage) {
      // Draw PNG image scaled relative to hex size
      const size = hexMetrics.hexSize * 1.75
      ctx.drawImage(cityImage, -size / 2, -size / 2, size, size)
    } else {
      // Fallback to drawn sprite
      const spriteSize = hexMetrics.hexSize * 1.25
      const citySprite = spriteCache.get(generateCitySprite(isPlayerCity), spriteSize, spriteSize, (spriteCtx) => drawCitySprite(spriteCtx, isPlayerCity))
      ctx.drawImage(citySprite, -spriteSize / 2, -spriteSize / 2, spriteSize, spriteSize)
    }

    // HP bar (always drawn on top)
    const hpPercent = city.hp / city.maxHp
    const barWidth = hexMetrics.hexSize
    ctx.fillStyle = hpPercent > 0.5 ? '#22c55e' : hpPercent > 0.25 ? '#f59e0b' : '#ef4444'
    ctx.fillRect(-barWidth / 2, -hexMetrics.hexSize * 0.7, barWidth * hpPercent, 4)

    ctx.restore()
  })
}

function renderUnits(
  ctx: CanvasRenderingContext2D,
  units: GameStateDto['units'],
  participants: GameStateDto['participants'],
  unitDefs: UnitDefinitionDto[],
  camera: CameraState,
  viewport: { width: number; height: number },
  spriteCache: ReturnType<typeof getGlobalSpriteCache>,
  imageLoader: ReturnType<typeof getGlobalImageLoader>,
  imagesLoaded: boolean,
  hexMetrics: ReturnType<typeof calculateOptimalHexSize>
) {
  ctx.clearRect(0, 0, viewport.width, viewport.height)

  units.forEach((unit) => {
    const pos = oddrToPixel(unit.col, unit.row, hexMetrics.hexWidth, hexMetrics.hexVertSpacing)
    const screen = toScreenCoords(pos.x, pos.y, camera, viewport)

    const participant = participants.find((p) => p.id === unit.participantId)
    const isPlayerUnit = participant?.kind === 'human'

    ctx.save()
    ctx.translate(screen.x, screen.y)
    ctx.scale(camera.scale, camera.scale)

    // Map unit typeCode to image name
    const unitImageName = unit.typeCode.toLowerCase()
    const unitImage = imageLoader.getImage('unit', unitImageName)
    
    const spriteSize = hexMetrics.hexSize * 1.25

    // Debug: log image loading status (only once per unit type)
    if (process.env.NODE_ENV === 'development' && imagesLoaded) {
      if (!unitImage) {
        console.warn(`Unit image not loaded: 'unit'/${unitImageName} (typeCode: ${unit.typeCode})`)
        console.warn(`Expected path: /images/game/unit/${unitImageName}.png`)
      }
    }

    if (unitImage) {
      // Draw 3D disk platform viewed from 45° angle above
      const platformRadiusX = spriteSize * 0.55 // Horizontal radius (wider due to perspective)
      const platformRadiusY = spriteSize * 0.4  // Vertical radius (shorter due to perspective)
      const platformThickness = spriteSize * 0.12 // Thickness of the disk
      const platformY = spriteSize * 0.35 // Position platform below center
      
      // Determine colors based on unit ownership and whether it has acted
      let baseColor: string
      let sideColor: string
      let topColor: string
      let edgeColor: string
      
      if (unit.hasActed && isPlayerUnit) {
        // Blue-gray for player units that have moved
        baseColor = '#94a3b8' // slate-400
        sideColor = '#64748b' // slate-500
        topColor = '#cbd5e1' // slate-300 (lighter)
        edgeColor = '#475569' // slate-600 (darkest)
      } else {
        // Normal vibrant colors (player units not moved, or enemy units regardless)
        baseColor = isPlayerUnit ? '#93c5fd' : '#ef4444' // Blue for player, red for enemy
        sideColor = isPlayerUnit ? '#60a5fa' : '#dc2626' // Darker shade for side
        topColor = isPlayerUnit ? '#bae6fd' : '#f87171' // Lighter for top highlight
        edgeColor = isPlayerUnit ? '#3b82f6' : '#b91c1c' // Darkest for edge
      }
      
      // Calculate positions for top and bottom of disk
      const topY = platformY - platformThickness * 0.5
      const bottomY = platformY + platformThickness * 0.5
      const bottomRadiusY = platformThickness * 0.4
      
      // Draw the bottom edge ellipse first (visible from the angle)
      ctx.fillStyle = edgeColor
      ctx.beginPath()
      ctx.ellipse(0, bottomY, platformRadiusX, bottomRadiusY, 0, 0, Math.PI * 2)
      ctx.fill()
      
      // Draw the side rim of the disk (shows thickness from perspective)
      // Create a shape connecting top and bottom ellipses
      ctx.fillStyle = sideColor
      ctx.beginPath()
      // Create points along the top ellipse (front half)
      const points: { x: number; y: number }[] = []
      for (let i = 0; i <= 16; i++) {
        const angle = (i / 16) * Math.PI - Math.PI / 2 // -90° to 90°
        const x = Math.cos(angle) * platformRadiusX
        const y = topY + Math.sin(angle) * platformRadiusY
        points.push({ x, y })
      }
      // Draw top ellipse edge
      ctx.moveTo(points[0].x, points[0].y)
      for (let i = 1; i < points.length; i++) {
        ctx.lineTo(points[i].x, points[i].y)
      }
      // Draw down to bottom ellipse
      const bottomAngle = Math.PI / 2
      ctx.lineTo(Math.cos(bottomAngle) * platformRadiusX, bottomY + Math.sin(bottomAngle) * bottomRadiusY)
      // Draw bottom ellipse edge (backwards)
      for (let i = points.length - 1; i >= 0; i--) {
        const angle = (i / 16) * Math.PI - Math.PI / 2
        const x = Math.cos(angle) * platformRadiusX
        const y = bottomY + Math.sin(angle) * bottomRadiusY
        ctx.lineTo(x, y)
      }
      ctx.closePath()
      ctx.fill()
      
      // Draw the top surface of the disk (ellipse viewed from angle)
      // Create radial gradient for 3D lighting effect
      const gradient = ctx.createRadialGradient(
        0, platformY - platformRadiusY * 0.4, 0,
        0, platformY - platformThickness * 0.5, platformRadiusX * 1.2
      )
      gradient.addColorStop(0, topColor)
      gradient.addColorStop(0.5, baseColor)
      gradient.addColorStop(1, sideColor)
      
      ctx.fillStyle = gradient
      ctx.beginPath()
      ctx.ellipse(0, platformY - platformThickness * 0.5, platformRadiusX, platformRadiusY, 0, 0, Math.PI * 2)
      ctx.fill()
      
      // Add a subtle border to the top edge for definition
      ctx.strokeStyle = edgeColor
      ctx.lineWidth = 1.5
      ctx.beginPath()
      ctx.ellipse(0, platformY - platformThickness * 0.5, platformRadiusX, platformRadiusY, 0, 0, Math.PI * 2)
      ctx.stroke()
      
      // Draw unit image on top of platform (centered)
      ctx.drawImage(unitImage, -spriteSize / 2, -spriteSize / 2, spriteSize, spriteSize)
    } else {
      // Fallback to drawn sprite
      const spriteKey = generateUnitSprite(isPlayerUnit, unit.hasActed)
      const sprite = spriteCache.get(spriteKey, spriteSize, spriteSize, (spriteCtx) => drawUnitSprite(spriteCtx, isPlayerUnit, unit.hasActed))
      ctx.drawImage(sprite, -spriteSize / 2, -spriteSize / 2, spriteSize, spriteSize)
      
      // Draw unit type (scaled font) - only for fallback
      ctx.fillStyle = '#ffffff'
      const fontSize = Math.max(8, hexMetrics.hexSize * 0.3)
      ctx.font = `bold ${fontSize}px sans-serif`
      ctx.textAlign = 'center'
      ctx.textBaseline = 'middle'
      ctx.fillText(unit.typeCode.slice(0, 2).toUpperCase(), 0, 0)
    }

    // HP bar (always drawn on top, similar to cities)
    const unitDef = unitDefs.find((d) => d.code === unit.typeCode)
    if (unitDef) {
      const hpPercent = unit.hp / unitDef.health
      const barWidth = hexMetrics.hexSize
      ctx.fillStyle = hpPercent > 0.5 ? '#22c55e' : hpPercent > 0.25 ? '#f59e0b' : '#ef4444'
      ctx.fillRect(-barWidth / 2, -hexMetrics.hexSize * 0.7, barWidth * hpPercent, 4)
    }

    ctx.restore()
  })
}

function renderOverlay(
  ctx: CanvasRenderingContext2D,
  selection: SelectionState,
  gameState: GameStateDto,
  mapTiles: MapTileDto[],
  tileLookup: Map<number, MapTileDto>,
  preview: PreviewState,
  hoverTile: GridPosition | null,
  camera: CameraState,
  viewport: { width: number; height: number },
  debug: boolean,
  hexMetrics: ReturnType<typeof calculateOptimalHexSize>,
  imageLoader: ReturnType<typeof getGlobalImageLoader>
) {
  ctx.clearRect(0, 0, viewport.width, viewport.height)
  const playerParticipant = gameState.participants.find((p) => p.kind === 'human')

  // Draw resource icons below selection overlays
  const iconSize = hexMetrics.hexSize * 1.1
  const resourceFallbacks: Record<string, string> = {
    wood: 'rgba(163, 98, 41, 0.65)',
    stone: 'rgba(120, 120, 120, 0.65)',
    wheat: 'rgba(236, 201, 75, 0.65)',
    iron: 'rgba(90, 106, 128, 0.65)',
  }
  mapTiles.forEach((tile) => {
    if (!tile.resourceType) return
    const resourceKey = tile.resourceType.toLowerCase()
    const pos = oddrToPixel(tile.col, tile.row, hexMetrics.hexWidth, hexMetrics.hexVertSpacing)
    const screen = toScreenCoords(pos.x, pos.y, camera, viewport)

    ctx.save()
    ctx.translate(screen.x, screen.y)
    ctx.scale(camera.scale, camera.scale)

    const icon = imageLoader.getImage('resources', resourceKey)
    if (icon) {
      ctx.drawImage(icon, -iconSize / 2, -iconSize / 2, iconSize, iconSize)
    } else {
      ctx.fillStyle = resourceFallbacks[resourceKey] ?? 'rgba(255, 215, 0, 0.6)'
      ctx.beginPath()
      ctx.arc(0, 0, Math.max(6, iconSize / 2.5), 0, Math.PI * 2)
      ctx.fill()
    }

    ctx.restore()
  })

  // Draw reachable tiles
  if (preview.reachable) {
    preview.reachable.forEach((tile) => {
      const pos = oddrToPixel(tile.col, tile.row, hexMetrics.hexWidth, hexMetrics.hexVertSpacing)
      const screen = toScreenCoords(pos.x, pos.y, camera, viewport)

      ctx.save()
      ctx.translate(screen.x, screen.y)
      ctx.scale(camera.scale, camera.scale)

      drawHexPath(ctx, 0, 0, hexMetrics.hexSize)
      ctx.fillStyle = 'rgba(255, 255, 255, 0.3)'
      ctx.fill()

      ctx.restore()
    })
  }

  // Draw attackable tiles
  if (preview.attackable) {
    preview.attackable.forEach((tile) => {
      const pos = oddrToPixel(tile.col, tile.row, hexMetrics.hexWidth, hexMetrics.hexVertSpacing)
      const screen = toScreenCoords(pos.x, pos.y, camera, viewport)

      ctx.save()
      ctx.translate(screen.x, screen.y)
      ctx.scale(camera.scale, camera.scale)

      drawHexPath(ctx, 0, 0, hexMetrics.hexSize)
      ctx.fillStyle = 'rgba(239, 68, 68, 0.15)'
      ctx.fill()

      ctx.restore()
    })
  }

  // Highlight managed city tiles when a city is selected
  if (selection.kind === 'city' && selection.id) {
    const selectedCity = gameState.cities.find((c) => c.id === selection.id)
    if (selectedCity) {
      const workedTiles = gameState.cityTiles.filter((t) => t.cityId === selectedCity.id)
      const isPlayerCity = selectedCity.participantId === playerParticipant?.id
      workedTiles.forEach((link) => {
        const mapTile = tileLookup.get(link.tileId)
        if (!mapTile) return

        const pos = oddrToPixel(mapTile.col, mapTile.row, hexMetrics.hexWidth, hexMetrics.hexVertSpacing)
        const screen = toScreenCoords(pos.x, pos.y, camera, viewport)

        ctx.save()
        ctx.translate(screen.x, screen.y)
        ctx.scale(camera.scale, camera.scale)

        drawHexPath(ctx, 0, 0, hexMetrics.hexSize)
        ctx.fillStyle = isPlayerCity ? 'rgba(253, 253, 253, 0.4)' : 'rgba(248, 113, 113, 0.2)'
        ctx.fill()

        ctx.restore()
      })
    }
  }

  // Draw path preview
  if (preview.kind === 'move' && preview.path) {
    ctx.save()
    ctx.strokeStyle = '#fbbf24'
    ctx.lineWidth = 3 * camera.scale
    ctx.setLineDash([5 * camera.scale, 3 * camera.scale])

    ctx.beginPath()
    preview.path.forEach((tile, i) => {
      const pos = oddrToPixel(tile.col, tile.row, hexMetrics.hexWidth, hexMetrics.hexVertSpacing)
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
      const pos = oddrToPixel(targetCol, targetRow, hexMetrics.hexWidth, hexMetrics.hexVertSpacing)
      const screen = toScreenCoords(pos.x, pos.y, camera, viewport)

      ctx.save()
      ctx.translate(screen.x, screen.y)
      ctx.scale(camera.scale, camera.scale)

      drawHexPath(ctx, 0, 0, hexMetrics.hexSize)
      ctx.strokeStyle = '#fbbf24'
      ctx.lineWidth = 3
      ctx.stroke()

      ctx.restore()
    }
  }

  // Highlight target tile
  if (preview.targetTile) {
    const pos = oddrToPixel(preview.targetTile.col, preview.targetTile.row, hexMetrics.hexWidth, hexMetrics.hexVertSpacing)
    const screen = toScreenCoords(pos.x, pos.y, camera, viewport)

    ctx.save()
    ctx.translate(screen.x, screen.y)
    ctx.scale(camera.scale, camera.scale)

    drawHexPath(ctx, 0, 0, hexMetrics.hexSize)
    ctx.strokeStyle = preview.kind === 'attack' ? '#ef4444' : '#22c55e'
    ctx.lineWidth = 2
    ctx.stroke()

    ctx.restore()
  }

  if (debug) {
    renderDebug(ctx, gameState, hoverTile, selection, camera, viewport, hexMetrics)
  }
}

function renderDebug(
  ctx: CanvasRenderingContext2D,
  gameState: GameStateDto,
  hoverTile: GridPosition | null,
  selection: SelectionState,
  camera: CameraState,
  viewport: { width: number; height: number },
  hexMetrics: ReturnType<typeof calculateOptimalHexSize>
) {
  // Crosshair at center of every tile (light color)
  ctx.save()
  ctx.strokeStyle = 'rgba(255,255,255,0.3)'
  ctx.lineWidth = 1
  for (let row = 0; row < gameState.map.height; row++) {
    for (let col = 0; col < gameState.map.width; col++) {
      const pos = oddrToPixel(col, row, hexMetrics.hexWidth, hexMetrics.hexVertSpacing)
      const screen = toScreenCoords(pos.x, pos.y, camera, viewport)
      const s = 4 * camera.scale
      ctx.beginPath()
      ctx.moveTo(screen.x - s, screen.y)
      ctx.lineTo(screen.x + s, screen.y)
      ctx.moveTo(screen.x, screen.y - s)
      ctx.lineTo(screen.x, screen.y + s)
      ctx.stroke()
    }
  }
  ctx.restore()

  // Emphasize hover tile crosshair (cyan)
  if (hoverTile) {
    const pos = oddrToPixel(hoverTile.col, hoverTile.row, hexMetrics.hexWidth, hexMetrics.hexVertSpacing)
    const screen = toScreenCoords(pos.x, pos.y, camera, viewport)
    const s = 8 * camera.scale
    ctx.save()
    ctx.strokeStyle = 'rgba(34,211,238,0.9)'
    ctx.lineWidth = Math.max(1, 1.5 * camera.scale)
    ctx.beginPath()
    ctx.moveTo(screen.x - s, screen.y)
    ctx.lineTo(screen.x + s, screen.y)
    ctx.moveTo(screen.x, screen.y - s)
    ctx.lineTo(screen.x, screen.y + s)
    ctx.stroke()
    ctx.restore()
  }

  // Selection bounds: draw AABB of tile sprite area (magenta)
  if (selection.kind && selection.id) {
    let row: number | undefined
    let col: number | undefined
    if (selection.kind === 'unit') {
      const unit = gameState.units.find((u) => u.id === selection.id)
      if (unit) { row = unit.row; col = unit.col }
    } else if (selection.kind === 'city') {
      const city = gameState.cities.find((c) => c.id === selection.id)
      if (city) { row = city.row; col = city.col }
    }
    if (row !== undefined && col !== undefined) {
      const pos = oddrToPixel(col, row, hexMetrics.hexWidth, hexMetrics.hexVertSpacing)
      const screen = toScreenCoords(pos.x, pos.y, camera, viewport)
      const hw = hexMetrics.hexSize * 2 * camera.scale
      const hh = hexMetrics.hexSize * 2 * camera.scale
      ctx.save()
      ctx.strokeStyle = 'rgba(236,72,153,0.95)'
      ctx.lineWidth = Math.max(1, 1.5 * camera.scale)
      ctx.strokeRect(screen.x - hw, screen.y - hh, hw * 2, hh * 2)
      ctx.restore()
    }
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
