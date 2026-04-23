import { describe, it, expect, vi, beforeEach } from 'vitest'
import { mount } from '@vue/test-utils'
import { createPinia } from 'pinia'
import { ref, nextTick } from 'vue'
import RegistrationDetailPage from '../registrations/RegistrationDetailPage.vue'
import type { RegistrationResponse } from '@/types/registration'

// ── Auth mock ─────────────────────────────────────────────────────────────────
const authMock = vi.hoisted(() => ({
  user: { id: 'u1' },
  isAdmin: false,
  isBoard: false,
}))

vi.mock('@/stores/auth', () => ({ useAuthStore: () => authMock }))

// ── Router mock ───────────────────────────────────────────────────────────────
const routerPushMock = vi.fn()
const routeQueryMock = ref<Record<string, string>>({})

vi.mock('vue-router', () => ({
  useRouter: () => ({ push: routerPushMock }),
  useRoute: () => ({
    params: { id: 'reg-1' },
    query: routeQueryMock.value,
  }),
}))

// ── Composable mocks ──────────────────────────────────────────────────────────
const registrationMock = ref<RegistrationResponse | null>(null)

vi.mock('@/composables/useRegistrations', () => ({
  useRegistrations: () => ({
    registration: registrationMock,
    loading: ref(false),
    error: ref(null),
    getRegistrationById: vi.fn(),
    updateMembers: vi.fn(),
    setExtras: vi.fn(),
    updateInfo: vi.fn(),
    cancelRegistration: vi.fn(),
    deleteRegistration: vi.fn(),
    getAccommodationPreferences: vi.fn(),
  }),
}))

vi.mock('@/composables/usePayments', () => ({
  usePayments: () => ({
    getRegistrationPayments: vi.fn().mockResolvedValue([]),
    getPaymentSettings: vi.fn().mockResolvedValue(null),
  }),
}))

vi.mock('@/composables/useFamilyUnits', () => ({
  useFamilyUnits: () => ({
    getFamilyMembers: vi.fn().mockResolvedValue([]),
  }),
}))

vi.mock('@/composables/useCampEditions', () => ({
  useCampEditions: () => ({
    getEditionById: vi.fn().mockResolvedValue(null),
  }),
}))

vi.mock('@/utils/api', () => ({ api: { get: vi.fn(), post: vi.fn(), put: vi.fn(), delete: vi.fn() } }))
vi.mock('primevue/usetoast', () => ({ useToast: () => ({ add: vi.fn() }) }))

// ── Minimal registration fixture ──────────────────────────────────────────────
const makeRegistration = (): RegistrationResponse => ({
  id: 'reg-1',
  status: 'Pending',
  notes: null,
  specialNeeds: null,
  campatesPreference: null,
  hasPet: false,
  amountPaid: 0,
  amountRemaining: 500,
  createdAt: '2026-01-01T00:00:00Z',
  updatedAt: '2026-01-01T00:00:00Z',
  familyUnit: { id: 'fu-1', name: 'García', representativeUserId: 'u1' },
  campEdition: {
    id: 'ed-1',
    campName: 'Test Camp',
    year: 2026,
    startDate: '2026-07-01T00:00:00Z',
    endDate: '2026-07-15T00:00:00Z',
    location: 'Sierra Norte',
    duration: 14,
  },
  pricing: {
    members: [],
    baseTotalAmount: 500,
    extras: [],
    extrasAmount: 0,
    totalAmount: 500,
  },
  payments: [],
})

// ── Global stubs ──────────────────────────────────────────────────────────────
const globalStubs = {
  Button: {
    template: '<button @click="$emit(\'click\')" :aria-label="ariaLabel"><slot /></button>',
    props: ['icon', 'severity', 'text', 'label', 'disabled', 'ariaLabel'],
    emits: ['click'],
  },
  ProgressSpinner: true,
  Message: { template: '<div><slot /></div>', props: ['severity', 'closable'] },
  Container: { template: '<div><slot /></div>' },
  RegistrationStatusBadge: true,
  RegistrationPricingBreakdown: true,
  RegistrationMemberSelector: true,
  RegistrationExtrasSelector: true,
  RegistrationCancelDialog: true,
  RegistrationDeleteDialog: true,
  BankTransferInstructions: true,
  PaymentInstallmentCard: true,
  ManualPaymentDialog: true,
}

const mountPage = () =>
  mount(RegistrationDetailPage, {
    global: {
      plugins: [createPinia()],
      stubs: globalStubs,
    },
  })

// ── Tests ─────────────────────────────────────────────────────────────────────
describe('RegistrationDetailPage — back navigation', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    routeQueryMock.value = {}
    registrationMock.value = makeRegistration()
  })

  it('navigates to registrations when no returnTo param', async () => {
    routeQueryMock.value = {}
    const wrapper = mountPage()
    await nextTick()

    const backBtn = wrapper.find('[aria-label="Volver a mis inscripciones"]')
    expect(backBtn.exists()).toBe(true)
    await backBtn.trigger('click')

    expect(routerPushMock).toHaveBeenCalledWith({ name: 'registrations' })
  })

  it('navigates to admin-registrations when returnTo=admin-registrations', async () => {
    routeQueryMock.value = { returnTo: 'admin-registrations' }
    const wrapper = mountPage()
    await nextTick()

    const backBtn = wrapper.find('[aria-label="Volver a inscripciones"]')
    expect(backBtn.exists()).toBe(true)
    await backBtn.trigger('click')

    expect(routerPushMock).toHaveBeenCalledWith({ name: 'admin-registrations' })
  })

  it('renders default aria-label when no returnTo param', async () => {
    routeQueryMock.value = {}
    const wrapper = mountPage()
    await nextTick()

    expect(wrapper.find('[aria-label="Volver a mis inscripciones"]').exists()).toBe(true)
  })

  it('renders admin aria-label when returnTo=admin-registrations', async () => {
    routeQueryMock.value = { returnTo: 'admin-registrations' }
    const wrapper = mountPage()
    await nextTick()

    expect(wrapper.find('[aria-label="Volver a inscripciones"]').exists()).toBe(true)
  })
})
