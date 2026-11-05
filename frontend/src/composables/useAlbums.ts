import { computed, ref } from 'vue'
import { api } from '@/utils/api'
import type { ApiResponse } from '@/types/api'
import type { AlbumDetail, AlbumSummary, PagedMedia } from '@/types/album'
import type { MediaItemType } from '@/types/media-item'

type ApiErrorShape = { response?: { data?: { error?: { message?: string } }; status?: number } }

const FETCH_ERROR = 'No se pudieron cargar los álbumes'
const FETCH_ALBUM_ERROR = 'No se pudo cargar el álbum'
const FETCH_UNPLACED_ERROR = 'No se pudieron cargar los recuerdos sin ubicar'

/**
 * Camp edition albums.
 *
 * An album is a query over media filtered by edition — there is no album entity. The
 * index returns all fifty rows in one call, which is what makes the year lookup below
 * legitimate: there is no pagination to reconcile.
 */
export function useAlbums() {
  const albums = ref<AlbumSummary[]>([])
  const album = ref<AlbumDetail | null>(null)
  const unplaced = ref<PagedMedia | null>(null)
  const loading = ref(false)
  const error = ref<string | null>(null)

  const fetchIndex = async (): Promise<void> => {
    loading.value = true
    error.value = null
    try {
      const response = await api.get<ApiResponse<AlbumSummary[]>>('/camp-editions/albums')
      if (response.data.success && response.data.data) {
        albums.value = response.data.data
      } else {
        error.value = response.data.error?.message ?? FETCH_ERROR
      }
    } catch (err: unknown) {
      error.value = (err as ApiErrorShape)?.response?.data?.error?.message ?? FETCH_ERROR
      console.error('Failed to fetch albums:', err)
    } finally {
      loading.value = false
    }
  }

  const fetchAlbum = async (
    editionId: string,
    params?: { page?: number; pageSize?: number; type?: MediaItemType; themeId?: string }
  ): Promise<void> => {
    loading.value = true
    error.value = null
    try {
      const query = new URLSearchParams()
      if (params?.page != null) query.set('page', String(params.page))
      if (params?.pageSize != null) query.set('pageSize', String(params.pageSize))
      if (params?.type) query.set('type', params.type)
      if (params?.themeId) query.set('themeId', params.themeId)

      const qs = query.toString()
      const response = await api.get<ApiResponse<AlbumDetail>>(
        `/camp-editions/${editionId}/album${qs ? `?${qs}` : ''}`
      )

      if (response.data.success && response.data.data) {
        album.value = response.data.data
      } else {
        error.value = response.data.error?.message ?? FETCH_ALBUM_ERROR
      }
    } catch (err: unknown) {
      error.value = (err as ApiErrorShape)?.response?.data?.error?.message ?? FETCH_ALBUM_ERROR
      console.error('Failed to fetch album:', err)
    } finally {
      loading.value = false
    }
  }

  const fetchUnplaced = async (params?: {
    page?: number
    pageSize?: number
    type?: MediaItemType
    mediaSourceId?: string
    suggestedForMe?: boolean
  }): Promise<void> => {
    loading.value = true
    error.value = null
    try {
      const query = new URLSearchParams()
      if (params?.page != null) query.set('page', String(params.page))
      if (params?.pageSize != null) query.set('pageSize', String(params.pageSize))
      if (params?.type) query.set('type', params.type)
      if (params?.mediaSourceId) query.set('mediaSourceId', params.mediaSourceId)
      if (params?.suggestedForMe) query.set('suggestedForMe', 'true')

      const qs = query.toString()
      const response = await api.get<ApiResponse<PagedMedia>>(
        `/media-items/unplaced${qs ? `?${qs}` : ''}`
      )

      if (response.data.success && response.data.data) {
        unplaced.value = response.data.data
      } else {
        error.value = response.data.error?.message ?? FETCH_UNPLACED_ERROR
      }
    } catch (err: unknown) {
      error.value = (err as ApiErrorShape)?.response?.data?.error?.message ?? FETCH_UNPLACED_ERROR
      console.error('Failed to fetch unplaced media:', err)
    } finally {
      loading.value = false
    }
  }

  /**
   * Year to edition id.
   *
   * The anniversary journey (from the history feature) is keyed by year and has no
   * edition id, so this is the seam that lets it link into an album. Both endpoints
   * assume one edition per year, true for 1976-2025.
   */
  const editionIdByYear = computed(
    () => new Map(albums.value.map((a) => [a.year, a.campEditionId]))
  )

  /** Editions the viewer attended, for highlighting the map and the year strip. */
  const attendedYears = computed(
    () => new Set(albums.value.filter((a) => a.viewerAttended).map((a) => a.year))
  )

  return {
    albums,
    album,
    unplaced,
    loading,
    error,
    fetchIndex,
    fetchAlbum,
    fetchUnplaced,
    editionIdByYear,
    attendedYears,
  }
}
