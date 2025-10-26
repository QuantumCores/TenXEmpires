# View Implementation Plan Error AI Timeout Modal

## 1. Overview
Blocks the UI when the AI exceeds allowed time or backend returns `AI_TIMEOUT`. Offers Retry and Report Issue.

## 2. View Routing
- Path: `/game/:id?modal=error-ai`

## 3. Component Structure
- `ErrorAiTimeoutModal`
  - Message area
  - Buttons: Retry, Report Issue (mailto link)

## 4. Component Details
### ErrorAiTimeoutModal
- Description: Blocking dialog opened after `AI_TIMEOUT` on end-turn.
- Elements: Title, details, actions.
- Interactions: Retry refetches `GET /games/{id}/state`; Report opens mailto with JSON details (code, requestId, gameId, turnNo, ts, browser).
- Validation: None.
- Types: `ErrorResponse`.
- Props: `{ gameId: number; lastError: ErrorResponse }`.

## 5. Types
- Error response payload.

## 6. State Management
- UI store for last error metadata; include `requestId` if available.

## 7. API Integration
- Retry: `GET /games/{id}/state` with ETag; if unchanged, consider suggesting another Try or start-new.

## 8. User Interactions
- Retry fetching state; Report issue via mailto.

## 9. Conditions and Validation
- Only open for `AI_TIMEOUT` code.

## 10. Error Handling
- If repeated timeouts, keep modal and suggest “Contact support”.

## 11. Implementation Steps
1. Implement modal with actions.
2. Wire end-turn mutation error handler to open modal on `AI_TIMEOUT`.

