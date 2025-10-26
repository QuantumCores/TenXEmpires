# View Implementation Plan Login

## 1. Overview
Login page for email/password authentication via ASP.NET Identity; includes support modals for Forgot Password and Verify Email.

## 2. View Routing
- Path: `/login`
- Modals: `/login?modal=forgot`, `/login?modal=verify`

## 3. Component Structure
- `LoginPage`
  - `LoginForm`
  - `LoginSupportLinks` (Forgot, Register)
  - `ForgotPasswordModal` (optional)
  - `VerifyEmailModal` (optional)

## 4. Component Details
### LoginPage
- Description: Auth form shell with redirect handling.
- Main elements: Form container, fields, submit.
- Interactions: Submit to Identity endpoint; on success, navigate to `returnUrl` (default `/game/current`).
- Validation: Email format, password non-empty; server errors surfaced.
- Types: `LoginFormModel`.
- Props: None.

### LoginForm
- Description: Email and password inputs with submit.
- Main elements: `<input type=email>`, `<input type=password>`, submit button.
- Interactions: Submit triggers API call; disable during pending; show error summary.
- Validation: Client-side email; required fields; respect server lockout/backoff messages.
- Types: `LoginFormModel` `{ email: string; password: string; rememberMe?: boolean }`.
- Props: `{ onSubmit(model) }`.

### LoginSupportLinks
- Description: Links to Forgot and Register.
- Main elements: Anchor links.
- Interactions: Open modals/routes.
- Validation: N/A.
- Types: None.
- Props: None.

## 5. Types
- View model `LoginFormModel` above.

## 6. State Management
- Local form state; pending flag; error message store.

## 7. API Integration
- Authentication handled by ASP.NET Identity (server-managed). SPA responsibilities:
  - CSRF bootstrap via `GET /auth/csrf` on app init.
  - Submit login form to Identity login endpoint (server route), expect auth cookies.
  - On success, redirect to `returnUrl`.

## 8. User Interactions
- Fill email/password; submit; navigate based on outcome.

## 9. Conditions and Validation
- Lockout/backoff messages from server displayed; 401/invalid credentials shown inline.

## 10. Error Handling
- Network/5xx: show inline error; allow retry.

## 11. Implementation Steps
1. Build LoginPage and LoginForm with validation.
2. Wire submit to server login route and handle redirects.
3. Add support modals for Forgot/Verify via query param.

