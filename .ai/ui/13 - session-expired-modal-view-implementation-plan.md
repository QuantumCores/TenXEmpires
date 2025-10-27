# View Implementation Plan Session Expired Modal

Reference: See [01 - routing-and-modal-framework-implementation-plan.md](./01 - routing-and-modal-framework-implementation-plan.md) for shared routing and modal framework implementation details.

## 1. Overview
Prompts users to re-authenticate after idle timeout or CSRF/auth failure. Preserves return URL to continue gameplay.

## 2. View Routing
- Path: `/game/:id?modal=session-expired`

## 3. Component Structure
- `SessionExpiredModal`
  - Message area
  - Buttons: Login, Dismiss

## 4. Component Details
### SessionExpiredModal
- Description: Blocking dialog guiding the user to login; optional pre‑expiry banner uses `/auth/keepalive` to extend session.
- Elements: Dialog with title, description, buttons; optional countdown when opened via idle.
- Interactions: Login → navigate to `/login?returnUrl=/game/current`.
- Validation: None.
- Types: None.
- Props: `{ returnUrl?: string }`.

## 5. Types
- None (UI only).

## 6. State Management
- UI flag for visibility driven by interceptors reacting to `401/403` or explicit idle expiry.

## 7. API Integration
- Keepalive banner (outside modal): `GET /auth/keepalive` returns 204.

## 8. User Interactions
- Click Login to go to login page with returnUrl; close to remain on map (read-only until re-auth).

## 9. Conditions and Validation
- Interceptor shows modal on `403 CSRF_INVALID` (after one CSRF refresh retry) or `401`.

## 10. Error Handling
- If login fails or remains unauthenticated, modal can re-open on next request.

## 11. Implementation Steps
1. Implement modal and wire router navigation.
2. Add auth/CSRF interceptors to open modal on `401/403`.
3. Add idle timer with T‑60 s banner calling `/auth/keepalive`.
