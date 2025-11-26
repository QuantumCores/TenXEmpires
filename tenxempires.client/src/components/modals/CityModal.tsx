import { useId, useMemo } from 'react'
import type { CityInStateDto, CityResourceDto, GameStateDto } from '../../types/game'

// ============================================================================
// Types
// ============================================================================

export interface CityModalProps {
  onRequestClose: () => void
  gameState: GameStateDto
  cityId: number
}

interface ResourceDisplay {
  type: string
  label: string
  amount: number
  icon: string
  maxAmount: number
}

// ============================================================================
// Constants
// ============================================================================

const STORAGE_CAP = 100

const RESOURCE_CONFIG: Record<string, { label: string; icon: string }> = {
  wheat: { label: 'Wheat', icon: '/images/game/resources/wheat.png' },
  wood: { label: 'Wood', icon: '/images/game/resources/wood.png' },
  iron: { label: 'Iron', icon: '/images/game/resources/iron.png' },
  stone: { label: 'Stone', icon: '/images/game/resources/stone.png' },
}

const RESOURCE_ORDER = ['wheat', 'wood', 'iron', 'stone']

// ============================================================================
// Main Component
// ============================================================================

export function CityModal({ onRequestClose, gameState, cityId }: CityModalProps) {
  const titleId = useId()

  // Find the city data
  const city = useMemo(() => {
    return gameState.cities.find((c) => c.id === cityId)
  }, [gameState.cities, cityId])

  // Get city resources
  const resources = useMemo((): ResourceDisplay[] => {
    const cityResources = gameState.cityResources.filter((r) => r.cityId === cityId)
    const resourceMap = new Map<string, number>()
    
    cityResources.forEach((r) => {
      resourceMap.set(r.resourceType.toLowerCase(), r.amount)
    })

    return RESOURCE_ORDER.map((type) => {
      const config = RESOURCE_CONFIG[type]
      return {
        type,
        label: config.label,
        amount: resourceMap.get(type) ?? 0,
        icon: config.icon,
        maxAmount: STORAGE_CAP,
      }
    })
  }, [gameState.cityResources, cityId])

  // Count worked tiles
  const workedTilesCount = useMemo(() => {
    return gameState.cityTiles.filter((t) => t.cityId === cityId).length
  }, [gameState.cityTiles, cityId])

  if (!city) {
    return (
      <div className="flex flex-col gap-4 p-2">
        <p className="text-sm text-slate-600">City not found.</p>
        <button
          type="button"
          className="rounded bg-slate-100 px-4 py-2 text-sm font-medium text-slate-700 hover:bg-slate-200"
          onClick={onRequestClose}
        >
          Close
        </button>
      </div>
    )
  }

  return (
    <div className="flex flex-col gap-5">
      {/* Header */}
      <header className="flex items-center justify-between border-b border-slate-200 pb-3">
        <h2 id={titleId} className="text-xl font-semibold text-slate-800">
          City Management
        </h2>
        <button
          type="button"
          className="rounded p-1.5 text-slate-400 hover:bg-slate-100 hover:text-slate-600 transition-colors"
          onClick={onRequestClose}
          aria-label="Close city modal"
        >
          <svg className="h-5 w-5" fill="none" viewBox="0 0 24 24" stroke="currentColor">
            <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M6 18L18 6M6 6l12 12" />
          </svg>
        </button>
      </header>

      {/* City Stats */}
      <CityStats city={city} workedTilesCount={workedTilesCount} />

      {/* Resources Section */}
      <section aria-labelledby="resources-heading">
        <h3 id="resources-heading" className="mb-3 text-sm font-semibold uppercase tracking-wide text-slate-500">
          Resources
        </h3>
        <ResourceGrid resources={resources} />
      </section>

      {/* Buildings Section */}
      <section aria-labelledby="buildings-heading">
        <h3 id="buildings-heading" className="mb-3 text-sm font-semibold uppercase tracking-wide text-slate-500">
          Buildings
        </h3>
        <BuildingsList />
      </section>

      {/* Actions placeholder - will be implemented in later stories */}
      <section className="rounded-lg border border-slate-200 bg-slate-50 p-4">
        <p className="text-center text-sm text-slate-500">
          City actions coming soon...
        </p>
      </section>

      {/* Footer */}
      <footer className="flex items-center justify-end border-t border-slate-200 pt-4">
        <button
          type="button"
          className="rounded-md bg-slate-100 px-4 py-2 text-sm font-medium text-slate-700 hover:bg-slate-200 transition-colors"
          onClick={onRequestClose}
        >
          Close
        </button>
      </footer>
    </div>
  )
}

// ============================================================================
// Subcomponents
// ============================================================================

function CityStats({ city, workedTilesCount }: { city: CityInStateDto; workedTilesCount: number }) {
  const hpPercent = (city.hp / city.maxHp) * 100

  return (
    <div className="grid grid-cols-2 gap-4 rounded-lg bg-gradient-to-br from-slate-50 to-slate-100 p-4">
      <StatItem label="Health" value={`${city.hp} / ${city.maxHp}`}>
        <div className="mt-1.5 h-2 w-full overflow-hidden rounded-full bg-slate-200">
          <div
            className={`h-full transition-all ${
              hpPercent > 50 ? 'bg-emerald-500' : hpPercent > 25 ? 'bg-amber-500' : 'bg-red-500'
            }`}
            style={{ width: `${hpPercent}%` }}
          />
        </div>
      </StatItem>
      <StatItem label="Defence" value="10" />
      <StatItem label="Worked Tiles" value={String(workedTilesCount)} />
      <StatItem label="Storage Cap" value={`${STORAGE_CAP} / resource`} />
    </div>
  )
}

function StatItem({ label, value, children }: { label: string; value: string; children?: React.ReactNode }) {
  return (
    <div className="flex flex-col">
      <span className="text-xs text-slate-500">{label}</span>
      <span className="text-sm font-semibold text-slate-800">{value}</span>
      {children}
    </div>
  )
}

function ResourceGrid({ resources }: { resources: ResourceDisplay[] }) {
  return (
    <div className="grid grid-cols-2 gap-3 sm:grid-cols-4">
      {resources.map((resource) => (
        <ResourceCard key={resource.type} resource={resource} />
      ))}
    </div>
  )
}

function ResourceCard({ resource }: { resource: ResourceDisplay }) {
  const fillPercent = (resource.amount / resource.maxAmount) * 100
  const isAtCap = resource.amount >= resource.maxAmount

  return (
    <div
      className={`relative overflow-hidden rounded-lg border p-3 transition-all ${
        isAtCap
          ? 'border-amber-300 bg-amber-50'
          : 'border-slate-200 bg-white hover:border-slate-300'
      }`}
    >
      <div className="flex items-center gap-2">
        <img
          src={resource.icon}
          alt={resource.label}
          className="h-8 w-8 object-contain"
          loading="lazy"
        />
        <div className="flex flex-col">
          <span className="text-xs text-slate-500">{resource.label}</span>
          <span className="text-lg font-bold text-slate-800">
            {resource.amount}
          </span>
        </div>
      </div>
      {/* Storage bar */}
      <div className="mt-2 h-1.5 w-full overflow-hidden rounded-full bg-slate-100">
        <div
          className={`h-full transition-all ${
            isAtCap ? 'bg-amber-400' : 'bg-indigo-400'
          }`}
          style={{ width: `${fillPercent}%` }}
        />
      </div>
      {isAtCap && (
        <span className="absolute right-1 top-1 rounded bg-amber-200 px-1 text-[10px] font-medium text-amber-700">
          FULL
        </span>
      )}
    </div>
  )
}

function BuildingsList() {
  // Buildings will be implemented in US-104, for now show empty state
  return (
    <div className="rounded-lg border border-dashed border-slate-200 bg-slate-50 p-4">
      <div className="flex items-center justify-center gap-2 text-slate-400">
        <svg className="h-5 w-5" fill="none" viewBox="0 0 24 24" stroke="currentColor">
          <path
            strokeLinecap="round"
            strokeLinejoin="round"
            strokeWidth={1.5}
            d="M19 21V5a2 2 0 00-2-2H7a2 2 0 00-2 2v16m14 0h2m-2 0h-5m-9 0H3m2 0h5M9 7h1m-1 4h1m4-4h1m-1 4h1m-5 10v-5a1 1 0 011-1h2a1 1 0 011 1v5m-4 0h4"
          />
        </svg>
        <span className="text-sm">No buildings constructed</span>
      </div>
    </div>
  )
}

