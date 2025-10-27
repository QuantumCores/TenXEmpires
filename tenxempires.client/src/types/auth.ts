// Authentication-related types and interfaces

export interface LoginFormModel {
  email: string
  password: string
  rememberMe?: boolean
}

export interface ForgotPasswordFormModel {
  email: string
}

export interface VerifyEmailFormModel {
  email: string
}

export interface RegisterFormModel {
  email: string
  password: string
  confirm?: string
}

// Backend ApiErrorDto structure (serialized as camelCase)
export interface ApiError {
  code: string
  message: string
}

