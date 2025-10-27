# UI Architecture for TenX Empires MVP

## 1. UI Structure Overview

- Application model
  - Single active game per account in MVP. The map shell is the primary surface; public pages are accessible unauthenticated. Authenticated gameplay lives at `/game/current` (guard resolves to `/game/:id`).
  - Deep-linked, modal-first UX for secondary tasks (Saves, Settings, Help, Account Delete, Start New Game confirm, error dialogs). Modals use query parameters (e.g., `?modal=saves`) with Back/Forward support.
  - Rendering uses a five-canvas stack sized to `CSSpx * devicePixelRatio`, zoom via CSS transforms, and offscreen pre-rendered sprites for performance.
  - Landing page at `/` is the default entry point; shows Play for authenticated users (routes to `/game/current`) and Login/Register for visitors.

- Data model and sources of truth
  - Server-authoritative gameplay; client previews are advisory only. `GET /games/{id}/state` is the canonical source of truth and includes minimal `turnSummary` when changed.
  - Static lookups (`/unit-definitions`, `/maps/{code}/tiles`) are cached long-term with ETags.

- State management
  - Server state: TanStack Query with normalized keys (`['game', id]`, `['map-tiles', code]`, `['unit-defs']`, `['saves', id]`), ETag metadata, and targeted invalidation.
  - UI state: lightweight store (e.g., Zustand) for selection, camera, canvases, modal/query state, settings (grid toggle, invert zoom), banners (idle, multi-tab), and consent.
  - Mutations serialized per game; Idempotency-Key (UUIDv4) per action with 60 s dedupe; write-through cache using `{ state: GameState }` responses.

- Input and interaction
  - Pointer-anchored zoom (factor ~1.05/step, clamped 0.75–1.5), instant panning; a single 200 ms ease for Next Unit re-center (sole animation exception).
  - Picking priority: Units > City > Feature > Base Tile > Grid with zoom-scaled world radii, centralized in an interaction config. Right-click/ESC cancels; map canvases suppress native context menu.
  - Actions lock during AI/requests; pan/zoom remains available.

- Accessibility and responsiveness
  - Target 1080p, usable down to ~900p; ≥32×32 px targets; `:focus-visible` respected, focus-trapped modals. Aria: polite for toasts, assertive for session expiry/schema mismatch/AI timeout.
  - Color tokens for reach/path/targets/blocked/selection/focus/siege/grid are color-vision-deficiency safe and meet ≥4.5:1 contrast.

- Security and compliance
  - Cookie auth via ASP.NET Identity; CSRF bootstrap on `GET /auth/csrf` setting `XSRF-TOKEN` cookie; client sends `X-XSRF-TOKEN` header on non-GET. Prefer `403 { code: 'CSRF_INVALID' }` on CSRF failure.
  - End-turn endpoint: `POST /games/{id}/end-turn`. All mutations return `{ state: GameState }` and echo `X-Idempotency-Key`.
  - Session keepalive endpoint `GET /auth/keepalive` used only on user intent (idle T‑60 s banner).
  - Server manages CSP/Referrer-Policy/frame-ancestors; SPA does not inject CSP.

- Performance and network
  - Reads timeout: 10 s with a single auto-retry (1 s backoff) + visible Retry. Actions timeout: 10 s with inline spinner. ETag/If-None-Match on `/games/{id}/state`, `/maps/{code}/tiles`, `/unit-definitions`.
  - AI overlay appears after 150 ms; escalates messaging at 2 s/5 s; blocks only on `AI_TIMEOUT`.
  - Timestamps are ISO 8601 UTC seconds-only (`YYYY-MM-DDTHH:mm:ssZ`) in payloads and UI/tooltips.


## 2. View/Modal List

- Landing
  - Path: `/`
  - Purpose: Default entry point with clear CTA.
  - Key info: Short product blurb; primary Play button for authenticated users; Login and Register buttons for visitors.
  - Components: Page shell, intro/CTA, auth-aware action area.
  - UX/A11y/Security: Public; Play routes to `/game/current` if authenticated, otherwise `/login?returnUrl=/game/current`.

- About
  - Path: `/about`
  - Purpose: Public information about the prototype.
  - Key info: Product overview, links to Privacy/Cookies.
  - Components: Page shell, static content.
  - UX/A11y/Security: Public, no cookies required.
  - User stories: US‑005, US‑006.

- Gallery
  - Path: `/gallery`
  - Purpose: Public screenshots or art.
  - Key info: Images and captions.
  - Components: Grid of images, lightbox (optional).
  - UX/A11y/Security: Alt text for images; public.
  - User stories: US‑005.

- Privacy
  - Path: `/privacy`
  - Purpose: Privacy policy and retention details.
  - Key info: Analytics consent, retention window, delete-account effects.
  - Components: Page shell, anchor navigation.
  - UX/A11y/Security: Public; readable typography.
  - User stories: US‑006.

- Cookies
  - Path: `/cookies`
  - Purpose: Cookie and analytics usage.
  - Key info: Consent options, cookie names, durations.
  - Components: Page shell.
  - UX/A11y/Security: Public; link from consent banner.
  - User stories: US‑006.

- Login
  - Path: `/login`
  - Purpose: Authenticate with email/password.
  - Key info: Email, password; error messaging; verification prompt.
  - Components: Form; modals `?modal=forgot`, `?modal=verify`.
  - UX/A11y/Security: Trap focus; CSRF bootstrap on app init; redirect to `/game/current` with `returnUrl` support.
  - User stories: US‑001, US‑002, US‑004, US‑005.

- Register
  - Path: `/register`
  - Purpose: Create account; prompt email verification.
  - Key info: Email, password; terms acknowledgment.
  - Components: Form; post-register verify notice modal.
  - UX/A11y/Security: Strong password hints; rate-limit feedback.
  - User stories: US‑001, US‑008.

- Game (Map Shell)
  - Path: `/game/current` (guard → `/game/:id`)
  - Purpose: Primary gameplay surface, map-first.
  - Key info: Turn number, units/cities with stats, city reach, selection details, pending actions, autosave status.
  - Components: Five-canvas stack (base tiles, grid, features, units, overlays); Top bar (turn, status pill); Bottom center panel (Unit/City details only, collapses when none); Right action rail (Saves, Help); Bottom-right End Turn; Toasts; Turn Log side panel (collapsible, session-scoped); Banners (idle countdown, multi-tab control, offline/rate-limit); Consent banner; AI overlay.
  - UX/A11y/Security: Pointer-anchored zoom, instant pan; selection → preview → commit; ESC/right-click cancel; ≥32×32 targets; aria-live; CSRF/idempotency on actions; single-controller tab behavior.
  - User stories: US‑010, US‑020..US‑026, US‑030..US‑034, US‑040..US‑043, US‑015, US‑018.

- Start New Game (Confirm)
  - Path: `/game/current?modal=start-new`
  - Purpose: Confirm creation when single active game exists.
  - Key info: Message about finishing/deleting current game; checkbox “I understand”.
  - Components: Modal with confirm button; link to “Delete current game instead”.
  - UX/A11y/Security: Focus trap; session persistence of “Don’t show again”.
  - User stories: US‑010.

- Saves
  - Path: `/game/:id?modal=saves[&tab=manual|autosaves]`
  - Purpose: Manual save, load saves, view autosaves.
  - Key info: Manual slots (1..3) with name, turnNo, timestamp, expiresAt; Autosaves (latest five) with timestamp, expiresAt.
  - Components: Tabs (Manual default, Autosaves); Slot cards; Inline rename with overwrite confirm; Load/Delete (manual only).
  - UX/A11y/Security: Disabled when `turnInProgress=true`; deduped autosave toasts; expiry copy from `expiresAt`.
  - User stories: US‑011, US‑012, US‑013, US‑015, US‑016, US‑017, US‑019.

- Settings
  - Path: `/game/:id?modal=settings`
  - Purpose: Player preferences.
  - Key info: Grid toggle (default OFF), invert scroll zoom, debug flag (dev-only).
  - Components: Switches; description text.
  - UX/A11y/Security: Persist safe items per session; no auth secrets stored.

- Help
  - Path: `/game/:id?modal=help`
  - Purpose: Cheatsheet, color legend, about controls.
  - Key info: Hotkeys (E, N, G, ESC, +/-), color legend; zoom/pan tips.
  - Components: Tabs or sections; links to Privacy/Cookies.

- Account Delete
  - Path: `/game/:id?modal=account-delete`
  - Purpose: Irreversible delete confirmation.
  - Key info: Data deletion scope; type-to-confirm (DELETE or email).
  - Components: Two-step confirm; final action button.
  - UX/A11y/Security: Focus trap; post-success logout → `/about`.
  - User stories: US‑007.

- Session Expired / Re-auth
  - Path: `/game/:id?modal=session-expired`
  - Purpose: Resume session after idle timeout.
  - Key info: Sign-in prompt; explains idle expiry.
  - Components: Modal; “Login” button preserves return URL; optional “Stay signed in” pre-expiry banner calls `/auth/keepalive`.
  - User stories: US‑004.

- Error: Schema Mismatch
  - Path: `/game/:id?modal=error-schema`
  - Purpose: Block incompatible loads.
  - Key info: Code `SCHEMA_MISMATCH`; details; actions.
  - Components: Blocking dialog with “Start New Game” (primary), “View Details”.
  - User stories: US‑018.

- Error: AI Timeout
  - Path: `/game/:id?modal=error-ai`
  - Purpose: Handle `AI_TIMEOUT` failures.
  - Key info: Retry guidance; Report by email link.
  - Components: Blocking dialog; “Retry” and “Report Issue (mailto:)”.

- Victory/Defeat Overlay
  - Path: `/game/:id#result` (overlay only)
  - Purpose: Show outcome and next steps.
  - Key info: Turns taken, cities captured.
  - Components: Full-screen overlay; “Start New Game” (primary), “View Saves”, “About”.
  - User stories: Analytics `game_finish` emission.

- Unsupported Browser Screen
  - Path: `/unsupported`
  - Purpose: Block when required APIs are missing.
  - Key info: Supported browser versions; no fallback rendering.
  - Components: Static page with matrix and links.


## 3. User Journey Map

- First session (no active game)
  1) Visitor lands on `/` (Landing). If authenticated: clicks “Play” → `/game/current`. If not: clicks Login/Register → `/login` or `/register`.
  2) `/login` → authenticate (or register) → redirect to `/game/current`.
  3) Guard finds no active game → opens `?modal=start-new` → confirm → `POST /games`.
  4) Bootstrap in parallel: `GET /games/{id}/state` (ETag-enabled), `GET /unit-definitions`, `GET /maps/{code}/tiles`.
  5) Map shell mounts; consent banner shows; grid off by default.
  6) Select unit → path preview → second click commits move (`POST /games/{id}/actions/move`) → write `{ state }` into cache.
  7) Attack flow similar via `/actions/attack`; previews reflect tie → both die.
  8) End turn: press E or button → pending-actions toast (not blocking) → `POST /games/{id}/end-turn` → AI overlay → poll state (If-None-Match) until `turnInProgress=false`.
  9) Autosave toast near End Turn (deduped per turn); continue loop until victory/defeat overlay.

- Save/Load
  - Open `?modal=saves` → Manual tab (slots 1..3) → Save (overwrite confirm) or Load; Autosaves tab lists latest five with `expiresAt` and “Expires in ~N days”.

- Idle/session
  - On inactivity, T‑60 s banner offers “Stay signed in” (`GET /auth/keepalive`); on expiry, `?modal=session-expired` enforces re-auth and preserves return URL.

- Multi-tab control
  - Opening a second tab triggers BroadcastChannel coordination; only one tab controls actions; others show read-only banner with “Make this tab active”.

- Error recovery
  - CSRF invalid → refresh `/auth/csrf` then retry once; Schema mismatch → blocking dialog; AI timeout → blocking dialog with Retry/Report; 429 → back off per headers; Offline → banner disables actions until online.


## 4. Layout and Navigation Structure

 - Routing and guards
  - Public: `/`, `/about`, `/gallery`, `/privacy`, `/cookies`, `/login`, `/register`.
  - Authenticated: `/game/current` guard resolves to the latest active game → `/game/:id`; if none, open `?modal=start-new` inside map shell.
  - Redirect `/hub` → `/game/current` for legacy/bookmarks.

- Modal navigation
  - Primary modals opened with `history.pushState` (e.g., `?modal=saves`); confirmations use `replaceState` (`?modal=confirm`) so Back closes confirm → modal → map.
  - Deep-linkable modals for Forgot/Verify/Account Delete/Settings/Help/Errors; Back/Forward support closes/open modals without leaving map.

- In-game layout (1080p baseline, ≥900p supported)
  - Canvas stack fills main content area (five layers: base, grid, features, units, overlays).
  - Top bar: turn number, small status pill (Online/Offline/Limited), link to Help.
  - Right action rail (top-right): Saves, Help buttons.
  - Bottom-right: End Turn button with spinner/overlay hooks.
  - Bottom center panel (contextual): Unit or City details only; collapses when none selected.
  - Side panel (collapsed by default): Turn Log showing last ~20 entries (sessionStorage).
  - Banners: idle countdown, offline, multi-tab control, rate-limit.
  - Consent banner (first run) at bottom.

- Keyboard and focus
  - Hotkeys scoped to map focus: E (End Turn), N (Next Unit), G (Grid toggle), ESC (Cancel), +/- (zoom), arrows/WASD (pan). Suspend while modals/inputs focused.
  - Focus-visible outlines preserved; dialogs trap focus and restore on close.


## 5. Key Components

- Map rendering and interaction
  - `MapCanvasStack` (manages 5 canvases and DPR sizing)
  - `CameraController` (pan/zoom, pointer anchoring, clamp to bounds)
  - `InteractionController` (pointer capture, picking priority, cancel handling)
  - `TileLayer`, `GridLayer`, `FeatureLayer`, `UnitLayer`, `OverlayLayer` (draw order)
  - `SpriteCache` with offscreen pre-renders (LRU, theme+DPR+scale buckets)
  - `TurnLogPanel` (collapsible, sessionStorage-backed)

- HUD and modals
  - `TopBar` (turn, status pill)
  - `BottomPanel` (Unit/City details: HP numeric bars, stats, reach count, production status/siege)
  - `ActionRail` (Saves, Help)
  - `EndTurnButton` (pending-actions toast, spinner, AI overlay hooks)
  - `SavesModal` (Manual/Autosaves tabs, rename with overwrite confirm, expiresAt display)
  - `SettingsModal` (Grid toggle, invert zoom; dev-only debug)
  - `HelpModal` (hotkeys, color legend)
  - `StartNewGameModal` (single active game confirm)
  - `AccountDeleteModal` (type-to-confirm, final action)
  - `SessionExpiredModal` (re-auth)
  - `ErrorSchemaModal`, `ErrorAiTimeoutModal`
  - `VictoryDefeatOverlay` (full-screen result)

- Providers and infrastructure
  - `ApiClient` (fetch with CSRF, Idempotency-Key, timeouts, ETag handling)
  - `CsrfProvider` (bootstraps `/auth/csrf`, rotates on login, broadcast to tabs)
  - `QueryProvider` (TanStack Query config: keys, staleTime, retry)
  - `UiStore` (Zustand: selection, camera, overlays, settings, banners, consent)
  - `ToastCenter` (aria-live polite)
  - `Banners` (idle keepalive, offline, multi-tab controller, rate-limit)
  - `ConsentBanner` (analytics opt-in)
  - `Router` (public/auth routes, `/game/current` guard to `/game/:id`, legacy redirect `/hub`  `/game/current`)
  - `ModalManager` (query-param modals `?modal=...`, Back/Forward semantics using `pushState`/`replaceState`, ESC/backdrop close, focus trap via `ModalContainer`)

- API compatibility and mappings (UI → API)
  - Game bootstrap: `GET /games?status=active&sort=lastTurnAt&order=desc&pageSize=1` → guard → `GET /games/{id}/state`, `GET /unit-definitions`, `GET /maps/{code}/tiles`.
  - Actions: `POST /games/{id}/actions/move` / `attack` / `end-turn` → `{ state }` write-through.
  - Saves: `GET /games/{id}/saves` (includes `expiresAt`), `POST /games/{id}/saves/manual`, `DELETE /games/{id}/saves/manual/{slot}`, `POST /saves/{saveId}/load`.
  - Analytics: `POST /analytics/batch` once per turn (after consent).
  - Auth/session: `GET /auth/csrf`, `GET /auth/keepalive`; Login/Register via Identity endpoints.

- User stories coverage (PRD → UI elements)
  - US‑001/002/003/004: Login/Register/Logout/Idle → Login/Register pages, SessionExpiredModal, Keepalive banner.
  - US‑005/006: Public About/Gallery/Privacy/Cookies pages.
  - US‑008: Rate-limit feedback → status pill, backoff toasts.
  - US‑010: Start new game → StartNewGameModal, guard at `/game/current`.
  - US‑011/012/013/015/016/017/019: Saves modal (Manual/Autosaves), expiresAt display, overwrite confirm, load flows.
  - US‑018: Schema gate → ErrorSchemaModal blocking dialog.
  - US‑020/021/022/023/024/025/026: Map rendering, pan/zoom, selection, preview path, move-on-second-click, 1UPT enforcement, pass-through friendlies, action-per-turn.
  - US‑030/031/032/033/034: Combat previews (advisory), ranged rules, rounding/min damage, counterattack, tie → both die indication.
  - US‑040/041/042/043: City regen/capture visualization, radius‑2 reach overlay, auto-harvest hints in city panel.

- Edge cases and error states
  - `TURN_IN_PROGRESS` on end-turn: poll `/games/{id}/state` until clear.
  - `SCHEMA_MISMATCH`/`MAP_SCHEMA_MISMATCH`: block, offer Start New.
  - `AI_TIMEOUT`: blocking retry/report dialog.
  - `CSRF_INVALID` (403): refresh token then retry once; otherwise re-auth modal.
  - `429`: back off per headers and surface limited status.
  - Offline: banner disables actions; re-enable on reconnect.
  - Multi-tab: read-only banner on background tabs; takeover flow.
