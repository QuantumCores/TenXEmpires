# REST API Plan

## 1. Resources

- Users — `auth.users`
- Maps — `app.maps`
- MapTiles — `app.map_tiles`
- Games — `app.games`
- Participants — `app.participants`
- UnitDefinitions — `app.unit_definitions`
- Units — `app.units`
- Cities — `app.cities`
- CityTiles — `app.city_tiles`
- CityResources — `app.city_resources`
- Saves — `app.saves`
- Turns — `app.turns`
- AnalyticsEvents — `app.analytics_events`
- GameState (projection) — read-only aggregate over `games` and children

Notes

- Read-mostly: `maps`, `map_tiles`, `unit_definitions` (exposed as GET only in MVP).
- RLS-enabled: `games`, `participants`, `units`, `cities`, `city_tiles`, `city_resources`, `saves`, `turns`.
- Non-RLS: `analytics_events` by design; `users` is managed by ASP.NET Identity.
- Determinism: gameplay and AI are server-authoritative. RNG fields: `games.rng_seed`, `games.rng_version`.

## 2. Endpoints

Conventions

- JSON only; `Content-Type: application/json`.
- Timestamps in ISO-8601 UTC.
- Idempotency: support `Idempotency-Key` header on creates and turn actions.
- Pagination: `page` (1-based), `pageSize` (default 20, max 100), `sort`, `order` (`asc|desc`).
- Errors: `{ code: string, message: string, details?: object }` payload with 4xx/5xx.
- CSRF: all state-changing endpoints require anti-forgery token (see Auth section).
- RLS: backend sets `SET LOCAL app.user_id = '<uuid>'` per request transaction.


### 2.1 Lookups (Read-Only)

- GET `/unit-definitions`
  - Returns game unit stats.
  - Query: none.
  - Response: `200 OK`, `{ items: Array<{ id: number, code: string, isRanged: boolean, attack: number, defence: number, rangeMin: number, rangeMax: number, movePoints: number, health: number }> }`.

- GET `/maps/{code}`
  - Map metadata.
  - Response: `200 OK`, `{ id: number, code: string, schemaVersion: number, width: number, height: number }`.
  - Errors: `404 MAP_NOT_FOUND`.

- GET `/maps/{code}/tiles`
  - Stream or page tiles for client rendering.
  - Query: `page`, `pageSize` (optional if streaming disabled).
  - Response: `200 OK`, `{ items: Array<{ id: number, row: number, col: number, terrain: string, resourceType?: string, resourceAmount: number }> }`.

### 2.2 Games

- GET `/games`
  - List current user’s games.
  - Query: `status?=active|finished`, `page`, `pageSize`, `sort?=startedAt|lastTurnAt|turnNo`, `order`.
  - Response: `200 OK`, `{ items: Array<{ id: number, status: string, turnNo: number, mapId: number, mapSchemaVersion: number, startedAt: string, finishedAt?: string, lastTurnAt?: string }>, page: number, pageSize: number, total?: number }`.

- POST `/games`
  - Start a new game on fixed map.
  - Request: `{ mapCode?: string, settings?: object }`
  - Response: `201 Created`, `{ id: number, state: GameState }`.
  - Side effects: create participants (human + AI), seed cities/units, set `activeParticipantId` to human.
  - Errors: `409 GAME_LIMIT_REACHED` (if any limit), `422 MAP_SCHEMA_MISMATCH`.

- GET `/games/{id}`
  - Game summary.
  - Response: `200 OK`, `{ id, userId, mapId, mapSchemaVersion, turnNo, activeParticipantId, turnInProgress, status, startedAt, finishedAt, lastTurnAt, settings }`.
  - Errors: `404 GAME_NOT_FOUND`, `403 FORBIDDEN` (RLS).

- DELETE `/games/{id}`
  - Delete a game and children.
  - Response: `204 No Content`.

- GET `/games/{id}/state`
  - Aggregate state projection for efficient client sync.
  - Response: `200 OK`, `GameState`:
    - `{ game: { id, turnNo, activeParticipantId, turnInProgress, status },
         map: { id, code, schemaVersion, width, height },
         participants: Array<{ id, gameId, kind: 'human'|'ai', userId?: string, displayName: string, isEliminated: boolean }>,
         units: Array<{ id, participantId, typeCode: string, hp: number, hasActed: boolean, tileId: number, row: number, col: number }>,
         cities: Array<{ id, participantId, hp: number, maxHp: number, tileId: number, row: number, col: number }>,
         cityTiles: Array<{ cityId: number, tileId: number }>,
         cityResources: Array<{ cityId: number, resourceType: string, amount: number }>,
         unitDefinitions: Array<...>,
         turnSummary?: object }`.

- GET `/games/{id}/turns`
  - List committed turns for timeline/history.
  - Query: `page`, `pageSize`, `sort?=turnNo|committedAt`, `order`.
  - Response: `200 OK`, `{ items: Array<{ id, turnNo, participantId, committedAt, durationMs?: number, summary?: object }> }`.

### 2.3 Actions & Turn Flow

- POST `/games/{id}/actions/move`
  - Move a unit along a valid path (A*, uniform cost, pass-through friendlies, 1UPT).
  - Request: `{ unitId: number, to: { row: number, col: number } }`
  - Response: `200 OK`, `{ state: GameState }`.
  - Errors: `409 NOT_PLAYER_TURN`, `422 ILLEGAL_MOVE`, `409 ONE_UNIT_PER_TILE`, `409 NO_ACTIONS_LEFT`.

- POST `/games/{id}/actions/attack`
  - Attack a target using deterministic formula; ranged never receive counterattack.
  - Request: `{ attackerUnitId: number, targetUnitId: number }`
  - Response: `200 OK`, `{ state: GameState }`.
  - Errors: `409 NOT_PLAYER_TURN`, `422 OUT_OF_RANGE`, `422 INVALID_TARGET`, `409 NO_ACTIONS_LEFT`.

- POST `/games/{id}/end-turn`
  - End the human turn; server applies city regen/harvest/production, autosave, then performs AI turn (< 500 ms), increments `turnNo`.
  - Request: `{}` (empty); idempotent via `Idempotency-Key`.
  - Response: `200 OK`, `{ state: GameState, turnSummary: object, autosaveId: number }`.
  - Errors: `409 TURN_IN_PROGRESS`, `409 NOT_PLAYER_TURN`, `503 AI_TIMEOUT`.

### 2.4 Saves

- GET `/games/{id}/saves`
  - List manual slots (1..3) and last 5 autosaves.
  - Response: `200 OK`, `{ manual: Array<{ id: number, slot: 1|2|3, turnNo: number, createdAt: string, name: string }>, autosaves: Array<{ id: number, turnNo: number, createdAt: string }> }`.

- POST `/games/{id}/saves/manual`
  - Create/overwrite manual save in a slot.
  - Request: `{ slot: 1|2|3, name: string }`
  - Response: `201 Created`, `{ id: number, slot: number, turnNo: number, createdAt: string, name: string }`.
  - Errors: `422 SAVE_SLOT_OUT_OF_RANGE`, `409 SAVE_CONFLICT` (concurrent write), `413 STATE_TOO_LARGE` (if applicable).
  - Notes: `name` is stored directly on the save record (`saves.name`); autogenerated for autosaves.

- DELETE `/games/{id}/saves/manual/{slot}`
  - Delete a manual save slot.
  - Response: `204 No Content`.
  - Errors: `404 SAVE_NOT_FOUND`.

- POST `/saves/{saveId}/load`
  - Load a save (manual or autosave) into its game (server replaces normalized state).
  - Request: `{}`
  - Response: `200 OK`, `{ gameId: number, state: GameState }`.
  - Errors: `422 SCHEMA_MISMATCH`, `404 SAVE_NOT_FOUND`, `409 TURN_IN_PROGRESS`.

### 2.6 Analytics

- POST `/analytics/batch`
  - Batch analytics per turn.
  - Request: `{ events: Array<{ eventType: 'game_start'|'turn_end'|'autosave'|'manual_save'|'manual_load'|'game_finish', gameId?: number, turnNo?: number, occurredAt?: string, clientRequestId?: string, payload?: object }> }`
  - Response: `202 Accepted`, `{ accepted: number }` (best-effort). Server sets `user_key` using salted hash and copies `game_id` to `game_key`.
  - Errors: `429 RATE_LIMIT_EXCEEDED`.

### 2.7 Auth & Session

- GET `/auth/csrf`
  - Issue/refresh anti-forgery token for SPA.
  - Response: `204 No Content` with `Set-Cookie: XSRF-TOKEN=<token>; Path=/; SameSite=Lax; Secure`.
  - Notes: Public endpoint; clients echo token via `X-XSRF-TOKEN` header on non-GET requests.

- GET `/auth/keepalive`
  - Refresh authenticated session (sliding expiration) without changing user state.
  - Response: `204 No Content`.
  - Errors: `401 UNAUTHORIZED` when not signed in.

## 3. Authentication and Authorization

- Mechanism: Cookie-based ASP.NET Identity authentication. Use `SameSite=Lax`, `Secure`, `HttpOnly` cookies. Enforce TLS.
- CSRF protection: Anti-forgery tokens for non-GET requests. For SPA, issue an `XSRF-TOKEN` cookie and require `X-XSRF-TOKEN` header.
- Authorization: Policy-based.
  - `Authenticated` for gameplay endpoints.
  - `Admin` (if present) for maintenance (not required in MVP).
- PostgreSQL RLS: On each request within a transaction, execute `SET LOCAL app.user_id = '<currentUserId>'` to activate RLS policies for `games` and child tables.
- CORS: Allow only configured origins; disallow credentials for public GETs; allow credentials for authenticated routes.
- Rate limiting: Global 60 req/min per client identity with `X-RateLimit-*` headers and `429` responses.

## 4. Validation and Business Logic

Validation Rules

- Identity & Session
  - Email/password format checks; lockout/backoff on repeated failures.
  - Idle timeout 30 min; session invalidated server-side.

- Game Creation
  - Map exists and `schemaVersion` accepted; return `422 MAP_SCHEMA_MISMATCH` if not.
  - Initialize deterministic starting positions (1 city, Warrior, Slinger for both sides).

- Turn System
  - Strict alternating turns using `activeParticipantId` and `turnInProgress` guard.
  - A unit may move or attack once per turn; reset on owner’s next turn.
  - End-turn triggers: autosave (ring buffer size 5), city regen/harvest/production, AI turn.

- Movement & Combat
  - Pathfinding with A* (uniform cost 1). Pass-through friendlies allowed.
  - Enforce 1UPT: disallow ending a move on occupied tile. Unique index `(game_id, tile_id)` ensures DB integrity.
  - Attack resolution: `DMG = Attack × (1 + (Attack - Defence)/Defence) × 0.5`, round half up, min 1.
  - Order: attacker hits first; if defender survives and is eligible, it counterattacks; ranged never receive counterattack.
  - Ties do not go to attacker. Validate target in range for ranged and melee rules.

- Cities & Economy
  - City regen: +4 HP/turn normally; +2 HP/turn under siege (enemy present within reach). Cap at `max_hp`.
  - Auto-harvest tiles within radius 2; under siege only non-occupied tiles contribute.
  - Auto-produce at most 1 unit/city/turn when thresholds met: Warrior=10 iron, Slinger=10 stone. If all adjacent tiles blocked, delay and include in summary.
  - Capture: when city HP ≤ 0, a melee unit ending turn on the city captures it; capturing last enemy city finishes game.

- Saves
  - Manual slots: 1..3; `CHECK` and partial unique index enforce constraints.
  - Autosave: ring buffer of 5 per game; oldest evicted on insertion of 6th.
  - Load schema gate: validate `schemaVersion`; reject incompatible saves with `422 SCHEMA_MISMATCH` and clear message.
  - Retention purge: saves older than 3 months eligible for background purge (analytics retained by design).

- Analytics
  - Batch 1 payload/turn recommended. Deduplicate via `clientRequestId` (partial unique index) when present.
  - Persist events: `event_type`, `occurred_at`, `game_key`, `user_key` (salted hash), optional `payload`.

Security & Performance

- Rate limiting at 60 req/min; compliant retries for idempotent endpoints only.
- Strict CORS; anti-forgery enforcement; HTTPS-only cookies; secure headers.
- Transactions wrap state-changing operations; RLS active within transaction.
- Deterministic AI execution under 500 ms; log `duration_ms` in `turns` and monitor.
- Caching: cache `unit-definitions`, `maps`, and `maps/{code}/tiles` with ETags/`Last-Modified`.

Error Codes (examples)

- `INVALID_INPUT`, `INVALID_CREDENTIALS`, `UNAUTHENTICATED`, `FORBIDDEN`, `RATE_LIMIT_EXCEEDED`.
- `GAME_NOT_FOUND`, `NOT_PLAYER_TURN`, `TURN_IN_PROGRESS`.
- `ILLEGAL_MOVE`, `ONE_UNIT_PER_TILE`, `NO_ACTIONS_LEFT`, `OUT_OF_RANGE`, `INVALID_TARGET`.
- `MAP_NOT_FOUND`, `MAP_SCHEMA_MISMATCH`, `SCHEMA_MISMATCH`.
- `SAVE_SLOT_OUT_OF_RANGE`, `SAVE_CONFLICT`, `SAVE_NOT_FOUND`.
- `AI_TIMEOUT`, `INTERNAL_ERROR`.

Request/Response Examples (abbreviated)

- POST `/games` request:
  - `{ "settings": { "difficulty": "normal" } }`
- POST `/games` response:
  - `{ "id": 123, "state": { "game": { "id": 123, "turnNo": 1, "activeParticipantId": 456, "turnInProgress": false, "status": "active" }, "participants": [...], "units": [...], "cities": [...] } }`

- POST `/games/{id}/actions/move` request:
  - `{ "unitId": 789, "to": { "row": 7, "col": 12 } }`
- Error example:
  - `422 Unprocessable Entity` `{ "code": "ILLEGAL_MOVE", "message": "Destination not reachable under rules." }`

Assumptions

- Map content is fixed and pre-seeded in DB; clients generally consume via `GET /games/{id}/state` that includes map metadata; tiles may be fetched separately if needed for rendering.
- Idempotency enforced via application-side keys and DB uniqueness where applicable (e.g., analytics `client_request_id`).
