import { describe, it, expect, beforeEach, vi } from 'vitest'
import { setActivePinia, createPinia } from 'pinia'
import { useAuthStore } from '@/stores/auth'
import type { UserInfo } from '@/types/auth'

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

    it('should save token to localStorage', () => {
      const store = useAuthStore()
      const authData = {
        user: mockUser,
        token: 'mock-jwt-token'
      }

      store.setAuth(authData)

      expect(localStorage.getItem('authToken')).toBe('mock-jwt-token')
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

    it('should remove token from localStorage', () => {
      const store = useAuthStore()

      // First set auth
      store.setAuth({
        user: mockUser,
        token: 'mock-jwt-token'
      })

      expect(localStorage.getItem('authToken')).toBe('mock-jwt-token')

      // Then clear it
      store.clearAuth()

      expect(localStorage.getItem('authToken')).toBeNull()
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
    it('should restore token from localStorage', () => {
      const store = useAuthStore()

      // Simulate token in localStorage
      localStorage.setItem('authToken', 'saved-token')

      store.restoreSession()

      expect(store.token).toBe('saved-token')
    })

    it('should not restore if no token in localStorage', () => {
      const store = useAuthStore()

      store.restoreSession()

      expect(store.token).toBeNull()
    })

    it('should mark as authenticated after restoring token', () => {
      const store = useAuthStore()

      localStorage.setItem('authToken', 'saved-token')

      expect(store.isAuthenticated).toBe(false)

      store.restoreSession()

      expect(store.isAuthenticated).toBe(true)
    })

    it('should not restore user data (only token)', () => {
      const store = useAuthStore()

      // Token in localStorage but no user data
      localStorage.setItem('authToken', 'saved-token')

      store.restoreSession()

      expect(store.token).toBe('saved-token')
      expect(store.user).toBeNull() // User data not stored in localStorage
    })
  })

  describe('isAuthenticated computed', () => {
    it('should be false when token is null', () => {
      const store = useAuthStore()

      expect(store.isAuthenticated).toBe(false)
    })

    it('should be true when token is set', () => {
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

      localStorage.setItem('authToken', 'saved-token')

      store.restoreSession()
      expect(store.token).toBe('saved-token')

      store.restoreSession() // Should not cause issues
      expect(store.token).toBe('saved-token')
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
    it('should persist token across store instances', () => {
      const store1 = useAuthStore()

      store1.setAuth({
        user: mockUser,
        token: 'persistent-token'
      })

      // Create new store instance (simulating page reload)
      setActivePinia(createPinia())
      const store2 = useAuthStore()

      // Token should not be in new instance yet
      expect(store2.token).toBeNull()

      // But should be restored from localStorage
      store2.restoreSession()
      expect(store2.token).toBe('persistent-token')
    })

    it('should not persist user data', () => {
      const store1 = useAuthStore()

      store1.setAuth({
        user: mockUser,
        token: 'token'
      })

      // Create new store instance
      setActivePinia(createPinia())
      const store2 = useAuthStore()

      store2.restoreSession()

      // Token restored but user data not persisted
      expect(store2.token).toBe('token')
      expect(store2.user).toBeNull()
    })
  })
})
