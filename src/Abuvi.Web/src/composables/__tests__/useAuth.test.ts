import { describe, it, expect, vi, beforeEach } from 'vitest'
import { useAuth } from '../useAuth'
import { api } from '@/utils/api'
import type { User } from '@/types/auth'

vi.mock('@/utils/api')

describe('useAuth', () => {
  beforeEach(() => {
    vi.clearAllMocks()
  })

  describe('registerUser', () => {
    it('should register user successfully', async () => {
      // Arrange
      const mockUser: User = {
        id: '123',
        email: 'test@example.com',
        firstName: 'John',
        lastName: 'Doe',
        role: 'Member',
        isActive: false,
        emailVerified: false,
        createdAt: '2026-02-12T00:00:00Z',
        updatedAt: '2026-02-12T00:00:00Z'
      }
      vi.mocked(api.post).mockResolvedValue({
        data: { success: true, data: mockUser, error: null }
      } as any)

      // Act
      const { registerUser, loading, error } = useAuth()
      const result = await registerUser({
        email: 'test@example.com',
        password: 'Test123!@#',
        firstName: 'John',
        lastName: 'Doe',
        acceptedTerms: true
      })

      // Assert
      expect(result).toEqual(mockUser)
      expect(loading.value).toBe(false)
      expect(error.value).toBeNull()
      expect(api.post).toHaveBeenCalledWith('/auth/register-user', expect.any(Object))
    })

    it('should handle EMAIL_EXISTS error', async () => {
      // Arrange
      vi.mocked(api.post).mockRejectedValue({
        response: {
          data: {
            success: false,
            error: { code: 'EMAIL_EXISTS', message: 'Email exists' }
          }
        }
      })

      // Act
      const { registerUser, error } = useAuth()
      const result = await registerUser({
        email: 'existing@example.com',
        password: 'Test123!@#',
        firstName: 'John',
        lastName: 'Doe',
        acceptedTerms: true
      })

      // Assert
      expect(result).toBeNull()
      expect(error.value).toBe('An account with this email already exists')
    })

    it('should handle DOCUMENT_EXISTS error', async () => {
      // Arrange
      vi.mocked(api.post).mockRejectedValue({
        response: {
          data: {
            success: false,
            error: { code: 'DOCUMENT_EXISTS', message: 'Document exists' }
          }
        }
      })

      // Act
      const { registerUser, error } = useAuth()
      const result = await registerUser({
        email: 'test@example.com',
        password: 'Test123!@#',
        firstName: 'John',
        lastName: 'Doe',
        documentNumber: '12345678A',
        acceptedTerms: true
      })

      // Assert
      expect(result).toBeNull()
      expect(error.value).toBe('An account with this document number already exists')
    })

    it('should handle validation errors', async () => {
      // Arrange
      vi.mocked(api.post).mockRejectedValue({
        response: {
          data: {
            success: false,
            error: {
              code: 'VALIDATION_ERROR',
              message: 'Validation failed',
              details: { Password: ['Password is too weak'] }
            }
          }
        }
      })

      // Act
      const { registerUser, error } = useAuth()
      const result = await registerUser({
        email: 'test@example.com',
        password: 'weak',
        firstName: 'John',
        lastName: 'Doe',
        acceptedTerms: true
      })

      // Assert
      expect(result).toBeNull()
      expect(error.value).toBe('An error occurred. Please try again.')
    })

    it('should set loading state correctly', async () => {
      // Arrange
      vi.mocked(api.post).mockImplementation(
        () =>
          new Promise((resolve) =>
            setTimeout(
              () => resolve({ data: { success: true, data: null, error: null } } as any),
              100
            )
          )
      )

      // Act
      const { registerUser, loading } = useAuth()
      const promise = registerUser({
        email: 'test@example.com',
        password: 'Test123!@#',
        firstName: 'John',
        lastName: 'Doe',
        acceptedTerms: true
      })

      // Assert - loading should be true during request
      expect(loading.value).toBe(true)

      await promise

      // Assert - loading should be false after request
      expect(loading.value).toBe(false)
    })
  })

  describe('verifyEmail', () => {
    it('should verify email successfully', async () => {
      // Arrange
      vi.mocked(api.post).mockResolvedValue({
        data: {
          success: true,
          data: { message: 'Email verified successfully' },
          error: null
        }
      } as any)

      // Act
      const { verifyEmail, loading, error } = useAuth()
      const result = await verifyEmail({ token: 'valid-token' })

      // Assert
      expect(result).toBe(true)
      expect(loading.value).toBe(false)
      expect(error.value).toBeNull()
      expect(api.post).toHaveBeenCalledWith('/auth/verify-email', { token: 'valid-token' })
    })

    it('should handle invalid token error (NOT_FOUND)', async () => {
      // Arrange
      vi.mocked(api.post).mockRejectedValue({
        response: {
          data: {
            success: false,
            error: { code: 'NOT_FOUND', message: 'User not found' }
          }
        }
      })

      // Act
      const { verifyEmail, error } = useAuth()
      const result = await verifyEmail({ token: 'invalid-token' })

      // Assert
      expect(result).toBe(false)
      expect(error.value).toBe('Invalid verification token')
    })

    it('should handle expired token error (VERIFICATION_FAILED)', async () => {
      // Arrange
      vi.mocked(api.post).mockRejectedValue({
        response: {
          data: {
            success: false,
            error: { code: 'VERIFICATION_FAILED', message: 'Token expired' }
          }
        }
      })

      // Act
      const { verifyEmail, error } = useAuth()
      const result = await verifyEmail({ token: 'expired-token' })

      // Assert
      expect(result).toBe(false)
      expect(error.value).toBe('Verification token has expired. Please request a new one.')
    })

    it('should set loading state correctly', async () => {
      // Arrange
      vi.mocked(api.post).mockImplementation(
        () =>
          new Promise((resolve) =>
            setTimeout(
              () => resolve({ data: { success: true, data: null, error: null } } as any),
              100
            )
          )
      )

      // Act
      const { verifyEmail, loading } = useAuth()
      const promise = verifyEmail({ token: 'token' })

      // Assert - loading should be true during request
      expect(loading.value).toBe(true)

      await promise

      // Assert - loading should be false after request
      expect(loading.value).toBe(false)
    })
  })

  describe('resendVerification', () => {
    it('should resend verification successfully', async () => {
      // Arrange
      vi.mocked(api.post).mockResolvedValue({
        data: {
          success: true,
          data: { message: 'Verification email sent' },
          error: null
        }
      } as any)

      // Act
      const { resendVerification, loading, error } = useAuth()
      const result = await resendVerification({ email: 'test@example.com' })

      // Assert
      expect(result).toBe(true)
      expect(loading.value).toBe(false)
      expect(error.value).toBeNull()
      expect(api.post).toHaveBeenCalledWith('/auth/resend-verification', {
        email: 'test@example.com'
      })
    })

    it('should handle email not found error', async () => {
      // Arrange
      vi.mocked(api.post).mockRejectedValue({
        response: {
          data: {
            success: false,
            error: { code: 'NOT_FOUND', message: 'User not found' }
          }
        }
      })

      // Act
      const { resendVerification, error } = useAuth()
      const result = await resendVerification({ email: 'notfound@example.com' })

      // Assert
      expect(result).toBe(false)
      expect(error.value).toBe('Invalid verification token')
    })

    it('should handle already verified error (RESEND_FAILED)', async () => {
      // Arrange
      vi.mocked(api.post).mockRejectedValue({
        response: {
          data: {
            success: false,
            error: { code: 'RESEND_FAILED', message: 'Already verified' }
          }
        }
      })

      // Act
      const { resendVerification, error } = useAuth()
      const result = await resendVerification({ email: 'verified@example.com' })

      // Assert
      expect(result).toBe(false)
      expect(error.value).toBe('Email is already verified')
    })

    it('should set loading state correctly', async () => {
      // Arrange
      vi.mocked(api.post).mockImplementation(
        () =>
          new Promise((resolve) =>
            setTimeout(
              () => resolve({ data: { success: true, data: null, error: null } } as any),
              100
            )
          )
      )

      // Act
      const { resendVerification, loading } = useAuth()
      const promise = resendVerification({ email: 'test@example.com' })

      // Assert - loading should be true during request
      expect(loading.value).toBe(true)

      await promise

      // Assert - loading should be false after request
      expect(loading.value).toBe(false)
    })
  })
})
