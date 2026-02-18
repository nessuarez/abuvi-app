import { ref } from 'vue'
import { api } from '@/utils/api'
import type { ApiResponse } from '@/types/api'
import type {
  CampPhoto,
  AddCampPhotoRequest,
  UpdateCampPhotoRequest,
  ReorderCampPhotosRequest
} from '@/types/camp-photo'

type ApiErrorShape = { response?: { data?: { error?: { message?: string } } } }

export function useCampPhotos() {
  const photos = ref<CampPhoto[]>([])
  const loading = ref(false)
  const error = ref<string | null>(null)

  const addPhoto = async (campId: string, request: AddCampPhotoRequest): Promise<CampPhoto | null> => {
    loading.value = true
    error.value = null
    try {
      const response = await api.post<ApiResponse<CampPhoto>>(`/camps/${campId}/photos`, request)
      if (response.data.success && response.data.data) {
        photos.value.push(response.data.data)
        return response.data.data
      }
      return null
    } catch (err: unknown) {
      error.value =
        (err as ApiErrorShape)?.response?.data?.error?.message || 'Error al añadir la foto'
      console.error('Failed to add photo:', err)
      return null
    } finally {
      loading.value = false
    }
  }

  const updatePhoto = async (
    campId: string,
    photoId: string,
    request: UpdateCampPhotoRequest
  ): Promise<CampPhoto | null> => {
    loading.value = true
    error.value = null
    try {
      const response = await api.put<ApiResponse<CampPhoto>>(
        `/camps/${campId}/photos/${photoId}`,
        request
      )
      if (response.data.success && response.data.data) {
        const updated = response.data.data
        photos.value = photos.value.map((p) => (p.id === photoId ? updated : p))
        return updated
      }
      return null
    } catch (err: unknown) {
      error.value =
        (err as ApiErrorShape)?.response?.data?.error?.message || 'Error al actualizar la foto'
      console.error('Failed to update photo:', err)
      return null
    } finally {
      loading.value = false
    }
  }

  const deletePhoto = async (campId: string, photoId: string): Promise<boolean> => {
    loading.value = true
    error.value = null
    try {
      await api.delete(`/camps/${campId}/photos/${photoId}`)
      photos.value = photos.value.filter((p) => p.id !== photoId)
      return true
    } catch (err: unknown) {
      error.value =
        (err as ApiErrorShape)?.response?.data?.error?.message || 'Error al eliminar la foto'
      console.error('Failed to delete photo:', err)
      return false
    } finally {
      loading.value = false
    }
  }

  const setPrimaryPhoto = async (campId: string, photoId: string): Promise<CampPhoto | null> => {
    loading.value = true
    error.value = null
    try {
      const response = await api.post<ApiResponse<CampPhoto>>(
        `/camps/${campId}/photos/${photoId}/set-primary`
      )
      if (response.data.success && response.data.data) {
        const updated = response.data.data
        photos.value = photos.value.map((p) => ({
          ...p,
          isPrimary: p.id === photoId
        }))
        return updated
      }
      return null
    } catch (err: unknown) {
      error.value =
        (err as ApiErrorShape)?.response?.data?.error?.message ||
        'Error al establecer la foto principal'
      console.error('Failed to set primary photo:', err)
      return null
    } finally {
      loading.value = false
    }
  }

  const reorderPhotos = async (
    campId: string,
    request: ReorderCampPhotosRequest
  ): Promise<boolean> => {
    loading.value = true
    error.value = null
    try {
      await api.put(`/camps/${campId}/photos/reorder`, request)
      return true
    } catch (err: unknown) {
      error.value =
        (err as ApiErrorShape)?.response?.data?.error?.message || 'Error al reordenar las fotos'
      console.error('Failed to reorder photos:', err)
      return false
    } finally {
      loading.value = false
    }
  }

  return {
    photos,
    loading,
    error,
    addPhoto,
    updatePhoto,
    deletePhoto,
    setPrimaryPhoto,
    reorderPhotos
  }
}
