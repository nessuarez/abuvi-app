import { describe, it, expect, vi, beforeEach } from 'vitest'
import { useProfile } from '@/composables/useProfile'
import { api } from '@/utils/api'
import { useAuthStore } from '@/stores/auth'
import { createPinia, setActivePinia } from 'pinia'
import type { User } from '@/types/user'

vi.mock('@/utils/api')

const mockUser: User = {
  id: 'user-1',
  email: 'maria@example.com',
  firstName: 'María',
  lastName: 'García',
  phone: '+34612345678',
  role: 'Member',
  isActive: true,
  createdAt: '2024-01-01T00:00:00Z',
  updatedAt: '2024-01-01T00:00:00Z',
}

describe('useProfile', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    vi.clearAllMocks()

    // Seed the auth store with the current user
    const auth = useAuthStore()
    auth.setAuth({
      user: {
        id: 'user-1',
        email: 'maria@example.com',
        firstName: 'María',
        lastName: 'García',
        phone: null,
        role: 'Member',
        isActive: true,
      },
      token: 'fake-token',
    })
  })

  describe('loadProfile', () => {
    it('should load full user profile successfully', async () => {
      vi.mocked(api.get).mockResolvedValue({
        data: { success: true, data: mockUser, error: null },
      })

      const { fullUser, loading, error, loadProfile } = useProfile()

      await loadProfile()

      expect(fullUser.value).toEqual(mockUser)
      expect(loading.value).toBe(false)
      expect(error.value).toBeNull()
      expect(api.get).toHaveBeenCalledWith('/users/user-1')
    })

    it('should set error when loadProfile API call fails', async () => {
      vi.mocked(api.get).mockRejectedValue(new Error('Network error'))

      const { fullUser, error, loadProfile } = useProfile()

      await loadProfile()

      expect(fullUser.value).toBeNull()
      expect(error.value).toBe('Error al cargar el perfil')
    })

    it('should clear error before each loadProfile call', async () => {
      vi.mocked(api.get)
        .mockRejectedValueOnce(new Error('fail'))
        .mockResolvedValueOnce({ data: { success: true, data: mockUser, error: null } })

      const { error, loadProfile } = useProfile()
      await loadProfile()
      expect(error.value).not.toBeNull()

      await loadProfile()
      expect(error.value).toBeNull()
    })
  })

  describe('updateProfile', () => {
    it('should update profile successfully and return true', async () => {
      const updatedUser = { ...mockUser, firstName: 'Ana', lastName: 'López', phone: null }
      vi.mocked(api.put).mockResolvedValue({
        data: { success: true, data: updatedUser, error: null },
      })

      const { updateProfile } = useProfile()

      const result = await updateProfile({ firstName: 'Ana', lastName: 'López', phone: null })

      expect(result).toBe(true)
      expect(api.put).toHaveBeenCalledWith('/users/user-1', {
        firstName: 'Ana',
        lastName: 'López',
        phone: null,
        isActive: true,
      })
    })

    it('should sync auth store after successful update', async () => {
      const updatedUser = { ...mockUser, firstName: 'Ana', lastName: 'López', phone: '+34611111111' }
      vi.mocked(api.put).mockResolvedValue({
        data: { success: true, data: updatedUser, error: null },
      })

      const { updateProfile } = useProfile()
      const auth = useAuthStore()

      await updateProfile({ firstName: 'Ana', lastName: 'López', phone: '+34611111111' })

      expect(auth.user?.firstName).toBe('Ana')
      expect(auth.user?.lastName).toBe('López')
      expect(auth.user?.phone).toBe('+34611111111')
    })

    it('should update fullUser ref after successful update', async () => {
      const updatedUser = { ...mockUser, firstName: 'Ana' }
      vi.mocked(api.put).mockResolvedValue({
        data: { success: true, data: updatedUser, error: null },
      })

      const { fullUser, updateProfile } = useProfile()

      await updateProfile({ firstName: 'Ana', lastName: 'García', phone: null })

      expect(fullUser.value?.firstName).toBe('Ana')
    })

    it('should set error and return false when updateProfile API call fails', async () => {
      vi.mocked(api.put).mockRejectedValue({
        response: { data: { error: { message: 'Error del servidor' } } },
      })

      const { error, updateProfile } = useProfile()

      const result = await updateProfile({ firstName: 'Ana', lastName: 'López', phone: null })

      expect(result).toBe(false)
      expect(error.value).toBe('Error del servidor')
    })

    it('should use fallback error message when API error has no message', async () => {
      vi.mocked(api.put).mockRejectedValue(new Error('Network error'))

      const { error, updateProfile } = useProfile()

      await updateProfile({ firstName: 'Ana', lastName: 'López', phone: null })

      expect(error.value).toBe('Error al actualizar el perfil')
    })

    it('should always send isActive: true in the update request', async () => {
      vi.mocked(api.put).mockResolvedValue({
        data: { success: true, data: mockUser, error: null },
      })

      const { updateProfile } = useProfile()

      await updateProfile({ firstName: 'Ana', lastName: 'García', phone: null })

      expect(api.put).toHaveBeenCalledWith(
        expect.any(String),
        expect.objectContaining({ isActive: true }),
      )
    })

    it('should clear error before each updateProfile call', async () => {
      vi.mocked(api.put)
        .mockRejectedValueOnce(new Error('fail'))
        .mockResolvedValueOnce({ data: { success: true, data: mockUser, error: null } })

      const { error, updateProfile } = useProfile()
      await updateProfile({ firstName: 'A', lastName: 'B', phone: null })
      expect(error.value).not.toBeNull()

      await updateProfile({ firstName: 'A', lastName: 'B', phone: null })
      expect(error.value).toBeNull()
    })
  })
})
