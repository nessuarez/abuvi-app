import { describe, it, expect } from 'vitest'
import { mount } from '@vue/test-utils'
import PrimeVue from 'primevue/config'
import RegistrationMemberSelector from '@/components/registrations/RegistrationMemberSelector.vue'
import type { FamilyMemberResponse } from '@/types/family-unit'
import { FamilyRelationship } from '@/types/family-unit'
import type { WizardMemberSelection } from '@/types/registration'
import type { CampEdition } from '@/types/camp-edition'

const mockMembers: FamilyMemberResponse[] = [
  {
    id: 'member-1',
    familyUnitId: 'fu-1',
    userId: null,
    firstName: 'Juan',
    lastName: 'García',
    dateOfBirth: '1990-05-15',
    relationship: FamilyRelationship.Parent,
    documentNumber: '12345678A',
    email: null,
    phone: null,
    hasMedicalNotes: false,
    hasAllergies: false,
    createdAt: '2026-01-01T00:00:00Z',
    updatedAt: '2026-01-01T00:00:00Z'
  },
  {
    id: 'member-2',
    familyUnitId: 'fu-1',
    userId: null,
    firstName: 'Ana',
    lastName: 'García',
    dateOfBirth: '2015-08-20',
    relationship: FamilyRelationship.Child,
    documentNumber: null,
    email: null,
    phone: null,
    hasMedicalNotes: true,
    hasAllergies: true,
    createdAt: '2026-01-01T00:00:00Z',
    updatedAt: '2026-01-01T00:00:00Z'
  }
]

const mockMembersExtended: FamilyMemberResponse[] = [
  ...mockMembers,
  {
    id: 'member-3',
    familyUnitId: 'fu-1',
    userId: null,
    firstName: 'María',
    lastName: 'López',
    dateOfBirth: '1988-03-10',
    relationship: FamilyRelationship.Parent,
    documentNumber: '87654321B',
    email: null,
    phone: null,
    hasMedicalNotes: false,
    hasAllergies: false,
    createdAt: '2026-01-01T00:00:00Z',
    updatedAt: '2026-01-01T00:00:00Z'
  },
  {
    id: 'member-4',
    familyUnitId: 'fu-1',
    userId: null,
    firstName: 'Pedro',
    lastName: 'García',
    dateOfBirth: '2018-11-01',
    relationship: FamilyRelationship.Child,
    documentNumber: null,
    email: null,
    phone: null,
    hasMedicalNotes: false,
    hasAllergies: false,
    createdAt: '2026-01-01T00:00:00Z',
    updatedAt: '2026-01-01T00:00:00Z'
  }
]

// Edition with only Complete period (no week pricing, no weekend)
const mockEditionComplete = {
  startDate: '2025-07-01',
  endDate: '2025-07-14',
  pricePerAdultWeek: null,
  weekendStartDate: null,
  weekendEndDate: null
} as unknown as CampEdition

// Edition with all periods enabled
const mockEditionWithPeriods = {
  startDate: '2025-07-01',
  endDate: '2025-07-14',
  pricePerAdultWeek: 110,
  halfDate: null,
  weekendStartDate: '2025-07-05',
  weekendEndDate: '2025-07-07'
} as unknown as CampEdition

const mountComponent = (
  modelValue: WizardMemberSelection[] = [],
  edition: CampEdition = mockEditionComplete,
  members: FamilyMemberResponse[] = mockMembers
) =>
  mount(RegistrationMemberSelector, {
    props: { members, modelValue, edition },
    global: { plugins: [PrimeVue] }
  })

describe('RegistrationMemberSelector', () => {
  it('should render all family members as checkboxes', () => {
    const wrapper = mountComponent()
    const checkboxes = wrapper.findAll('[data-testid="member-checkbox"]')
    expect(checkboxes).toHaveLength(2)
    expect(wrapper.text()).toContain('Juan García')
    expect(wrapper.text()).toContain('Ana García')
  })

  it('should emit update:modelValue with WizardMemberSelection when member is checked', async () => {
    const wrapper = mountComponent([])
    const card = wrapper.find('[data-testid="member-label-member-1"]')
    await card.trigger('click')
    const emitted = wrapper.emitted('update:modelValue') as WizardMemberSelection[][]
    expect(emitted).toBeTruthy()
    const emittedSelections = emitted[emitted.length - 1][0]
    expect(Array.isArray(emittedSelections)).toBe(true)
    expect(emittedSelections[0]).toMatchObject({
      memberId: 'member-1',
      attendancePeriod: 'Complete',
      visitStartDate: null,
      visitEndDate: null,
      guardianName: null,
      guardianDocumentNumber: null
    })
  })

  it('should emit update:modelValue removing member when unchecked', async () => {
    const preSelected: WizardMemberSelection[] = [
      { memberId: 'member-1', attendancePeriod: 'Complete', visitStartDate: null, visitEndDate: null, guardianName: null, guardianDocumentNumber: null }
    ]
    const wrapper = mountComponent(preSelected)
    const card = wrapper.find('[data-testid="member-label-member-1"]')
    await card.trigger('click')
    const emitted = wrapper.emitted('update:modelValue') as WizardMemberSelection[][]
    const emittedSelections = emitted[emitted.length - 1][0]
    expect(emittedSelections).toHaveLength(0)
  })

  it('should not show period selector when edition has only Complete period', () => {
    const selected: WizardMemberSelection[] = [
      { memberId: 'member-1', attendancePeriod: 'Complete', visitStartDate: null, visitEndDate: null, guardianName: null, guardianDocumentNumber: null }
    ]
    const wrapper = mountComponent(selected, mockEditionComplete)
    expect(wrapper.find('[data-testid="period-select-member-1"]').exists()).toBe(false)
  })

  it('should show period selector when member is selected and edition allows multiple periods', () => {
    const selected: WizardMemberSelection[] = [
      { memberId: 'member-1', attendancePeriod: 'Complete', visitStartDate: null, visitEndDate: null, guardianName: null, guardianDocumentNumber: null }
    ]
    const wrapper = mountComponent(selected, mockEditionWithPeriods)
    expect(wrapper.find('[data-testid="period-select-member-1"]').exists()).toBe(true)
  })

  it('should not show period selector for unselected members', () => {
    const wrapper = mountComponent([], mockEditionWithPeriods)
    expect(wrapper.find('[data-testid="period-select-member-1"]').exists()).toBe(false)
  })

  it('should show WeekendVisit date pickers when WeekendVisit period is selected', () => {
    const selected: WizardMemberSelection[] = [
      {
        memberId: 'member-1',
        attendancePeriod: 'WeekendVisit',
        visitStartDate: null,
        visitEndDate: null,
        guardianName: null,
        guardianDocumentNumber: null
      }
    ]
    const wrapper = mountComponent(selected, mockEditionWithPeriods)
    expect(wrapper.find('[data-testid="visit-start-member-1"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="visit-end-member-1"]').exists()).toBe(true)
  })

  it('should not show WeekendVisit date pickers when period is Complete', () => {
    const selected: WizardMemberSelection[] = [
      { memberId: 'member-1', attendancePeriod: 'Complete', visitStartDate: null, visitEndDate: null, guardianName: null, guardianDocumentNumber: null }
    ]
    const wrapper = mountComponent(selected, mockEditionWithPeriods)
    expect(wrapper.find('[data-testid="visit-start-member-1"]').exists()).toBe(false)
  })

  it('should not deselect member when clicking on the period selector area', async () => {
    const selected: WizardMemberSelection[] = [
      { memberId: 'member-1', attendancePeriod: 'Complete', visitStartDate: null, visitEndDate: null, guardianName: null, guardianDocumentNumber: null }
    ]
    const wrapper = mountComponent(selected, mockEditionWithPeriods)

    // Click on the period selector wrapper (has @click.stop)
    const periodSelect = wrapper.find('[data-testid="period-select-member-1"]')
    expect(periodSelect.exists()).toBe(true)
    await periodSelect.trigger('click')

    // Should NOT emit update:modelValue (member should remain selected)
    expect(wrapper.emitted('update:modelValue')).toBeFalsy()
  })

  describe('guardian pre-fill from first selected adult', () => {
    it('should pre-fill guardian fields when selecting a minor after an adult is already selected', async () => {
      // Adult (member-1) is already selected
      const preSelected: WizardMemberSelection[] = [
        { memberId: 'member-1', attendancePeriod: 'Complete', visitStartDate: null, visitEndDate: null, guardianName: null, guardianDocumentNumber: null }
      ]
      const wrapper = mountComponent(preSelected, mockEditionComplete)

      // Click minor (member-2) card
      const card = wrapper.find('[data-testid="member-label-member-2"]')
      await card.trigger('click')

      const emitted = wrapper.emitted('update:modelValue') as WizardMemberSelection[][]
      const lastEmit = emitted[emitted.length - 1][0]
      const minorSelection = (lastEmit as unknown as WizardMemberSelection[]).find(
        (s: WizardMemberSelection) => s.memberId === 'member-2'
      )

      expect(minorSelection).toBeDefined()
      expect(minorSelection!.guardianName).toBe('Juan García')
      expect(minorSelection!.guardianDocumentNumber).toBe('12345678A')
    })

    it('should NOT pre-fill guardian fields when no adult is selected', async () => {
      const wrapper = mountComponent([], mockEditionComplete)

      // Click minor (member-2) card directly
      const card = wrapper.find('[data-testid="member-label-member-2"]')
      await card.trigger('click')

      const emitted = wrapper.emitted('update:modelValue') as WizardMemberSelection[][]
      const lastEmit = emitted[emitted.length - 1][0]
      const minorSelection = (lastEmit as unknown as WizardMemberSelection[]).find(
        (s: WizardMemberSelection) => s.memberId === 'member-2'
      )

      expect(minorSelection).toBeDefined()
      expect(minorSelection!.guardianName).toBeNull()
      expect(minorSelection!.guardianDocumentNumber).toBeNull()
    })

    it('should backfill empty guardian fields on existing minors when first adult is selected', async () => {
      // Minor (member-2) already selected with empty guardian fields
      const preSelected: WizardMemberSelection[] = [
        { memberId: 'member-2', attendancePeriod: 'Complete', visitStartDate: null, visitEndDate: null, guardianName: null, guardianDocumentNumber: null }
      ]
      const wrapper = mountComponent(preSelected, mockEditionComplete)

      // Click adult (member-1) card
      const card = wrapper.find('[data-testid="member-label-member-1"]')
      await card.trigger('click')

      const emitted = wrapper.emitted('update:modelValue') as WizardMemberSelection[][]
      const lastEmit = emitted[emitted.length - 1][0]
      const minorSelection = (lastEmit as unknown as WizardMemberSelection[]).find(
        (s: WizardMemberSelection) => s.memberId === 'member-2'
      )

      expect(minorSelection).toBeDefined()
      expect(minorSelection!.guardianName).toBe('Juan García')
      expect(minorSelection!.guardianDocumentNumber).toBe('12345678A')
    })

    it('should NOT overwrite manually edited guardian fields when a new adult is selected', async () => {
      // Minor with manually edited guardian, plus one adult already selected
      const preSelected: WizardMemberSelection[] = [
        { memberId: 'member-1', attendancePeriod: 'Complete', visitStartDate: null, visitEndDate: null, guardianName: null, guardianDocumentNumber: null },
        { memberId: 'member-2', attendancePeriod: 'Complete', visitStartDate: null, visitEndDate: null, guardianName: 'Custom Tutor', guardianDocumentNumber: '99999999Z' }
      ]
      // Use extended members (includes member-3 as second adult)
      const wrapper = mountComponent(preSelected, mockEditionComplete, mockMembersExtended)

      // Select second adult (member-3) card
      const card = wrapper.find('[data-testid="member-label-member-3"]')
      await card.trigger('click')

      const emitted = wrapper.emitted('update:modelValue') as WizardMemberSelection[][]
      const lastEmit = emitted[emitted.length - 1][0]
      const minorSelection = (lastEmit as unknown as WizardMemberSelection[]).find(
        (s: WizardMemberSelection) => s.memberId === 'member-2'
      )

      expect(minorSelection).toBeDefined()
      // Should keep manual values, NOT overwrite
      expect(minorSelection!.guardianName).toBe('Custom Tutor')
      expect(minorSelection!.guardianDocumentNumber).toBe('99999999Z')
    })

    it('should use first adult by member list order when multiple adults are selected', async () => {
      // member-3 (María López) selected first, then member-1 (Juan García) — but member-1 appears first in list
      const preSelected: WizardMemberSelection[] = [
        { memberId: 'member-3', attendancePeriod: 'Complete', visitStartDate: null, visitEndDate: null, guardianName: null, guardianDocumentNumber: null },
        { memberId: 'member-1', attendancePeriod: 'Complete', visitStartDate: null, visitEndDate: null, guardianName: null, guardianDocumentNumber: null }
      ]
      const wrapper = mountComponent(preSelected, mockEditionComplete, mockMembersExtended)

      // Select minor (member-4) card
      const card = wrapper.find('[data-testid="member-label-member-4"]')
      await card.trigger('click')

      const emitted = wrapper.emitted('update:modelValue') as WizardMemberSelection[][]
      const lastEmit = emitted[emitted.length - 1][0]
      const minorSelection = (lastEmit as unknown as WizardMemberSelection[]).find(
        (s: WizardMemberSelection) => s.memberId === 'member-4'
      )

      expect(minorSelection).toBeDefined()
      // Should use member-1 (Juan García) since it appears first in the members list
      expect(minorSelection!.guardianName).toBe('Juan García')
      expect(minorSelection!.guardianDocumentNumber).toBe('12345678A')
    })

    it('should backfill multiple minors when first adult is selected', async () => {
      // Two minors selected, no adults yet
      const preSelected: WizardMemberSelection[] = [
        { memberId: 'member-2', attendancePeriod: 'Complete', visitStartDate: null, visitEndDate: null, guardianName: null, guardianDocumentNumber: null },
        { memberId: 'member-4', attendancePeriod: 'Complete', visitStartDate: null, visitEndDate: null, guardianName: null, guardianDocumentNumber: null }
      ]
      const wrapper = mountComponent(preSelected, mockEditionComplete, mockMembersExtended)

      // Select first adult (member-1) card
      const card = wrapper.find('[data-testid="member-label-member-1"]')
      await card.trigger('click')

      const emitted = wrapper.emitted('update:modelValue') as WizardMemberSelection[][]
      const lastEmit = emitted[emitted.length - 1][0] as unknown as WizardMemberSelection[]

      const minor2 = lastEmit.find((s: WizardMemberSelection) => s.memberId === 'member-2')
      const minor4 = lastEmit.find((s: WizardMemberSelection) => s.memberId === 'member-4')

      expect(minor2!.guardianName).toBe('Juan García')
      expect(minor2!.guardianDocumentNumber).toBe('12345678A')
      expect(minor4!.guardianName).toBe('Juan García')
      expect(minor4!.guardianDocumentNumber).toBe('12345678A')
    })
  })
})
