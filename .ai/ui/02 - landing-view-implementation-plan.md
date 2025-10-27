# View Implementation Plan Landing

Reference: See [01 - routing-and-modal-framework-implementation-plan.md](./01 - routing-and-modal-framework-implementation-plan.md) for shared routing and modal framework implementation details.

## 1. Overview
The Landing view is the unauthenticated default entry point at the root path (`/`). It introduces the product and provides clear calls to action:
- For authenticated users: a primary Play button that routes to `/game/current`.
- For visitors: Login and Register buttons that route to `/login` and `/register` (with `returnUrl=/game/current`).
The view also links to public pages (About, Gallery, Privacy, Cookies) and shows the analytics consent banner on first visit.

## 2. View Routing
- Path: `/`
- Guarding: None (public). CTA behavior changes based on authentication state.
- Navigation outcomes:
  - Play (authenticated) → `/game/current` (guard resolves to `/game/:id` or opens `?modal=start-new` inside the map shell if no active game).
  - Login → `/login?returnUrl=/game/current`
  - Register → `/register?returnUrl=/game/current`
  - Public links → `/about`, `/gallery`, `/privacy`, `/cookies`

## 3. Component Structure
- `LandingPage`
  - `LandingNavbar` (links: About, Gallery, Privacy, Cookies; optional logo)
  - `LandingHero`
    - `AuthAwareCTA` (Play or Login/Register)
  - `LandingHighlights` (optional short blurb/features from PRD; keep lightweight)
  - `ConsentBanner` (analytics opt-in)
  - `FooterLinks` (Privacy, Cookies)

## 4. Component Details
### LandingPage
- Component description: Root container for the landing route. Coordinates auth status detection, renders hero and CTAs, and shows consent banner.
- Main elements: `<main>`, hero section, nav, footer; responsive single-column layout.
- Handled interactions:
  - On mount, triggers auth status query.
  - Navigation clicks to other routes.
- Handled validation: None (display-only), but CTA shown depends on auth detection.
- Types: `AuthStatus`, `GameSummary`, `LandingViewModel`.
- Props: None (obtains state via hooks/providers).

### LandingNavbar
- Component description: Top navigation bar with links to public pages.
- Main elements: `<header>`, nav list with links.
- Handled interactions: Navigate to `/about`, `/gallery`, `/privacy`, `/cookies`.
- Handled validation: None.
- Types: None.
- Props: Optional `compact?: boolean`.

### LandingHero
- Component description: Hero block with product name, blurb, and the `AuthAwareCTA`.
- Main elements: Title, subtitle, CTA area.
- Handled interactions: Delegated to `AuthAwareCTA`.
- Handled validation: None.
- Types: None.
- Props: `cta: ReactNode` (usually `AuthAwareCTA`).

### AuthAwareCTA
- Component description: Shows Play button for authenticated users, Login/Register buttons for visitors.
- Main elements:
  - If authenticated: `Play` primary button.
  - Else: `Login` and `Register` buttons; optional divider text.
- Handled interactions:
  - Click Play → navigate to `/game/current`.
  - Click Login → navigate to `/login?returnUrl=/game/current`.
  - Click Register → navigate to `/register?returnUrl=/game/current`.
- Handled validation:
  - Requires an auth detection result; handles loading/unknown with a safe fallback: show Login/Register immediately, and replace with Play if auth is later confirmed.
- Types: `AuthStatus` (input from hook).
- Props: `auth: AuthStatus`.

### LandingHighlights (optional)
- Component description: Minimal feature bullets (e.g., fixed 20x15 map, deterministic AI < 500 ms, saves/autosaves).
- Main elements: list of short bullets/icons.
- Handled interactions: None.
- Handled validation: None.
- Types: None.
- Props: None.

### ConsentBanner
- Component description: Displays analytics consent with accept/decline choices and links to Privacy/Cookies.
- Main elements: Banner with message, Accept and Decline buttons.
- Handled interactions: Accept/Decline; persists cookie per policy.
- Handled validation: None.
- Types: `ConsentState` (internal), no DTO.
- Props: None.

### FooterLinks
- Component description: Footer with secondary links (Privacy, Cookies).
- Main elements: Footer bar with links.
- Handled interactions: Navigate to legal pages.
- Handled validation: None.
- Types: None.
- Props: None.

## 5. Types
- DTOs (from API Plan / DataContracts) used implicitly for auth detection:
  - `GamesListResponse` (from `GET /games`): `{ items: GameSummary[], page: number, pageSize: number, total?: number }`
  - `GameSummary`: `{ id: number, status: string, turnNo: number, mapId: number, mapSchemaVersion: number, startedAt: string, finishedAt?: string, lastTurnAt?: string }`

- View models (frontend-only):
  - `AuthStatus`:
    - `isAuthenticated: boolean`
    - `hasActiveGame?: boolean` (true if there is at least one active game)
    - `latestActiveGame?: Pick<GameSummary, 'id'|'turnNo'|'lastTurnAt'>`
    - `checkedAt: string` (ISO 8601 UTC seconds-only)
  - `LandingViewModel`:
    - `auth: AuthStatus`
    - `consentAccepted: boolean`

## 6. State Management
- React Query:
  - Key: `['auth-status']`
  - Fetcher: `GET /games?status=active&sort=lastTurnAt&order=desc&pageSize=1`
    - If `200`: `isAuthenticated=true`, set `hasActiveGame = items.length > 0`.
    - If `401/403`: `isAuthenticated=false`.
    - If network/5xx: return `isAuthenticated=false` but keep a transient error to display a soft warning (do not block CTAs).
  - `staleTime`: 60 s; `retry`: 0 (show soft warning on failure).
- UI Store (Zustand):
  - `consentAccepted: boolean` (also persisted to cookie per policy).
  - No selection/camera state in landing.

## 7. API Integration
- Endpoint(s) used:
  - `GET /games`
    - Purpose: Lightweight auth detection and to determine existence of an active game.
    - Query: `?status=active&sort=lastTurnAt&order=desc&pageSize=1`
    - Response: `{ items: GameSummary[], page, pageSize, total? }`
    - Errors: `401 UNAUTHENTICATED` → treat as visitor; `429 RATE_LIMIT_EXCEEDED` → show non-blocking banner; `5xx` → show soft warning.
- Notes:
  - No CSRF needed for GET; cookie-based auth automatically included.
  - Do not prefetch heavy resources on the Landing page.

## 8. User Interactions
- Click Play (authenticated): navigate to `/game/current`.
- Click Login: navigate to `/login?returnUrl=/game/current`.
- Click Register: navigate to `/register?returnUrl=/game/current`.
- Click About/Gallery/Privacy/Cookies: navigate to respective public routes.
- Accept/Decline Consent: set consent cookie and update UI store; if accepted, subsequent gameplay will batch analytics per turn.

## 9. Conditions and Validation
- Auth detection condition: `isAuthenticated` derived from `GET /games` response vs. 401.
- CTA rendering:
  - If `isAuthenticated=true` → show Play.
  - Else → show Login and Register.
- Rate limit/Offline:
  - If `429` → show small “Limited connectivity” banner; CTAs remain usable.
  - If offline → show “Offline” banner; CTAs still navigate (login/register may fail until back online).

## 10. Error Handling
- `401/403`: Treat as visitor; display Login/Register (no error banner).
- `429`: Non-blocking “Limited” banner, auto-dismiss after 5 s.
- Network/5xx: Soft warning “Couldn’t check session. You can still log in or register.”
- Offline: Show “Offline” banner; no blocking dialogs.

## 11. Implementation Steps
1. Add route `/` to the router and point to `LandingPage`.
2. Create `LandingPage` with semantic structure and responsive layout.
3. Implement `LandingNavbar` with links to `/about`, `/gallery`, `/privacy`, `/cookies`.
4. Implement `LandingHero` and `AuthAwareCTA` components; wire CTAs to the router.
5. Add React Query hook `useAuthStatusQuery` (key `['auth-status']`) calling `GET /games?...` per spec; map to `AuthStatus` view model.
6. Handle loading/unknown by rendering Login/Register by default; if `isAuthenticated` becomes true, replace with Play.
7. Implement `ConsentBanner` with accept/decline handlers; store consent in cookie and UI store.
8. Add soft warning banners for `429`, offline, and session check failures; ensure they do not block CTAs.
9. Test flows:
   - Visitor: Landing → Login/Register → redirect back to `/game/current` after auth.
   - Authenticated: Landing → Play → `/game/current` guard behavior.
   - Rate limit/offline conditions.
10. Accessibility pass: focus order, link names, button labels, `aria-live` for banners.
11. Performance check: no heavy prefetching on Landing; bundle impact minimal.
