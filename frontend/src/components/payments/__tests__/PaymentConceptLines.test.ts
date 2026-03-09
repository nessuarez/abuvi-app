import { describe, it, expect } from 'vitest'
import { mount } from '@vue/test-utils'
import PrimeVue from 'primevue/config'
import PaymentConceptLines from '../PaymentConceptLines.vue'
import type { PaymentConceptLine, PaymentExtraConceptLine } from '@/types/payment'

const memberLines: PaymentConceptLine[] = [
  {
    personFullName: 'Juan García',
    ageCategory: 'Adulto',
    attendancePeriod: 'Completo',
    individualAmount: 500,
    amountInPayment: 250,
    percentage: 50
  },
  {
    personFullName: 'María García',
    ageCategory: 'Adulto',
    attendancePeriod: '1ª Semana',
    individualAmount: 300,
    amountInPayment: 150,
    percentage: 50
  },
  {
    personFullName: 'Pablo García',
    ageCategory: 'Niño',
    attendancePeriod: 'Completo',
    individualAmount: 300,
    amountInPayment: 150,
    percentage: 50
  }
]

const extraLines: PaymentExtraConceptLine[] = [
  {
    extraName: 'Camiseta',
    quantity: 3,
    unitPrice: 15,
    totalAmount: 45,
    userInput: 'Talla M x2, Talla S x1',
    pricingType: 'PerPerson'
  },
  {
    extraName: 'Parking',
    quantity: 1,
    unitPrice: 20,
    totalAmount: 20,
    userInput: null,
    pricingType: 'PerFamily'
  }
]

const mountComponent = (props: {
  conceptLines?: PaymentConceptLine[] | null
  extraConceptLines?: PaymentExtraConceptLine[] | null
}) =>
  mount(PaymentConceptLines, {
    props: {
      conceptLines: props.conceptLines ?? null,
      extraConceptLines: props.extraConceptLines ?? null
    },
    global: { plugins: [PrimeVue] }
  })

describe('PaymentConceptLines', () => {
  it('renders nothing when both are null', () => {
    const wrapper = mountComponent({})
    expect(wrapper.text()).toBe('')
  })

  it('renders nothing when both are empty arrays', () => {
    const wrapper = mountComponent({ conceptLines: [], extraConceptLines: [] })
    expect(wrapper.text()).toBe('')
  })

  it('starts collapsed by default', () => {
    const wrapper = mountComponent({ conceptLines: memberLines })
    expect(wrapper.text()).toContain('Detalle del pago')
    expect(wrapper.text()).not.toContain('Juan García')
  })

  it('expands on click to show member lines', async () => {
    const wrapper = mountComponent({ conceptLines: memberLines })
    await wrapper.find('button').trigger('click')
    expect(wrapper.text()).toContain('Juan García')
    expect(wrapper.text()).toContain('María García')
    expect(wrapper.text()).toContain('Pablo García')
    expect(wrapper.text()).toContain('Adulto')
    expect(wrapper.text()).toContain('Niño')
    expect(wrapper.text()).toContain('Completo')
    expect(wrapper.text()).toContain('1ª Semana')
  })

  it('shows correct member total', async () => {
    const wrapper = mountComponent({ conceptLines: memberLines })
    await wrapper.find('button').trigger('click')
    // Total: 250 + 150 + 150 = 550
    expect(wrapper.text()).toContain('550')
  })

  it('shows percentage for member lines', async () => {
    const wrapper = mountComponent({ conceptLines: memberLines })
    await wrapper.find('button').trigger('click')
    expect(wrapper.text()).toContain('50%')
  })

  it('renders extra concept lines', async () => {
    const wrapper = mountComponent({ extraConceptLines: extraLines })
    await wrapper.find('button').trigger('click')
    expect(wrapper.text()).toContain('Camiseta')
    expect(wrapper.text()).toContain('Parking')
    expect(wrapper.text()).toContain('por persona')
    expect(wrapper.text()).toContain('por familia')
    expect(wrapper.text()).toContain('x3')
    expect(wrapper.text()).toContain('x1')
  })

  it('shows user input for extras when present', async () => {
    const wrapper = mountComponent({ extraConceptLines: extraLines })
    await wrapper.find('button').trigger('click')
    expect(wrapper.text()).toContain('Talla M x2, Talla S x1')
  })

  it('shows correct extras total', async () => {
    const wrapper = mountComponent({ extraConceptLines: extraLines })
    await wrapper.find('button').trigger('click')
    // Total: 45 + 20 = 65
    expect(wrapper.text()).toContain('65')
  })

  it('renders both member and extra lines together', async () => {
    const wrapper = mountComponent({ conceptLines: memberLines, extraConceptLines: extraLines })
    await wrapper.find('button').trigger('click')
    expect(wrapper.text()).toContain('Juan García')
    expect(wrapper.text()).toContain('Camiseta')
  })
})
