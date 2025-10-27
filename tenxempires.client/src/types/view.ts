export interface AuthStatus {
  isAuthenticated: boolean
  hasActiveGame?: boolean
  latestActiveGame?: {
    id: number
    turnNo: number
    lastTurnAt?: string | null
  }
  checkedAt: string
}

export interface LandingViewModel {
  auth: AuthStatus
  consentAccepted: boolean
}

