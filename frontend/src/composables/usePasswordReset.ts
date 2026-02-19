import { ref } from 'vue'
import { api } from '@/utils/api'
import type { ApiResponse } from '@/types/api'

export function usePasswordReset() {
  const loading = ref(false)
  const error = ref<string | null>(null)

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
    try {
      await api.post<ApiResponse<object>>('/auth/reset-password', { token, newPassword })
      return true
    } catch (err: any) {
      error.value =
        err.response?.data?.error?.message ||
        'El enlace de recuperación es inválido o ha expirado.'
      return false
    } finally {
      loading.value = false
    }
  }

  return { loading, error, forgotPassword, resetPassword }
}
