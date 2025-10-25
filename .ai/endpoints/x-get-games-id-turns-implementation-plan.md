# API Endpoint Implementation Plan: GET /games/{id}/turns

## 1. Endpoint Overview
Returns a paginated list of committed turns for a game, for client timeline/history views.

## 2. Request Details
- HTTP Method: GET
- URL Pattern: /games/{id}/turns
- Parameters:
  - Required: `id` (long) path parameter
  - Optional: `page` (1-based), `pageSize` (default 20, max 100), `sort` (turnNo|committedAt), `order` (asc|desc)
- Request Body: none

## 3. Used Types
- DTOs: `TurnDto`
- Wrappers: `PagedResult<TurnDto>`

## 4. Response Details
- 200 OK: `{ items, page, pageSize, total? }`
- 401 Unauthorized if not signed in
- 404 Not Found if game not accessible
- 500 Server Error on unhandled exceptions

## 5. Data Flow
- RLS session var set via middleware.
- Controller validates query params and calls `ITurnService.ListTurnsAsync(gameId, paging, sorting)`.
- Service filters `Turns` by `gameId`, applies sort/paging, projects to `TurnDto`.

## 6. Security Considerations
- Authentication required.
- Authorization via RLS and `Authenticated` policy.
- CSRF: Not applicable.

## 7. Error Handling
- 404 if game not accessible.
- 400 for invalid sort/order/page sizes.
- Log failures with `ILogger` (`Turns.ListFailed`).

## 8. Performance Considerations
- `AsNoTracking()` and projection.
- Index on `(game_id, turn_no)` and `(game_id, committed_at)` for sorting.
- Consider returning without `total` for performance on large histories.

## 9. Implementation Steps
1. Define `ListTurnsQuery` model and validate query params.
2. Implement `ITurnService.ListTurnsAsync` returning `PagedResult<TurnDto>`.
3. Add controller action `GET /games/{id}/turns` with 404 handling.
4. Swagger docs; tests for paging and sorting.

