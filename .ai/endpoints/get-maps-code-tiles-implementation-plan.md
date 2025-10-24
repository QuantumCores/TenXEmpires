# API Endpoint Implementation Plan: GET /maps/{code}/tiles

## 1. Endpoint Overview
Returns the list of tiles for a given map, used by the client to render terrain and resources. Read-only; supports pagination for large maps.

## 2. Request Details
- HTTP Method: GET
- URL Pattern: /maps/{code}/tiles
- Parameters:
  - Required: `code` (string) path parameter
  - Optional: `page` (int, 1-based), `pageSize` (int, default 20, max 100)
- Request Body: none

## 3. Used Types
- DTOs: `MapTileDto`
- Wrappers: `ItemsResult<MapTileDto>` or `PagedResult<MapTileDto>` if paged

## 4. Response Details
- 200 OK: `{ items: MapTileDto[] }` or paged `{ items, page, pageSize, total? }`
- 404 Not Found if map code does not exist: `{ code: "MAP_NOT_FOUND" }`
- 304 Not Modified when ETag validates
- 500 Server Error on unhandled exceptions

## 5. Data Flow
- Validate map by code via `ILookupService.GetMapByCodeAsync`.
- Query `MapTiles` by `MapId` with `AsNoTracking()`, project to `MapTileDto`.
- If pagination specified, apply `Skip/Take` and return `PagedResult`.
- Cache per map code with ETag; for pagination, vary by `page`/`pageSize`.

## 6. Security Considerations
- Authentication: Not required.
- Authorization: None.
- CORS: Strict origins; no credentials for this GET.
- CSRF: Not applicable.

## 7. Error Handling
- 404 for unknown map code.
- Log exceptions with `ILogger` event id `Lookup.MapTiles.FetchFailed`.
- No error table; structured logging only.

## 8. Performance Considerations
- `AsNoTracking()` and projection to DTO.
- Add composite index `(map_id, row, col)` (already implied by constraints) for ordered retrieval.
- Cache results; if very large, consider server-side gzip and chunked transfer (optional).

## 9. Implementation Steps
1. Add `ILookupService.GetMapTilesAsync(string code, int? page, int? pageSize)` returning `ItemsResult<MapTileDto>` or `PagedResult<MapTileDto>`.
2. Implement EF query with joins only if needed (tiles are self-contained); project to DTO.
3. Add controller action `GET /maps/{code}/tiles` with pagination validation and ETag.
4. Swagger docs with examples.
5. Unit tests for paging bounds and not-found handling.

