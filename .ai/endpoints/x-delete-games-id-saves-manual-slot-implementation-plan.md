# API Endpoint Implementation Plan: DELETE /games/{id}/saves/manual/{slot}

## 1. Endpoint Overview
Deletes a manual save in the specified slot for a game owned by the authenticated user.

## 2. Request Details
- HTTP Method: DELETE
- URL Pattern: /games/{id}/saves/manual/{slot}
- Parameters:
  - Required: `id` (long), `slot` (int, 1..3)
  - Optional: Header `Idempotency-Key` (string)
- Request Body: none

## 3. Used Types
- None (no response body)

## 4. Response Details
- 204 No Content on success
- 400 Bad Request for invalid slot
- 401 Unauthorized
- 404 Not Found if save not found or game inaccessible
- 500 Server Error

## 5. Data Flow
- RLS session var set; validate anti-forgery.
- Service `ISaveService.DeleteManualAsync(gameId, slot, idempotencyKey)` deletes where `kind='manual'` and slot matches.
- No-op deletes return 404.

## 6. Security Considerations
- Authentication and anti-forgery required.
- RLS ownership enforced.

## 7. Error Handling
- 404 for missing save; 400 for out-of-range slot.
- Log with `ILogger` (`Saves.DeleteManualFailed`).

## 8. Performance Considerations
- Single-row delete with index on `(user_id, game_id, slot)` (partial unique index covers it).

## 9. Implementation Steps
1. Add controller action `DELETE /games/{id}/saves/manual/{slot}`.
2. Implement `ISaveService.DeleteManualAsync(long gameId, int slot, string? idempotencyKey)`.
3. Validate slot in `[1,3]`; return 204 on success; 404 otherwise.
4. Swagger docs and tests.

