# View Implementation Plan Register

Reference: See [01 - routing-and-modal-framework-implementation-plan.md](./01 - routing-and-modal-framework-implementation-plan.md) for shared routing and modal framework implementation details.

## 1. Overview
Registration page for creating an account with email/password. After success, show verify email notice and route back to login or start flow.

## 2. View Routing
- Path: `/register`
- Modal: `/login?modal=verify` after success

## 3. Component Structure
- `RegisterPage`
  - `RegisterForm`
  - `RegisterSupportLinks` (Login)

## 4. Component Details
### RegisterPage
- Description: Registration shell with form and redirect handling.
- Main elements: Form container; headings; submit.
- Interactions: Submit form to Identity registration endpoint; on success, show verify notice and link back to login with returnUrl.
- Validation: Email and password rules; server errors; rate-limit handling.
- Types: `RegisterFormModel`.
- Props: None.

### RegisterForm
- Description: Inputs for email and password (and confirmation if required).
- Main elements: `<input type=email>`, `<input type=password>` (and confirm), submit.
- Interactions: Submit; disable while pending.
- Validation: 
  - Email format (required, RFC5322 basic pattern).
  - Password rules (live checklist):
    - Minimum length: 8 characters
    - At least 1 digit
    - At least 1 uppercase letter
    - At least 1 lowercase letter
    - At least 1 non‑alphanumeric (symbol)
  - Optional confirm password must match.
- Types: `RegisterFormModel` `{ email: string; password: string; confirm?: string }`.
- Props: `{ onSubmit(model) }`.

## 5. Types
- `RegisterFormModel` above.

## 6. State Management
- Local form state; pending flag; error store.
 - Derived UI state for password checklist (per-rule booleans) and overall validity.

## 7. API Integration
- Submit to server-managed Identity registration endpoint (TBD route, e.g., `POST /v1/auth/register`).
- CSRF cookie via `GET /auth/csrf` (app init).
 - Headers: include `X-XSRF-TOKEN` on submit.
 - Response handling:
   - 201/204: success → show verify notice modal and route to Login.
   - 400: map Identity errors to fields (e.g., DuplicateEmail, PasswordTooShort, PasswordRequiresNonAlphanumeric/Upper/Lower/Digit); display per-rule guidance and summary.
   - 409: email already registered → show generic message to check email or sign in (avoid enumeration in copy).
   - 429: show rate-limit message and honor `Retry-After` seconds with a countdown on the submit button.
   - 5xx: generic error banner with retry.

## 8. User Interactions
- Fill email/password; submit; navigate to login with verify notice.
 - Toggle password visibility (aria-controls for a11y).
 - On 429, disable submit and show countdown until retry is allowed.

## 9. Conditions and Validation
- Enforce client validation and mirror server policy to reduce round‑trips.
- Unique email required (do not confirm existence in UI copy to avoid account enumeration; use generic success/errors).
- Show inline errors next to fields; keep a summary region for screen readers.

## 10. Error Handling
- 400 field errors: map and highlight failing rules, keep server message in a visually hidden region for screen readers.
- 409 duplicate email: show generic guidance to sign in or reset password (no enumeration wording).
- 429: show "Too many attempts" with retry countdown based on `Retry-After`.
- Network/5xx: inline error; allow retry.
- CSRF invalid: refresh token once via `GET /auth/csrf`, then retry once; if still failing, open Session Expired modal.

## 11. Implementation Steps
1. Build RegisterPage and RegisterForm with validation.
2. Wire submit to server registration route.
3. After success, open `/login?modal=verify` with returnUrl.
 4. Add password rule checklist with live validation tied to input.
 5. Implement 429 handling using `Retry-After` header and countdown.
 6. Add a11y: labels, described-by for rules, error summary region, focus management on errors.
