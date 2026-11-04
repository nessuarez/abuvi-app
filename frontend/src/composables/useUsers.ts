import { ref } from 'vue'
import { api } from '@/utils/api'
import type {
  User,
  CreateUserRequest,
  UpdateUserRequest,
  UpdateUserRoleRequest,
  UserListParams,
  PagedUsersResponse
} from '@/types/user'
import type { ApiResponse } from '@/types/api'

export function useUsers() {
  const users = ref<User[]>([])
  const selectedUser = ref<User | null>(null)
  const loading = ref(false)
  const error = ref<string | null>(null)
  const pagination = ref({ page: 1, pageSize: 20, totalCount: 0, totalPages: 0 })
  const lastParams = ref<UserListParams>({ page: 1, pageSize: 20 })
  let requestSeq = 0

  /**
   * Fetch one page of users. Params are merged into the last used query so that
   * callers can change a single aspect (e.g. only the page).
   */
  const fetchUsers = async (params: UserListParams = {}): Promise<void> => {
    const merged: UserListParams = { ...lastParams.value, ...params }
    lastParams.value = merged

    const query: Record<string, string | number | boolean> = {}
    if (merged.page !== undefined) query.page = merged.page
    if (merged.pageSize !== undefined) query.pageSize = merged.pageSize
    const search = merged.search?.trim()
    if (search) query.search = search
    if (merged.role != null) query.role = merged.role
    if (merged.isActive != null) query.isActive = merged.isActive
    if (merged.sortBy) query.sortBy = merged.sortBy
    if (merged.sortOrder) query.sortOrder = merged.sortOrder

    const seq = ++requestSeq
    loading.value = true
    error.value = null
    try {
      const response = await api.get<ApiResponse<PagedUsersResponse>>('/users', { params: query })
      if (seq !== requestSeq) return // a newer request superseded this one
      const data = response.data.data
      users.value = data?.items ?? []
      if (data) {
        pagination.value = {
          page: data.page,
          pageSize: data.pageSize,
          totalCount: data.totalCount,
          totalPages: data.totalPages
        }
      }
    } catch (err) {
      if (seq !== requestSeq) return
      error.value = 'Failed to load users. Please try again.'
      console.error('fetchUsers error:', err)
    } finally {
      if (seq === requestSeq) loading.value = false
    }
  }

  /**
   * Re-run the last query (keeps page, search and sort)
   */
  const refresh = (): Promise<void> => fetchUsers(lastParams.value)

  /**
   * Fetch a single user by ID
   */
  const fetchUserById = async (id: string): Promise<User | null> => {
    loading.value = true
    error.value = null
    try {
      const response = await api.get<ApiResponse<User>>(`/users/${id}`)
      if (response.data.data) {
        selectedUser.value = response.data.data
        return response.data.data
      }
      return null
    } catch (err: any) {
      if (err.response?.status === 404) {
        error.value = 'User not found.'
      } else {
        error.value = 'Failed to load user details.'
      }
      console.error('fetchUserById error:', err)
      return null
    } finally {
      loading.value = false
    }
  }

  /**
   * Create a new user
   */
  const createUser = async (request: CreateUserRequest): Promise<User | null> => {
    loading.value = true
    error.value = null
    try {
      const response = await api.post<ApiResponse<User>>('/users', request)
      if (response.data.data) {
        await refresh()
        return response.data.data
      }
      return null
    } catch (err: any) {
      if (err.response?.status === 409) {
        error.value = 'Email already exists. Please use a different email.'
      } else if (err.response?.data?.error?.details) {
        const details = err.response.data.error.details
        error.value = details.map((d: any) => d.message).join(', ')
      } else {
        error.value = 'Failed to create user. Please try again.'
      }
      console.error('createUser error:', err)
      return null
    } finally {
      loading.value = false
    }
  }

  /**
   * Update an existing user
   */
  const updateUser = async (
    id: string,
    request: UpdateUserRequest
  ): Promise<User | null> => {
    loading.value = true
    error.value = null
    try {
      const response = await api.put<ApiResponse<User>>(`/users/${id}`, request)
      if (response.data.data) {
        // Update user in list
        const index = users.value.findIndex((u) => u.id === id)
        if (index !== -1) {
          users.value[index] = response.data.data
        }
        selectedUser.value = response.data.data
        return response.data.data
      }
      return null
    } catch (err: any) {
      if (err.response?.status === 404) {
        error.value = 'User not found.'
      } else if (err.response?.data?.error?.details) {
        const details = err.response.data.error.details
        error.value = details.map((d: any) => d.message).join(', ')
      } else {
        error.value = 'Failed to update user. Please try again.'
      }
      console.error('updateUser error:', err)
      return null
    } finally {
      loading.value = false
    }
  }

  /**
   * Update a user's role (Admin/Board only)
   * Calls the backend PATCH /api/users/{id}/role endpoint
   */
  const updateUserRole = async (
    userId: string,
    request: UpdateUserRoleRequest
  ): Promise<User | null> => {
    loading.value = true
    error.value = null
    try {
      const response = await api.patch<ApiResponse<User>>(
        `/users/${userId}/role`,
        request
      )
      if (response.data.data) {
        // Update user in the list
        const index = users.value.findIndex((u) => u.id === userId)
        if (index !== -1) {
          users.value[index] = response.data.data
        }
        // Update selected user if it's the same
        if (selectedUser.value?.id === userId) {
          selectedUser.value = response.data.data
        }
        return response.data.data
      }
      return null
    } catch (err: any) {
      if (err.response?.status === 400) {
        // Self-role change attempt or invalid operation
        error.value = err.response.data?.error?.message || 'Invalid role change operation'
      } else if (err.response?.status === 403) {
        error.value = 'Insufficient privileges to change this role'
      } else if (err.response?.status === 404) {
        error.value = 'User not found'
      } else {
        error.value = 'Failed to update user role. Please try again.'
      }
      console.error('updateUserRole error:', err)
      return null
    } finally {
      loading.value = false
    }
  }

  /**
   * Toggle user active status
   */
  const toggleUserActive = async (user: User): Promise<User | null> => {
    error.value = null
    try {
      const response = await api.put<ApiResponse<User>>(`/users/${user.id}`, {
        firstName: user.firstName,
        lastName: user.lastName,
        phone: user.phone,
        isActive: !user.isActive
      })
      if (response.data.data) {
        const index = users.value.findIndex((u) => u.id === user.id)
        if (index !== -1) {
          users.value[index] = response.data.data
        }
        return response.data.data
      }
      return null
    } catch (err: any) {
      error.value = 'Failed to update user status. Please try again.'
      console.error('toggleUserActive error:', err)
      return null
    }
  }

  /**
   * Delete a user (Admin only)
   */
  const deleteUser = async (id: string): Promise<boolean> => {
    error.value = null
    try {
      await api.delete(`/users/${id}`)
      if (users.value.length === 1 && pagination.value.page > 1) {
        await fetchUsers({ page: pagination.value.page - 1 })
      } else {
        await refresh()
      }
      return true
    } catch (err: any) {
      if (err.response?.status === 404) {
        error.value = 'User not found.'
      } else {
        error.value = 'Failed to delete user. Please try again.'
      }
      console.error('deleteUser error:', err)
      return false
    }
  }

  /**
   * Clear error state
   */
  const clearError = () => {
    error.value = null
  }

  return {
    users,
    selectedUser,
    loading,
    error,
    pagination,
    fetchUsers,
    refresh,
    fetchUserById,
    createUser,
    updateUser,
    updateUserRole,
    toggleUserActive,
    deleteUser,
    clearError
  }
}
