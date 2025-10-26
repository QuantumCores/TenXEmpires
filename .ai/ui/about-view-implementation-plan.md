# View Implementation Plan About

## 1. Overview
Public informational page describing TenX Empires MVP, linking to Privacy and Cookie policies, with a Play CTA that routes to login or game depending on auth state.

## 2. View Routing
- Path: `/about`

## 3. Component Structure
- `AboutPage`
  - `AboutHeader`
  - `AboutContent`
  - `AboutCTA` (Play/Login/Register)
  - `FooterLinks`

## 4. Component Details
### AboutPage
- Component description: Static page shell for about content with CTA.
- Main elements: `<main>`, headings, paragraphs, CTA area, footer.
- Handled events: CTA clicks (Play/Login/Register navigation).
- Handled validation: N/A.
- Types: `AuthStatus` (from landing approach).
- Props: None.

### AboutContent
- Component description: Product overview and goals extracted from PRD.
- Main elements: Headings, list of goals, features.
- Handled interactions: None.
- Handled validation: N/A.
- Types: None.
- Props: None.

### AboutCTA
- Component description: Auth-aware CTA similar to landing.
- Main elements: Play or Login/Register buttons.
- Handled interactions: Clicks to `/game/current` or `/login?returnUrl=/game/current` or `/register?returnUrl=/game/current`.
- Handled validation: N/A.
- Types: `AuthStatus`.
- Props: `{ auth: AuthStatus }`.

## 5. Types
- `AuthStatus` (as defined in landing plan).

## 6. State Management
- Optional `useAuthStatusQuery` to alter CTA.

## 7. API Integration
- None (optional `GET /games` for auth detection as in landing).

## 8. User Interactions
- Navigate using CTA; link to Privacy/Cookies.

## 9. Conditions and Validation
- If authenticated, show Play; else Login/Register.

## 10. Error Handling
- Same soft handling for auth detection failure as landing (optional banner).

## 11. Implementation Steps
1. Scaffold About page structure with content from PRD.
2. Add `AboutCTA` with auth-aware buttons.
3. Link to `/privacy` and `/cookies` in footer.
4. Content outline (from PRD): Overview of MVP scope (single-player, turn-based, 20x15 hex map), goals (end-to-end loop, saves/autosaves, fast AI < 500 ms, small bundle), audience, and non-functional targets (bundle size, AI timing). Include links to Privacy/Cookies and brief analytics/retention note.
