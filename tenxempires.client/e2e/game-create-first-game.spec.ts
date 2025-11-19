import { test, expect } from '@playwright/test'
import { LoginPage } from './pages/LoginPage'
import { GameMapPage } from './pages/GameMapPage'
import { StartNewGameModal } from './pages/StartNewGameModal'
import { createTestUser, loginUser, API_BASE } from './helpers/api'

/**
 * Playwright E2E coverage for TC-GAME-01.1
 * Requires backend + database fixtures running locally.
 */
test.describe('Game Creation (TC-GAME-01.1)', () => {
  let loginPage: LoginPage
  let gameMapPage: GameMapPage
  let startNewGameModal: StartNewGameModal

  test.beforeEach(async ({ page }) => {
    loginPage = new LoginPage(page)
    gameMapPage = new GameMapPage(page)
    startNewGameModal = new StartNewGameModal(page)
  })

  test('TC-GAME-01.1: Create First Game (Happy Path)', async ({ page, request }) => {
    const testUser = await createTestUser(request)
    expect(testUser.success).toBeTruthy()

    const apiLogin = await loginUser(request, testUser.email, testUser.password, true)
    expect(apiLogin.success).toBeTruthy()
    const cookieHeader = (apiLogin.cookies ?? []).join('; ')
    expect(cookieHeader).not.toEqual('')

    await loginPage.goto()
    expect(await loginPage.isLoaded()).toBeTruthy()

    await loginPage.fillCredentials(testUser.email, testUser.password)
    await loginPage.submit()

    await page.waitForURL('**/game/**', { waitUntil: 'networkidle' })
    await gameMapPage.waitForNewGameShell()
    await startNewGameModal.waitForVisible()

    await startNewGameModal.acknowledgeAndStart()

    await page.waitForURL(/\/game\/\d+$/, { waitUntil: 'networkidle' })
    const match = page.url().match(/\/game\/(\d+)$/)
    const newGameId = match ? Number(match[1]) : NaN
    expect(newGameId).toBeGreaterThan(0)
    await gameMapPage.waitForMapLoaded()

    await expect(gameMapPage.mapCanvas).toBeVisible()
    expect(await gameMapPage.getTurnNumber()).toBe(1)

    await gameMapPage.clickEndTurn()
    await expect(gameMapPage.turnCounter).toHaveText(/2|3/, { timeout: 15000 })

    const gamesResponse = await request.get(`${API_BASE}/games?status=active`, {
      headers: { Cookie: cookieHeader },
    })
    expect(gamesResponse.ok()).toBeTruthy()
    const gamesPayload = (await gamesResponse.json()) as {
      items: Array<{ id: number; status: string }>
    }
    const createdGameSummary = gamesPayload.items.find((game) => game.id === newGameId)
    expect(createdGameSummary).toBeDefined()
    expect(createdGameSummary?.status).toBe('active')

    const savesResponse = await request.get(`${API_BASE}/games/${newGameId}/saves`, {
      headers: { Cookie: cookieHeader },
    })
    expect(savesResponse.ok()).toBeTruthy()
    const savesPayload = (await savesResponse.json()) as {
      autosaves: Array<{ id: number; turnNo: number }>
    }
    expect(Array.isArray(savesPayload.autosaves)).toBeTruthy()
    expect(savesPayload.autosaves.length).toBeGreaterThan(0)
    const hasTurnOneAutosave = savesPayload.autosaves.some((save) => save.turnNo === 1)
    expect(hasTurnOneAutosave).toBeTruthy()
  })
})
