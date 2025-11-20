import { Locator, Page, expect } from '@playwright/test'
import { BasePage } from './BasePage'

/**
 * Page object for the "Start New Game" modal workflow.
 * Mirrors the data-testid attributes introduced for TC-GAME-01.1.
 */
export class StartNewGameModal extends BasePage {
  readonly modal: Locator
  readonly closeButton: Locator
  readonly errorAlert: Locator
  readonly acknowledgementCheckbox: Locator
  readonly openDeleteFlowLink: Locator
  readonly cancelDeleteButton: Locator
  readonly confirmDeleteButton: Locator
  readonly cancelButton: Locator
  readonly submitButton: Locator

  constructor(page: Page) {
    super(page)
    this.modal = this.getByTestId('start-new-game-modal')
    this.closeButton = this.getByTestId('start-new-game-close')
    this.errorAlert = this.getByTestId('start-new-game-error')
    this.acknowledgementCheckbox = this.getByTestId('start-new-game-ack')
    this.openDeleteFlowLink = this.getByTestId('start-new-game-open-delete')
    this.cancelDeleteButton = this.getByTestId('start-new-game-cancel-delete')
    this.confirmDeleteButton = this.getByTestId('start-new-game-confirm-delete')
    this.cancelButton = this.getByTestId('start-new-game-cancel')
    this.submitButton = this.getByTestId('start-new-game-submit')
  }

  /**
   * Wait until the modal becomes visible.
   */
  async waitForVisible() {
    await this.modal.waitFor({ state: 'visible' })
  }

  /**
   * Ensure acknowledgement checkbox matches requested state.
   */
  async setAcknowledged(checked: boolean) {
    const isChecked = await this.acknowledgementCheckbox.isChecked()
    if (isChecked !== checked) {
      await this.acknowledgementCheckbox.click()
    }
  }

  /**
   * Click the Start Game button (assumes acknowledgement already checked).
   */
  async submit() {
    await expect(this.submitButton).toBeEnabled()
    await this.submitButton.click()
  }

  /**
   * Convenience helper to acknowledge and start a new game.
   */
  async acknowledgeAndStart() {
    await this.setAcknowledged(true)
    await this.submit()
  }

  /**
   * Open the delete-current-game flow (only available for users with active game).
   */
  async openDeleteFlow() {
    await this.openDeleteFlowLink.click()
  }

  /**
   * Cancel out of the delete flow.
   */
  async cancelDeleteFlow() {
    await this.cancelDeleteButton.click()
  }

  /**
   * Confirm delete in the delete flow.
   */
  async confirmDeleteFlow() {
    await this.confirmDeleteButton.click()
  }

  /**
   * Close the modal via the footer cancel button.
   */
  async cancel() {
    await this.cancelButton.click()
  }

  /**
   * Close the modal via the header close button.
   */
  async dismiss() {
    await this.closeButton.click()
  }

  /**
   * Retrieve any inline error text that is currently shown.
   */
  async getErrorMessage(): Promise<string | null> {
    const isVisible = await this.errorAlert.isVisible().catch(() => false)
    if (!isVisible) return null
    const text = await this.errorAlert.textContent()
    return text?.trim() ?? null
  }
}
