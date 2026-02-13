import { ref, computed } from 'vue'
import { defineStore } from 'pinia'
import { api } from '@/utils/api'
import type { UserInfo, UserRole } from '@/types/auth'
import type { LoginRequest, RegisterRequest, AuthResponse } from '@/types/auth'
import type { ApiResponse } from '@/types/api'

const TOKEN_KEY = 'abuvi_auth_token'
const USER_KEY = 'abuvi_user'

export const useAuthStore = defineStore('auth', () => {
  // State
  const user = ref<UserInfo | null>(loadUserFromStorage())
  const token = ref<string | null>(loadTokenFromStorage())

  // Getters
  const isAuthenticated = computed(() => !!token.value && !!user.value)
  const isAdmin = computed(() => user.value?.role === 'Admin')
  const isBoard = computed(() =>
    user.value?.role === 'Admin' || user.value?.role === 'Board'
  )
  const isMember = computed(() => !!user.value)
  const fullName = computed(() =>
    user.value ? `${user.value.firstName} ${user.value.lastName}` : ''
  )

  // Actions
  async function login(credentials: LoginRequest): Promise<{ success: boolean; error?: string }> {
    try {
      const response = await api.post<ApiResponse<AuthResponse>>(
        '/auth/login',
        credentials
      )

      if (response.data.success && response.data.data) {
        const { user: userData, token: authToken } = response.data.data
        user.value = userData
        token.value = authToken

        // Persist to localStorage if rememberMe is true
        if (credentials.rememberMe) {
          saveToStorage(userData, authToken)
        }

        return { success: true }
      }

      return {
        success: false,
        error: response.data.error?.message || 'Login failed'
      }
    } catch (err: any) {
      return {
        success: false,
        error: err.response?.data?.error?.message || 'Network error. Please try again.'
      }
    }
  }

  async function register(data: RegisterRequest): Promise<{ success: boolean; error?: string }> {
    try {
      const response = await api.post<ApiResponse<AuthResponse>>(
        '/auth/register',
        data
      )

      if (response.data.success && response.data.data) {
        const { user: userData, token: authToken } = response.data.data
        user.value = userData
        token.value = authToken
        saveToStorage(userData, authToken)

        return { success: true }
      }

      return {
        success: false,
        error: response.data.error?.message || 'Registration failed'
      }
    } catch (err: any) {
      return {
        success: false,
        error: err.response?.data?.error?.message || 'Network error. Please try again.'
      }
    }
  }

  function logout() {
    user.value = null
    token.value = null
    clearStorage()
  }

  // Legacy methods for backward compatibility
  function setAuth(authData: { user: UserInfo; token: string }) {
    user.value = authData.user
    token.value = authData.token
    localStorage.setItem(TOKEN_KEY, authData.token)
    localStorage.setItem(USER_KEY, JSON.stringify(authData.user))
  }

  function clearAuth() {
    logout()
  }

  function restoreSession() {
    const savedUser = loadUserFromStorage()
    const savedToken = loadTokenFromStorage()
    if (savedUser && savedToken) {
      user.value = savedUser
      token.value = savedToken
    }
  }

  // Helper functions
  function loadUserFromStorage(): UserInfo | null {
    const userData = localStorage.getItem(USER_KEY)
    try {
      return userData ? JSON.parse(userData) : null
    } catch (error) {
      console.error('Failed to parse stored user data', error)
      return null
    }
  }

  function loadTokenFromStorage(): string | null {
    return localStorage.getItem(TOKEN_KEY)
  }

  function saveToStorage(userData: UserInfo, authToken: string) {
    localStorage.setItem(USER_KEY, JSON.stringify(userData))
    localStorage.setItem(TOKEN_KEY, authToken)
  }

  function clearStorage() {
    localStorage.removeItem(USER_KEY)
    localStorage.removeItem(TOKEN_KEY)
  }

  return {
    user,
    token,
    isAuthenticated,
    isAdmin,
    isBoard,
    isMember,
    fullName,
    login,
    register,
    logout,
    // Legacy methods
    setAuth,
    clearAuth,
    restoreSession
  }
})
