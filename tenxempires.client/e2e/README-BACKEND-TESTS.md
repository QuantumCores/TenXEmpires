# Backend-Dependent E2E Tests

This guide explains how to write and run E2E tests that require the backend server to be running.

## Overview

Some E2E tests need to interact with the backend API to:
- Set up test data (create users, games, etc.)
- Verify API responses
- Test authenticated flows
- Clean up test data after tests

## Configuration

The Playwright configuration (`playwright.config.ts`) is set up to automatically start both the frontend and backend servers separately for reliable test execution.

### How It Works

For E2E tests, Playwright starts both servers independently:
- **Frontend dev server**: `npm run dev` on `http://localhost:5173`
- **Backend API server**: `dotnet run` on `http://localhost:5019`

**Note**: In development, running `dotnet run --project TenXEmpires.Server/TenXEmpires.Server.csproj` automatically starts the frontend via SPA proxy. However, for tests we start both servers separately for better reliability and control.

### Environment Variables

You can control backend behavior with environment variables:

```bash
# Use a different backend URL (e.g., for CI/CD)
# In this case, only the frontend will be started
API_BASE_URL=http://your-backend-url:5019 npm run test:e2e

# Default: Start both frontend and backend servers
npm run test:e2e
```

### Automatic Server Startup

When you run tests, Playwright will:
1. Start the frontend dev server (`npm run dev`) on `http://localhost:5173`
2. Start the backend API server (`dotnet run`) on `http://localhost:5019`
3. Wait for both servers to be ready (checks frontend at `http://localhost:5173` and backend at `/swagger` endpoint)
4. Run the tests
5. Clean up servers when done

## Writing Backend-Dependent Tests

### Using API Helpers

The `e2e/helpers/api.ts` file provides helper functions for common API operations:

```typescript
import { createTestUser, registerUser, loginUser, getCurrentUser } from './helpers/api'

test('my test', async ({ page, request }) => {
  // Create a test user
  const testUser = await createTestUser(request)
  
  // Use the user in your test
  await page.goto('/login')
  // ... login with testUser.email and testUser.password
})
```

### Available API Helpers

- `getCsrfToken(request)` - Get CSRF token from API
- `registerUser(request, email, password, confirm?)` - Register a new user (confirm defaults to password if not provided)
- `loginUser(request, email, password, rememberMe?)` - Login a user
- `logoutUser(request)` - Logout current user
- `getCurrentUser(request)` - Get current authenticated user info
- `createTestUser(request, email?, password?)` - Create a test user with random email

**Important**: The backend API requires a `confirm` field (password confirmation) for registration. The `registerUser` helper automatically uses the password as confirmation if not provided.

### Example Test

```typescript
import { test, expect } from '@playwright/test'
import { createTestUser, getCurrentUser } from './helpers/api'
import { LandingPage } from './pages/LandingPage'

test.describe('Authenticated Flow', () => {
  test('should access protected page after login', async ({ page, request }) => {
    // Setup: Create test user
    const testUser = await createTestUser(request)
    expect(testUser.success).toBeTruthy()

    // Navigate and login
    const landingPage = new LandingPage(page)
    await landingPage.goto()
    await landingPage.clickLogin()
    
    // Fill login form and submit
    // ... (use your LoginPage POM)

    // Verify authentication
    const userResult = await getCurrentUser(request)
    expect(userResult.success).toBeTruthy()
    expect(userResult.user?.email).toBe(testUser.email)
  })
})
```

## Test Organization

### File Naming Convention

- `*.spec.ts` - Regular E2E tests (frontend only)
- `*-with-backend.spec.ts` - Tests that require backend server

### Test Structure

```typescript
test.describe('Feature Name with Backend', () => {
  test.beforeEach(async ({ page }) => {
    // Setup page objects
  })

  test('should do something', async ({ page, request }) => {
    // Use both page (for UI) and request (for API) fixtures
  })

  test.afterEach(async ({ request }) => {
    // Optional: Clean up test data
  })
})
```

## Running Tests

### Run All Tests (including backend-dependent)

```bash
cd tenxempires.client
npm run test:e2e
```

### Run Only Backend-Dependent Tests

```bash
cd tenxempires.client
npx playwright test --grep "with Backend"
```

### Run Specific Test File

```bash
cd tenxempires.client
npx playwright test registration-with-backend.spec.ts
```

### Run with Custom Backend URL

```bash
cd tenxempires.client
# If backend is already running elsewhere, only start frontend
API_BASE_URL=http://your-backend-url:5019 npm run test:e2e
```

### Run from Project Root

You can also run tests from the project root using npm scripts:

```bash
# From project root
npm run test:e2e

# Or specify a test file
npm run test:e2e -- registration
```

## Prerequisites

### Backend Server Requirements

1. **.NET SDK 8.0+** installed
2. **Database** running (PostgreSQL on localhost:5432)
3. **Database migrations** applied
4. **Environment variables** configured (if needed)

### Check Backend is Running

```bash
# Check if backend is accessible
curl http://localhost:5019/v1/auth/csrf

# Or visit Swagger UI
open http://localhost:5019/swagger
```

## Troubleshooting

### Backend Server Won't Start

1. **Check .NET SDK**: `dotnet --version` (should be 8.0+)
2. **Check database**: Ensure PostgreSQL is running
3. **Check ports**: Ensure port 5019 is not in use
4. **Check project path**: Verify `TenXEmpires.Server` directory exists relative to `tenxempires.client`

### Tests Fail with "Connection Refused"

- Frontend or backend server might not be starting
- Check Playwright output for server startup errors
- Verify both servers are accessible:
  - Frontend: `curl http://localhost:5173`
  - Backend: `curl http://localhost:5019/v1/auth/csrf`
- Try running servers manually:
  ```bash
  # Terminal 1 - Frontend
  cd tenxempires.client && npm run dev
  
  # Terminal 2 - Backend
  cd TenXEmpires.Server && dotnet run
  ```

### CSRF Token Errors

- Ensure CSRF endpoint is accessible: `GET /v1/auth/csrf`
- Check that cookies are being set correctly
- Verify `X-XSRF-TOKEN` header is being sent

### Database Errors

- Ensure PostgreSQL is running
- Check connection string in `appsettings.Development.json`
- Verify database exists and migrations are applied

## Best Practices

1. **Isolate Test Data**: Use unique emails/timestamps to avoid conflicts
2. **Clean Up**: Consider cleaning up test data in `afterEach` hooks
3. **Use Fixtures**: Leverage Playwright's `request` fixture for API calls
4. **Error Handling**: Always check `success` property from API helpers
5. **Wait for Servers**: Let Playwright handle server startup automatically
6. **Parallel Tests**: Be careful with shared resources (database, etc.)
7. **API Request Format**: Remember that backend requires `confirm` field for registration - the helper handles this automatically
8. **Error Detection**: Server errors are displayed in a general error div, not field-specific errors - use `hasValidationErrors()` to detect both types

## CI/CD Considerations

For CI/CD pipelines:

1. **Use Environment Variables**: Set `API_BASE_URL` to your test backend
2. **Skip Backend Startup**: If backend is already running in CI
3. **Database Setup**: Ensure test database is set up before tests run
4. **Timeout Configuration**: Increase timeouts for slower CI environments

```yaml
# Example GitHub Actions
env:
  API_BASE_URL: http://localhost:5019
steps:
  - name: Start backend
    run: dotnet run --project TenXEmpires.Server
  - name: Run E2E tests
    run: cd tenxempires.client && npm run test:e2e
```

