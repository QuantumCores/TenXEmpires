Here is the **English translation** of your `<architecture_analysis>` and Mermaid diagram block:

---

<architecture_analysis>

1. **List of Components**

* `Login.tsx`, `Register.tsx`, `GameAuthGuard.tsx`, `GameCurrentGuardRoute.tsx`
* Form components: `LoginForm`, `RegisterForm`, `ForgotPasswordModal`, `VerifyEmailModal`
* Support components: `LoginSupportLinks`, `RegisterSupportLinks`, `AuthAwareCTA`
* State providers: `CsrfProvider`, `IdleSessionProvider`, hook `useAuthStatusQuery`
* API layer: `postJson`, `getJson`, modules `auth.ts`, `csrf.ts`
* Server controller: `AuthController` with actions for CSRF, login, register, logout, keepalive, forgot, resend

2. **Main Pages and Their Components**

* `Login.tsx` renders `LoginForm`, `LoginSupportLinks`, and the recovery/verification modals
* `Register.tsx` renders `RegisterForm`, `RegisterSupportLinks`, and `VerifyEmailModal`
* Game pages use `GameAuthGuard` and session-dependent start modals

3. **Data Flow Between Components**

* Forms delegate validation to Zod and call `postJson` with a CSRF header
* Upon success, the client invalidates the `auth-status` query (React Query) and redirects
* `CsrfProvider` and `withCsrfRetry` handle token refresh before mutating requests
* The server controller uses ASP.NET Identity and communicates with Postgres via `UserManager`
* `IdleSessionProvider` and the `SessionExpiredModal` respond to 401/idle events

4. **Component Function Descriptions**

* `LoginForm`: collects login data, validates, and sends request
* `RegisterForm`: handles registration and maps error codes to messages
* `ForgotPasswordModal`: sends reset request and shows success state
* `VerifyEmailModal`: allows resending of confirmation link
* `CsrfProvider`: initializes and refreshes the XSRF cookie, exposes context status
* `IdleSessionProvider`: monitors user inactivity and triggers keepalive
* `AuthController`: handles authentication, registration, session refresh, and logout

</architecture_analysis>

---

<mermaid_diagram>

```mermaid
flowchart TD

classDef ui fill:#E6F4EA,stroke:#2D6A4F,stroke-width:1px
classDef state fill:#E0ECFF,stroke:#1D4ED8,stroke-width:1px
classDef api fill:#FFF4E6,stroke:#D97706,stroke-width:1px
classDef server fill:#FDE68A,stroke:#92400E,stroke-width:1px
classDef db fill:#FFE4E6,stroke:#BE123C,stroke-width:1px

subgraph UI_Layer["UI Layer"]
  direction TB
  subgraph PublicPages["Public Pages"]
    LPage["Login.tsx Page"]:::ui
    RPage["Register.tsx Page"]:::ui
  end

  subgraph FormComponents["Forms and Modals"]
    LForm["LoginForm"]:::ui
    LLinks["LoginSupportLinks"]:::ui
    FModal["ForgotPasswordModal"]:::ui
    VModal["VerifyEmailModal"]:::ui
    RForm["RegisterForm"]:::ui
    RLinks["RegisterSupportLinks"]:::ui
  end

  subgraph SharedUI["Shared Components"]
    CTA["AuthAwareCTA"]:::ui
    Guard["GameAuthGuard"]:::ui
    SessionModal["SessionExpiredModal"]:::ui
  end

  subgraph ClientState["State and Contexts"]
    CsrfProv["CsrfProvider"]:::state
    IdleProv["IdleSessionProvider"]:::state
    AuthQuery["useAuthStatusQuery"]:::state
  end
end

subgraph ClientAPI["Client API Layer"]
  direction TB
  PostJson["postJson"]:::api
  GetJson["getJson"]:::api
  CsrfLib["withCsrfRetry / refreshCsrfToken"]:::api
  AuthApi["auth.ts (keepalive)"]:::api
end

subgraph Server["ASP.NET Server"]
  direction TB
  AuthCtrl["AuthController"]:::server
  SignInMgr["SignInManager"]:::server
  UserMgr["UserManager"]:::server
  AntiForgery["IAntiforgery"]:::server
end

DB["Postgres Identity"]:::db

%% Page bindings
LPage --> LForm
LPage --> LLinks
LPage -.-> FModal
LPage -.-> VModal
RPage --> RForm
RPage --> RLinks
RPage -.-> VModal
Guard --> SessionModal

%% State to UI
CsrfProv --> LForm
CsrfProv --> RForm
CsrfProv --> FModal
CsrfProv --> VModal
IdleProv --> SessionModal
IdleProv --> AuthApi
AuthQuery --> LPage
AuthQuery --> Guard

%% UI to client API
LForm --> PostJson
RForm --> PostJson
FModal --> PostJson
VModal --> PostJson
SessionModal --> AuthApi
Guard --> GetJson
CTA --> GetJson
CsrfProv --> CsrfLib
CsrfLib --> PostJson

%% Client to server
PostJson --> AuthCtrl
GetJson --> AuthCtrl
AuthApi --> AuthCtrl
CsrfLib --> AuthCtrl

%% Server uses services
AuthCtrl --> SignInMgr
AuthCtrl --> UserMgr
AuthCtrl --> AntiForgery
SignInMgr --> UserMgr
UserMgr --> DB

%% Responses back
AuthCtrl --> PostJson
AuthCtrl --> GetJson
AuthCtrl --> AuthApi
DB --> UserMgr

%% Redirect flow
AuthCtrl -. "Informs" .-> LPage
AuthCtrl -. "Informs" .-> RPage
SessionModal -. "Redirects" .-> LPage

```

</mermaid_diagram>
