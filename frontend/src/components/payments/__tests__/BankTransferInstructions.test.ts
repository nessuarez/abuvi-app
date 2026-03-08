import { describe, it, expect, vi } from 'vitest'
import { shallowMount } from '@vue/test-utils'
import PrimeVue from 'primevue/config'
import BankTransferInstructions from '../BankTransferInstructions.vue'

vi.mock('primevue/usetoast', () => ({
  useToast: () => ({ add: vi.fn() })
}))

const defaultProps = {
  iban: 'ES1234567890123456789012',
  bankName: 'Banco Test',
  accountHolder: 'Asociación ABUVI'
}

const mount = (props = {}) =>
  shallowMount(BankTransferInstructions, {
    props: { ...defaultProps, ...props },
    global: { plugins: [PrimeVue] }
  })

describe('BankTransferInstructions', () => {
  it('displays IBAN formatted with spaces', () => {
    const wrapper = mount()
    expect(wrapper.text()).toContain('ES12 3456 7890 1234 5678 9012')
  })

  it('displays bank name and account holder', () => {
    const wrapper = mount()
    expect(wrapper.text()).toContain('Banco Test')
    expect(wrapper.text()).toContain('Asociación ABUVI')
  })

  it('displays amount when provided', () => {
    const wrapper = mount({ amount: 225 })
    expect(wrapper.text()).toContain('225')
  })

  it('displays transfer concept when provided', () => {
    const wrapper = mount({ transferConcept: 'CAMP-GAR-1' })
    expect(wrapper.text()).toContain('CAMP-GAR-1')
  })

  it('does not show amount when not provided', () => {
    const wrapper = mount()
    expect(wrapper.text()).not.toContain('Importe')
  })
})
