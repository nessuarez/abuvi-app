import { ref } from 'vue'
import { api } from '@/utils/api'
import type {
  CampEditionAccommodation,
  CreateCampEditionAccommodationRequest,
  UpdateCampEditionAccommodationRequest
} from '@/types/camp-edition'
import type { ApiResponse } from '@/types/api'

export function useCampAccommodations(editionId: string) {
  const accommodations = ref<CampEditionAccommodation[]>([])
  const loading = ref(false)
  const error = ref<string | null>(null)

  const fetchAccommodations = async (): Promise<void> => {
    loading.value = true
    error.value = null
    try {
      const response = await api.get<ApiResponse<CampEditionAccommodation[]>>(
        `/camps/editions/${editionId}/accommodations`
      )
      if (response.data.success && response.data.data) {
        accommodations.value = response.data.data
      } else {
        accommodations.value = []
      }
    } catch (err: unknown) {
      error.value =
        (err as { response?: { data?: { error?: { message?: string } } } })?.response?.data?.error
          ?.message || 'Error al cargar alojamientos'
      console.error('Failed to fetch accommodations:', err)
      accommodations.value = []
    } finally {
      loading.value = false
    }
  }

  const getAccommodationById = async (
    id: string
  ): Promise<CampEditionAccommodation | null> => {
    loading.value = true
    error.value = null
    try {
      const response = await api.get<ApiResponse<CampEditionAccommodation>>(
        `/camps/editions/accommodations/${id}`
      )
      if (response.data.success && response.data.data) {
        return response.data.data
      }
      return null
    } catch (err: unknown) {
      error.value =
        (err as { response?: { data?: { error?: { message?: string } } } })?.response?.data?.error
          ?.message || 'Error al cargar alojamiento'
      console.error('Failed to fetch accommodation:', err)
      return null
    } finally {
      loading.value = false
    }
  }

  const createAccommodation = async (
    request: CreateCampEditionAccommodationRequest
  ): Promise<CampEditionAccommodation | null> => {
    loading.value = true
    error.value = null
    try {
      const response = await api.post<ApiResponse<CampEditionAccommodation>>(
        `/camps/editions/${editionId}/accommodations`,
        request
      )
      if (response.data.success && response.data.data) {
        accommodations.value.push(response.data.data)
        return response.data.data
      }
      return null
    } catch (err: unknown) {
      error.value =
        (err as { response?: { data?: { error?: { message?: string } } } })?.response?.data?.error
          ?.message || 'Error al crear alojamiento'
      console.error('Failed to create accommodation:', err)
      return null
    } finally {
      loading.value = false
    }
  }

  const updateAccommodation = async (
    id: string,
    request: UpdateCampEditionAccommodationRequest
  ): Promise<CampEditionAccommodation | null> => {
    loading.value = true
    error.value = null
    try {
      const response = await api.put<ApiResponse<CampEditionAccommodation>>(
        `/camps/editions/accommodations/${id}`,
        request
      )
      if (response.data.success && response.data.data) {
        const index = accommodations.value.findIndex((a) => a.id === id)
        if (index !== -1) {
          accommodations.value[index] = response.data.data
        }
        return response.data.data
      }
      return null
    } catch (err: unknown) {
      error.value =
        (err as { response?: { data?: { error?: { message?: string } } } })?.response?.data?.error
          ?.message || 'Error al actualizar alojamiento'
      console.error('Failed to update accommodation:', err)
      return null
    } finally {
      loading.value = false
    }
  }

  const deleteAccommodation = async (id: string): Promise<boolean> => {
    loading.value = true
    error.value = null
    try {
      await api.delete(`/camps/editions/accommodations/${id}`)
      accommodations.value = accommodations.value.filter((a) => a.id !== id)
      return true
    } catch (err: unknown) {
      error.value =
        (err as { response?: { data?: { error?: { message?: string } } } })?.response?.data?.error
          ?.message || 'Error al eliminar alojamiento'
      console.error('Failed to delete accommodation:', err)
      return false
    } finally {
      loading.value = false
    }
  }

  const activateAccommodation = async (id: string): Promise<boolean> => {
    loading.value = true
    error.value = null
    try {
      await api.patch(`/camps/editions/accommodations/${id}/activate`)
      const index = accommodations.value.findIndex((a) => a.id === id)
      if (index !== -1) {
        accommodations.value[index] = { ...accommodations.value[index], isActive: true }
      }
      return true
    } catch (err: unknown) {
      error.value =
        (err as { response?: { data?: { error?: { message?: string } } } })?.response?.data?.error
          ?.message || 'Error al activar alojamiento'
      console.error('Failed to activate accommodation:', err)
      return false
    } finally {
      loading.value = false
    }
  }

  const deactivateAccommodation = async (id: string): Promise<boolean> => {
    loading.value = true
    error.value = null
    try {
      await api.patch(`/camps/editions/accommodations/${id}/deactivate`)
      const index = accommodations.value.findIndex((a) => a.id === id)
      if (index !== -1) {
        accommodations.value[index] = { ...accommodations.value[index], isActive: false }
      }
      return true
    } catch (err: unknown) {
      error.value =
        (err as { response?: { data?: { error?: { message?: string } } } })?.response?.data?.error
          ?.message || 'Error al desactivar alojamiento'
      console.error('Failed to deactivate accommodation:', err)
      return false
    } finally {
      loading.value = false
    }
  }

  return {
    accommodations,
    loading,
    error,
    fetchAccommodations,
    getAccommodationById,
    createAccommodation,
    updateAccommodation,
    deleteAccommodation,
    activateAccommodation,
    deactivateAccommodation
  }
}
