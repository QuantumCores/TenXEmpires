# API Endpoint Implementation Plan: GET /auth/csrf

## 1. Endpoint Overview
Issues and refreshes the anti-forgery token for the SPA. Sets a non-HttpOnly `XSRF-TOKEN` cookie that the client echoes via `X-XSRF-TOKEN` on state-changing requests. No body is returned.

## 2. Request Details
- HTTP Method: GET
- URL Pattern: `/auth/csrf`
- Parameters:
  - Required: none
  - Optional: none
- Request Body: none

## 3. Used Types
- DTOs: none
- Command Models: none

## 4. Response Details
- Status Codes:
  - 204 No Content on success (sets cookie)
  - 429 Too Many Requests if rate-limited
  - 500 Internal Server Error on unexpected failure
- Headers/Cookies:
  - `Set-Cookie: XSRF-TOKEN=<token>; Path=/; SameSite=Lax; Secure`
  - No response body

## 5. Data Flow
- Controller action calls `IAntiforgery.GetAndStoreTokens(HttpContext)` to generate tokens.
- Configure Antiforgery to:
  - Use cookie name `XSRF-TOKEN` (not HttpOnly) for the request token
  - Expect header `X-XSRF-TOKEN` on non-GET endpoints validated by `[ValidateAntiForgeryToken]`
- No database or domain service interactions.
- Minimal Serilog tracing for observability (no sensitive data logged).

## 6. Security Considerations
- Cookie flags: `Secure`, `SameSite=Lax`, `Path=/`, not `HttpOnly` (client must read it), short-lived.
- Do not log token values. Redact if emitted via diagnostics.
- CORS: allow credentials only for trusted origins; ensure this endpoint abides by the same policy.
- CSRF validation is enforced on state-changing routes via `[ValidateAntiForgeryToken]`; this endpoint only issues tokens.

## 7. Error Handling
- Log failures as warnings with event id (no token content): generation/storage exceptions result in `500` with `{ code: 'CSRF_ISSUE_FAILED', message: 'Unable to issue CSRF token.' }`.
- Apply global rate limiting; return `429` with standard `{ code: 'RATE_LIMIT_EXCEEDED' }` payload.

## 8. Performance Considerations
- Token generation is in-memory and cheap; avoid extra allocations/logging.
- Cache-control: `no-store` to prevent intermediary caching.

## 9. Implementation Steps
1. Configure Antiforgery in `Program.cs`:
   - `builder.Services.AddAntiforgery(o => { o.HeaderName = "X-XSRF-TOKEN"; o.Cookie.Name = "XSRF-TOKEN"; o.Cookie.SameSite = SameSiteMode.Lax; o.Cookie.SecurePolicy = CookieSecurePolicy.Always; o.Cookie.Path = "/"; o.SuppressXFrameOptionsHeader = false; });`
2. Add `AuthController` with `[ApiController]`, `[ApiVersion("1.0")]`, `[Route("v{version:apiVersion}/auth")]` and `GET /csrf` action:
   - Inject `IAntiforgery`; call `GetAndStoreTokens(HttpContext)`; set cookie via antiforgery (no body); return `NoContent()`.
   - Add `[AllowAnonymous]` so unauthenticated users can obtain a token for Login/Register.
3. Ensure state-changing endpoints already carry `[ValidateAntiForgeryToken]` (present on Games/Saves/Analytics) and clients send `X-XSRF-TOKEN` header.
4. Configure CORS to allow credentials for the SPA origin; confirm cookies flow on same-site.
5. Update Swagger with a brief description; mark response type `204` and document cookie behavior.
6. Add integration test: `GET /v1/auth/csrf` returns 204 and sets `XSRF-TOKEN` cookie with `Secure` and `SameSite=Lax`.

