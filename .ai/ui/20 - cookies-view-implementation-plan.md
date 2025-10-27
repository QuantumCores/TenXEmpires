# View Implementation Plan Cookies

Reference: See [01 - routing-and-modal-framework-implementation-plan.md](./01 - routing-and-modal-framework-implementation-plan.md) for shared routing and modal framework implementation details.

## 1. Overview
Public Cookies page describing cookie usage and consent; links back to Privacy.

## 2. View Routing
- Path: `/cookies`

## 3. Component Structure
- `CookiesPage`
  - `CookiesHeader`
  - `CookiesContent`
  - `FooterLinks`

## 4. Component Details
### CookiesPage
- Description: Static legal content; explain consent banner behavior and cookie names (XSRF-TOKEN, auth cookies, consent).
- Main elements: Headings, paragraphs, list of cookies with purpose/duration.
- Interactions: Links to Privacy.
- Validation: N/A.
- Types: None.
- Props: None.

## 5. Types
- None.

## 6. State Management
- None.

## 7. API Integration
- None.

## 8. User Interactions
- Read-only; follow links.

## 9. Conditions and Validation
- Ensure banner behavior and retention are documented.

## 10. Error Handling
- N/A.

## 11. Implementation Steps
1. Build page content.
2. Link to Privacy.
