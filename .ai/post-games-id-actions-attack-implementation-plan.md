# API Endpoint Implementation Plan: POST /games/{id}/actions/attack

## 1. Endpoint Overview
Executes combat between an attacking unit and a target unit using deterministic damage rules. Ranged units never receive counterattacks. Returns updated `GameState`.

## 2. Request Details
- HTTP Method: POST
- URL Pattern: /games/{id}/actions/attack
- Parameters:
  - Required: `id` (long) path parameter
  - Optional: Header `Idempotency-Key` (string)
- Request Body: `AttackUnitCommand` → `{ attackerUnitId: number, targetUnitId: number }`

## 3. Used Types
- Command Model: `AttackUnitCommand`
- Response DTO: `ActionStateResponse`

## 4. Response Details
- 200 OK: `{ state: GameState }`
- 400 Bad Request: invalid payload
- 401 Unauthorized
- 409 Conflict: `NOT_PLAYER_TURN`, `NO_ACTIONS_LEFT`
- 422 Unprocessable Entity: `OUT_OF_RANGE`, `INVALID_TARGET`
- 500 Server Error

## 5. Data Flow
- RLS session var set; begin transaction.
- Controller validates anti-forgery and model.
- Service `IActionService.AttackAsync(gameId, command, idempotencyKey)`:
  - Validate player’s turn and `turn_in_progress=false`; set guard true.
  - Load attacker and target units with ownership, types (ranged/melee), positions.
  - Validate target is enemy, in range per unit definition (ranged: [min,max]; melee: adjacent), LoS rules as defined.
  - Ensure attacker hasn’t acted; compute damage with provided formula; apply rounding; min 1.
  - Apply attacker damage; if defender survives and is eligible (melee vs melee), apply counterattack.
  - Remove destroyed units; if city HP interactions exist, handle via separate city attack rules (if applicable).
  - Set attacker `has_acted=true`; persist changes and guard reset.
  - Rebuild `GameStateDto` and return.
  - Idempotency: return cached result if duplicate.

## 6. Security Considerations
- Authentication and anti-forgery required.
- RLS scoping within transaction.
- Concurrency: `turn_in_progress` guard and transaction isolation.

## 7. Error Handling
- Map to codes: `NOT_PLAYER_TURN` (409), `OUT_OF_RANGE` (422), `INVALID_TARGET` (422), `NO_ACTIONS_LEFT` (409).
- Log with `ILogger` (`Actions.AttackFailed`) including `gameId`, `attackerUnitId`, `targetUnitId`.

## 8. Performance Considerations
- Minimal queries with joins for type and tile data.
- Keep updates targeted; use FK relationships without loading full graph.

## 9. Implementation Steps
1. Add controller action `POST /games/{id}/actions/attack` binding `AttackUnitCommand`.
2. Implement `IActionService.AttackAsync` with validation and combat resolution.
3. Ensure deterministic formula implementation with unit tests.
4. Add idempotency via `IIdempotencyStore` (`game:{id}:attack:{key}`).
5. Rebuild state and return `ActionStateResponse`.
6. Swagger docs and tests for edge cases (range boundaries, counterattack conditions).

