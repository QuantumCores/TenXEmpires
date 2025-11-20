import { Locator, Page } from '@playwright/test'
import { BasePage } from './BasePage'

/**
 * Page object for the Game Map Page.
 * Mirrors the data-testid attributes for TC-PLAY-01.
 */
export class GameMapPage extends BasePage {
  readonly mapCanvas: Locator
  readonly actionRail: Locator
  readonly endTurnButton: Locator
  readonly turnCounter: Locator
  
  // New Game Shell (displayed when no game is active)
  readonly newGameShell: Locator
  
  // Action Rail Buttons
  readonly homeButton: Locator
  readonly savesButton: Locator
  readonly settingsButton: Locator
  readonly helpButton: Locator
  readonly logoutButton: Locator

  // Bottom Panel
  readonly bottomPanelUnit: Locator
  readonly bottomPanelCity: Locator
  readonly bottomPanelTile: Locator

  constructor(page: Page) {
    super(page)
    this.mapCanvas = this.getByTestId('game-map-canvas')
    this.actionRail = this.getByTestId('action-rail')
    this.endTurnButton = this.getByTestId('end-turn-button')
    this.turnCounter = this.getByTestId('turn-counter')
    this.newGameShell = this.getByTestId('new-game-shell')

    this.homeButton = this.getByTestId('action-rail-home')
    this.savesButton = this.getByTestId('action-rail-saves')
    this.settingsButton = this.getByTestId('action-rail-settings')
    this.helpButton = this.getByTestId('action-rail-help')
    this.logoutButton = this.getByTestId('action-rail-logout')

    this.bottomPanelUnit = this.getByTestId('bottom-panel-unit')
    this.bottomPanelCity = this.getByTestId('bottom-panel-city')
    this.bottomPanelTile = this.getByTestId('bottom-panel-tile')
  }

  async waitForMapLoaded(timeout = 45000) {
    await this.mapCanvas.waitFor({ state: 'visible', timeout })
    // Wait for turn counter to be visible as an indication that game state is loaded
    await this.turnCounter.waitFor({ state: 'visible', timeout })
  }
  
  async waitForNewGameShell() {
    await this.newGameShell.waitFor({ state: 'visible' })
  }

  async getTurnNumber(): Promise<number> {
    const text = await this.turnCounter.textContent()
    return parseInt(text || '0', 10)
  }
  
  async endTurn() {
      await this.endTurnButton.click()
  }
  
  // Alias for backward compatibility if needed, or preferred method name
  async clickEndTurn() {
      await this.endTurn()
  }
}
