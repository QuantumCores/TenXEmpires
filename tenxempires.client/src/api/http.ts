export interface HttpResult<T> {
  ok: boolean
  status: number
  data?: T
}

/**
 * Constructs the full API URL for a given path.
 * - If VITE_API_BASE_URL is set, uses it as the base URL and appends /v1 + path
 * - Otherwise, uses relative path (for dev mode with Vite proxy)
 */
export function getApiUrl(path: string): string {
  const apiBaseUrl = import.meta.env.VITE_API_BASE_URL
  if (apiBaseUrl) {
    // Remove leading slash from path if present, then construct full URL
    const cleanPath = path.startsWith('/') ? path.slice(1) : path
    // Replace /api prefix with /v1 if present, otherwise add /v1
    const apiPath = cleanPath.startsWith('api/') 
      ? cleanPath.replace(/^api\//, 'v1/')
      : `v1/${cleanPath}`
    return `${apiBaseUrl}/${apiPath}`
  }
  // Dev mode: use relative path (Vite proxy will handle /api -> /v1)
  return path
}

const defaultInit: RequestInit = {
  credentials: 'include',
  headers: {
    'Accept': 'application/json',
  },
}

export async function getJson<T>(path: string, init?: RequestInit): Promise<HttpResult<T>> {
  try {
    const url = getApiUrl(path)
    const res = await fetch(url, { ...defaultInit, ...init, method: 'GET' })
    const status = res.status
    if (!res.headers.get('content-type')?.includes('application/json')) {
      return { ok: res.ok, status }
    }
    const data = (await res.json()) as T
    return { ok: res.ok, status, data }
  } catch {
    return { ok: false, status: 0 }
  }
}

function readCookie(name: string): string | undefined {
  const match = document.cookie.match(new RegExp('(?:^|; )' + name.replace(/([.$?*|{}()[\]\\/+^])/g, '\\$1') + '=([^;]*)'))
  return match ? decodeURIComponent(match[1]) : undefined
}

async function sendJson<TReq, TRes>(path: string, method: 'POST'|'PUT'|'DELETE', body?: TReq, init?: RequestInit): Promise<HttpResult<TRes>> {
  try {
    const headers: Record<string, string> = {
      'Accept': 'application/json',
      'Content-Type': 'application/json',
    }

    // Merge any headers from init
    if (init?.headers) {
      const initHeaders = new Headers(init.headers)
      initHeaders.forEach((value, key) => {
        headers[key] = value
      })
    }

    const token = readCookie('XSRF-TOKEN')
    if (token) {
      // Server expects this header name per SecurityConstants.XsrfHeader
      headers['X-XSRF-TOKEN'] = token
    }

    const url = getApiUrl(path)
    const res = await fetch(url, {
      ...defaultInit,
      ...init,
      method,
      headers,
      body: body !== undefined ? JSON.stringify(body) : undefined,
    })
    const status = res.status
    const contentType = res.headers.get('content-type') || ''
    if (!contentType.includes('application/json')) {
      return { ok: res.ok, status }
    }
    const data = (await res.json()) as TRes
    return { ok: res.ok, status, data }
  } catch (err: unknown) {
    // Network error or JSON parse error
    console.error('[HTTP] Request failed:', err)
    return { ok: false, status: 0 }
  }
}

export function postJson<TReq, TRes>(path: string, body: TReq, init?: RequestInit): Promise<HttpResult<TRes>> {
  return sendJson<TReq, TRes>(path, 'POST', body, init)
}

export function putJson<TReq, TRes>(path: string, body: TReq, init?: RequestInit): Promise<HttpResult<TRes>> {
  return sendJson<TReq, TRes>(path, 'PUT', body, init)
}

export function deleteJson<TRes>(path: string, init?: RequestInit): Promise<HttpResult<TRes>> {
  return sendJson<undefined, TRes>(path, 'DELETE', undefined, init)
}
