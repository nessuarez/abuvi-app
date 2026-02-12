import { ref } from 'vue'
import { api } from '@/utils/api'
import type { ApiResponse } from '@/types/api'
import type {
  RegisterUserRequest,
  VerifyEmailRequest,
  ResendVerificationRequest,
  User,
  MessageResponse
} from '@/types/auth'

export function useAuth() {
  const loading = ref(false)
  const error = ref<string | null>(null)

  const mapErrorCodeToMessage = (errorCode?: string): string => {
    switch (errorCode) {
      case 'EMAIL_EXISTS':
        return 'An account with this email already exists'
      case 'DOCUMENT_EXISTS':
        return 'An account with this document number already exists'
      case 'VERIFICATION_FAILED':
        return 'Verification token has expired. Please request a new one.'
      case 'EMAIL_NOT_VERIFIED':
        return 'Please verify your email before logging in'
      case 'NOT_FOUND':
        return 'Invalid verification token'
      case 'RESEND_FAILED':
        return 'Email is already verified'
      default:
        return 'An error occurred. Please try again.'
    }
  }

  const registerUser = async (
    request: RegisterUserRequest
  ): Promise<User | null> => {
    loading.value = true
    error.value = null
    try {
      const response = await api.post<ApiResponse<User>>(
        '/auth/register-user',
        request
      )
      return response.data.data
    } catch (err: any) {
      const errorCode = err.response?.data?.error?.code
      error.value = mapErrorCodeToMessage(errorCode)
      return null
    } finally {
      loading.value = false
    }
  }

  const verifyEmail = async (
    request: VerifyEmailRequest
  ): Promise<boolean> => {
    loading.value = true
    error.value = null
    try {
      await api.post<ApiResponse<MessageResponse>>(
        '/auth/verify-email',
        request
      )
      return true
    } catch (err: any) {
      const errorCode = err.response?.data?.error?.code
      error.value = mapErrorCodeToMessage(errorCode)
      return false
    } finally {
      loading.value = false
    }
  }

  const resendVerification = async (
    request: ResendVerificationRequest
  ): Promise<boolean> => {
    loading.value = true
    error.value = null
    try {
      await api.post<ApiResponse<MessageResponse>>(
        '/auth/resend-verification',
        request
      )
      return true
    } catch (err: any) {
      const errorCode = err.response?.data?.error?.code
      error.value = mapErrorCodeToMessage(errorCode)
      return false
    } finally {
      loading.value = false
    }
  }

  return {
    loading,
    error,
    registerUser,
    verifyEmail,
    resendVerification
  }
}
