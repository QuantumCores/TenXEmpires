/**
 * Test fixtures and mock data for E2E tests
 * Centralized location for test data to maintain consistency
 */

export const testUsers = {
  valid: {
    username: 'testuser',
    password: 'TestPassword123!',
    email: 'test@example.com',
  },
  invalid: {
    username: 'invaliduser',
    password: 'wrongpassword',
  },
}

export const registrationTestData = {
  valid: {
    email: `test_${Date.now()}@example.com`,
    password: 'TestPassword123!',
    confirmPassword: 'TestPassword123!',
  },
  invalid: {
    emailTooShort: 'test@',
    emailInvalid: 'test@test', // Passes browser validation but fails Zod's stricter validation
    passwordTooShort: 'Short1!',
    passwordNoDigit: 'NoDigitPass!',
    passwordNoUppercase: 'nouppercase123!',
    passwordNoLowercase: 'NOLOWERCASE123!',
    passwordNoSymbol: 'NoSymbol123',
    passwordsMismatch: {
      password: 'TestPassword123!',
      confirmPassword: 'DifferentPassword123!',
    },
  },
}

export const testGameData = {
  newGame: {
    name: 'Test Empire',
    mapSize: 'medium',
  },
}

export const apiEndpoints = {
  unitDefinitions: '/api/unit-definitions',
  games: '/api/games',
  maps: '/api/maps',
  auth: '/api/auth',
}

/**
 * Helper function to generate random test data
 */
export const generateTestUsername = () => {
  return `testuser_${Date.now()}`
}

export const generateTestEmail = () => {
  return `test_${Date.now()}@example.com`
}

