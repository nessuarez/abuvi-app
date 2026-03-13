import { describe, it, expect, vi, beforeEach } from 'vitest'
import { ref } from 'vue'
import { shallowMount } from '@vue/test-utils'
import PrimeVue from 'primevue/config'
import PaymentsAllList from '../PaymentsAllList.vue'

const mockToastAdd = vi.fn()
const mockGetAllPayments = vi.fn()
const mockUpdateManualPayment = vi.fn()
const mockDeleteManualPayment = vi.fn()
const mockFetchAllEditions = vi.fn()

const mockLoading = ref(false)
const mockError = ref<string | null>(null)
const mockAllEditions = ref<{ id: string; name: string; year: number }[]>([])

vi.mock('primevue/usetoast', () => ({
  useToast: () => ({ add: mockToastAdd }),
}))

vi.mock('@/composables/usePayments', () => ({
  usePayments: () => ({
    getAllPayments: mockGetAllPayments,
    updateManualPayment: mockUpdateManualPayment,
    deleteManualPayment: mockDeleteManualPayment,
    loading: mockLoading,
    error: mockError,
  }),
}))

vi.mock('@/composables/useCampEditions', () => ({
  useCampEditions: () => ({
    allEditions: mockAllEditions,
    fetchAllEditions: mockFetchAllEditions,
  }),
}))

vi.mock('@/utils/date', () => ({
  formatDateLocal: (d: Date) => d.toISOString().slice(0, 10),
}))

function mountComponent() {
  return shallowMount(PaymentsAllList, {
    global: {
      plugins: [PrimeVue],
      stubs: {
        SelectButton: {
          template: '<div data-testid="select-button"><slot /></div>',
          props: ['modelValue', 'options'],
        },
      },
    },
  })
}

describe('PaymentsAllList', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    mockLoading.value = false
    mockError.value = null
    mockAllEditions.value = []
    mockGetAllPayments.mockResolvedValue({
      items: [],
      totalCount: 0,
      page: 1,
      pageSize: 20,
    })
  })

  it('calls getAllPayments on mount', async () => {
    mountComponent()
    // onMounted triggers fetchPayments
    expect(mockGetAllPayments).toHaveBeenCalledWith(
      expect.objectContaining({ page: 1, pageSize: 20 })
    )
  })

  it('calls getAllPayments without installmentNumber when selectedInstallment is null', async () => {
    mountComponent()
    const callArgs = mockGetAllPayments.mock.calls[0][0]
    expect(callArgs).not.toHaveProperty('installmentNumber')
  })

  it('calls fetchAllEditions on mount', () => {
    mountComponent()
    expect(mockFetchAllEditions).toHaveBeenCalled()
  })

  it('does not send date params when filterMode is installment', async () => {
    // Default filterMode is 'installment', so date params should not be sent
    mountComponent()
    const callArgs = mockGetAllPayments.mock.calls[0][0]
    expect(callArgs).not.toHaveProperty('fromDate')
    expect(callArgs).not.toHaveProperty('toDate')
  })
})
