import { describe, it, expect, beforeAll } from 'vitest'
import { mount } from '@vue/test-utils'
import PrimeVue from 'primevue/config'
import ToastService from 'primevue/toastservice'
import FamilyMemberForm from '../FamilyMemberForm.vue'
import { FamilyRelationship } from '@/types/family-unit'

beforeAll(() => {
  global.ResizeObserver = class ResizeObserver {
    observe() {}
    unobserve() {}
    disconnect() {}
  }
})

const globalConfig = {
  plugins: [[PrimeVue, { unstyled: true }], [ToastService]] as [unknown, unknown],
}

describe('FamilyMemberForm — email hint', () => {
  it('renders email hint text', () => {
    const wrapper = mount(FamilyMemberForm, {
      props: { loading: false },
      global: globalConfig,
    })
    expect(wrapper.find('[data-testid="email-hint"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="email-hint"]').text()).toContain(
      'registrarse en la plataforma'
    )
  })
})

describe('FamilyMemberForm — DNI required for adults', () => {
  it('does not show DNI required hint when no date of birth is set', () => {
    const wrapper = mount(FamilyMemberForm, {
      props: { loading: false },
      global: globalConfig,
    })
    expect(wrapper.find('[data-testid="dni-required-hint"]').exists()).toBe(false)
  })

  it('shows DNI required hint when editing an adult member', () => {
    const adultDob = '1990-05-15'
    const wrapper = mount(FamilyMemberForm, {
      props: {
        member: {
          id: 'm1',
          familyUnitId: 'f1',
          firstName: 'Juan',
          lastName: 'García',
          dateOfBirth: adultDob,
          relationship: FamilyRelationship.Parent,
          documentNumber: null,
          email: null,
          phone: null,
          hasMedicalNotes: false,
          hasAllergies: false,
          userId: null,
          createdAt: '2026-01-01T00:00:00Z',
          updatedAt: '2026-01-01T00:00:00Z',
        },
        loading: false,
      },
      global: globalConfig,
    })
    expect(wrapper.find('[data-testid="dni-required-hint"]').exists()).toBe(true)
  })

  it('does not show DNI required hint when editing a minor member', () => {
    // A child born 5 years ago
    const now = new Date()
    const minorYear = now.getFullYear() - 5
    const minorDob = `${minorYear}-06-15`
    const wrapper = mount(FamilyMemberForm, {
      props: {
        member: {
          id: 'm2',
          familyUnitId: 'f1',
          firstName: 'Lucia',
          lastName: 'García',
          dateOfBirth: minorDob,
          relationship: FamilyRelationship.Child,
          documentNumber: null,
          email: null,
          phone: null,
          hasMedicalNotes: false,
          hasAllergies: false,
          userId: null,
          createdAt: '2026-01-01T00:00:00Z',
          updatedAt: '2026-01-01T00:00:00Z',
        },
        loading: false,
      },
      global: globalConfig,
    })
    expect(wrapper.find('[data-testid="dni-required-hint"]').exists()).toBe(false)
  })

  it('shows required asterisk on document number label for adult member', () => {
    const wrapper = mount(FamilyMemberForm, {
      props: {
        member: {
          id: 'm1',
          familyUnitId: 'f1',
          firstName: 'Juan',
          lastName: 'García',
          dateOfBirth: '1990-05-15',
          relationship: FamilyRelationship.Parent,
          documentNumber: null,
          email: null,
          phone: null,
          hasMedicalNotes: false,
          hasAllergies: false,
          userId: null,
          createdAt: '2026-01-01T00:00:00Z',
          updatedAt: '2026-01-01T00:00:00Z',
        },
        loading: false,
      },
      global: globalConfig,
    })
    const docLabel = wrapper.find('label[for="document-number"]')
    expect(docLabel.text()).toContain('*')
  })
})
