import { ref } from 'vue'
import { api } from '@/utils/api'
import type { ApiResponse } from '@/types/api'
import type {
  AccommodationFeature,
  CreateAccommodationFeatureRequest,
  UpdateAccommodationFeatureRequest,
} from '@/types/accommodation-feature'

type ApiErrorShape = { response?: { data?: { error?: { message?: string } }; status?: number } }

export function useAccommodationFeatures() {
  const features = ref<AccommodationFeature[]>([])
  const loading = ref(false)
  const error = ref<string | null>(null)
  const saving = ref(false)
  const saveError = ref<string | null>(null)

  const extractError = (err: unknown): string =>
    (err as ApiErrorShape)?.response?.data?.error?.message ?? 'Ha ocurrido un error inesperado'

  async function fetchFeatures(activeOnly?: boolean): Promise<void> {
    loading.value = true
    error.value = null
    try {
      const params = activeOnly !== undefined ? { activeOnly } : {}
      const res = await api.get<ApiResponse<AccommodationFeature[]>>('/accommodation-features', {
        params,
      })
      features.value = res.data.data ?? []
    } catch (err) {
      error.value = extractError(err)
    } finally {
      loading.value = false
    }
  }

  async function createFeature(
    request: CreateAccommodationFeatureRequest,
  ): Promise<AccommodationFeature | null> {
    saving.value = true
    saveError.value = null
    try {
      const res = await api.post<ApiResponse<AccommodationFeature>>(
        '/accommodation-features',
        request,
      )
      const created = res.data.data!
      features.value.push(created)
      return created
    } catch (err) {
      saveError.value = extractError(err)
      return null
    } finally {
      saving.value = false
    }
  }

  async function updateFeature(
    id: string,
    request: UpdateAccommodationFeatureRequest,
  ): Promise<AccommodationFeature | null> {
    saving.value = true
    saveError.value = null
    try {
      const res = await api.put<ApiResponse<AccommodationFeature>>(
        `/accommodation-features/${id}`,
        request,
      )
      const updated = res.data.data!
      const idx = features.value.findIndex((f) => f.id === id)
      if (idx !== -1) features.value[idx] = updated
      return updated
    } catch (err) {
      saveError.value = extractError(err)
      return null
    } finally {
      saving.value = false
    }
  }

  async function deleteFeature(id: string): Promise<boolean> {
    saveError.value = null
    try {
      await api.delete(`/accommodation-features/${id}`)
      features.value = features.value.filter((f) => f.id !== id)
      return true
    } catch (err) {
      const status = (err as ApiErrorShape)?.response?.status
      if (status === 409) {
        saveError.value = 'Esta característica está en uso. Desactívala en lugar de eliminarla.'
      } else {
        saveError.value = extractError(err)
      }
      return false
    }
  }

  return {
    features,
    loading,
    error,
    saving,
    saveError,
    fetchFeatures,
    createFeature,
    updateFeature,
    deleteFeature,
  }
}
