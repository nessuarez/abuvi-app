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
    documentNumber: null,
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
  edition: CampEdition = mockEditionComplete
) =>
  mount(RegistrationMemberSelector, {
    props: { members: mockMembers, modelValue, edition },
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
    const input = wrapper.find('input[type="checkbox"]')
    await input.trigger('change')
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
      { memberId: 'member-1', attendancePeriod: 'Complete', visitStartDate: null, visitEndDate: null }
    ]
    const wrapper = mountComponent(preSelected)
    const input = wrapper.find('input[type="checkbox"]')
    await input.trigger('change')
    const emitted = wrapper.emitted('update:modelValue') as WizardMemberSelection[][]
    const emittedSelections = emitted[emitted.length - 1][0]
    expect(emittedSelections).toHaveLength(0)
  })

  it('should show medical notes warning icon when hasMedicalNotes is true', () => {
    const wrapper = mountComponent()
    const medicalIcon = wrapper.find('[data-testid="medical-notes-icon"]')
    expect(medicalIcon.exists()).toBe(true)
    expect(medicalIcon.attributes('aria-label')).toBe('Tiene notas médicas')
  })

  it('should NOT expose actual medical note content in any rendered text', () => {
    const wrapper = mountComponent()
    const text = wrapper.text()
    expect(text).not.toContain('medicalNotes')
    expect(text).not.toContain('allergies')
    expect(text).not.toContain('nota médica')
    expect(text).not.toContain('alergia a')
  })

  it('should not show period selector when edition has only Complete period', () => {
    const selected: WizardMemberSelection[] = [
      { memberId: 'member-1', attendancePeriod: 'Complete', visitStartDate: null, visitEndDate: null }
    ]
    const wrapper = mountComponent(selected, mockEditionComplete)
    expect(wrapper.find('[data-testid="period-select-member-1"]').exists()).toBe(false)
  })

  it('should show period selector when member is selected and edition allows multiple periods', () => {
    const selected: WizardMemberSelection[] = [
      { memberId: 'member-1', attendancePeriod: 'Complete', visitStartDate: null, visitEndDate: null }
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
        visitEndDate: null
      }
    ]
    const wrapper = mountComponent(selected, mockEditionWithPeriods)
    expect(wrapper.find('[data-testid="visit-start-member-1"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="visit-end-member-1"]').exists()).toBe(true)
  })

  it('should not show WeekendVisit date pickers when period is Complete', () => {
    const selected: WizardMemberSelection[] = [
      { memberId: 'member-1', attendancePeriod: 'Complete', visitStartDate: null, visitEndDate: null }
    ]
    const wrapper = mountComponent(selected, mockEditionWithPeriods)
    expect(wrapper.find('[data-testid="visit-start-member-1"]').exists()).toBe(false)
  })
})
