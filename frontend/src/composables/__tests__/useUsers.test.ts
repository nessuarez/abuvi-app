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

  const paged = (items: User[], extra: Partial<{ page: number; pageSize: number; totalCount: number; totalPages: number }> = {}) => ({
    data: {
      success: true,
      data: { items, totalCount: items.length, page: 1, pageSize: 20, totalPages: 1, ...extra },
      error: null
    }
  })

  describe('fetchUsers', () => {
    it('should fetch users successfully', async () => {
      vi.mocked(api.get).mockResolvedValue(paged([mockUser]))

      const { users, loading, error, fetchUsers } = useUsers()

      await fetchUsers()

      expect(users.value).toEqual([mockUser])
      expect(loading.value).toBe(false)
      expect(error.value).toBeNull()
    })

    it('sends page, pageSize, search, sortBy and sortOrder as query params', async () => {
      vi.mocked(api.get).mockResolvedValue(paged([mockUser]))
      const { fetchUsers } = useUsers()

      await fetchUsers({ page: 2, pageSize: 20, search: 'ana', sortBy: 'email', sortOrder: 'asc' })

      expect(api.get).toHaveBeenCalledWith('/users', {
        params: { page: 2, pageSize: 20, search: 'ana', sortBy: 'email', sortOrder: 'asc' }
      })
    })

    it('omits empty search and null filters from params', async () => {
      vi.mocked(api.get).mockResolvedValue(paged([]))
      const { fetchUsers } = useUsers()

      await fetchUsers({ page: 1, pageSize: 20, search: '   ', role: null, isActive: null })

      expect(api.get).toHaveBeenCalledWith('/users', { params: { page: 1, pageSize: 20 } })
    })

    it('maps PagedUsersResponse into users and pagination', async () => {
      vi.mocked(api.get).mockResolvedValue(
        paged([mockUser], { page: 3, pageSize: 10, totalCount: 25, totalPages: 3 })
      )
      const { users, pagination, fetchUsers } = useUsers()

      await fetchUsers({ page: 3, pageSize: 10 })

      expect(users.value).toEqual([mockUser])
      expect(pagination.value).toEqual({ page: 3, pageSize: 10, totalCount: 25, totalPages: 3 })
    })

    it('ignores a stale response when a newer request was issued', async () => {
      const second = { ...mockUser, id: '2' }
      let resolveFirst!: (v: unknown) => void
      vi.mocked(api.get)
        .mockImplementationOnce(() => new Promise((r) => { resolveFirst = r }))
        .mockResolvedValueOnce(paged([second]))
      const { users, fetchUsers } = useUsers()

      const p1 = fetchUsers({ search: 'a' })
      const p2 = fetchUsers({ search: 'ab' })
      await p2
      resolveFirst(paged([mockUser]))
      await p1

      expect(users.value).toEqual([second])
    })

    it('should set error when fetch fails', async () => {
      vi.mocked(api.get).mockRejectedValue(new Error('Network error'))

      const { users, error, fetchUsers } = useUsers()

      await fetchUsers()

      expect(users.value).toEqual([])
      expect(error.value).toBe('Failed to load users. Please try again.')
    })

    it('refresh re-runs the last query', async () => {
      vi.mocked(api.get).mockResolvedValue(paged([mockUser]))
      const { fetchUsers, refresh } = useUsers()
      await fetchUsers({ page: 2, pageSize: 10, search: 'x' })

      await refresh()

      expect(api.get).toHaveBeenLastCalledWith('/users', {
        params: { page: 2, pageSize: 10, search: 'x' }
      })
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

      const { createUser } = useUsers()

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
      expect(api.post).toHaveBeenCalledWith('/users', request)
    })

    it('createUser refreshes current page after success', async () => {
      vi.mocked(api.get).mockResolvedValue(paged([mockUser]))
      vi.mocked(api.post).mockResolvedValue({
        data: { success: true, data: mockUser, error: null }
      })
      const { fetchUsers, createUser } = useUsers()
      await fetchUsers({ page: 2, pageSize: 10, search: 'x' })
      vi.mocked(api.get).mockClear()

      await createUser({
        email: 'a@b.com', password: 'pw', firstName: 'A', lastName: 'B', phone: null, role: 'Member'
      })

      expect(api.get).toHaveBeenCalledWith('/users', { params: { page: 2, pageSize: 10, search: 'x' } })
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

      vi.mocked(api.get).mockResolvedValue(paged([mockUser]))

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

  describe('deleteUser', () => {
    it('goes to the previous page when the last row of a page > 1 is deleted', async () => {
      vi.mocked(api.get).mockResolvedValue(
        paged([mockUser], { page: 3, pageSize: 10, totalCount: 21, totalPages: 3 })
      )
      vi.mocked(api.delete).mockResolvedValue({ data: {} })
      const { fetchUsers, deleteUser } = useUsers()
      await fetchUsers({ page: 3, pageSize: 10 })
      vi.mocked(api.get).mockClear()

      const ok = await deleteUser('1')

      expect(ok).toBe(true)
      expect(api.get).toHaveBeenCalledWith('/users', { params: { page: 2, pageSize: 10 } })
    })

    it('refreshes the current page otherwise', async () => {
      vi.mocked(api.get).mockResolvedValue(
        paged([mockUser, { ...mockUser, id: '2' }], { page: 2, pageSize: 10, totalCount: 12, totalPages: 2 })
      )
      vi.mocked(api.delete).mockResolvedValue({ data: {} })
      const { fetchUsers, deleteUser } = useUsers()
      await fetchUsers({ page: 2, pageSize: 10 })
      vi.mocked(api.get).mockClear()

      await deleteUser('1')

      expect(api.get).toHaveBeenCalledWith('/users', { params: { page: 2, pageSize: 10 } })
    })

    it('refreshes the first page when its last row is deleted', async () => {
      vi.mocked(api.get).mockResolvedValue(paged([mockUser]))
      vi.mocked(api.delete).mockResolvedValue({ data: {} })
      const { fetchUsers, deleteUser } = useUsers()
      await fetchUsers({ page: 1, pageSize: 10 })
      vi.mocked(api.get).mockClear()

      await deleteUser('1')

      expect(api.get).toHaveBeenCalledWith('/users', { params: { page: 1, pageSize: 10 } })
    })
  })
})
