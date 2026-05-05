import { describe, it, expect, vi, beforeEach } from 'vitest'
import { ref } from 'vue'
import { shallowMount } from '@vue/test-utils'
import PrimeVue from 'primevue/config'
import RegistrationFriendLinks from '../RegistrationFriendLinks.vue'
import type { FriendLinkResponse } from '@/types/registration'
import type { AdminRegistrationListItem } from '@/types/registration'

const mockToastAdd = vi.fn()
const mockFetchAdminRegistrations = vi.fn()
const mockUpdateFriendLinks = vi.fn()

const mockEditionRegistrations = ref<AdminRegistrationListItem[]>([])
const mockFriendLinks = ref<FriendLinkResponse[]>([])
const mockSaving = ref(false)
const mockSaveError = ref<string | null>(null)

vi.mock('primevue/usetoast', () => ({
  useToast: () => ({ add: mockToastAdd }),
}))

vi.mock('@/composables/useAdminRegistrations', () => ({
  useAdminRegistrations: () => ({
    registrations: mockEditionRegistrations,
    fetchAdminRegistrations: mockFetchAdminRegistrations,
  }),
}))

vi.mock('@/composables/useRegistrationAccommodationTagging', () => ({
  useRegistrationAccommodationTagging: () => ({
    friendLinks: mockFriendLinks,
    saving: mockSaving,
    saveError: mockSaveError,
    updateFriendLinks: mockUpdateFriendLinks,
  }),
}))

const makeAdminReg = (id: string, familyName: string): AdminRegistrationListItem =>
  ({
    id,
    familyUnit: { id: `fu-${id}`, name: familyName },
    representative: { id: 'user-1', firstName: 'Test', lastName: 'User', email: 'test@test.com' },
    status: 'Confirmed',
    memberCount: 2,
    totalAmount: 400,
    amountPaid: 400,
    amountRemaining: 0,
    createdAt: '2026-03-01T00:00:00Z',
    attendancePeriods: ['Complete'],
    accommodationPreferences: [],
  } as AdminRegistrationListItem)

const defaultProps = {
  registrationId: 'reg-1',
  campEditionId: 'edition-1',
  initialFriendLinks: [] as FriendLinkResponse[],
}

function mountComponent(props = defaultProps) {
  return shallowMount(RegistrationFriendLinks, {
    props,
    global: { plugins: [PrimeVue] },
  })
}

describe('RegistrationFriendLinks', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    mockFriendLinks.value = []
    mockEditionRegistrations.value = []
    mockSaving.value = false
    mockSaveError.value = null
    mockFetchAdminRegistrations.mockResolvedValue(undefined)
  })

  it('shows "Sin vínculos" when no friend links', () => {
    const wrapper = mountComponent()

    expect(wrapper.text()).toContain('Sin vínculos')
  })

  it('renders linked family names in read mode', () => {
    mockFriendLinks.value = [
      {
        linkedRegistrationId: 'reg-xyz',
        linkedFamilyName: 'Martínez Family',
        createdByUserId: 'user-1',
        createdAt: '2026-05-01T10:00:00Z',
      },
    ]
    const wrapper = mountComponent({
      ...defaultProps,
      initialFriendLinks: mockFriendLinks.value,
    })

    expect(wrapper.text()).toContain('Martínez Family')
  })

  it('calls fetchAdminRegistrations on entering edit mode', async () => {
    const wrapper = mountComponent()

    await wrapper.find('[data-testid="edit-friend-links-btn"]').trigger('click')
    await wrapper.vm.$nextTick()

    expect(mockFetchAdminRegistrations).toHaveBeenCalledWith('edition-1', { pageSize: 200 })
  })

  it('shows MultiSelect in edit mode', async () => {
    const wrapper = mountComponent()

    await wrapper.find('[data-testid="edit-friend-links-btn"]').trigger('click')
    await wrapper.vm.$nextTick()

    expect(wrapper.find('[data-testid="friend-links-multiselect"]').exists()).toBe(true)
  })

  it('filters out own registrationId from available options', async () => {
    mockEditionRegistrations.value = [
      makeAdminReg('reg-1', 'Own Family'),
      makeAdminReg('reg-2', 'García Family'),
    ]
    const wrapper = mountComponent()

    await wrapper.find('[data-testid="edit-friend-links-btn"]').trigger('click')
    await wrapper.vm.$nextTick()

    const ms = wrapper.findComponent({ name: 'MultiSelect' })
    const options = ms.props('options') as { id: string }[]
    expect(options.every((o) => o.id !== 'reg-1')).toBe(true)
    expect(options).toHaveLength(1)
  })

  it('calls updateFriendLinks on save and emits updated', async () => {
    const updatedLinks: FriendLinkResponse[] = [
      {
        linkedRegistrationId: 'reg-2',
        linkedFamilyName: 'García Family',
        createdByUserId: 'user-1',
        createdAt: '2026-05-01T10:00:00Z',
      },
    ]
    mockUpdateFriendLinks.mockResolvedValueOnce({
      registrationId: 'reg-1',
      friendLinks: updatedLinks,
    })
    const wrapper = mountComponent()

    await wrapper.find('[data-testid="edit-friend-links-btn"]').trigger('click')
    await wrapper.vm.$nextTick()
    await wrapper.find('[data-testid="save-friend-links-btn"]').trigger('click')
    await wrapper.vm.$nextTick()

    expect(mockUpdateFriendLinks).toHaveBeenCalledWith('reg-1', [])
    expect(wrapper.emitted('updated')).toBeTruthy()
    expect(wrapper.emitted('updated')![0]).toEqual([updatedLinks])
  })

  it('shows error toast on save failure', async () => {
    mockUpdateFriendLinks.mockResolvedValueOnce(null)
    mockSaveError.value = 'SAME_EDITION_REQUIRED'
    const wrapper = mountComponent()

    await wrapper.find('[data-testid="edit-friend-links-btn"]').trigger('click')
    await wrapper.vm.$nextTick()
    await wrapper.find('[data-testid="save-friend-links-btn"]').trigger('click')
    await wrapper.vm.$nextTick()

    expect(mockToastAdd).toHaveBeenCalledWith(
      expect.objectContaining({ severity: 'error' })
    )
  })
})
