import { ref, type Ref } from 'vue'
import { api } from '@/utils/api'
import type {
  AccommodationZoneResponse,
  CreateAccommodationZoneRequest,
  UpdateAccommodationZoneRequest
} from '@/types/accommodation-assignment'
import type { ApiResponse } from '@/types/api'

export function useAccommodationZones(campEditionId: Ref<string>) {
  const zones = ref<AccommodationZoneResponse[]>([])
  const loading = ref(false)
  const error = ref<string | null>(null)

  const extractError = (err: unknown): string =>
    (err as { response?: { data?: { error?: { message?: string } } } })?.response?.data?.error
      ?.message ?? 'Ha ocurrido un error inesperado'

  async function loadZones(): Promise<void> {
    loading.value = true
    error.value = null
    try {
      const res = await api.get<ApiResponse<AccommodationZoneResponse[]>>(
        `/camps/editions/${campEditionId.value}/accommodation-zones`
      )
      if (res.data.success && res.data.data) {
        zones.value = res.data.data
      }
    } catch (err: unknown) {
      error.value = extractError(err)
    } finally {
      loading.value = false
    }
  }

  async function createZone(req: CreateAccommodationZoneRequest): Promise<AccommodationZoneResponse | null> {
    loading.value = true
    error.value = null
    try {
      const res = await api.post<ApiResponse<AccommodationZoneResponse>>(
        `/camps/editions/${campEditionId.value}/accommodation-zones`,
        req
      )
      if (res.data.success && res.data.data) {
        zones.value.push(res.data.data)
        return res.data.data
      }
      return null
    } catch (err: unknown) {
      error.value = extractError(err)
      return null
    } finally {
      loading.value = false
    }
  }

  async function updateZone(
    zoneId: string,
    req: UpdateAccommodationZoneRequest
  ): Promise<AccommodationZoneResponse | null> {
    loading.value = true
    error.value = null
    try {
      const res = await api.put<ApiResponse<AccommodationZoneResponse>>(
        `/camps/editions/${campEditionId.value}/accommodation-zones/${zoneId}`,
        req
      )
      if (res.data.success && res.data.data) {
        const idx = zones.value.findIndex((z) => z.id === zoneId)
        if (idx !== -1) zones.value[idx] = res.data.data
        return res.data.data
      }
      return null
    } catch (err: unknown) {
      error.value = extractError(err)
      return null
    } finally {
      loading.value = false
    }
  }

  async function deleteZone(zoneId: string): Promise<boolean> {
    loading.value = true
    error.value = null
    try {
      await api.delete(`/camps/editions/${campEditionId.value}/accommodation-zones/${zoneId}`)
      zones.value = zones.value.filter((z) => z.id !== zoneId)
      return true
    } catch (err: unknown) {
      error.value = extractError(err)
      return false
    } finally {
      loading.value = false
    }
  }

  async function attachAccommodations(
    zoneId: string,
    accommodationIds: string[]
  ): Promise<boolean> {
    loading.value = true
    error.value = null
    try {
      const res = await api.patch<ApiResponse<AccommodationZoneResponse>>(
        `/camps/editions/${campEditionId.value}/accommodation-zones/${zoneId}/accommodations`,
        { accommodationIds }
      )
      if (res.data.success && res.data.data) {
        const idx = zones.value.findIndex((z) => z.id === zoneId)
        if (idx !== -1) zones.value[idx] = res.data.data
        return true
      }
      return false
    } catch (err: unknown) {
      error.value = extractError(err)
      return false
    } finally {
      loading.value = false
    }
  }

  return {
    zones,
    loading,
    error,
    loadZones,
    createZone,
    updateZone,
    deleteZone,
    attachAccommodations
  }
}
