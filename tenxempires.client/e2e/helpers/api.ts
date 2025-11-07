import { APIRequestContext } from '@playwright/test'

/**
 * API helper utilities for E2E tests
 * Provides methods to interact with the backend API directly
 */

const API_BASE_URL = process.env.API_BASE_URL || 'http://localhost:5019'
const API_VERSION = 'v1'

export interface RegisterRequest {
  email: string
  password: string
  confirm: string
}

export interface LoginRequest {
  email: string
  password: string
  rememberMe?: boolean
}

export interface ApiError {
  code: string
  message: string
}

/**
 * Get CSRF token from the API
 */
export async function getCsrfToken(request: APIRequestContext): Promise<string | null> {
  try {
    // Use absolute URL to ensure we hit the backend directly
    // Playwright's request fixture respects absolute URLs
    const response = await request.get(`${API_BASE_URL}/${API_VERSION}/auth/csrf`, {
      // Ensure we're hitting the backend, not going through frontend proxy
      ignoreHTTPSErrors: true,
    })
    
    if (!response.ok()) {
      console.error(`[API Helper] CSRF token request failed: ${response.status()} ${response.statusText()}`)
      return null
    }
    
    // Extract CSRF token from Set-Cookie header
    const setCookieHeader = response.headers()['set-cookie']
    if (!setCookieHeader) {
      console.error('[API Helper] No Set-Cookie header in CSRF response')
      return null
    }
    
    // Handle both string and array formats
    const cookieString = Array.isArray(setCookieHeader) ? setCookieHeader.join('; ') : setCookieHeader
    const xsrfMatch = cookieString.match(/XSRF-TOKEN=([^;]+)/)
    if (!xsrfMatch) {
      console.error('[API Helper] XSRF-TOKEN not found in Set-Cookie header')
      return null
    }
    return decodeURIComponent(xsrfMatch[1])
  } catch (error) {
    console.error('[API Helper] Error getting CSRF token:', error)
    return null
  }
}

/**
 * Register a new user via API
 */
export async function registerUser(
  request: APIRequestContext,
  email: string,
  password: string,
  confirm?: string
): Promise<{ success: boolean; error?: ApiError }> {
  try {
    // Get CSRF token first
    const csrfToken = await getCsrfToken(request)
    if (!csrfToken) {
      return { success: false, error: { code: 'CSRF_FAILED', message: 'Failed to get CSRF token' } }
    }

    // Backend requires Confirm field (password confirmation)
    const confirmPassword = confirm ?? password

    const url = `${API_BASE_URL}/${API_VERSION}/auth/register`
    const response = await request.post(url, {
      data: { email, password, confirm: confirmPassword },
      headers: {
        'Content-Type': 'application/json',
        'X-XSRF-TOKEN': csrfToken,
      },
    })

    if (response.ok()) {
      return { success: true }
    }

    const error = await response.json().catch(() => ({ code: 'UNKNOWN', message: 'Unknown error' }))
    console.error(`[API Helper] Registration failed: ${response.status()}`, error)
    return { success: false, error: error as ApiError }
  } catch (error) {
    console.error('[API Helper] Registration error:', error)
    return { success: false, error: { code: 'NETWORK_ERROR', message: String(error) } }
  }
}

/**
 * Login a user via API
 */
export async function loginUser(
  request: APIRequestContext,
  email: string,
  password: string,
  rememberMe = false
): Promise<{ success: boolean; cookies?: string[]; error?: ApiError }> {
  // Get CSRF token first
  const csrfToken = await getCsrfToken(request)
  if (!csrfToken) {
    return { success: false, error: { code: 'CSRF_FAILED', message: 'Failed to get CSRF token' } }
  }

  const response = await request.post(`${API_BASE_URL}/${API_VERSION}/auth/login`, {
    data: { email, password, rememberMe },
    headers: {
      'Content-Type': 'application/json',
      'X-XSRF-TOKEN': csrfToken,
    },
  })

  if (response.ok()) {
    // Extract cookies from response
    const setCookieHeaders = response.headers()['set-cookie'] || []
    return { success: true, cookies: Array.isArray(setCookieHeaders) ? setCookieHeaders : [setCookieHeaders] }
  }

  const error = await response.json().catch(() => ({ code: 'UNKNOWN', message: 'Unknown error' }))
  return { success: false, error: error as ApiError }
}

/**
 * Logout current user via API
 */
export async function logoutUser(request: APIRequestContext): Promise<{ success: boolean; error?: ApiError }> {
  // Get CSRF token first
  const csrfToken = await getCsrfToken(request)
  if (!csrfToken) {
    return { success: false, error: { code: 'CSRF_FAILED', message: 'Failed to get CSRF token' } }
  }

  const response = await request.post(`${API_BASE_URL}/${API_VERSION}/auth/logout`, {
    headers: {
      'X-XSRF-TOKEN': csrfToken,
    },
  })

  if (response.ok()) {
    return { success: true }
  }

  const error = await response.json().catch(() => ({ code: 'UNKNOWN', message: 'Unknown error' }))
  return { success: false, error: error as ApiError }
}

/**
 * Get current user info via API
 */
export async function getCurrentUser(request: APIRequestContext): Promise<{ success: boolean; user?: { id: string; email: string }; error?: ApiError }> {
  const response = await request.get(`${API_BASE_URL}/${API_VERSION}/auth/me`)

  if (response.ok()) {
    const user = await response.json()
    return { success: true, user }
  }

  const error = await response.json().catch(() => ({ code: 'UNKNOWN', message: 'Unknown error' }))
  return { success: false, error: error as ApiError }
}

/**
 * Create a test user and return credentials
 * Useful for test setup
 */
export async function createTestUser(
  request: APIRequestContext,
  email?: string,
  password = 'TestPassword123!'
): Promise<{ email: string; password: string; success: boolean; error?: ApiError }> {
  const testEmail = email || `test_${Date.now()}_${Math.random().toString(36).substring(7)}@example.com`
  
  // Backend requires confirm field, use same password for confirmation
  const result = await registerUser(request, testEmail, password, password)
  
  if (result.success) {
    return { email: testEmail, password, success: true }
  }
  
  return { email: testEmail, password, success: false, error: result.error }
}

