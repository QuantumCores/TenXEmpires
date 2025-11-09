export interface HttpResult<T> {
  ok: boolean
  status: number
  data?: T
}

const defaultInit: RequestInit = {
  credentials: 'include',
  headers: {
    'Accept': 'application/json',
  },
}

export async function getJson<T>(path: string, init?: RequestInit): Promise<HttpResult<T>> {
  try {
    const res = await fetch(path, { ...defaultInit, ...init, method: 'GET' })
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

    const res = await fetch(path, {
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
