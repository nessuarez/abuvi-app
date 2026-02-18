import { ref } from 'vue'
import type { Ref } from 'vue'
import { api } from '@/utils/api'
import type { ApiResponse } from '@/types/api'
import type { GuestResponse, CreateGuestRequest, UpdateGuestRequest } from '@/types/guest'

export function useGuests() {
  const guests: Ref<GuestResponse[]> = ref([])
  const loading = ref(false)
  const error = ref<string | null>(null)

  /**
   * List all active guests for a family unit.
   */
  const listGuests = async (familyUnitId: string): Promise<GuestResponse[]> => {
    loading.value = true
    error.value = null
    try {
      const response = await api.get<ApiResponse<GuestResponse[]>>(
        `/family-units/${familyUnitId}/guests`,
      )
      guests.value = response.data.data
      return response.data.data
    } catch (err: any) {
      error.value = err.response?.data?.error?.message || 'Error al obtener los invitados'
      return []
    } finally {
      loading.value = false
    }
  }

  /**
   * Create a new guest for a family unit.
   */
  const createGuest = async (
    familyUnitId: string,
    request: CreateGuestRequest,
  ): Promise<GuestResponse | null> => {
    loading.value = true
    error.value = null
    try {
      const response = await api.post<ApiResponse<GuestResponse>>(
        `/family-units/${familyUnitId}/guests`,
        request,
      )
      guests.value.push(response.data.data)
      return response.data.data
    } catch (err: any) {
      error.value = err.response?.data?.error?.message || 'Error al crear el invitado'
      return null
    } finally {
      loading.value = false
    }
  }

  /**
   * Update an existing guest.
   */
  const updateGuest = async (
    familyUnitId: string,
    guestId: string,
    request: UpdateGuestRequest,
  ): Promise<GuestResponse | null> => {
    loading.value = true
    error.value = null
    try {
      const response = await api.put<ApiResponse<GuestResponse>>(
        `/family-units/${familyUnitId}/guests/${guestId}`,
        request,
      )
      const idx = guests.value.findIndex((g) => g.id === guestId)
      if (idx !== -1) guests.value[idx] = response.data.data
      return response.data.data
    } catch (err: any) {
      error.value = err.response?.data?.error?.message || 'Error al actualizar el invitado'
      return null
    } finally {
      loading.value = false
    }
  }

  /**
   * Soft-delete a guest (sets isActive=false on backend).
   */
  const deleteGuest = async (familyUnitId: string, guestId: string): Promise<boolean> => {
    loading.value = true
    error.value = null
    try {
      await api.delete(`/family-units/${familyUnitId}/guests/${guestId}`)
      guests.value = guests.value.filter((g) => g.id !== guestId)
      return true
    } catch (err: any) {
      error.value = err.response?.data?.error?.message || 'Error al eliminar el invitado'
      return false
    } finally {
      loading.value = false
    }
  }

  return {
    guests,
    loading,
    error,
    listGuests,
    createGuest,
    updateGuest,
    deleteGuest,
  }
}
