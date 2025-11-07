import { Page, Locator } from '@playwright/test'
import { BasePage } from './BasePage'

/**
 * Register Page Object Model
 * Represents the user registration page
 */
export class RegisterPage extends BasePage {
  // Locators
  readonly pageContainer: Locator
  readonly form: Locator
  readonly emailInput: Locator
  readonly passwordInput: Locator
  readonly confirmPasswordInput: Locator
  readonly createAccountButton: Locator
  readonly heading: Locator

  constructor(page: Page) {
    super(page)
    this.pageContainer = this.getByTestId('register-page')
    this.form = this.getByTestId('register-form')
    this.emailInput = this.getByTestId('register-email-input')
    this.passwordInput = this.getByTestId('register-password-input')
    this.confirmPasswordInput = this.getByTestId('register-confirm-password-input')
    this.createAccountButton = this.getByTestId('create-account-button')
    this.heading = page.locator('h1#register-heading')
  }

  /**
   * Navigate to register page
   */
  async goto(returnUrl?: string) {
    const url = returnUrl 
      ? `/register?returnUrl=${encodeURIComponent(returnUrl)}`
      : '/register'
    await super.goto(url)
    await this.waitForPageLoad()
  }

  /**
   * Check if register page is loaded
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
  async fillEmail(email: string): Promise<void> {
    await this.emailInput.fill(email)
  }

  /**
   * Fill password input
   */
  async fillPassword(password: string): Promise<void> {
    await this.passwordInput.fill(password)
  }

  /**
   * Fill confirm password input
   */
  async fillConfirmPassword(password: string): Promise<void> {
    await this.confirmPasswordInput.fill(password)
  }

  /**
   * Fill all registration form fields
   */
  async fillForm(email: string, password: string, confirmPassword?: string): Promise<void> {
    await this.fillEmail(email)
    await this.fillPassword(password)
    await this.fillConfirmPassword(confirmPassword ?? password)
  }

  /**
   * Click the Create Account button
   */
  async clickCreateAccount(): Promise<void> {
    await this.createAccountButton.click()
  }

  /**
   * Submit the registration form
   */
  async submitForm(email: string, password: string, confirmPassword?: string): Promise<void> {
    await this.fillForm(email, password, confirmPassword)
    await this.clickCreateAccount()
  }

  /**
   * Check if form has validation errors
   * Looks for visible error messages (excludes sr-only elements)
   */
  async hasValidationErrors(): Promise<boolean> {
    try {
      // Look for visible error messages - check for specific error IDs first
      // These are the actual error message elements, not the sr-only summary
      const emailError = this.page.locator('#email-error')
      const passwordError = this.page.locator('#password-error')
      const confirmError = this.page.locator('#confirm-error')
      // Also check for server error messages (general error div)
      const serverError = this.page.locator('[role="alert"].rounded-md.border-rose-300')
      
      // Wait for any visible error to appear
      await Promise.race([
        emailError.waitFor({ state: 'visible', timeout: 2000 }).catch(() => {}),
        passwordError.waitFor({ state: 'visible', timeout: 2000 }).catch(() => {}),
        confirmError.waitFor({ state: 'visible', timeout: 2000 }).catch(() => {}),
        serverError.waitFor({ state: 'visible', timeout: 2000 }).catch(() => {}),
      ])
      
      // Check if any are visible
      const hasEmailError = await emailError.isVisible().catch(() => false)
      const hasPasswordError = await passwordError.isVisible().catch(() => false)
      const hasConfirmError = await confirmError.isVisible().catch(() => false)
      const hasServerError = await serverError.isVisible().catch(() => false)
      
      return hasEmailError || hasPasswordError || hasConfirmError || hasServerError
    } catch {
      // Fallback: check for any visible alert elements (excluding sr-only)
      const visibleErrors = this.page.locator('[role="alert"]:not(.sr-only)')
      const count = await visibleErrors.count()
      return count > 0
    }
  }

  /**
   * Get validation error messages
   * Returns only visible error messages (excludes sr-only elements)
   */
  async getValidationErrors(): Promise<string[]> {
    const errors: string[] = []
    
    // Check for specific error message elements
    const emailError = this.page.locator('#email-error')
    const passwordError = this.page.locator('#password-error')
    const confirmError = this.page.locator('#confirm-error')
    // Also check for server error messages (general error div)
    const serverError = this.page.locator('[role="alert"].rounded-md.border-rose-300')
    
    // Wait a bit for errors to appear
    await this.page.waitForTimeout(100)
    
    // Collect visible error messages
    if (await emailError.isVisible().catch(() => false)) {
      const text = await emailError.textContent()
      if (text) errors.push(text.trim())
    }
    
    if (await passwordError.isVisible().catch(() => false)) {
      const text = await passwordError.textContent()
      if (text) errors.push(text.trim())
    }
    
    if (await confirmError.isVisible().catch(() => false)) {
      const text = await confirmError.textContent()
      if (text) errors.push(text.trim())
    }
    
    // Check for server error (duplicate email, etc.)
    if (await serverError.isVisible().catch(() => false)) {
      const text = await serverError.textContent()
      if (text) errors.push(text.trim())
    }
    
    return errors
  }

  /**
   * Check if Create Account button is disabled
   */
  async isSubmitDisabled(): Promise<boolean> {
    return await this.createAccountButton.isDisabled()
  }

  /**
   * Wait for form submission to complete
   */
  async waitForSubmission(): Promise<void> {
    // Wait for either success (modal) or error message
    await Promise.race([
      this.page.waitForSelector('[role="dialog"]', { timeout: 10000 }).catch(() => {}),
      this.page.waitForSelector('[role="alert"]', { timeout: 10000 }).catch(() => {}),
    ])
  }
}

