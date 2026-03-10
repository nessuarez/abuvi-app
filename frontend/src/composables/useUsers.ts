import { ref, type Ref } from 'vue'
import { api } from '@/utils/api'
import type { User, CreateUserRequest, UpdateUserRequest, UpdateUserRoleRequest } from '@/types/user'
import type { ApiResponse } from '@/types/api'

export function useUsers() {
  const users = ref<User[]>([])
  const selectedUser = ref<User | null>(null)
  const loading = ref(false)
  const error = ref<string | null>(null)

  /**
   * Fetch all users from the API
   */
  const fetchUsers = async (): Promise<void> => {
    loading.value = true
    error.value = null
    try {
      const response = await api.get<ApiResponse<User[]>>('/users')
      users.value = response.data.data ?? []
    } catch (err) {
      error.value = 'Failed to load users. Please try again.'
      console.error('fetchUsers error:', err)
    } finally {
      loading.value = false
    }
  }

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
        users.value.push(response.data.data)
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
      users.value = users.value.filter((u) => u.id !== id)
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
    fetchUsers,
    fetchUserById,
    createUser,
    updateUser,
    updateUserRole,
    toggleUserActive,
    deleteUser,
    clearError
  }
}
