# View Implementation Plan Account Delete Modal

Reference: See [01 - routing-and-modal-framework-implementation-plan.md](./01 - routing-and-modal-framework-implementation-plan.md) for shared routing and modal framework implementation details.

## 1. Overview
Irreversible deletion confirmation flow requiring typed confirmation before enabling final action.

## 2. View Routing
- Path: `/game/:id?modal=account-delete`

## 3. Component Structure
- `AccountDeleteModal`
  - Warning text and consequences
  - Input: type DELETE (or email)
  - Checkbox: I understand
  - Primary button: Delete my account
  - Secondary: Cancel

## 4. Component Details
### AccountDeleteModal
- Description: Two-step confirm for account deletion; logs out and routes to `/about` upon success.
- Elements: Dialog, inputs, checkbox, buttons.
- Interactions: Enable button only when typed text matches and checkbox checked; on submit, call delete endpoint.
- Validation: Input matches required text; checkbox checked.
- Types: None (server endpoint not detailed in API plan; treat as server-managed).
- Props: `{ userEmail?: string }` (if using email for confirm).

## 5. Types
- None.

## 6. State Management
- Local input/checkbox state; pending flag.

## 7. API Integration
- Server-managed Identity endpoint for account deletion (TBD). After success, clear client state and navigate to `/about`.

## 8. User Interactions
- Type confirmation; check acknowledgment; submit; cancel.

## 9. Conditions and Validation
- Button disabled until validation satisfied.

## 10. Error Handling
- Show inline error on failure; allow retry; on auth loss, show session expired modal.

## 11. Implementation Steps
1. Implement modal with validation gating.
2. Wire submit to server delete endpoint.
3. On success, log out client and navigate to `/about`.
