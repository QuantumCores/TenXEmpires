<user_journey_analysis>
1) User flows
- Browse public pages (About, Gallery, Privacy, Cookies)
- Attempt to access game routes while unauthenticated → redirect to Login with returnUrl
- Register a new account → signed in, verify email modal, optional resend verification
- Log in with email/password → redirect to returnUrl
- Forgot password request → generic success, await email link (reset flow TBD)
- Resend verification email (from modal or login) → generic success
- Keep session alive on idle prompt → keepalive; session may expire and require re-login
- Logout → return to public area

2) Main journeys and states
- Public Area: Home/Landing, About, Gallery, Privacy, Cookies
- Authentication: Login, Registration, Password Recovery, Verify Email (resend)
- Authenticated Session: Active, Idle Warning, Expired
- App Core (Authenticated): Hub (games list), New Game, Game Map

3) Decision points and alternative paths
- Credentials valid? → Success (redirect) vs Error (stay on Login)
- Registration valid? → Signed-in + Verify modal vs Validation errors
- Email verified? → Verified vs Unverified (can still play in current impl)
- CSRF valid? → OK vs Refresh CSRF and retry once; then redirect to Login on failure
- Idle action? → User chooses to stay signed in (keepalive) vs do nothing (session expiry)
- Access control? → If unauthenticated, guard redirects to Login with returnUrl

4) State purposes
- Public.Home: Entry point; links to Login/Register; public content access
- Auth.Login.LoginForm: Collect credentials; links to Forgot/Verify/Register
- Auth.Registration.RegisterForm: Create account; on success shows VerifyEmail modal; user is signed in
- Auth.PasswordRecovery.ForgotForm: Request reset; always shows success message (no enumeration)
- Auth.VerifyEmail.Resend: Trigger verification email resend; generic success
- Session.Active: User is authenticated; can access Hub/Game
- Session.IdleWarning: Prompt to keep session alive via keepalive
- Session.Expired: Session ended; modal offers re-login with returnUrl
- Core.Hub: Authenticated hub; lists games; guards against unauth access
- Core.NewGame: Start new game (modal); requires CSRF for POST
- Core.Game: Game map; CSRF refresh on 403, redirect to Login on failure
</user_journey_analysis>

<mermaid_diagram>

```mermaid
stateDiagram-v2

[*] --> Public

state "Public Area" as Public {
  [*] --> Home
  Home: Landing and public content
  Home --> About
  Home --> Gallery
  Home --> Privacy
  Home --> Cookies

  note right of Home
    Unauthenticated users can browse About, Gallery,
    Privacy, and Cookies pages.
  end note

  About --> Home
  Gallery --> Home
  Privacy --> Home
  Cookies --> Home

  Home --> Login: Access core route [unauthenticated]
}

Public --> Auth: User chooses Login or Register

state "Authentication" as Auth {
  [*] --> Login

  state "Login" as Login {
    [*] --> LoginForm
    LoginForm: Email, password, remember me
    note right of LoginForm
      Links: Forgot password, Register, Verify Email.
      On submit → POST /v1/auth/login.
      Redirect to returnUrl on success.
    end note

    LoginForm --> if_creds <<choice>>: Submit
    if_creds --> LoginSuccess: Valid credentials
    if_creds --> LoginError: Invalid credentials
    LoginError --> LoginForm: Show error
    LoginSuccess --> [*]
  }

  Login --> Registration: Chooses Create Account
  Login --> PasswordRecovery: Opens Forgot Password
  Login --> VerifyEmail: Opens Verify Email modal

  state "Registration" as Registration {
    [*] --> RegisterForm
    RegisterForm: Email + password + confirm
    note right of RegisterForm
      On submit → POST /v1/auth/register.
      On success: user signed in and Verify Email modal shown.
      Close modal navigates to Login with returnUrl.
    end note

    RegisterForm --> if_reg <<choice>>: Submit
    if_reg --> RegSuccess: Valid input
    if_reg --> RegError: Validation errors
    RegError --> RegisterForm: Show mapped messages

    RegSuccess --> VerifyEmail
    RegSuccess --> [*]
  }

  state "Password Recovery" as PasswordRecovery {
    [*] --> ForgotForm
    ForgotForm: Request reset email
    note right of ForgotForm
      POST /v1/auth/forgot-password.
      Always returns generic success to
      avoid account enumeration.
    end note
    ForgotForm --> ForgotSuccess: Request accepted
    ForgotSuccess --> [*]
  }

  state "Verify Email" as VerifyEmail {
    [*] --> VerifyModal
    VerifyModal: Resend verification link
    note right of VerifyModal
      POST /v1/auth/resend-verification.
      Shows success or rate limit message.
    end note
    VerifyModal --> [*]
  }
}

Auth --> Session: Login success
Auth --> Session: Registration success (signed in)

state "Authenticated Session" as Session {
  [*] --> Active
  Active: User is signed in
  Active --> IdleWarning: User idle near timeout
  IdleWarning: Prompt to stay signed in
  IdleWarning --> if_idle <<choice>>
  if_idle --> Active: Keepalive accepted
  if_idle --> Expired: No action or failure
  Expired: Session expired
  note right of Expired
    UI shows Session Expired modal and
    navigates to Login with returnUrl.
  end note
}

Session --> Core: Access core functionality
Session --> Public: Logout

state "App Core" as Core {
  [*] --> Hub
  Hub: Games list (auth required)
  note right of Hub
    Guard calls GET /v1/games.
    401/403 → redirect to Login with returnUrl.
  end note

  Hub --> NewGame: Start new game
  NewGame: Create game (modal)
  note right of NewGame
    Uses CSRF-protected POST.
    On 403 CSRF error → refresh token and retry once,
    then redirect to Login on failure.
  end note

  Hub --> Game: Open current/selected game
  Game: Game map (auth required)
  note right of Game
    Non-GET actions require CSRF.
    On CSRF 403 → refresh and retry once.
    On 401/403 → redirect to Login with returnUrl.
  end note
  Game --> Hub: Exit to hub
}

state if_email <<choice>>
Session --> if_email: Email status check
if_email --> Verified: Email confirmed
if_email --> Unverified: Not confirmed
Verified: No additional gating in MVP
Unverified: User can still play

Core --> [*]: User exits app
```

</mermaid_diagram>

