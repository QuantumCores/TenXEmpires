# API Endpoint Implementation Plan: GET /games/{id}/saves

## 1. Endpoint Overview
Returns manual and autosave lists for a specific game, grouped for client UI.

## 2. Request Details
- HTTP Method: GET
- URL Pattern: /games/{id}/saves
- Parameters:
  - Required: `id` (long) path parameter
  - Optional: none
- Request Body: none

## 3. Used Types
- DTOs: `SaveManualDto`, `SaveAutosaveDto`, `GameSavesListDto`

## 4. Response Details
- 200 OK: `GameSavesListDto` with `{ manual: SaveManualDto[], autosaves: SaveAutosaveDto[] }`
- 401 Unauthorized
- 404 Not Found if game not accessible
- 500 Server Error

## 5. Data Flow
- RLS session var set via middleware.
- Controller calls `ISaveService.ListSavesAsync(gameId)`.
- Service queries `Saves` filtered by `gameId` and groups into manual vs autosaves, projects to DTOs.

## 6. Security Considerations
- Authentication required; RLS enforces game ownership.
- CSRF: Not applicable to GET.

## 7. Error Handling
- 404 if game not accessible.
- Log failures via `ILogger` (`Saves.ListFailed`).

## 8. Performance Considerations
- `AsNoTracking()` and projection to DTO.
- Indexes on `(game_id)`.

## 9. Implementation Steps
1. Implement `ISaveService.ListSavesAsync(long gameId)` returning `GameSavesListDto`.
2. Add controller action `GET /games/{id}/saves`.
3. Return 404 for inaccessible game.
4. Add Swagger docs and tests.

