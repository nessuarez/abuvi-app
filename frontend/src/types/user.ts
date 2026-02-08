/**
 * User domain types matching backend DTOs
 */

export type UserRole = 'Admin' | 'Board' | 'Member'

export interface User {
  id: string
  email: string
  firstName: string
  lastName: string
  phone: string | null
  role: UserRole
  isActive: boolean
  createdAt: string
  updatedAt: string
}

export interface CreateUserRequest {
  email: string
  password: string
  firstName: string
  lastName: string
  phone: string | null
  role: UserRole
}

export interface UpdateUserRequest {
  firstName: string
  lastName: string
  phone: string | null
  isActive: boolean
}
