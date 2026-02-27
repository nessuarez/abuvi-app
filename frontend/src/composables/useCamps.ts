import { ref } from 'vue'
import { api } from '@/utils/api'
import type {
  Camp,
  CampDetailResponse,
  CreateCampRequest,
  UpdateCampRequest,
  CampObservation,
  CampAuditLogEntry,
  AddCampObservationRequest
} from '@/types/camp'
import type { ApiResponse } from '@/types/api'

export function useCamps() {
  const camps = ref<Camp[]>([])
  const loading = ref(false)
  const error = ref<string | null>(null)

  // Observations state
  const campObservations = ref<CampObservation[]>([])
  const observationsLoading = ref(false)
  const observationsError = ref<string | null>(null)

  // Audit log state
  const campAuditLog = ref<CampAuditLogEntry[]>([])
  const auditLogLoading = ref(false)
  const auditLogError = ref<string | null>(null)

  const extractError = (err: unknown, fallback: string): string => {
    return (
      (err as { response?: { data?: { error?: { message?: string } } } })?.response?.data?.error
        ?.message || fallback
    )
  }

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
      error.value = extractError(err, 'Error al cargar campamentos')
      console.error('Failed to fetch camps:', err)
      camps.value = []
    } finally {
      loading.value = false
    }
  }

  const getCampById = async (id: string): Promise<CampDetailResponse | null> => {
    loading.value = true
    error.value = null
    try {
      const response = await api.get<ApiResponse<CampDetailResponse>>(`/camps/${id}`)
      if (response.data.success && response.data.data) {
        return response.data.data
      }
      return null
    } catch (err: unknown) {
      error.value = extractError(err, 'Error al cargar campamento')
      console.error('Failed to fetch camp:', err)
      return null
    } finally {
      loading.value = false
    }
  }

  const createCamp = async (request: CreateCampRequest): Promise<CampDetailResponse | null> => {
    loading.value = true
    error.value = null
    try {
      const response = await api.post<ApiResponse<CampDetailResponse>>('/camps', request)
      if (response.data.success && response.data.data) {
        camps.value = [...(camps.value || []), response.data.data]
        return response.data.data
      }
      return null
    } catch (err: unknown) {
      error.value = extractError(err, 'Error al crear campamento')
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
      error.value = extractError(err, 'Error al actualizar campamento')
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
      error.value = extractError(err, 'Error al eliminar campamento')
      console.error('Failed to delete camp:', err)
      return false
    } finally {
      loading.value = false
    }
  }

  const fetchCampObservations = async (campId: string) => {
    observationsLoading.value = true
    observationsError.value = null
    try {
      const res = await api.get<ApiResponse<CampObservation[]>>(
        `/camps/${campId}/observations`
      )
      if (res.data.success && res.data.data) {
        campObservations.value = res.data.data
      }
    } catch (err: unknown) {
      observationsError.value = extractError(err, 'Error al cargar las observaciones')
    } finally {
      observationsLoading.value = false
    }
  }

  const addCampObservation = async (
    campId: string,
    request: AddCampObservationRequest
  ): Promise<CampObservation | null> => {
    observationsLoading.value = true
    observationsError.value = null
    try {
      const res = await api.post<ApiResponse<CampObservation>>(
        `/camps/${campId}/observations`,
        request
      )
      if (res.data.success && res.data.data) {
        campObservations.value.unshift(res.data.data)
        return res.data.data
      }
      return null
    } catch (err: unknown) {
      observationsError.value = extractError(err, 'Error al añadir la observación')
      return null
    } finally {
      observationsLoading.value = false
    }
  }

  const fetchCampAuditLog = async (campId: string) => {
    auditLogLoading.value = true
    auditLogError.value = null
    try {
      const res = await api.get<ApiResponse<CampAuditLogEntry[]>>(
        `/camps/${campId}/audit-log`
      )
      if (res.data.success && res.data.data) {
        campAuditLog.value = res.data.data
      }
    } catch (err: unknown) {
      auditLogError.value = extractError(err, 'Error al cargar el registro de auditoría')
    } finally {
      auditLogLoading.value = false
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
    deleteCamp,
    // Observations
    campObservations,
    observationsLoading,
    observationsError,
    fetchCampObservations,
    addCampObservation,
    // Audit log
    campAuditLog,
    auditLogLoading,
    auditLogError,
    fetchCampAuditLog
  }
}
