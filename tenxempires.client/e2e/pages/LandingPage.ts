import { Page, Locator } from '@playwright/test'
import { BasePage } from './BasePage'

/**
 * Landing Page Object Model
 * Represents the landing/home page of the application
 */
export class LandingPage extends BasePage {
  // Locators
  readonly registerButton: Locator
  readonly loginButton: Locator
  readonly playButton: Locator

  constructor(page: Page) {
    super(page)
    this.registerButton = this.getByTestId('register-button')
    this.loginButton = this.getByRole('link', { name: 'Login' })
    this.playButton = this.getByRole('link', { name: 'Play' })
  }

  /**
   * Navigate to landing page
   */
  async goto() {
    await super.goto('/')
    await this.waitForPageLoad()
  }

  /**
   * Check if landing page is loaded
   */
  async isLoaded(): Promise<boolean> {
    try {
      await this.registerButton.waitFor({ state: 'visible', timeout: 5000 })
      return true
    } catch {
      return false
    }
  }

  /**
   * Click the Register button
   */
  async clickRegister(): Promise<void> {
    await this.registerButton.click()
    await this.waitForPageLoad()
  }

  /**
   * Click the Login button
   */
  async clickLogin(): Promise<void> {
    await this.loginButton.click()
    await this.waitForPageLoad()
  }

  /**
   * Click the Play button (for authenticated users)
   */
  async clickPlay(): Promise<void> {
    await this.playButton.click()
    await this.waitForPageLoad()
  }
}

