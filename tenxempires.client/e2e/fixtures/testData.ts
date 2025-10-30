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

