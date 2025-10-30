# Unit & Component Tests

This directory contains unit and component tests using Vitest and React Testing Library.

## Structure

```
src/
├── test/
│   ├── setup.ts          # Global test setup
│   └── README.md         # This file
└── **/*.test.{ts,tsx}    # Test files colocated with source
```

## Running Tests

```bash
# Run all unit tests
npm test

# Run tests in watch mode
npm run test:watch

# Run tests with UI
npm run test:ui

# Run tests with coverage
npm run test:coverage
```

## Writing Unit Tests

### Testing a Utility Function

```typescript
import { describe, it, expect } from 'vitest'
import { myUtilFunction } from './myUtil'

describe('myUtilFunction', () => {
  it('should return expected result', () => {
    const result = myUtilFunction(input)
    expect(result).toBe(expected)
  })

  it('should handle edge cases', () => {
    expect(myUtilFunction(null)).toBeNull()
  })
})
```

### Testing a React Component

```typescript
import { describe, it, expect, vi } from 'vitest'
import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MyComponent } from './MyComponent'

describe('MyComponent', () => {
  it('renders correctly', () => {
    render(<MyComponent />)
    expect(screen.getByText('Hello')).toBeInTheDocument()
  })

  it('handles user interactions', async () => {
    const handleClick = vi.fn()
    const user = userEvent.setup()
    
    render(<MyComponent onClick={handleClick} />)
    await user.click(screen.getByRole('button'))
    
    expect(handleClick).toHaveBeenCalledTimes(1)
  })
})
```

## Mocking

### Mock a Module

```typescript
import { vi } from 'vitest'

vi.mock('./myModule', () => ({
  myFunction: vi.fn(() => 'mocked result'),
}))
```

### Mock with Factory Pattern

```typescript
vi.mock('./api', () => ({
  fetchData: vi.fn(),
}))

import { fetchData } from './api'

// In your test
fetchData.mockResolvedValue({ data: 'test' })
```

### Spy on Module

```typescript
import { vi } from 'vitest'
import * as myModule from './myModule'

const spy = vi.spyOn(myModule, 'myFunction')
spy.mockReturnValue('mocked')
```

## React Testing Library Patterns

### Query Priority
1. `getByRole` - Most accessible
2. `getByLabelText` - For form fields
3. `getByPlaceholderText` - For inputs
4. `getByText` - For non-interactive content
5. `getByTestId` - Last resort

### User Interactions

```typescript
import userEvent from '@testing-library/user-event'

const user = userEvent.setup()

// Click
await user.click(element)

// Type
await user.type(input, 'text')

// Keyboard
await user.keyboard('{Enter}')

// Hover
await user.hover(element)
```

### Async Tests

```typescript
import { waitFor } from '@testing-library/react'

// Wait for element to appear
await waitFor(() => {
  expect(screen.getByText('Loaded')).toBeInTheDocument()
})

// Or use findBy queries (built-in waitFor)
const element = await screen.findByText('Loaded')
```

## Testing Hooks

### Using renderHook

```typescript
import { renderHook } from '@testing-library/react'
import { useMyHook } from './useMyHook'

it('should update value', () => {
  const { result } = renderHook(() => useMyHook())
  
  expect(result.current.value).toBe(initial)
  
  act(() => {
    result.current.setValue(newValue)
  })
  
  expect(result.current.value).toBe(newValue)
})
```

## Testing with Context/Providers

```typescript
import { render } from '@testing-library/react'
import { MyProvider } from './MyProvider'

function renderWithProvider(ui: React.ReactElement) {
  return render(
    <MyProvider>
      {ui}
    </MyProvider>
  )
}

it('works with context', () => {
  renderWithProvider(<MyComponent />)
  // assertions
})
```

## Snapshot Testing

```typescript
it('matches snapshot', () => {
  const { container } = render(<MyComponent />)
  expect(container).toMatchSnapshot()
})

// Inline snapshot (preferred)
it('matches inline snapshot', () => {
  const result = myFunction()
  expect(result).toMatchInlineSnapshot(`"expected value"`)
})
```

## Best Practices

1. **Test behavior, not implementation**: Focus on what the component does, not how it does it
2. **Use semantic queries**: Prefer `getByRole` and `getByLabelText` for accessibility
3. **Avoid testing implementation details**: Don't test state, class names, or internal methods
4. **Keep tests simple**: One assertion per test when possible
5. **Use userEvent over fireEvent**: More realistic user interactions
6. **Clean up**: Cleanup is automatic with the setup file
7. **Descriptive test names**: Use "should" statements
8. **Arrange-Act-Assert**: Structure tests clearly
9. **Mock external dependencies**: Keep tests isolated
10. **Test edge cases**: Error states, empty states, loading states

## Coverage

Coverage reports are generated in the `coverage/` directory.

```bash
npm run test:coverage
```

View the HTML report at `coverage/index.html`.

### Coverage Thresholds

Thresholds can be configured in `vitest.config.ts`:

```typescript
coverage: {
  statements: 80,
  branches: 80,
  functions: 80,
  lines: 80,
}
```

## Debugging

### Debug in VS Code
1. Set breakpoints in your test
2. Run "Debug Test" from the testing panel

### Debug in Browser
```bash
npm run test:ui
```

### Verbose Output
```bash
npm test -- --reporter=verbose
```

## Common Issues

### Module not found
- Check import paths
- Ensure file extensions are correct
- Verify module is installed

### Component not rendering
- Check if all required props are provided
- Verify providers/context are set up
- Check console for errors

### Async timing issues
- Use `waitFor` or `findBy` queries
- Avoid arbitrary timeouts
- Check if act warnings appear

