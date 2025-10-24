# API Endpoint Implementation Plan: GET /maps/{code}

## 1. Endpoint Overview
Returns metadata for a specific map by its code. Read-only and cacheable.

## 2. Request Details
- HTTP Method: GET
- URL Pattern: /maps/{code}
- Parameters:
  - Required: `code` (string) path parameter
  - Optional: none
- Request Body: none

## 3. Used Types
- DTOs: `MapDto`

## 4. Response Details
- 200 OK: `MapDto`
- 404 Not Found: `{ code: "MAP_NOT_FOUND", message: "..." }`
- 304 Not Modified: when ETag/Last-Modified validates
- 500 Server Error on unhandled exceptions

## 5. Data Flow
- Controller extracts `{code}` and calls `ILookupService.GetMapByCodeAsync(code)`.
- Service queries EF Core `Maps` by `Code` using `AsNoTracking()` and projects to `MapDto`.
- Apply response caching (ETag) with a checksum per map (e.g., schemaVersion + width/height).

## 6. Security Considerations
- Authentication: Not required (public lookup).
- Authorization: None.
- CORS: Strict origins; no credentials.
- CSRF: Not applicable to GET.

## 7. Error Handling
- If not found, return 404 with `MAP_NOT_FOUND`.
- Log failures via `ILogger` with event id `Lookup.Maps.GetByCodeFailed`.
- No error table; structured logging only.

## 8. Performance Considerations
- Use `AsNoTracking()` and compile query for lookups by code.
- Cache by code using `IMemoryCache`.
- Add ETag/Last-Modified headers.

## 9. Implementation Steps
1. Add `ILookupService.GetMapByCodeAsync(string code)`.
2. Implement EF Core query with projection to `MapDto`.
3. Add controller action `GET /maps/{code}`.
4. Add not-found handling and ETag support.
5. Document in Swagger with example responses.
6. Add unit tests for found/not found and caching behavior.

