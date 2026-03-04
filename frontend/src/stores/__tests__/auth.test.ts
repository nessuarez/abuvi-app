import { describe, it, expect, beforeEach, vi } from 'vitest'
import { setActivePinia, createPinia } from 'pinia'
import { useAuthStore } from '@/stores/auth'
import { api } from '@/utils/api'
import type { UserInfo } from '@/types/auth'

vi.mock('@/utils/api', () => ({
  api: {
    post: vi.fn()
  }
}))

const mockUser: UserInfo = {
  id: '1',
  email: 'test@example.com',
  firstName: 'John',
  lastName: 'Doe',
  role: 'Member'
}

const mockAdminUser: UserInfo = {
  ...mockUser,
  id: '2',
  email: 'admin@example.com',
  role: 'Admin'
}

const mockBoardUser: UserInfo = {
  ...mockUser,
  id: '3',
  email: 'board@example.com',
  role: 'Board'
}

describe('Auth Store', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    // Clear localStorage before each test
    localStorage.clear()
    vi.clearAllMocks()
  })

  describe('initial state', () => {
    it('should have null user and token initially', () => {
      const store = useAuthStore()

      expect(store.user).toBeNull()
      expect(store.token).toBeNull()
    })

    it('should not be authenticated initially', () => {
      const store = useAuthStore()

      expect(store.isAuthenticated).toBe(false)
    })

    it('should not be admin initially', () => {
      const store = useAuthStore()

      expect(store.isAdmin).toBe(false)
    })

    it('should not be board initially', () => {
      const store = useAuthStore()

      expect(store.isBoard).toBe(false)
    })
  })

  describe('setAuth', () => {
    it('should set user and token', () => {
      const store = useAuthStore()
      const authData = {
        user: mockUser,
        token: 'mock-jwt-token'
      }

      store.setAuth(authData)

      expect(store.user).toEqual(mockUser)
      expect(store.token).toBe('mock-jwt-token')
      expect(store.isAuthenticated).toBe(true)
    })

    it('should save both token and user to localStorage', () => {
      const store = useAuthStore()
      const authData = {
        user: mockUser,
        token: 'mock-jwt-token'
      }

      store.setAuth(authData)

      expect(localStorage.getItem('abuvi_auth_token')).toBe('mock-jwt-token')
      expect(localStorage.getItem('abuvi_user')).toBe(JSON.stringify(mockUser))
    })

    it('should update isAuthenticated computed property', () => {
      const store = useAuthStore()

      expect(store.isAuthenticated).toBe(false)

      store.setAuth({
        user: mockUser,
        token: 'mock-jwt-token'
      })

      expect(store.isAuthenticated).toBe(true)
    })
  })

  describe('clearAuth', () => {
    it('should clear user and token', () => {
      const store = useAuthStore()

      // First set auth
      store.setAuth({
        user: mockUser,
        token: 'mock-jwt-token'
      })

      expect(store.user).not.toBeNull()
      expect(store.token).not.toBeNull()

      // Then clear it
      store.clearAuth()

      expect(store.user).toBeNull()
      expect(store.token).toBeNull()
      expect(store.isAuthenticated).toBe(false)
    })

    it('should remove both token and user from localStorage', () => {
      const store = useAuthStore()

      // First set auth
      store.setAuth({
        user: mockUser,
        token: 'mock-jwt-token'
      })

      expect(localStorage.getItem('abuvi_auth_token')).toBe('mock-jwt-token')
      expect(localStorage.getItem('abuvi_user')).toBe(JSON.stringify(mockUser))

      // Then clear it
      store.clearAuth()

      expect(localStorage.getItem('abuvi_auth_token')).toBeNull()
      expect(localStorage.getItem('abuvi_user')).toBeNull()
    })

    it('should reset role-based flags', () => {
      const store = useAuthStore()

      // Set admin user
      store.setAuth({
        user: mockAdminUser,
        token: 'mock-jwt-token'
      })

      expect(store.isAdmin).toBe(true)

      // Clear auth
      store.clearAuth()

      expect(store.isAdmin).toBe(false)
      expect(store.isBoard).toBe(false)
    })
  })

  describe('restoreSession', () => {
    it('should restore both token and user from localStorage', () => {
      const store = useAuthStore()

      // Simulate both in localStorage
      localStorage.setItem('abuvi_auth_token', 'saved-token')
      localStorage.setItem('abuvi_user', JSON.stringify(mockUser))

      store.restoreSession()

      expect(store.token).toBe('saved-token')
      expect(store.user).toEqual(mockUser)
      expect(store.isAuthenticated).toBe(true)
    })

    it('should not restore if no data in localStorage', () => {
      const store = useAuthStore()

      store.restoreSession()

      expect(store.token).toBeNull()
      expect(store.user).toBeNull()
    })

    it('should clear auth if user data is corrupted', () => {
      const store = useAuthStore()
      const consoleErrorSpy = vi.spyOn(console, 'error').mockImplementation(() => {})

      localStorage.setItem('abuvi_auth_token', 'valid-token')
      localStorage.setItem('abuvi_user', '{invalid json}')

      store.restoreSession()

      expect(store.token).toBeNull()
      expect(store.user).toBeNull()
      expect(store.isAuthenticated).toBe(false)
      expect(consoleErrorSpy).toHaveBeenCalled()

      consoleErrorSpy.mockRestore()
    })

    it('should clear auth if only token exists without user', () => {
      const store = useAuthStore()
      const consoleWarnSpy = vi.spyOn(console, 'warn').mockImplementation(() => {})

      localStorage.setItem('abuvi_auth_token', 'token-without-user')
      // No user data in localStorage

      store.restoreSession()

      expect(store.token).toBeNull()
      expect(store.user).toBeNull()
      expect(store.isAuthenticated).toBe(false)
      expect(consoleWarnSpy).toHaveBeenCalledWith('Inconsistent auth state detected, clearing storage')

      consoleWarnSpy.mockRestore()
    })

    it('should not restore if only user exists without token', () => {
      const store = useAuthStore()

      // Only user data, no token
      localStorage.setItem('abuvi_user', JSON.stringify(mockUser))

      store.restoreSession()

      expect(store.token).toBeNull()
      expect(store.user).toBeNull()
      expect(store.isAuthenticated).toBe(false)
    })
  })

  describe('isAuthenticated computed', () => {
    it('should be false when token is null', () => {
      const store = useAuthStore()

      expect(store.isAuthenticated).toBe(false)
    })

    it('should be false when user is null', () => {
      const store = useAuthStore()
      store.token = 'some-token'

      expect(store.isAuthenticated).toBe(false)
    })

    it('should be false when token is null but user exists', () => {
      const store = useAuthStore()
      store.user = mockUser

      expect(store.isAuthenticated).toBe(false)
    })

    it('should be true when both token and user are set', () => {
      const store = useAuthStore()

      store.setAuth({
        user: mockUser,
        token: 'mock-jwt-token'
      })

      expect(store.isAuthenticated).toBe(true)
    })

    it('should be false after clearAuth', () => {
      const store = useAuthStore()

      store.setAuth({
        user: mockUser,
        token: 'mock-jwt-token'
      })

      store.clearAuth()

      expect(store.isAuthenticated).toBe(false)
    })
  })

  describe('isAdmin computed', () => {
    it('should be true for Admin role', () => {
      const store = useAuthStore()

      store.setAuth({
        user: mockAdminUser,
        token: 'mock-jwt-token'
      })

      expect(store.isAdmin).toBe(true)
    })

    it('should be false for Board role', () => {
      const store = useAuthStore()

      store.setAuth({
        user: mockBoardUser,
        token: 'mock-jwt-token'
      })

      expect(store.isAdmin).toBe(false)
    })

    it('should be false for Member role', () => {
      const store = useAuthStore()

      store.setAuth({
        user: mockUser,
        token: 'mock-jwt-token'
      })

      expect(store.isAdmin).toBe(false)
    })

    it('should be false when user is null', () => {
      const store = useAuthStore()

      expect(store.isAdmin).toBe(false)
    })
  })

  describe('isBoard computed', () => {
    it('should be true for Admin role', () => {
      const store = useAuthStore()

      store.setAuth({
        user: mockAdminUser,
        token: 'mock-jwt-token'
      })

      expect(store.isBoard).toBe(true)
    })

    it('should be true for Board role', () => {
      const store = useAuthStore()

      store.setAuth({
        user: mockBoardUser,
        token: 'mock-jwt-token'
      })

      expect(store.isBoard).toBe(true)
    })

    it('should be false for Member role', () => {
      const store = useAuthStore()

      store.setAuth({
        user: mockUser,
        token: 'mock-jwt-token'
      })

      expect(store.isBoard).toBe(false)
    })

    it('should be false when user is null', () => {
      const store = useAuthStore()

      expect(store.isBoard).toBe(false)
    })
  })

  describe('edge cases', () => {
    it('should handle setting auth multiple times', () => {
      const store = useAuthStore()

      store.setAuth({
        user: mockUser,
        token: 'token-1'
      })

      expect(store.token).toBe('token-1')
      expect(store.user?.email).toBe('test@example.com')

      store.setAuth({
        user: mockAdminUser,
        token: 'token-2'
      })

      expect(store.token).toBe('token-2')
      expect(store.user?.email).toBe('admin@example.com')
      expect(store.isAdmin).toBe(true)
    })

    it('should handle clearing auth when already cleared', () => {
      const store = useAuthStore()

      store.clearAuth()
      store.clearAuth() // Should not throw

      expect(store.user).toBeNull()
      expect(store.token).toBeNull()
    })

    it('should handle restoring session multiple times', () => {
      const store = useAuthStore()

      localStorage.setItem('abuvi_auth_token', 'saved-token')
      localStorage.setItem('abuvi_user', JSON.stringify(mockUser))

      store.restoreSession()
      expect(store.token).toBe('saved-token')
      expect(store.user).toEqual(mockUser)

      store.restoreSession() // Should not cause issues
      expect(store.token).toBe('saved-token')
      expect(store.user).toEqual(mockUser)
    })

    it('should handle role case sensitivity', () => {
      const store = useAuthStore()

      store.setAuth({
        user: { ...mockUser, role: 'admin' }, // lowercase
        token: 'mock-token'
      })

      // Should not be admin (role must be exact "Admin")
      expect(store.isAdmin).toBe(false)
      expect(store.isBoard).toBe(false)
    })
  })

  describe('localStorage integration', () => {
    it('should persist both token and user across store instances', () => {
      const store1 = useAuthStore()

      store1.setAuth({
        user: mockUser,
        token: 'persistent-token'
      })

      // Create new store instance (simulating page reload)
      setActivePinia(createPinia())
      const store2 = useAuthStore()

      // Neither should be in new instance yet
      expect(store2.token).toBeNull()
      expect(store2.user).toBeNull()

      // But should be restored from localStorage
      store2.restoreSession()
      expect(store2.token).toBe('persistent-token')
      expect(store2.user).toEqual(mockUser)
      expect(store2.isAuthenticated).toBe(true)
    })
  })

  describe('register', () => {
    const registerData = {
      email: 'test@example.com',
      password: 'Password123!',
      firstName: 'John',
      lastName: 'Doe',
      acceptedTerms: true
    }

    it('should call register-user endpoint', async () => {
      vi.mocked(api.post).mockResolvedValue({
        data: { success: true, data: {}, error: null }
      })

      const store = useAuthStore()
      await store.register(registerData)

      expect(api.post).toHaveBeenCalledWith('/auth/register-user', registerData)
    })

    it('should not modify user or token state after registration', async () => {
      vi.mocked(api.post).mockResolvedValue({
        data: { success: true, data: {}, error: null }
      })

      const store = useAuthStore()
      await store.register(registerData)

      expect(store.user).toBeNull()
      expect(store.token).toBeNull()
      expect(store.isAuthenticated).toBe(false)
    })

    it('should not save to localStorage after registration', async () => {
      vi.mocked(api.post).mockResolvedValue({
        data: { success: true, data: {}, error: null }
      })

      const store = useAuthStore()
      await store.register(registerData)

      expect(localStorage.getItem('abuvi_auth_token')).toBeNull()
      expect(localStorage.getItem('abuvi_user')).toBeNull()
    })

    it('should return success with email on successful registration', async () => {
      vi.mocked(api.post).mockResolvedValue({
        data: { success: true, data: {}, error: null }
      })

      const store = useAuthStore()
      const result = await store.register(registerData)

      expect(result).toEqual({ success: true, email: 'test@example.com' })
    })

    it('should return error on failed registration', async () => {
      vi.mocked(api.post).mockResolvedValue({
        data: {
          success: false,
          data: null,
          error: { message: 'Email already exists' }
        }
      })

      const store = useAuthStore()
      const result = await store.register(registerData)

      expect(result).toEqual({ success: false, error: 'Email already exists' })
    })

    it('should handle network error during registration', async () => {
      vi.mocked(api.post).mockRejectedValue({
        response: {
          data: { error: { message: 'Este correo electrónico ya está registrado.' } }
        }
      })

      const store = useAuthStore()
      const result = await store.register(registerData)

      expect(result).toEqual({
        success: false,
        error: 'Este correo electrónico ya está registrado.'
      })
    })
  })
})
