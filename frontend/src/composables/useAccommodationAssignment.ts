import { ref, computed, type Ref } from 'vue'
import { api } from '@/utils/api'
import type {
  AccommodationAssignmentProposalSummaryResponse,
  ProposalAssignmentStateResponse,
  AssignmentFamilyResponse
} from '@/types/accommodation-assignment'
import type { ApiResponse } from '@/types/api'

export function useAccommodationAssignment(campEditionId: Ref<string>) {
  const proposals = ref<AccommodationAssignmentProposalSummaryResponse[]>([])
  const selectedProposalId = ref<string | null>(null)
  const assignmentState = ref<ProposalAssignmentStateResponse | null>(null)
  const selectedRegistrationId = ref<string | null>(null)
  const loading = ref(false)
  const saving = ref(false)
  const error = ref<string | null>(null)

  const assignmentsMap = computed((): Map<string, { accommodationId: string; unitIndex: number | null }> => {
    const map = new Map<string, { accommodationId: string; unitIndex: number | null }>()
    assignmentState.value?.assignments.forEach((a) =>
      map.set(a.registrationId, { accommodationId: a.accommodationId, unitIndex: a.unitIndex })
    )
    return map
  })

  const sortedFamilies = computed((): AssignmentFamilyResponse[] => {
    if (!assignmentState.value) return []
    return [...assignmentState.value.families].sort((a, b) => {
      const aAssigned = assignmentsMap.value.has(a.registrationId)
      const bAssigned = assignmentsMap.value.has(b.registrationId)
      if (aAssigned !== bAssigned) return aAssigned ? 1 : -1
      return a.familyName.localeCompare(b.familyName)
    })
  })

  const extractError = (err: unknown): string =>
    (err as { response?: { data?: { error?: { message?: string } } } })?.response?.data?.error
      ?.message ?? 'Ha ocurrido un error inesperado'

  async function loadProposals(): Promise<void> {
    loading.value = true
    error.value = null
    try {
      const res = await api.get<ApiResponse<AccommodationAssignmentProposalSummaryResponse[]>>(
        `/camps/editions/${campEditionId.value}/assignment-proposals`
      )
      if (res.data.success && res.data.data) {
        proposals.value = res.data.data
        if (!selectedProposalId.value) {
          const active = res.data.data.find((p) => p.isActive)
          if (active) selectedProposalId.value = active.id
        }
      }
    } catch (err: unknown) {
      error.value = extractError(err)
    } finally {
      loading.value = false
    }
  }

  async function loadAssignmentState(): Promise<void> {
    if (!selectedProposalId.value) return
    loading.value = true
    error.value = null
    try {
      const res = await api.get<ApiResponse<ProposalAssignmentStateResponse>>(
        `/camps/editions/${campEditionId.value}/assignment-proposals/${selectedProposalId.value}/assignments`
      )
      if (res.data.success && res.data.data) {
        assignmentState.value = res.data.data
      }
    } catch (err: unknown) {
      error.value = extractError(err)
    } finally {
      loading.value = false
    }
  }

  async function selectProposal(proposalId: string): Promise<void> {
    selectedProposalId.value = proposalId
    await loadAssignmentState()
  }

  async function createProposal(
    name: string,
    notes: string | null,
    copyFromId?: string
  ): Promise<void> {
    saving.value = true
    error.value = null
    try {
      const res = await api.post<ApiResponse<AccommodationAssignmentProposalSummaryResponse>>(
        `/camps/editions/${campEditionId.value}/assignment-proposals`,
        { name, notes, copyFromProposalId: copyFromId ?? null }
      )
      if (res.data.success && res.data.data) {
        proposals.value.push(res.data.data)
        await selectProposal(res.data.data.id)
      }
    } catch (err: unknown) {
      error.value = extractError(err)
    } finally {
      saving.value = false
    }
  }

  async function updateProposal(
    proposalId: string,
    name: string,
    notes: string | null
  ): Promise<void> {
    saving.value = true
    error.value = null
    try {
      const res = await api.put<ApiResponse<AccommodationAssignmentProposalSummaryResponse>>(
        `/camps/editions/${campEditionId.value}/assignment-proposals/${proposalId}`,
        { name, notes }
      )
      if (res.data.success && res.data.data) {
        const idx = proposals.value.findIndex((p) => p.id === proposalId)
        if (idx !== -1) proposals.value[idx] = res.data.data
      }
    } catch (err: unknown) {
      error.value = extractError(err)
    } finally {
      saving.value = false
    }
  }

  async function deleteProposal(proposalId: string): Promise<void> {
    saving.value = true
    error.value = null
    try {
      await api.delete(
        `/camps/editions/${campEditionId.value}/assignment-proposals/${proposalId}`
      )
      proposals.value = proposals.value.filter((p) => p.id !== proposalId)
      if (selectedProposalId.value === proposalId) {
        selectedProposalId.value = proposals.value[0]?.id ?? null
        assignmentState.value = null
        await loadAssignmentState()
      }
    } catch (err: unknown) {
      error.value = extractError(err)
    } finally {
      saving.value = false
    }
  }

  async function activateProposal(proposalId: string): Promise<void> {
    saving.value = true
    error.value = null
    try {
      const res = await api.post<ApiResponse<AccommodationAssignmentProposalSummaryResponse>>(
        `/camps/editions/${campEditionId.value}/assignment-proposals/${proposalId}/activate`
      )
      if (res.data.success) {
        await loadProposals()
      }
    } catch (err: unknown) {
      error.value = extractError(err)
    } finally {
      saving.value = false
    }
  }

  async function assignFamily(registrationId: string, accommodationId: string, unitIndex: number | null): Promise<void> {
    if (!selectedProposalId.value) return
    saving.value = true
    error.value = null
    try {
      await api.post(
        `/camps/editions/${campEditionId.value}/assignment-proposals/${selectedProposalId.value}/assignments/${registrationId}`,
        { accommodationId, unitIndex }
      )
      await loadAssignmentState()
      selectedRegistrationId.value = null
    } catch (err: unknown) {
      error.value = extractError(err)
    } finally {
      saving.value = false
    }
  }

  async function unassignFamily(registrationId: string): Promise<void> {
    if (!selectedProposalId.value) return
    saving.value = true
    error.value = null
    try {
      await api.delete(
        `/camps/editions/${campEditionId.value}/assignment-proposals/${selectedProposalId.value}/assignments/${registrationId}`
      )
      await loadAssignmentState()
    } catch (err: unknown) {
      error.value = extractError(err)
    } finally {
      saving.value = false
    }
  }

  async function autoAssign(overwriteExisting: boolean): Promise<void> {
    if (!selectedProposalId.value) return
    saving.value = true
    error.value = null
    try {
      const res = await api.post<ApiResponse<ProposalAssignmentStateResponse>>(
        `/camps/editions/${campEditionId.value}/assignment-proposals/${selectedProposalId.value}/assignments/auto-assign`,
        { overwriteExisting }
      )
      if (res.data.success && res.data.data) {
        assignmentState.value = res.data.data
        await loadProposals()
      }
    } catch (err: unknown) {
      error.value = extractError(err)
    } finally {
      saving.value = false
    }
  }

  return {
    proposals,
    selectedProposalId,
    assignmentState,
    selectedRegistrationId,
    loading,
    saving,
    error,
    assignmentsMap,
    sortedFamilies,
    loadProposals,
    loadAssignmentState,
    selectProposal,
    createProposal,
    updateProposal,
    deleteProposal,
    activateProposal,
    assignFamily,
    unassignFamily,
    autoAssign
  }
}
