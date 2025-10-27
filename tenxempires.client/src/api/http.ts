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
  } catch (err) {
    return { ok: false, status: 0 }
  }
}

