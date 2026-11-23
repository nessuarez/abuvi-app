import { ref } from 'vue'
import { api } from '@/utils/api'
import type { ApiResponse } from '@/types/api'
import type { UpsertYearProposalRequest, YearProposalTally } from '@/types/media-dating'

type ApiErrorShape = { response?: { data?: { error?: { message?: string } }; status?: number } }

const FETCH_ERROR = 'No se pudo cargar la datación'
const SUBMIT_ERROR = 'No se pudo enviar tu propuesta'
const WITHDRAW_ERROR = 'No se pudo retirar tu propuesta'

/**
 * Collaborative dating: "¿de qué año es esta?".
 *
 * Every mutation returns the fresh tally, so the panel re-renders from the response
 * rather than refetching. Withdrawing can un-resolve an item — the tally coming back
 * with `isResolved: false` is expected, not an error.
 */
export function useMediaDating() {
  const tally = ref<YearProposalTally | null>(null)
  const loading = ref(false)
  const submitting = ref(false)
  const error = ref<string | null>(null)

  const fetchTally = async (mediaItemId: string): Promise<void> => {
    loading.value = true
    error.value = null
    try {
      const response = await api.get<ApiResponse<YearProposalTally>>(
        `/media-items/${mediaItemId}/year-proposals`
      )
      if (response.data.success && response.data.data) {
        tally.value = response.data.data
      } else {
        error.value = response.data.error?.message ?? FETCH_ERROR
      }
    } catch (err: unknown) {
      error.value = (err as ApiErrorShape)?.response?.data?.error?.message ?? FETCH_ERROR
      console.error('Failed to fetch year proposals:', err)
    } finally {
      loading.value = false
    }
  }

  const propose = async (
    mediaItemId: string,
    request: UpsertYearProposalRequest
  ): Promise<boolean> => {
    submitting.value = true
    error.value = null
    try {
      const response = await api.put<ApiResponse<YearProposalTally>>(
        `/media-items/${mediaItemId}/year-proposals`,
        request
      )
      if (response.data.success && response.data.data) {
        tally.value = response.data.data
        return true
      }
      error.value = response.data.error?.message ?? SUBMIT_ERROR
      return false
    } catch (err: unknown) {
      error.value = (err as ApiErrorShape)?.response?.data?.error?.message ?? SUBMIT_ERROR
      console.error('Failed to submit year proposal:', err)
      return false
    } finally {
      submitting.value = false
    }
  }

  const withdraw = async (mediaItemId: string): Promise<boolean> => {
    submitting.value = true
    error.value = null
    try {
      const response = await api.delete<ApiResponse<YearProposalTally>>(
        `/media-items/${mediaItemId}/year-proposals`
      )
      if (response.data.success && response.data.data) {
        tally.value = response.data.data
        return true
      }
      error.value = response.data.error?.message ?? WITHDRAW_ERROR
      return false
    } catch (err: unknown) {
      error.value = (err as ApiErrorShape)?.response?.data?.error?.message ?? WITHDRAW_ERROR
      console.error('Failed to withdraw year proposal:', err)
      return false
    } finally {
      submitting.value = false
    }
  }

  /** Admin/Board override. Freezes the item against community consensus for good. */
  const setYearAsAdmin = async (
    mediaItemId: string,
    year: number,
    campEditionId?: string | null
  ): Promise<boolean> => {
    submitting.value = true
    error.value = null
    try {
      const response = await api.patch<ApiResponse<YearProposalTally>>(
        `/media-items/${mediaItemId}/year`,
        { year, campEditionId: campEditionId ?? null }
      )
      if (response.data.success && response.data.data) {
        tally.value = response.data.data
        return true
      }
      error.value = response.data.error?.message ?? SUBMIT_ERROR
      return false
    } catch (err: unknown) {
      error.value = (err as ApiErrorShape)?.response?.data?.error?.message ?? SUBMIT_ERROR
      console.error('Failed to set year:', err)
      return false
    } finally {
      submitting.value = false
    }
  }

  return { tally, loading, submitting, error, fetchTally, propose, withdraw, setYearAsAdmin }
}
