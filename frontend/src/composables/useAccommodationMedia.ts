import { ref } from 'vue'
import { api } from '@/utils/api'
import type { ApiResponse } from '@/types/api'
import type {
  AccommodationMediaItem,
  AddAccommodationMediaRequest,
  AccommodationTypeMediaItem,
} from '@/types/accommodation-media'

type ApiErrorShape = { response?: { data?: { error?: { message?: string } } } }

export function useAccommodationMedia() {
  const items = ref<AccommodationMediaItem[]>([])
  const typeItems = ref<AccommodationTypeMediaItem[]>([])
  const loading = ref(false)
  const error = ref<string | null>(null)

  // ── Zone media ────────────────────────────────────────────────────────────

  async function fetchZoneMedia(editionId: string, zoneId: string): Promise<void> {
    loading.value = true
    error.value = null
    try {
      const res = await api.get<ApiResponse<AccommodationMediaItem[]>>(
        `/camps/editions/${editionId}/accommodation-zones/${zoneId}/media`
      )
      if (res.data.success && res.data.data) items.value = res.data.data
      else error.value = res.data.error?.message ?? 'Error al cargar los archivos'
    } catch (err: unknown) {
      error.value =
        (err as ApiErrorShape)?.response?.data?.error?.message ?? 'Error al cargar los archivos'
    } finally {
      loading.value = false
    }
  }

  async function addZoneMedia(
    editionId: string,
    zoneId: string,
    request: AddAccommodationMediaRequest
  ): Promise<AccommodationMediaItem | null> {
    loading.value = true
    error.value = null
    try {
      const res = await api.post<ApiResponse<AccommodationMediaItem>>(
        `/camps/editions/${editionId}/accommodation-zones/${zoneId}/media`,
        request
      )
      if (res.data.success && res.data.data) {
        items.value.push(res.data.data)
        return res.data.data
      }
      error.value = res.data.error?.message ?? 'Error al añadir el archivo'
      return null
    } catch (err: unknown) {
      error.value =
        (err as ApiErrorShape)?.response?.data?.error?.message ?? 'Error al añadir el archivo'
      return null
    } finally {
      loading.value = false
    }
  }

  async function deleteZoneMedia(
    editionId: string,
    zoneId: string,
    mediaId: string
  ): Promise<boolean> {
    loading.value = true
    error.value = null
    try {
      await api.delete(
        `/camps/editions/${editionId}/accommodation-zones/${zoneId}/media/${mediaId}`
      )
      items.value = items.value.filter((i) => i.id !== mediaId)
      return true
    } catch (err: unknown) {
      error.value =
        (err as ApiErrorShape)?.response?.data?.error?.message ?? 'Error al eliminar el archivo'
      return false
    } finally {
      loading.value = false
    }
  }

  async function setZonePrimary(
    editionId: string,
    zoneId: string,
    mediaId: string
  ): Promise<boolean> {
    loading.value = true
    error.value = null
    try {
      await api.patch(
        `/camps/editions/${editionId}/accommodation-zones/${zoneId}/media/${mediaId}/primary`
      )
      items.value = items.value.map((i) => ({ ...i, isPrimary: i.id === mediaId }))
      return true
    } catch (err: unknown) {
      error.value =
        (err as ApiErrorShape)?.response?.data?.error?.message ??
        'Error al establecer como principal'
      return false
    } finally {
      loading.value = false
    }
  }

  // ── Accommodation media ───────────────────────────────────────────────────

  async function fetchAccommodationMedia(
    editionId: string,
    accommodationId: string
  ): Promise<void> {
    loading.value = true
    error.value = null
    try {
      const res = await api.get<ApiResponse<AccommodationMediaItem[]>>(
        `/camps/editions/${editionId}/accommodations/${accommodationId}/media`
      )
      if (res.data.success && res.data.data) items.value = res.data.data
      else error.value = res.data.error?.message ?? 'Error al cargar los archivos'
    } catch (err: unknown) {
      error.value =
        (err as ApiErrorShape)?.response?.data?.error?.message ?? 'Error al cargar los archivos'
    } finally {
      loading.value = false
    }
  }

  async function addAccommodationMedia(
    editionId: string,
    accommodationId: string,
    request: AddAccommodationMediaRequest
  ): Promise<AccommodationMediaItem | null> {
    loading.value = true
    error.value = null
    try {
      const res = await api.post<ApiResponse<AccommodationMediaItem>>(
        `/camps/editions/${editionId}/accommodations/${accommodationId}/media`,
        request
      )
      if (res.data.success && res.data.data) {
        items.value.push(res.data.data)
        return res.data.data
      }
      error.value = res.data.error?.message ?? 'Error al añadir el archivo'
      return null
    } catch (err: unknown) {
      error.value =
        (err as ApiErrorShape)?.response?.data?.error?.message ?? 'Error al añadir el archivo'
      return null
    } finally {
      loading.value = false
    }
  }

  async function deleteAccommodationMedia(
    editionId: string,
    accommodationId: string,
    mediaId: string
  ): Promise<boolean> {
    loading.value = true
    error.value = null
    try {
      await api.delete(
        `/camps/editions/${editionId}/accommodations/${accommodationId}/media/${mediaId}`
      )
      items.value = items.value.filter((i) => i.id !== mediaId)
      return true
    } catch (err: unknown) {
      error.value =
        (err as ApiErrorShape)?.response?.data?.error?.message ?? 'Error al eliminar el archivo'
      return false
    } finally {
      loading.value = false
    }
  }

  async function setAccommodationPrimary(
    editionId: string,
    accommodationId: string,
    mediaId: string
  ): Promise<boolean> {
    loading.value = true
    error.value = null
    try {
      await api.patch(
        `/camps/editions/${editionId}/accommodations/${accommodationId}/media/${mediaId}/primary`
      )
      items.value = items.value.map((i) => ({ ...i, isPrimary: i.id === mediaId }))
      return true
    } catch (err: unknown) {
      error.value =
        (err as ApiErrorShape)?.response?.data?.error?.message ??
        'Error al establecer como principal'
      return false
    } finally {
      loading.value = false
    }
  }

  // ── Accommodation type default media ──────────────────────────────────────

  async function fetchTypeMedia(type?: string): Promise<void> {
    loading.value = true
    error.value = null
    try {
      const url = type ? `/accommodation-types/${type}/media` : '/accommodation-types/media'
      const res = await api.get<ApiResponse<AccommodationTypeMediaItem[]>>(url)
      if (res.data.success && res.data.data) typeItems.value = res.data.data
      else error.value = res.data.error?.message ?? 'Error al cargar los archivos'
    } catch (err: unknown) {
      error.value =
        (err as ApiErrorShape)?.response?.data?.error?.message ?? 'Error al cargar los archivos'
    } finally {
      loading.value = false
    }
  }

  async function addTypeMedia(
    type: string,
    request: AddAccommodationMediaRequest
  ): Promise<AccommodationTypeMediaItem | null> {
    loading.value = true
    error.value = null
    try {
      const res = await api.post<ApiResponse<AccommodationTypeMediaItem>>(
        `/accommodation-types/${type}/media`,
        request
      )
      if (res.data.success && res.data.data) {
        typeItems.value.push(res.data.data)
        return res.data.data
      }
      error.value = res.data.error?.message ?? 'Error al añadir el archivo'
      return null
    } catch (err: unknown) {
      error.value =
        (err as ApiErrorShape)?.response?.data?.error?.message ?? 'Error al añadir el archivo'
      return null
    } finally {
      loading.value = false
    }
  }

  async function deleteTypeMedia(mediaId: string): Promise<boolean> {
    loading.value = true
    error.value = null
    try {
      await api.delete(`/accommodation-types/media/${mediaId}`)
      typeItems.value = typeItems.value.filter((i) => i.id !== mediaId)
      return true
    } catch (err: unknown) {
      error.value =
        (err as ApiErrorShape)?.response?.data?.error?.message ?? 'Error al eliminar el archivo'
      return false
    } finally {
      loading.value = false
    }
  }

  async function setTypePrimary(mediaId: string): Promise<boolean> {
    loading.value = true
    error.value = null
    try {
      await api.patch(`/accommodation-types/media/${mediaId}/primary`)
      typeItems.value = typeItems.value.map((i) => ({ ...i, isPrimary: i.id === mediaId }))
      return true
    } catch (err: unknown) {
      error.value =
        (err as ApiErrorShape)?.response?.data?.error?.message ??
        'Error al establecer como principal'
      return false
    } finally {
      loading.value = false
    }
  }

  return {
    items,
    typeItems,
    loading,
    error,
    fetchZoneMedia,
    addZoneMedia,
    deleteZoneMedia,
    setZonePrimary,
    fetchAccommodationMedia,
    addAccommodationMedia,
    deleteAccommodationMedia,
    setAccommodationPrimary,
    fetchTypeMedia,
    addTypeMedia,
    deleteTypeMedia,
    setTypePrimary,
  }
}
