import { describe, it, expect, vi, beforeEach } from 'vitest'
import { ref } from 'vue'
import { shallowMount } from '@vue/test-utils'
import PrimeVue from 'primevue/config'
import AdminEditPaymentDialog from '../AdminEditPaymentDialog.vue'
import type { AdminPaymentResponse } from '@/types/payment'

const mockAdminEditPayment = vi.fn()
const mockToastAdd = vi.fn()
const mockLoading = ref(false)
const mockError = ref<string | null>(null)

vi.mock('primevue/usetoast', () => ({
  useToast: () => ({ add: mockToastAdd }),
}))

vi.mock('@/composables/usePayments', () => ({
  usePayments: () => ({
    adminEditPayment: mockAdminEditPayment,
    loading: mockLoading,
    error: mockError,
  }),
}))

vi.mock('@/utils/date', () => ({
  formatDateLocal: (d: Date) => d.toISOString().slice(0, 10),
  parseDateSafe: (s: string) => new Date(s),
}))

const pendingPayment: AdminPaymentResponse = {
  id: 'pay-1',
  registrationId: 'reg-1',
  installmentNumber: 1,
  amount: 150,
  dueDate: '2026-06-01',
  method: 'BankTransfer',
  status: 'Pending',
  transferConcept: null,
  proofFileUrl: null,
  proofFileName: null,
  proofUploadedAt: null,
  adminNotes: null,
  createdAt: '2026-01-01T00:00:00Z',
  isActionable: true,
  isManual: false,
  conceptLines: null,
  extraConceptLines: null,
  manualConceptLine: null,
  familyUnitName: 'García',
  campEditionName: 'Camp 2026',
  confirmedByUserName: null,
  confirmedAt: null,
  conceptOverridden: false,
  originalAmount: null,
}

const completedWithOverride: AdminPaymentResponse = {
  ...pendingPayment,
  id: 'pay-2',
  status: 'Completed',
  conceptOverridden: true,
  originalAmount: 200,
  amount: 150,
}

function mountDialog(payment: AdminPaymentResponse = pendingPayment) {
  return shallowMount(AdminEditPaymentDialog, {
    props: { visible: true, payment },
    global: {
      plugins: [PrimeVue],
    },
  })
}

describe('AdminEditPaymentDialog', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    mockLoading.value = false
    mockError.value = null
  })

  it('isCompleted is false for Pending payment', () => {
    const wrapper = mountDialog(pendingPayment)
    expect((wrapper.vm as any).isCompleted).toBe(false)
  })

  it('isCompleted is true for Completed payment', () => {
    const wrapper = mountDialog(completedWithOverride)
    expect((wrapper.vm as any).isCompleted).toBe(true)
  })

  it('payment.conceptOverridden is true for overridden payment', () => {
    const wrapper = mountDialog(completedWithOverride)
    expect((wrapper.vm as any).payment.conceptOverridden).toBe(true)
    expect((wrapper.vm as any).payment.originalAmount).toBe(200)
  })

  it('payment.conceptOverridden is false for non-overridden payment', () => {
    const wrapper = mountDialog(pendingPayment)
    expect((wrapper.vm as any).payment.conceptOverridden).toBe(false)
  })

  it('calls adminEditPayment with changed amount on save', async () => {
    mockAdminEditPayment.mockResolvedValue({ ...pendingPayment, amount: 200 })
    const wrapper = mountDialog(pendingPayment)
    ;(wrapper.vm as any).amount = 200
    await (wrapper.vm as any).handleSave()
    expect(mockAdminEditPayment).toHaveBeenCalledWith(
      'pay-1',
      expect.objectContaining({ amount: 200 })
    )
  })

  it('does NOT include amount in request when amount unchanged', async () => {
    mockAdminEditPayment.mockResolvedValue(pendingPayment)
    const wrapper = mountDialog(pendingPayment)
    ;(wrapper.vm as any).amount = 150
    await (wrapper.vm as any).handleSave()
    expect(mockAdminEditPayment).toHaveBeenCalledWith(
      'pay-1',
      expect.not.objectContaining({ amount: expect.anything() })
    )
  })

  it('emits saved event with updated payment on success', async () => {
    const updated = { ...pendingPayment, amount: 200 }
    mockAdminEditPayment.mockResolvedValue(updated)
    const wrapper = mountDialog(pendingPayment)
    ;(wrapper.vm as any).amount = 200
    await (wrapper.vm as any).handleSave()
    expect(wrapper.emitted('saved')?.[0]).toEqual([updated])
  })

  it('emits update:visible false on success', async () => {
    mockAdminEditPayment.mockResolvedValue({ ...pendingPayment })
    const wrapper = mountDialog(pendingPayment)
    await (wrapper.vm as any).handleSave()
    expect(wrapper.emitted('update:visible')?.[0]).toEqual([false])
  })

  it('error ref reflects mock error value', () => {
    mockError.value = 'No se puede editar un pago fallido'
    const wrapper = mountDialog(pendingPayment)
    expect(mockError.value).toBe('No se puede editar un pago fallido')
    mockError.value = null
  })

  it('resets form when dialog reopens', async () => {
    const wrapper = mountDialog(pendingPayment)
    ;(wrapper.vm as any).amount = 999
    ;(wrapper.vm as any).overrideConceptChecked = true
    await wrapper.setProps({ visible: false })
    await wrapper.setProps({ visible: true })
    expect((wrapper.vm as any).amount).toBe(150)
    expect((wrapper.vm as any).overrideConceptChecked).toBe(false)
  })
})
