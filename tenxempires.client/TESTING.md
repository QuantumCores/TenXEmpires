# Testing Guide

This project uses a comprehensive testing setup with both unit/component tests and end-to-end tests.

## Testing Stack

### Unit & Component Tests
- **Vitest**: Fast, modern test runner with native ESM and TypeScript support
- **React Testing Library**: Component testing utilities focused on testing behavior
- **@testing-library/jest-dom**: Custom matchers for DOM assertions
- **@testing-library/user-event**: Realistic user interaction simulation

### End-to-End Tests
- **Playwright**: Cross-browser E2E testing (configured with Chromium only)
- **Page Object Model**: Maintainable test structure

## Quick Start

### Running Tests

```bash
# Unit & Component Tests
npm test                    # Run all unit tests once
npm run test:watch         # Run tests in watch mode (during development)
npm run test:coverage      # Run tests with coverage report

# E2E Tests
npm run test:e2e           # Run all E2E tests
npm run test:e2e:headed    # Run E2E tests with visible browser
npm run test:e2e:ui        # Run E2E tests in interactive UI mode
npm run test:e2e:debug     # Run E2E tests in debug mode
npm run test:e2e:codegen   # Generate E2E test code interactively
npm run test:e2e:report    # View last E2E test report

# All Tests
npm run test:all           # Run both unit and E2E tests
```

## Project Structure

```
tenxempires.client/
├── src/
│   ├── test/
│   │   ├── setup.ts              # Global test setup
│   │   └── README.md             # Unit testing guide
│   ├── components/
│   │   └── **/*.test.tsx         # Component tests
│   └── **/*.test.ts              # Unit tests
├── e2e/
│   ├── pages/                    # Page Object Model classes
│   │   ├── BasePage.ts          # Base page class
│   │   └── HomePage.ts          # Example page object
│   ├── fixtures/                 # Test data and fixtures
│   │   └── testData.ts          # Centralized test data
│   ├── example.spec.ts          # Example E2E test
│   └── README.md                # E2E testing guide
├── vitest.config.ts             # Vitest configuration
├── playwright.config.ts         # Playwright configuration
└── tsconfig.test.json          # TypeScript config for tests
```

## Writing Tests

### Unit Test Example

```typescript
import { describe, it, expect } from 'vitest'
import { myFunction } from './myModule'

describe('myFunction', () => {
  it('should return expected result', () => {
    const result = myFunction(input)
    expect(result).toBe(expected)
  })
})
```

### Component Test Example

```typescript
import { describe, it, expect, vi } from 'vitest'
import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MyComponent } from './MyComponent'

describe('MyComponent', () => {
  it('renders and handles clicks', async () => {
    const handleClick = vi.fn()
    const user = userEvent.setup()
    
    render(<MyComponent onClick={handleClick} />)
    
    const button = screen.getByRole('button')
    await user.click(button)
    
    expect(handleClick).toHaveBeenCalledTimes(1)
  })
})
```

### E2E Test Example

```typescript
import { test, expect } from '@playwright/test'
import { HomePage } from './pages/HomePage'

test.describe('Home Page', () => {
  test('should load successfully', async ({ page }) => {
    const homePage = new HomePage(page)
    await homePage.goto()
    
    await expect(homePage.heading).toBeVisible()
  })
})
```

## Configuration

### Vitest Configuration (`vitest.config.ts`)

Key features:
- jsdom environment for DOM testing
- Global test setup file
- Path aliases (@/* imports)
- Coverage reporting with v8
- Excludes E2E tests from unit test runs

### Playwright Configuration (`playwright.config.ts`)

Key features:
- Chromium/Desktop Chrome only (as specified)
- Automatic dev server startup
- Trace on first retry
- Screenshot and video on failure
- HTML reporter

## Best Practices

### Unit Tests
1. **Test behavior, not implementation**: Focus on what components do, not how
2. **Use semantic queries**: Prefer `getByRole`, `getByLabelText` for accessibility
3. **Keep tests isolated**: Each test should be independent
4. **Mock external dependencies**: Keep tests fast and deterministic
5. **Follow Arrange-Act-Assert**: Clear test structure

### E2E Tests
1. **Use Page Object Model**: Encapsulate page logic in classes
2. **Use resilient locators**: Prefer role-based selectors
3. **Wait for elements properly**: Use built-in waiting mechanisms
4. **Keep tests independent**: Each test should set up its own state
5. **Use fixtures**: Centralize test data

## Debugging

### Unit Tests

**VS Code Debugging:**
1. Set breakpoints in your test file
2. Use the testing panel to debug specific tests

**Browser Debugging:**
```bash
npm run test:ui
```

### E2E Tests

**Debug Mode:**
```bash
npm run test:e2e:debug
```

**View Traces:**
```bash
npx playwright show-trace trace.zip
```

**View Reports:**
```bash
npm run test:e2e:report
```

## CI/CD Integration

Tests are configured for CI environments:

### Unit Tests
- Fast execution
- Coverage reporting
- Fail fast on errors

### E2E Tests
- Retry on failure (2 retries in CI)
- Sequential execution in CI
- Artifacts: traces, screenshots, videos
- HTML reports

### Example GitHub Actions

```yaml
- name: Install dependencies
  run: npm ci

- name: Install Playwright browsers
  run: npx playwright install chromium --with-deps

- name: Run unit tests
  run: npm test

- name: Run E2E tests
  run: npm run test:e2e

- name: Upload test results
  if: always()
  uses: actions/upload-artifact@v3
  with:
    name: playwright-report
    path: playwright-report/
```

## Coverage

Coverage reports are generated in `coverage/` directory.

```bash
npm run test:coverage
```

View the HTML report: `coverage/index.html`

### Coverage Configuration

Configured in `vitest.config.ts`:
- Provider: v8
- Reporters: text, json, html
- Excludes: test files, config files, type definitions

## Troubleshooting

### Common Issues

**Tests not found:**
- Check file naming: `*.test.ts` or `*.spec.ts`
- Verify file is in included directories

**Module resolution errors:**
- Check import paths
- Verify tsconfig paths are correct

**Playwright browser not found:**
```bash
npx playwright install chromium
```

**Port already in use:**
- Check if dev server is already running
- Playwright will reuse existing server if available

## Resources

- [Vitest Documentation](https://vitest.dev/)
- [React Testing Library](https://testing-library.com/react)
- [Playwright Documentation](https://playwright.dev/)
- [Testing Best Practices](https://kentcdodds.com/blog/common-mistakes-with-react-testing-library)

## Getting Help

- Check the [Unit Testing Guide](src/test/README.md)
- Check the [E2E Testing Guide](e2e/README.md)
- Review example tests in the codebase
- Consult the official documentation linked above

