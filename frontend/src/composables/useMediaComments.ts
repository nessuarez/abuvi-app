import { ref } from 'vue'
import { api } from '@/utils/api'
import type { ApiResponse } from '@/types/api'
import type {
  MediaComment,
  MediaCommentReport,
  MediaCommentReportStatus,
  ReportMediaCommentRequest,
} from '@/types/media-comment'

type ApiErrorShape = { response?: { data?: { error?: { message?: string } }; status?: number } }

const FETCH_ERROR = 'No se pudieron cargar los comentarios'
const CREATE_ERROR = 'No se pudo enviar tu comentario'
const RATE_LIMIT_ERROR = 'Has enviado muchos comentarios seguidos. Espera un minuto.'
const FORBIDDEN_ERROR = 'No puedes comentar en este recuerdo todavía'
const DELETE_ERROR = 'No se pudo eliminar el comentario'
const REPORT_ERROR = 'No se pudo enviar la denuncia'

/** Marks an optimistic comment so the UI can render it as pending. */
export const PENDING_COMMENT_PREFIX = 'pending-'

export function useMediaComments() {
  const comments = ref<MediaComment[]>([])
  const reports = ref<MediaCommentReport[]>([])
  const loading = ref(false)
  const submitting = ref(false)
  const error = ref<string | null>(null)

  const fetchThread = async (mediaItemId: string): Promise<void> => {
    loading.value = true
    error.value = null
    try {
      const response = await api.get<ApiResponse<MediaComment[]>>(
        `/media-items/${mediaItemId}/comments`
      )
      if (response.data.success && response.data.data) {
        comments.value = response.data.data
      } else {
        error.value = response.data.error?.message ?? FETCH_ERROR
      }
    } catch (err: unknown) {
      error.value = (err as ApiErrorShape)?.response?.data?.error?.message ?? FETCH_ERROR
      console.error('Failed to fetch comments:', err)
    } finally {
      loading.value = false
    }
  }

  /**
   * Posts a comment with an optimistic insert.
   *
   * The comment appears immediately and is replaced by the server's version on success.
   * On failure it is removed again and `error` explains why — a comment that lingers
   * after a failed request is worse than one that never appeared.
   */
  const createComment = async (
    mediaItemId: string,
    body: string,
    author: { userId: string; name: string }
  ): Promise<boolean> => {
    const provisionalId = `${PENDING_COMMENT_PREFIX}${Date.now()}`
    const now = new Date().toISOString()

    comments.value.push({
      id: provisionalId,
      mediaItemId,
      authorUserId: author.userId,
      authorName: author.name,
      body,
      canEdit: false,
      canDelete: false,
      viewerReported: false,
      createdAt: now,
      updatedAt: now,
    })

    submitting.value = true
    error.value = null
    try {
      const response = await api.post<ApiResponse<MediaComment>>(
        `/media-items/${mediaItemId}/comments`,
        { body }
      )

      if (response.data.success && response.data.data) {
        const index = comments.value.findIndex((c) => c.id === provisionalId)
        if (index !== -1) comments.value[index] = response.data.data
        return true
      }

      comments.value = comments.value.filter((c) => c.id !== provisionalId)
      error.value = response.data.error?.message ?? CREATE_ERROR
      return false
    } catch (err: unknown) {
      comments.value = comments.value.filter((c) => c.id !== provisionalId)

      const status = (err as ApiErrorShape)?.response?.status
      // A generic error on 429 reads as a bug; on 403 it reads as one too, when in fact
      // the item simply is not approved yet.
      error.value =
        status === 429
          ? RATE_LIMIT_ERROR
          : status === 403
            ? FORBIDDEN_ERROR
            : ((err as ApiErrorShape)?.response?.data?.error?.message ?? CREATE_ERROR)

      console.error('Failed to create comment:', err)
      return false
    } finally {
      submitting.value = false
    }
  }

  const updateComment = async (id: string, body: string): Promise<boolean> => {
    submitting.value = true
    error.value = null
    try {
      const response = await api.put<ApiResponse<MediaComment>>(`/media-comments/${id}`, { body })
      if (response.data.success && response.data.data) {
        const index = comments.value.findIndex((c) => c.id === id)
        if (index !== -1) comments.value[index] = response.data.data
        return true
      }
      error.value = response.data.error?.message ?? CREATE_ERROR
      return false
    } catch (err: unknown) {
      error.value = (err as ApiErrorShape)?.response?.data?.error?.message ?? CREATE_ERROR
      console.error('Failed to update comment:', err)
      return false
    } finally {
      submitting.value = false
    }
  }

  const deleteComment = async (id: string): Promise<boolean> => {
    error.value = null
    try {
      await api.delete(`/media-comments/${id}`)
      comments.value = comments.value.filter((c) => c.id !== id)
      return true
    } catch (err: unknown) {
      error.value = (err as ApiErrorShape)?.response?.data?.error?.message ?? DELETE_ERROR
      console.error('Failed to delete comment:', err)
      return false
    }
  }

  const reportComment = async (
    id: string,
    request: ReportMediaCommentRequest
  ): Promise<boolean> => {
    error.value = null
    try {
      await api.post(`/media-comments/${id}/report`, request)
      const index = comments.value.findIndex((c) => c.id === id)
      if (index !== -1) comments.value[index] = { ...comments.value[index], viewerReported: true }
      return true
    } catch (err: unknown) {
      const status = (err as ApiErrorShape)?.response?.status
      error.value =
        status === 409
          ? 'Ya has denunciado este comentario'
          : ((err as ApiErrorShape)?.response?.data?.error?.message ?? REPORT_ERROR)

      console.error('Failed to report comment:', err)
      return false
    }
  }

  // ── Moderation queue (Admin/Board) ──────────────────────────────────────────

  const fetchReports = async (status?: MediaCommentReportStatus): Promise<void> => {
    loading.value = true
    error.value = null
    try {
      const qs = status ? `?status=${status}` : ''
      const response = await api.get<ApiResponse<MediaCommentReport[]>>(
        `/media-comments/reports${qs}`
      )
      if (response.data.success && response.data.data) {
        reports.value = response.data.data
      } else {
        error.value = response.data.error?.message ?? FETCH_ERROR
      }
    } catch (err: unknown) {
      error.value = (err as ApiErrorShape)?.response?.data?.error?.message ?? FETCH_ERROR
      console.error('Failed to fetch comment reports:', err)
    } finally {
      loading.value = false
    }
  }

  const reviewReport = async (
    id: string,
    status: Exclude<MediaCommentReportStatus, 'Pending'>
  ): Promise<boolean> => {
    error.value = null
    try {
      await api.patch(`/media-comments/reports/${id}`, { status })
      reports.value = reports.value.filter((r) => r.id !== id)
      return true
    } catch (err: unknown) {
      error.value = (err as ApiErrorShape)?.response?.data?.error?.message ?? REPORT_ERROR
      console.error('Failed to review report:', err)
      return false
    }
  }

  return {
    comments,
    reports,
    loading,
    submitting,
    error,
    fetchThread,
    createComment,
    updateComment,
    deleteComment,
    reportComment,
    fetchReports,
    reviewReport,
  }
}
