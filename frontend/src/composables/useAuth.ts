import { ref } from 'vue'
import { useRouter } from 'vue-router'
import { api } from '@/utils/api'
import { useAuthStore } from '@/stores/auth'
import type { LoginRequest, RegisterRequest, LoginResponse, UserInfo } from '@/types/auth'
import type { ApiResponse } from '@/types/api'

export function useAuth() {
  const loading = ref(false)
  const error = ref<string | null>(null)
  const authStore = useAuthStore()
  const router = useRouter()

  async function login(credentials: LoginRequest): Promise<boolean> {
    loading.value = true
    error.value = null
    try {
      const response = await api.post<ApiResponse<LoginResponse>>(
        '/auth/login',
        credentials
      )

      if (response.data.success && response.data.data) {
        authStore.setAuth(response.data.data)
        return true
      } else {
        error.value = response.data.error?.message || 'Login failed'
        return false
      }
    } catch (err: any) {
      // Handle 401 Unauthorized
      if (err.response?.status === 401) {
        error.value = 'Invalid email or password'
      } else {
        error.value = 'Network error. Please try again.'
      }
      return false
    } finally {
      loading.value = false
    }
  }

  async function register(data: RegisterRequest): Promise<UserInfo | null> {
    loading.value = true
    error.value = null
    try {
      const response = await api.post<ApiResponse<UserInfo>>(
        '/auth/register',
        data
      )

      if (response.data.success && response.data.data) {
        return response.data.data
      } else {
        error.value = response.data.error?.message || 'Registration failed'
        return null
      }
    } catch (err: any) {
      // Handle 400 Bad Request (email exists)
      if (err.response?.status === 400) {
        error.value = err.response.data.error?.message || 'Email already registered'
      } else {
        error.value = 'Network error. Please try again.'
      }
      return null
    } finally {
      loading.value = false
    }
  }

  function logout(): void {
    authStore.clearAuth()
    router.push('/login')
  }

  return { loading, error, login, register, logout }
}
