# View Implementation Plan Help Modal

## 1. Overview
Help modal showing hotkeys, color legend, and brief control tips.

## 2. View Routing
- Path: `/game/:id?modal=help`

## 3. Component Structure
- `HelpModal`
  - `HotkeysSection`
  - `LegendSection` (reach/path/targets/blocked/selection/focus/siege/grid)
  - `LinksSection` (Privacy, Cookies)

## 4. Component Details
### HelpModal
- Description: Informational modal with accessible content.
- Elements: Headings, lists, color swatches.
- Interactions: Close.
- Validation: Ensure contrast guidance present.
- Types: None.
- Props: None.

## 5. Types
- None.

## 6. State Management
- None.

## 7. API Integration
- None.

## 8. User Interactions
- Read-only; close modal.

## 9. Conditions and Validation
- Ensure CVD-safe colors and ≥4.5:1 contrast guidance.

## 10. Error Handling
- N/A.

## 11. Implementation Steps
1. Implement modal with sections and color tokens display.
2. Link to Privacy/Cookies.

