# API Endpoint Implementation Plan: GET /auth/keepalive

## 1. Endpoint Overview
Refreshes the authenticated session to extend sliding expiration without changing user state. Returns no content; used by the UI’s idle banner when the user opts to stay signed in.

## 2. Request Details
- HTTP Method: GET
- URL Pattern: `/auth/keepalive`
- Parameters:
  - Required: none
  - Optional: none
- Request Body: none

## 3. Used Types
- DTOs: none
- Command Models: none

## 4. Response Details
- Status Codes:
  - 204 No Content on success
  - 401 Unauthorized if not signed in
  - 429 Too Many Requests if rate-limited
  - 500 Internal Server Error on unexpected failure
- Headers/Cookies:
  - Sliding cookie refresh occurs per ASP.NET Identity configuration; optionally include `Cache-Control: no-store`

## 5. Data Flow
- Controller verifies authentication via `[Authorize]`.
- For cookie auth with sliding expiration, a successful authenticated request typically refreshes the cookie near half-life. To force refresh, optionally call `SignInManager.RefreshSignInAsync(user)`.
- No database writes; a user lookup may occur to refresh the security stamp.
- Serilog traces success/failure (no PII beyond user id claim).

## 6. Security Considerations
- Requires `[Authorize]` and valid auth cookie; no anti-forgery required for GET.
- Respect CORS/credentials policy; do not expose user data in response.
- Enforce rate limiting to prevent abuse.
- Do not log tokens or cookie content; only user id claim and outcome.

## 7. Error Handling
- 401 with `{ code: 'UNAUTHORIZED', message: 'Not signed in.' }` when no valid session.
- 429 with standard rate-limit payload when exceeded.
- 500 with `{ code: 'KEEPALIVE_FAILED', message: 'Unable to refresh session.' }` on unexpected errors.

## 8. Performance Considerations
- Very lightweight; ensure endpoint remains fast (<5 ms server time).
- Avoid extra allocations; no body serialization.

## 9. Implementation Steps
1. Ensure Identity/cookie auth is configured (AddAuthentication/AddIdentity) with sliding expiration enabled and reasonable cookie lifetime.
2. Add `AuthController` action `GET /keepalive` under `[Authorize]`:
   - If using Identity, inject `UserManager`/`SignInManager`; call `RefreshSignInAsync` to proactively extend cookie lifetime.
   - Return `NoContent()` on success.
3. Add Swagger docs: `204` on success, `401` when unauthenticated.
4. Add integration tests:
   - Unauthenticated call returns `401`.
   - Authenticated call returns `204` and results in a refreshed auth cookie (validate Set-Cookie presence when near expiry if forcing refresh).
5. Apply global rate limiting and `Cache-Control: no-store`.

