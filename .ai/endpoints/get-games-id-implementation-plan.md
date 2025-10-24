# API Endpoint Implementation Plan: GET /games/{id}

## 1. Endpoint Overview
Returns detailed summary information for a specific game owned by the authenticated user.

## 2. Request Details
- HTTP Method: GET
- URL Pattern: /games/{id}
- Parameters:
  - Required: `id` (long) path parameter
  - Optional: none
- Request Body: none

## 3. Used Types
- DTOs: `GameDetailDto`

## 4. Response Details
- 200 OK: `GameDetailDto`
- 401 Unauthorized: not signed in
- 403 Forbidden: blocked by RLS when not owner
- 404 Not Found: `{ code: "GAME_NOT_FOUND" }`
- 500 Server Error on unhandled exception

## 5. Data Flow
- RLS session var set via middleware.
- Controller calls `IGameService.GetGameDetailAsync(id)`.
- Service queries EF `Games` by `Id` and `UserId` (defense-in-depth), projects to `GameDetailDto`.

## 6. Security Considerations
- Authentication required.
- Authorization: `Authenticated` policy + RLS.
- CSRF: Not applicable to GET.

## 7. Error Handling
- If no row returned (due to not-found or RLS), return 404 with `GAME_NOT_FOUND`; avoid leaking existence.
- Log with `ILogger` (`Games.GetDetailFailed`) for exceptions.

## 8. Performance Considerations
- `AsNoTracking()` and direct projection to DTO.
- Consider ETag based on `last_turn_at` and `turn_no` to enable 304 responses.

## 9. Implementation Steps
1. Add controller action `GET /games/{id}` with route constraint `:long`.
2. Implement `IGameService.GetGameDetailAsync(long id)` using EF Core.
3. Map entity to `GameDetailDto` via `GameDetailDto.From` or projection.
4. Return 404 if not found; add ETag support.
5. Document in Swagger with examples.
6. Tests for not owner (RLS) returns 404; owner returns 200.

