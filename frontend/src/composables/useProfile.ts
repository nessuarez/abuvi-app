import { ref } from 'vue'
import { api } from '@/utils/api'
import { useAuthStore } from '@/stores/auth'
import type { User } from '@/types/user'
import type { ApiResponse } from '@/types/api'

export interface UpdateProfileRequest {
  firstName: string
  lastName: string
  phone: string | null
}

export function useProfile() {
  const fullUser = ref<User | null>(null)
  const loading = ref(false)
  const error = ref<string | null>(null)

  /**
   * Load the current user's full profile (includes phone not in auth store).
   */
  const loadProfile = async (): Promise<void> => {
    const auth = useAuthStore()
    if (!auth.user) return

    loading.value = true
    error.value = null
    try {
      const response = await api.get<ApiResponse<User>>(`/users/${auth.user.id}`)
      fullUser.value = response.data.data
    } catch {
      error.value = 'Error al cargar el perfil'
    } finally {
      loading.value = false
    }
  }

  /**
   * Update the current user's profile and sync the auth store.
   * Always sends isActive: true (users cannot self-deactivate).
   */
  const updateProfile = async (request: UpdateProfileRequest): Promise<boolean> => {
    const auth = useAuthStore()
    if (!auth.user) return false

    loading.value = true
    error.value = null
    try {
      const response = await api.put<ApiResponse<User>>(`/users/${auth.user.id}`, {
        firstName: request.firstName,
        lastName: request.lastName,
        phone: request.phone,
        isActive: true,
      })
      fullUser.value = response.data.data
      auth.updateProfile({
        firstName: request.firstName,
        lastName: request.lastName,
        phone: request.phone,
      })
      return true
    } catch (err: any) {
      error.value = err.response?.data?.error?.message || 'Error al actualizar el perfil'
      return false
    } finally {
      loading.value = false
    }
  }

  return { fullUser, loading, error, loadProfile, updateProfile }
}
