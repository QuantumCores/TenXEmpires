# API Endpoint Implementation Plan: POST /analytics/batch

## 1. Endpoint Overview
Ingests a batch of client analytics events with best-effort deduplication and privacy protection (salted user key). Non-RLS by design.

## 2. Request Details
- HTTP Method: POST
- URL Pattern: /analytics/batch
- Parameters:
  - Required: none
  - Optional: none
- Request Body: `AnalyticsBatchCommand` → `{ events: [{ eventType, gameId?, turnNo?, occurredAt?, clientRequestId?, payload? }] }`

## 3. Used Types
- Command Model: `AnalyticsBatchCommand`, `AnalyticsEventItem`
- Response DTO: `AnalyticsBatchResponse`

## 4. Response Details
- 202 Accepted: `{ accepted: number }`
- 429 Too Many Requests: `{ code: "RATE_LIMIT_EXCEEDED" }`
- 400 Bad Request: invalid payload
- 500 Server Error

## 5. Data Flow
- Controller validates anti-forgery (state-changing), model, and per-identity rate limit.
- Service `IAnalyticsService.IngestBatchAsync(command)`:
  - For each item: normalize timestamps; compute `user_key` = salted hash of current userId (or anonymous id if allowed).
  - Copy `game_id` to `game_key` without FK.
  - Deduplicate using DB partial unique index when `client_request_id` present; otherwise best-effort insert.
- Return count of accepted events.

## 6. Security Considerations
- Authentication may be optional; if authenticated, use userId for hash; otherwise use device cookie id if available.
- Anti-forgery required for POST if cookies are used.
- PII: store only salted hash; no raw user id.
- Rate limit: enforce 60 req/min per identity.

## 7. Error Handling
- 429 when exceeding limits; include `Retry-After` header.
- Log failures with `ILogger` (`Analytics.IngestFailed`).

## 8. Performance Considerations
- Use bulk insert when supported by EF or raw SQL for high throughput.
- Ensure index on `(event_type, occurred_at)` and uniqueness on `client_request_id` when present.

## 9. Implementation Steps
1. Add controller action `POST /analytics/batch` binding `AnalyticsBatchCommand`.
2. Implement `IAnalyticsService.IngestBatchAsync` with hashing and dedupe.
3. Enforce rate limiting and return `202 Accepted` with count.
4. Swagger docs and tests for dedup and rate limiting.

