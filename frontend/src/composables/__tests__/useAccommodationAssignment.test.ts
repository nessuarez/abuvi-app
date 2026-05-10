import { describe, it, expect, vi, beforeEach } from 'vitest'
import { ref } from 'vue'
import { useAccommodationAssignment } from '../useAccommodationAssignment'
import { api } from '@/utils/api'

vi.mock('@/utils/api', () => ({
  api: { get: vi.fn(), post: vi.fn(), put: vi.fn(), patch: vi.fn(), delete: vi.fn() }
}))

const campEditionId = ref('edition-1')

const makeProposal = (overrides = {}) => ({
  id: 'proposal-1',
  campEditionId: 'edition-1',
  name: 'Propuesta A',
  notes: null,
  isActive: true,
  assignmentCount: 2,
  unassignedCount: 1,
  createdByUserId: 'user-1',
  createdAt: '2026-01-01T00:00:00Z',
  updatedAt: '2026-01-01T00:00:00Z',
  ...overrides
})

const makeFamily = (overrides = {}) => ({
  registrationId: 'reg-1',
  familyUnitId: 'fam-1',
  familyName: 'García',
  representativeName: 'Ana García',
  memberCount: 4,
  adultCount: 2,
  childCount: 2,
  hasPet: false,
  specialNeeds: null,
  campatesPreference: null,
  accommodationPreferences: [],
  ...overrides
})

const makeState = (overrides = {}) => ({
  proposalId: 'proposal-1',
  families: [makeFamily()],
  accommodations: [],
  assignments: [] as { registrationId: string; accommodationId: string; unitIndex: number | null }[],
  ...overrides
})

describe('useAccommodationAssignment', () => {
  beforeEach(() => vi.clearAllMocks())

  describe('loadProposals', () => {
    it('loadProposals_withActiveProposal_autoSelectsIt', async () => {
      const proposal = makeProposal({ isActive: true })
      vi.mocked(api.get).mockResolvedValueOnce({ data: { success: true, data: [proposal], error: null } })

      const { proposals, selectedProposalId, loadProposals } = useAccommodationAssignment(campEditionId)
      await loadProposals()

      expect(proposals.value).toHaveLength(1)
      expect(selectedProposalId.value).toBe('proposal-1')
    })

    it('loadProposals_withApiError_setsErrorMessage', async () => {
      vi.mocked(api.get).mockRejectedValueOnce({
        response: { data: { error: { message: 'Sin acceso' } } }
      })

      const { error, loadProposals } = useAccommodationAssignment(campEditionId)
      await loadProposals()

      expect(error.value).toBe('Sin acceso')
    })
  })

  describe('assignFamily', () => {
    it('assignFamily_callsCorrectEndpoint', async () => {
      vi.mocked(api.get).mockResolvedValue({ data: { success: true, data: makeState(), error: null } })
      vi.mocked(api.post).mockResolvedValueOnce({ data: { success: true, data: null, error: null } })

      const { selectedProposalId, assignFamily } = useAccommodationAssignment(campEditionId)
      selectedProposalId.value = 'proposal-1'

      await assignFamily('reg-1', 'acc-1', 2)

      expect(api.post).toHaveBeenCalledWith(
        '/camps/editions/edition-1/assignment-proposals/proposal-1/assignments/reg-1',
        { accommodationId: 'acc-1', unitIndex: 2 }
      )
    })

    it('assignFamily_withNullUnitIndex_sendsNullInBody', async () => {
      vi.mocked(api.get).mockResolvedValue({ data: { success: true, data: makeState(), error: null } })
      vi.mocked(api.post).mockResolvedValueOnce({ data: { success: true, data: null, error: null } })

      const { selectedProposalId, assignFamily } = useAccommodationAssignment(campEditionId)
      selectedProposalId.value = 'proposal-1'

      await assignFamily('reg-1', 'acc-1', null)

      expect(api.post).toHaveBeenCalledWith(
        '/camps/editions/edition-1/assignment-proposals/proposal-1/assignments/reg-1',
        { accommodationId: 'acc-1', unitIndex: null }
      )
    })

    it('assignFamily_onApiError_setsErrorMessage', async () => {
      vi.mocked(api.post).mockRejectedValueOnce({
        response: { data: { error: { message: 'Capacidad máxima alcanzada' } } }
      })

      const { selectedProposalId, error, assignFamily } = useAccommodationAssignment(campEditionId)
      selectedProposalId.value = 'proposal-1'

      await assignFamily('reg-1', 'acc-1', null)

      expect(error.value).toBe('Capacidad máxima alcanzada')
    })
  })

  describe('unassignFamily', () => {
    it('unassignFamily_callsDeleteEndpoint', async () => {
      vi.mocked(api.delete).mockResolvedValueOnce({})
      vi.mocked(api.get).mockResolvedValue({ data: { success: true, data: makeState(), error: null } })

      const { selectedProposalId, unassignFamily } = useAccommodationAssignment(campEditionId)
      selectedProposalId.value = 'proposal-1'

      await unassignFamily('reg-1')

      expect(api.delete).toHaveBeenCalledWith(
        '/camps/editions/edition-1/assignment-proposals/proposal-1/assignments/reg-1'
      )
    })
  })

  describe('autoAssign', () => {
    it('autoAssign_callsAutoAssignEndpoint_andUpdatesState', async () => {
      const newState = makeState({ assignments: [{ registrationId: 'reg-1', accommodationId: 'acc-1', unitIndex: null }] })
      vi.mocked(api.post).mockResolvedValueOnce({ data: { success: true, data: newState, error: null } })
      vi.mocked(api.get).mockResolvedValueOnce({ data: { success: true, data: [], error: null } })

      const { selectedProposalId, assignmentState, autoAssign } = useAccommodationAssignment(campEditionId)
      selectedProposalId.value = 'proposal-1'

      await autoAssign(false)

      expect(api.post).toHaveBeenCalledWith(
        '/camps/editions/edition-1/assignment-proposals/proposal-1/assignments/auto-assign',
        { overwriteExisting: false }
      )
      expect(assignmentState.value?.assignments).toHaveLength(1)
    })
  })

  describe('computed properties', () => {
    it('assignmentsMap_returnsCorrectLookup', async () => {
      const state = makeState({
        assignments: [
          { registrationId: 'reg-1', accommodationId: 'acc-A', unitIndex: 0 },
          { registrationId: 'reg-2', accommodationId: 'acc-B', unitIndex: null }
        ]
      })
      vi.mocked(api.get).mockResolvedValue({ data: { success: true, data: state, error: null } })

      const { selectedProposalId, assignmentsMap, loadAssignmentState } = useAccommodationAssignment(campEditionId)
      selectedProposalId.value = 'proposal-1'
      await loadAssignmentState()

      expect(assignmentsMap.value.get('reg-1')).toEqual({ accommodationId: 'acc-A', unitIndex: 0 })
      expect(assignmentsMap.value.get('reg-2')).toEqual({ accommodationId: 'acc-B', unitIndex: null })
      expect(assignmentsMap.value.has('reg-3')).toBe(false)
    })

    it('sortedFamilies_putsUnassignedFirst', async () => {
      const state = makeState({
        families: [
          makeFamily({ registrationId: 'reg-assigned', familyName: 'Martínez' }),
          makeFamily({ registrationId: 'reg-unassigned', familyName: 'Abad' })
        ],
        assignments: [{ registrationId: 'reg-assigned', accommodationId: 'acc-1', unitIndex: null }]
      })
      vi.mocked(api.get).mockResolvedValue({ data: { success: true, data: state, error: null } })

      const { selectedProposalId, sortedFamilies, loadAssignmentState } = useAccommodationAssignment(campEditionId)
      selectedProposalId.value = 'proposal-1'
      await loadAssignmentState()

      expect(sortedFamilies.value[0].registrationId).toBe('reg-unassigned')
      expect(sortedFamilies.value[1].registrationId).toBe('reg-assigned')
    })
  })
})
