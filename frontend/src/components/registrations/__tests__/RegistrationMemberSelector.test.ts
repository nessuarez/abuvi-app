import { describe, it, expect } from 'vitest'
import { mount } from '@vue/test-utils'
import PrimeVue from 'primevue/config'
import RegistrationMemberSelector from '@/components/registrations/RegistrationMemberSelector.vue'
import type { FamilyMemberResponse } from '@/types/family-unit'
import { FamilyRelationship } from '@/types/family-unit'

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

const mountComponent = (modelValue: string[] = []) =>
  mount(RegistrationMemberSelector, {
    props: { members: mockMembers, modelValue },
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

  it('should emit update:modelValue when label is clicked', async () => {
    const wrapper = mountComponent([])
    // Click the native input inside PrimeVue Checkbox
    const input = wrapper.find('input[type="checkbox"]')
    await input.trigger('change')
    expect(wrapper.emitted('update:modelValue')).toBeTruthy()
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
    // Should not contain any indication of actual note content
    expect(text).not.toContain('medicalNotes')
    expect(text).not.toContain('allergies')
    expect(text).not.toContain('nota médica')
    expect(text).not.toContain('alergia a')
  })
})
