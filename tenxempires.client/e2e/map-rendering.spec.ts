import { test, expect } from '@playwright/test'
import { GameMapPage } from './pages/GameMapPage'
import fs from 'fs'
import path from 'path'
import { fileURLToPath } from 'url'

// Define __dirname for ES modules
const __filename = fileURLToPath(import.meta.url)
const __dirname = path.dirname(__filename)

// Load mock data using fs instead of direct import to avoid JSON module issues in some environments
const gameStateMock = JSON.parse(fs.readFileSync(path.join(__dirname, 'fixtures/mocks/gameState.json'), 'utf-8'))
const mapTilesMock = JSON.parse(fs.readFileSync(path.join(__dirname, 'fixtures/mocks/mapTiles.json'), 'utf-8'))
const unitDefinitionsMock = JSON.parse(fs.readFileSync(path.join(__dirname, 'fixtures/mocks/unitDefinitions.json'), 'utf-8'))

const fulfillJson = (data: unknown) => ({
  status: 200,
  contentType: 'application/json',
  body: JSON.stringify(data),
})

const matchesApiPath = (targetUrl: string | URL, endpoint: string) => {
  const url = typeof targetUrl === 'string' ? targetUrl : targetUrl.toString()
  // Handle both /api/... (dev) and /v1/... (CI/prod build)
  return url.includes(`/api/${endpoint}`) || url.includes(`/v1/${endpoint}`)
}

test.describe('TC-PLAY-01: Map Rendering', () => {
  test('Map renders correctly with mocked data', async ({ page }) => {
    // 1. Mock API responses for deterministic rendering
    await page.route(
      (url) => matchesApiPath(url, 'auth/me'),
      async (route) => {
        await route.fulfill(fulfillJson({ id: 'user-1', email: 'test@example.com' }))
      }
    )

    await page.route(
      (url) => matchesApiPath(url, 'games/999/state'),
      async (route) => {
        await route.fulfill(fulfillJson(gameStateMock))
      }
    )

    await page.route(
      (url) => matchesApiPath(url, 'maps/test-map-small/tiles'),
      async (route) => {
        await route.fulfill(fulfillJson(mapTilesMock))
      }
    )

    await page.route(
      (url) => matchesApiPath(url, 'unit-definitions'),
      async (route) => {
        await route.fulfill(fulfillJson(unitDefinitionsMock))
      }
    )

    // 2. Navigate directly to the mocked game
    // We bypass the login UI because we mocked the auth check (api/auth/me)
    const gameMap = new GameMapPage(page)
    await gameMap.goto('/game/999')

    // 3. Verify map components
    await gameMap.waitForMapLoaded()
    
    // Check Canvas presence
    await expect(gameMap.mapCanvas).toBeVisible()
    
    // Check UI Overlays presence
    await expect(gameMap.actionRail).toBeVisible()
    await expect(gameMap.endTurnButton).toBeVisible()
    
    // Verify Turn 1 (from mock)
    expect(await gameMap.getTurnNumber()).toBe(1)

    // Take screenshot for visual regression
    // We wait a bit for the canvas to fully render images and any animations to settle
    await page.waitForTimeout(2000) 
    
    await expect(page).toHaveScreenshot('map-rendering-mocked.png', {
        maxDiffPixels: 400, // Allow small differences
        fullPage: true
    })
  })
})
