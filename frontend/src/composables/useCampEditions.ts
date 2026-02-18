import { ref } from 'vue'
import { api } from '@/utils/api'
import type {
  CampEdition,
  CreateCampEditionRequest,
  ProposeCampEditionRequest,
  CampEditionStatus
} from '@/types/camp-edition'
import type { ApiResponse } from '@/types/api'

export function useCampEditions() {
  const editions = ref<CampEdition[]>([])
  const activeEdition = ref<CampEdition | null>(null)
  const currentCampEdition = ref<CampEdition | null>(null)
  const loading = ref(false)
  const error = ref<string | null>(null)

  const fetchProposedEditions = async (year: number): Promise<void> => {
    loading.value = true
    error.value = null
    try {
      const response = await api.get<ApiResponse<CampEdition[]>>(
        `/camps/editions/proposed?year=${year}`
      )
      if (response.data.success && response.data.data) {
        editions.value = response.data.data
      } else {
        editions.value = []
      }
    } catch (err: unknown) {
      error.value = (err as { response?: { data?: { error?: { message?: string } } } })
        ?.response?.data?.error?.message || 'Error al cargar propuestas'
      console.error('Failed to fetch proposed editions:', err)
      editions.value = []
    } finally {
      loading.value = false
    }
  }

  const getActiveEdition = async (): Promise<void> => {
    loading.value = true
    error.value = null
    try {
      const response = await api.get<ApiResponse<CampEdition>>('/camps/editions/active')
      if (response.data.success && response.data.data) {
        activeEdition.value = response.data.data
      } else {
        activeEdition.value = null
      }
    } catch (err: unknown) {
      error.value = (err as { response?: { data?: { error?: { message?: string } } } })
        ?.response?.data?.error?.message || 'Error al cargar edición activa'
      console.error('Failed to fetch active edition:', err)
      activeEdition.value = null
    } finally {
      loading.value = false
    }
  }

  const getEditionById = async (id: string): Promise<CampEdition | null> => {
    loading.value = true
    error.value = null
    try {
      const response = await api.get<ApiResponse<CampEdition>>(`/camps/editions/${id}`)
      if (response.data.success && response.data.data) {
        return response.data.data
      }
      return null
    } catch (err: unknown) {
      error.value = (err as { response?: { data?: { error?: { message?: string } } } })
        ?.response?.data?.error?.message || 'Error al cargar edición'
      console.error('Failed to fetch edition:', err)
      return null
    } finally {
      loading.value = false
    }
  }

  const createEdition = async (
    request: CreateCampEditionRequest
  ): Promise<CampEdition | null> => {
    loading.value = true
    error.value = null
    try {
      const response = await api.post<ApiResponse<CampEdition>>('/camps/editions', request)
      if (response.data.success && response.data.data) {
        editions.value.push(response.data.data)
        return response.data.data
      }
      return null
    } catch (err: unknown) {
      error.value = (err as { response?: { data?: { error?: { message?: string } } } })
        ?.response?.data?.error?.message || 'Error al crear edición'
      console.error('Failed to create edition:', err)
      return null
    } finally {
      loading.value = false
    }
  }

  const proposeEdition = async (
    request: ProposeCampEditionRequest
  ): Promise<CampEdition | null> => {
    loading.value = true
    error.value = null
    try {
      const response = await api.post<ApiResponse<CampEdition>>(
        '/camps/editions/propose',
        request
      )
      if (response.data.success && response.data.data) {
        editions.value.push(response.data.data)
        return response.data.data
      }
      return null
    } catch (err: unknown) {
      error.value = (err as { response?: { data?: { error?: { message?: string } } } })
        ?.response?.data?.error?.message || 'Error al proponer campamento'
      console.error('Failed to propose edition:', err)
      return null
    } finally {
      loading.value = false
    }
  }

  const promoteEdition = async (id: string): Promise<CampEdition | null> => {
    loading.value = true
    error.value = null
    try {
      const response = await api.post<ApiResponse<CampEdition>>(`/camps/editions/${id}/promote`)
      if (response.data.success && response.data.data) {
        const index = editions.value.findIndex((e) => e.id === id)
        if (index !== -1) {
          editions.value[index] = response.data.data
        }
        return response.data.data
      }
      return null
    } catch (err: unknown) {
      error.value = (err as { response?: { data?: { error?: { message?: string } } } })
        ?.response?.data?.error?.message || 'Error al promover propuesta'
      console.error('Failed to promote edition:', err)
      return null
    } finally {
      loading.value = false
    }
  }

  const rejectEdition = async (id: string, reason: string): Promise<boolean> => {
    loading.value = true
    error.value = null
    try {
      await api.delete(`/camps/editions/${id}/reject`, {
        data: { reason }
      })
      editions.value = editions.value.filter((e) => e.id !== id)
      return true
    } catch (err: unknown) {
      error.value = (err as { response?: { data?: { error?: { message?: string } } } })
        ?.response?.data?.error?.message || 'Error al rechazar propuesta'
      console.error('Failed to reject edition:', err)
      return false
    } finally {
      loading.value = false
    }
  }

  const updateEdition = async (
    id: string,
    request: Partial<CreateCampEditionRequest>
  ): Promise<CampEdition | null> => {
    loading.value = true
    error.value = null
    try {
      const response = await api.put<ApiResponse<CampEdition>>(`/camps/editions/${id}`, request)
      if (response.data.success && response.data.data) {
        const index = editions.value.findIndex((e) => e.id === id)
        if (index !== -1) {
          editions.value[index] = response.data.data
        }
        return response.data.data
      }
      return null
    } catch (err: unknown) {
      error.value = (err as { response?: { data?: { error?: { message?: string } } } })
        ?.response?.data?.error?.message || 'Error al actualizar edición'
      console.error('Failed to update edition:', err)
      return null
    } finally {
      loading.value = false
    }
  }

  const changeStatus = async (
    id: string,
    newStatus: CampEditionStatus
  ): Promise<CampEdition | null> => {
    loading.value = true
    error.value = null
    try {
      const response = await api.post<ApiResponse<CampEdition>>(`/camps/editions/${id}/status`, {
        newStatus
      })
      if (response.data.success && response.data.data) {
        const index = editions.value.findIndex((e) => e.id === id)
        if (index !== -1) {
          editions.value[index] = response.data.data
        }
        if (activeEdition.value?.id === id) {
          activeEdition.value = response.data.data
        }
        return response.data.data
      }
      return null
    } catch (err: unknown) {
      error.value = (err as { response?: { data?: { error?: { message?: string } } } })
        ?.response?.data?.error?.message || 'Error al cambiar estado'
      console.error('Failed to change status:', err)
      return null
    } finally {
      loading.value = false
    }
  }

  const fetchCurrentCampEdition = async (): Promise<void> => {
    loading.value = true
    error.value = null
    try {
      const response = await api.get<ApiResponse<CampEdition>>('/camps/current')
      if (response.data.success && response.data.data) {
        currentCampEdition.value = response.data.data
      } else {
        currentCampEdition.value = null
      }
    } catch (err: unknown) {
      const apiErr = err as { response?: { status?: number; data?: { error?: { message?: string } } } }
      if (apiErr?.response?.status === 404) {
        currentCampEdition.value = null
        error.value = null
      } else {
        error.value = apiErr?.response?.data?.error?.message || 'Error al cargar campamento actual'
        console.error('Failed to fetch current camp edition:', err)
      }
    } finally {
      loading.value = false
    }
  }

  const deleteEdition = async (id: string): Promise<boolean> => {
    loading.value = true
    error.value = null
    try {
      await api.delete(`/camps/editions/${id}`)
      editions.value = editions.value.filter((e) => e.id !== id)
      if (activeEdition.value?.id === id) {
        activeEdition.value = null
      }
      return true
    } catch (err: unknown) {
      error.value = (err as { response?: { data?: { error?: { message?: string } } } })
        ?.response?.data?.error?.message || 'Error al eliminar edición'
      console.error('Failed to delete edition:', err)
      return false
    } finally {
      loading.value = false
    }
  }

  return {
    editions,
    activeEdition,
    currentCampEdition,
    loading,
    error,
    fetchProposedEditions,
    getActiveEdition,
    getEditionById,
    createEdition,
    proposeEdition,
    promoteEdition,
    rejectEdition,
    updateEdition,
    changeStatus,
    deleteEdition,
    fetchCurrentCampEdition
  }
}
