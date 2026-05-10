import { describe, it, expect, vi, beforeEach } from 'vitest'
import { ref } from 'vue'
import { shallowMount } from '@vue/test-utils'
import PrimeVue from 'primevue/config'
import ConfirmCombinedPaymentsDialog from '../ConfirmCombinedPaymentsDialog.vue'
import type { AdminPaymentResponse } from '@/types/payment'

const mockConfirmCombinedPayments = vi.fn()
const mockToastAdd = vi.fn()
const mockLoading = ref(false)
const mockError = ref<string | null>(null)

vi.mock('primevue/usetoast', () => ({
  useToast: () => ({ add: mockToastAdd }),
}))

vi.mock('@/composables/usePayments', () => ({
  usePayments: () => ({
    confirmCombinedPayments: mockConfirmCombinedPayments,
    loading: mockLoading,
    error: mockError,
  }),
}))

const pay1: AdminPaymentResponse = {
  id: 'pay-1',
  registrationId: 'reg-1',
  installmentNumber: 1,
  amount: 100,
  dueDate: null,
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

const pay2: AdminPaymentResponse = {
  ...pay1,
  id: 'pay-2',
  installmentNumber: 2,
  amount: 150,
}

function mountDialog(payments = [pay1, pay2]) {
  return shallowMount(ConfirmCombinedPaymentsDialog, {
    props: {
      visible: true,
      registrationId: 'reg-1',
      familyUnitName: 'García',
      payments,
    },
    global: {
      plugins: [PrimeVue],
    },
  })
}

describe('ConfirmCombinedPaymentsDialog', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    mockLoading.value = false
    mockError.value = null
  })

  it('selects all payments by default on open', () => {
    const wrapper = mountDialog()
    expect((wrapper.vm as any).selectedPaymentIds).toEqual(['pay-1', 'pay-2'])
  })

  it('shows distribution preview when totalReceivedAmount is set', async () => {
    const wrapper = mountDialog()
    ;(wrapper.vm as any).totalReceivedAmount = 180
    await wrapper.vm.$nextTick()
    const preview = (wrapper.vm as any).distributionPreview
    expect(preview).toHaveLength(2)
    expect(preview[0].assignedAmount).toBe(100)
    expect(preview[1].assignedAmount).toBe(80)
  })

  it('greedy fill assigns full amount to first payment then remainder to second', () => {
    const wrapper = mountDialog()
    ;(wrapper.vm as any).totalReceivedAmount = 100
    const preview = (wrapper.vm as any).distributionPreview
    expect(preview[0].assignedAmount).toBe(100)
    expect(preview[1].assignedAmount).toBe(0)
  })

  it('computes surplus when total exceeds sum of selected payments', () => {
    const wrapper = mountDialog()
    ;(wrapper.vm as any).totalReceivedAmount = 300
    expect((wrapper.vm as any).surplus).toBe(50) // 300 - 100 - 150
  })

  it('surplus is zero when total is less than payment sum', () => {
    const wrapper = mountDialog()
    ;(wrapper.vm as any).totalReceivedAmount = 200
    expect((wrapper.vm as any).surplus).toBe(0)
  })

  it('calls confirmCombinedPayments with correct request', async () => {
    mockConfirmCombinedPayments.mockResolvedValue([pay1, pay2])
    const wrapper = mountDialog()
    ;(wrapper.vm as any).totalReceivedAmount = 250
    ;(wrapper.vm as any).applySurplusToNext = true
    ;(wrapper.vm as any).adminNotes = 'Test note'
    await (wrapper.vm as any).handleConfirm()
    expect(mockConfirmCombinedPayments).toHaveBeenCalledWith('reg-1', {
      paymentIds: ['pay-1', 'pay-2'],
      totalReceivedAmount: 250,
      applySurplusToNext: true,
      adminNotes: 'Test note',
    })
  })

  it('emits confirmed event with updated payments on success', async () => {
    const result = [pay1, pay2]
    mockConfirmCombinedPayments.mockResolvedValue(result)
    const wrapper = mountDialog()
    ;(wrapper.vm as any).totalReceivedAmount = 250
    await (wrapper.vm as any).handleConfirm()
    expect(wrapper.emitted('confirmed')?.[0]).toEqual([result])
  })

  it('emits update:visible false on success', async () => {
    mockConfirmCombinedPayments.mockResolvedValue([pay1, pay2])
    const wrapper = mountDialog()
    ;(wrapper.vm as any).totalReceivedAmount = 250
    await (wrapper.vm as any).handleConfirm()
    expect(wrapper.emitted('update:visible')?.[0]).toEqual([false])
  })

  it('does not call confirmCombinedPayments when totalReceivedAmount is null', async () => {
    const wrapper = mountDialog()
    ;(wrapper.vm as any).totalReceivedAmount = null
    await (wrapper.vm as any).handleConfirm()
    expect(mockConfirmCombinedPayments).not.toHaveBeenCalled()
  })

  it('resets form when dialog reopens', async () => {
    const wrapper = mountDialog()
    ;(wrapper.vm as any).totalReceivedAmount = 999
    ;(wrapper.vm as any).applySurplusToNext = true
    await wrapper.setProps({ visible: false })
    await wrapper.setProps({ visible: true })
    expect((wrapper.vm as any).totalReceivedAmount).toBeNull()
    expect((wrapper.vm as any).applySurplusToNext).toBe(false)
  })
})
