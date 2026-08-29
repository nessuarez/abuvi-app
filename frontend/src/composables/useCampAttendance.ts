import { computed, ref } from 'vue'
import { api } from '@/utils/api'
import type { ApiResponse } from '@/types/api'
import type { AttendanceEntry, CampTimeline } from '@/types/camp-attendance'

type ApiErrorShape = { response?: { data?: { error?: { message?: string } }; status?: number } }

const FETCH_ERROR = 'No se pudo cargar tu histórico de campamentos'
const DECLARE_ERROR = 'No se pudo guardar tu asistencia'
const FORBIDDEN_ERROR = 'No puedes declarar asistencia por este familiar'

/**
 * "Yo estuve en este campamento".
 *
 * The timeline returns every edition, attended or not, so the map and the year strip can
 * be painted from one call. Entries with `attendanceSource: 'Registration'` are derived
 * from the family's registrations: real, but not withdrawable.
 */
export function useCampAttendance() {
  const timeline = ref<CampTimeline | null>(null)
  const attendees = ref<AttendanceEntry[]>([])
  const loading = ref(false)
  const submitting = ref(false)
  const error = ref<string | null>(null)

  const fetchTimeline = async (): Promise<void> => {
    loading.value = true
    error.value = null
    try {
      const response = await api.get<ApiResponse<CampTimeline>>('/users/me/camp-timeline')
      if (response.data.success && response.data.data) {
        timeline.value = response.data.data
      } else {
        error.value = response.data.error?.message ?? FETCH_ERROR
      }
    } catch (err: unknown) {
      error.value = (err as ApiErrorShape)?.response?.data?.error?.message ?? FETCH_ERROR
      console.error('Failed to fetch camp timeline:', err)
    } finally {
      loading.value = false
    }
  }

  const fetchAttendees = async (editionId: string): Promise<void> => {
    loading.value = true
    error.value = null
    try {
      const response = await api.get<ApiResponse<AttendanceEntry[]>>(
        `/camp-editions/${editionId}/attendance`
      )
      if (response.data.success && response.data.data) {
        attendees.value = response.data.data
      } else {
        error.value = response.data.error?.message ?? FETCH_ERROR
      }
    } catch (err: unknown) {
      error.value = (err as ApiErrorShape)?.response?.data?.error?.message ?? FETCH_ERROR
      console.error('Failed to fetch attendance:', err)
    } finally {
      loading.value = false
    }
  }

  /** Idempotent by design: declaring twice succeeds rather than erroring. */
  const declare = async (editionId: string, familyMemberId?: string | null): Promise<boolean> => {
    submitting.value = true
    error.value = null
    try {
      await api.post(`/camp-editions/${editionId}/attendance`, {
        familyMemberId: familyMemberId ?? null,
      })
      patchTimeline(editionId, true)
      return true
    } catch (err: unknown) {
      const status = (err as ApiErrorShape)?.response?.status
      error.value =
        status === 403
          ? FORBIDDEN_ERROR
          : ((err as ApiErrorShape)?.response?.data?.error?.message ?? DECLARE_ERROR)

      console.error('Failed to declare attendance:', err)
      return false
    } finally {
      submitting.value = false
    }
  }

  const withdraw = async (editionId: string, familyMemberId?: string | null): Promise<boolean> => {
    submitting.value = true
    error.value = null
    try {
      const qs = familyMemberId ? `?familyMemberId=${familyMemberId}` : ''
      await api.delete(`/camp-editions/${editionId}/attendance${qs}`)
      patchTimeline(editionId, false)
      return true
    } catch (err: unknown) {
      // 400 here means the attendance came from a registration and cannot be removed.
      // The API message says so in Spanish; surface it rather than a generic failure.
      error.value = (err as ApiErrorShape)?.response?.data?.error?.message ?? DECLARE_ERROR
      console.error('Failed to withdraw attendance:', err)
      return false
    } finally {
      submitting.value = false
    }
  }

  /** Keeps the timeline in step with a toggle without refetching fifty rows. */
  const patchTimeline = (editionId: string, attended: boolean): void => {
    if (!timeline.value) return

    const entries = timeline.value.entries.map((e) =>
      e.campEditionId === editionId
        ? { ...e, attended, attendanceSource: attended ? ('Declared' as const) : ('None' as const) }
        : e
    )

    timeline.value = {
      totalEditionsAttended: entries.filter((e) => e.attended).length,
      entries,
    }
  }

  const attendedEditionIds = computed(
    () => new Set((timeline.value?.entries ?? []).filter((e) => e.attended).map((e) => e.campEditionId))
  )

  const attendedYears = computed(
    () => new Set((timeline.value?.entries ?? []).filter((e) => e.attended).map((e) => e.year))
  )

  return {
    timeline,
    attendees,
    loading,
    submitting,
    error,
    fetchTimeline,
    fetchAttendees,
    declare,
    withdraw,
    attendedEditionIds,
    attendedYears,
  }
}
