# F-001 / US-102 Manual Unit Spawn Implementation Guide

## 1. Client side Step-by-step implementation plan
- **Types & API surface**: Extend `tenxempires.client/src/types/game.ts` with `SpawnUnitCommand { cityId: number; unitCode: string; }`, city `hasActed`, and per-city resources so the modal can show availability. Add `spawnUnit` POST in `src/api/games.ts` targeting `/api/games/{gameId}/actions/city/spawn` with `Idempotency-Key` header.
- **React Query hooks**: In `src/features/game/useGameQueries.ts`, add `useSpawnUnit(gameId)` mutation (mirroring move/attack) that calls `spawnUnit`, routes API errors through `useGameErrorHandler`, caches optimistic previous state, and writes returned `ActionStateResponse.state` to `gameKeys.state`.
- **UI state & selection**: Ensure `useUiStore` tracks the selected city ID and whether the city has already acted; keep End Turn (E) disabled while the city modal is open (existing hotkey block from US-100 should stay in place). Maintain unit-first click behavior in `src/components/game/MapCanvasStack.tsx`; second click opens city modal with that city preselected.
- **City Modal updates** (`src/components/modals/CityModal.tsx` or equivalent): Add spawn panel with Warrior (cost 10 iron) and Slinger (cost 10 stone). Show costs, current stock, and disabled state when resources are insufficient or the city has already acted. Selection enables Confirm; Confirm triggers `useSpawnUnit`. While the request is in-flight, show a spinner/disabled Confirm to prevent double-submit. After success, close modal or show a lightweight success toast; modal actions remain disabled if `hasActed` is true in returned state.
- **Placement messaging**: In the modal, show helper text about spawn rules (“nearest free adjacent tile; 1 unit per tile”) and surface server error toasts when spawn is blocked due to no space.
- **Map refresh & overlays**: After a successful spawn mutation, rely on updated `GameState` to render the new unit. If expansion highlights or other overlays exist, ensure they clear when the modal closes or after spawn to avoid stale UI.
- **Assets & copy**: Reuse existing unit icons; no new art required. Keep text strings in `src/locales`/constants if present for consistency.

## 2. Server side Step-by-step implementation plan
- **Contracts & routing**: Add `SpawnUnitCommand(long CityId, string UnitCode)` to `TenXEmpires.Server.Domain/DataContracts/Commands.cs`. Update `IGamesController`/`GamesController` to expose `POST /api/games/{gameId}/actions/city/spawn` accepting the command and optional `Idempotency-Key` header; document in Swagger.
- **Action service**: Extend `IActionService` and `ActionService` with `SpawnUnitAsync(Guid userId, long gameId, SpawnUnitCommand cmd, string? idempotencyKey, CancellationToken)`. Use idempotency cache key `CacheKeys.SpawnUnitIdempotency(gameId, key)`.
- **Validation**: In `SpawnUnitAsync`, load game + map + active participant; ensure active participant is the requesting human and `TurnInProgress` is false before setting the guard. Load the target city with resources, tile, tile links, and participant. Reject if city not owned, city already acted, invalid unit code (allowlist Warrior/Slinger), missing unit definition, or insufficient resources (Iron for Warrior, Stone for Slinger).
- **Spawn tile selection**: Gather occupied tiles (all units) and disallow water/sea tiles. Compute spawn tile deterministically: prefer the city tile if empty and passable; otherwise iterate the 6 neighbors in a fixed clockwise order from `HexagonalGrid.CubeDirections` and pick the first free, in-bounds, non-water, non-occupied tile. If none exist, return a spawn-blocked error without consuming resources or marking action.
- **State changes**: Create the Unit entity with `HasActed = false`, place on chosen tile, deduct resources atomically, set `city.HasActedThisTurn = true`, update timestamps. Save changes within the transaction. Clear `TurnInProgress` on completion/failure.
- **Game state projection**: Ensure `GameStateService` includes city resources and `HasActedThisTurn` so the client can disable actions post-spawn. Update `UnitDefinitionDto`/city DTOs if additional fields are needed for UI messaging.
- **Turn reset**: In `TurnService.AdvanceTurnAsync`, reset `HasActedThisTurn` for all cities of the next participant (similar to unit reset) so a new turn re-enables actions.
- **Logging & metrics**: Add Serilog events for spawn attempts (cityId, unitCode, success/failure reason) and include spawn results in the turn summary if applicable for analytics.

## 3. Database changes (if needed)
- No new schema is required for US-102 beyond the city resources and `has_acted_this_turn` flag introduced for the manual economy. Verify migrations already include the city action flag and resource tables; otherwise, add the column to `app.cities` (bool default false) and ensure DbContext tracks it.

## 4. Error handling
1. Unauthorized or game not owned by user → 401/403 `UNAUTHORIZED`.  
2. Not the player’s turn or active participant not human → 409 `NOT_PLAYER_TURN`.  
3. Turn already in progress (concurrent action) → 409 `TURN_IN_PROGRESS`.  
4. City not found for game/participant → 404/403 `CITY_NOT_FOUND_OR_OWNED`.  
5. City has already acted this turn → 409 `CITY_ALREADY_ACTED`.  
6. Invalid unit code or missing unit definition → 400 `INVALID_UNIT`.  
7. Insufficient resources (iron/stone) → 409 `INSUFFICIENT_RESOURCES` with required/current amounts.  
8. No valid spawn tile (all adjacent blocked/water/occupied) → 409 `SPAWN_BLOCKED` without consuming resources or action flag.  
9. Idempotency key replay → 200 with cached response; conflict reuse with different payload → 409 `IDEMPOTENCY_CONFLICT`.  
10. Persistence/transaction failure → 500 `INTERNAL_ERROR` with correlation ID; ensure `TurnInProgress` guard is cleared in a finally block.

## 5. Security considerations
- Enforce `[Authorize]` on the spawn endpoint and verify game ownership plus active participant matching the caller.  
- Server-authoritative validation of resource balances, allowed unit codes, and city action limit; ignore any client-side cost calculations.  
- Maintain 1UPT and terrain checks server-side to prevent spawning on blocked/water tiles regardless of client UI.  
- Require/accept idempotency keys for POST to prevent double-spend on retries; log correlation IDs for auditability.  
- Keep DTO validation strict (non-null cityId/unitCode) and sanitize logging to avoid leaking PII.

## 6. Backend unit test scenarios
- Spawn succeeds for Warrior when iron ≥10: verifies resource deduction, new unit placement on nearest free tile, city action flag set, and returned state includes the unit.  
- Spawn fails when city already acted: returns `CITY_ALREADY_ACTED`, no resource change.  
- Spawn fails with insufficient stone/iron: `INSUFFICIENT_RESOURCES`, nothing created.  
- Spawn fails with invalid unit code or missing definition: `INVALID_UNIT`.  
- Spawn blocked when all adjacent tiles (and city tile if allowed) are occupied/water: returns `SPAWN_BLOCKED`, action flag remains false.  
- Turn ownership/turn-in-progress guards: attempting spawn when not player turn or while `TurnInProgress` true yields correct errors and clears guard.  
- Turn advance resets city action flag: after `AdvanceTurnAsync`, cities for next participant have `HasActedThisTurn = false`.

## 7. Frontend unit test scenarios
- City modal shows spawn options and disables Confirm until a unit type is selected.  
- Confirm button disabled when resources are insufficient or `hasActed` is true; enabled otherwise.  
- `useSpawnUnit` mutation updates React Query cache with returned game state and propagates errors through `useGameErrorHandler`.  
- Interaction priority: first click selects unit, second click opens city modal; spawning from modal does not break selection state.  
- After a successful spawn, modal closes (or shows success) and actions remain disabled according to updated `hasActed`.  
- Error toast shown when spawn blocked; previous state restored if mutation fails.

## 8. e2e test scenarios
- Start a game with a city having ≥10 iron: open city modal, select Warrior, Confirm; verify new unit appears on nearest free adjacent tile and other city actions are disabled for that turn.  
- Attempt to spawn twice in the same turn: first succeeds, second shows disabled UI or server error; city action stays blocked.  
- Spawn with insufficient resources: Confirm remains disabled or request rejected; resource counts unchanged.  
- Spawn when all adjacent tiles are occupied: server returns `SPAWN_BLOCKED`, UI surfaces error, no resources spent.  
- After ending the turn, actions re-enable for that city in the next turn and another spawn is possible if resources allow.
