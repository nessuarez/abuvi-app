import { ref } from 'vue'
import { api } from '@/utils/api'
import type { AgeRangeSettings, UpdateAgeRangesRequest } from '@/types/association-settings'
import type { ApiResponse } from '@/types/api'

export function useAssociationSettings() {
  const ageRanges = ref<AgeRangeSettings | null>(null)
  const loading = ref(false)
  const error = ref<string | null>(null)

  const fetchAgeRanges = async (): Promise<void> => {
    loading.value = true
    error.value = null
    try {
      const response = await api.get<ApiResponse<AgeRangeSettings>>('/settings/age-ranges')
      if (response.data.success && response.data.data) {
        ageRanges.value = response.data.data
      } else {
        ageRanges.value = null
      }
    } catch (err: unknown) {
      error.value = (err as { response?: { data?: { error?: { message?: string } } } })
        ?.response?.data?.error?.message || 'Error al cargar rangos de edad'
      console.error('Failed to fetch age ranges:', err)
      ageRanges.value = null
    } finally {
      loading.value = false
    }
  }

  const updateAgeRanges = async (request: UpdateAgeRangesRequest): Promise<boolean> => {
    loading.value = true
    error.value = null
    try {
      const response = await api.put<ApiResponse<AgeRangeSettings>>(
        '/settings/age-ranges',
        request
      )
      if (response.data.success && response.data.data) {
        ageRanges.value = response.data.data
        return true
      }
      return false
    } catch (err: unknown) {
      error.value = (err as { response?: { data?: { error?: { message?: string } } } })
        ?.response?.data?.error?.message || 'Error al actualizar rangos de edad'
      console.error('Failed to update age ranges:', err)
      return false
    } finally {
      loading.value = false
    }
  }

  return {
    ageRanges,
    loading,
    error,
    fetchAgeRanges,
    updateAgeRanges
  }
}
