# API Endpoint Implementation Plan: POST /games/{id}/end-turn

## 1. Endpoint Overview
Ends the active participant’s turn, triggers end-of-turn systems (regen, harvest, production), commits the turn record, creates an autosave, and advances to the next participant (including AI execution). Returns updated `GameState` with `turnSummary` and `autosaveId`.

## 2. Request Details
- HTTP Method: POST
- URL Pattern: /games/{id}/end-turn
- Parameters:
  - Required: `id` (long) path parameter
  - Optional: Header `Idempotency-Key` (string)
- Request Body: none or `EndTurnCommand` (empty)

## 3. Used Types
- Command Model: `EndTurnCommand`
- Response DTO: `EndTurnResponse` (contains `GameStateDto`, `TurnSummary`, `AutosaveId`)

## 4. Response Details
- 200 OK: `EndTurnResponse`
- 401 Unauthorized
- 409 Conflict: `NOT_PLAYER_TURN`, `TURN_IN_PROGRESS`
- 500 Server Error

## 5. Data Flow
- RLS session var set; begin transaction and set `turn_in_progress=true` to guard.
- Validate it is the active participant’s turn and not already in progress.
- Apply end-of-turn systems per rules:
  - City regen (+4 normal, +2 under siege; cap at max).
  - Harvest tiles within radius; siege rules apply.
  - Auto-produce at most 1 unit/city/turn if resource thresholds met; delay if blocked.
- Commit `Turn` row with `turn_no`, `participant_id`, `committed_at`, `duration_ms`, `summary`.
- Create autosave (ring buffer of 5) and return its id.
- Advance `turn_no` and `active_participant_id`; reset `has_acted=false` for owner’s units.
- If next participant is AI: execute AI turn deterministically within 500ms; log `duration_ms`.
- Rebuild `GameStateDto` and return.
- Idempotency: ensure `Idempotency-Key` returns same outcome if retried.

## 6. Security Considerations
- Authentication and anti-forgery required.
- RLS enforced; transaction isolation to prevent double-commit.
- Rate limiting applies; consider stricter per-route.

## 7. Error Handling
- Map to codes: `NOT_PLAYER_TURN` (409), `TURN_IN_PROGRESS` (409), `AI_TIMEOUT` (500 with specific code if exceeded).
- Log with `ILogger` (`Turns.EndTurnFailed`) with `gameId`, `participantId`.

## 8. Performance Considerations
- Keep end-turn processing O(nCities + nUnits) with efficient queries.
- Minimize contention by scoping locks and using set-based updates where possible.
- AI execution budget under 500ms; fallback to simplified AI if exceeded.

## 9. Implementation Steps
1. Add controller action `POST /games/{id}/end-turn` (bind empty `EndTurnCommand` if desired).
2. Implement `ITurnService.EndTurnAsync(gameId, idempotencyKey)` encapsulating all steps.
3. Implement autosave ring buffer operations in `ISaveService`.
4. Rebuild state and return `EndTurnResponse`.
5. Add Swagger docs and comprehensive tests for turn rules and autosave behavior.

