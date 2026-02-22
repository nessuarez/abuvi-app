import { describe, it, expect } from 'vitest'
import { mount } from '@vue/test-utils'
import PrimeVue from 'primevue/config'
import RegistrationCard from '@/components/registrations/RegistrationCard.vue'
import type { RegistrationResponse } from '@/types/registration'

const mockRegistration: RegistrationResponse = {
  id: 'reg-1',
  familyUnit: { id: 'fu-1', name: 'Familia García', representativeUserId: 'user-1' },
  campEdition: {
    id: 'edition-1',
    campName: 'Campamento ABUVI',
    year: 2026,
    startDate: '2026-07-01',
    endDate: '2026-07-15',
    location: 'Montaña Norte'
  },
  status: 'Pending',
  notes: null,
  pricing: {
    members: [
      { familyMemberId: 'member-1', fullName: 'Juan García', ageAtCamp: 35, ageCategory: 'Adult', individualAmount: 450 }
    ],
    baseTotalAmount: 450,
    extras: [],
    extrasAmount: 0,
    totalAmount: 450
  },
  payments: [],
  amountPaid: 0,
  amountRemaining: 450,
  createdAt: '2026-02-01T00:00:00Z',
  updatedAt: '2026-02-01T00:00:00Z'
}

const mountComponent = (props: { registration: RegistrationResponse }) =>
  mount(RegistrationCard, {
    props,
    global: { plugins: [PrimeVue] }
  })

describe('RegistrationCard', () => {
  it('should render camp name and year', () => {
    const wrapper = mountComponent({ registration: mockRegistration })
    expect(wrapper.text()).toContain('Campamento ABUVI')
    expect(wrapper.text()).toContain('2026')
  })

  it('should display status badge', () => {
    const wrapper = mountComponent({ registration: mockRegistration })
    const badge = wrapper.find('[data-testid="registration-status"]')
    expect(badge.exists()).toBe(true)
    expect(badge.text()).toBe('Pendiente')
  })

  it('should emit view event with registration id on button click', async () => {
    const wrapper = mountComponent({ registration: mockRegistration })
    const btn = wrapper.find('[data-testid="view-registration-btn"]')
    await btn.trigger('click')
    expect(wrapper.emitted('view')).toHaveLength(1)
    expect(wrapper.emitted('view')![0]).toEqual(['reg-1'])
  })
})
