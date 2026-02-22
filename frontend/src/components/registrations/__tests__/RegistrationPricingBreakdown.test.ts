import { describe, it, expect } from 'vitest'
import { mount } from '@vue/test-utils'
import PrimeVue from 'primevue/config'
import RegistrationPricingBreakdown from '@/components/registrations/RegistrationPricingBreakdown.vue'
import type { PricingBreakdown } from '@/types/registration'

const basePricing: PricingBreakdown = {
  members: [
    { familyMemberId: 'member-1', fullName: 'Ana García', ageAtCamp: 35, ageCategory: 'Adult', individualAmount: 450 },
    { familyMemberId: 'member-2', fullName: 'Luis García', ageAtCamp: 10, ageCategory: 'Child', individualAmount: 300 },
    { familyMemberId: 'member-3', fullName: 'Sofía García', ageAtCamp: 1, ageCategory: 'Baby', individualAmount: 0 }
  ],
  baseTotalAmount: 750,
  extras: [],
  extrasAmount: 0,
  totalAmount: 750
}

const mountComponent = (props: { pricing: PricingBreakdown }) =>
  mount(RegistrationPricingBreakdown, {
    props,
    global: { plugins: [PrimeVue] }
  })

describe('RegistrationPricingBreakdown', () => {
  it('should render all member rows with correct age categories', () => {
    const wrapper = mountComponent({ pricing: basePricing })

    expect(wrapper.text()).toContain('Ana García')
    expect(wrapper.text()).toContain('Adulto/Adulta')
    expect(wrapper.text()).toContain('Luis García')
    expect(wrapper.text()).toContain('Niño/Niña')
    expect(wrapper.text()).toContain('Sofía García')
    expect(wrapper.text()).toContain('Bebé')
  })

  it('should show extras section only when extras exist', () => {
    const wrapperNoExtras = mountComponent({ pricing: basePricing })
    expect(wrapperNoExtras.text()).not.toContain('Subtotal extras')

    const pricingWithExtras: PricingBreakdown = {
      ...basePricing,
      extras: [{
        campEditionExtraId: 'extra-1',
        name: 'Camiseta',
        unitPrice: 15,
        pricingType: 'PerPerson',
        pricingPeriod: 'OneTime',
        quantity: 2,
        campDurationDays: null,
        calculation: '15 × 2 personas = 30 €',
        totalAmount: 30
      }],
      extrasAmount: 30,
      totalAmount: 780
    }
    const wrapperWithExtras = mountComponent({ pricing: pricingWithExtras })
    expect(wrapperWithExtras.text()).toContain('Subtotal extras')
    expect(wrapperWithExtras.text()).toContain('Camiseta')
  })

  it('should display total amount correctly formatted', () => {
    const wrapper = mountComponent({ pricing: basePricing })
    expect(wrapper.text()).toContain('750')
    expect(wrapper.text()).toContain('Total inscripción')
  })

  it('should show zero extras amount when no extras selected', () => {
    const wrapper = mountComponent({ pricing: basePricing })
    expect(wrapper.text()).toContain('0')
    expect(wrapper.text()).toContain('Sin extras seleccionados')
  })
})
