import axios from 'axios'

const API_BASE_URL = import.meta.env.VITE_API_URL || 'http://localhost:5000/api'

export const api = axios.create({
  baseURL: API_BASE_URL,
  headers: {
    'Content-Type': 'application/json'
  }
})

// Request interceptor - attach auth token
api.interceptors.request.use((config) => {
  // Future: attach token from auth store
  return config
})

// Response interceptor - global error handling
api.interceptors.response.use(
  (response) => response,
  (error) => {
    console.error('API Error:', error)
    // Future: handle 401 with auth logout
    return Promise.reject(error)
  }
)
