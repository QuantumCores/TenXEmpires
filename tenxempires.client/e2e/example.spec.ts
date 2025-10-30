import { test, expect } from '@playwright/test'
import { HomePage } from './pages/HomePage'

/**
 * Example E2E test suite
 * This demonstrates basic Playwright testing patterns
 */
test.describe('Home Page', () => {
  let homePage: HomePage

  test.beforeEach(async ({ page }) => {
    homePage = new HomePage(page)
    await homePage.goto()
  })

  test('should load successfully', async () => {
    const isLoaded = await homePage.isLoaded()
    expect(isLoaded).toBeTruthy()
  })

  test('should have correct title', async ({ page }) => {
    await expect(page).toHaveTitle(/TenX/i)
  })

  test('should display main heading', async () => {
    await expect(homePage.heading).toBeVisible()
  })

  test('should have navigation menu', async () => {
    await expect(homePage.navigationMenu).toBeVisible()
  })
})

/**
 * Example visual regression test
 */
test.describe('Visual Regression', () => {
  test('homepage should match screenshot', async ({ page }) => {
    const homePage = new HomePage(page)
    await homePage.goto()
    
    // This will fail on first run and create a baseline
    // Subsequent runs will compare against the baseline
    await expect(page).toHaveScreenshot('homepage.png', {
      fullPage: true,
      maxDiffPixels: 100,
    })
  })
})

/**
 * Example API testing
 */
test.describe('API Tests', () => {
  test('should be able to fetch unit definitions', async ({ request }) => {
    const response = await request.get('/api/unit-definitions')
    
    expect(response.ok()).toBeTruthy()
    expect(response.status()).toBe(200)
    
    const data = await response.json()
    expect(data).toBeDefined()
  })
})

