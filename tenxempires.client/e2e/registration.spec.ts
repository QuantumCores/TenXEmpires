import { test, expect } from '@playwright/test'
import { LandingPage } from './pages/LandingPage'
import { RegisterPage } from './pages/RegisterPage'
import { registrationTestData, generateTestEmail } from './fixtures/testData'

test.describe('User Registration', () => {
  let landingPage: LandingPage
  let registerPage: RegisterPage

  test.beforeEach(async ({ page }) => {
    landingPage = new LandingPage(page)
    registerPage = new RegisterPage(page)
  })

  test('should complete registration flow', async ({ page }) => {
    // 1. User opens landing page
    await landingPage.goto()
    await expect(landingPage.registerButton).toBeVisible()

    // 2. User clicks the register button
    await landingPage.clickRegister()

    // 3. On the registration page user provides email, password, confirms passwords
    const testEmail = generateTestEmail()
    await registerPage.fillForm(
      testEmail,
      registrationTestData.valid.password,
      registrationTestData.valid.confirmPassword
    )

    // Verify form fields are filled
    await expect(registerPage.emailInput).toHaveValue(testEmail)
    await expect(registerPage.passwordInput).toHaveValue(registrationTestData.valid.password)
    await expect(registerPage.confirmPasswordInput).toHaveValue(registrationTestData.valid.confirmPassword)

    // 4. User clicks Create Account
    await registerPage.clickCreateAccount()

    // Wait for submission to complete
    await registerPage.waitForSubmission()
  })

  test('should display validation errors for invalid email', async ({ page }) => {
    await registerPage.goto()

    // Fill form with invalid email (passes browser validation but fails Zod)
    await registerPage.fillEmail(registrationTestData.invalid.emailInvalid)
    await registerPage.fillPassword(registrationTestData.valid.password)
    await registerPage.fillConfirmPassword(registrationTestData.valid.password)

    // Try to submit - this should trigger Zod validation
    await registerPage.clickCreateAccount()

    // Wait for validation errors to appear
    // Check for email error specifically
    const emailError = page.locator('#email-error')
    await emailError.waitFor({ state: 'visible', timeout: 3000 })

    // Should show validation error
    const hasErrors = await registerPage.hasValidationErrors()
    expect(hasErrors).toBeTruthy()

    // Verify the error message is about email
    const errors = await registerPage.getValidationErrors()
    expect(errors.length).toBeGreaterThan(0)
    expect(errors.some(error => error.toLowerCase().includes('email'))).toBeTruthy()
  })

  test('should display validation errors for password that does not meet requirements', async ({ page }) => {
    await registerPage.goto()

    const testEmail = generateTestEmail()

    // Test password too short
    await registerPage.fillForm(
      testEmail,
      registrationTestData.invalid.passwordTooShort,
      registrationTestData.invalid.passwordTooShort
    )

    // Submit button should be disabled or show errors
    const isDisabled = await registerPage.isSubmitDisabled()
    expect(isDisabled).toBeTruthy()
  })

  test('should display validation error when passwords do not match', async ({ page }) => {
    await registerPage.goto()

    const testEmail = generateTestEmail()

    // Fill form with mismatched passwords
    await registerPage.fillForm(
      testEmail,
      registrationTestData.invalid.passwordsMismatch.password,
      registrationTestData.invalid.passwordsMismatch.confirmPassword
    )

    // Try to submit
    await registerPage.clickCreateAccount()

    // Should show validation error
    const errors = await registerPage.getValidationErrors()
    expect(errors.length).toBeGreaterThan(0)
    expect(errors.some(error => error.toLowerCase().includes('match'))).toBeTruthy()
  })

  test('should enable submit button only when all password rules are met', async ({ page }) => {
    await registerPage.goto()

    const testEmail = generateTestEmail()

    // Initially, submit should be disabled (no password entered)
    let isDisabled = await registerPage.isSubmitDisabled()
    expect(isDisabled).toBeTruthy()

    // Fill email
    await registerPage.fillEmail(testEmail)

    // Fill password that meets all requirements
    await registerPage.fillPassword(registrationTestData.valid.password)
    await registerPage.fillConfirmPassword(registrationTestData.valid.password)

    // Submit button should be enabled
    isDisabled = await registerPage.isSubmitDisabled()
    expect(isDisabled).toBeFalsy()
  })

  test('should navigate to register page from landing page', async ({ page }) => {
    // Start on landing page
    await landingPage.goto()
    await expect(landingPage.registerButton).toBeVisible()

    // Click register button
    await landingPage.clickRegister()

    // Should be on register page
    await expect(registerPage.pageContainer).toBeVisible()
    await expect(registerPage.heading).toContainText('Create account')
    await expect(registerPage.form).toBeVisible()
  })

  test('should have all required form fields visible', async ({ page }) => {
    await registerPage.goto()

    // Verify all form elements are visible
    await expect(registerPage.emailInput).toBeVisible()
    await expect(registerPage.passwordInput).toBeVisible()
    await expect(registerPage.confirmPasswordInput).toBeVisible()
    await expect(registerPage.createAccountButton).toBeVisible()
  })
})

