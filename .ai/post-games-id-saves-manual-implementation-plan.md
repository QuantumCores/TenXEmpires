# API Endpoint Implementation Plan: POST /games/{id}/saves/manual

## 1. Endpoint Overview
Creates or overwrites a manual save in a slot (1..3) for the specified game and current turn, returning save metadata.

## 2. Request Details
- HTTP Method: POST
- URL Pattern: /games/{id}/saves/manual
- Parameters:
  - Required: `id` (long) path parameter
  - Optional: Header `Idempotency-Key` (string)
- Request Body: `CreateManualSaveCommand` → `{ slot: number (1..3), name: string }`

## 3. Used Types
- Command Model: `CreateManualSaveCommand`
- Response DTO: `SaveCreatedDto`

## 4. Response Details
- 201 Created: `SaveCreatedDto`
- 400 Bad Request: invalid slot/name
- 401 Unauthorized
- 404 Not Found: game not accessible
- 409 Conflict: `SAVE_CONFLICT` if unique constraint conflict not resolved as upsert
- 500 Server Error

## 5. Data Flow
- RLS session var set; begin transaction.
- Validate anti-forgery and model (slot range, non-empty name).
- Service `ISaveService.CreateManualAsync(gameId, command, idempotencyKey)`:
  - Validate game ownership via RLS; get current turn and state snapshot.
  - Upsert manual save in slot (`unique (user_id, game_id, slot) where kind='manual'`).
  - Persist snapshot fields: `turn_no`, `active_participant_id`, `schema_version`, `map_code`, `state`.
  - Return `SaveCreatedDto`.

## 6. Security Considerations
- Authentication and anti-forgery required.
- RLS enforces ownership.

## 7. Error Handling
- 400 for invalid slot/name; 404 for inaccessible game; 409 `SAVE_CONFLICT` if upsert strategy not used.
- Log with `ILogger` (`Saves.CreateManualFailed`).

## 8. Performance Considerations
- Snapshot generation should reuse `IGameStateService` and serialize efficiently.
- Transactional upsert to avoid race conditions.

## 9. Implementation Steps
1. Add controller action `POST /games/{id}/saves/manual` binding `CreateManualSaveCommand`.
2. Implement `ISaveService.CreateManualAsync` with transactional upsert behavior.
3. Return 201 with `Location: /games/{id}/saves` and body `SaveCreatedDto`.
4. Swagger docs and tests for slot range and overwrite behavior.

