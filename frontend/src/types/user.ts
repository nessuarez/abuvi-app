/**
 * User domain types matching backend DTOs
 */

import type { PagedResult } from '@/types/api'

export type UserRole = 'Admin' | 'Board' | 'Member'

export interface User {
  id: string
  email: string
  firstName: string
  lastName: string
  phone: string | null
  role: UserRole
  isActive: boolean
  emailVerified: boolean
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

/**
 * Request to update a user's role (Admin/Board only)
 */
export interface UpdateUserRoleRequest {
  newRole: UserRole
  reason?: string | null
}

/** Sort fields accepted by GET /api/users (backend allowlist) */
export type UserSortField = 'email' | 'firstName' | 'lastName' | 'createdAt' | 'role'

/** Query parameters for GET /api/users */
export interface UserListParams {
  page?: number // 1-based, default 1
  pageSize?: number // default 20, backend clamps to 100
  search?: string
  role?: UserRole | null
  isActive?: boolean | null
  sortBy?: UserSortField
  sortOrder?: 'asc' | 'desc'
}

/** Paged response of GET /api/users (backend omits hasNextPage/hasPreviousPage) */
export type PagedUsersResponse = Omit<PagedResult<User>, 'hasNextPage' | 'hasPreviousPage'>
