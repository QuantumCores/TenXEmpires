# View Implementation Plan Forgot Password Modal

## 1. Overview
A focused modal that lets users request a password reset email by entering their account email. After submission, the modal switches to a confirmation state explaining that, if the email exists, instructions were sent.

## 2. View Routing
- Path: `/login?modal=forgot`

## 3. Component Structure
- `ForgotPasswordModal`
  - `ForgotForm`
    - `EmailInput`
    - `SubmitButton`
  - `SuccessState` (post-submit)
  - `FooterActions` (Close)

## 4. Component Details
### ForgotPasswordModal
- Component description: Focus-trapped dialog opened from the Login page. Handles form submission and success feedback.
- Main elements: Title, description, form with email input, submit; success message and Close.
- Handled events: Submit, Close, Esc to close.
- Handled validation: Email format client-side; server responses mapped to generic success to avoid account enumeration; rate-limit feedback.
- Types: `ForgotPasswordRequest`.
- Props: None (reads returnUrl from login route if needed).

### ForgotForm
- Component description: Minimal form for email entry.
- Main elements: `<input type=email>` with label, submit button.
- Handled interactions: On submit, call server endpoint; disable while pending.
- Handled validation: Required email; RFC5322 basic pattern; show inline error for invalid format.
- Types: `ForgotPasswordRequest` `{ email: string }`.
- Props: `{ onSubmit(req) }`.

### SuccessState
- Component description: Confirmation copy that a reset email was sent if the address exists.
- Main elements: Success icon/text, Close button, link back to Login.
- Handled interactions: Close modal; navigate to Login.
- Handled validation: N/A.
- Types: None.
- Props: None.

## 5. Types
- View model / DTO
  - `ForgotPasswordRequest` `{ email: string }`

## 6. State Management
- Local component state: `email`, `isSubmitting`, `isSuccess`, `errorMessage?`.
- No global state required.

## 7. API Integration
- Server-managed Identity endpoint (TBD route, e.g., `POST /auth/forgot-password`).
  - Request: `{ email }` JSON or form-encoded per server.
  - Headers: `X-XSRF-TOKEN` on non-GET.
  - Response: `204 No Content` or `200 OK` with generic message.
  - 429 Too Many Requests: show rate-limit error and honor `Retry-After` header for retry UI.
- CSRF: Ensure `GET /auth/csrf` performed at app init.

## 8. User Interactions
- Enter email, submit → show success state regardless of existence of account.
- Close the modal to return to Login.

## 9. Conditions and Validation
- Disable submit with invalid email or while pending.
- Prevent account enumeration by always showing generic success.
- Throttle rapid submissions: disable submit and show countdown on 429 using `Retry-After`.

## 10. Error Handling
- Network/5xx: show generic error message and allow retry.
- CSRF invalid: refresh token once, retry, else show Session Expired modal.
 - 429: display "Too many attempts" with `Retry-After` countdown; keep modal open.
 - Other 4xx: keep copy generic to avoid enumeration (e.g., invalid email format remains a client-side validation error).

## 11. Implementation Steps
1. Implement `ForgotPasswordModal` with focus trap and ARIA roles (`role="dialog"`, `aria-modal="true"`).
2. Add `ForgotForm` with email validation and pending state.
3. Wire submit to server endpoint with CSRF header; map responses to success state.
4. Integrate modal opening from `/login?modal=forgot` and back button behavior.
 5. Add rate-limit handling (429): parse `Retry-After`, disable submit, show countdown, re-enable when elapsed.
 6. A11y: ensure error messages are announced; restore focus on close; Esc handler.
