# F-001 / US-103 Manual Territory Expansion Implementation Guide

## 1. Client-side Step-by-step Implementation Plan

### 1.1 Types & API Surface

- **Extend `tenxempires.client/src/types/game.ts`** with:
  - `ExpandTerritoryCommand { cityId: number; targetTileId: number; }` command type
  - Add expansion-related constants: `TERRITORY_BASE_COST = 20`, `TERRITORY_COST_PER_TILE = 10`, `INITIAL_CITY_TILES = 7`

- **Add API function** in `src/api/games.ts`:
  - `expandTerritory(gameId: number, command: ExpandTerritoryCommand, idempotencyKey: string)` targeting `POST /api/games/{gameId}/actions/city/expand` with `Idempotency-Key` header

### 1.2 State Management

- **Extend `useGameMapStore`** (or create dedicated expansion store) to track expansion mode:
  ```typescript
  interface ExpansionModeState {
    active: boolean
    cityId?: number
    validTileIds?: number[]
  }
  ```
  - Add actions: `enterExpansionMode(cityId: number, validTileIds: number[])`, `exitExpansionMode()`, `clearExpansionMode()`

- **Extend `useUiStore`** to track if expansion modal return is pending (so ESC/right-click returns to modal instead of fully canceling)

### 1.3 React Query Hooks

- **Add `useExpandTerritory(gameId)` mutation** in `src/features/game/useGameQueries.ts`:
  - Follows the same pattern as `useSpawnUnit`
  - Calls `expandTerritory` API
  - Routes errors through `useGameErrorHandler`
  - Caches optimistic previous state
  - Updates game state on success
  - Clears expansion mode on success/failure

### 1.4 Utility Functions

- **Add expansion validation helpers** in `src/features/game/expansionUtils.ts`:
  - `calculateExpansionCost(controlledTilesCount: number): number` - implements cost formula: `BaseCost + ((ControlledTilesCount - InitialTilesCount) * 10)`
  - `getValidExpansionTiles(cityId: number, gameState: GameStateDto, mapTiles: MapTileDto[]): number[]` - returns tile IDs that are valid expansion targets:
    1. Get all tiles currently owned by the city from `gameState.cityTiles`
    2. For each owned tile, find adjacent tiles using hex neighbor logic
    3. Filter out tiles that are: already owned by this city, owned by enemy city, occupied by enemy unit, water/ocean terrain
    4. Return deduplicated list of valid tile IDs

### 1.5 CityModal Updates

- **Update `src/components/modals/CityModal.tsx`**:
  - Add "Territory" section below Buildings section with:
    - Current territory size display (worked tiles count)
    - Expansion cost calculation display: `BaseCost (20) + (ExtraTiles × 10) = TotalCost Wheat`
    - "Expand Territory" button with wheat icon
  - Disable "Expand Territory" when:
    - City has already acted (`hasActed === true`)
    - Insufficient wheat resources
    - Expansion mutation is pending
  - On "Expand Territory" click:
    1. Calculate valid expansion tiles
    2. Call `enterExpansionMode(cityId, validTileIds)` from store
    3. Close the modal via `onRequestClose()`
  - Add keyboard handler to prevent "E" (End Turn) while modal is open (already exists from US-100)

### 1.6 MapCanvasStack Updates

- **Extend `src/components/game/MapCanvasStack.tsx`**:
  - Subscribe to expansion mode state from store
  - **Overlay rendering** in `renderOverlay()`:
    - When `expansionMode.active === true`, draw strong blue border (`#3b82f6`, line width 4) around valid expansion tiles
    - Add subtle blue fill (`rgba(59, 130, 246, 0.15)`) to valid tiles for better visibility
  - **Click handling** in `handleClick()`:
    - If expansion mode is active and clicked tile is in `validTileIds`:
      - Execute `expandTerritoryMutation.mutate({ cityId, targetTileId })`
      - On success: clear expansion mode, update state shows new tile ownership
    - If expansion mode is active and clicked tile is NOT in `validTileIds`:
      - Optionally show toast "Invalid expansion target"
  - **Context menu (right-click) handling** in `handleContextMenu()`:
    - If expansion mode is active:
      - Cancel expansion mode
      - Reopen city modal with the same city selected
  - **Keyboard handling**:
    - Add ESC key listener (useEffect with keydown event)
    - If expansion mode is active and ESC pressed:
      - Cancel expansion mode
      - Reopen city modal with the same city selected

### 1.7 Visual Feedback

- **Toast notifications** for:
  - Expansion success: "Territory expanded successfully"
  - Expansion blocked: Show specific error from server
- **Loading state**: Disable valid tile clicks while mutation is pending
- **Cursor change**: Show pointer cursor on valid expansion tiles when in expansion mode

### 1.8 Assets

- Reuse existing wheat icon from `/images/game/resources/wheat.png`
- Use existing territory highlight styling patterns from city selection overlay

---

## 2. Server-side Step-by-step Implementation Plan

### 2.1 Configuration

- **Extend `TenXEmpires.Server.Domain/Configuration/GameSettings.cs`**:
  ```csharp
  /// <summary>
  /// Base cost in wheat for territory expansion.
  /// </summary>
  public int TerritoryExpansionBaseCost { get; set; } = 20;

  /// <summary>
  /// Additional wheat cost per extra tile beyond initial territory.
  /// </summary>
  public int TerritoryExpansionCostPerTile { get; set; } = 10;

  /// <summary>
  /// Number of tiles a city starts with (center + 6 neighbors).
  /// </summary>
  public int InitialCityTerritorySize { get; set; } = 7;
  ```

- **Update `TenXEmpires.Server/appsettings.json`**:
  ```json
  "GameSettings": {
    ...
    "TerritoryExpansionBaseCost": 20,
    "TerritoryExpansionCostPerTile": 10,
    "InitialCityTerritorySize": 7
  }
  ```

### 2.2 Contracts

- **Add to `TenXEmpires.Server.Domain/DataContracts/Commands.cs`**:
  ```csharp
  /// <summary>
  /// Command to expand a city's territory to an adjacent tile.
  /// </summary>
  public sealed record ExpandTerritoryCommand(long CityId, long TargetTileId);
  ```

### 2.3 IActionService Interface

- **Extend `TenXEmpires.Server.Domain/Services/IActionService.cs`**:
  ```csharp
  /// <summary>
  /// Expands a city's territory to an adjacent tile by spending wheat.
  /// </summary>
  /// <param name="userId">The authenticated user's ID.</param>
  /// <param name="gameId">The game ID.</param>
  /// <param name="command">The expand command with city ID and target tile ID.</param>
  /// <param name="idempotencyKey">Optional idempotency key for safe retries.</param>
  /// <param name="cancellationToken">Cancellation token.</param>
  /// <returns>The updated game state after expansion.</returns>
  Task<ActionStateResponse> ExpandTerritoryAsync(
      Guid userId,
      long gameId,
      ExpandTerritoryCommand command,
      string? idempotencyKey,
      CancellationToken cancellationToken = default);
  ```

### 2.4 ActionService Implementation

- **Implement `ExpandTerritoryAsync` in `TenXEmpires.Server.Infrastructure/Services/ActionService.cs`**:

  **Step 1: Idempotency Check**
  - If `idempotencyKey` provided, check `IIdempotencyStore` for cached response
  - Return cached response if found, throw conflict if payload differs

  **Step 2: Load and Validate Game/Participant**
  - Load game with map and active participant
  - Verify game belongs to user (`game.UserId == userId`)
  - Verify active participant is human and matches user
  - Verify `TurnInProgress == false`
  - Set `TurnInProgress = true` as guard

  **Step 3: Load and Validate City**
  - Load city with resources and city tiles
  - Verify city exists and belongs to active participant
  - Verify `city.HasActedThisTurn == false`

  **Step 4: Load and Validate Target Tile**
  - Load target tile from map
  - Verify tile exists and is within map bounds
  - Verify tile is NOT water/ocean terrain

  **Step 5: Validate Adjacency**
  - Get all tiles owned by the city (from CityTiles)
  - Convert owned tiles to cube coordinates
  - Get all adjacent tiles of owned tiles using `HexagonalGrid.GetHexNeighbours`
  - Verify target tile is in the set of adjacent tiles (not already owned)

  **Step 6: Validate Ownership**
  - Check if target tile is owned by any other city
  - If owned by enemy city → reject with `TILE_OWNED_BY_ENEMY`

  **Step 7: Validate Unit Occupation**
  - Check if an enemy unit occupies the target tile
  - If enemy unit present → reject with `TILE_OCCUPIED_BY_ENEMY`

  **Step 8: Calculate and Validate Cost**
  - Get current controlled tiles count from city tiles
  - Calculate cost: `BaseCost + ((ControlledTilesCount - InitialTilesCount) * CostPerTile)`
  - Ensure cost is never negative (min 0 for extra tiles calculation)
  - Load wheat resource for city
  - Verify wheat amount >= cost → else reject with `INSUFFICIENT_RESOURCES`

  **Step 9: Execute Expansion**
  - Deduct wheat cost from city resources
  - Create new `CityTile` entity linking city to target tile
  - Set `city.HasActedThisTurn = true`
  - Update timestamps

  **Step 10: Persist and Return**
  - Save changes within transaction
  - Clear `TurnInProgress` guard
  - Cache response if idempotency key provided
  - Return `ActionStateResponse` with updated game state

  **Error Handling**:
  - Use try-finally to ensure `TurnInProgress` is cleared
  - Log all errors with correlation ID
  - Throw specific exceptions matching controller error handling

### 2.5 Controller Endpoint

- **Add to `TenXEmpires.Server/Controllers/GamesController.cs`**:
  ```csharp
  /// <summary>
  /// Expands a city's territory to an adjacent tile by spending wheat.
  /// </summary>
  /// <param name="id">The game ID.</param>
  /// <param name="command">The expand command containing city ID and target tile ID.</param>
  /// <param name="cancellationToken">Cancellation token.</param>
  /// <returns>The updated game state after expansion.</returns>
  /// <response code="200">Territory expanded successfully.</response>
  /// <response code="400">Bad Request - Invalid input.</response>
  /// <response code="401">Unauthorized - Not authenticated.</response>
  /// <response code="404">Not Found - Game or city not found.</response>
  /// <response code="409">Conflict - Various action conflicts.</response>
  /// <response code="422">Unprocessable Entity - Invalid tile target.</response>
  /// <response code="500">Internal server error occurred.</response>
  [HttpPost("{id:long}/actions/city/expand", Name = "ExpandTerritory")]
  [ProducesResponseType(typeof(ActionStateResponse), StatusCodes.Status200OK)]
  [ProducesResponseType(StatusCodes.Status400BadRequest)]
  [ProducesResponseType(StatusCodes.Status401Unauthorized)]
  [ProducesResponseType(StatusCodes.Status404NotFound)]
  [ProducesResponseType(StatusCodes.Status409Conflict)]
  [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
  [ProducesResponseType(StatusCodes.Status500InternalServerError)]
  public async Task<ActionResult<ActionStateResponse>> ExpandTerritory(
      long id,
      [FromBody] ExpandTerritoryCommand command,
      CancellationToken cancellationToken = default)
  ```
  - Handle exceptions matching the spawn endpoint pattern
  - Map specific exceptions to appropriate HTTP status codes

### 2.6 Logging

- Add Serilog events for:
  - Expansion attempts (cityId, targetTileId, userId)
  - Successful expansions (include cost paid, new tile count)
  - Failed expansions (reason code, details)

---

## 3. Database Changes

- **No new schema required** for US-103
- Existing tables used:
  - `app.city_tiles` - stores city-to-tile ownership links
  - `app.city_resources` - stores city resources including wheat
  - `app.cities` - has `has_acted_this_turn` flag
- Verify indexes exist on `city_tiles` for efficient adjacency queries

---

## 4. Error Handling

| # | Error Code | HTTP Status | Condition |
|---|------------|-------------|-----------|
| 1 | `UNAUTHORIZED` | 401/403 | User doesn't own the game or not authenticated |
| 2 | `NOT_PLAYER_TURN` | 409 | Not the player's turn or active participant not human |
| 3 | `TURN_IN_PROGRESS` | 409 | Another action is already being processed |
| 4 | `CITY_NOT_FOUND_OR_OWNED` | 404/403 | City doesn't exist or not owned by player |
| 5 | `CITY_ALREADY_ACTED` | 409 | City has already performed an action this turn |
| 6 | `INVALID_TILE` | 400/404 | Target tile doesn't exist in the map |
| 7 | `TILE_NOT_ADJACENT` | 422 | Target tile is not adjacent to any owned territory tile |
| 8 | `TILE_OWNED_BY_ENEMY` | 409 | Target tile is already owned by an enemy city |
| 9 | `TILE_OCCUPIED_BY_ENEMY` | 409 | Target tile has an enemy unit occupying it |
| 10 | `INVALID_TERRAIN` | 422 | Target tile is water/ocean (cannot be claimed) |
| 11 | `INSUFFICIENT_RESOURCES` | 409 | Not enough wheat to pay expansion cost (include required/current amounts) |
| 12 | `IDEMPOTENCY_CONFLICT` | 409 | Same idempotency key used with different command payload |
| 13 | `INTERNAL_ERROR` | 500 | Database/transaction failure (include correlation ID for debugging) |

---

## 5. Security Considerations

- **Authentication & Authorization**:
  - Enforce `[Authorize]` attribute on the expand endpoint
  - Verify game ownership (`game.UserId == userId`)
  - Verify active participant matches the requesting user

- **Server-Authoritative Validation**:
  - Calculate expansion cost server-side; ignore any client-provided cost
  - Validate tile adjacency using server-side hex grid logic
  - Check tile ownership against database state, not client claims
  - Verify enemy unit occupation from current game state

- **Anti-Cheat Measures**:
  - Cannot expand to non-adjacent tiles regardless of client UI
  - Cannot expand if insufficient wheat, even if client bypasses check
  - Cannot expand after city has acted, enforced by `HasActedThisTurn` flag

- **Idempotency**:
  - Accept `Idempotency-Key` header for POST requests
  - Cache successful responses keyed by game + idempotency key
  - Return cached response on replay; conflict if payload differs
  - Prevents double-spend on network retries

- **Input Validation**:
  - Validate `CityId` and `TargetTileId` are positive non-zero longs
  - Sanitize logging to avoid leaking PII
  - Log correlation IDs for audit trail

- **Rate Limiting**:
  - Existing `[EnableRateLimiting("AuthenticatedApi")]` applies
  - Consider additional per-action rate limits if abuse detected

---

## 6. Backend Unit Test Scenarios

### Positive Cases
1. **Expansion succeeds with sufficient wheat**: 
   - City with 7 tiles, 30 wheat
   - Expand to valid adjacent tile
   - Verify: wheat deducted (20), new CityTile created, `HasActedThisTurn = true`, returned state includes new tile

2. **Expansion cost calculation is correct**:
   - City with 10 tiles (3 extra), 60 wheat
   - Expected cost: 20 + (3 × 10) = 50
   - Verify: exactly 50 wheat deducted

3. **Can expand to tile adjacent to any owned tile**:
   - City owns tiles A, B, C
   - Target tile is adjacent to C but not A or B
   - Verify: expansion succeeds

### Negative Cases - Turn/Action Guards
4. **Expansion fails when city already acted**:
   - Set `HasActedThisTurn = true`
   - Attempt expand → `CITY_ALREADY_ACTED`
   - Verify: no resource change, no tile added

5. **Expansion fails when not player's turn**:
   - Active participant is AI
   - Attempt expand → `NOT_PLAYER_TURN`

6. **Expansion fails when turn in progress**:
   - Set `TurnInProgress = true`
   - Attempt expand → `TURN_IN_PROGRESS`
   - Verify: guard is properly cleared after failure

### Negative Cases - Tile Validation
7. **Expansion fails for non-adjacent tile**:
   - Target tile 3 hexes away from all owned tiles
   - → `TILE_NOT_ADJACENT`

8. **Expansion fails for tile owned by enemy city**:
   - Target tile linked to enemy city
   - → `TILE_OWNED_BY_ENEMY`

9. **Expansion fails for tile with enemy unit**:
   - Enemy unit positioned on target tile
   - → `TILE_OCCUPIED_BY_ENEMY`

10. **Expansion fails for water/ocean tile**:
    - Target tile terrain is "water" or "ocean"
    - → `INVALID_TERRAIN`

### Negative Cases - Resources
11. **Expansion fails with insufficient wheat**:
    - City with 15 wheat, cost is 20
    - → `INSUFFICIENT_RESOURCES` with required/current amounts

12. **Expansion fails with zero wheat**:
    - City with 0 wheat
    - → `INSUFFICIENT_RESOURCES`

### Integration/State Tests
13. **Turn advance resets city action flag**:
    - After `AdvanceTurnAsync`, `HasActedThisTurn = false`
    - New expansion possible next turn

14. **Idempotency returns cached response**:
    - Successful expand with key "abc123"
    - Replay with same key → same response, no double expansion

15. **Idempotency conflict on payload mismatch**:
    - Expand tile 1 with key "abc123"
    - Try expand tile 2 with same key → `IDEMPOTENCY_CONFLICT`

---

## 7. Frontend Unit Test Scenarios

### CityModal Tests
1. **Expansion section displays correct cost calculation**:
   - City with 9 tiles → shows "20 + (2 × 10) = 40 Wheat"

2. **Expand button disabled when city has acted**:
   - `hasActed = true` → button disabled with visual indication

3. **Expand button disabled when insufficient wheat**:
   - Wheat < calculated cost → button disabled, shows "Need more wheat"

4. **Expand button enabled when resources sufficient and city hasn't acted**:
   - Wheat >= cost, `hasActed = false` → button enabled

5. **Clicking Expand enters expansion mode**:
   - Click Expand → `enterExpansionMode` called with cityId and valid tile IDs
   - Modal closes via `onRequestClose`

### MapCanvasStack Tests
6. **Valid expansion tiles highlighted in expansion mode**:
   - `expansionMode.active = true` with valid tile IDs
   - Verify: overlay draws blue borders on those tiles

7. **Click on valid tile triggers expansion**:
   - In expansion mode, click valid tile
   - Verify: `useExpandTerritory` mutation called with correct params

8. **Click on invalid tile shows error/ignored**:
   - In expansion mode, click tile not in valid list
   - Verify: no mutation called, optional toast shown

9. **ESC key cancels expansion mode and reopens modal**:
   - In expansion mode, press ESC
   - Verify: `exitExpansionMode` called, modal reopens with same city

10. **Right-click cancels expansion mode and reopens modal**:
    - In expansion mode, right-click
    - Verify: `exitExpansionMode` called, modal reopens with same city

### useExpandTerritory Hook Tests
11. **Mutation updates React Query cache on success**:
    - Successful expansion → game state cache updated

12. **Mutation clears expansion mode on success**:
    - After success → `exitExpansionMode` called

13. **Mutation shows error toast on failure**:
    - API returns error → toast displayed via `useGameErrorHandler`

14. **Mutation restores previous state on error**:
    - On error → cache restored to snapshot

### Expansion Utilities Tests
15. **calculateExpansionCost returns correct values**:
    - 7 tiles → 20, 8 tiles → 30, 10 tiles → 50

16. **getValidExpansionTiles filters correctly**:
    - Excludes already owned tiles
    - Excludes enemy-owned tiles
    - Excludes tiles with enemy units
    - Excludes water/ocean tiles
    - Includes valid adjacent land tiles

---

## 8. E2E Test Scenarios

### Happy Path
1. **Full expansion flow**:
   - Start game with city having ≥20 wheat
   - Open city modal (click city twice)
   - Click "Expand Territory" button
   - Verify: modal closes, map shows blue-highlighted valid tiles
   - Click a valid highlighted tile
   - Verify: tile now shows as owned by city (white overlay)
   - Verify: city actions disabled for rest of turn
   - Verify: wheat reduced by correct amount

2. **Expansion after turn advance**:
   - Expand territory in turn 1
   - End turn, complete AI turn, return to player turn
   - Open same city modal
   - Verify: Expand button enabled again
   - Successfully expand to another tile

### Cancellation
3. **Cancel expansion with ESC**:
   - Enter expansion mode
   - Press ESC
   - Verify: highlights disappear, city modal reopens
   - Verify: no resources spent, no tile claimed

4. **Cancel expansion with right-click**:
   - Enter expansion mode
   - Right-click anywhere
   - Verify: same as ESC cancellation

### Validation
5. **Cannot expand when city has acted**:
   - Spawn a unit from city (uses action)
   - Verify: Expand button is disabled in modal

6. **Cannot expand with insufficient wheat**:
   - City with <20 wheat
   - Verify: Expand button disabled, shows resource requirement

7. **Expansion mode only shows valid tiles**:
   - City adjacent to enemy territory and water
   - Enter expansion mode
   - Verify: enemy tiles NOT highlighted
   - Verify: water tiles NOT highlighted
   - Verify: only valid land tiles highlighted

8. **Cannot click non-highlighted tiles in expansion mode**:
   - Enter expansion mode
   - Click on enemy tile or water tile
   - Verify: no expansion happens, mode remains active

### Edge Cases
9. **Expansion cost increases correctly**:
   - Expand once (cost 20)
   - Next turn, verify cost is now 30
   - Expand again (cost 30)
   - Next turn, verify cost is now 40

10. **Multiple cities can expand in same turn**:
    - Player has 2 cities with sufficient wheat
    - Expand from city A
    - Expand from city B
    - Verify: both expansions successful

