import { describe, it, expect, vi, beforeEach } from 'vitest'
import { useUsers } from '@/composables/useUsers'
import { api } from '@/utils/api'
import type { User } from '@/types/user'

vi.mock('@/utils/api')

const mockUser: User = {
  id: '1',
  email: 'test@example.com',
  firstName: 'John',
  lastName: 'Doe',
  phone: '+34 123 456 789',
  role: 'Member',
  isActive: true,
  createdAt: '2026-02-08T10:00:00Z',
  updatedAt: '2026-02-08T10:00:00Z'
}

describe('useUsers', () => {
  beforeEach(() => {
    vi.clearAllMocks()
  })

  describe('fetchUsers', () => {
    it('should fetch users successfully', async () => {
      const mockUsers = [mockUser]
      vi.mocked(api.get).mockResolvedValue({
        data: { success: true, data: mockUsers, error: null }
      })

      const { users, loading, error, fetchUsers } = useUsers()

      await fetchUsers()

      expect(users.value).toEqual(mockUsers)
      expect(loading.value).toBe(false)
      expect(error.value).toBeNull()
      expect(api.get).toHaveBeenCalledWith('/users')
    })

    it('should set error when fetch fails', async () => {
      vi.mocked(api.get).mockRejectedValue(new Error('Network error'))

      const { users, error, fetchUsers } = useUsers()

      await fetchUsers()

      expect(users.value).toEqual([])
      expect(error.value).toBe('Failed to load users. Please try again.')
    })
  })

  describe('fetchUserById', () => {
    it('should fetch user by ID successfully', async () => {
      vi.mocked(api.get).mockResolvedValue({
        data: { success: true, data: mockUser, error: null }
      })

      const { selectedUser, fetchUserById } = useUsers()

      const result = await fetchUserById('1')

      expect(result).toEqual(mockUser)
      expect(selectedUser.value).toEqual(mockUser)
      expect(api.get).toHaveBeenCalledWith('/users/1')
    })

    it('should return null when user not found', async () => {
      vi.mocked(api.get).mockRejectedValue({
        response: { status: 404 }
      })

      const { error, fetchUserById } = useUsers()

      const result = await fetchUserById('999')

      expect(result).toBeNull()
      expect(error.value).toBe('User not found.')
    })
  })

  describe('createUser', () => {
    it('should create user successfully', async () => {
      vi.mocked(api.post).mockResolvedValue({
        data: { success: true, data: mockUser, error: null }
      })

      const { users, createUser } = useUsers()

      const request = {
        email: 'test@example.com',
        password: 'password123',
        firstName: 'John',
        lastName: 'Doe',
        phone: null,
        role: 'Member' as const
      }

      const result = await createUser(request)

      expect(result).toEqual(mockUser)
      expect(users.value).toContainEqual(mockUser)
      expect(api.post).toHaveBeenCalledWith('/users', request)
    })

    it('should set error on duplicate email', async () => {
      vi.mocked(api.post).mockRejectedValue({
        response: { status: 409 }
      })

      const { error, createUser } = useUsers()

      const request = {
        email: 'test@example.com',
        password: 'password123',
        firstName: 'John',
        lastName: 'Doe',
        phone: null,
        role: 'Member' as const
      }

      const result = await createUser(request)

      expect(result).toBeNull()
      expect(error.value).toBe('Email already exists. Please use a different email.')
    })
  })

  describe('updateUser', () => {
    it('should update user successfully', async () => {
      const updatedUser = { ...mockUser, firstName: 'Jane' }
      vi.mocked(api.put).mockResolvedValue({
        data: { success: true, data: updatedUser, error: null }
      })

      const { users, selectedUser, updateUser } = useUsers()
      users.value = [mockUser]

      const request = {
        firstName: 'Jane',
        lastName: 'Doe',
        phone: null,
        isActive: true
      }

      const result = await updateUser('1', request)

      expect(result).toEqual(updatedUser)
      expect(selectedUser.value).toEqual(updatedUser)
      expect(users.value[0].firstName).toBe('Jane')
      expect(api.put).toHaveBeenCalledWith('/users/1', request)
    })

    it('should return null when user not found', async () => {
      vi.mocked(api.put).mockRejectedValue({
        response: { status: 404 }
      })

      const { error, updateUser } = useUsers()

      const request = {
        firstName: 'Jane',
        lastName: 'Doe',
        phone: null,
        isActive: true
      }

      const result = await updateUser('999', request)

      expect(result).toBeNull()
      expect(error.value).toBe('User not found.')
    })
  })

  describe('updateUserRole', () => {
    it('should update user role successfully', async () => {
      const updatedUser = { ...mockUser, role: 'Board' as const }
      vi.mocked(api.patch).mockResolvedValue({
        data: { success: true, data: updatedUser, error: null }
      })

      const { users, updateUserRole } = useUsers()
      users.value = [mockUser]

      const request = {
        newRole: 'Board' as const,
        reason: 'Promotion to board member'
      }

      const result = await updateUserRole('1', request)

      expect(result).toEqual(updatedUser)
      expect(users.value[0].role).toBe('Board')
      expect(api.patch).toHaveBeenCalledWith('/users/1/role', request)
    })

    it('should handle 400 error for self-role change', async () => {
      vi.mocked(api.patch).mockRejectedValue({
        response: {
          status: 400,
          data: {
            error: { message: 'Users cannot change their own role' }
          }
        }
      })

      const { error, updateUserRole } = useUsers()

      const request = {
        newRole: 'Admin' as const,
        reason: null
      }

      const result = await updateUserRole('1', request)

      expect(result).toBeNull()
      expect(error.value).toBe('Users cannot change their own role')
    })

    it('should handle 403 error for insufficient privileges', async () => {
      vi.mocked(api.patch).mockRejectedValue({
        response: { status: 403 }
      })

      const { error, updateUserRole } = useUsers()

      const request = {
        newRole: 'Admin' as const,
        reason: null
      }

      const result = await updateUserRole('1', request)

      expect(result).toBeNull()
      expect(error.value).toBe('Insufficient privileges to change this role')
    })

    it('should handle 404 error when user not found', async () => {
      vi.mocked(api.patch).mockRejectedValue({
        response: { status: 404 }
      })

      const { error, updateUserRole } = useUsers()

      const request = {
        newRole: 'Board' as const,
        reason: null
      }

      const result = await updateUserRole('nonexistent-user', request)

      expect(result).toBeNull()
      expect(error.value).toBe('User not found')
    })

    it('should update user in list after successful role change', async () => {
      const updatedUser = { ...mockUser, role: 'Board' as const }

      vi.mocked(api.get).mockResolvedValue({
        data: { success: true, data: [mockUser], error: null }
      })

      vi.mocked(api.patch).mockResolvedValue({
        data: { success: true, data: updatedUser, error: null }
      })

      const { users, fetchUsers, updateUserRole } = useUsers()
      await fetchUsers()

      const request = {
        newRole: 'Board' as const,
        reason: null
      }

      await updateUserRole('1', request)

      expect(users.value[0].role).toBe('Board')
    })

    it('should update selectedUser if it matches the updated user', async () => {
      const updatedUser = { ...mockUser, role: 'Board' as const }

      vi.mocked(api.get).mockResolvedValue({
        data: { success: true, data: mockUser, error: null }
      })

      vi.mocked(api.patch).mockResolvedValue({
        data: { success: true, data: updatedUser, error: null }
      })

      const { selectedUser, fetchUserById, updateUserRole } = useUsers()
      await fetchUserById('1')

      const request = {
        newRole: 'Board' as const,
        reason: 'Promotion'
      }

      await updateUserRole('1', request)

      expect(selectedUser.value?.role).toBe('Board')
    })
  })
})
