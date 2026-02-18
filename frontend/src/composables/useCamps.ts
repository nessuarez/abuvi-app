import { ref } from 'vue'
import { api } from '@/utils/api'
import type { Camp, CreateCampRequest, UpdateCampRequest } from '@/types/camp'
import type { ApiResponse } from '@/types/api'

export function useCamps() {
  const camps = ref<Camp[]>([])
  const loading = ref(false)
  const error = ref<string | null>(null)

  const fetchCamps = async (params?: {
    isActive?: boolean
    skip?: number
    take?: number
  }): Promise<void> => {
    loading.value = true
    error.value = null
    try {
      const queryParams = new URLSearchParams()
      if (params?.isActive !== undefined) queryParams.append('isActive', params.isActive.toString())
      if (params?.skip !== undefined) queryParams.append('skip', params.skip.toString())
      if (params?.take !== undefined) queryParams.append('take', params.take.toString())

      const url = `/camps${queryParams.toString() ? `?${queryParams.toString()}` : ''}`
      const response = await api.get<ApiResponse<Camp[]>>(url)

      if (response.data.success && response.data.data) {
        camps.value = response.data.data
      } else {
        camps.value = []
      }
    } catch (err: unknown) {
      error.value = (err as { response?: { data?: { error?: { message?: string } } } })
        ?.response?.data?.error?.message || 'Error al cargar campamentos'
      console.error('Failed to fetch camps:', err)
      camps.value = []
    } finally {
      loading.value = false
    }
  }

  const getCampById = async (id: string): Promise<Camp | null> => {
    loading.value = true
    error.value = null
    try {
      const response = await api.get<ApiResponse<Camp>>(`/camps/${id}`)
      if (response.data.success && response.data.data) {
        return response.data.data
      }
      return null
    } catch (err: unknown) {
      error.value = (err as { response?: { data?: { error?: { message?: string } } } })
        ?.response?.data?.error?.message || 'Error al cargar campamento'
      console.error('Failed to fetch camp:', err)
      return null
    } finally {
      loading.value = false
    }
  }

  const createCamp = async (request: CreateCampRequest): Promise<Camp | null> => {
    loading.value = true
    error.value = null
    try {
      const response = await api.post<ApiResponse<Camp>>('/camps', request)
      if (response.data.success && response.data.data) {
        camps.value = [...(camps.value || []), response.data.data]
        return response.data.data
      }
      return null
    } catch (err: unknown) {
      error.value = (err as { response?: { data?: { error?: { message?: string } } } })
        ?.response?.data?.error?.message || 'Error al crear campamento'
      console.error('Failed to create camp:', err)
      return null
    } finally {
      loading.value = false
    }
  }

  const updateCamp = async (id: string, request: UpdateCampRequest): Promise<Camp | null> => {
    loading.value = true
    error.value = null
    try {
      const response = await api.put<ApiResponse<Camp>>(`/camps/${id}`, request)
      if (response.data.success && response.data.data) {
        camps.value = (camps.value || []).map((c) =>
          c.id === id ? response.data.data! : c
        )
        return response.data.data
      }
      return null
    } catch (err: unknown) {
      error.value = (err as { response?: { data?: { error?: { message?: string } } } })
        ?.response?.data?.error?.message || 'Error al actualizar campamento'
      console.error('Failed to update camp:', err)
      return null
    } finally {
      loading.value = false
    }
  }

  const deleteCamp = async (id: string): Promise<boolean> => {
    loading.value = true
    error.value = null
    try {
      await api.delete(`/camps/${id}`)
      camps.value = (camps.value || []).filter((c) => c.id !== id)
      return true
    } catch (err: unknown) {
      error.value = (err as { response?: { data?: { error?: { message?: string } } } })
        ?.response?.data?.error?.message || 'Error al eliminar campamento'
      console.error('Failed to delete camp:', err)
      return false
    } finally {
      loading.value = false
    }
  }

  return {
    camps,
    loading,
    error,
    fetchCamps,
    getCampById,
    createCamp,
    updateCamp,
    deleteCamp
  }
}
