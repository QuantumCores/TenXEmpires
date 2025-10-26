# View Implementation Plan Register

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
- Validation: Email and password rules; server errors.
- Types: `RegisterFormModel`.
- Props: None.

### RegisterForm
- Description: Inputs for email and password (and confirmation if required).
- Main elements: `<input type=email>`, `<input type=password>` (and confirm), submit.
- Interactions: Submit; disable while pending.
- Validation: Email format; password rules per Identity policy; optional confirm match.
- Types: `RegisterFormModel` `{ email: string; password: string; confirm?: string }`.
- Props: `{ onSubmit(model) }`.

## 5. Types
- `RegisterFormModel` above.

## 6. State Management
- Local form state; pending flag; error store.

## 7. API Integration
- Submit to server-managed Identity registration endpoint.
- CSRF cookie via `GET /auth/csrf` (app init).

## 8. User Interactions
- Fill email/password; submit; navigate to login with verify notice.

## 9. Conditions and Validation
- Enforce basic client validation and show server validation messages.

## 10. Error Handling
- Network/5xx: show inline error; retry allowed.

## 11. Implementation Steps
1. Build RegisterPage and RegisterForm with validation.
2. Wire submit to server registration route.
3. After success, open `/login?modal=verify` with returnUrl.

