# View Implementation Plan Game Map

## 1. Overview
The Game Map view is the primary authenticated gameplay surface. It renders the fixed 20x15 hex map, units, and cities; supports selection → preview → commit for move/attack; manages turn flow including End Turn and AI processing; exposes in-game modals (Saves, Settings, Help); and surfaces toasts, banners, and a collapsible Turn Log. It treats the server GameState as the sole source of truth.

## 2. View Routing
- Path: `/game/current` (guard resolves to latest active) and canonical `/game/:id`
- Guard: If at `/game/current`, fetch `GET /games?status=active&sort=lastTurnAt&order=desc&pageSize=1`; if none, open `?modal=start-new` inside map shell; else route to `/game/:id`.

## 3. Component Structure
- `GameMapPage`
  - `TopBar` (turn number, status pill, Help link)
  - `MapCanvasStack`
    - `TileLayer`
    - `GridLayer`
    - `FeatureLayer` (cities/resources/reach rings)
    - `UnitLayer`
    - `OverlayLayer` (hover/path/targets)
  - `BottomPanel` (Unit/City details; collapses when none)
  - `ActionRail` (Saves, Help)
  - `EndTurnButton` (spinner/overlay hooks)
  - `TurnLogPanel` (collapsible, session-scoped)
  - `ToastsCenter` (aria-live polite)
  - `Banners` (idle keepalive, offline, multi-tab, rate-limit)
  - Modals via query: Saves/Settings/Help/StartNew/ErrorSchema/ErrorAI/SessionExpired

## 4. Component Details
### GameMapPage
- Description: Orchestrates bootstrap, polling during AI turns, and modal/query state; wires hotkeys and banners.
- Main elements: Page layout shell containing canvases and HUD.
- Interactions: Keyboard E/N/G/ESC/+/-/WASD; open modals; responds to network status; manages multi-tab control.
- Validation: Blocks actions when `turnInProgress=true` or mutation pending; allows pan/zoom.
- Types: `GameState`, `UnitDefinition`, `MapTilesResponse`, `TurnSummary`, `ErrorResponse`, `GameId`.
- Props: Route params `{ id?: number }`.

### MapCanvasStack (+ layers)
- Description: Five-canvas rendering with DPR sizing, CSS transform zoom, and offscreen sprite caches.
- Main elements: `<canvas>` x5; draw order tiles → grid → features → units → overlays.
- Interactions: Pointer capture; picking by priority (Units > City > Feature > Tile > Grid); path preview and range highlights; right-click/ESC cancel.
- Validation: Client-side geometry checks for preview only; server authoritative on commit.
- Types: `CameraState`, `SelectionState`, `InteractionConfig`.
- Props: `{ state: GameState; unitDefs: UnitDefinition[]; tiles: MapTile[] }` and camera/selection setters.

### BottomPanel
- Description: Shows selected Unit or City info; collapses when nothing selected.
- Main elements: HP bars (numeric), attack/defence, move points, range; city worked tiles count, resources, production, siege tag.
- Interactions: Center camera; Next Unit CTA.
- Validation: None (display from `GameState`).
- Types: `GameState`, local `UnitView`, `CityView` view models.
- Props: `{ selection, state, onCenter, onNextUnit }`.

### EndTurnButton
- Description: Commits end turn; shows pending-actions toast; triggers AI overlay and polling.
- Main elements: Button with spinner; optional countdown/backoff indicators.
- Interactions: Click or E hotkey → `POST /games/{id}/end-turn` with Idempotency-Key; handle disabled state during in-flight.
- Validation: Disabled if `turnInProgress=true` or mutation pending.
- Types: `EndTurnRequest`, `{ state: GameState }` response.
- Props: `{ gameId, disabled }`.

### TurnLogPanel
- Description: Collapsible panel listing last ~20 combat/turn events; sessionStorage-persisted per gameId.
- Main elements: List with icons and concise lines.
- Interactions: Toggle open; optional “center on event” action.
- Validation: None.
- Types: `TurnLogEntry`.
- Props: `{ entries, onToggle }`.

### Banners/Toasts/Overlays
- Description: Idle keepalive, offline, multi-tab control, rate-limit; toasts for autosave, pending actions; AI overlay escalations.
- Interactions: Keepalive call; takeover flow; dismiss banners;
- Validation: None.
- Types: small local structs.
- Props: N/A (providers/hooks).

## 5. Types
- DTOs (TenXEmpires.Server.Domain/DataContracts)
  - `GameState` (see API plan projection)
  - `Lookups.UnitDefinition`
  - `Lookups.MapTile`
  - `Games.GameSummary`
  - `Errors.ErrorResponse` `{ code: string, message: string, details?: object }`
- View models
  - `CameraState` `{ scale: number; offsetX: number; offsetY: number }`
  - `SelectionState` `{ kind: 'unit'|'city'|null; id?: number }`
  - `InteractionConfig` `{ radii: { unit: number; city: number; feature: number }; thresholds: { pan: number } }`
  - `TurnLogEntry` `{ id: string; at: string; kind: 'move'|'attack'|'city'|'save'|'system'; text: string }`

## 6. State Management
- React Query keys:
  - `['game', id]` (staleTime: 0); bootstrap on mount; write-through on mutations
  - `['unit-defs']` (Infinity)
  - `['map-tiles', code]` (Infinity; ETag metadata)
- Zustand UI store:
  - `camera`, `selection`, `gridOn`, `invertZoom`, `isAiOverlayVisible`, `turnLog` (sessionStorage), `banners`
- Mutation queue per game prevents concurrent actions.

## 7. API Integration
- Read
  - `GET /games/{id}/state` → `GameState` (ETag; poll 1 Hz during AI with If-None-Match)
  - `GET /unit-definitions` → `{ items: UnitDefinition[] }` (cache forever)
  - `GET /maps/{code}/tiles` → `{ items: MapTile[] }` (cache forever)
- Mutations (all include `Idempotency-Key`, return `{ state: GameState }`)
  - `POST /games/{id}/actions/move` `{ unitId, to: { row, col } }`
  - `POST /games/{id}/actions/attack` `{ attackerUnitId, targetUnitId }`
  - `POST /games/{id}/end-turn` `{}`
- Errors → UI mapping
  - `409 TURN_IN_PROGRESS` (poll until clear), `422 ILLEGAL_MOVE`, `409 ONE_UNIT_PER_TILE`, `409 NO_ACTIONS_LEFT`, `OUT_OF_RANGE`, `INVALID_TARGET`, `AI_TIMEOUT`

## 8. User Interactions
- Select unit/city; preview paths/ranges; second click commits move or attack.
- End Turn via button/E; shows pending-actions toast (units with `hasActed=false`); AI overlay with escalations at 2 s and 5 s.
- Toggle grid (G), Next Unit (N), ESC cancel, +/- zoom, WASD/Arrows pan.
- Open modals via action rail or hotkeys.

## 9. Conditions and Validation
- Disable moves/attacks when `turnInProgress=true` or unit `hasActed=true`.
- Validate that destination differs and is reachable in preview; server enforces rules.
- Respect 1UPT (server returns `ONE_UNIT_PER_TILE`)
- Ranged never counterattacked; tie rule: both die when both < 0 HP (match previews).

## 10. Error Handling
- Centralized error mapping to toasts/dialogs; log code and message.
- `403 CSRF_INVALID`: refresh CSRF then retry once; on failure, route to login with returnUrl.
- `429`: show limited banner; back off polling to 2–3 s.
- Offline: banner; disable actions; re-enable on reconnect.

## 11. Implementation Steps
1. Add routes `/game/current` and `/game/:id` with guard logic.
2. Implement React Query loaders for GameState, unit-defs, and map-tiles.
3. Build `MapCanvasStack` with five layers and DPR sizing; wire `CameraController` and `InteractionController`.
4. Implement selection/preview rendering; integrate move/attack mutations with write-through `{ state }`.
5. Add `EndTurnButton` with pending-actions toast, AI overlay, and poll loop.
6. Implement `BottomPanel`, `TopBar`, `ActionRail`, `TurnLogPanel`, `ToastsCenter`, `Banners`.
7. Wire hotkeys and modal query handling; suspend hotkeys while any modal is open.
8. Handle error codes and CSRF/idle flows; test rate-limit/offline.
9. Performance pass: cache sprites offscreen; ETag use; verify 1080p and ≥900p.

