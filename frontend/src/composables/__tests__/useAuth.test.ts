import { describe, it, expect, vi, beforeEach } from 'vitest'
import { useAuth } from '@/composables/useAuth'
import { api } from '@/utils/api'
import { useAuthStore } from '@/stores/auth'
import { useRouter } from 'vue-router'
import type { LoginResponse, UserInfo } from '@/types/auth'

vi.mock('@/utils/api')
vi.mock('@/stores/auth')
vi.mock('vue-router')

const mockUser: UserInfo = {
  id: '1',
  email: 'test@example.com',
  firstName: 'John',
  lastName: 'Doe',
  role: 'Member'
}

const mockLoginResponse: LoginResponse = {
  token: 'mock-jwt-token',
  user: mockUser
}

describe('useAuth', () => {
  let mockAuthStore: any
  let mockRouter: any

  beforeEach(() => {
    vi.clearAllMocks()

    mockAuthStore = {
      setAuth: vi.fn(),
      clearAuth: vi.fn()
    }

    mockRouter = {
      push: vi.fn()
    }

    vi.mocked(useAuthStore).mockReturnValue(mockAuthStore)
    vi.mocked(useRouter).mockReturnValue(mockRouter)
  })

  describe('login', () => {
    it('should login successfully', async () => {
      vi.mocked(api.post).mockResolvedValue({
        data: { success: true, data: mockLoginResponse, error: null }
      })

      const { loading, error, login } = useAuth()

      const result = await login({
        email: 'test@example.com',
        password: 'password123'
      })

      expect(result).toBe(true)
      expect(loading.value).toBe(false)
      expect(error.value).toBeNull()
      expect(mockAuthStore.setAuth).toHaveBeenCalledWith(mockLoginResponse)
      expect(api.post).toHaveBeenCalledWith('/auth/login', {
        email: 'test@example.com',
        password: 'password123'
      })
    })

    it('should handle 401 Unauthorized error', async () => {
      vi.mocked(api.post).mockRejectedValue({
        response: { status: 401 }
      })

      const { loading, error, login } = useAuth()

      const result = await login({
        email: 'test@example.com',
        password: 'wrongpassword'
      })

      expect(result).toBe(false)
      expect(loading.value).toBe(false)
      expect(error.value).toBe('Invalid email or password')
      expect(mockAuthStore.setAuth).not.toHaveBeenCalled()
    })

    it('should handle network error', async () => {
      vi.mocked(api.post).mockRejectedValue(new Error('Network error'))

      const { error, login } = useAuth()

      const result = await login({
        email: 'test@example.com',
        password: 'password123'
      })

      expect(result).toBe(false)
      expect(error.value).toBe('Network error. Please try again.')
    })

    it('should handle API response error', async () => {
      vi.mocked(api.post).mockResolvedValue({
        data: {
          success: false,
          data: null,
          error: { message: 'Custom error message' }
        }
      })

      const { error, login } = useAuth()

      const result = await login({
        email: 'test@example.com',
        password: 'password123'
      })

      expect(result).toBe(false)
      expect(error.value).toBe('Custom error message')
      expect(mockAuthStore.setAuth).not.toHaveBeenCalled()
    })

    it('should clear previous errors on new login attempt', async () => {
      vi.mocked(api.post)
        .mockRejectedValueOnce({ response: { status: 401 } })
        .mockResolvedValueOnce({
          data: { success: true, data: mockLoginResponse, error: null }
        })

      const { error, login } = useAuth()

      // First attempt - should fail
      await login({ email: 'test@example.com', password: 'wrong' })
      expect(error.value).toBe('Invalid email or password')

      // Second attempt - should clear error and succeed
      const result = await login({ email: 'test@example.com', password: 'correct' })
      expect(result).toBe(true)
      expect(error.value).toBeNull()
    })

    it('should update loading state during login', async () => {
      let resolveLogin: any
      const loginPromise = new Promise((resolve) => {
        resolveLogin = resolve
      })

      vi.mocked(api.post).mockReturnValue(loginPromise as any)

      const { loading, login } = useAuth()

      expect(loading.value).toBe(false)

      const loginCall = login({ email: 'test@example.com', password: 'password123' })

      // Should be loading during API call
      expect(loading.value).toBe(true)

      resolveLogin({
        data: { success: true, data: mockLoginResponse, error: null }
      })

      await loginCall

      // Should not be loading after completion
      expect(loading.value).toBe(false)
    })
  })

  describe('register', () => {
    it('should register successfully', async () => {
      vi.mocked(api.post).mockResolvedValue({
        data: { success: true, data: mockUser, error: null }
      })

      const { loading, error, register } = useAuth()

      const result = await register({
        email: 'test@example.com',
        password: 'Password123',
        firstName: 'John',
        lastName: 'Doe',
        phone: null
      })

      expect(result).toEqual(mockUser)
      expect(loading.value).toBe(false)
      expect(error.value).toBeNull()
      expect(api.post).toHaveBeenCalledWith('/auth/register', {
        email: 'test@example.com',
        password: 'Password123',
        firstName: 'John',
        lastName: 'Doe',
        phone: null
      })
    })

    it('should handle 400 Bad Request (email exists)', async () => {
      vi.mocked(api.post).mockRejectedValue({
        response: {
          status: 400,
          data: {
            error: { message: 'Email already exists' }
          }
        }
      })

      const { error, register } = useAuth()

      const result = await register({
        email: 'existing@example.com',
        password: 'Password123',
        firstName: 'John',
        lastName: 'Doe',
        phone: null
      })

      expect(result).toBeNull()
      expect(error.value).toBe('Email already exists')
    })

    it('should handle 400 Bad Request without message', async () => {
      vi.mocked(api.post).mockRejectedValue({
        response: {
          status: 400,
          data: {}
        }
      })

      const { error, register } = useAuth()

      const result = await register({
        email: 'test@example.com',
        password: 'Password123',
        firstName: 'John',
        lastName: 'Doe',
        phone: null
      })

      expect(result).toBeNull()
      expect(error.value).toBe('Email already registered')
    })

    it('should handle network error', async () => {
      vi.mocked(api.post).mockRejectedValue(new Error('Network error'))

      const { error, register } = useAuth()

      const result = await register({
        email: 'test@example.com',
        password: 'Password123',
        firstName: 'John',
        lastName: 'Doe',
        phone: null
      })

      expect(result).toBeNull()
      expect(error.value).toBe('Network error. Please try again.')
    })

    it('should handle API response error', async () => {
      vi.mocked(api.post).mockResolvedValue({
        data: {
          success: false,
          data: null,
          error: { message: 'Validation failed' }
        }
      })

      const { error, register } = useAuth()

      const result = await register({
        email: 'invalid',
        password: 'short',
        firstName: '',
        lastName: '',
        phone: null
      })

      expect(result).toBeNull()
      expect(error.value).toBe('Validation failed')
    })

    it('should clear previous errors on new registration attempt', async () => {
      vi.mocked(api.post)
        .mockRejectedValueOnce({
          response: {
            status: 400,
            data: { error: { message: 'Email already exists' } }
          }
        })
        .mockResolvedValueOnce({
          data: { success: true, data: mockUser, error: null }
        })

      const { error, register } = useAuth()

      // First attempt - should fail
      await register({
        email: 'existing@example.com',
        password: 'Password123',
        firstName: 'John',
        lastName: 'Doe',
        phone: null
      })
      expect(error.value).toBe('Email already exists')

      // Second attempt - should clear error and succeed
      const result = await register({
        email: 'new@example.com',
        password: 'Password123',
        firstName: 'John',
        lastName: 'Doe',
        phone: null
      })
      expect(result).toEqual(mockUser)
      expect(error.value).toBeNull()
    })

    it('should update loading state during registration', async () => {
      let resolveRegister: any
      const registerPromise = new Promise((resolve) => {
        resolveRegister = resolve
      })

      vi.mocked(api.post).mockReturnValue(registerPromise as any)

      const { loading, register } = useAuth()

      expect(loading.value).toBe(false)

      const registerCall = register({
        email: 'test@example.com',
        password: 'Password123',
        firstName: 'John',
        lastName: 'Doe',
        phone: null
      })

      // Should be loading during API call
      expect(loading.value).toBe(true)

      resolveRegister({
        data: { success: true, data: mockUser, error: null }
      })

      await registerCall

      // Should not be loading after completion
      expect(loading.value).toBe(false)
    })
  })

  describe('logout', () => {
    it('should clear auth and redirect to login', () => {
      const { logout } = useAuth()

      logout()

      expect(mockAuthStore.clearAuth).toHaveBeenCalled()
      expect(mockRouter.push).toHaveBeenCalledWith('/login')
    })

    it('should not have loading or error states', () => {
      const { loading, error, logout } = useAuth()

      logout()

      // Logout is synchronous, so no loading/error handling
      expect(loading.value).toBe(false)
      expect(error.value).toBeNull()
    })
  })
})
