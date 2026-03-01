import { ref } from 'vue'
import { api } from '@/utils/api'
import type { ApiResponse } from '@/types/api'

export function usePasswordReset() {
  const loading = ref(false)
  const error = ref<string | null>(null)
  const fieldErrors = ref<Record<string, string>>({})

  const forgotPassword = async (email: string): Promise<boolean> => {
    loading.value = true
    error.value = null
    try {
      await api.post<ApiResponse<object>>('/auth/forgot-password', { email })
      return true
    } catch (err: any) {
      // Network-level failure only — backend always returns 200
      error.value =
        err.response?.data?.error?.message ||
        'Error de red. Por favor intenta de nuevo.'
      return false
    } finally {
      loading.value = false
    }
  }

  const resetPassword = async (token: string, newPassword: string): Promise<boolean> => {
    loading.value = true
    error.value = null
    fieldErrors.value = {}
    try {
      await api.post<ApiResponse<object>>('/auth/reset-password', { token, newPassword })
      return true
    } catch (err: any) {
      const apiError = err.response?.data?.error
      const details = apiError?.details as Array<{ field: string; message: string }> | undefined

      if (details?.length) {
        for (const detail of details) {
          const key = detail.field.charAt(0).toLowerCase() + detail.field.slice(1)
          fieldErrors.value[key] = detail.message
        }
      } else {
        error.value =
          apiError?.message ||
          'El enlace de recuperación es inválido o ha expirado.'
      }
      return false
    } finally {
      loading.value = false
    }
  }

  return { loading, error, fieldErrors, resetPassword, forgotPassword }
}
