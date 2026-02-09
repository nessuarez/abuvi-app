import { ref, computed } from 'vue'
import { defineStore } from 'pinia'
import type { UserInfo } from '@/types/auth'

const AUTH_TOKEN_KEY = 'authToken'

export const useAuthStore = defineStore('auth', () => {
  // State
  const user = ref<UserInfo | null>(null)
  const token = ref<string | null>(null)

  // Getters
  const isAuthenticated = computed(() => !!token.value)
  const isAdmin = computed(() => user.value?.role === 'Admin')
  const isBoard = computed(() =>
    user.value?.role === 'Admin' || user.value?.role === 'Board'
  )

  // Actions
  function setAuth(authData: { user: UserInfo; token: string }) {
    user.value = authData.user
    token.value = authData.token
    localStorage.setItem(AUTH_TOKEN_KEY, authData.token)
  }

  function clearAuth() {
    user.value = null
    token.value = null
    localStorage.removeItem(AUTH_TOKEN_KEY)
  }

  function restoreSession() {
    const savedToken = localStorage.getItem(AUTH_TOKEN_KEY)
    if (savedToken) {
      token.value = savedToken
      // Note: User info will be fetched from API or decoded from token if needed
      // For now, just restore the token; user info will be fetched on protected route access
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
