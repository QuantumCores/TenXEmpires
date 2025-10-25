# API Endpoint Implementation Plan: DELETE /games/{id}

## 1. Endpoint Overview
Deletes a game and all associated child entities (participants, units, cities, saves, turns) owned by the authenticated user.

## 2. Request Details
- HTTP Method: DELETE
- URL Pattern: /games/{id}
- Parameters:
  - Required: `id` (long) path parameter
  - Optional: Header `Idempotency-Key` (string) — optional if implementing idempotent deletes
- Request Body: none

## 3. Used Types
- None (no response body)

## 4. Response Details
- 204 No Content on successful deletion
- 401 Unauthorized if not signed in
- 404 Not Found if not accessible or does not exist
- 500 Server Error on unhandled exceptions

## 5. Data Flow
- RLS session var set via middleware.
- Controller validates anti-forgery token and calls `IGameService.DeleteGameAsync(id, idempotencyKey)`.
- Service executes transactional delete: load stub, verify ownership via RLS, delete game; cascading FKs remove children.
- Optionally record idempotency key for safe retries.

## 6. Security Considerations
- Authentication required; CSRF token required for DELETE.
- Authorization via RLS; return 404 on non-ownership to avoid information disclosure.
- Rate limiting applies.

## 7. Error Handling
- Return 404 if no game deleted.
- Log with `ILogger` (`Games.DeleteFailed`) including `gameId` and `userId`.

## 8. Performance Considerations
- Use FK ON DELETE CASCADE to avoid N+1 deletes.
- Wrap in a single transaction; avoid loading full graph.

## 9. Implementation Steps
1. Add controller action `DELETE /games/{id}`.
2. Implement `IGameService.DeleteGameAsync(long id, string? idempotencyKey)`.
3. Ensure FK cascade configured; verify via migration or DbUp scripts.
4. Return 204 on success; 404 otherwise.
5. Add Swagger and tests for ownership and idempotency behavior.

