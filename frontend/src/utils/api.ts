import axios from 'axios'
import { useAuthStore } from '@/stores/auth'

const API_BASE_URL = import.meta.env.VITE_API_URL || 'http://localhost:5079/api'

export const api = axios.create({
  baseURL: API_BASE_URL,
  headers: {
    'Content-Type': 'application/json'
  }
})

// Request interceptor - attach JWT token
api.interceptors.request.use(
  (config) => {
    const authStore = useAuthStore()
    if (authStore.token) {
      config.headers.Authorization = `Bearer ${authStore.token}`
    }
    return config
  },
  (error) => {
    return Promise.reject(error)
  }
)

// Response interceptor - handle 401 and errors
api.interceptors.response.use(
  (response) => response,
  (error) => {
    console.error('API Error:', error)

    // Handle 401 Unauthorized globally
    if (error.response?.status === 401) {
      const authStore = useAuthStore()
      authStore.clearAuth()
      // Use window.location to avoid circular dependency with router
      window.location.href = '/login'
    }

    return Promise.reject(error)
  }
)
