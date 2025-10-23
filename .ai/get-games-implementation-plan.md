# API Endpoint Implementation Plan: GET /games

## 1. Endpoint Overview
Lists the authenticated user’s games with optional filtering and pagination. RLS enforced via session `app.user_id`.

## 2. Request Details
- HTTP Method: GET
- URL Pattern: /games
- Parameters:
  - Required: none
  - Optional: `status` (active|finished), `page` (1-based), `pageSize` (default 20, max 100), `sort` (startedAt|lastTurnAt|turnNo), `order` (asc|desc)
- Request Body: none

## 3. Used Types
- DTOs: `GameListItemDto`
- Wrappers: `PagedResult<GameListItemDto>`

## 4. Response Details
- 200 OK: `{ items, page, pageSize, total? }`
- 401 Unauthorized: if not signed in
- 500 Server Error on unhandled exceptions

## 5. Data Flow
- Middleware sets `SET LOCAL app.user_id = '<userId>'` at request start within transaction scope.
- Controller validates query parameters and calls `IGameService.ListGamesAsync(filter, paging, sorting)`.
- Service queries EF Core `Games` filtered by `UserId` (defense-in-depth) and/or RLS, projects to `GameListItemDto`, applies sort and pagination.

## 6. Security Considerations
- Authentication required (cookie-based ASP.NET Identity).
- Authorization: `Authenticated` policy.
- CSRF: Not applicable to GET.
- Rate limit: 60 req/min per client identity.

## 7. Error Handling
- Validate query parameter values; on invalid sort/order/page sizes return 400 `{ code: "INVALID_INPUT" }`.
- Log failures with `ILogger` event id `Games.ListFailed`.
- No error table; structured logging only.

## 8. Performance Considerations
- Use `AsNoTracking()` and `Select` projection to DTO.
- Add indexes on `(user_id, status)` and on `(last_turn_at)` if sorting frequently.
- Consider deferred `total` computation; optionally omit `total` for performance.

## 9. Implementation Steps
1. Define `ListGamesQuery` model (status, page, pageSize, sort, order) with DataAnnotations for ranges and enums.
2. Implement `IGameService.ListGamesAsync` returning `PagedResult<GameListItemDto>`.
3. Add controller action `GET /games` with model binding for query params and validation.
4. Map sort keys to EF columns, default `lastTurnAt desc`.
5. Swagger docs with query examples.
6. Tests: filtering, sorting, pagination bounds.

