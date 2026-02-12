// User type matching backend UserResponse DTO
export interface User {
  id: string
  email: string
  firstName: string
  lastName: string
  phone?: string
  documentNumber?: string
  role: UserRole
  isActive: boolean
  emailVerified: boolean
  createdAt: string
  updatedAt: string
}

export type UserRole = 'Admin' | 'Board' | 'Member'

// Registration request matching backend RegisterUserRequest
export interface RegisterUserRequest {
  email: string
  password: string
  firstName: string
  lastName: string
  documentNumber?: string
  phone?: string
  acceptedTerms: boolean
}

// Verify email request
export interface VerifyEmailRequest {
  token: string
}

// Resend verification request
export interface ResendVerificationRequest {
  email: string
}

// Generic message response
export interface MessageResponse {
  message: string
}
