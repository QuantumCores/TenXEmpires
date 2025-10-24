# API Endpoint Implementation Plan: GET /unit-definitions

## 1. Endpoint Overview
Returns the static list of unit definitions (game rules data) for client rendering and validation. Read-only and cacheable.

## 2. Request Details
- HTTP Method: GET
- URL Pattern: /unit-definitions
- Parameters:
  - Required: none
  - Optional: none
- Request Body: none

## 3. Used Types
- DTOs: `UnitDefinitionDto`
- Wrappers: `ItemsResult<UnitDefinitionDto>`

## 4. Response Details
- 200 OK: `{ items: UnitDefinitionDto[] }`
- 304 Not Modified: when ETag/Last-Modified validates
- 500 Server Error: on unhandled exceptions

## 5. Data Flow
- Controller calls `ILookupService.GetUnitDefinitionsAsync()`.
- Service queries EF Core `UnitDefinitions` (read-only), projects to `UnitDefinitionDto`.
- Apply response caching: ETag derived from a stable checksum (e.g., count + max(updatedAt if available) or hash of codes) stored in-memory.

## 6. Security Considerations
- Authentication: Not required (public lookup).
- Authorization: None.
- CORS: Allow only configured origins; no credentials for this GET.
- CSRF: Not applicable to GET.

## 7. Error Handling
- Log exceptions via Serilog `ILogger` with event id `Lookup.UnitDefinitions.FetchFailed`.
- Return `{ code: "INTERNAL_ERROR", message: "..." }` with 500 on failure.
- No error table; standard structured logging only.

## 8. Performance Considerations
- Use `AsNoTracking()` and projection to DTO at query time.
- Cache results in `IMemoryCache` with an absolute expiration (e.g., 10 minutes) and ETag.
- Consider preloading into a singleton cache on startup if dataset is fully static.

## 9. Implementation Steps
1. Add `ILookupService` with `Task<IReadOnlyList<UnitDefinitionDto>> GetUnitDefinitionsAsync()`.
2. Implement service using EF Core with `AsNoTracking()` and projection to DTO.
3. Add controller action `GET /unit-definitions` returning `ItemsResult<UnitDefinitionDto>`.
4. Add conditional ETag support (If-None-Match) and set `Cache-Control` and `ETag` headers.
5. Add Swagger documentation and examples.
6. Add unit test for projection and caching behavior.

