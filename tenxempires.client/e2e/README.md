# End-to-End Tests

This directory contains E2E tests using Playwright.

## Structure

```
e2e/
├── pages/           # Page Object Model classes
│   ├── BasePage.ts  # Base class for all pages
│   └── HomePage.ts  # Example home page object
├── fixtures/        # Test data and fixtures
│   └── testData.ts  # Centralized test data
└── *.spec.ts        # Test files
```

## Page Object Model

We use the Page Object Model (POM) pattern to:
- Encapsulate page-specific logic
- Improve test maintainability
- Reduce code duplication
- Make tests more readable

### Creating a New Page Object

```typescript
import { Page, Locator } from '@playwright/test'
import { BasePage } from './BasePage'

export class MyPage extends BasePage {
  readonly myElement: Locator

  constructor(page: Page) {
    super(page)
    this.myElement = page.locator('#my-element')
  }

  async goto() {
    await super.goto('/my-page')
    await this.waitForPageLoad()
  }

  async performAction() {
    await this.myElement.click()
  }
}
```

## Running Tests

```bash
# Run all E2E tests
npm run test:e2e

# Run tests in headed mode (visible browser)
npm run test:e2e:headed

# Run tests in UI mode (interactive)
npm run test:e2e:ui

# Run tests in debug mode
npm run test:e2e:debug

# Generate test code
npm run test:e2e:codegen
```

## Writing Tests

### Basic Test

```typescript
import { test, expect } from '@playwright/test'
import { MyPage } from './pages/MyPage'

test.describe('My Feature', () => {
  let myPage: MyPage

  test.beforeEach(async ({ page }) => {
    myPage = new MyPage(page)
    await myPage.goto()
  })

  test('should do something', async () => {
    await myPage.performAction()
    await expect(myPage.myElement).toBeVisible()
  })
})
```

### Visual Regression Test

```typescript
test('should match screenshot', async ({ page }) => {
  await page.goto('/')
  await expect(page).toHaveScreenshot('my-page.png')
})
```

### API Test

```typescript
test('should fetch data', async ({ request }) => {
  const response = await request.get('/api/endpoint')
  expect(response.ok()).toBeTruthy()
  const data = await response.json()
  expect(data).toBeDefined()
})
```

## Best Practices

1. **Use locators over selectors**: Prefer `page.getByRole()`, `page.getByText()`, `page.getByTestId()` over CSS selectors
2. **Wait for elements**: Use `await expect(element).toBeVisible()` instead of arbitrary timeouts
3. **Keep tests independent**: Each test should be able to run in isolation
4. **Use Page Objects**: Encapsulate page logic in Page Object classes
5. **Use fixtures**: Store test data in fixtures for reusability
6. **Clean up**: Tests should clean up after themselves (handled by test hooks)
7. **Meaningful assertions**: Use specific matchers like `toBeVisible()`, `toHaveText()`, etc.

## Debugging

### View test traces
```bash
npx playwright show-trace trace.zip
```

### View HTML report
```bash
npx playwright show-report
```

## CI/CD

Tests are configured to run in CI with:
- Retry on failure (2 retries)
- Sequential execution
- HTML reporter for artifacts

