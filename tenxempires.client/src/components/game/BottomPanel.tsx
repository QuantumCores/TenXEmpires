import type { GameStateDto, SelectionState } from '../../types/game'

interface BottomPanelProps {
  gameState: GameStateDto
  selection: SelectionState
}

export function BottomPanel({ gameState, selection }: BottomPanelProps) {
  if (!selection.kind || !selection.id) {
    return null
  }

  if (selection.kind === 'unit') {
    const unit = gameState.units.find((u) => u.id === selection.id)
    if (!unit) return null

    const unitDef = gameState.unitDefinitions.find((d) => d.code === unit.typeCode)
    if (!unitDef) return null

    return (
      <div className="absolute bottom-4 left-4 rounded-lg border border-slate-300 bg-white p-4 shadow-lg">
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

  if (selection.kind === 'city') {
    const city = gameState.cities.find((c) => c.id === selection.id)
    if (!city) return null

    const cityResources = gameState.cityResources.filter((r) => r.cityId === city.id)
    const workedTiles = gameState.cityTiles.filter((t) => t.cityId === city.id).length

    return (
      <div className="absolute bottom-4 left-4 rounded-lg border border-slate-300 bg-white p-4 shadow-lg">
        <h3 className="mb-2 font-semibold">City</h3>
        <div className="space-y-1 text-sm">
          <div className="flex justify-between gap-8">
            <span className="text-slate-600">HP:</span>
            <span className="font-medium">{city.hp}/{city.maxHp}</span>
          </div>
          <div className="flex justify-between gap-8">
            <span className="text-slate-600">Tiles:</span>
            <span className="font-medium">{workedTiles}</span>
          </div>
          {cityResources.length > 0 && (
            <div className="mt-2">
              <div className="text-xs text-slate-600">Resources:</div>
              {cityResources.map((r) => (
                <div key={r.resourceType} className="ml-2 text-xs">
                  {r.resourceType}: {r.amount}
                </div>
              ))}
            </div>
          )}
        </div>
      </div>
    )
  }

  return null
}

