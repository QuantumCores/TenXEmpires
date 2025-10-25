# API Endpoint Implementation Plan: POST /games/{id}/actions/move

## 1. Endpoint Overview
Moves a unit along a valid path according to deterministic rules (A*, uniform cost), enforcing 1UPT and action limits. Returns updated `GameState`.

## 2. Request Details
- HTTP Method: POST
- URL Pattern: /games/{id}/actions/move
- Parameters:
  - Required: `id` (long) path parameter
  - Optional: Header `Idempotency-Key` (string)
- Request Body: `MoveUnitCommand` → `{ unitId: number, to: { row: number, col: number } }`

## 3. Used Types
- Command Model: `MoveUnitCommand`
- Response DTO: `ActionStateResponse` (contains `GameStateDto`)

## 4. Response Details
- 200 OK: `{ state: GameState }`
- 400 Bad Request: invalid payload
- 401 Unauthorized: not signed in
- 409 Conflict: `NOT_PLAYER_TURN`, `ONE_UNIT_PER_TILE`, `NO_ACTIONS_LEFT`
- 422 Unprocessable Entity: `ILLEGAL_MOVE`
- 500 Server Error on unhandled exceptions

## 5. Data Flow
- RLS session var set; begin serializable or repeatable-read transaction.
- Controller validates anti-forgery token and model.
- Service `IActionService.MoveAsync(gameId, command, idempotencyKey)`:
  - Load game with `active_participant_id`, `turn_in_progress`.
  - Validate it is the human user’s turn and not in-progress; set `turn_in_progress=true` as a guard.
  - Load unit (belongs to active participant), tile map reference, and occupancy state.
  - Compute path with A* (uniform cost 1), ensure reachable and within move points; reject if blocked.
  - Enforce 1UPT using unique `(game_id, tile_id)`; ensure destination unoccupied.
  - Update unit position and `has_acted=true`; persist.
  - Clear `turn_in_progress` and rebuild `GameStateDto` via `IGameStateService`.
  - Idempotency: return previous result if duplicate key.

## 6. Security Considerations
- Authentication required; anti-forgery token required.
- RLS ensures user actions are scoped to their game.
- Concurrency: use transactions and `turn_in_progress` guard to prevent race conditions.

## 7. Error Handling
- Map rule violations to codes: `NOT_PLAYER_TURN` (409), `ILLEGAL_MOVE` (422), `ONE_UNIT_PER_TILE` (409), `NO_ACTIONS_LEFT` (409).
- Log with `ILogger` (`Actions.MoveFailed`) including `gameId`, `unitId`.

## 8. Performance Considerations
- Efficient A* with early-exit; use grid bounds from map.
- Index on `(game_id, tile_id)` already enforces occupancy checks.
- Keep queries minimal and `FOR UPDATE` only when necessary.

## 9. Implementation Steps
1. Add controller action `POST /games/{id}/actions/move` binding `MoveUnitCommand`.
2. Implement `IActionService.MoveAsync` with transactional logic and RLS.
3. Implement A* pathfinding utility tested separately.
4. Enforce idempotency using `IIdempotencyStore` (`game:{id}:move:{key}`).
5. Rebuild state with `IGameStateService.BuildAsync` and return `ActionStateResponse`.
6. Add Swagger docs and comprehensive unit tests for rule enforcement.

