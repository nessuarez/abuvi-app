import { ref } from 'vue'
import { api } from '@/utils/api'
import type { ApiResponse } from '@/types/api'

export interface PlaceAutocomplete {
  placeId: string
  description: string
  mainText: string
  secondaryText: string
}

export interface PlaceDetails {
  placeId: string
  name: string
  formattedAddress: string
  latitude: number
  longitude: number
  types: string[]
}

export function useGooglePlaces() {
  const loading = ref(false)
  const error = ref<string | null>(null)

  const searchPlaces = async (input: string): Promise<PlaceAutocomplete[]> => {
    if (!input || input.length < 3) {
      return []
    }

    loading.value = true
    error.value = null

    try {
      const response = await api.post<ApiResponse<PlaceAutocomplete[]>>(
        '/places/autocomplete',
        { input }
      )

      if (response.data.success && response.data.data) {
        return response.data.data
      }

      error.value = response.data.error?.message || 'Error al buscar lugares'
      return []
    } catch (err: any) {
      error.value = err.response?.data?.error?.message || 'Error al buscar lugares'
      return []
    } finally {
      loading.value = false
    }
  }

  const getPlaceDetails = async (placeId: string): Promise<PlaceDetails | null> => {
    loading.value = true
    error.value = null

    try {
      const response = await api.post<ApiResponse<PlaceDetails>>(
        '/places/details',
        { placeId }
      )

      if (response.data.success && response.data.data) {
        return response.data.data
      }

      error.value = response.data.error?.message || 'Error al obtener detalles del lugar'
      return null
    } catch (err: any) {
      error.value = err.response?.data?.error?.message || 'Error al obtener detalles del lugar'
      return null
    } finally {
      loading.value = false
    }
  }

  return {
    loading,
    error,
    searchPlaces,
    getPlaceDetails
  }
}
