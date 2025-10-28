// Error response types aligned with TenXEmpires.Server.Domain.DataContracts

export interface ApiErrorDto {
  code: string
  message: string
}

export interface ErrorResponse {
  error?: ApiErrorDto
}

