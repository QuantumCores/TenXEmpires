# View Implementation Plan Saves Modal

Reference: See [01 - routing-and-modal-framework-implementation-plan.md](./01 - routing-and-modal-framework-implementation-plan.md) for shared routing and modal framework implementation details.

## 1. Overview
The Saves modal allows players to manage manual saves (slots 1..3) and load autosaves (last five). It is map-first and opens as a modal so the map remains visible.

## 2. View Routing
- Path: `/game/:id?modal=saves[&tab=manual|autosaves]`

## 3. Component Structure
- `SavesModal`
  - `Tabs` (Manual | Autosaves)
  - `ManualSavesTab`
    - `SaveSlotCard` × 3 (empty or filled)
  - `AutosavesTab`
    - `AutosaveItem` list (newest first)
  - `FooterActions` (Close)

## 4. Component Details
### SavesModal
- Description: Focus-trapped modal; reads/writes using saves endpoints; disables actions when `turnInProgress=true`.
- Elements: Dialog container, header with tabs, content area, footer.
- Interactions: Switch tabs; close on Back or explicit Close; open overwrite confirm.
- Validation: Disallow actions while game has `turnInProgress=true`.
- Types: `SavesListResponse`, `ManualSave`, `Autosave`, `SaveRequest`, `SaveLoadResponse` (`{ gameId, state: GameState }`).
- Props: `{ gameId: number; turnInProgress: boolean }`.

### ManualSavesTab / SaveSlotCard
- Description: Shows 3 fixed slots with name, turnNo, createdAt, expiresAt; supports Save, Load, Delete, Rename (via overwrite).
- Elements: Cards with metadata; buttons Save, Load, Delete; inline name input.
- Interactions: Save to slot with confirm on overwrite; Delete slot with confirm; Load applies state and closes modal.
- Validation:
  - Slot must be 1|2|3; if overwrite, show confirm.
  - Disabled when `turnInProgress=true`; surface `409 TURN_IN_PROGRESS` inline.
- Types: `ManualSave` `{ id, slot, turnNo, createdAt, name, expiresAt }`.
- Props: `{ saves: ManualSave[]; onSave(slot,name); onLoad(id); onDelete(slot) }`.

### AutosavesTab / AutosaveItem
- Description: Lists latest five autosaves; supports Load.
- Elements: List rows with createdAt (and expiresAt) and Load action.
- Interactions: Click Load to apply state and close.
- Validation: None other than disabled when `turnInProgress=true`.
- Types: `Autosave` `{ id, turnNo, createdAt, expiresAt }`.
- Props: `{ autosaves: Autosave[]; onLoad(id) }`.

## 5. Types
- DTOs
  - `GET /games/{id}/saves` → `{ manual: ManualSave[], autosaves: Autosave[] }` (plus `expiresAt` per decision)
  - `POST /games/{id}/saves/manual` → `{ id, slot, turnNo, createdAt, name, expiresAt }`
  - `DELETE /games/{id}/saves/manual/{slot}` → 204
  - `POST /saves/{saveId}/load` → `{ gameId, state: GameState }`
- View models
  - `OverwriteConfirm` `{ slot: 1|2|3; oldName?: string; newName: string }`

## 6. State Management
- React Query keys:
  - `['saves', gameId]` (staleTime: 60 s)
- Mutations:
  - Save manual → write result to `['saves', gameId]`
  - Delete manual → invalidate `['saves', gameId]`
  - Load → write `{ state }` to `['game', gameId]` and refresh; clear selection/camera if needed

## 7. API Integration
- Calls
  - `GET /games/{id}/saves`
  - `POST /games/{id}/saves/manual` `{ slot, name }`
  - `DELETE /games/{id}/saves/manual/{slot}`
  - `POST /saves/{saveId}/load`
- Headers: `X-XSRF-TOKEN` on non-GET; `Idempotency-Key` on POSTs; handle `422 SCHEMA_MISMATCH` with blocking dialog.

## 8. User Interactions
- Save to slot (with overwrite confirm); Delete slot; Load save/autosave; Switch tabs; Close modal.

## 9. Conditions and Validation
- Disable Save/Load/Delete when `turnInProgress=true`.
- Enforce slot range 1..3; sanitise name (1–40 chars, allowed charset); confirm overwrite.

## 10. Error Handling
- `409 TURN_IN_PROGRESS`: show inline message and disable controls.
- `422 SCHEMA_MISMATCH`: close modal and open error dialog.
- `404 SAVE_NOT_FOUND`: toast and refresh saves list.
- `429`: toast and suggest retry later.

## 11. Implementation Steps
1. Implement `SavesModal` scaffold with tabs and focus trap.
2. Add `useSavesQuery(gameId)` and mutations for save/delete/load.
3. Wire validations and disabled states; overwrite confirm flow.
4. On Load success, write `{ state }` to cache and close modal; clear selection.
5. Add expiresAt display and relative time.
