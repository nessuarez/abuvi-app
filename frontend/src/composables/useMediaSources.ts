import { ref } from 'vue'
import { api } from '@/utils/api'
import type { ApiResponse } from '@/types/api'
import type { PagedMedia } from '@/types/album'
import type {
  CreateMediaSourceRequest,
  MediaSource,
  UpdateMediaSourceRequest,
} from '@/types/media-source'

type ApiErrorShape = { response?: { data?: { error?: { message?: string } }; status?: number } }

const FETCH_ERROR = 'No se pudieron cargar las aportaciones'
const SAVE_ERROR = 'No se pudo guardar el aportante'
const MERGE_ERROR = 'No se pudieron fusionar los aportantes'

/**
 * Provenance: who gave us each batch of material.
 *
 * `contributorContact` arrives already stripped by the API for anyone below Admin/Board.
 * Render it only in the admin panel — never add a second path to it.
 */
export function useMediaSources() {
  const sources = ref<MediaSource[]>([])
  const source = ref<MediaSource | null>(null)
  const sourceItems = ref<PagedMedia | null>(null)
  const loading = ref(false)
  const saving = ref(false)
  const error = ref<string | null>(null)

  const fetchAll = async (): Promise<void> => {
    loading.value = true
    error.value = null
    try {
      const response = await api.get<ApiResponse<MediaSource[]>>('/media-sources')
      if (response.data.success && response.data.data) {
        sources.value = response.data.data
      } else {
        error.value = response.data.error?.message ?? FETCH_ERROR
      }
    } catch (err: unknown) {
      error.value = (err as ApiErrorShape)?.response?.data?.error?.message ?? FETCH_ERROR
      console.error('Failed to fetch media sources:', err)
    } finally {
      loading.value = false
    }
  }

  const fetchById = async (id: string): Promise<void> => {
    loading.value = true
    error.value = null
    try {
      const response = await api.get<ApiResponse<MediaSource>>(`/media-sources/${id}`)
      if (response.data.success && response.data.data) {
        source.value = response.data.data
      } else {
        error.value = response.data.error?.message ?? FETCH_ERROR
      }
    } catch (err: unknown) {
      error.value = (err as ApiErrorShape)?.response?.data?.error?.message ?? FETCH_ERROR
      console.error('Failed to fetch media source:', err)
    } finally {
      loading.value = false
    }
  }

  const fetchItems = async (
    id: string,
    params?: { page?: number; pageSize?: number }
  ): Promise<void> => {
    loading.value = true
    error.value = null
    try {
      const query = new URLSearchParams()
      if (params?.page != null) query.set('page', String(params.page))
      if (params?.pageSize != null) query.set('pageSize', String(params.pageSize))

      const qs = query.toString()
      const response = await api.get<ApiResponse<PagedMedia>>(
        `/media-sources/${id}/items${qs ? `?${qs}` : ''}`
      )

      if (response.data.success && response.data.data) {
        sourceItems.value = response.data.data
      } else {
        error.value = response.data.error?.message ?? FETCH_ERROR
      }
    } catch (err: unknown) {
      error.value = (err as ApiErrorShape)?.response?.data?.error?.message ?? FETCH_ERROR
      console.error('Failed to fetch source items:', err)
    } finally {
      loading.value = false
    }
  }

  const createSource = async (
    request: CreateMediaSourceRequest
  ): Promise<MediaSource | null> => {
    saving.value = true
    error.value = null
    try {
      const response = await api.post<ApiResponse<MediaSource>>('/media-sources', request)
      if (response.data.success && response.data.data) {
        sources.value.push(response.data.data)
        return response.data.data
      }
      error.value = response.data.error?.message ?? SAVE_ERROR
      return null
    } catch (err: unknown) {
      error.value = (err as ApiErrorShape)?.response?.data?.error?.message ?? SAVE_ERROR
      console.error('Failed to create media source:', err)
      return null
    } finally {
      saving.value = false
    }
  }

  const updateSource = async (
    id: string,
    request: UpdateMediaSourceRequest
  ): Promise<boolean> => {
    saving.value = true
    error.value = null
    try {
      const response = await api.put<ApiResponse<MediaSource>>(`/media-sources/${id}`, request)
      if (response.data.success && response.data.data) {
        const index = sources.value.findIndex((s) => s.id === id)
        if (index !== -1) sources.value[index] = response.data.data
        return true
      }
      error.value = response.data.error?.message ?? SAVE_ERROR
      return false
    } catch (err: unknown) {
      const status = (err as ApiErrorShape)?.response?.status
      error.value =
        status === 403
          ? 'Solo puede editarlo quien lo registró o un administrador'
          : ((err as ApiErrorShape)?.response?.data?.error?.message ?? SAVE_ERROR)

      console.error('Failed to update media source:', err)
      return false
    } finally {
      saving.value = false
    }
  }

  /** Admin/Board. Folds one contributor into another; free-text names guarantee duplicates. */
  const mergeSources = async (sourceId: string, targetId: string): Promise<number | null> => {
    saving.value = true
    error.value = null
    try {
      const response = await api.post<ApiResponse<{ movedItems: number }>>(
        `/media-sources/${sourceId}/merge`,
        { targetId }
      )
      if (response.data.success && response.data.data) {
        sources.value = sources.value.filter((s) => s.id !== sourceId)
        return response.data.data.movedItems
      }
      error.value = response.data.error?.message ?? MERGE_ERROR
      return null
    } catch (err: unknown) {
      error.value = (err as ApiErrorShape)?.response?.data?.error?.message ?? MERGE_ERROR
      console.error('Failed to merge media sources:', err)
      return null
    } finally {
      saving.value = false
    }
  }

  /** Admin only. RGPD erasure: keeps the row and the media, blanks the person. */
  const anonymiseSource = async (id: string): Promise<boolean> => {
    error.value = null
    try {
      await api.patch(`/media-sources/${id}/anonymise`)
      await fetchAll()
      return true
    } catch (err: unknown) {
      error.value = (err as ApiErrorShape)?.response?.data?.error?.message ?? SAVE_ERROR
      console.error('Failed to anonymise media source:', err)
      return false
    }
  }

  return {
    sources,
    source,
    sourceItems,
    loading,
    saving,
    error,
    fetchAll,
    fetchById,
    fetchItems,
    createSource,
    updateSource,
    mergeSources,
    anonymiseSource,
  }
}
