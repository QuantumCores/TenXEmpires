Here is the comprehensive test plan for the TenX Empires project.

# Test Plan for TenX Empires

---

### 1. Introduction and Objectives

This document outlines the comprehensive testing strategy for the **TenX Empires** project, a turn-based 4X strategy game featuring a .NET 8 backend and a React/TypeScript frontend.

The primary objectives of this test plan are:
*   **Ensure Quality and Stability:** Verify that the application is robust, reliable, and free of critical defects before release.
*   **Validate Functional Requirements:** Confirm that all specified features, from user authentication to core gameplay mechanics, function as intended.
*   **Verify State Consistency:** Guarantee that the game state remains consistent between the server (the source of truth) and the client, especially during actions and turn transitions.
*   **Assess Performance and Security:** Ensure the application is performant under typical loads and secure against common web vulnerabilities.
*   **Guarantee a Positive User Experience:** Validate that the user interface is intuitive, responsive, and provides clear feedback for all interactions and error conditions.

---

### 2. Scope of Testing

#### 2.1 In-Scope

*   **Frontend (Client Application):**
    *   All React components and UI elements.
    *   Client-side state management (Zustand and React Query).
    *   User authentication flows (Login, Registration, Password Reset).
    *   API integration and server communication.
    *   Core gameplay interactions on the HTML5 Canvas map.
    *   Modals, notifications, and other UI feedback mechanisms.
    *   Client-side routing and browser history management.
*   **Backend (Server API):**
    *   All REST API endpoints.
    *   Server-side business logic (game actions, turn processing, combat).
    *   Database interactions (data persistence, retrieval, and integrity).
    *   Authentication, authorization, and session management.
    *   Security features, including CSRF protection and rate limiting.
    *   Idempotency of critical POST operations.
*   **Integration:**
    *   End-to-end testing of user flows across the client and server.
    *   Data consistency between the client's cached state and the backend database.

#### 2.2 Out-of-Scope

*   Testing of third-party libraries and frameworks (e.g., React, .NET, Vite) beyond their integration with the application.
*   Testing the underlying infrastructure (e.g., cloud provider, PostgreSQL server) itself, though its interaction with the application is in scope.
*   Comprehensive load testing simulating massive concurrent users (initial performance testing will focus on API response times and client rendering).

---

### 3. Types of Tests to be Performed

A multi-layered testing approach will be adopted to ensure thorough coverage.

*   **Unit Testing:**
    *   **Frontend:** Individual React components, hooks, and utility functions (e.g., `hexGeometry.ts`, `pathfinding.ts`) will be tested in isolation using **Vitest** and **React Testing Library**. The focus will be on component rendering based on props and state, and the correctness of pure logic functions.
    *   **Backend:** Business logic within services (`GameService`, `TurnService`, `ActionService`) and domain models will be tested using a framework like **xUnit** or **NUnit**. Repositories will be mocked to isolate the logic.

*   **Integration Testing:**
    *   **Frontend:** Testing the integration of multiple components, such as a form component with its parent page, and verifying client-side state management (Zustand, React Query) updates the UI correctly.
    *   **Backend:** Testing the interaction between the API controllers, services, and a real test database. This will verify the entire request pipeline, including data persistence and retrieval.

*   **API Testing:**
    *   Dedicated testing of each API endpoint using tools like **Postman** or **Insomnia**. Tests will cover valid requests, invalid inputs, error responses (4xx/5xx), authentication/authorization headers, and CSRF token validation.

*   **End-to-End (E2E) Testing:**
    *   Automated tests simulating complete user scenarios in a browser environment using a framework like **Playwright** or **Cypress**. These tests will cover critical user flows from start to finish.

*   **Visual Regression Testing:**
    *   Due to the complexity of the HTML5 Canvas map, visual regression tests will be implemented as part of the E2E suite. Snapshots of the game map will be taken and compared against baseline images to automatically detect unintended visual changes in rendering.

*   **Security Testing:**
    *   Manual and automated checks for common vulnerabilities, with a strong focus on:
        *   **CSRF:** Verifying that all state-changing requests (POST, PUT, DELETE) are rejected without a valid CSRF token.
        *   **Authentication:** Ensuring protected routes are inaccessible to unauthenticated users.
        *   **Authorization:** Confirming users cannot perform actions on games or data they do not own.

*   **Performance Testing:**
    *   **Backend:** Basic load testing on critical API endpoints (e.g., game state fetching, actions) to measure response times and identify bottlenecks using a tool like **k6** or **JMeter**.
    *   **Frontend:** Analyzing client-side rendering performance, especially for the canvas map, using browser developer tools (Lighthouse, Profiler).

*   **Manual Exploratory Testing:**
    *   Unscripted testing by the QA team to discover defects and usability issues that automated tests might miss. This is particularly important for the interactive game map.

---

### 4. Test Scenarios for Key Functionalities

#### 4.1 User Authentication
*   **TC-AUTH-01 (Registration):** Verify a user can successfully create a new account with valid credentials. Test edge cases like duplicate emails and password policy violations.
*   **TC-AUTH-02 (Login):** Verify a user can log in with correct credentials and is redirected to the appropriate page (`/game/current`). Test invalid credentials and locked accounts.
*   **TC-AUTH-03 (Session Management):** Verify the "Remember Me" functionality. Test session expiration due to inactivity (`IdleSessionProvider`) and the subsequent modal flow.
*   **TC-AUTH-04 (CSRF Protection):** Verify that any `POST`/`DELETE` request sent without a valid `X-XSRF-TOKEN` header is rejected with a 4xx error.
*   **TC-AUTH-05 (Password Reset):** Verify the full "Forgot Password" flow, from requesting a reset link to successfully setting a new password.

#### 4.2 Game Management
*   **TC-GAME-01 (Create New Game):** Verify a user can create a new game via the "Start New Game" modal. Test the flow where a user already has an active game.
*   **TC-GAME-02 (Redirect to Current Game):** Verify that navigating to `/game/current` correctly redirects the user to their most recent active game or to the "Start New Game" flow if none exists.
*   **TC-GAME-03 (Delete Game):** Verify a user can delete an active game.

#### 4.3 Core Gameplay
*   **TC-PLAY-01 (Map Rendering):** Verify that the game map, including tiles, units, and cities, renders correctly on the HTML5 canvas. Use visual regression tests to confirm consistency.
*   **TC-PLAY-02 (Unit Movement):** Select a unit and issue a valid move command within its range. Verify the unit's position is updated on the client and server. Attempt an invalid move (out of range, blocked tile) and verify it is rejected with a user-facing notification.
*   **TC-PLAY-03 (Unit Combat):** Order a unit to attack an enemy unit. Verify that HP is updated for both units according to server-side combat calculations.
*   **TC-PLAY-04 (End Turn):** Click the "End Turn" button. Verify the turn number increments, the AI takes its turn, and the game state is updated correctly. Verify autosave is created.
*   **TC-PLAY-05 (State Polling):** While the AI turn is in progress (`turnInProgress: true`), verify the client polls the server for state updates and the "AI is thinking" overlay is displayed.
*   **TC-PLAY-06 (Game Over):** Complete a game by meeting victory or defeat conditions. Verify the `ResultOverlay` is displayed with the correct status.

#### 4.4 Save/Load System
*   **TC-SAVE-01 (Manual Save):** Create a manual save in an empty slot. Verify the save appears in the list with the correct turn number and timestamp.
*   **TC-SAVE-02 (Overwrite Save):** Overwrite an existing manual save. Verify the overwrite confirmation modal appears and that the save is updated upon confirmation.
*   **TC-SAVE-03 (Load Game):** Load a game from a manual save or autosave. Verify the game state is correctly restored to the state at the time of the save.
*   **TC-SAVE-04 (Schema Mismatch):** Attempt to load a save file with an incompatible schema version. Verify the `ErrorSchemaModal` is displayed and blocks gameplay.

---

### 5. Test Environment

| Component          | Specification                                                              |
| ------------------ | -------------------------------------------------------------------------- |
| **Server**         | Staging environment running the ASP.NET Core application in a Docker container. |
| **Database**       | Dedicated PostgreSQL instance for the staging environment, seeded with test data. |
| **Client Browsers**| Google Chrome (latest), Mozilla Firefox (latest), Microsoft Edge (latest). |
| **Mobile Browsers**| Basic responsive checks on Chrome for Android and Safari for iOS.          |
| **Operating Systems**| Windows 11, macOS (latest).                                                |

---

### 6. Testing Tools

| Tool                  | Purpose                                        |
| --------------------- | ---------------------------------------------- |
| **Vitest & RTL**      | Frontend Unit & Component Testing              |
| **xUnit / NUnit**     | Backend Unit & Integration Testing             |
| **Playwright / Cypress** | End-to-End and Visual Regression Testing     |
| **Postman / Insomnia**| Manual & Automated API Testing                 |
| **k6 / JMeter**       | Backend Performance & Load Testing             |
| **Browser DevTools**  | Frontend Performance Profiling & Debugging     |
| **GitHub Issues / Jira** | Bug Tracking and Project Management          |

---

### 7. Test Schedule

The testing process will be integrated into the agile development workflow.
*   **Sprint Testing:** During each sprint, new features will undergo unit, integration, and manual testing.
*   **Regression Testing:** Before each release, a full suite of automated regression tests (E2E, API) will be executed to ensure existing functionality is not broken.
*   **UAT Phase:** A User Acceptance Testing phase will be conducted before major releases, involving key stakeholders.

---

### 8. Acceptance Criteria for Tests

*   **Test Case Execution:** A test case is considered **Passed** if the actual result matches the expected result for all steps. It is **Failed** if any step's actual result deviates from the expected result.
*   **Feature Acceptance:** A feature is ready for release when all associated high-priority test cases pass, there are no open critical or major bugs, and it meets the defined requirements.
*   **Build Acceptance:** A build is accepted into the testing environment if it passes a basic smoke test suite that covers critical application flows like login and starting a game.

---

### 9. Roles and Responsibilities

| Role                | Responsibilities                                                                   |
| ------------------- | ---------------------------------------------------------------------------------- |
| **Developers**      | Writing and maintaining unit tests. Fixing bugs reported by the QA team.            |
| **QA Engineers**    | Creating and maintaining the test plan, writing and executing automated integration and E2E tests, performing manual exploratory testing, reporting bugs. |
| **DevOps**          | Maintaining the test environments and CI/CD pipelines for automated testing.        |
| **Project Manager** | Overseeing the testing schedule and prioritizing bug fixes.                          |

---

### 10. Bug Reporting Procedures

1.  **Tool:** All bugs will be reported and tracked using **GitHub Issues** (or a similar tool like Jira).
2.  **Reporting:** When a bug is found, the reporter will create a new issue with the following information:
    *   **Title:** A clear, concise summary of the bug.
    *   **Description:** A detailed explanation of the issue.
    *   **Steps to Reproduce:** A numbered list of steps to reliably reproduce the bug.
    *   **Expected Result:** What the application should have done.
    *   **Actual Result:** What the application actually did.
    *   **Environment:** Browser version, OS, etc.
    *   **Attachments:** Screenshots, videos, or console logs.
    *   **Labels:** Priority (Critical, High, Medium, Low), Type (Bug, UI/UX, Performance).
3.  **Triage:** The project manager and lead developer will triage new bugs, assign a priority, and allocate them to a developer for fixing.
4.  **Verification:** Once a bug is fixed, it is assigned back to the QA team for verification in the staging environment. If verified, the issue is closed. If not, it is reopened with comments.