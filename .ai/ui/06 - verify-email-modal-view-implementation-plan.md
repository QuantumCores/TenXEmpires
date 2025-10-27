# View Implementation Plan Verify Email Modal

Reference: See [01 - routing-and-modal-framework-implementation-plan.md](./01 - routing-and-modal-framework-implementation-plan.md) for shared routing and modal framework implementation details.

## 1. Overview
A simple modal shown after registration (or when login detects an unverified account) instructing the user to verify their email. Provides a “Resend verification email” action.

## 2. View Routing
- Path: `/login?modal=verify`

## 3. Component Structure
- `VerifyEmailModal`
  - `Instructions`
  - `ResendSection`
    - `EmailDisplay` (optional)
    - `ResendButton`
  - `FooterActions` (Back to Login)

## 4. Component Details
### VerifyEmailModal
- Component description: Focus-trapped dialog indicating next steps to verify; exposes a resend action.
- Main elements: Title, instructions, resend controls, back button.
- Handled events: Click Resend; Back to Login.
- Handled validation: None (email format not re-entered; uses known account context when available).
- Types: `ResendVerificationRequest`.
- Props: `{ email?: string }`.

## 5. Types
- `ResendVerificationRequest` `{ email?: string }` (server may infer from session).

## 6. State Management
- Local state: `isResending`, `successMessage?`, `errorMessage?`.

## 7. API Integration
- Server-managed Identity endpoint (TBD route, e.g., `POST /auth/resend-verification`).
  - Request: `{}` or `{ email }` depending on server policy.
  - Headers: `X-XSRF-TOKEN` on non-GET.
  - Response: `204 No Content` or `200 OK` with generic message.
- CSRF: Ensure bootstrap via `GET /auth/csrf` at app init.

## 8. User Interactions
- Click “Resend verification email” → show success or soft error message.
- Close modal to return to Login.

## 9. Conditions and Validation
- Disable resend button while pending; optionally throttle repeated resends.

## 10. Error Handling
- Network/5xx: show inline error and allow retry after short cooldown.
- CSRF invalid: refresh once then retry; on failure, show Session Expired modal.

## 11. Implementation Steps
1. Implement `VerifyEmailModal` with focus trap and ARIA roles.
2. Wire Resend action to server endpoint with CSRF; show success feedback.
3. Integrate with registration success flow and `/login?modal=verify` deep link.
