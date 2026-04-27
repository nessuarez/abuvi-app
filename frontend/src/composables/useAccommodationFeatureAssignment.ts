import { ref } from 'vue'
import { api } from '@/utils/api'
import type { ApiResponse } from '@/types/api'
import type {
  AccommodationFeature,
  SetFeatureAssignmentsRequest,
} from '@/types/accommodation-feature'

type ApiErrorShape = { response?: { data?: { error?: { message?: string } } } }

export function useAccommodationFeatureAssignment(editionId: string) {
  const saving = ref(false)
  const error = ref<string | null>(null)

  const extractError = (err: unknown): string =>
    (err as ApiErrorShape)?.response?.data?.error?.message ?? 'Ha ocurrido un error inesperado'

  async function getAccommodationFeatures(accommodationId: string): Promise<AccommodationFeature[]> {
    const res = await api.get<ApiResponse<AccommodationFeature[]>>(
      `/camps/editions/${editionId}/accommodations/${accommodationId}/features`,
    )
    return res.data.data ?? []
  }

  async function setAccommodationFeatures(
    accommodationId: string,
    featureIds: string[],
  ): Promise<AccommodationFeature[] | null> {
    saving.value = true
    error.value = null
    try {
      const body: SetFeatureAssignmentsRequest = { featureIds }
      const res = await api.put<ApiResponse<AccommodationFeature[]>>(
        `/camps/editions/${editionId}/accommodations/${accommodationId}/features`,
        body,
      )
      return res.data.data ?? []
    } catch (err) {
      error.value = extractError(err)
      return null
    } finally {
      saving.value = false
    }
  }

  async function getZoneFeatures(zoneId: string): Promise<AccommodationFeature[]> {
    const res = await api.get<ApiResponse<AccommodationFeature[]>>(
      `/camps/editions/${editionId}/accommodation-zones/${zoneId}/features`,
    )
    return res.data.data ?? []
  }

  async function setZoneFeatures(
    zoneId: string,
    featureIds: string[],
  ): Promise<AccommodationFeature[] | null> {
    saving.value = true
    error.value = null
    try {
      const body: SetFeatureAssignmentsRequest = { featureIds }
      const res = await api.put<ApiResponse<AccommodationFeature[]>>(
        `/camps/editions/${editionId}/accommodation-zones/${zoneId}/features`,
        body,
      )
      return res.data.data ?? []
    } catch (err) {
      error.value = extractError(err)
      return null
    } finally {
      saving.value = false
    }
  }

  return {
    saving,
    error,
    getAccommodationFeatures,
    setAccommodationFeatures,
    getZoneFeatures,
    setZoneFeatures,
  }
}
