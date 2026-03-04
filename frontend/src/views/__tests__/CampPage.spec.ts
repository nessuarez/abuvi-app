import { describe, it, expect, vi, beforeEach } from 'vitest'
import { mount } from '@vue/test-utils'
import { createPinia } from 'pinia'
import { ref, nextTick } from 'vue'
import CampPage from '../CampPage.vue'
import type { CurrentCampEditionResponse } from '@/types/camp-edition'

// ── Auth mock ─────────────────────────────────────────────────────────────────
const authMock = vi.hoisted(() => ({
  user: { id: 'u1', role: 'Member' },
  isBoard: false,
  isAdmin: false,
}))

vi.mock('@/stores/auth', () => ({ useAuthStore: () => authMock }))

// ── Router mock ───────────────────────────────────────────────────────────────
const routerPushMock = vi.fn()
vi.mock('vue-router', () => ({
  useRouter: () => ({ push: routerPushMock }),
  RouterLink: { template: '<a><slot /></a>' },
}))

// ── Composable mocks ─────────────────────────────────────────────────────────
const mockFetchCurrentCampEdition = vi.fn()
const mockGetCurrentUserFamilyUnit = vi.fn()

const currentCampEdition = ref<CurrentCampEditionResponse | null>(null)
const loading = ref(false)
const error = ref<string | null>(null)
const familyUnit = ref<{ representativeUserId: string } | null>(null)

vi.mock('@/composables/useCampEditions', () => ({
  useCampEditions: () => ({
    currentCampEdition,
    loading,
    error,
    fetchCurrentCampEdition: mockFetchCurrentCampEdition,
  }),
}))

vi.mock('@/composables/useFamilyUnits', () => ({
  useFamilyUnits: () => ({
    familyUnit,
    getCurrentUserFamilyUnit: mockGetCurrentUserFamilyUnit,
  }),
}))

// ── PrimeVue stubs ────────────────────────────────────────────────────────────
const globalStubs = {
  ProgressSpinner: true,
  Message: { template: '<div><slot /></div>', props: ['severity', 'closable'] },
  Button: {
    template: '<button @click="$emit(\'click\')"><slot /></button>',
    props: ['label', 'disabled', 'size', 'severity', 'icon'],
    emits: ['click'],
  },
  ProgressBar: true,
  CampEditionStatusBadge: true,
  CampPlacesGallery: true,
  CampLocationMap: true,
  AccommodationCapacityDisplay: true,
  PricingBreakdown: true,
  CampExtrasSection: true,
  Container: { template: '<div><slot /></div>' },
  RouterLink: { template: '<a><slot /></a>', props: ['to'] },
}

// ── Factory helper ────────────────────────────────────────────────────────────
const makeEdition = (
  overrides: Partial<CurrentCampEditionResponse> = {},
): CurrentCampEditionResponse => ({
  id: 'edition-1',
  campId: 'camp-1',
  campName: 'Test Camp',
  campLocation: 'Sierra Norte',
  campFormattedAddress: 'Calle Test, Madrid',
  campLatitude: 40.4,
  campLongitude: -3.7,
  year: 2026,
  startDate: '2026-07-01T00:00:00Z',
  endDate: '2026-07-15T00:00:00Z',
  pricePerAdult: 450,
  pricePerChild: 300,
  pricePerBaby: 0,
  useCustomAgeRanges: false,
  customBabyMaxAge: null,
  customChildMinAge: null,
  customChildMaxAge: null,
  customAdultMinAge: null,
  status: 'Open',
  maxCapacity: 100,
  registrationCount: 30,
  availableSpots: 70,
  notes: null,
  description: null,
  createdAt: '2026-01-01T00:00:00Z',
  updatedAt: '2026-01-01T00:00:00Z',
  campDescription: null,
  campPhoneNumber: null,
  campNationalPhoneNumber: null,
  campWebsiteUrl: null,
  campGoogleMapsUrl: null,
  campGoogleRating: null,
  campGoogleRatingCount: null,
  campPhotos: [],
  accommodationCapacity: null,
  calculatedTotalBedCapacity: null,
  extras: [],
  ...overrides,
})

const mountPage = () =>
  mount(CampPage, {
    global: {
      plugins: [createPinia()],
      stubs: globalStubs,
    },
  })

// ── Tests ─────────────────────────────────────────────────────────────────────
describe('CampPage.vue', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    currentCampEdition.value = null
    loading.value = false
    error.value = null
    familyUnit.value = null
  })

  it('calls fetchCurrentCampEdition and getCurrentUserFamilyUnit on mount', () => {
    mountPage()
    expect(mockFetchCurrentCampEdition).toHaveBeenCalledOnce()
    expect(mockGetCurrentUserFamilyUnit).toHaveBeenCalledOnce()
  })

  it('shows loading spinner when loading', () => {
    loading.value = true
    const wrapper = mountPage()
    expect(wrapper.find('[data-testid="camp-loading"]').exists()).toBe(true)
  })

  it('shows empty state when no edition exists', () => {
    currentCampEdition.value = null
    const wrapper = mountPage()
    expect(wrapper.find('[data-testid="camp-empty"]').exists()).toBe(true)
    expect(wrapper.text()).toContain('No hay información de campamento disponible')
  })

  it('shows camp name and year in h1 when edition exists', async () => {
    currentCampEdition.value = makeEdition()
    const wrapper = mountPage()
    await nextTick()
    expect(wrapper.find('h1').text()).toContain('Campamento 2026')
  })

  it('shows previous-year warning message for old editions', async () => {
    currentCampEdition.value = makeEdition({ year: 2025 })
    const wrapper = mountPage()
    await nextTick()
    expect(wrapper.text()).toContain('Mostrando información del campamento de 2025')
  })

  it('shows register button for representative on Open edition', async () => {
    currentCampEdition.value = makeEdition({ status: 'Open' })
    familyUnit.value = { representativeUserId: 'u1' }
    const wrapper = mountPage()
    await nextTick()
    expect(wrapper.find('[data-testid="register-button"]').exists()).toBe(true)
  })

  it('does NOT show register button for non-representative on Open edition', async () => {
    currentCampEdition.value = makeEdition({ status: 'Open' })
    familyUnit.value = { representativeUserId: 'other-user' }
    const wrapper = mountPage()
    await nextTick()
    expect(wrapper.find('[data-testid="register-button"]').exists()).toBe(false)
  })

  it('navigates to registration-new on register button click', async () => {
    currentCampEdition.value = makeEdition({ id: 'edition-abc', status: 'Open' })
    familyUnit.value = { representativeUserId: 'u1' }
    const wrapper = mountPage()
    await nextTick()
    await wrapper.find('[data-testid="register-button"]').trigger('click')
    expect(routerPushMock).toHaveBeenCalledWith({
      name: 'registration-new',
      params: { editionId: 'edition-abc' },
    })
  })

  it('shows error message when error is set', async () => {
    error.value = 'Error de conexión'
    const wrapper = mountPage()
    await nextTick()
    expect(wrapper.text()).toContain('Error de conexión')
  })
})
