# View Implementation Plan Error Schema Mismatch Modal

## 1. Overview
Blocks the UI when a save/load schemaVersion is incompatible. Offers to start a new game or view details.

## 2. View Routing
- Path: `/game/:id?modal=error-schema`

## 3. Component Structure
- `ErrorSchemaModal`
  - Error summary with code
  - Details collapsible (raw message/details)
  - Actions: Start New Game (primary), View Details (secondary)

## 4. Component Details
### ErrorSchemaModal
- Description: Blocking dialog opened after receiving `422 SCHEMA_MISMATCH` or `MAP_SCHEMA_MISMATCH`.
- Elements: Title, description, details area, primary/secondary actions.
- Interactions: Start New → open `?modal=start-new`; Details → expand/copy JSON details.
- Validation: None.
- Types: `ErrorResponse` `{ code, message, details? }`.
- Props: `{ error: ErrorResponse }`.

## 5. Types
- DTO: Error response payload from failed request.

## 6. State Management
- Store last blocking error in UI store to populate modal.

## 7. API Integration
- None directly; invoked after handling a 422 response.

## 8. User Interactions
- Read error; start a new game; copy details.

## 9. Conditions and Validation
- Only open for schema-related codes.

## 10. Error Handling
- N/A; modal is the handler.

## 11. Implementation Steps
1. Implement modal and error store slice.
2. Wire saves/load and game bootstrap handlers to open this modal on 422.

