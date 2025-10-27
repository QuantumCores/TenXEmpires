# View Implementation Plan Settings Modal

Reference: See [01 - routing-and-modal-framework-implementation-plan.md](./01 - routing-and-modal-framework-implementation-plan.md) for shared routing and modal framework implementation details.

## 1. Overview
Modal providing player preferences that affect client-only behavior: Grid toggle (default OFF), Invert scroll zoom, and optional dev-only debug toggle.

## 2. View Routing
- Path: `/game/:id?modal=settings`

## 3. Component Structure
- `SettingsModal`
  - `SettingsList`
    - `ToggleRow` (Grid)
    - `ToggleRow` (Invert Zoom)
    - `ToggleRow` (Debug, dev-only)
  - `FooterActions` (Close)

## 4. Component Details
### SettingsModal
- Description: Focus-trapped dialog with simple toggles persisted in UI store and session/cookie (where safe).
- Main elements: Switch controls, labels, descriptions.
- Interactions: Toggle switches update UI store immediately.
- Validation: N/A (client-only settings).
- Types: `SettingsState`.
- Props: None.

## 5. Types
- `SettingsState` `{ gridOn: boolean; invertZoom: boolean; debug?: boolean }`

## 6. State Management
- Zustand store slice for settings; optionally mirror to `localStorage` or cookie for persistence per session.

## 7. API Integration
- None.

## 8. User Interactions
- Toggle settings; close modal.

## 9. Conditions and Validation
- N/A.

## 10. Error Handling
- N/A.

## 11. Implementation Steps
1. Implement modal with focus trap and ARIA roles.
2. Wire toggles to UI store and persistence.
3. Ensure grid/invertZoom take effect immediately on map.
