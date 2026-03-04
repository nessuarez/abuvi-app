import { describe, it, expect } from 'vitest'
import { mount } from '@vue/test-utils'
import PrimeVue from 'primevue/config'
import RegistrationPricingBreakdown from '@/components/registrations/RegistrationPricingBreakdown.vue'
import type { PricingBreakdown, MemberPricingDetail } from '@/types/registration'

const baseMember: MemberPricingDetail = {
  familyMemberId: 'member-1',
  fullName: 'Ana García',
  ageAtCamp: 35,
  ageCategory: 'Adult',
  attendancePeriod: 'Complete',
  attendanceDays: 14,
  visitStartDate: null,
  visitEndDate: null,
  individualAmount: 450,
  guardianName: null,
  guardianDocumentNumber: null
}

const basePricing: PricingBreakdown = {
  members: [
    baseMember,
    {
      familyMemberId: 'member-2',
      fullName: 'Luis García',
      ageAtCamp: 10,
      ageCategory: 'Child',
      attendancePeriod: 'Complete',
      attendanceDays: 14,
      visitStartDate: null,
      visitEndDate: null,
      individualAmount: 300,
      guardianName: null,
      guardianDocumentNumber: null
    },
    {
      familyMemberId: 'member-3',
      fullName: 'Sofía García',
      ageAtCamp: 1,
      ageCategory: 'Baby',
      attendancePeriod: 'Complete',
      attendanceDays: 14,
      visitStartDate: null,
      visitEndDate: null,
      individualAmount: 0,
      guardianName: null,
      guardianDocumentNumber: null
    }
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
      extras: [
        {
          campEditionExtraId: 'extra-1',
          name: 'Camiseta',
          unitPrice: 15,
          pricingType: 'PerPerson',
          pricingPeriod: 'OneTime',
          quantity: 2,
          campDurationDays: null,
          calculation: '15 × 2 personas = 30 €',
          totalAmount: 30
        }
      ],
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

  it('should NOT show period column when all members have Complete period', () => {
    const wrapper = mountComponent({ pricing: basePricing })
    expect(wrapper.find('[data-testid="member-period-member-1"]').exists()).toBe(false)
  })

  it('should show period column when any member has a non-Complete period', () => {
    const pricingWithPeriods: PricingBreakdown = {
      ...basePricing,
      members: [
        { ...baseMember, attendancePeriod: 'FirstWeek', attendanceDays: 7 },
        {
          familyMemberId: 'member-2',
          fullName: 'Luis García',
          ageAtCamp: 10,
          ageCategory: 'Child',
          attendancePeriod: 'Complete',
          attendanceDays: 14,
          visitStartDate: null,
          visitEndDate: null,
          individualAmount: 300,
          guardianName: null,
          guardianDocumentNumber: null
        }
      ]
    }
    const wrapper = mountComponent({ pricing: pricingWithPeriods })
    expect(wrapper.find('[data-testid="member-period-member-1"]').exists()).toBe(true)
    expect(wrapper.text()).toContain('Primera semana')
  })

  it('should show visit dates for WeekendVisit members', () => {
    const pricingWithWeekend: PricingBreakdown = {
      ...basePricing,
      members: [
        {
          ...baseMember,
          attendancePeriod: 'WeekendVisit',
          attendanceDays: 2,
          visitStartDate: '2025-07-05',
          visitEndDate: '2025-07-07'
        }
      ]
    }
    const wrapper = mountComponent({ pricing: pricingWithWeekend })
    expect(wrapper.find('[data-testid="member-period-member-1"]').exists()).toBe(true)
    expect(wrapper.text()).toContain('Visita de fin de semana')
    expect(wrapper.text()).toContain('2025-07-05')
  })

  it('should show guardian info for members with guardianName set', () => {
    const pricingWithGuardian: PricingBreakdown = {
      ...basePricing,
      members: [
        {
          ...baseMember,
          familyMemberId: 'child-1',
          fullName: 'Pau Garcia',
          ageAtCamp: 8,
          ageCategory: 'Child',
          individualAmount: 300,
          guardianName: 'Ana Garcia',
          guardianDocumentNumber: '12345678A'
        }
      ],
      baseTotalAmount: 300,
      totalAmount: 300
    }
    const wrapper = mountComponent({ pricing: pricingWithGuardian })
    expect(wrapper.find('[data-testid="guardian-info-child-1"]').exists()).toBe(true)
    expect(wrapper.text()).toContain('Tutor/a: Ana Garcia')
    expect(wrapper.text()).toContain('12345678A')
  })

  it('should NOT show guardian info when guardianName is null', () => {
    const wrapper = mountComponent({ pricing: basePricing })
    expect(wrapper.find('[data-testid="guardian-info-member-1"]').exists()).toBe(false)
  })
})
