import { ref, computed } from 'vue'
import { defineStore } from 'pinia'
import type { UserInfo } from '@/types/auth'

const AUTH_TOKEN_KEY = 'abuvi_auth_token'
const AUTH_USER_KEY = 'abuvi_user'

export const useAuthStore = defineStore('auth', () => {
  // State
  const user = ref<UserInfo | null>(null)
  const token = ref<string | null>(null)

  // Getters
  const isAuthenticated = computed(() => !!token.value && !!user.value)
  const isAdmin = computed(() => user.value?.role === 'Admin')
  const isBoard = computed(() =>
    user.value?.role === 'Admin' || user.value?.role === 'Board'
  )

  // Actions
  /**
   * Authenticates user and persists both token and user data to localStorage.
   * @param authData - Object containing user info and JWT token
   */
  function setAuth(authData: { user: UserInfo; token: string }) {
    user.value = authData.user
    token.value = authData.token

    // Persist both token and user data
    localStorage.setItem(AUTH_TOKEN_KEY, authData.token)
    localStorage.setItem(AUTH_USER_KEY, JSON.stringify(authData.user))
  }

  /**
   * Clears all authentication state from memory and localStorage.
   * Automatically called on logout or when receiving 401 response.
   */
  function clearAuth() {
    user.value = null
    token.value = null
    localStorage.removeItem(AUTH_TOKEN_KEY)
    localStorage.removeItem(AUTH_USER_KEY)
  }

  /**
   * Restores authentication state from localStorage.
   * Validates data integrity and clears corrupted or inconsistent state.
   * Called automatically when app initializes (App.vue onMounted).
   */
  function restoreSession() {
    const savedToken = localStorage.getItem(AUTH_TOKEN_KEY)
    const savedUser = localStorage.getItem(AUTH_USER_KEY)

    if (savedToken && savedUser) {
      token.value = savedToken

      try {
        user.value = JSON.parse(savedUser)
      } catch (error) {
        console.error('Failed to parse stored user data', error)
        clearAuth() // Clear corrupted data
      }
    } else if (savedToken && !savedUser) {
      // Token exists but user data missing - clear inconsistent state
      console.warn('Inconsistent auth state detected, clearing storage')
      clearAuth()
    }
  }

  return {
    user,
    token,
    isAuthenticated,
    isAdmin,
    isBoard,
    setAuth,
    clearAuth,
    restoreSession
  }
})
