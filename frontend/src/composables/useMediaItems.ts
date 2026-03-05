import { ref } from 'vue'
import { api } from '@/utils/api'
import type { ApiResponse } from '@/types/api'
import type { MediaItem, MediaItemType, CreateMediaItemRequest } from '@/types/media-item'

type ApiErrorShape = { response?: { data?: { error?: { message?: string } }; status?: number } }

export function useMediaItems() {
  const mediaItems = ref<MediaItem[]>([])
  const loading = ref(false)
  const error = ref<string | null>(null)
  const creating = ref(false)
  const createError = ref<string | null>(null)

  const fetchMediaItems = async (params?: {
    year?: number
    approved?: boolean
    context?: string
    type?: MediaItemType
  }): Promise<void> => {
    loading.value = true
    error.value = null
    try {
      const query = new URLSearchParams()
      if (params?.year != null) query.set('year', String(params.year))
      if (params?.approved != null) query.set('approved', String(params.approved))
      if (params?.context) query.set('context', params.context)
      if (params?.type) query.set('type', params.type)

      const qs = query.toString()
      const url = `/media-items${qs ? `?${qs}` : ''}`
      const response = await api.get<ApiResponse<MediaItem[]>>(url)

      if (response.data.success && response.data.data) {
        mediaItems.value = response.data.data
      } else {
        error.value = response.data.error?.message ?? 'Error al obtener los elementos multimedia'
      }
    } catch (err: unknown) {
      error.value =
        (err as ApiErrorShape)?.response?.data?.error?.message ??
        'Error al obtener los elementos multimedia'
      console.error('Failed to fetch media items:', err)
    } finally {
      loading.value = false
    }
  }

  const createMediaItem = async (request: CreateMediaItemRequest): Promise<MediaItem | null> => {
    creating.value = true
    createError.value = null
    try {
      const response = await api.post<ApiResponse<MediaItem>>('/media-items', request)
      if (response.data.success && response.data.data) {
        mediaItems.value.push(response.data.data)
        return response.data.data
      }
      createError.value = response.data.error?.message ?? 'Error al crear el elemento multimedia'
      return null
    } catch (err: unknown) {
      createError.value =
        (err as ApiErrorShape)?.response?.data?.error?.message ??
        'Error al crear el elemento multimedia'
      console.error('Failed to create media item:', err)
      return null
    } finally {
      creating.value = false
    }
  }

  const approveMediaItem = async (id: string): Promise<boolean> => {
    loading.value = true
    error.value = null
    try {
      await api.patch<ApiResponse<MediaItem>>(`/media-items/${id}/approve`)
      return true
    } catch (err: unknown) {
      error.value =
        (err as ApiErrorShape)?.response?.data?.error?.message ??
        'Error al aprobar el elemento multimedia'
      console.error('Failed to approve media item:', err)
      return false
    } finally {
      loading.value = false
    }
  }

  const rejectMediaItem = async (id: string): Promise<boolean> => {
    loading.value = true
    error.value = null
    try {
      await api.patch<ApiResponse<MediaItem>>(`/media-items/${id}/reject`)
      return true
    } catch (err: unknown) {
      error.value =
        (err as ApiErrorShape)?.response?.data?.error?.message ??
        'Error al rechazar el elemento multimedia'
      console.error('Failed to reject media item:', err)
      return false
    } finally {
      loading.value = false
    }
  }

  const deleteMediaItem = async (id: string): Promise<boolean> => {
    loading.value = true
    error.value = null
    try {
      await api.delete(`/media-items/${id}`)
      mediaItems.value = mediaItems.value.filter((i) => i.id !== id)
      return true
    } catch (err: unknown) {
      error.value =
        (err as ApiErrorShape)?.response?.data?.error?.message ??
        'Error al eliminar el elemento multimedia'
      console.error('Failed to delete media item:', err)
      return false
    } finally {
      loading.value = false
    }
  }

  return {
    mediaItems,
    loading,
    error,
    creating,
    createError,
    fetchMediaItems,
    createMediaItem,
    approveMediaItem,
    rejectMediaItem,
    deleteMediaItem
  }
}
