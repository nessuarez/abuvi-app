/**
 * Auth-related TypeScript interfaces matching backend DTOs
 */

export interface LoginRequest {
  email: string
  password: string
}

export interface RegisterRequest {
  email: string
  password: string
  firstName: string
  lastName: string
  phone: string | null
}

export interface LoginResponse {
  token: string
  user: UserInfo
}

export interface UserInfo {
  id: string
  email: string
  firstName: string
  lastName: string
  role: string // 'Admin' | 'Board' | 'Member'
}
