import { describe, it, expect, vi, beforeEach } from 'vitest'
import { usePasswordReset } from '@/composables/usePasswordReset'
import { api } from '@/utils/api'

vi.mock('@/utils/api')

describe('usePasswordReset', () => {
  beforeEach(() => {
    vi.clearAllMocks()
  })

  // --- forgotPassword ---

  describe('forgotPassword', () => {
    it('should call POST /auth/forgot-password with email', async () => {
      vi.mocked(api.post).mockResolvedValue({ data: { success: true, data: {}, error: null } })

      const { forgotPassword } = usePasswordReset()
      await forgotPassword('test@example.com')

      expect(api.post).toHaveBeenCalledWith('/auth/forgot-password', { email: 'test@example.com' })
    })

    it('should return true on 200 response', async () => {
      vi.mocked(api.post).mockResolvedValue({ data: { success: true, data: {}, error: null } })

      const { forgotPassword } = usePasswordReset()
      const result = await forgotPassword('test@example.com')

      expect(result).toBe(true)
    })

    it('should return false and set error on network error', async () => {
      vi.mocked(api.post).mockRejectedValue(new Error('Network error'))

      const { error, forgotPassword } = usePasswordReset()
      const result = await forgotPassword('test@example.com')

      expect(result).toBe(false)
      expect(error.value).toBe('Error de red. Por favor intenta de nuevo.')
    })

    it('should reset loading to false after success', async () => {
      vi.mocked(api.post).mockResolvedValue({ data: { success: true, data: {}, error: null } })

      const { loading, forgotPassword } = usePasswordReset()
      await forgotPassword('test@example.com')

      expect(loading.value).toBe(false)
    })

    it('should reset loading to false after error', async () => {
      vi.mocked(api.post).mockRejectedValue(new Error('Network error'))

      const { loading, forgotPassword } = usePasswordReset()
      await forgotPassword('test@example.com')

      expect(loading.value).toBe(false)
    })

    it('should clear previous error on new call', async () => {
      vi.mocked(api.post)
        .mockRejectedValueOnce(new Error('Network error'))
        .mockResolvedValueOnce({ data: { success: true, data: {}, error: null } })

      const { error, forgotPassword } = usePasswordReset()
      await forgotPassword('test@example.com') // fails
      expect(error.value).not.toBeNull()

      await forgotPassword('test@example.com') // succeeds
      expect(error.value).toBeNull()
    })
  })

  // --- resetPassword ---

  describe('resetPassword', () => {
    it('should call POST /auth/reset-password with token and newPassword', async () => {
      vi.mocked(api.post).mockResolvedValue({ data: { success: true, data: {}, error: null } })

      const { resetPassword } = usePasswordReset()
      await resetPassword('my-token', 'newPass123')

      expect(api.post).toHaveBeenCalledWith('/auth/reset-password', {
        token: 'my-token',
        newPassword: 'newPass123'
      })
    })

    it('should return true on 200 response', async () => {
      vi.mocked(api.post).mockResolvedValue({ data: { success: true, data: {}, error: null } })

      const { resetPassword } = usePasswordReset()
      const result = await resetPassword('valid-token', 'newPass123')

      expect(result).toBe(true)
    })

    it('should return false and set error message from backend on 400', async () => {
      vi.mocked(api.post).mockRejectedValue({
        response: {
          status: 400,
          data: { error: { message: 'El enlace de recuperación es inválido o ha expirado.' } }
        }
      })

      const { error, resetPassword } = usePasswordReset()
      const result = await resetPassword('expired-token', 'newPass123')

      expect(result).toBe(false)
      expect(error.value).toBe('El enlace de recuperación es inválido o ha expirado.')
    })

    it('should use fallback error message when backend provides none', async () => {
      vi.mocked(api.post).mockRejectedValue({ response: { status: 400, data: {} } })

      const { error, resetPassword } = usePasswordReset()
      await resetPassword('expired-token', 'newPass123')

      expect(error.value).toBe('El enlace de recuperación es inválido o ha expirado.')
    })

    it('should reset loading to false after success', async () => {
      vi.mocked(api.post).mockResolvedValue({ data: { success: true, data: {}, error: null } })

      const { loading, resetPassword } = usePasswordReset()
      await resetPassword('valid-token', 'newPass123')

      expect(loading.value).toBe(false)
    })

    it('should reset loading to false after error', async () => {
      vi.mocked(api.post).mockRejectedValue({ response: { status: 400, data: {} } })

      const { loading, resetPassword } = usePasswordReset()
      await resetPassword('expired-token', 'newPass123')

      expect(loading.value).toBe(false)
    })

    it('should clear previous error on new call', async () => {
      vi.mocked(api.post)
        .mockRejectedValueOnce({ response: { status: 400, data: {} } })
        .mockResolvedValueOnce({ data: { success: true, data: {}, error: null } })

      const { error, resetPassword } = usePasswordReset()
      await resetPassword('bad-token', 'newPass123')
      expect(error.value).not.toBeNull()

      await resetPassword('good-token', 'newPass123')
      expect(error.value).toBeNull()
    })

    it('should map validation details to fieldErrors', async () => {
      vi.mocked(api.post).mockRejectedValue({
        response: {
          status: 400,
          data: {
            error: {
              message: 'Validation failed',
              code: 'VALIDATION_ERROR',
              details: [
                { field: 'NewPassword', message: 'La contraseña debe contener al menos un carácter especial' }
              ]
            }
          }
        }
      })

      const { error, fieldErrors, resetPassword } = usePasswordReset()
      const result = await resetPassword('valid-token', 'weakpass')

      expect(result).toBe(false)
      expect(error.value).toBeNull()
      expect(fieldErrors.value).toEqual({
        newPassword: 'La contraseña debe contener al menos un carácter especial'
      })
    })

    it('should clear fieldErrors on new call', async () => {
      vi.mocked(api.post)
        .mockRejectedValueOnce({
          response: {
            status: 400,
            data: {
              error: {
                message: 'Validation failed',
                details: [{ field: 'NewPassword', message: 'error' }]
              }
            }
          }
        })
        .mockResolvedValueOnce({ data: { success: true, data: {}, error: null } })

      const { fieldErrors, resetPassword } = usePasswordReset()
      await resetPassword('token', 'weak')
      expect(Object.keys(fieldErrors.value).length).toBeGreaterThan(0)

      await resetPassword('token', 'Strong1!')
      expect(Object.keys(fieldErrors.value).length).toBe(0)
    })
  })
})
