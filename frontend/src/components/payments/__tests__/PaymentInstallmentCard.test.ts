import { describe, it, expect, vi } from 'vitest'
import { shallowMount } from '@vue/test-utils'
import PrimeVue from 'primevue/config'
import PaymentInstallmentCard from '../PaymentInstallmentCard.vue'
import type { PaymentResponse } from '@/types/payment'

vi.mock('primevue/usetoast', () => ({
  useToast: () => ({ add: vi.fn() })
}))

vi.mock('@/composables/usePayments', () => ({
  usePayments: () => ({
    uploadProof: vi.fn(),
    removeProof: vi.fn(),
    loading: { value: false },
    error: { value: null }
  })
}))

const makePayment = (overrides: Partial<PaymentResponse> = {}): PaymentResponse => ({
  id: 'pay-1',
  registrationId: 'reg-1',
  installmentNumber: 1,
  amount: 225,
  dueDate: '2026-07-01T00:00:00Z',
  method: 'Transfer',
  status: 'Pending',
  transferConcept: 'CAMP-2026-GARCIA-1',
  proofFileUrl: null,
  proofFileName: null,
  proofUploadedAt: null,
  adminNotes: null,
  createdAt: '2026-03-06T00:00:00Z',
  ...overrides
})

const mount = (payment: PaymentResponse, showUpload = true) =>
  shallowMount(PaymentInstallmentCard, {
    props: { payment, showUpload },
    global: { plugins: [PrimeVue] }
  })

describe('PaymentInstallmentCard', () => {
  it('displays installment number and amount', () => {
    const wrapper = mount(makePayment())
    expect(wrapper.text()).toContain('Plazo 1')
    expect(wrapper.text()).toContain('225')
  })

  it('displays transfer concept', () => {
    const wrapper = mount(makePayment())
    expect(wrapper.text()).toContain('CAMP-2026-GARCIA-1')
  })

  it('renders ProofUploader when showUpload is true', () => {
    const wrapper = mount(makePayment(), true)
    expect(wrapper.findComponent({ name: 'ProofUploader' }).exists()).toBe(true)
  })

  it('hides ProofUploader when showUpload is false', () => {
    const wrapper = mount(makePayment(), false)
    expect(wrapper.findComponent({ name: 'ProofUploader' }).exists()).toBe(false)
  })

  it('shows "Inmediato" when dueDate is null', () => {
    const wrapper = mount(makePayment({ dueDate: null }))
    expect(wrapper.text()).toContain('Inmediato')
  })
})
