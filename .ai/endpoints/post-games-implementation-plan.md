# API Endpoint Implementation Plan: POST /games

## 1. Endpoint Overview
Creates a new game for the authenticated user on a fixed map, initializes participants (human + AI), seeds starting cities/units, and returns the initial `GameState`.

## 2. Request Details
- HTTP Method: POST
- URL Pattern: /games
- Parameters:
  - Required: none
  - Optional: Header `X-Tenx-Idempotency-Key` (string)
- Request Body: `{ mapCode?: string, settings?: object, displayName?: string }`

## 3. Used Types
- Command Model: `CreateGameCommand`
- Response DTO: `GameCreatedResponse` (contains `GameStateDto`)

## 4. Response Details
- 201 Created: `{ id: number, state: GameState }`
- 401 Unauthorized: if not signed in
- 409 Conflict: `{ code: "GAME_LIMIT_REACHED" }` if per-user limit enforced
- 422 Unprocessable Entity: `{ code: "MAP_SCHEMA_MISMATCH" }` if map schema invalid
- 400 Bad Request: `{ code: "INVALID_INPUT" }` for invalid payload
- 500 Server Error on unhandled exceptions

## 5. Data Flow
- Middleware sets `SET LOCAL app.user_id` within transaction.
- Controller validates anti-forgery token and model-binds to `CreateGameCommand`.
- Service `IGameService.CreateGameAsync(command, idempotencyKey)`:
  - Validate map exists by code (or default map) and schema accepted.
  - Create `Game` row with `rng_seed`, `rng_version`, `map_schema_version`, `status='active'`.
  - Create `Participants`: human (userId, displayName from request or default "Player") and AI (generated name like "Charlemagne", "Cyrus the Great", etc.).
  - Seed deterministic starting `Cities` and `Units` for both sides.
  - Set `active_participant_id` to human; `turn_no=1`, `turn_in_progress=false`.
  - Persist in a single transaction.
  - Build and return `GameStateDto` via `IGameStateService.BuildAsync(gameId)`.
- Idempotency: if `X-Tenx-Idempotency-Key` present, use `IIdempotencyStore` to dedupe and return original response.

## 6. Security Considerations
- Authentication required; `Authenticated` policy.
- CSRF protection: require anti-forgery token header.
- RLS active via session var.
- Rate limit: 60 req/min per identity; apply stricter per-route if needed.

## 7. Error Handling
- 422 for map schema mismatch; 409 for user game limit; 400 for invalid payload.
- Log with `ILogger` (`Games.CreateFailed`) including `userId`, `mapCode`, correlation id.
- No error table; structured logging only.

## 8. Performance Considerations
- Single transaction for all inserts.
- Use batched inserts where possible.
- Keep AI seeding deterministic and O(1) per city/unit.

## 9. Implementation Steps
1. Define anti-forgery for POST and bind `CreateGameCommand` from body.
2. Implement `IIdempotencyStore` (e.g., DB or distributed cache) keyed by user+route+`Idempotency-Key`.
3. Implement `IGameService.CreateGameAsync` with transactional EF Core.
4. Implement seeding helpers for cities/units.
5. Implement `IGameStateService.BuildAsync(gameId)` to construct `GameStateDto`.
6. Return 201 with `Location: /games/{id}` and body `GameCreatedResponse`.
7. Swagger docs and example payloads; tests for idempotency and validation.

