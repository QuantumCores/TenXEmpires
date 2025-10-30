import { Page, Locator } from '@playwright/test'
import { BasePage } from './BasePage'

/**
 * Home Page Object Model
 * Represents the home/landing page of the application
 */
export class HomePage extends BasePage {
  // Locators
  readonly heading: Locator
  readonly navigationMenu: Locator

  constructor(page: Page) {
    super(page)
    this.heading = page.locator('h1').first()
    this.navigationMenu = page.locator('nav')
  }

  /**
   * Navigate to home page
   */
  async goto() {
    await super.goto('/')
    await this.waitForPageLoad()
  }

  /**
   * Check if home page is loaded
   */
  async isLoaded(): Promise<boolean> {
    try {
      await this.heading.waitFor({ state: 'visible', timeout: 5000 })
      return true
    } catch {
      return false
    }
  }

  /**
   * Get page title
   */
  async getTitle(): Promise<string> {
    return await this.page.title()
  }

  /**
   * Navigate to a specific section
   */
  async navigateToSection(sectionName: string) {
    const link = this.navigationMenu.getByRole('link', { name: sectionName })
    await link.click()
    await this.waitForPageLoad()
  }
}

