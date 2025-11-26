import type { GameStateDto, MapTileDto, SelectionState } from '../../types/game'

interface BottomPanelProps {
  gameState: GameStateDto
  mapTiles: MapTileDto[]
  selection: SelectionState
}

export function BottomPanel({ gameState, mapTiles, selection }: BottomPanelProps) {
  if (!selection.kind || !selection.id) {
    return null
  }

  if (selection.kind === 'unit') {
    const unit = gameState.units.find((u) => u.id === selection.id)
    if (!unit) return null

    const unitDef = gameState.unitDefinitions.find((d) => d.code === unit.typeCode)
    if (!unitDef) return null

    return (
      <div className="absolute bottom-4 left-4 rounded-lg border border-slate-300 bg-white p-4 shadow-lg" data-testid="bottom-panel-unit">
        <h3 className="mb-2 font-semibold">{unitDef.code}</h3>
        <div className="space-y-1 text-sm">
          <div className="flex justify-between gap-8">
            <span className="text-slate-600">HP:</span>
            <span className="font-medium">{unit.hp}/{unitDef.health}</span>
          </div>
          <div className="flex justify-between gap-8">
            <span className="text-slate-600">Attack:</span>
            <span className="font-medium">{unitDef.attack}</span>
          </div>
          <div className="flex justify-between gap-8">
            <span className="text-slate-600">Defence:</span>
            <span className="font-medium">{unitDef.defence}</span>
          </div>
          <div className="flex justify-between gap-8">
            <span className="text-slate-600">Move:</span>
            <span className="font-medium">{unitDef.movePoints}</span>
          </div>
          {unitDef.isRanged && (
            <div className="flex justify-between gap-8">
              <span className="text-slate-600">Range:</span>
              <span className="font-medium">{unitDef.rangeMin}-{unitDef.rangeMax}</span>
            </div>
          )}
          <div className="mt-2 flex items-center gap-2">
            <span className={`rounded px-2 py-1 text-xs ${
              unit.hasActed 
                ? 'bg-slate-100 text-slate-600' 
                : 'bg-green-100 text-green-700'
            }`}>
              {unit.hasActed ? 'Acted' : 'Ready'}
            </span>
          </div>
        </div>
      </div>
    )
  }

  // City info is now shown in the City Modal, so don't render bottom panel for cities
  if (selection.kind === 'city') {
    return null
  }

  if (selection.kind === 'tile') {
    const tile = mapTiles.find((t) => t.id === selection.id)
    if (!tile) return null

    const formatLabel = (value: string) => value.charAt(0).toUpperCase() + value.slice(1)
    const tileState = (gameState.gameTiles ?? []).find((t) => t.tileId === tile.id)
    const resourceType = tileState?.resourceType ?? tile.resourceType
    const resourceAmount = tileState?.resourceAmount ?? tile.resourceAmount

    return (
      <div className="absolute bottom-4 left-4 rounded-lg border border-slate-300 bg-white p-4 shadow-lg" data-testid="bottom-panel-tile">
        <h3 className="mb-2 font-semibold">Tile</h3>
        <div className="space-y-1 text-sm">
          <div className="flex justify-between gap-8">
            <span className="text-slate-600">Terrain:</span>
            <span className="font-medium">{formatLabel(tile.terrain)}</span>
          </div>
          {resourceType && (
            <div className="flex justify-between gap-8">
              <span className="text-slate-600">Resource:</span>
              <span className="font-medium">
                {formatLabel(resourceType)} ({resourceAmount})
              </span>
            </div>
          )}
        </div>
      </div>
    )
  }

  return null
}
