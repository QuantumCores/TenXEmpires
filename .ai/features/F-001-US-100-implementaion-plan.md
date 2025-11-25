# F-001 / US-100 Manual City Economy Implementation Guide

## 1. Client side Step-by-step implementation plan
- **DTOs & API wiring**: Extend `tenxempires.client/src/types/game.ts` with city flags/fields (`cityHasActed`, `defence`, `buildings`, `storageCap`, `initialTiles`) and new commands (`SpawnUnitCommand`, `BuildCityBuildingCommand`, `ExpandTerritoryCommand`). Add REST calls in `src/api/games.ts` for `/actions/city/spawn`, `/actions/city/build`, `/actions/city/expand`, including idempotency header. Wire mutations + optimistic cache updates in `useGameQueries.ts` and export React Query keys.
- **UI state & hotkeys**: Add city modal open/close + expansion mode flags to `useUiStore` (or a focused game UI store) so `useGameHotkeys` can treat modal/expansion as blocking End Turn (E) and other shortcuts. Ensure `EndTurnButton` and `GameMapPage` compute `isActionsDisabled` with modal/expansion state.
- **Interaction priority**: Update `MapCanvasStack.tsx` click handling so a tile with a friendly unit + city selects the unit on first click and selects/opens the city on second click. Preserve unit-first priority for enemies. Keep right-click/ESC clearing selection. Add stable order and state for expansion highlight overlay (strong blue borders).
- **City Modal UX**: Create `CityModal` in `src/components/modals` using `ModalContainer`. Show resources with icons from `public/images/game/resources`, building icons from `public/images/game/buildings`, defence, and action-availability state. Action buttons: Spawn Warrior (10 iron), Spawn Slinger (10 stone), Build Wooden Walls (50 wood, one-time, shows `human-wooden-wall.png`), Expand (closes modal and enters expansion mode). Disable End Turn while modal is open. After any city action, mark other actions disabled for that city and show tooltip/state.
- **Expansion mode**: When user clicks "Expand", close modal, store active city context, compute valid targets (adjacent, not enemy-owned/occupied). Render highlights on `MapCanvasStack` overlay. Clicking a valid tile calls expand mutation, deducts wheat using server response, refreshes state, and reopens modal. ESC/right-click cancels without spending.
- **Spawn/build flows**: On action selection, enable Confirm. Confirm sends mutation; on success, refresh game state and close modal. For spawn placement, show deterministic preview order (city tile then clockwise neighbors) in UI copy/tooltips to match server tie-breaker. After action, reflect city action flag and disable further actions until next turn.
- **Visual updates**: Update sprites/overlay renderer to show wooden wall variant for cities with that building and to surface city action-used indicator (e.g., greyed city badge). Add resource cap display (100) and overflow hint.
- **Testing**: Add Vitest/RTL coverage for city modal states, action button disabling after action, interaction priority (unit-first then city), expansion highlight filtering, and hotkey blocking while modal open.

## 2. Server side Step-by-step implementation plan
- **Config & constants**: Add `GameSettings.ManualExpansionBaseCost` (default 20) and, if needed, `CityStorageCap` (100). Add building/resource constants (e.g., `BuildingTypes.WoodenWall`, `CityActionErrors`) and update `ResourceTypes.InitialAmounts` to match PRD (50 wheat, 50 wood, 20 iron, 20 stone).
- **Entities & DbContext**: Extend `City` with `Defence`, `HasActedThisTurn`, `InitialTileCount`. Create `CityBuilding` entity (Id, CityId, BuildingType, CreatedAt). Update `TenXDbContext` with new DbSets/relationships and configure unique (CityId, BuildingType) constraint. Ensure `GameTileState` and `CityResource` remain tracked for harvest.
- **Data contracts**: Add commands `SpawnUnitCommand(long CityId, string UnitCode)`, `BuildCityBuildingCommand(long CityId, string BuildingType)`, `ExpandTerritoryCommand(long CityId, GridPosition Target)`. Extend `GameStateDto` + DTOs to emit city defence, hasActed flag, buildings collection, storage cap, and initial tiles (for cost UI). Update examples and `types` alignment for Swagger generation.
- **Controllers**: In `GamesController`, add POST endpoints under `/games/{gameId}/actions/city/spawn`, `/build`, `/expand` delegating to `IActionService`. Apply `[Authorize]`, rate limiting, idempotency header, and consistent error bodies. Update swagger examples for new commands.
- **Services**: 
  - Update `ActionService` to handle spawn/build/expand: validate user turn, city ownership, city not already acted, resource sufficiency, and target validity; enforce cap-aware spending; mark `HasActedThisTurn` upon success; use deterministic spawn tile order (city tile then clockwise neighbors). Reject duplicate buildings.
  - Update `TurnService.HarvestCityResources` to enforce cap 100 per resource (discard overflow) and stop harvest when cap reached; ensure tile resource depletion still applied. Reset `HasActedThisTurn` for next participant in `AdvanceTurnAsync`. Keep AI auto-production logic unchanged but obey caps. 
  - Update `CombatHelper.ResolveAttackOnCity` to use city.Defence (base 10 + building bonuses) instead of fixed constant; persist defence changes when walls built.
  - Update `GameStateService` projection to include new city fields/buildings and city action flag.
  - Update `GameSeedingService` to initialize starting resources to PRD values, set `Defence = 10`, `HasActedThisTurn = false`, and `InitialTileCount = CityTiles.Count` at creation.
- **Persistence & saves**: Ensure `SaveService` and load paths serialize/restore new city fields/buildings. Add repository/UoW updates if needed.
- **Validation & errors**: Define specific messages/codes for insufficient resources, invalid target, city already acted, building already exists, spawn blocked, expansion blocked, not player turn. Map to 400/403/409 as appropriate.
- **Testing**: Add xUnit + Testcontainers integration tests for harvest cap, spawn/build/expand success/failure cases, per-turn city action enforcement, expansion cost formula, city defence damage change after walls, and serialization of new fields in saves. Add controller tests for auth/validation and idempotency reuse.

## 3. Database changes (if needed)
- Add migration via DbUp in `db/migrations` to:
  - Alter `app.cities` to include `defence INT NOT NULL DEFAULT 10`, `has_acted_this_turn BOOLEAN NOT NULL DEFAULT FALSE`, `initial_tile_count INT NOT NULL DEFAULT 0`.
  - Create `app.city_buildings` table (id bigserial PK, city_id FK references cities, building_type text, created_at timestamptz default now()) with unique index on (city_id, building_type).
  - Backfill `initial_tile_count` using existing `city_tiles` counts and set `defence=10` for existing rows; ensure existing resources unaffected.
  - Seed starting resources in `city_resources` for new games to PRD defaults and set `appsettings` JSON for `ManualExpansionBaseCost` (20).
- Verify DbUp order and idempotency, update `TenXDbContext` model snapshot if used, and extend testing scripts in `db/testing` if needed.

## 4. Error handling
1) Unauthorized or missing game access -> 401/403 with `UNAUTHORIZED`.  
2) Not player turn / turn in progress -> 409 `NOT_PLAYER_TURN` or `TURN_IN_PROGRESS`.  
3) Invalid input (bad coords/unit code/building type) -> 400 `INVALID_INPUT`.  
4) City not owned or city already acted this turn -> 403 `CITY_NOT_OWNED` / 409 `CITY_ALREADY_ACTED`.  
5) Insufficient resources -> 409 `INSUFFICIENT_RESOURCES` with required/current amounts.  
6) Resource cap reached (harvest overflow) -> silently discard harvest; expose warning in turn summary/UI toast.  
7) Spawn blocked (no free tile) -> 409 `SPAWN_BLOCKED`; do not consume resources or action flag.  
8) Expansion invalid (non-adjacent, enemy-owned, enemy unit present, tile already claimed/out of bounds) -> 400/409 `INVALID_EXPANSION_TARGET`.  
9) Building already constructed -> 409 `BUILDING_EXISTS`.  
10) Idempotency reuse / concurrent mutation conflict -> return cached response or 409 `IDEMPOTENCY_CONFLICT`.  
11) Persistence/transaction failure -> 500 `INTERNAL_ERROR` with Serilog correlation IDs.  
12) Config/assets missing (base cost, sprites) -> 500 with actionable message; add startup validation/logs.

## 5. Security considerations
- Enforce `[Authorize]` + per-game ownership checks on all new endpoints; preserve rate limiting and anti-forgery settings. 
- Server-authoritative validation for resources, action limits, and tile eligibility; never trust client-calculated costs. 
- Use idempotency keys for state-changing city actions; log correlation IDs with Serilog for auditability. 
- Keep data access RLS in migrations (city_buildings inherits city ownership via FK). 
- Validate inputs against allowlists (resource/building/unit codes) to prevent injection; parameterize SQL in DbUp scripts. 
- Ensure hotkeys/actions are disabled client-side while modal open to avoid unintended commands; still enforce server guards for replay protection.
