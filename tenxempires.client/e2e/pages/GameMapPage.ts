import { Locator, Page } from '@playwright/test'
import { BasePage } from './BasePage'

/**
 * Page object for both the "/game/current" shell and the in-game map view.
 */
export class GameMapPage extends BasePage {
  readonly newGameShell: Locator
  readonly startNewGameStatus: Locator
  readonly loadingState: Locator
  readonly missingState: Locator
  readonly pageContainer: Locator
  readonly mapCanvas: Locator
  readonly endTurnButton: Locator
  readonly turnCounter: Locator

  constructor(page: Page) {
    super(page)
    this.newGameShell = this.getByTestId('new-game-shell')
    this.startNewGameStatus = this.getByTestId('start-new-game-status')
    this.loadingState = this.getByTestId('game-loading')
    this.missingState = this.getByTestId('game-missing')
    this.pageContainer = this.getByTestId('game-map-page')
    this.mapCanvas = this.getByTestId('game-map-canvas')
    this.endTurnButton = this.getByTestId('end-turn-button')
    this.turnCounter = this.getByTestId('turn-counter')
  }

  /**
   * Navigate directly to /game/current which triggers guard routing.
   */
  async gotoCurrentGame() {
    await this.goto('/game/current')
  }

  /**
   * Navigate to specific /game/:id route.
   */
  async gotoGameById(id: number | 'new' | string) {
    await this.goto(`/game/${id}`)
  }

  /**
   * Wait for the new game shell to acknowledge load state.
   */
  async waitForNewGameShell() {
    await this.newGameShell.waitFor({ state: 'visible' })
    await this.startNewGameStatus.waitFor({ state: 'visible' })
  }

  /**
   * Wait for the in-game map UI to be visible.
   */
  async waitForMapLoaded() {
    await this.pageContainer.waitFor({ state: 'visible' })
    await this.mapCanvas.waitFor({ state: 'visible' })
  }

  /**
   * Determine current turn number displayed on the end turn button.
   */
  async getTurnNumber(): Promise<number | null> {
    const text = (await this.turnCounter.textContent())?.trim()
    if (!text) return null
    const parsed = Number.parseInt(text, 10)
    return Number.isNaN(parsed) ? null : parsed
  }

  /**
   * Click the big circular End Turn button.
   */
  async clickEndTurn() {
    await this.endTurnButton.click()
  }

  /**
   * Check for loading state visibility (useful before assertions).
   */
  async isLoadingVisible(): Promise<boolean> {
    return await this.loadingState.isVisible().catch(() => false)
  }

  /**
   * Check for missing game fallback.
   */
  async isMissingStateVisible(): Promise<boolean> {
    return await this.missingState.isVisible().catch(() => false)
  }
}
