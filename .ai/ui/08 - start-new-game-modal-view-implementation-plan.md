# View Implementation Plan Start New Game Modal

Reference: See [01 - routing-and-modal-framework-implementation-plan.md](./01 - routing-and-modal-framework-implementation-plan.md) for shared routing and modal framework implementation details.

## 1. Overview
Confirms creating a new game when only one active game is allowed. Provides a primary "Start New Game" action and a link to delete the current active game instead.

## 2. View Routing
- Path: `/game/current?modal=start-new`

## 3. Component Structure
- `StartNewGameModal`
  - Message area
  - Checkbox: "I understand"
  - Primary button: Start New Game
  - Secondary link: Delete current game instead

## 4. Component Details
### StartNewGameModal
- Description: Blocking confirm dialog; on confirm, posts to create a new game.
- Elements: Dialog, message, checkbox, buttons.
- Interactions: Toggle checkbox; click primary to create game.
- Validation: Primary disabled until checkbox checked.
- Types: `{ state: GameState }` response from POST /games; `ErrorResponse`.
- Props: `{ hasActiveGame: boolean; currentGameId?: number }`.

## 5. Types
- DTOs: `POST /games` `{ mapCode?: string, settings?: object }` → `{ id: number, state: GameState }`

## 6. State Management
- None beyond mutation state and routing.

## 7. API Integration
- `POST /games` (with `Idempotency-Key`)
- On success: navigate to `/game/:id`; write `{ state }` to cache.
- On `409 GAME_LIMIT_REACHED`: show inline guidance and offer delete.
- Delete path: `DELETE /games/{id}` (if user chooses delete current instead) → then `POST /games` again.

## 8. User Interactions
- Check acknowledgment; click Start New; optionally click delete current.

## 9. Conditions and Validation
- Disable actions when a mutation is in-flight.

## 10. Error Handling
- `422 MAP_SCHEMA_MISMATCH`: open error dialog; do not create.
- `403/401`: route to login with returnUrl.
- `429`: show limited banner; suggest retry.

## 11. Implementation Steps
1. Implement modal with checkbox‑gated primary.
2. Wire `POST /games` mutation with Idempotency‑Key and write‑through cache.
3. Handle `409 GAME_LIMIT_REACHED` with delete‑current flow (calls `DELETE /games/{id}`).
4. Route to `/game/:id` on success and close modal.
