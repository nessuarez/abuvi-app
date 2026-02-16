import { ref } from 'vue'
import { api } from '@/utils/api'
import type { CampEditionExtra, CreateCampExtraRequest } from '@/types/camp-edition'
import type { ApiResponse } from '@/types/api'

export function useCampExtras(editionId: string) {
  const extras = ref<CampEditionExtra[]>([])
  const loading = ref(false)
  const error = ref<string | null>(null)

  const fetchExtras = async (): Promise<void> => {
    loading.value = true
    error.value = null
    try {
      const response = await api.get<ApiResponse<CampEditionExtra[]>>(
        `/camps/editions/${editionId}/extras`
      )
      if (response.data.success && response.data.data) {
        extras.value = response.data.data
      } else {
        extras.value = []
      }
    } catch (err: unknown) {
      error.value = (err as { response?: { data?: { error?: { message?: string } } } })
        ?.response?.data?.error?.message || 'Error al cargar extras'
      console.error('Failed to fetch extras:', err)
      extras.value = []
    } finally {
      loading.value = false
    }
  }

  const getExtraById = async (extraId: string): Promise<CampEditionExtra | null> => {
    loading.value = true
    error.value = null
    try {
      const response = await api.get<ApiResponse<CampEditionExtra>>(
        `/camps/editions/${editionId}/extras/${extraId}`
      )
      if (response.data.success && response.data.data) {
        return response.data.data
      }
      return null
    } catch (err: unknown) {
      error.value = (err as { response?: { data?: { error?: { message?: string } } } })
        ?.response?.data?.error?.message || 'Error al cargar extra'
      console.error('Failed to fetch extra:', err)
      return null
    } finally {
      loading.value = false
    }
  }

  const createExtra = async (
    request: CreateCampExtraRequest
  ): Promise<CampEditionExtra | null> => {
    loading.value = true
    error.value = null
    try {
      const response = await api.post<ApiResponse<CampEditionExtra>>(
        `/camps/editions/${editionId}/extras`,
        request
      )
      if (response.data.success && response.data.data) {
        extras.value.push(response.data.data)
        return response.data.data
      }
      return null
    } catch (err: unknown) {
      error.value = (err as { response?: { data?: { error?: { message?: string } } } })
        ?.response?.data?.error?.message || 'Error al crear extra'
      console.error('Failed to create extra:', err)
      return null
    } finally {
      loading.value = false
    }
  }

  const updateExtra = async (
    extraId: string,
    request: CreateCampExtraRequest
  ): Promise<CampEditionExtra | null> => {
    loading.value = true
    error.value = null
    try {
      const response = await api.put<ApiResponse<CampEditionExtra>>(
        `/camps/editions/${editionId}/extras/${extraId}`,
        request
      )
      if (response.data.success && response.data.data) {
        const index = extras.value.findIndex((e) => e.id === extraId)
        if (index !== -1) {
          extras.value[index] = response.data.data
        }
        return response.data.data
      }
      return null
    } catch (err: unknown) {
      error.value = (err as { response?: { data?: { error?: { message?: string } } } })
        ?.response?.data?.error?.message || 'Error al actualizar extra'
      console.error('Failed to update extra:', err)
      return null
    } finally {
      loading.value = false
    }
  }

  const deleteExtra = async (extraId: string): Promise<boolean> => {
    loading.value = true
    error.value = null
    try {
      await api.delete(`/camps/editions/${editionId}/extras/${extraId}`)
      extras.value = extras.value.filter((e) => e.id !== extraId)
      return true
    } catch (err: unknown) {
      error.value = (err as { response?: { data?: { error?: { message?: string } } } })
        ?.response?.data?.error?.message || 'Error al eliminar extra'
      console.error('Failed to delete extra:', err)
      return false
    } finally {
      loading.value = false
    }
  }

  return {
    extras,
    loading,
    error,
    fetchExtras,
    getExtraById,
    createExtra,
    updateExtra,
    deleteExtra
  }
}
