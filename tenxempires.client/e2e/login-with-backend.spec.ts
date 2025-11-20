import { test, expect, Route } from '@playwright/test'
import { LoginPage } from './pages/LoginPage'
import { createTestUser } from './helpers/api'

/**
 * Playwright E2E tests covering TC-AUTH-02 scenarios
 * Requires the backend server to be running
 */
test.describe('User Login (TC-AUTH-02)', () => {
  let loginPage: LoginPage

  test.beforeEach(async ({ page }) => {
    loginPage = new LoginPage(page)
  })

  test('TC-AUTH-02.1: Successful login with Remember Me', async ({ page, request }) => {
    const testUser = await createTestUser(request)
    expect(testUser.success).toBeTruthy()

    await loginPage.goto()
    expect(await loginPage.isLoaded()).toBeTruthy()

    // User interacts with Remember Me checkbox before submitting
    await loginPage.fillCredentials(testUser.email, testUser.password)
    await loginPage.setRememberMe(false)
    await loginPage.setRememberMe(true)
    await loginPage.submit()

    await page.waitForURL('**/game/current', { waitUntil: 'networkidle' })
    await expect(page).toHaveURL(/\/game\/current/)
  })

  test('TC-AUTH-02.2: Invalid credentials show generic error', async ({ request }) => {
    const testUser = await createTestUser(request)
    expect(testUser.success).toBeTruthy()

    await loginPage.goto()
    expect(await loginPage.isLoaded()).toBeTruthy()

    await loginPage.fillCredentials(testUser.email, 'WrongPassword123!')
    await loginPage.submit()

    const errorText = await loginPage.getErrorMessage()
    expect(errorText).toBeTruthy()
    expect(errorText?.toLowerCase()).toContain('invalid email or password')
    await expect(loginPage.submitButton).not.toBeDisabled()
  })

  test('TC-AUTH-02.3: Lockout after repeated failures', async ({ page, request }) => {
    const testUser = await createTestUser(request)
    expect(testUser.success).toBeTruthy()

    await loginPage.goto()
    expect(await loginPage.isLoaded()).toBeTruthy()

    const wrongPassword = 'WrongPassword123!'
    let attemptCount = 0

    const handleLoginRequest = async (route: Route) => {
      attemptCount += 1
      if (attemptCount <= 5) {
        await route.fulfill({
          status: 400,
          headers: { 'Content-Type': 'application/json' },
          body: JSON.stringify({
            code: 'INVALID_CREDENTIALS',
            message: 'Invalid email or password.',
          }),
        })
        return
      }

      if (attemptCount === 6) {
        await route.fulfill({
          status: 429,
          headers: { 'Content-Type': 'application/json' },
          body: JSON.stringify({
            code: 'RATE_LIMIT_EXCEEDED',
            message: 'Too many failed login attempts. Account temporarily locked. Try again in 15 minutes.',
            retryAfterSeconds: 5,
          }),
        })
        return
      }

      await route.fulfill({ status: 204 })
    }

    const loginMatchers = ['**/api/auth/login', '**/v1/auth/login']
    await Promise.all(
      loginMatchers.map((matcher) => page.route(matcher, handleLoginRequest))
    )

    try {
      // Perform five failed attempts
      for (let i = 0; i < 5; i += 1) {
        await loginPage.fillCredentials(testUser.email, wrongPassword)
        await loginPage.submit()
        const errorText = await loginPage.getErrorMessage()
        expect(errorText?.toLowerCase()).toContain('invalid email or password')
      }

      // Sixth attempt triggers lockout even with correct password
      await loginPage.fillCredentials(testUser.email, testUser.password)
      await loginPage.submit()

      const lockoutMessage = await loginPage.getErrorMessage()
      expect(lockoutMessage).toBeTruthy()
      expect(lockoutMessage).toContain('Too many failed login attempts')
      await expect(loginPage.submitButton).toBeDisabled()
      await expect(loginPage.retryCountdown).toBeVisible()

      // Wait for retry countdown (~5 seconds)
      await page.waitForTimeout(5200)
      await expect(loginPage.submitButton).not.toBeDisabled()
      await expect(loginPage.retryCountdown).toBeHidden()

      // Attempt login again after lockout expires
      await loginPage.fillCredentials(testUser.email, testUser.password)
      await loginPage.submit()
      await page.waitForURL('**/game/current', { waitUntil: 'networkidle' })
      await expect(page).toHaveURL(/\/game\/current/)
    } finally {
      await Promise.all(
        loginMatchers.map((matcher) => page.unroute(matcher, handleLoginRequest))
      )
    }
  })
})
