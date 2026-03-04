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

      const { register } = useAuth()
      await register(registerData)

      expect(api.post).toHaveBeenCalledWith('/auth/register-user', registerData)
    })

    it('should return success with email on successful registration', async () => {
      vi.mocked(api.post).mockResolvedValue({
        data: { success: true, data: {}, error: null }
      })

      const { loading, error, register } = useAuth()
      const result = await register(registerData)

      expect(result).toEqual({ success: true, email: 'test@example.com' })
      expect(loading.value).toBe(false)
      expect(error.value).toBeNull()
    })

    it('should not call setAuth after registration', async () => {
      vi.mocked(api.post).mockResolvedValue({
        data: { success: true, data: {}, error: null }
      })

      const { register } = useAuth()
      await register(registerData)

      expect(mockAuthStore.setAuth).not.toHaveBeenCalled()
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
      const result = await register(registerData)

      expect(result).toEqual({ success: false, error: 'Email already exists' })
      expect(error.value).toBe('Email already exists')
    })

    it('should handle 400 Bad Request for duplicate document', async () => {
      vi.mocked(api.post).mockRejectedValue({
        response: {
          status: 400,
          data: {
            error: { message: 'Document number already exists' }
          }
        }
      })

      const { error, register } = useAuth()
      const result = await register({ ...registerData, documentNumber: '12345' } as any)

      expect(result).toEqual({ success: false, error: 'Document number already exists' })
      expect(error.value).toBe('Document number already exists')
    })

    it('should handle 400 Bad Request without message', async () => {
      vi.mocked(api.post).mockRejectedValue({
        response: {
          status: 400,
          data: {}
        }
      })

      const { error, register } = useAuth()
      const result = await register(registerData)

      expect(result).toEqual({ success: false, error: 'Email already registered' })
      expect(error.value).toBe('Email already registered')
    })

    it('should handle network error', async () => {
      vi.mocked(api.post).mockRejectedValue(new Error('Network error'))

      const { error, register } = useAuth()
      const result = await register(registerData)

      expect(result).toEqual({ success: false, error: 'Network error. Please try again.' })
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
      const result = await register(registerData)

      expect(result).toEqual({ success: false, error: 'Validation failed' })
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
          data: { success: true, data: {}, error: null }
        })

      const { error, register } = useAuth()

      // First attempt - should fail
      await register(registerData)
      expect(error.value).toBe('Email already exists')

      // Second attempt - should clear error and succeed
      const result = await register({ ...registerData, email: 'new@example.com' })
      expect(result).toEqual({ success: true, email: 'new@example.com' })
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

      const registerCall = register(registerData)

      // Should be loading during API call
      expect(loading.value).toBe(true)

      resolveRegister({
        data: { success: true, data: {}, error: null }
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
