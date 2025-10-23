# API Endpoint Implementation Plan: GET /games/{id}/state

## 1. Endpoint Overview
Returns the aggregate `GameState` projection combining game, map, participants, units, cities, cityTiles, cityResources, unitDefinitions, and optional turn summary — optimized for client sync.

## 2. Request Details
- HTTP Method: GET
- URL Pattern: /games/{id}/state
- Parameters:
  - Required: `id` (long) path parameter
  - Optional: none
- Request Body: none

## 3. Used Types
- DTOs: `GameStateDto` and its component DTOs

## 4. Response Details
- 200 OK: `GameStateDto`
- 401 Unauthorized if not signed in
- 404 Not Found if not accessible
- 500 Server Error on unhandled exceptions

## 5. Data Flow
- RLS session var set via middleware.
- Controller calls `IGameStateService.BuildAsync(gameId)`.
- Service performs read-only queries:
  - Load `Game`, `Map`, `Participants`.
  - Load `Units` incl. `Type` and `Tile` for positional info.
  - Load `Cities`, `CityTiles`, `CityResources`.
  - Load `UnitDefinitions` (global list).
  - Compose `GameStateDto` using `From` helpers.

## 6. Security Considerations
- Authentication required; `Authenticated` policy.
- RLS ensures owner-only access.
- CSRF: Not applicable.

## 7. Error Handling
- If any core row is missing due to not-found/RLS, return 404 `{ code: "GAME_NOT_FOUND" }`.
- Log exceptions with `ILogger` event id `GameState.BuildFailed`.

## 8. Performance Considerations
- Use `AsNoTracking()` and selective projections.
- Batch queries: avoid N+1 by loading children with filters on `gameId` and projecting to DTOs.
- Consider server-side caching per `gameId` for short intervals when state is hot and read-mostly.

## 9. Implementation Steps
1. Implement `IGameStateService.BuildAsync(long gameId)` with efficient, read-only EF queries.
2. Add controller action `GET /games/{id}/state`.
3. Return 404 if game not accessible.
4. Add Swagger schema example; add integration test against seeded data.

