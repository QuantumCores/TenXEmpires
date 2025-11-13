let currentToken: string | null = null

export function setCsrfToken(token: string | null) {
  currentToken = token
}

export function getCsrfToken(): string | null {
  return currentToken
}

export function clearCsrfToken() {
  currentToken = null
}
