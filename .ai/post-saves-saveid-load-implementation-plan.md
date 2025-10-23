# API Endpoint Implementation Plan: POST /saves/{saveId}/load

## 1. Endpoint Overview
Loads a save into its game, replacing current state with the saved snapshot, subject to schema/version compatibility checks. Returns updated `GameState` and game id.

## 2. Request Details
- HTTP Method: POST
- URL Pattern: /saves/{saveId}/load
- Parameters:
  - Required: `saveId` (long)
  - Optional: Header `Idempotency-Key` (string)
- Request Body: `LoadSaveCommand` (empty)

## 3. Used Types
- Command Model: `LoadSaveCommand`
- Response DTO: `LoadSaveResponse` (contains `GameId`, `GameStateDto`)

## 4. Response Details
- 200 OK: `LoadSaveResponse`
- 401 Unauthorized
- 403 Forbidden: when save does not belong to user via game ownership (RLS)
- 404 Not Found: save not found
- 422 Unprocessable Entity: `{ code: "SCHEMA_MISMATCH" }`
- 500 Server Error

## 5. Data Flow
- RLS session var set; begin transaction.
- Validate anti-forgery.
- Service `ISaveService.LoadAsync(saveId, idempotencyKey)`:
  - Load `Save` and owning `Game` with RLS; validate ownership.
  - Validate `schema_version` compatibility; reject with 422 if not compatible.
  - Replace game state (units, cities, resources, etc.) with snapshot from `state` JSON; ensure constraints (1UPT) hold.
  - Update `turn_no`, `active_participant_id`, map/code if needed.
  - Rebuild `GameStateDto` and return `LoadSaveResponse`.

## 6. Security Considerations
- Authentication and anti-forgery required.
- RLS ensures ownership.

## 7. Error Handling
- 422 for schema mismatch; 404 for save not found; 403 for ownership conflicts if RLS indicates forbidden.
- Log with `ILogger` (`Saves.LoadFailed`).

## 8. Performance Considerations
- Snapshot application should use set-based deletes/inserts where efficient.
- Validate integrity with minimal queries; use indexes.

## 9. Implementation Steps
1. Add controller action `POST /saves/{saveId}/load` binding `LoadSaveCommand` (empty body).
2. Implement `ISaveService.LoadAsync(long saveId, string? idempotencyKey)` with transactional replace logic.
3. Validate schema; return 422 on mismatch.
4. Return `LoadSaveResponse` with new state.
5. Swagger docs and tests including schema mismatch and ownership.

