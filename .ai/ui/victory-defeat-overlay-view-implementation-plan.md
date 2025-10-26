# View Implementation Plan Victory/Defeat Overlay

## 1. Overview
Fullscreen overlay shown when a game ends (victory/defeat). Presents summary and next actions.

## 2. View Routing
- Path: overlay-only within `/game/:id` (no standalone route); can use hash `#result` for deep-linking.

## 3. Component Structure
- `ResultOverlay`
  - `ResultSummary` (turns taken, cities captured)
  - `ResultActions` (Start New Game, View Saves, About)

## 4. Component Details
### ResultOverlay
- Description: Blocks map inputs behind overlay; focuses primary action.
- Elements: Title (Victory/Defeat), summary lines, buttons.
- Interactions: Primary: Start New Game (open `?modal=start-new`), Secondary: View Saves (`?modal=saves`), Tertiary: About.
- Validation: N/A.
- Types: `GameState` (for status and summary), optional local `ResultSummaryModel`.
- Props: `{ status: 'victory'|'defeat'; turns: number; citiesCaptured?: number }`.

## 5. Types
- `ResultSummaryModel` `{ status: 'victory'|'defeat'; turns: number; citiesCaptured?: number }`.

## 6. State Management
- Driven by `GameState.game.status==='finished'`; overlay visibility in UI store.

## 7. API Integration
- None; triggered by state change.

## 8. User Interactions
- Click actions to proceed; close not offered (end-of-game state).

## 9. Conditions and Validation
- Show only when status is finished.

## 10. Error Handling
- N/A.

## 11. Implementation Steps
1. Implement overlay with focus trap and disable map inputs.
2. Wire to `GameState` finish detection; populate summary.
3. Hook buttons to open modals or navigate as specified.

