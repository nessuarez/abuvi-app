/**
 * Auth-related TypeScript interfaces matching backend DTOs
 */

export interface LoginRequest {
  email: string
  password: string
  rememberMe?: boolean
}

export interface RegisterRequest {
  firstName: string
  lastName: string
  email: string
  password: string
  confirmPassword: string
  acceptTerms: boolean
  phone?: string | null
}

export interface LoginResponse {
  token: string
  user: UserInfo
}

export interface AuthResponse {
  user: UserInfo
  token: string
}

export type UserRole = 'Admin' | 'Board' | 'Member'

export interface UserInfo {
  id: string
  email: string
  firstName: string
  lastName: string
  role: UserRole
  isActive: boolean
}
