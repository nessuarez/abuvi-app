import { ref } from 'vue'
import { api } from '@/utils/api'
import type { ApiResponse } from '@/types/api'
import type { ThemeItems } from '@/types/album'
import type { MediaItemType } from '@/types/media-item'
import type {
  CreateMediaThemeRequest,
  MediaTheme,
  UpdateMediaThemeRequest,
} from '@/types/media-theme'

type ApiErrorShape = { response?: { data?: { error?: { message?: string } }; status?: number } }

const FETCH_ERROR = 'No se pudieron cargar los temas'
const FETCH_ITEMS_ERROR = 'No se pudieron cargar los recuerdos de este tema'
const SAVE_ERROR = 'No se pudo guardar el tema'
const TAG_ERROR = 'No se pudo etiquetar el recuerdo'

export function useMediaThemes() {
  const themes = ref<MediaTheme[]>([])
  const themeItems = ref<ThemeItems | null>(null)
  const loading = ref(false)
  const saving = ref(false)
  const error = ref<string | null>(null)

  const fetchCatalogue = async (includeInactive = false): Promise<void> => {
    loading.value = true
    error.value = null
    try {
      const qs = includeInactive ? '?includeInactive=true' : ''
      const response = await api.get<ApiResponse<MediaTheme[]>>(`/media-themes${qs}`)
      if (response.data.success && response.data.data) {
        themes.value = response.data.data
      } else {
        error.value = response.data.error?.message ?? FETCH_ERROR
      }
    } catch (err: unknown) {
      error.value = (err as ApiErrorShape)?.response?.data?.error?.message ?? FETCH_ERROR
      console.error('Failed to fetch themes:', err)
    } finally {
      loading.value = false
    }
  }

  const fetchThemeItems = async (
    slug: string,
    params?: {
      page?: number
      pageSize?: number
      year?: number
      campEditionId?: string
      undatedOnly?: boolean
      type?: MediaItemType
    }
  ): Promise<void> => {
    loading.value = true
    error.value = null
    try {
      const query = new URLSearchParams()
      if (params?.page != null) query.set('page', String(params.page))
      if (params?.pageSize != null) query.set('pageSize', String(params.pageSize))
      if (params?.year != null) query.set('year', String(params.year))
      if (params?.campEditionId) query.set('campEditionId', params.campEditionId)
      if (params?.undatedOnly) query.set('undatedOnly', 'true')
      if (params?.type) query.set('type', params.type)

      const qs = query.toString()
      const response = await api.get<ApiResponse<ThemeItems>>(
        `/media-themes/${slug}/items${qs ? `?${qs}` : ''}`
      )

      if (response.data.success && response.data.data) {
        themeItems.value = response.data.data
      } else {
        error.value = response.data.error?.message ?? FETCH_ITEMS_ERROR
      }
    } catch (err: unknown) {
      error.value = (err as ApiErrorShape)?.response?.data?.error?.message ?? FETCH_ITEMS_ERROR
      console.error('Failed to fetch theme items:', err)
    } finally {
      loading.value = false
    }
  }

  const createTheme = async (request: CreateMediaThemeRequest): Promise<MediaTheme | null> => {
    saving.value = true
    error.value = null
    try {
      const response = await api.post<ApiResponse<MediaTheme>>('/media-themes', request)
      if (response.data.success && response.data.data) {
        themes.value.push(response.data.data)
        return response.data.data
      }
      error.value = response.data.error?.message ?? SAVE_ERROR
      return null
    } catch (err: unknown) {
      error.value = (err as ApiErrorShape)?.response?.data?.error?.message ?? SAVE_ERROR
      console.error('Failed to create theme:', err)
      return null
    } finally {
      saving.value = false
    }
  }

  const updateTheme = async (
    id: string,
    request: UpdateMediaThemeRequest
  ): Promise<boolean> => {
    saving.value = true
    error.value = null
    try {
      const response = await api.put<ApiResponse<MediaTheme>>(`/media-themes/${id}`, request)
      if (response.data.success && response.data.data) {
        const index = themes.value.findIndex((t) => t.id === id)
        if (index !== -1) themes.value[index] = response.data.data
        return true
      }
      error.value = response.data.error?.message ?? SAVE_ERROR
      return false
    } catch (err: unknown) {
      error.value = (err as ApiErrorShape)?.response?.data?.error?.message ?? SAVE_ERROR
      console.error('Failed to update theme:', err)
      return false
    } finally {
      saving.value = false
    }
  }

  const deleteTheme = async (id: string, force = false): Promise<boolean> => {
    error.value = null
    try {
      await api.delete(`/media-themes/${id}${force ? '?force=true' : ''}`)
      themes.value = themes.value.filter((t) => t.id !== id)
      return true
    } catch (err: unknown) {
      const status = (err as ApiErrorShape)?.response?.status
      error.value =
        status === 409
          ? ((err as ApiErrorShape)?.response?.data?.error?.message ??
            'El tema tiene recuerdos asociados')
          : SAVE_ERROR

      console.error('Failed to delete theme:', err)
      return false
    }
  }

  /** Attaching an existing theme is open to any member — it is the cheapest contribution. */
  const attachTheme = async (mediaItemId: string, themeId: string): Promise<boolean> => {
    error.value = null
    try {
      await api.post(`/media-items/${mediaItemId}/themes`, { themeId })
      return true
    } catch (err: unknown) {
      error.value = (err as ApiErrorShape)?.response?.data?.error?.message ?? TAG_ERROR
      console.error('Failed to attach theme:', err)
      return false
    }
  }

  const detachTheme = async (mediaItemId: string, themeId: string): Promise<boolean> => {
    error.value = null
    try {
      await api.delete(`/media-items/${mediaItemId}/themes/${themeId}`)
      return true
    } catch (err: unknown) {
      error.value = (err as ApiErrorShape)?.response?.data?.error?.message ?? TAG_ERROR
      console.error('Failed to detach theme:', err)
      return false
    }
  }

  return {
    themes,
    themeItems,
    loading,
    saving,
    error,
    fetchCatalogue,
    fetchThemeItems,
    createTheme,
    updateTheme,
    deleteTheme,
    attachTheme,
    detachTheme,
  }
}
