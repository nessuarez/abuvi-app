import { ref } from 'vue'
import { api } from '@/utils/api'
import type { ApiResponse } from '@/types/api'
import type {
  RegistrationResponse,
  RegistrationListItem,
  CreateRegistrationRequest,
  UpdateRegistrationMembersRequest,
  UpdateRegistrationExtrasRequest,
  UpdateAccommodationPreferencesRequest,
  AccommodationPreferenceResponse
} from '@/types/registration'

function toListItem(r: RegistrationResponse): RegistrationListItem {
  return {
    id: r.id,
    familyUnit: r.familyUnit,
    campEdition: r.campEdition,
    status: r.status,
    totalAmount: r.pricing.totalAmount,
    amountPaid: r.amountPaid,
    amountRemaining: r.amountRemaining,
    createdAt: r.createdAt
  }
}

export function useRegistrations() {
  const registrations = ref<RegistrationListItem[]>([])
  const registration = ref<RegistrationResponse | null>(null)
  const loading = ref(false)
  const error = ref<string | null>(null)

  const fetchMyRegistrations = async (): Promise<void> => {
    loading.value = true
    error.value = null
    try {
      const response = await api.get<ApiResponse<RegistrationListItem[]>>('/registrations')
      registrations.value = response.data.success ? (response.data.data ?? []) : []
    } catch (err: unknown) {
      error.value = (err as { response?: { data?: { error?: { message?: string } } } })
        ?.response?.data?.error?.message || 'Error al cargar inscripciones'
      console.error('Failed to fetch registrations:', err)
      registrations.value = []
    } finally {
      loading.value = false
    }
  }

  const getRegistrationById = async (id: string): Promise<RegistrationResponse | null> => {
    loading.value = true
    error.value = null
    try {
      const response = await api.get<ApiResponse<RegistrationResponse>>(`/registrations/${id}`)
      const data = response.data.success ? (response.data.data ?? null) : null
      registration.value = data
      return data
    } catch (err: unknown) {
      error.value = (err as { response?: { data?: { error?: { message?: string } } } })
        ?.response?.data?.error?.message || 'Error al cargar la inscripción'
      console.error('Failed to fetch registration:', err)
      registration.value = null
      return null
    } finally {
      loading.value = false
    }
  }

  const createRegistration = async (
    request: CreateRegistrationRequest
  ): Promise<RegistrationResponse | null> => {
    loading.value = true
    error.value = null
    try {
      const response = await api.post<ApiResponse<RegistrationResponse>>('/registrations', request)
      if (response.data.success && response.data.data) {
        registrations.value.push(toListItem(response.data.data))
        return response.data.data
      }
      return null
    } catch (err: unknown) {
      error.value = (err as { response?: { data?: { error?: { message?: string } } } })
        ?.response?.data?.error?.message || 'Error al crear la inscripción'
      console.error('Failed to create registration:', err)
      return null
    } finally {
      loading.value = false
    }
  }

  const updateMembers = async (
    id: string,
    request: UpdateRegistrationMembersRequest
  ): Promise<RegistrationResponse | null> => {
    loading.value = true
    error.value = null
    try {
      const response = await api.put<ApiResponse<RegistrationResponse>>(
        `/registrations/${id}/members`,
        request
      )
      if (response.data.success && response.data.data) {
        const updated = response.data.data
        const index = registrations.value.findIndex((r) => r.id === id)
        if (index !== -1) registrations.value[index] = toListItem(updated)
        if (registration.value?.id === id) registration.value = updated
        return updated
      }
      return null
    } catch (err: unknown) {
      error.value = (err as { response?: { data?: { error?: { message?: string } } } })
        ?.response?.data?.error?.message || 'Error al actualizar participantes'
      console.error('Failed to update members:', err)
      return null
    } finally {
      loading.value = false
    }
  }

  const setExtras = async (
    id: string,
    request: UpdateRegistrationExtrasRequest
  ): Promise<RegistrationResponse | null> => {
    loading.value = true
    error.value = null
    try {
      const response = await api.post<ApiResponse<RegistrationResponse>>(
        `/registrations/${id}/extras`,
        request
      )
      if (response.data.success && response.data.data) {
        const updated = response.data.data
        const index = registrations.value.findIndex((r) => r.id === id)
        if (index !== -1) registrations.value[index] = toListItem(updated)
        if (registration.value?.id === id) registration.value = updated
        return updated
      }
      return null
    } catch (err: unknown) {
      error.value = (err as { response?: { data?: { error?: { message?: string } } } })
        ?.response?.data?.error?.message || 'Error al guardar los extras'
      console.error('Failed to set extras:', err)
      return null
    } finally {
      loading.value = false
    }
  }

  const setAccommodationPreferences = async (
    id: string,
    request: UpdateAccommodationPreferencesRequest
  ): Promise<AccommodationPreferenceResponse[] | null> => {
    loading.value = true
    error.value = null
    try {
      const response = await api.put<ApiResponse<AccommodationPreferenceResponse[]>>(
        `/registrations/${id}/accommodation-preferences`,
        request
      )
      if (response.data.success && response.data.data) {
        return response.data.data
      }
      return null
    } catch (err: unknown) {
      error.value = (err as { response?: { data?: { error?: { message?: string } } } })
        ?.response?.data?.error?.message || 'Error al guardar preferencias de alojamiento'
      console.error('Failed to set accommodation preferences:', err)
      return null
    } finally {
      loading.value = false
    }
  }

  const getAccommodationPreferences = async (
    id: string
  ): Promise<AccommodationPreferenceResponse[] | null> => {
    try {
      const response = await api.get<ApiResponse<AccommodationPreferenceResponse[]>>(
        `/registrations/${id}/accommodation-preferences`
      )
      if (response.data.success && response.data.data) {
        return response.data.data
      }
      return null
    } catch (err: unknown) {
      console.error('Failed to fetch accommodation preferences:', err)
      return null
    }
  }

  const cancelRegistration = async (id: string): Promise<boolean> => {
    loading.value = true
    error.value = null
    try {
      await api.post(`/registrations/${id}/cancel`)
      if (registration.value?.id === id) {
        registration.value = { ...registration.value, status: 'Cancelled' }
      }
      const index = registrations.value.findIndex((r) => r.id === id)
      if (index !== -1) {
        registrations.value[index] = { ...registrations.value[index], status: 'Cancelled' }
      }
      return true
    } catch (err: unknown) {
      error.value = (err as { response?: { data?: { error?: { message?: string } } } })
        ?.response?.data?.error?.message || 'Error al cancelar la inscripción'
      console.error('Failed to cancel registration:', err)
      return false
    } finally {
      loading.value = false
    }
  }

  const deleteRegistration = async (id: string): Promise<boolean> => {
    loading.value = true
    error.value = null
    try {
      await api.delete(`/registrations/${id}`)
      if (registration.value?.id === id) {
        registration.value = null
      }
      registrations.value = registrations.value.filter((r) => r.id !== id)
      return true
    } catch (err: unknown) {
      error.value = (err as { response?: { data?: { error?: { message?: string } } } })
        ?.response?.data?.error?.message || 'Could not delete the registration.'
      console.error('Failed to delete registration:', err)
      return false
    } finally {
      loading.value = false
    }
  }

  return {
    registrations,
    registration,
    loading,
    error,
    fetchMyRegistrations,
    getRegistrationById,
    createRegistration,
    updateMembers,
    setExtras,
    setAccommodationPreferences,
    getAccommodationPreferences,
    cancelRegistration,
    deleteRegistration
  }
}
