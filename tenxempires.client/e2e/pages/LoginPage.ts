import { Locator, Page } from '@playwright/test'
import { BasePage } from './BasePage'

/**
 * Login Page Object Model
 * Provides helpers for interacting with the sign-in screen
 */
export class LoginPage extends BasePage {
  readonly pageContainer: Locator
  readonly form: Locator
  readonly heading: Locator
  readonly emailInput: Locator
  readonly passwordInput: Locator
  readonly rememberMeCheckbox: Locator
  readonly submitButton: Locator
  readonly errorAlert: Locator
  readonly retryCountdown: Locator
  readonly forgotPasswordLink: Locator
  readonly createAccountLink: Locator

  constructor(page: Page) {
    super(page)
    this.pageContainer = this.getByTestId('login-page')
    this.form = this.getByTestId('login-form')
    this.heading = this.getByTestId('login-heading')
    this.emailInput = this.getByTestId('login-email-input')
    this.passwordInput = this.getByTestId('login-password-input')
    this.rememberMeCheckbox = this.getByTestId('login-remember-me-checkbox')
    this.submitButton = this.getByTestId('login-submit-button')
    this.errorAlert = this.getByTestId('login-error')
    this.retryCountdown = this.getByTestId('login-retry-countdown')
    this.forgotPasswordLink = this.getByTestId('login-forgot-password-link')
    this.createAccountLink = this.getByTestId('login-create-account-link')
  }

  /**
   * Navigate to login page
   */
  async goto(returnUrl?: string) {
    const url = returnUrl
      ? `/login?returnUrl=${encodeURIComponent(returnUrl)}`
      : '/login'
    await super.goto(url)
    await this.waitForPageLoad()
  }

  /**
   * Check if login page is ready
   */
  async isLoaded(): Promise<boolean> {
    try {
      await this.pageContainer.waitFor({ state: 'visible', timeout: 5000 })
      await this.form.waitFor({ state: 'visible', timeout: 5000 })
      return true
    } catch {
      return false
    }
  }

  /**
   * Fill email input
   */
  async fillEmail(email: string) {
    await this.emailInput.fill(email)
  }

  /**
   * Fill password input
   */
  async fillPassword(password: string) {
    await this.passwordInput.fill(password)
  }

  /**
   * Toggle remember me checkbox
   */
  async setRememberMe(checked: boolean) {
    const isChecked = await this.rememberMeCheckbox.isChecked()
    if (isChecked !== checked) {
      await this.rememberMeCheckbox.click()
    }
  }

  /**
   * Fill form with provided credentials
   */
  async fillCredentials(email: string, password: string) {
    await this.fillEmail(email)
    await this.fillPassword(password)
  }

  /**
   * Submit the login form
   */
  async submit() {
    await this.submitButton.click()
  }

  /**
   * Perform login flow with credentials
   */
  async login(email: string, password: string, opts?: { rememberMe?: boolean }) {
    await this.fillCredentials(email, password)
    if (opts?.rememberMe !== undefined) {
      await this.setRememberMe(opts.rememberMe)
    }
    await this.submit()
  }

  /**
   * Get current error message text
   */
  async getErrorMessage(): Promise<string | null> {
    try {
      await this.errorAlert.waitFor({ state: 'visible', timeout: 5000 })
      return (await this.errorAlert.textContent())?.trim() ?? null
    } catch {
      return null
    }
  }

  /**
   * Check if retry countdown is visible
   */
  async isRetryCountdownVisible(): Promise<boolean> {
    return await this.retryCountdown.isVisible().catch(() => false)
  }

  /**
   * Get retry countdown text for assertions
   */
  async getRetryCountdownText(): Promise<string | null> {
    if (await this.isRetryCountdownVisible()) {
      const text = await this.retryCountdown.textContent()
      return text?.trim() ?? null
    }
    return null
  }

  /**
   * Determine if submit button is disabled
   */
  async isSubmitDisabled(): Promise<boolean> {
    return await this.submitButton.isDisabled()
  }
}
