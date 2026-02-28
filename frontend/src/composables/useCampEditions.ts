import { ref } from 'vue'
import { api } from '@/utils/api'
import type {
  CampEdition,
  CreateCampEditionRequest,
  ProposeCampEditionRequest,
  CampEditionStatus,
  UpdateCampEditionRequest,
  ChangeEditionStatusRequest,
  ActiveCampEditionResponse,
  CampEditionFilters,
  CurrentCampEditionResponse
} from '@/types/camp-edition'
import type { ApiResponse } from '@/types/api'

export function useCampEditions() {
  const editions = ref<CampEdition[]>([])
  const allEditions = ref<CampEdition[]>([])
  const activeEdition = ref<ActiveCampEditionResponse | null>(null)
  const currentCampEdition = ref<CurrentCampEditionResponse | null>(null)
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

  const getActiveEdition = async (year?: number): Promise<void> => {
    loading.value = true
    error.value = null
    try {
      const url = year
        ? `/camps/editions/active?year=${year}`
        : '/camps/editions/active'
      const response = await api.get<ApiResponse<ActiveCampEditionResponse>>(url)
      activeEdition.value = response.data.success ? (response.data.data ?? null) : null
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

  const fetchAllEditions = async (filters?: CampEditionFilters): Promise<void> => {
    loading.value = true
    error.value = null
    try {
      const params = new URLSearchParams()
      if (filters?.year) params.append('year', String(filters.year))
      if (filters?.status) params.append('status', filters.status)
      if (filters?.campId) params.append('campId', filters.campId)
      const query = params.toString() ? `?${params.toString()}` : ''
      const response = await api.get<ApiResponse<CampEdition[]>>(`/camps/editions${query}`)
      allEditions.value = response.data.success ? (response.data.data ?? []) : []
    } catch (err: unknown) {
      error.value = (err as { response?: { data?: { error?: { message?: string } } } })
        ?.response?.data?.error?.message || 'Error al cargar ediciones'
      console.error('Failed to fetch all editions:', err)
      allEditions.value = []
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
    request: UpdateCampEditionRequest
  ): Promise<CampEdition | null> => {
    loading.value = true
    error.value = null
    try {
      const response = await api.put<ApiResponse<CampEdition>>(`/camps/editions/${id}`, request)
      if (response.data.success && response.data.data) {
        const updatedEdition = response.data.data
        const index = editions.value.findIndex((e) => e.id === id)
        if (index !== -1) {
          editions.value[index] = updatedEdition
        }
        const allIndex = allEditions.value.findIndex((e) => e.id === id)
        if (allIndex !== -1) {
          allEditions.value[allIndex] = updatedEdition
        }
        return updatedEdition
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
    newStatus: CampEditionStatus,
    force?: boolean
  ): Promise<CampEdition | null> => {
    loading.value = true
    error.value = null
    try {
      const body: ChangeEditionStatusRequest = { status: newStatus }
      if (force) body.force = true
      const response = await api.patch<ApiResponse<CampEdition>>(
        `/camps/editions/${id}/status`,
        body
      )
      if (response.data.success && response.data.data) {
        const updatedEdition = response.data.data
        const index = editions.value.findIndex((e) => e.id === id)
        if (index !== -1) {
          editions.value[index] = updatedEdition
        }
        const allIndex = allEditions.value.findIndex((e) => e.id === id)
        if (allIndex !== -1) {
          allEditions.value[allIndex] = updatedEdition
        }
        return updatedEdition
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
      const response = await api.get<ApiResponse<CurrentCampEditionResponse>>('/camps/current')
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
    allEditions,
    activeEdition,
    currentCampEdition,
    loading,
    error,
    fetchProposedEditions,
    getActiveEdition,
    getEditionById,
    fetchAllEditions,
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
