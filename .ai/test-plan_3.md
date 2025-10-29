# Test Plan for TenX Empires (v3.0)

**Project:** TenX Empires - Turn-Based 4X Strategy Game  
**Document Version:** 3.0  
**Last Updated:** 2025-10-29  
**Author:** QA Team

---

## Table of Contents
1. [Introduction and Objectives](#1-introduction-and-objectives)
2. [Scope of Testing](#2-scope-of-testing)
3. [Requirements Traceability Matrix](#3-requirements-traceability-matrix)
4. [Types of Tests](#4-types-of-tests)
5. [Detailed Test Scenarios](#5-detailed-test-scenarios)
6. [Test Data Management](#6-test-data-management)
7. [Test Environment](#7-test-environment)
8. [Testing Tools](#8-testing-tools)
9. [CI/CD Integration](#9-cicd-integration)
10. [Test Schedule](#10-test-schedule)
11. [Release Acceptance Criteria](#11-release-acceptance-criteria)
12. [Roles and Responsibilities](#12-roles-and-responsibilities)
13. [Bug Reporting Procedures](#13-bug-reporting-procedures)
14. [Risk Management](#14-risk-management)

---

## 1. Introduction and Objectives

This document outlines the comprehensive testing strategy for the **TenX Empires** project, a turn-based 4X strategy game featuring a .NET 8 backend, React/TypeScript frontend, and PostgreSQL database with Row-Level Security (RLS).

### 1.1 Primary Objectives

*   **Ensure Quality and Stability:** Verify that the application is robust, reliable, and free of critical defects before release.
*   **Validate Functional Requirements:** Confirm that all specified features, from user authentication to core gameplay mechanics, function as intended.
*   **Verify State Consistency:** Guarantee that the game state remains consistent between the server (source of truth) and the client, especially during actions and turn transitions.
*   **Assess Performance and Security:** Ensure the application is performant under typical loads and secure against common web vulnerabilities, including proper RLS enforcement.
*   **Guarantee a Positive User Experience:** Validate that the user interface is intuitive, responsive, and provides clear feedback for all interactions and error conditions.

### 1.2 Success Metrics

*   ≥80% code coverage for backend services (GameService, TurnService, ActionService)
*   ≥70% code coverage for critical frontend components (auth, game map, save/load)
*   100% P0 test case pass rate
*   Zero open P0/P1 defects at release
*   All performance targets met (detailed in Section 5.6)
*   No High/Critical security vulnerabilities

---

## 2. Scope of Testing

### 2.1 In-Scope

*   **Frontend (Client Application):**
    *   All React components and UI elements
    *   Client-side state management (Zustand and React Query)
    *   User authentication flows (Login, Registration, Password Reset, Email Verification)
    *   API integration and server communication
    *   Core gameplay interactions on the HTML5 Canvas map
    *   Modals, notifications, and other UI feedback mechanisms
    *   Client-side routing and browser history management
    *   Session management (IdleSessionProvider, keepalive, SessionExpiredModal)
    *   CSRF token handling (CsrfProvider)

*   **Backend (Server API):**
    *   All REST API endpoints (AuthController, GamesController, SavesController, MapsController, etc.)
    *   Server-side business logic (game actions, turn processing, combat, AI)
    *   Database interactions (data persistence, retrieval, and integrity)
    *   Authentication, authorization, and session management (ASP.NET Core Identity)
    *   Security features: CSRF protection, rate limiting, Row-Level Security (RLS)
    *   Idempotency of critical POST operations
    *   DbUp migration scripts (forward-only, idempotency)
    *   Logging (Serilog) and analytics batching

*   **Database:**
    *   PostgreSQL Row-Level Security (RLS) enforcement
    *   Data isolation between users
    *   Schema migrations (create_identity_schema, init_app_schema)
    *   Session timeout (30 minutes)

*   **Integration:**
    *   End-to-end testing of user flows across client, server, and database
    *   Data consistency between client cache (React Query) and backend database
    *   Cross-browser compatibility (Chrome, Firefox, Edge)

### 2.2 Out-of-Scope

*   Testing of third-party libraries and frameworks (e.g., React, .NET, Vite, PostgreSQL) beyond their integration with the application
*   Testing the underlying infrastructure (e.g., DigitalOcean, cloud monitoring, autoscaling) itself, though its interaction with the application is in scope
*   Comprehensive load testing simulating massive concurrent users (initial performance testing will focus on API response times under expected load of 10-50 concurrent users)
*   Full accessibility audit (basic axe-core checks will be performed, but comprehensive WCAG 2.1 AA compliance is deferred to post-MVP)

---

## 3. Requirements Traceability Matrix

This matrix maps business requirements to test scenarios, ensuring complete coverage.

| Requirement ID | Description | Priority | Test Case(s) | Status |
|----------------|-------------|----------|--------------|--------|
| **REQ-AUTH-001** | Users must authenticate to access game features | P0 | TC-AUTH-02, TC-AUTH-04, TC-SEC-01 | ✅ Covered |
| **REQ-AUTH-002** | Users can register with email and password | P0 | TC-AUTH-01 | ✅ Covered |
| **REQ-AUTH-003** | Users can reset forgotten passwords | P1 | TC-AUTH-05 | ✅ Covered |
| **REQ-AUTH-004** | Sessions expire after 30 minutes of inactivity | P0 | TC-AUTH-03, TC-SEC-03.1 | ✅ Covered |
| **REQ-SEC-001** | All state-changing requests must include CSRF token | P0 | TC-SEC-01 | ✅ Covered |
| **REQ-SEC-002** | Users can only access their own game data (RLS) | P0 | TC-SEC-02 | ✅ Covered |
| **REQ-SEC-003** | Rate limiting on authentication endpoints | P1 | TC-SEC-04 | ✅ Covered |
| **REQ-GAME-001** | Users can create a new game | P0 | TC-GAME-01 | ✅ Covered |
| **REQ-GAME-002** | Game state persists between sessions | P0 | TC-SAVE-01, TC-SAVE-03 | ✅ Covered |
| **REQ-GAME-003** | Users can save and load games | P0 | TC-SAVE-01, TC-SAVE-02, TC-SAVE-03 | ✅ Covered |
| **REQ-PLAY-001** | Users can move units on the map | P0 | TC-PLAY-02 | ✅ Covered |
| **REQ-PLAY-002** | Users can attack enemy units | P0 | TC-PLAY-03 | ✅ Covered |
| **REQ-PLAY-003** | Combat outcomes determined server-side | P0 | TC-PLAY-03 | ✅ Covered |
| **REQ-PLAY-004** | Turn-based gameplay with AI opponents | P0 | TC-PLAY-04, TC-PLAY-05 | ✅ Covered |
| **REQ-PLAY-005** | Game map renders on HTML5 Canvas | P0 | TC-PLAY-01, TC-VR-01 | ✅ Covered |
| **REQ-PERF-001** | Game actions respond within 500ms (P95) | P1 | TC-PERF-02 | ✅ Covered |
| **REQ-PERF-002** | Map loads within 1000ms (P95) | P1 | TC-PERF-04 | ✅ Covered |
| **REQ-SAVE-001** | Save files include schema version for compatibility | P0 | TC-SAVE-04 | ✅ Covered |

---

## 4. Types of Tests

A multi-layered testing approach will be adopted to ensure thorough coverage.

### 4.1 Unit Testing

*   **Frontend:** Individual React components, hooks, and utility functions (e.g., `hexGeometry.ts`, `pathfinding.ts`) will be tested in isolation using **Vitest** and **React Testing Library**. Focus on:
    *   Component rendering based on props and state
    *   Event handlers and user interactions
    *   Pure logic functions (hex math, pathfinding algorithms)
    *   Custom hooks (useAuth, useGameState, useCsrf)

*   **Backend:** Business logic within services (`GameService`, `TurnService`, `ActionService`) and domain models will be tested using **xUnit**. Focus on:
    *   Service method logic with mocked repositories
    *   Domain entity validation and business rules
    *   Utility functions (combat calculations, visibility checks)

### 4.2 Integration Testing

*   **Frontend:** Testing the integration of multiple components and client-side state management:
    *   Form components with parent pages
    *   React Query cache updates after API calls
    *   Zustand store synchronization with UI

*   **Backend:** Testing the interaction between API controllers, services, and a real test database:
    *   Full request pipeline (controller → service → repository → database)
    *   Data persistence and retrieval
    *   Transaction handling and rollback
    *   Using **Testcontainers** or local PostgreSQL with **Respawn** for cleanup

### 4.3 API Testing

*   Dedicated testing of each API endpoint using **Postman collections** or **Playwright API tests**
*   Coverage includes:
    *   Valid requests with expected responses
    *   Invalid inputs and validation errors
    *   Error responses (4xx/5xx) with proper error messages
    *   Authentication/authorization headers
    *   CSRF token validation
    *   Idempotency of POST operations

### 4.4 End-to-End (E2E) Testing

*   Automated tests simulating complete user scenarios using **Playwright**
*   Critical user flows from start to finish
*   Browser automation in headless mode for CI/CD
*   Cross-browser testing (Chromium, Firefox, WebKit)

### 4.5 Visual Regression Testing

Due to the complexity of the HTML5 Canvas map, visual regression tests will be implemented as part of the E2E suite.

**Implementation Details:**
*   **Tool:** Playwright with `toHaveScreenshot()` API
*   **Scope:** Static game states (turn-based snapshots), not mid-animation frames
*   **Baseline Storage:** Git repository with `.png` files in `tests/visual-regression/baselines/`
*   **Approval Process:** PR requires manual visual review if diff >0.1% pixels (configurable `maxDiffPixels` threshold)
*   **Environment:** Headless Chromium in Docker container to ensure consistent rendering across machines
*   **Font Handling:** Use system fonts with fallback to ensure consistent text rendering

**Key Visual Test Scenarios:**
*   **TC-VR-01:** Hexagonal grid renders correctly at zoom levels 1.0x, 1.5x, 2.0x
*   **TC-VR-02:** Unit sprites, health bars, and movement indicators render correctly
*   **TC-VR-03:** Fog of war displays correctly (explored, unexplored, visible tiles)
*   **TC-VR-04:** City markers and resource icons render at correct positions
*   **TC-VR-05:** UI overlays (end turn button, notifications) render correctly on top of canvas

### 4.6 Security Testing

Manual and automated security checks focusing on:

*   **CSRF Protection:** Verifying state-changing requests require valid tokens
*   **Authentication & Authorization:** Ensuring proper access control
*   **Row-Level Security (RLS):** Confirming database-level data isolation
*   **Session Management:** Validating timeout and keepalive mechanisms
*   **Rate Limiting:** Preventing brute-force attacks
*   **Input Validation:** Protecting against injection attacks (SQL, XSS)
*   **Dependency Scanning:** Checking for vulnerable packages (`npm audit`, `dotnet list package --vulnerable`)

### 4.7 Performance Testing

*   **Backend:** Load testing on critical API endpoints using **k6**
*   **Frontend:** Client-side rendering performance analysis using:
    *   Lighthouse (Performance score ≥90)
    *   Chrome DevTools Profiler (frame rate, render time)
    *   React DevTools Profiler (component render cost)
*   **Database:** Query performance monitoring (slow query log, explain plans)

### 4.8 Manual Exploratory Testing

Structured exploratory testing sessions to discover defects and usability issues that automated tests might miss.

**Session-Based Testing** (90-minute focused sessions):

| Session Charter | Focus Areas | Deliverable |
|----------------|-------------|-------------|
| "Explore unit movement edge cases on varied terrain" | Canvas interactions, drag-and-drop, keyboard shortcuts | Session notes + screenshots |
| "Test error recovery and resilience" | Network interruption mid-action, browser back button, page refresh | Bug reports for unexpected behaviors |
| "Cross-browser rendering and interactions" | Firefox/Edge-specific rendering issues, vendor prefixes | Compatibility matrix |
| "Usability and user experience" | Confusing UI, missing feedback, unclear error messages | UX improvement recommendations |

**Key Exploratory Areas:**
1. **Canvas Interactions:** Pan, zoom, click precision at different screen resolutions (1080p, 1440p, 4K)
2. **Error Recovery:** Network interruption during turn processing, browser crash recovery, session restoration
3. **Edge Cases:** Boundary conditions (e.g., units at map edges, maximum unit count, turn 9999)
4. **Concurrent Actions:** Rapid clicking, race conditions, double-submission prevention

---

## 5. Detailed Test Scenarios

### 5.1 User Authentication

#### TC-AUTH-01: User Registration

**Priority:** P0 | **Type:** Functional | **Automation:** E2E (Playwright)

##### TC-AUTH-01.1: Successful Registration (Happy Path)
**Preconditions:** User email not in database, valid CSRF token obtained

**Test Data:**
```json
{
  "email": "newuser@example.com",
  "password": "SecureP@ss123!",
  "confirmPassword": "SecureP@ss123!"
}
```

**Steps:**
1. Navigate to `/register`
2. Verify CSRF token is present in `<meta>` tag and `CsrfProvider` context
3. Enter email: `newuser@example.com`
4. Enter password: `SecureP@ss123!`
5. Enter confirm password: `SecureP@ss123!`
6. Click "Register" button

**Expected Result:**
- HTTP 201 Created response from `POST /api/auth/register`
- User redirected to `/game/current`
- Session cookie `TenXEmpires.Session` is set with `HttpOnly` and `SameSite=Strict` flags
- User record created in `auth.users` table (verify via database query)
- Email verification modal appears (if email verification is enabled)
- React Query cache updated with user profile

**Actual Result:** _(to be filled during test execution)_

**Status:** ⬜ Not Run | ✅ Pass | ❌ Fail

---

##### TC-AUTH-01.2: Duplicate Email
**Preconditions:** User with email `existing@example.com` already exists in database

**Test Data:**
```json
{
  "email": "existing@example.com",
  "password": "SecureP@ss123!"
}
```

**Steps:**
1. Navigate to `/register`
2. Enter email: `existing@example.com` (already in database)
3. Enter password: `SecureP@ss123!`
4. Click "Register" button

**Expected Result:**
- HTTP 409 Conflict response
- Error notification displayed: "An account with this email already exists"
- No new database record created (verify count in `auth.users`)
- User remains on `/register` page

---

##### TC-AUTH-01.3: Weak Password
**Test Data:**
```json
{
  "email": "newuser2@example.com",
  "password": "123"
}
```

**Steps:**
1. Navigate to `/register`
2. Enter email: `newuser2@example.com`
3. Enter password: `123` (too short)
4. Attempt to submit form

**Expected Result:**
- Client-side validation error displayed before form submission
- Error message: "Password must be at least 8 characters and include uppercase, lowercase, number, and special character"
- Register button disabled or form submission prevented
- No API call made

---

##### TC-AUTH-01.4: CSRF Token Missing
**Test Data:** Valid registration data

**Steps:**
1. Use API client (Postman/curl) to send `POST /api/auth/register` without `X-XSRF-TOKEN` header
2. Include valid registration data in request body

**Expected Result:**
- HTTP 403 Forbidden response
- Error message: "CSRF token validation failed"
- No user account created
- Security log entry created (verify in Serilog output)

---

#### TC-AUTH-02: User Login

**Priority:** P0 | **Type:** Functional | **Automation:** E2E (Playwright)

##### TC-AUTH-02.1: Successful Login
**Preconditions:** User account exists with email `testuser@example.com` and password `TestP@ss123!`

**Steps:**
1. Navigate to `/login`
2. Enter email: `testuser@example.com`
3. Enter password: `TestP@ss123!`
4. Check "Remember Me" checkbox
5. Click "Login" button

**Expected Result:**
- HTTP 200 OK response from `POST /api/auth/login`
- User redirected to `/game/current` or to `returnUrl` parameter if present
- Session cookie set with extended expiration (if "Remember Me" is checked)
- User profile loaded into React Query cache
- Zustand auth store updated with `isAuthenticated: true`
- IdleSessionProvider starts inactivity timer

---

##### TC-AUTH-02.2: Invalid Credentials
**Test Data:**
```json
{
  "email": "testuser@example.com",
  "password": "WrongPassword123!"
}
```

**Expected Result:**
- HTTP 401 Unauthorized response
- Generic error message: "Invalid email or password" (no user enumeration)
- Failed login attempt logged to database (for rate limiting)
- User remains on `/login` page
- Password field cleared

---

##### TC-AUTH-02.3: Account Lockout After Failed Attempts
**Steps:**
1. Attempt login with wrong password 5 times in succession
2. Attempt 6th login with correct password

**Expected Result:**
- After 5 failed attempts: HTTP 429 Too Many Requests
- Error message: "Too many failed login attempts. Account temporarily locked. Try again in 15 minutes."
- Even correct password fails during lockout period
- Lockout entry created in database with expiration timestamp
- After 15 minutes: login with correct password succeeds

---

#### TC-AUTH-03: Session Management

**Priority:** P0 | **Type:** Functional | **Automation:** E2E (Playwright)

##### TC-AUTH-03.1: Session Expiry Due to Inactivity
**Preconditions:** User logged in

**Steps:**
1. Log in as valid user
2. Navigate to `/game/{gameId}`
3. Wait 30 minutes without any user interaction (or simulate by manipulating session expiration in database)
4. Attempt to perform any game action (e.g., move unit)

**Expected Result:**
- `IdleSessionProvider` detects inactivity
- `SessionExpiredModal` appears with message: "Your session has expired due to inactivity. Please log in again."
- Modal has "Login" button that redirects to `/login?returnUrl=/game/{gameId}`
- Any pending API requests fail with HTTP 401 Unauthorized
- React Query cache is cleared
- Zustand auth store reset to `isAuthenticated: false`

---

##### TC-AUTH-03.2: Keepalive Prevents Session Expiry
**Steps:**
1. Log in as valid user
2. Navigate to `/game/{gameId}`
3. Perform game actions periodically (every 20 minutes)
4. Verify keepalive requests are sent automatically

**Expected Result:**
- Every 25 minutes (before 30-minute timeout), `IdleSessionProvider` sends keepalive request
- `POST /api/auth/keepalive` returns HTTP 200 OK
- Session cookie expiration is renewed
- User can continue playing without interruption for extended periods

---

#### TC-AUTH-04: CSRF Protection

**Priority:** P0 | **Type:** Security | **Automation:** API Test (Playwright)

##### TC-AUTH-04.1: POST Request Without CSRF Token
**Steps:**
1. Obtain valid session cookie by logging in
2. Use API client to send `POST /api/games` without `X-XSRF-TOKEN` header
3. Include valid game creation data in request body

**Expected Result:**
- HTTP 403 Forbidden response
- Error response body: `{ "error": "CSRF token validation failed" }`
- No game created in database
- Security event logged

---

##### TC-AUTH-04.2: POST Request With Invalid CSRF Token
**Steps:**
1. Obtain valid session cookie
2. Send `POST /api/games` with `X-XSRF-TOKEN: invalid-token-12345`

**Expected Result:**
- HTTP 403 Forbidden response
- Same error as TC-AUTH-04.1
- Security event logged with attempted token value (for analysis)

---

##### TC-AUTH-04.3: CSRF Token Refresh After Expiration
**Steps:**
1. Log in and obtain CSRF token
2. Wait for token expiration (or simulate by clearing `CsrfProvider` context)
3. Attempt game action that requires CSRF token
4. Verify CsrfProvider automatically retries with new token

**Expected Result:**
- First request fails with HTTP 403
- `CsrfProvider` automatically fetches new token via `GET /api/auth/csrf-token`
- Request automatically retried with new token
- Second request succeeds (HTTP 200/201)
- User sees no error (transparent retry)

---

#### TC-AUTH-05: Password Reset

**Priority:** P1 | **Type:** Functional | **Automation:** E2E (Playwright) + Manual

##### TC-AUTH-05.1: Request Password Reset
**Steps:**
1. Navigate to `/forgot-password`
2. Enter email: `resetuser@example.com`
3. Click "Send Reset Link" button

**Expected Result:**
- HTTP 200 OK response (always, regardless of whether email exists - prevents user enumeration)
- Generic success message: "If an account exists with this email, a password reset link has been sent."
- If email exists: password reset token generated and email sent (verify in email logs/mock service)
- If email doesn't exist: no email sent, but same generic success message shown
- Rate limiting applies: max 3 reset requests per email per hour

---

##### TC-AUTH-05.2: Complete Password Reset
**Preconditions:** Password reset email received with valid token

**Steps:**
1. Click link in email: `/reset-password?token=abc123xyz`
2. Enter new password: `NewSecureP@ss456!`
3. Confirm new password: `NewSecureP@ss456!`
4. Click "Reset Password" button

**Expected Result:**
- HTTP 200 OK response
- Password updated in database (hashed)
- Old password no longer works
- Reset token invalidated (single-use)
- User redirected to `/login` with success message
- User can log in with new password

---

##### TC-AUTH-05.3: Expired Reset Token
**Steps:**
1. Use password reset token that was generated >24 hours ago
2. Attempt to reset password

**Expected Result:**
- HTTP 400 Bad Request
- Error message: "This password reset link has expired. Please request a new one."
- Password not changed
- User prompted to request new reset link

---

### 5.2 Game Management

#### TC-GAME-01: Create New Game

**Priority:** P0 | **Type:** Functional | **Automation:** E2E (Playwright)

##### TC-GAME-01.1: Create First Game (Happy Path)
**Preconditions:** User logged in, no active games

**Steps:**
1. Navigate to `/game/current`
2. "Start New Game" modal appears automatically
3. Select map size: "Small (40x40)"
4. Select difficulty: "Normal"
5. Click "Start Game" button

**Expected Result:**
- HTTP 201 Created response from `POST /api/games`
- New game record created in `app.games` table with `user_id` = current user
- User redirected to `/game/{newGameId}`
- Game map loads on canvas
- Turn counter shows "Turn 1"
- Autosave created (verify in `app.saves` table with `is_autosave = true`)

---

##### TC-GAME-01.2: Overwrite Existing Active Game
**Preconditions:** User already has an active game (status = 'active')

**Steps:**
1. Navigate to `/game/current`
2. User redirected to existing game automatically
3. Open game menu, select "Start New Game"
4. Confirmation modal appears: "You have an active game. Starting a new game will end the current one. Continue?"
5. Click "Yes, Start New Game"

**Expected Result:**
- Old game status updated to `ended` in database
- New game created as in TC-GAME-01.1
- User redirected to new game

---

#### TC-GAME-02: Redirect to Current Game

**Priority:** P0 | **Type:** Functional | **Automation:** E2E (Playwright)

**Steps:**
1. User has an active game (ID = 123)
2. Navigate to `/game/current`

**Expected Result:**
- Automatic redirect to `/game/123` (most recent active game)
- No "Start New Game" modal shown
- Game state loads immediately

---

#### TC-GAME-03: Delete Game

**Priority:** P1 | **Type:** Functional | **Automation:** E2E (Playwright)

**Steps:**
1. Navigate to active game
2. Open game menu, select "Delete Game"
3. Confirmation modal: "Are you sure you want to delete this game? This action cannot be undone."
4. Click "Yes, Delete"

**Expected Result:**
- HTTP 200 OK response from `DELETE /api/games/{id}`
- Game record deleted from database (or soft-deleted with `deleted_at` timestamp)
- Associated save files deleted
- User redirected to `/game/current`
- "Start New Game" modal appears

---

### 5.3 Core Gameplay

#### TC-PLAY-01: Map Rendering

**Priority:** P0 | **Type:** Functional + Visual | **Automation:** E2E (Playwright) + Visual Regression

**Steps:**
1. Log in and create new game
2. Verify map renders on HTML5 canvas
3. Take screenshot for visual regression comparison

**Expected Result:**
- Hexagonal grid renders correctly with proper tile spacing
- Terrain types visually distinct (water, grassland, forest, mountains)
- Units render on top of tiles with correct sprites
- Cities render with city markers
- Resource icons (food, production, science) visible on appropriate tiles
- Fog of war applied (unexplored tiles darkened)
- No visual artifacts or rendering glitches
- Screenshot matches baseline (< 0.1% pixel difference)

---

#### TC-PLAY-02: Unit Movement

**Priority:** P0 | **Type:** Functional | **Automation:** E2E (Playwright)

##### TC-PLAY-02.1: Valid Unit Movement
**Preconditions:** Game loaded with player unit (Warrior) at position (10, 10) with 2 movement points

**Steps:**
1. Click on Warrior unit at (10, 10)
2. Unit is selected (highlighted visually)
3. Movement range overlay appears (tiles within 2 hexes)
4. Click on valid destination tile at (11, 10) (1 movement point cost)
5. Verify movement animation plays
6. Wait for server response

**Expected Result:**
- HTTP 200 OK response from `POST /api/games/{id}/actions` with action type `MoveUnit`
- Unit position updated on server: `(10, 10) → (11, 10)`
- Unit position updated on client canvas immediately (optimistic update)
- Unit movement points reduced: `2 → 1`
- React Query cache updated with new game state
- Movement animation completes smoothly
- If server rejects movement (e.g., tile now occupied by another unit), client reverts optimistic update

---

##### TC-PLAY-02.2: Invalid Movement - Out of Range
**Steps:**
1. Click on Warrior unit with 2 movement points
2. Click on tile 4 hexes away (out of range)

**Expected Result:**
- Tile not highlighted in movement range overlay
- Click on out-of-range tile has no effect (or shows error notification)
- No API request sent
- Unit does not move

---

##### TC-PLAY-02.3: Invalid Movement - Blocked Tile
**Steps:**
1. Click on Warrior unit
2. Attempt to move to tile occupied by mountain terrain (impassable)

**Expected Result:**
- Mountain tile not included in movement range overlay
- If somehow click reaches server: HTTP 400 Bad Request with error "Cannot move to impassable terrain"
- Error notification appears: "Unit cannot move there"
- Unit does not move

---

##### TC-PLAY-02.4: Invalid Movement - Insufficient Movement Points
**Preconditions:** Unit has 1 movement point, target tile costs 2 movement points (difficult terrain)

**Steps:**
1. Select unit with 1 movement point remaining
2. Attempt to move to tile requiring 2 movement points (forest)

**Expected Result:**
- Target tile not highlighted in movement range (overlay respects movement points)
- If somehow attempted: HTTP 400 Bad Request "Insufficient movement points"
- Unit does not move

---

#### TC-PLAY-03: Unit Combat

**Priority:** P0 | **Type:** Functional | **Automation:** E2E (Playwright)

##### TC-PLAY-03.1: Successful Attack
**Preconditions:**
- Player Warrior at (10, 10) with 20 HP, 10 attack power
- Enemy Warrior at adjacent tile (11, 10) with 20 HP, 10 attack power

**Steps:**
1. Click on player Warrior
2. Right-click on enemy Warrior (or click attack button)
3. Confirm attack action
4. Wait for server response

**Expected Result:**
- HTTP 200 OK response from `POST /api/games/{id}/actions` with action type `AttackUnit`
- Combat calculation performed on server (deterministic or RNG with seed)
- Both units take damage according to combat formula
- Example result: Player Warrior HP: `20 → 15`, Enemy Warrior HP: `20 → 12`
- HP bars updated on canvas for both units
- Combat animation plays (optional)
- If enemy unit HP reaches 0: unit removed from map and database
- Player unit cannot move after attacking (movement points set to 0)

---

##### TC-PLAY-03.2: Attack Out of Range
**Steps:**
1. Select player unit
2. Attempt to attack enemy unit 3 tiles away (melee unit range = 1)

**Expected Result:**
- Attack action not available in UI (button disabled or not shown)
- If somehow sent to server: HTTP 400 Bad Request "Target out of range"
- No combat occurs

---

##### TC-PLAY-03.3: Unit Destroyed in Combat
**Preconditions:** Player Warrior at (10, 10) with 5 HP attacks Enemy Warrior at (11, 10) with 20 HP

**Steps:**
1. Initiate attack (player unit likely to be destroyed)

**Expected Result:**
- Combat calculation performed
- Player unit destroyed (HP reaches 0)
- Player unit removed from canvas and database
- Enemy unit survives with reduced HP
- Notification: "Your Warrior was destroyed in combat"
- Turn can continue with remaining units

---

#### TC-PLAY-04: End Turn

**Priority:** P0 | **Type:** Functional | **Automation:** E2E (Playwright)

##### TC-PLAY-04.1: End Turn - AI Processing
**Preconditions:** Player turn active, at least one AI opponent

**Steps:**
1. Perform some actions (move units, etc.)
2. Click "End Turn" button
3. Confirm action if prompted

**Expected Result:**
- HTTP 200 OK response from `POST /api/games/{id}/end-turn`
- Turn number increments: e.g., `Turn 1 → Turn 2`
- Player turn changes: `player → ai`
- Game state updated: `turnInProgress: true`
- "AI is thinking" overlay displayed on UI
- Client begins polling `GET /api/games/{id}/state` every 2 seconds
- AI performs its turn on server (move units, attack, etc.)
- When AI turn completes: `turnInProgress: false`, control returns to player
- Autosave created after turn completion
- All player units have movement points restored
- Client stops polling, overlay removed

---

##### TC-PLAY-04.2: Autosave Creation
**Steps:**
1. Complete turn as in TC-PLAY-04.1
2. Verify autosave in database

**Expected Result:**
- New record in `app.saves` table:
  - `is_autosave = true`
  - `turn_number = 2`
  - `game_snapshot` contains serialized game state
  - `created_at = current timestamp`
- Only 3 most recent autosaves retained (older autosaves deleted)

---

#### TC-PLAY-05: State Polling During AI Turn

**Priority:** P1 | **Type:** Functional | **Automation:** E2E (Playwright)

**Steps:**
1. End turn, triggering AI turn
2. Monitor network requests during AI processing

**Expected Result:**
- Client polls `GET /api/games/{id}/state` every 2 seconds while `turnInProgress: true`
- Each poll response includes updated game state
- When `turnInProgress: false`, polling stops
- React Query cache updated with final state
- UI refreshes to show AI's actions (unit positions, combat results)

---

#### TC-PLAY-06: Game Over

**Priority:** P1 | **Type:** Functional | **Automation:** E2E (Playwright)

##### TC-PLAY-06.1: Victory Condition
**Steps:**
1. Play game to victory condition (e.g., eliminate all AI players)
2. Verify game over flow

**Expected Result:**
- Game status updated to `victory` in database
- `ResultOverlay` appears with message: "Victory! You have conquered the world."
- Overlay shows game statistics (turns played, units destroyed, etc.)
- Options: "View Game", "Start New Game", "Return to Menu"
- No more actions allowed on game map

---

##### TC-PLAY-06.2: Defeat Condition
**Steps:**
1. Play game to defeat condition (e.g., all player units destroyed)

**Expected Result:**
- Game status updated to `defeat` in database
- `ResultOverlay` appears with message: "Defeat. Your empire has fallen."
- Same overlay options as victory

---

### 5.4 Save/Load System

#### TC-SAVE-01: Manual Save

**Priority:** P0 | **Type:** Functional | **Automation:** E2E (Playwright)

##### TC-SAVE-01.1: Create Manual Save in Empty Slot
**Steps:**
1. During active game, open save menu
2. Click "Save Game"
3. Select empty slot 1
4. Enter save name: "My First Save"
5. Click "Save"

**Expected Result:**
- HTTP 201 Created response from `POST /api/games/{id}/saves`
- New record in `app.saves` table:
  - `slot_number = 1`
  - `is_autosave = false`
  - `save_name = "My First Save"`
  - `turn_number = current turn`
  - `game_snapshot = serialized state`
- Save appears in save list with name, turn number, and timestamp
- Success notification: "Game saved successfully"

---

#### TC-SAVE-02: Overwrite Save

**Priority:** P1 | **Type:** Functional | **Automation:** E2E (Playwright)

**Steps:**
1. Open save menu
2. Select occupied slot 1 (contains "My First Save")
3. Overwrite confirmation modal appears: "Slot 1 already contains a save. Overwrite?"
4. Click "Yes, Overwrite"

**Expected Result:**
- HTTP 200 OK response from `PUT /api/games/{id}/saves/{slotNumber}`
- Existing save record updated with new game state and timestamp
- Save list shows updated turn number and timestamp
- Success notification: "Game saved successfully"

---

#### TC-SAVE-03: Load Game

**Priority:** P0 | **Type:** Functional | **Automation:** E2E (Playwright)

##### TC-SAVE-03.1: Load Manual Save
**Preconditions:** Manual save exists in slot 1 from Turn 5

**Steps:**
1. From main menu or game menu, click "Load Game"
2. Select save from slot 1 (Turn 5)
3. If currently in an active game: confirmation modal "Loading will lose unsaved progress. Continue?"
4. Click "Load"

**Expected Result:**
- HTTP 200 OK response from `POST /api/games/{gameId}/loads/{saveId}`
- Game state restored from save snapshot:
  - Turn number reverted to 5
  - All unit positions restored
  - Map state (fog of war, explored tiles) restored
  - Resources and city states restored
- User redirected to `/game/{gameId}`
- Success notification: "Game loaded successfully"
- Schema version validated before load

---

##### TC-SAVE-03.2: Load Autosave
**Steps:**
1. Open load game menu
2. Select autosave from Turn 8
3. Load game

**Expected Result:**
- Same as TC-SAVE-03.1, but loading from autosave
- Autosave remains in database (not deleted on load)

---

#### TC-SAVE-04: Schema Mismatch

**Priority:** P0 | **Type:** Functional | **Automation:** API Test

**Steps:**
1. Create save file with `schema_version = 1`
2. Simulate application upgrade to schema version 2
3. Attempt to load old save file

**Expected Result:**
- Server detects schema mismatch during load
- HTTP 400 Bad Request response with error: "Save file schema is incompatible with current game version"
- `ErrorSchemaModal` appears on client:
  - Message: "This save file is from an older version and cannot be loaded."
  - Options: "Return to Menu", "Check for Updates"
- Game does not load
- User cannot bypass error (modal blocks interaction)
- Alternative: If migration is possible, server automatically migrates save to new schema

---

### 5.5 Security Test Scenarios

#### TC-SEC-01: CSRF Protection

_(Covered in TC-AUTH-04)_

---

#### TC-SEC-02: Authorization (Postgres RLS)

**Priority:** P0 | **Type:** Security | **Automation:** Integration Test (xUnit + PostgreSQL)

##### TC-SEC-02.1: User Cannot Access Another User's Game via API
**Preconditions:**
- User A (ID = 1) has game (ID = 100)
- User B (ID = 2) logged in

**Steps:**
1. User B sends `GET /api/games/100/state` with valid session
2. Request reaches controller, EF Core applies RLS policy

**Expected Result:**
- HTTP 404 Not Found (not 403 to prevent game ID enumeration)
- PostgreSQL RLS policy blocks query: `WHERE user_id = 2` returns no results
- No game data leaked to User B
- Security event logged

---

##### TC-SEC-02.2: User Cannot Update Another User's Game
**Steps:**
1. User B sends `POST /api/games/100/actions` (User A's game)

**Expected Result:**
- HTTP 404 Not Found or HTTP 403 Forbidden
- RLS policy prevents update
- No game state modified

---

##### TC-SEC-02.3: Direct Database Query Filtered by RLS
**Steps:**
1. Open PostgreSQL session as User B (via connection string with user context)
2. Execute: `SELECT * FROM app.games;`

**Expected Result:**
- Query returns only games where `user_id = 2` (User B's games)
- User A's games not visible in result set
- RLS policy enforced at database level (defense in depth)

---

##### TC-SEC-02.4: RLS Disabled in Development
**Steps:**
1. Run `db/testing/disable-rls-dev.sql` script in dev environment
2. Execute test queries

**Expected Result:**
- RLS policies disabled for easier testing
- All games visible (for debugging purposes)
- Script not used in staging/production (CI/CD validation)

---

#### TC-SEC-03: Session Management

##### TC-SEC-03.1: Session Timeout After 30 Minutes
_(Covered in TC-AUTH-03.1)_

---

##### TC-SEC-03.2: Keepalive Extends Session
_(Covered in TC-AUTH-03.2)_

---

##### TC-SEC-03.3: Session Cookie Attributes
**Steps:**
1. Log in and inspect session cookie in browser DevTools

**Expected Result:**
- Cookie name: `TenXEmpires.Session` (or configured name)
- Attributes:
  - `HttpOnly = true` (prevents JavaScript access)
  - `Secure = true` (HTTPS only, in production)
  - `SameSite = Strict` or `Lax` (CSRF protection)
  - `Path = /`
  - `Max-Age` or `Expires` set appropriately (30 min for standard session, longer for "Remember Me")

---

#### TC-SEC-04: Rate Limiting

**Priority:** P1 | **Type:** Security | **Automation:** Load Test (k6)

##### TC-SEC-04.1: Login Rate Limiting
**Steps:**
1. Send 10 failed login requests to `/api/auth/login` within 5 minutes
2. Send 11th request

**Expected Result:**
- First 5 requests: HTTP 401 Unauthorized (invalid credentials)
- After 5 failures: HTTP 429 Too Many Requests
- Response headers include:
  - `Retry-After: 900` (15 minutes in seconds)
  - `X-RateLimit-Limit: 5`
  - `X-RateLimit-Remaining: 0`
- Error message: "Too many failed login attempts. Try again in 15 minutes."
- Rate limit entry stored in database or cache (Redis) with expiration

---

##### TC-SEC-04.2: API Request Rate Limiting
**Steps:**
1. Send 100 requests to `/api/games/{id}/state` within 1 minute
2. Send 101st request

**Expected Result:**
- First 100 requests: HTTP 200 OK (if valid)
- 101st request: HTTP 429 Too Many Requests
- Error message: "Rate limit exceeded. Please slow down."
- Rate limit per IP address or per user (configurable)

---

##### TC-SEC-04.3: Analytics Rate Limiting
**Steps:**
1. Send 20 analytics events via `POST /api/analytics/batch` within 1 minute

**Expected Result:**
- Requests accepted (analytics batching allows higher rate)
- If excessive (e.g., >100 events/min): HTTP 429
- Rate limit less strict than for game-state-changing endpoints

---

#### TC-SEC-05: Input Validation

**Priority:** P1 | **Type:** Security | **Automation:** API Test

##### TC-SEC-05.1: SQL Injection Prevention
**Steps:**
1. Attempt to register with email: `admin'--@example.com`
2. Attempt to create game with name: `'; DROP TABLE app.games; --`

**Expected Result:**
- Inputs sanitized and treated as literal strings
- No SQL injection executed
- Parameterized queries or EF Core ORM prevents injection
- Invalid email format rejected by validation

---

##### TC-SEC-05.2: XSS Prevention
**Steps:**
1. Create save with name: `<script>alert('XSS')</script>`
2. Load save list

**Expected Result:**
- Save name stored as-is in database (data integrity)
- On display: HTML-escaped to `&lt;script&gt;alert('XSS')&lt;/script&gt;`
- No JavaScript executed in browser
- React automatically escapes JSX text content

---

##### TC-SEC-05.3: Command Injection Prevention
**Steps:**
1. Attempt to upload save file with malicious filename: `save; rm -rf /`

**Expected Result:**
- Filename sanitized or rejected
- No shell commands executed on server
- File storage uses UUID or sanitized names

---

### 5.6 Performance Test Scenarios

**Tool:** k6 for load testing

#### TC-PERF-01: Game State Fetch Performance

**Priority:** P1 | **Type:** Performance | **Automation:** k6 Script

**Scenario:** 10 concurrent users repeatedly fetch game state

**k6 Configuration:**
```javascript
export const options = {
  vus: 10, // 10 virtual users
  duration: '2m', // 2 minutes
  thresholds: {
    http_req_duration: ['p(95)<300'], // 95% of requests < 300ms
  },
};
```

**Steps:**
1. Authenticate 10 virtual users
2. Each user fetches `GET /api/games/{gameId}/state` repeatedly
3. Measure response times

**Expected Result:**
- **P50 (median):** <150ms
- **P95:** <300ms
- **P99:** <500ms
- No HTTP 500 errors
- Database query time <50ms (check Serilog slow query log)

---

#### TC-PERF-02: Game Action Performance

**Priority:** P1 | **Type:** Performance | **Automation:** k6 Script

**Scenario:** 50 requests/second for move unit action

**Expected Result:**
- **P95:** <500ms
- **P99:** <1000ms
- Action processing (business logic + DB write) <300ms
- No database deadlocks or lock timeouts

---

#### TC-PERF-03: End Turn Performance

**Priority:** P1 | **Type:** Performance | **Automation:** Manual + Logging

**Scenario:** End turn with AI processing

**Expected Result:**
- Player turn ends immediately (HTTP 200 < 200ms)
- AI turn processing completes within 5 seconds (simple AI on small map)
- Autosave creation <500ms
- Total end turn cycle <6 seconds

---

#### TC-PERF-04: Map Tiles Fetch Performance

**Priority:** P1 | **Type:** Performance | **Automation:** k6 Script

**Scenario:** Initial map load (paged tile fetch)

**Expected Result:**
- `GET /api/maps/{id}/tiles?page=1&pageSize=100`
- **P95:** <1000ms
- Large maps may require multiple pages, but first page loads quickly
- Client-side rendering of tiles <2 seconds (100 tiles)

---

#### TC-PERF-05: Frontend Rendering Performance

**Priority:** P1 | **Type:** Performance | **Automation:** Lighthouse

**Steps:**
1. Run Lighthouse audit on `/game/{id}` page
2. Measure performance score and core web vitals

**Expected Result:**
- **Lighthouse Performance Score:** ≥90
- **First Contentful Paint (FCP):** <1.5s
- **Largest Contentful Paint (LCP):** <2.5s
- **Time to Interactive (TTI):** <3.5s
- **Cumulative Layout Shift (CLS):** <0.1
- **Canvas rendering:** Maintains 60 FPS (16.67ms per frame) during pan/zoom

---

#### TC-PERF-06: Database Query Performance

**Priority:** P1 | **Type:** Performance | **Automation:** SQL Profiling

**Steps:**
1. Enable PostgreSQL slow query log (threshold: 100ms)
2. Run full test suite
3. Review slow queries

**Expected Result:**
- No queries >100ms under normal load
- Proper indexes on:
  - `app.games (user_id, status)`
  - `app.saves (game_id, slot_number)`
  - `app.tiles (map_id, q, r)` (hex coordinates)
  - `app.units (game_id, position_q, position_r)`
- `EXPLAIN ANALYZE` shows index scans, not sequential scans

---

### 5.7 Visual Regression Test Scenarios

**Tool:** Playwright `toHaveScreenshot()`

#### TC-VR-01: Hexagonal Grid Rendering at Multiple Zoom Levels

**Steps:**
1. Load game with standard map
2. Set zoom to 1.0x, take screenshot
3. Zoom to 1.5x, take screenshot
4. Zoom to 2.0x, take screenshot

**Expected Result:**
- All screenshots match baselines (< 0.1% pixel difference)
- Hex grid maintains proper proportions at all zoom levels
- No blurry or pixelated hexagons

---

#### TC-VR-02: Unit Sprites and Health Bars

**Steps:**
1. Load game with various units (Warrior, Archer, Settler, etc.)
2. Take screenshot of map area containing all unit types
3. Damage some units to show health bars at different levels (100%, 75%, 50%, 25% HP)

**Expected Result:**
- Unit sprites render correctly (colors, details)
- Health bars positioned above units
- Health bar colors: green (>50%), yellow (25-50%), red (<25%)
- Screenshots match baselines

---

#### TC-VR-03: Fog of War

**Steps:**
1. Load game with partially explored map
2. Screenshot areas showing:
   - Unexplored tiles (black/fully darkened)
   - Explored but not visible tiles (greyed out)
   - Currently visible tiles (full color)

**Expected Result:**
- Three distinct fog of war states visible
- No visual artifacts at fog boundaries
- Tile details hidden in unexplored areas
- Screenshots match baselines

---

#### TC-VR-04: City Markers and Resource Icons

**Steps:**
1. Load game with cities and resource tiles visible
2. Screenshot areas showing city markers and various resources (wheat, production, science)

**Expected Result:**
- City markers clearly visible on top of city tiles
- Resource icons positioned correctly on tiles
- Icons not overlapping illegibly
- Screenshots match baselines

---

#### TC-VR-05: UI Overlays

**Steps:**
1. Display "End Turn" button, notification toasts, and modals
2. Screenshot UI elements overlaid on canvas

**Expected Result:**
- UI elements render on top of canvas with proper z-index
- No canvas content bleeding through modals
- Buttons and text legible
- Screenshots match baselines

---

## 6. Test Data Management

### 6.1 Data Requirements

| Test Scenario | Required Data | Source | Refresh Frequency |
|---------------|---------------|--------|-------------------|
| TC-AUTH-01, TC-AUTH-02 | Valid user accounts (5x) | Seed script (`db/migrations/seed-test-users.sql`) | Per deploy |
| TC-AUTH-02.3 | User with failed login attempts | Test setup method | Per test run |
| TC-GAME-01 | Map definitions (small, medium, large) | Seed script | Per deploy |
| TC-PLAY-03 | Game state with units in combat range | Factory method in test code | Per test run |
| TC-SAVE-03 | Historical save files (schema v1.0, v1.1) | Version-controlled fixtures (`tests/fixtures/saves/`) | Manual (version-specific) |
| TC-SEC-02 | Multiple users with separate game data | Seed script + test isolation | Per test run |
| TC-PERF-* | Realistic game states (mid-game, large maps) | Snapshot from real gameplay | Monthly refresh |

### 6.2 Data Strategy by Test Type

#### Unit Tests
- **Approach:** In-memory fixtures, no database dependency
- **Generation:** Hand-crafted minimal data or Bogus library for randomized test data
- **Isolation:** Each test creates its own data, no shared state

#### Integration Tests (Backend)
- **Approach:** Real PostgreSQL database (Testcontainers or local Docker)
- **Cleanup:** Respawn library resets database to seed state after each test
- **Transactions:** Wrap tests in transactions and rollback (alternative to Respawn)
- **Seed Data:** Minimal set of users, maps, and reference data loaded via DbUp migrations

#### E2E Tests (Playwright)
- **Approach:** Dedicated test database, reset before each test suite
- **User Accounts:** Pre-created test accounts:
  - `test1@example.com` / `TestP@ss123!`
  - `test2@example.com` / `TestP@ss123!`
  - `admin@example.com` / `AdminP@ss123!` (for admin tests, future)
- **Game States:** Created on-the-fly via API calls in test setup
- **Cleanup:** Database reset script or Docker container recreation

#### Performance Tests (k6)
- **Approach:** Persistent test database with realistic data volume
- **Data Volume:**
  - 100 users
  - 500 games (various states: active, ended)
  - 5000 save files
  - 1M+ tiles (pre-generated maps)
- **Refresh:** Weekly or after significant schema changes

### 6.3 Data Privacy and Anonymization

- **No Production Data:** Never use production data in test environments
- **Synthetic Data:** All test data generated via Bogus or similar library
- **Anonymization:** If production-like data needed (for debugging), anonymize PII (emails, names) before copying to test environment
- **Compliance:** Test data management complies with GDPR/privacy regulations (no real user data)

### 6.4 Test Data Creation Scripts

**Seed Script Example (PostgreSQL):**
```sql
-- db/migrations/seed-test-users.sql
-- Run in test environments only

INSERT INTO auth.users (id, email, password_hash, email_verified, created_at)
VALUES
  (1, 'test1@example.com', '$2a$11$...', true, NOW()),
  (2, 'test2@example.com', '$2a$11$...', true, NOW()),
  (3, 'test3@example.com', '$2a$11$...', true, NOW())
ON CONFLICT (email) DO NOTHING;

-- Create test games
INSERT INTO app.games (id, user_id, map_id, status, turn_number, created_at)
VALUES
  (100, 1, 1, 'active', 5, NOW() - INTERVAL '2 days'),
  (101, 2, 1, 'active', 10, NOW() - INTERVAL '1 day')
ON CONFLICT (id) DO NOTHING;
```

**Factory Method Example (C# xUnit):**
```csharp
public static class TestDataFactory
{
    public static Game CreateTestGame(int userId, int turnNumber = 1)
    {
        return new Game
        {
            UserId = userId,
            MapId = 1,
            Status = GameStatus.Active,
            TurnNumber = turnNumber,
            CreatedAt = DateTime.UtcNow
        };
    }
}
```

---

## 7. Test Environment

### 7.1 Environment Specifications

| Component          | Development | Staging | Production |
|--------------------|-------------|---------|------------|
| **Backend Server** | Local / Docker | DigitalOcean Droplet | DigitalOcean Droplet (load-balanced) |
| **Database**       | Local PostgreSQL 15 | Managed PostgreSQL 15 | Managed PostgreSQL 15 (HA) |
| **Frontend Build** | Vite dev server | Nginx serving production build | Nginx serving production build + CDN |
| **HTTPS**          | No (HTTP only) | Yes (Let's Encrypt) | Yes (Let's Encrypt) |
| **RLS Enabled**    | Optional (toggle via script) | Yes | Yes |
| **Logging**        | Console + File (Serilog) | File + Seq (centralized) | File + Seq + AlertManager |
| **Session Store**  | In-memory | PostgreSQL | Redis (distributed) |

### 7.2 Browser and OS Matrix

| Browser | Version | Operating System | Priority |
|---------|---------|------------------|----------|
| **Google Chrome** | Latest (stable) | Windows 11, macOS | P0 (primary target) |
| **Mozilla Firefox** | Latest (stable) | Windows 11, macOS | P1 (secondary target) |
| **Microsoft Edge** | Latest (stable) | Windows 11 | P1 (secondary target) |
| **Safari** | Latest (stable) | macOS, iOS | P2 (tertiary, mobile) |
| **Chrome Mobile** | Latest (stable) | Android | P2 (tertiary, mobile) |

**Note:** Focus on desktop browsers for MVP; mobile support is limited to basic responsive design.

### 7.3 Test Environment Access

- **Staging URL:** `https://staging.tenxempires.com` (example)
- **Test Database:** `staging-db.tenxempires.com:5432`
- **Credentials:** Stored in secure vault (e.g., 1Password, Azure Key Vault)
- **Access Control:** VPN required for database access, HTTPS for web access
- **Deployment:** Automated via GitHub Actions on merge to `main` (staging) and `release` (production)

---

## 8. Testing Tools

| Tool | Purpose | License | Team Members Trained |
|------|---------|---------|----------------------|
| **Vitest** | Frontend unit & component testing | MIT | All frontend devs |
| **React Testing Library** | React component testing utilities | MIT | All frontend devs |
| **xUnit** | Backend unit & integration testing | Apache 2.0 | All backend devs |
| **FluentAssertions** | Readable C# test assertions | Apache 2.0 | All backend devs |
| **Bogus** | Fake data generation (C#) | MIT | All backend devs |
| **Testcontainers** | Docker containers for integration tests | MIT | Backend devs (advanced) |
| **Respawn** | Database reset/cleanup for tests | Apache 2.0 | Backend devs (advanced) |
| **Playwright** | E2E and visual regression testing | Apache 2.0 | QA engineers, 1-2 devs |
| **k6** | Load and performance testing | AGPL v3 | QA lead, DevOps |
| **Postman / Insomnia** | Manual API testing | Free/Proprietary | All team members |
| **Lighthouse** | Frontend performance auditing | Apache 2.0 | Frontend devs, QA |
| **axe-core** | Accessibility testing | MPL 2.0 | QA engineers |
| **OWASP ZAP** | Security scanning (DAST) | Apache 2.0 | Security engineer (external) |
| **npm audit / dotnet list package --vulnerable** | Dependency vulnerability scanning | Built-in | DevOps (automated in CI) |
| **Serilog** | Application logging | Apache 2.0 | All developers |
| **Seq** | Centralized log viewer | Proprietary (free tier) | DevOps, QA lead |
| **GitHub Issues** | Bug tracking and project management | Free (GitHub) | All team members |

---

## 9. CI/CD Integration

### 9.1 GitHub Actions Pipelines

#### Pipeline 1: Pull Request Validation
**Trigger:** On every PR to `main`

**Steps:**
1. Checkout code
2. Install dependencies (npm, dotnet restore)
3. Lint and type-check (eslint, TypeScript, dotnet format)
4. Run unit tests:
   - Frontend: `npm run test` (Vitest)
   - Backend: `dotnet test` (xUnit)
5. Build projects:
   - Frontend: `npm run build` (Vite)
   - Backend: `dotnet build`
6. Generate code coverage reports (upload to Codecov or similar)
7. Comment on PR with test results and coverage changes

**Pass Criteria:**
- All linters pass (0 errors)
- All unit tests pass (100%)
- Code coverage does not decrease by >2%
- Build succeeds

---

#### Pipeline 2: Main Branch Integration
**Trigger:** On merge to `main`

**Steps:**
1. All steps from Pipeline 1
2. Run integration tests:
   - Backend integration tests with Testcontainers
3. Run E2E critical path tests (subset of Playwright tests, ~10 min)
4. Build Docker images (backend, frontend nginx)
5. Push Docker images to registry (DigitalOcean Container Registry)
6. Deploy to staging environment
7. Run smoke tests on staging
8. Send Slack notification with deploy status

**Pass Criteria:**
- All tests pass (unit, integration, E2E critical path)
- Deployment succeeds
- Smoke tests pass on staging

---

#### Pipeline 3: Nightly Regression Suite
**Trigger:** Scheduled (2:00 AM UTC)

**Steps:**
1. Checkout `main` branch
2. Deploy to dedicated nightly test environment
3. Run full E2E regression suite (Playwright, ~60 min)
4. Run visual regression tests (baseline comparison)
5. Run performance tests (k6 scripts)
6. Run security scans:
   - `npm audit` / `dotnet list package --vulnerable`
   - OWASP ZAP (if configured)
7. Generate test report (HTML/Allure)
8. Send email/Slack notification with results

**Pass Criteria:**
- ≥95% E2E tests pass
- No new security vulnerabilities (High/Critical)
- Performance metrics within acceptable range

---

#### Pipeline 4: Pre-Release Validation
**Trigger:** On tag `v*.*.*` (e.g., `v1.0.0`)

**Steps:**
1. All steps from Pipeline 2
2. Run full E2E regression suite
3. Run full performance test suite
4. Generate release notes from changelog
5. Create GitHub release draft
6. Await manual approval from QA lead
7. Deploy to production (after approval)
8. Run smoke tests on production
9. Send announcement notification

**Pass Criteria:**
- 100% critical E2E tests pass
- Manual QA lead approval
- Smoke tests pass on production

---

### 9.2 CI/CD Best Practices

- **Fail Fast:** Run fastest tests first (linters, unit tests) to provide quick feedback
- **Parallelization:** Run frontend and backend tests in parallel
- **Caching:** Cache dependencies (npm modules, NuGet packages) to speed up builds
- **Flaky Test Handling:** Auto-retry flaky tests (max 2 retries), flag persistent failures
- **Test Reports:** Archive test results (JUnit XML) and artifacts (screenshots, logs)
- **Branch Protection:** Require passing CI checks before merging PRs
- **Environment Parity:** Use Docker to ensure test environment matches production

---

## 10. Test Schedule

The testing process is integrated into the agile development workflow (2-week sprints).

### 10.1 Sprint Testing Activities

| Sprint Phase | Week | Testing Activities | Responsible |
|--------------|------|-------------------|-------------|
| **Sprint Planning** | Week 1, Day 1 | Review user stories, identify test scenarios, update test plan | QA Lead + Team |
| **Development** | Week 1-2 | Developers write unit tests alongside code, QA reviews test coverage | Developers |
| **Feature Complete** | Week 2, Day 3 | Feature code freeze, integration tests written, manual exploratory testing begins | QA Engineers |
| **Regression Testing** | Week 2, Day 4 | Run full automated regression suite, fix failing tests | QA + Developers |
| **UAT / Demo** | Week 2, Day 5 | Stakeholder demo, UAT on staging, gather feedback | QA + PM + Stakeholders |
| **Release** | Week 2, Day 5 (or next sprint) | Deploy to production (if acceptance criteria met), smoke tests | DevOps + QA |

### 10.2 Testing Milestones

| Milestone | Target Date | Exit Criteria |
|-----------|-------------|---------------|
| **MVP Feature Complete** | Week 8 | All P0 features implemented, unit tests passing |
| **Alpha Release** | Week 10 | All P0/P1 tests passing, ready for internal testing |
| **Beta Release** | Week 12 | All automated tests passing, UAT complete, ≤3 open P2 bugs |
| **Production Release (v1.0)** | Week 14 | All release acceptance criteria met (see Section 11) |

### 10.3 Ongoing Testing

- **Daily:** Developers run unit tests locally before committing
- **Per PR:** Automated CI pipeline runs unit + build tests
- **Per Merge:** Integration and critical E2E tests run on staging
- **Nightly:** Full regression suite + performance tests + security scans
- **Weekly:** QA team conducts exploratory testing sessions (2-3 sessions)
- **Monthly:** Security audit, dependency updates, performance benchmarking

---

## 11. Release Acceptance Criteria

### 11.1 Go/No-Go Criteria (Blockers)

A release **cannot proceed** unless all of the following are met:

- [ ] **All P0 test cases pass (100%)** - Critical functionality must work flawlessly
- [ ] **Zero open P0/P1 defects** - No critical or high-priority bugs
- [ ] **Code coverage ≥75% for backend services** (GameService, TurnService, ActionService, AuthService)
- [ ] **Code coverage ≥70% for critical frontend components** (auth, game map, save/load)
- [ ] **Security scan passes** - No High/Critical vulnerabilities in dependencies
- [ ] **RLS enforcement validated** - Security tests TC-SEC-02.* pass
- [ ] **Performance targets met** (see Section 11.4)
- [ ] **Staging smoke tests pass** - Login, create game, save/load, end turn work
- [ ] **Database migrations tested** - DbUp scripts run successfully on staging, rollback plan documented
- [ ] **Manual QA lead approval** - Final sign-off from QA lead
- [ ] **UAT sign-off** - Key stakeholders approve release
- [ ] **Release notes prepared** - Changelog and known issues documented

### 11.2 Acceptable Risks (Document/Accept)

These items are **not blockers** but must be documented and accepted:

- [ ] **≤3 open P2 defects** - Medium-priority bugs with documented workarounds
- [ ] **P1 test cases pass at ≥95%** - Minor failures acceptable if not user-facing
- [ ] **Known limitations documented** - Features deferred to post-MVP listed in release notes
- [ ] **Performance degradation <5%** - Slight performance regression acceptable if within targets
- [ ] **Mobile experience limited** - Basic responsive design only; full mobile optimization deferred

### 11.3 Deferred Items (Post-Launch Backlog)

These items are tracked but **not required for launch**:

- P3/P4 defects tracked for future sprints
- Accessibility improvements (WCAG 2.1 AA compliance)
- Additional browser support (older versions, niche browsers)
- Advanced analytics and telemetry
- Multiplayer features (future roadmap)

### 11.4 Performance Acceptance Targets

Release must meet these performance benchmarks:

| Metric | Target (P95) | Measurement Tool |
|--------|--------------|------------------|
| Game state fetch | <300ms | k6 (TC-PERF-01) |
| Game action (move unit) | <500ms | k6 (TC-PERF-02) |
| End turn (AI processing) | <6s total | Manual + Serilog |
| Map tiles fetch | <1000ms | k6 (TC-PERF-04) |
| Frontend Lighthouse score | ≥90 | Lighthouse (TC-PERF-05) |
| Canvas frame rate | 60 FPS (16.67ms/frame) | Chrome DevTools |

### 11.5 Security Acceptance Checklist

- [ ] CSRF protection verified (TC-SEC-01)
- [ ] RLS enforcement verified (TC-SEC-02)
- [ ] Session timeout tested (TC-SEC-03)
- [ ] Rate limiting verified (TC-SEC-04)
- [ ] Input validation tested (TC-SEC-05)
- [ ] No High/Critical CVEs in dependencies
- [ ] HTTPS enforced in production
- [ ] Security headers configured (CSP, HSTS, X-Frame-Options)

---

## 12. Roles and Responsibilities

### 12.1 Team Structure

| Role | Responsibilities | FTE | Names |
|------|------------------|-----|-------|
| **QA Lead** | Own test plan, coordinate testing activities, report status to PM, approve releases | 100% | _(TBD)_ |
| **QA Engineers** | Write and maintain automated tests (E2E, integration), execute manual exploratory testing, report bugs | 100% (2x) | _(TBD)_ |
| **Frontend Developers** | Write unit tests for React components and utilities, fix frontend bugs, support E2E test maintenance | 100% (2x) | _(TBD)_ |
| **Backend Developers** | Write unit and integration tests for services and controllers, fix backend bugs, support API testing | 100% (2x) | _(TBD)_ |
| **DevOps Engineer** | Maintain test environments, CI/CD pipelines, test infrastructure (Docker, databases), performance monitoring | 50% | _(TBD)_ |
| **Project Manager** | Prioritize bugs, manage test schedule, stakeholder communication, UAT coordination | 25% | _(TBD)_ |
| **Security Engineer** | Security testing, penetration testing, vulnerability assessment (external consultant) | As needed | _(External)_ |

### 12.2 Detailed Responsibilities

#### QA Lead
- **Planning:** Maintain and update test plan, define test strategy, identify test scenarios from requirements
- **Execution:** Coordinate test activities across sprints, track test progress, report metrics
- **Quality Gates:** Approve/reject releases based on acceptance criteria, escalate blocking issues
- **Tooling:** Evaluate and maintain testing tools, ensure proper training
- **Reporting:** Weekly status reports, test metrics dashboard, release readiness reports

#### QA Engineers
- **Automation:** Write and maintain E2E tests (Playwright), visual regression tests, API tests
- **Manual Testing:** Execute exploratory testing sessions, usability testing, cross-browser testing
- **Bug Management:** Report bugs with detailed reproduction steps, verify fixes, regression testing
- **Test Data:** Create and maintain test data sets, seed scripts, test fixtures
- **Documentation:** Update test case documentation, maintain test environment runbooks

#### Developers (Frontend & Backend)
- **Unit Testing:** Write unit tests for all new code, maintain >75% coverage
- **Test-Driven Development:** Write tests before/alongside code (where applicable)
- **Bug Fixing:** Fix bugs assigned by QA team, provide root cause analysis
- **Code Review:** Review test code in PRs, ensure testability of new features
- **Support:** Help QA team with test environment setup, debugging test failures

#### DevOps Engineer
- **Infrastructure:** Provision and maintain test environments (staging, nightly, performance)
- **CI/CD:** Maintain GitHub Actions pipelines, ensure test reliability, optimize build times
- **Monitoring:** Set up logging (Serilog, Seq), performance monitoring, alert configuration
- **Database:** Manage test databases, migration testing, backup/restore procedures
- **Tooling:** Docker containers, Testcontainers setup, k6 infrastructure

#### Project Manager
- **Backlog:** Ensure test tasks included in sprint planning, prioritize testing vs. feature work
- **Triage:** Lead bug triage meetings, assign priority/severity labels
- **Communication:** Stakeholder updates on quality status, manage UAT sessions
- **Risk Management:** Identify quality risks, ensure mitigation plans in place

---

## 13. Bug Reporting Procedures

### 13.1 Bug Reporting Tool

**Primary Tool:** GitHub Issues (integrated with project repository)

**Alternative:** Jira (if team prefers dedicated bug tracking)

### 13.2 Bug Report Template

When creating a new bug report, use this template:

```markdown
## Bug Summary
[Clear, concise one-line description]

## Severity / Priority
- **Severity:** P0 (Critical) | P1 (High) | P2 (Medium) | P3 (Low)
- **Type:** Bug | UI/UX | Performance | Security

## Environment
- **Browser/OS:** Chrome 120 / Windows 11
- **Environment:** Staging / Production
- **URL:** https://staging.tenxempires.com/game/123
- **User Account:** test1@example.com (if relevant)

## Steps to Reproduce
1. Navigate to /game/current
2. Click on Warrior unit at position (10, 10)
3. Attempt to move to position (11, 10)
4. Observe error

## Expected Result
Unit should move to (11, 10) and position should update on map

## Actual Result
Unit does not move, error notification appears: "Movement failed"

## Evidence
- Screenshot: [attach screenshot]
- Video: [attach screen recording if helpful]
- Console Logs: [browser console errors]
- Network Logs: [HAR file or relevant API responses]
- Server Logs: [Serilog correlation ID or log excerpt]

## Additional Context
- Issue started after deploy on 2025-10-28
- Works correctly in Firefox, only fails in Chrome
- Related to PR #123 (if known)

## Suggested Fix (Optional)
[Developer or QA suggestion for fix]
```

### 13.3 Severity / Priority Definitions

| Severity | Definition | Response Time | Examples |
|----------|------------|---------------|----------|
| **P0 (Critical)** | Blocks core functionality, security breach, data loss | Immediate (same day) | Cannot log in, game state corruption, RLS bypass |
| **P1 (High)** | Major functionality broken, workaround exists | 1-2 days | Save/load fails intermittently, AI turn hangs |
| **P2 (Medium)** | Minor functionality issue, cosmetic bug | 1 week | Unit sprite misaligned, notification typo |
| **P3 (Low)** | Enhancement, nice-to-have, edge case | Next sprint or backlog | Improve button hover effect, add keyboard shortcut |

### 13.4 Bug Workflow

```
[New] → [Triaged] → [Assigned] → [In Progress] → [Fixed] → [Verification] → [Closed]
          ↓                                          ↓
      [Rejected]                               [Reopened] (if verification fails)
```

**Workflow Steps:**

1. **New:** Bug reported by QA, developer, or user
2. **Triaged:** QA Lead + Dev Lead review, assign severity/priority, determine if valid
3. **Rejected:** If not reproducible, duplicate, or working as intended (with explanation)
4. **Assigned:** Allocated to specific developer
5. **In Progress:** Developer actively working on fix
6. **Fixed:** Fix merged to main branch, deployed to staging
7. **Verification:** QA verifies fix on staging, runs regression tests
8. **Reopened:** If verification fails, bug returns to "In Progress"
9. **Closed:** Fix confirmed, issue closed with reference to commit/PR

### 13.5 Bug Triage Process

**Frequency:** Daily (or as needed for critical bugs)

**Participants:** QA Lead, Dev Lead, Project Manager

**Agenda:**
1. Review new bugs (5-10 min)
2. Assign severity/priority (use definitions above)
3. Allocate to sprint or backlog
4. Assign to developer
5. Identify blockers and escalate

**Triage Decision Matrix:**

| Severity | In Sprint? | Assign Immediately? |
|----------|-----------|---------------------|
| P0 | Yes | Yes (interrupt current work) |
| P1 | Yes (if capacity) | Yes |
| P2 | Maybe (prioritize vs. features) | No (assign in sprint planning) |
| P3 | No (backlog) | No |

### 13.6 Bug Metrics and Reporting

**Weekly Metrics Report:**
- Open bugs by severity (P0/P1/P2/P3)
- Bugs opened vs. closed this week
- Average time to fix by severity
- Top bug categories (auth, gameplay, UI, etc.)
- Flaky test count and status

**Monthly Trends:**
- Bug discovery rate over time
- Defect density (bugs per 1000 LOC)
- Escaped defects (found in production)
- Test effectiveness (bugs caught in testing vs. production)

**Reporting Tool:** GitHub Issues dashboard, or custom dashboard (e.g., Grafana with GitHub API)

---

## 14. Risk Management

### 14.1 Test-Related Risks

| Risk | Impact | Probability | Mitigation Strategy | Owner |
|------|--------|-------------|---------------------|-------|
| **Visual regression tests fail due to font rendering differences** | Medium | High | Use headless Chromium in Docker with locked font versions; configure `maxDiffPixels` threshold | QA Lead |
| **Test database state pollution between E2E tests** | High | Medium | Implement Respawn for database reset; wrap tests in transactions; use isolated test data | QA Engineers |
| **Testcontainers slow on Windows environments** | Medium | High | Use Docker Compose with persistent volumes; provide macOS/Linux dev options; cache containers | DevOps |
| **Flaky E2E tests cause false negatives** | High | Medium | Implement retry logic (max 2); use `waitForSelector` with timeouts; add debug screenshots; quarantine persistently flaky tests | QA Engineers |
| **Performance tests not representative of production load** | Medium | Medium | Use realistic test data volume (100 users, 500 games); test against staging with production-like specs | QA Lead |
| **RLS complexity delays testing** | High | Low | Create RLS test utilities early; provide toggle script for dev; document RLS policies clearly | Backend Devs |
| **Production hotfix needed, bypassing full test suite** | High | Low | Define critical smoke test subset (5 min) for emergency deploys; require post-deploy verification | QA Lead |
| **Save file schema change breaks old saves** | High | Medium | TC-SAVE-04 must validate migration path; keep legacy parser; version-control save file fixtures | Backend Devs |
| **Insufficient test coverage for edge cases** | Medium | Medium | Allocate 20% of QA time to exploratory testing; maintain edge case test backlog | QA Lead |
| **Test environment drift from production** | High | Low | Use Docker for environment parity; automate environment provisioning; monthly production-staging sync | DevOps |
| **Lack of security testing expertise** | High | Medium | Hire external security consultant for penetration testing; train team on OWASP Top 10 | PM |
| **No access to production-like data for testing** | Medium | High | Generate synthetic data with Bogus; create data generation scripts; capture anonymized prod snapshots | QA Engineers |

### 14.2 Quality Risks (Product)

| Risk | Impact | Probability | Mitigation Strategy |
|------|--------|-------------|---------------------|
| **Canvas rendering issues on low-end devices** | Medium | Medium | Performance profiling on low-end hardware; canvas optimization (batch rendering); fallback 2D mode |
| **Session timeout UX causes player frustration** | Medium | High | Implement keepalive; clear warning before timeout; auto-save progress; remember returnUrl |
| **AI turn processing too slow (>10s)** | High | Medium | Profile and optimize AI algorithms; add progress indicator; allow AI turn cancellation |
| **Save file corruption due to concurrent writes** | High | Low | Implement database-level locking; validate save integrity on write; backup autosaves |
| **Cross-browser compatibility issues** | Medium | Medium | Test on Firefox/Edge in addition to Chrome; use polyfills; avoid vendor-specific APIs |
| **Memory leaks in long gameplay sessions** | Medium | Low | Profile memory usage; dispose React components properly; clear event listeners |

### 14.3 Risk Monitoring

**Frequency:** Weekly risk review in sprint retrospective

**Process:**
1. Review risk register
2. Update probability/impact based on recent findings
3. Evaluate mitigation effectiveness
4. Add new risks identified during sprint
5. Escalate high-impact risks to PM/stakeholders

**Escalation Criteria:**
- Any new **High Impact + High Probability** risk
- Existing risk mitigation not working
- Risk materializes (becomes an issue)

---

## 15. Appendices

### 15.1 Acronyms and Definitions

| Term | Definition |
|------|------------|
| **P0/P1/P2/P3** | Priority levels (0=Critical, 3=Low) |
| **RLS** | Row-Level Security (PostgreSQL feature) |
| **CSRF** | Cross-Site Request Forgery |
| **E2E** | End-to-End (testing) |
| **RTL** | React Testing Library |
| **FTE** | Full-Time Equivalent |
| **UAT** | User Acceptance Testing |
| **HAR** | HTTP Archive (browser network log format) |
| **CVE** | Common Vulnerabilities and Exposures |
| **WCAG** | Web Content Accessibility Guidelines |

### 15.2 References

- **Project Repository:** https://github.com/[org]/tenxempires (example)
- **Architecture Documentation:** `docs/architecture/project-structure.md`
- **API Documentation:** `docs/openapi.yaml`
- **DbUp Migrations:** `db/migrations/`
- **Testing Documentation:**
  - Frontend: `tenxempires.client/README.md`
  - Backend: `TenXEmpires.Server.Tests/README.md`
- **Deployment Guide:** `TenXEmpires.Server/Dockerfile`

### 15.3 Change History

| Version | Date | Author | Changes |
|---------|------|--------|---------|
| 1.0 | 2025-10-15 | QA Team | Initial test plan (basic outline) |
| 2.0 | 2025-10-20 | QA Team | Expanded test scenarios, added tools section |
| 3.0 | 2025-10-29 | QA Team | Complete rewrite: added traceability matrix, detailed test cases, data management, CI/CD integration, risk register |

---

## 16. Sign-Off

This test plan requires approval from the following stakeholders before execution:

| Role | Name | Signature | Date |
|------|------|-----------|------|
| **QA Lead** | _________________ | _________________ | ________ |
| **Dev Lead (Backend)** | _________________ | _________________ | ________ |
| **Dev Lead (Frontend)** | _________________ | _________________ | ________ |
| **Project Manager** | _________________ | _________________ | ________ |
| **Product Owner** | _________________ | _________________ | ________ |

---

**End of Test Plan v3.0**

*For questions or feedback on this test plan, contact the QA Lead or open a GitHub Discussion.*