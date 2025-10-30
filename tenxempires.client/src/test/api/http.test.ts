import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest'
import { getJson, postJson, putJson, deleteJson } from '../../api/http'

// Mock global fetch
const mockFetch = vi.fn()
global.fetch = mockFetch

describe('http - getJson', () => {
  beforeEach(() => {
    mockFetch.mockClear()
  })

  it('makes GET request with correct options', async () => {
    mockFetch.mockResolvedValueOnce({
      ok: true,
      status: 200,
      headers: new Map([['content-type', 'application/json']]),
      json: async () => ({ data: 'test' }),
    })

    await getJson('/api/test')

    expect(mockFetch).toHaveBeenCalledWith(
      '/api/test',
      expect.objectContaining({
        method: 'GET',
        credentials: 'include',
        headers: expect.objectContaining({
          Accept: 'application/json',
        }),
      })
    )
  })

  it('returns successful response with data', async () => {
    const mockData = { id: 1, name: 'test' }
    mockFetch.mockResolvedValueOnce({
      ok: true,
      status: 200,
      headers: new Map([['content-type', 'application/json']]),
      json: async () => mockData,
    })

    const result = await getJson('/api/test')

    expect(result).toEqual({
      ok: true,
      status: 200,
      data: mockData,
    })
  })

  it('returns response without data when content-type is not json', async () => {
    mockFetch.mockResolvedValueOnce({
      ok: true,
      status: 204,
      headers: new Map([['content-type', 'text/plain']]),
    })

    const result = await getJson('/api/test')

    expect(result).toEqual({
      ok: true,
      status: 204,
    })
  })

  it('handles error response with json data', async () => {
    const errorData = { code: 'ERROR', message: 'Something went wrong' }
    mockFetch.mockResolvedValueOnce({
      ok: false,
      status: 400,
      headers: new Map([['content-type', 'application/json']]),
      json: async () => errorData,
    })

    const result = await getJson('/api/test')

    expect(result).toEqual({
      ok: false,
      status: 400,
      data: errorData,
    })
  })

  it('handles network error (fetch fails)', async () => {
    mockFetch.mockRejectedValueOnce(new Error('Network error'))

    const result = await getJson('/api/test')

    expect(result).toEqual({
      ok: false,
      status: 0,
    })
  })

  it('merges custom init options', async () => {
    mockFetch.mockResolvedValueOnce({
      ok: true,
      status: 200,
      headers: new Map([['content-type', 'application/json']]),
      json: async () => ({}),
    })

    await getJson('/api/test', {
      headers: { 'X-Custom': 'value' },
      signal: new AbortController().signal,
    })

    const callArgs = mockFetch.mock.calls[0][1] as RequestInit
    expect(callArgs.method).toBe('GET')
    expect(callArgs.signal).toBeInstanceOf(AbortSignal)
    expect(callArgs.credentials).toBe('include')
    // Headers are merged from init, check they're present
    const headers = new Headers(callArgs.headers)
    expect(headers.get('x-custom')).toBe('value')
  })
})

describe('http - Cookie Parsing', () => {
  let originalCookie: string

  beforeEach(() => {
    originalCookie = document.cookie
    // Clear cookies
    document.cookie.split(';').forEach((c) => {
      const eqPos = c.indexOf('=')
      const name = eqPos > -1 ? c.substring(0, eqPos).trim() : c.trim()
      document.cookie = name + '=;expires=Thu, 01 Jan 1970 00:00:00 GMT;path=/'
    })
    mockFetch.mockClear()
  })

  afterEach(() => {
    // Restore original cookie
    document.cookie = originalCookie
  })

  it('includes CSRF token from cookie in POST request', async () => {
    // Set XSRF-TOKEN cookie
    document.cookie = 'XSRF-TOKEN=test-csrf-token-123; path=/'

    mockFetch.mockResolvedValueOnce({
      ok: true,
      status: 200,
      headers: new Map([['content-type', 'application/json']]),
      json: async () => ({}),
    })

    await postJson('/api/test', { data: 'test' })

    expect(mockFetch).toHaveBeenCalledWith(
      '/api/test',
      expect.objectContaining({
        headers: expect.objectContaining({
          'X-XSRF-TOKEN': 'test-csrf-token-123',
        }),
      })
    )
  })

  it('includes CSRF token in PUT request', async () => {
    document.cookie = 'XSRF-TOKEN=test-token; path=/'

    mockFetch.mockResolvedValueOnce({
      ok: true,
      status: 200,
      headers: new Map([['content-type', 'application/json']]),
      json: async () => ({}),
    })

    await putJson('/api/test', { data: 'test' })

    expect(mockFetch).toHaveBeenCalledWith(
      '/api/test',
      expect.objectContaining({
        headers: expect.objectContaining({
          'X-XSRF-TOKEN': 'test-token',
        }),
      })
    )
  })

  it('includes CSRF token in DELETE request', async () => {
    document.cookie = 'XSRF-TOKEN=delete-token; path=/'

    mockFetch.mockResolvedValueOnce({
      ok: true,
      status: 204,
      headers: new Map([['content-type', 'application/json']]),
      json: async () => ({}),
    })

    await deleteJson('/api/test')

    expect(mockFetch).toHaveBeenCalledWith(
      '/api/test',
      expect.objectContaining({
        headers: expect.objectContaining({
          'X-XSRF-TOKEN': 'delete-token',
        }),
      })
    )
  })

  it('handles missing CSRF token gracefully', async () => {
    // No XSRF-TOKEN cookie set

    mockFetch.mockResolvedValueOnce({
      ok: true,
      status: 200,
      headers: new Map([['content-type', 'application/json']]),
      json: async () => ({}),
    })

    await postJson('/api/test', { data: 'test' })

    // Should not include X-XSRF-TOKEN header if cookie not present
    const callArgs = mockFetch.mock.calls[0][1] as RequestInit
    const headers = callArgs.headers as Record<string, string>
    expect(headers['X-XSRF-TOKEN']).toBeUndefined()
  })

  it('handles URL-encoded CSRF token', async () => {
    // Set cookie with special characters
    const encodedToken = encodeURIComponent('token+with/special=chars')
    document.cookie = `XSRF-TOKEN=${encodedToken}; path=/`

    mockFetch.mockResolvedValueOnce({
      ok: true,
      status: 200,
      headers: new Map([['content-type', 'application/json']]),
      json: async () => ({}),
    })

    await postJson('/api/test', {})

    const callArgs = mockFetch.mock.calls[0][1] as RequestInit
    const headers = callArgs.headers as Record<string, string>
    expect(headers['X-XSRF-TOKEN']).toBe('token+with/special=chars')
  })

  it('extracts correct cookie when multiple cookies present', async () => {
    document.cookie = 'session=abc123; path=/'
    document.cookie = 'XSRF-TOKEN=correct-token; path=/'
    document.cookie = 'other=value; path=/'

    mockFetch.mockResolvedValueOnce({
      ok: true,
      status: 200,
      headers: new Map([['content-type', 'application/json']]),
      json: async () => ({}),
    })

    await postJson('/api/test', {})

    expect(mockFetch).toHaveBeenCalledWith(
      '/api/test',
      expect.objectContaining({
        headers: expect.objectContaining({
          'X-XSRF-TOKEN': 'correct-token',
        }),
      })
    )
  })
})

describe('http - postJson', () => {
  beforeEach(() => {
    mockFetch.mockClear()
  })

  it('makes POST request with JSON body', async () => {
    const requestData = { name: 'test', value: 123 }

    mockFetch.mockResolvedValueOnce({
      ok: true,
      status: 201,
      headers: new Map([['content-type', 'application/json']]),
      json: async () => ({ id: 1 }),
    })

    await postJson('/api/test', requestData)

    expect(mockFetch).toHaveBeenCalledWith(
      '/api/test',
      expect.objectContaining({
        method: 'POST',
        headers: expect.objectContaining({
          'Content-Type': 'application/json',
          Accept: 'application/json',
        }),
        body: JSON.stringify(requestData),
        credentials: 'include',
      })
    )
  })

  it('returns response data on success', async () => {
    const responseData = { id: 1, created: true }

    mockFetch.mockResolvedValueOnce({
      ok: true,
      status: 201,
      headers: new Map([['content-type', 'application/json']]),
      json: async () => responseData,
    })

    const result = await postJson('/api/test', {})

    expect(result).toEqual({
      ok: true,
      status: 201,
      data: responseData,
    })
  })

  it('handles error responses', async () => {
    const errorData = { code: 'VALIDATION_ERROR', message: 'Invalid input' }

    mockFetch.mockResolvedValueOnce({
      ok: false,
      status: 422,
      headers: new Map([['content-type', 'application/json']]),
      json: async () => errorData,
    })

    const result = await postJson('/api/test', {})

    expect(result).toEqual({
      ok: false,
      status: 422,
      data: errorData,
    })
  })

  it('handles network errors', async () => {
    mockFetch.mockRejectedValueOnce(new Error('Network error'))

    const result = await postJson('/api/test', {})

    expect(result).toEqual({
      ok: false,
      status: 0,
    })
  })

  it('handles non-JSON response', async () => {
    mockFetch.mockResolvedValueOnce({
      ok: true,
      status: 204,
      headers: new Map([['content-type', 'text/plain']]),
    })

    const result = await postJson('/api/test', {})

    expect(result).toEqual({
      ok: true,
      status: 204,
    })
  })

  it('logs error on network failure', async () => {
    const consoleErrorSpy = vi.spyOn(console, 'error').mockImplementation(() => {})
    mockFetch.mockRejectedValueOnce(new Error('Connection failed'))

    await postJson('/api/test', {})

    expect(consoleErrorSpy).toHaveBeenCalledWith(
      expect.stringContaining('[HTTP] Request failed:'),
      expect.any(Error)
    )

    consoleErrorSpy.mockRestore()
  })
})

describe('http - putJson', () => {
  beforeEach(() => {
    mockFetch.mockClear()
  })

  it('makes PUT request with JSON body', async () => {
    const requestData = { id: 1, name: 'updated' }

    mockFetch.mockResolvedValueOnce({
      ok: true,
      status: 200,
      headers: new Map([['content-type', 'application/json']]),
      json: async () => requestData,
    })

    await putJson('/api/test/1', requestData)

    expect(mockFetch).toHaveBeenCalledWith(
      '/api/test/1',
      expect.objectContaining({
        method: 'PUT',
        headers: expect.objectContaining({
          'Content-Type': 'application/json',
          Accept: 'application/json',
        }),
        body: JSON.stringify(requestData),
      })
    )
  })

  it('returns updated data', async () => {
    const updatedData = { id: 1, name: 'updated', modified: true }

    mockFetch.mockResolvedValueOnce({
      ok: true,
      status: 200,
      headers: new Map([['content-type', 'application/json']]),
      json: async () => updatedData,
    })

    const result = await putJson('/api/test/1', { name: 'updated' })

    expect(result).toEqual({
      ok: true,
      status: 200,
      data: updatedData,
    })
  })
})

describe('http - deleteJson', () => {
  beforeEach(() => {
    mockFetch.mockClear()
  })

  it('makes DELETE request without body', async () => {
    mockFetch.mockResolvedValueOnce({
      ok: true,
      status: 204,
      headers: new Map([]),
    })

    await deleteJson('/api/test/1')

    expect(mockFetch).toHaveBeenCalledWith(
      '/api/test/1',
      expect.objectContaining({
        method: 'DELETE',
        headers: expect.objectContaining({
          Accept: 'application/json',
        }),
        body: undefined,
      })
    )
  })

  it('returns success without data', async () => {
    mockFetch.mockResolvedValueOnce({
      ok: true,
      status: 204,
      headers: new Map([]),
    })

    const result = await deleteJson('/api/test/1')

    expect(result).toEqual({
      ok: true,
      status: 204,
    })
  })

  it('returns error response data if provided', async () => {
    const errorData = { code: 'NOT_FOUND', message: 'Resource not found' }

    mockFetch.mockResolvedValueOnce({
      ok: false,
      status: 404,
      headers: new Map([['content-type', 'application/json']]),
      json: async () => errorData,
    })

    const result = await deleteJson('/api/test/999')

    expect(result).toEqual({
      ok: false,
      status: 404,
      data: errorData,
    })
  })
})

describe('http - Header Handling', () => {
  beforeEach(() => {
    mockFetch.mockClear()
    document.cookie = 'XSRF-TOKEN=test-token; path=/'
  })

  it('merges custom headers with defaults in POST', async () => {
    mockFetch.mockResolvedValueOnce({
      ok: true,
      status: 200,
      headers: new Map([['content-type', 'application/json']]),
      json: async () => ({}),
    })

    await postJson(
      '/api/test',
      {},
      {
        headers: {
          'X-Custom-Header': 'custom-value',
          Authorization: 'Bearer token',
        },
      }
    )

    const callArgs = mockFetch.mock.calls[0][1] as RequestInit
    const headers = callArgs.headers as Record<string, string>
    
    // Check headers are present (note: may be normalized to lowercase by Headers API)
    expect(headers['Content-Type']).toBe('application/json')
    expect(headers['Accept']).toBe('application/json')
    expect(headers['X-XSRF-TOKEN']).toBe('test-token')
    // Custom headers from new Headers() are normalized to lowercase
    expect(headers['x-custom-header'] || headers['X-Custom-Header']).toBe('custom-value')
    expect(headers['authorization'] || headers['Authorization']).toBe('Bearer token')
  })

  it('custom headers can override defaults', async () => {
    mockFetch.mockResolvedValueOnce({
      ok: true,
      status: 200,
      headers: new Map([['content-type', 'application/json']]),
      json: async () => ({}),
    })

    await postJson(
      '/api/test',
      {},
      {
        headers: {
          Accept: 'application/xml',
        },
      }
    )

    const callArgs = mockFetch.mock.calls[0][1] as RequestInit
    const headers = callArgs.headers as Record<string, string>
    // Headers API normalizes to lowercase, so check with lowercase key
    expect(headers['accept'] || headers['Accept']).toBe('application/xml')
  })
})

describe('http - Credentials', () => {
  beforeEach(() => {
    mockFetch.mockClear()
  })

  it('always includes credentials for cross-origin requests', async () => {
    mockFetch.mockResolvedValueOnce({
      ok: true,
      status: 200,
      headers: new Map([['content-type', 'application/json']]),
      json: async () => ({}),
    })

    await getJson('/api/test')

    expect(mockFetch).toHaveBeenCalledWith(
      '/api/test',
      expect.objectContaining({
        credentials: 'include',
      })
    )
  })

  it('includes credentials in POST requests', async () => {
    mockFetch.mockResolvedValueOnce({
      ok: true,
      status: 200,
      headers: new Map([['content-type', 'application/json']]),
      json: async () => ({}),
    })

    await postJson('/api/test', {})

    expect(mockFetch).toHaveBeenCalledWith(
      '/api/test',
      expect.objectContaining({
        credentials: 'include',
      })
    )
  })
})

describe('http - Content-Type Detection', () => {
  beforeEach(() => {
    mockFetch.mockClear()
  })

  it('parses JSON when content-type includes application/json', async () => {
    mockFetch.mockResolvedValueOnce({
      ok: true,
      status: 200,
      headers: new Map([['content-type', 'application/json; charset=utf-8']]),
      json: async () => ({ data: 'test' }),
    })

    const result = await getJson('/api/test')

    expect(result.data).toEqual({ data: 'test' })
  })

  it('skips JSON parsing when content-type is not json', async () => {
    mockFetch.mockResolvedValueOnce({
      ok: true,
      status: 200,
      headers: new Map([['content-type', 'text/html']]),
    })

    const result = await getJson('/api/test')

    expect(result.data).toBeUndefined()
  })

  it('handles missing content-type header', async () => {
    mockFetch.mockResolvedValueOnce({
      ok: true,
      status: 204,
      headers: new Map([]),
    })

    const result = await getJson('/api/test')

    expect(result.data).toBeUndefined()
  })
})

