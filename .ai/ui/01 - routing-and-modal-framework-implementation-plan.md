# View Implementation Plan Routing and Modal Framework

## 1. Overview
The Routing and Modal Framework provides application-wide navigation and modal behavior for the TenX Empires MVP. It defines public and authenticated routes, the `/game/current` guard that resolves to `/game/:id`, and a query-parameter-driven modal system (`?modal=...`) with Back/Forward support, ESC/backdrop close, and focus trapping. This framework underpins all views and modals so downstream screens remain simple and consistent.

## 2. View Routing
- Base router with routes:
  - `/` Landing (public)
  - `/about`, `/gallery`, `/privacy`, `/cookies` (public)
  - `/login`, `/register` (public; redirect back via `returnUrl`)
  - `/game/current` (authenticated guard; resolves to `/game/:id` or opens `?modal=start-new` when no active game)
  - `/game/:id` (authenticated; hosts map shell and modal overlays)
  - `/unsupported` (public block page)
  - Redirect legacy `/hub` → `/game/current`
- Modals use query string: `?modal=<key>[&tab=...][&confirm=true]`. Back/Forward closes confirms → modal → map in order.

## 3. Component Structure
- `AppRouter`
  - `Routes`
    - `PublicRoutes`
      - `/`, `/about`, `/gallery`, `/privacy`, `/cookies`, `/login`, `/register`, `/unsupported`
    - `GameCurrentGuardRoute` → resolves `/game/current` to `/game/:id` or opens `?modal=start-new`
    - `/game/:id`
      - `GameShell` (host view)
        - `ModalManager` (renders chosen modal inside `ModalContainer`)
          - `SavesModal` | `SettingsModal` | `HelpModal` | `StartNewGameModal` | `AccountDeleteModal` | `SessionExpiredModal` | `ErrorSchemaModal` | `ErrorAiTimeoutModal`

## 4. Component Details
### AppRouter
- Component description: Top-level router configuration and provider mounting point.
- Main elements: Router provider, route declarations, legacy redirect.
- Handled interactions: Route transitions, redirects.
- Handled validation:
  - Ensures `/game/current` is guarded and resolves correctly.
  - Blocks navigation to `/game/:id` without authentication.
- Types: `RouteConfig`, `AppRoute`, `RouteGuardResult`.
- Props: None.

### GameCurrentGuardRoute
- Component description: Guard handler for `/game/current` that routes to the latest active game or opens `?modal=start-new` in the map shell if none.
- Main elements: Guard effect, navigation action (replace or push by context).
- Handled interactions: On mount, call Active Game lookup, navigate accordingly.
- Handled validation:
  - Authenticated user detected via `GET /games` success; `401`/`403` treated as unauthenticated (redirect to `/login?returnUrl=/game/current`).
  - If `items.length > 0`, navigate to `/game/:id` using the latest active game.
  - If none, navigate to `/game/:id` (new game container) and open `?modal=start-new`.
- Types: `GamesListResponse`, `GameSummary`, `GuardOutcome`.
- Props: None.

### ModalManager
- Component description: Central controller that reads `modal` from search params and mounts the correct modal component.
- Main elements: Mapping `{ [ModalKey]: Component }`, `ModalContainer`, Back/Forward coordination.
- Handled interactions:
  - Open via `history.pushState` for primary modals.
  - Use `history.replaceState` for confirm steps so Back unwinds confirm → modal → map.
  - Close on ESC, backdrop, or history Back.
- Handled validation:
  - Valid `modal` keys only; unknown keys ignored and removed from URL.
  - When `turnInProgress=true`, disable action-triggering modals.
- Types: `ModalKey`, `ModalRouteState`, `ModalAction`.
- Props: `gameId: number` (for in-game modals), optional `status: 'online'|'offline'|'limited'`.

### ModalContainer
- Component description: Generic, accessible overlay wrapper used by all modals.
- Main elements: Portal root, dialog role, labelled-by, inert background, focus trap, scroll lock.
- Handled interactions: ESC/backdrop click close, focus restore on close.
- Handled validation: Trap focus; prevent interaction with background; ARIA attributes.
- Types: `FocusTrapHandle`.
- Props: `onRequestClose: () => void`, `initialFocusRef?: Ref`, `closeOnBackdrop?: boolean`.

### QueryParamSync
- Component description: Utilities and hooks for reading/writing `modal` query params with push/replace semantics.
- Main elements: `useModalParam`, `openModal`, `closeModal`, `openConfirm`.
- Handled interactions: Writes to `history` API; syncs UI store and search params.
- Handled validation: Sanitizes values; dedupes no-op state changes.
- Types: `HistoryAction`, `ModalRouteState`.
- Props: None.

## 5. Types
- Backend DTOs (from TenXEmpires.Server.Domain\DataContracts)
  - `GamesListResponse`: `{ items: GameSummary[], page: number, pageSize: number, total?: number }`
  - `GameSummary`: `{ id: number, status: string, turnNo: number, mapId: number, mapSchemaVersion: number, startedAt: string, finishedAt?: string, lastTurnAt?: string }`
- New ViewModel types
  - `AppRoute`:
    - `name: 'root'|'about'|'gallery'|'privacy'|'cookies'|'login'|'register'|'gameCurrent'|'game'|'unsupported'`
    - `path: string`
  - `RouteGuardResult`:
    - `allowed: boolean`
    - `redirectTo?: string` (absolute path if not allowed)
    - `openModal?: ModalKey`
  - `ModalKey`: `'saves'|'settings'|'help'|'account-delete'|'start-new'|'session-expired'|'error-schema'|'error-ai'`
  - `ModalRouteState`:
    - `modal?: ModalKey`
    - `tab?: string`
    - `confirm?: boolean`
  - `HistoryAction`: `'push'|'replace'`
  - `GuardOutcome`:
    - `targetPath: string`
    - `historyAction: HistoryAction`
    - `openModal?: ModalKey`
  - `FocusTrapHandle`:
    - `trap(): void`
    - `release(): void`

## 6. State Management
- Providers
  - `CsrfProvider` initializes CSRF token on app init.
  - `QueryProvider` configures TanStack Query keys, stale times, and retries.
  - `UiStore` (Zustand) holds ephemeral UI flags: `isModalOpen`, `modalKey`, banners, consent; derived from search params when possible.
- Hooks and state
  - `useAuthStatusQuery` (Landing) remains independent; `GameCurrentGuardRoute` uses a leaner active-game query as guard.
  - `useModalParam` reads/writes `ModalRouteState` from search params and mirrors to `UiStore`.
  - `useBackstackCloseBehavior` ensures confirm → modal → map Back order.

## 7. API Integration
- `GET /games?status=active&sort=lastTurnAt&order=desc&pageSize=1`
  - Purpose: Determine latest active game for `/game/current` guard.
  - Request: none (cookies included)
  - Response: `GamesListResponse`
  - Errors: `401/403` unauthenticated → redirect to `/login?returnUrl=/game/current`; network/5xx → show soft banner, default to Landing where relevant.
- `GET /auth/csrf`
  - Purpose: CSRF bootstrap on app init; sets `XSRF-TOKEN` cookie.
  - Response: `200 OK` with cookie; errors retried once on demand.

## 8. User Interactions
- Open modal via UI action → `openModal(key, { tab })` uses `history.pushState` and sets `?modal=key[&tab=...]`.
- Open confirm step inside a modal → `openConfirm()` uses `history.replaceState` to add `&confirm=true` so Back unwinds confirm first.
- Close modal via ESC/backdrop → remove `modal` from query via `history.replaceState` for immediate close without adding history entries.
- Navigate Back → `ModalManager` interprets popstate; if confirm on stack, drop confirm; else close modal; else navigate to previous route.

## 9. Conditions and Validation
- Auth and guard
  - If unauthenticated on `/game/current`, redirect to `/login?returnUrl=/game/current`.
  - If active game exists, navigate to `/game/:id`; otherwise open `?modal=start-new` in map shell context.
- Modal validity
  - Only allow known `ModalKey` values; sanitize unknowns by cleaning query and not rendering any modal.
- Accessibility
  - Focus trap active while any modal open; background inert; restore focus to last active element on close.

## 10. Error Handling
- Guard fetch fails (network/5xx): show non-blocking banner; do not break navigation; allow manual retry from user actions.
- `401/403` on guard: treat as visitor; route to login with `returnUrl`.
- Invalid/unknown query params: strip and continue.
- Offline: `UiStore` raises Offline banner; modals remain navigable; action buttons disabled where appropriate.
- Rate limited `429`: show Limited banner; back off per headers; avoid thrashing history.

## 11. Implementation Steps
1. Add `AppRouter` with base routes and legacy redirect `/hub` → `/game/current`.
2. Implement `GameCurrentGuardRoute`:
   - Call `GET /games?status=active&sort=lastTurnAt&order=desc&pageSize=1`.
   - On success with active game → navigate to `/game/:id` (replace).
   - On success with none → navigate to `/game/:id` and open `?modal=start-new` (push for deep-linkability).
   - On `401/403` → redirect to `/login?returnUrl=/game/current`.
3. Create `ModalManager` with a map of `ModalKey` to component; wire to search params.
4. Build `ModalContainer` with portal root, dialog semantics, focus trap, ESC/backdrop close.
5. Implement `QueryParamSync` hooks: `useModalParam`, `openModal`, `openConfirm`, `closeModal` using push/replace semantics.
6. Integrate `UiStore` sync for modal open state; ensure Back/Forward support via `popstate` handling.
7. Add tests for:
   - Guard navigation with/without active games and unauthenticated users.
   - Modal open/close, confirm Back unwinding order, ESC/backdrop behavior.
8. Verify A11y: focus trap, labelled-by, restore focus on close; tab order; `aria-live` for banners.
9. Performance check: ensure no heavy prefetch in router/modals; lazy-load modal components if needed.
10. Document usage patterns for downstream views and reference this plan from UI architecture.

