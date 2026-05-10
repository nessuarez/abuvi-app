import { ref } from 'vue'
import { api } from '@/utils/api'
import type { ApiResponse } from '@/types/api'
import type {
  AccommodationNeedResponse,
  AccommodationNeedsResponse,
  AccommodationNotesResponse,
  FriendLinkResponse,
  FriendLinksResponse,
} from '@/types/registration'

type ApiErrorShape = { response?: { data?: { error?: { message?: string } }; status?: number } }

const extractError = (err: unknown): string =>
  (err as ApiErrorShape)?.response?.data?.error?.message ?? 'Ha ocurrido un error inesperado'

export function useRegistrationAccommodationTagging() {
  const needs = ref<AccommodationNeedResponse[]>([])
  const friendLinks = ref<FriendLinkResponse[]>([])
  const internalNotes = ref<string | null>(null)

  const loading = ref(false)
  const saving = ref(false)
  const error = ref<string | null>(null)
  const saveError = ref<string | null>(null)

  async function fetchNeeds(registrationId: string): Promise<void> {
    loading.value = true
    error.value = null
    try {
      const res = await api.get<ApiResponse<AccommodationNeedResponse[]>>(
        `/registrations/${registrationId}/accommodation-needs`,
      )
      needs.value = res.data.data ?? []
    } catch (err) {
      error.value = extractError(err)
    } finally {
      loading.value = false
    }
  }

  async function updateNeeds(
    registrationId: string,
    featureIds: string[],
  ): Promise<AccommodationNeedsResponse | null> {
    saving.value = true
    saveError.value = null
    try {
      const res = await api.put<ApiResponse<AccommodationNeedsResponse>>(
        `/registrations/${registrationId}/accommodation-needs`,
        { featureIds },
      )
      const data = res.data.data ?? null
      if (data) needs.value = data.needs
      return data
    } catch (err) {
      saveError.value = extractError(err)
      return null
    } finally {
      saving.value = false
    }
  }

  async function updateNotes(
    registrationId: string,
    notes: string | null,
  ): Promise<AccommodationNotesResponse | null> {
    saving.value = true
    saveError.value = null
    try {
      const res = await api.patch<ApiResponse<AccommodationNotesResponse>>(
        `/registrations/${registrationId}/accommodation-notes`,
        { accommodationInternalNotes: notes },
      )
      const data = res.data.data ?? null
      if (data) internalNotes.value = data.accommodationInternalNotes
      return data
    } catch (err) {
      saveError.value = extractError(err)
      return null
    } finally {
      saving.value = false
    }
  }

  async function fetchFriendLinks(registrationId: string): Promise<void> {
    loading.value = true
    error.value = null
    try {
      const res = await api.get<ApiResponse<FriendLinkResponse[]>>(
        `/registrations/${registrationId}/friend-links`,
      )
      friendLinks.value = res.data.data ?? []
    } catch (err) {
      error.value = extractError(err)
    } finally {
      loading.value = false
    }
  }

  async function updateFriendLinks(
    registrationId: string,
    linkedRegistrationIds: string[],
  ): Promise<FriendLinksResponse | null> {
    saving.value = true
    saveError.value = null
    try {
      const res = await api.put<ApiResponse<FriendLinksResponse>>(
        `/registrations/${registrationId}/friend-links`,
        { linkedRegistrationIds },
      )
      const data = res.data.data ?? null
      if (data) friendLinks.value = data.friendLinks
      return data
    } catch (err) {
      saveError.value = extractError(err)
      return null
    } finally {
      saving.value = false
    }
  }

  return {
    needs,
    friendLinks,
    internalNotes,
    loading,
    saving,
    error,
    saveError,
    fetchNeeds,
    updateNeeds,
    updateNotes,
    fetchFriendLinks,
    updateFriendLinks,
  }
}
