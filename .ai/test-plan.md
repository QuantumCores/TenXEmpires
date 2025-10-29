# TenX Empires Test Plan

## 1. Introduction and Objectives
- Ensure the authentication module (login, registration, password recovery, email verification) meets all functional and security requirements.
- Verify consistency between the React/TypeScript client and .NET 8/EF Core backend, including session handling, CSRF protection, and Postgres RLS.
- Confirm stability of single-player core flows (starting a game, saving/loading, turn progression) and integration with analytics and backend systems.
- Detect regressions, security gaps, and performance issues early.

## 2. Test Scope
- **Frontend**: public pages, auth components, routing guards, modals, CSRF and idle-state contexts.
- **Backend**: `AuthController`, Identity logic (sign-in/out, tokens), game/save endpoints, RLS enforcement, error handling.
- **Database**: DbUp migrations, auth/app schemas, data integrity, 30-minute session timeout.
- **Integrations**: React Query cache, fetch helpers (postJson/getJson), Serilog, GitHub Actions pipelines (lint/build/test), analytics batching.
- **Out of scope**: deep DigitalOcean infrastructure validation (monitoring, autoscaling) and full UI usability studies beyond critical flows.

## 3. Test Types
- **Integration tests**: Playwright (E2E auth + game flows); ASP.NET WebApplicationFactory with Postgres (Testcontainers or local).
- **Contract tests**: DTO schema alignment (TypeScript ↔ C#), OpenAPI validation.
- **Security tests**: CSRF, rate limiting, brute-force resilience, security headers, OWASP ZAP scans.
- **Performance tests**: k6 for auth/game endpoints, React render profiling.
- **Regression tests**: smoke + sanity suites post-deploy, covering critical end-to-end paths.
- **Accessibility tests**: axe-core for public/auth views.

## 4. Key Test Scenarios
1. **User registration**: success path (account + session); duplicate email; weak password; missing CSRF.
2. **Login**: valid/invalid credentials, enumeration protection, remember-me, `returnUrl` redirect.
3. **Password recovery**: email validation, rate limits, generic success response.
4. **Email verification**: post-registration modal, resend link, authenticated resend.
5. **Session & CSRF lifecycle**: CsrfProvider init, retry on 403, session expiry, SessionExpired modal, keepalive.
6. **Routing guards**: access to `/game/*` unauthenticated vs authenticated, near-session expiry.
7. **Game start and saves**: create game, save/load, RLS validation, error flows.
8. **Logging & analytics**: Serilog output, analytics events (rate limit, structure).
9. **DbUp migrations**: clean database run, idempotency, forward-only upgrades.

## 5. Test Environment
- **Frontend**: Node 20, Vite dev/prod builds, Chrome and Firefox (desktop).
- **Backend**: .NET 8 SDK, Docker Compose (API + Postgres), production-like config (HTTPS, cookies).
- **Relational DB**: Postgres 15 with DbUp migrations, seeded test data.
- **CI**: GitHub Actions (macOS/Linux runners), automated unit/integration suite with reports.
- **Security**: dedicated staging with production security headers for DAST tooling.

## 6. Testing Tools
- Vitest/Jest + React Testing Library.
- Playwright for UI/E2E coverage.
- xUnit, FluentAssertions, Bogus, Testcontainers for .NET / Respawn.
- k6, autocannon (performance).
- OWASP ZAP, npm audit, `dotnet list package --vulnerable`.
- axe-core CLI, Lighthouse (accessibility).
- ReportPortal or Allure (reporting); GitHub Issues/Projects (defect tracking).
- Serilog test sink, LogViewer for log inspections.

## 7. Schedule
| Phase | Timeline | Activities |
| --- | --- | --- |
| Requirements Analysis | Week 1 | Review PRD, derive test cases |
| Environment Setup | Week 2 | Configure environments, seed data, wire CI |
| Unit & Integration Tests | Weeks 3–4 | Build and run automated suites |
| E2E & Security Tests | Week 5 | Playwright scenarios, OWASP ZAP |
| Performance & Regression | Week 6 | k6 runs, smoke/regression packs |
| Stabilization / UAT | Week 7 | Retests, stakeholder sign-off, final report |

## 8. Acceptance Criteria
- 100% pass rate for critical unit and integration tests in CI (≥80% coverage for auth, game, save modules).
- Zero open defects of severity P0/P1; P2 issues documented with mitigation plan.
- Demonstrated protection against CSRF and brute-force attacks (rate limit, lockout).
- Performance target: <200 ms P95 for auth endpoints, <500 ms for critical game endpoints under expected load.
- UAT approval for registration, login, game start, save/load scenarios.

## 9. Roles and Responsibilities
- **QA Lead**: owns plan execution, scope decisions, risk reporting.
- **Automation QA**: builds and maintains automated suites and CI integration.
- **Manual QA**: executes exploratory, regression, and usability tests.
- **Security Engineer**: OWASP risk assessment, penetration testing on staging.
- **Backend/Frontend Devs**: fix defects, supply test hooks and documentation.
- **DevOps**: maintain environments, pipelines, and test telemetry.

## 10. Bug Reporting Process
1. Reproduce issue, capture logs (Serilog, HAR, responses, traces).
2. Log GitHub issue linked to commit/PR; classify severity, priority, component.
3. Provide steps, expected vs actual result, evidence (screenshots/video).
4. QA Lead triages and assigns to appropriate team (frontend/backend/devops).
5. Post-fix: verify, retest, update status, run targeted regression if needed.
6. Produce periodic (weekly/release) summaries with defect metrics and trends.

