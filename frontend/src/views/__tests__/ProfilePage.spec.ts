import { describe, it, expect, vi } from 'vitest'
import { mount } from '@vue/test-utils'
import { createPinia } from 'pinia'
import ProfilePage from '../ProfilePage.vue'

// Mutable auth state — hoisted so the vi.mock factory can close over it
const authMock = vi.hoisted(() => ({
  isBoard: false,
  isAdmin: false,
  user: { id: 'u1', role: 'Member', email: 'm@test.com', firstName: 'Ana', lastName: 'García' },
  fullName: 'Ana García',
}))

vi.mock('@/stores/auth', () => ({
  useAuthStore: () => authMock,
}))

vi.mock('@/composables/useProfile', () => ({
  useProfile: () => ({
    fullUser: { value: null },
    loading: { value: false },
    error: { value: null },
    loadProfile: vi.fn().mockResolvedValue(undefined),
    updateProfile: vi.fn(),
  }),
}))

vi.mock('@/composables/useFamilyUnits', () => ({
  useFamilyUnits: () => ({
    familyUnit: {
      value: {
        id: 'unit-1',
        name: 'Test Family',
        representativeUserId: 'u1',
        createdAt: '2026-01-01T00:00:00Z',
        updatedAt: '2026-01-01T00:00:00Z',
      },
    },
    familyMembers: { value: [] },
    getCurrentUserFamilyUnit: vi.fn().mockResolvedValue(null),
    getFamilyMembers: vi.fn().mockResolvedValue(undefined),
  }),
}))

vi.mock('@/composables/useMemberships', () => ({
  useMemberships: () => ({
    getMembership: vi.fn().mockResolvedValue(null),
    payFee: vi.fn(),
    bulkActivateMemberships: vi.fn(),
  }),
}))

vi.mock('vue-router', () => ({
  useRouter: () => ({ push: vi.fn() }),
}))

vi.mock('primevue/usetoast', () => ({
  useToast: () => ({ add: vi.fn() }),
}))

vi.mock('@/utils/user', () => ({
  getRoleLabel: (role: string) => role,
}))

const componentStubs = {
  // Container renders default slot so the overall structure is visible
  Container: { template: '<div><slot /></div>' },
  // Card stubs with true — named slots (#content) do NOT render,
  // which prevents formatDate(undefined) errors from fullUser being a truthy plain object in the mock
  Card: true,
  PayFeeDialog: true,
  MembershipDialog: { name: 'MembershipDialog', template: '<div />', props: ['visible', 'familyUnitId', 'memberId', 'memberName'] },
  BulkMembershipDialog: { name: 'BulkMembershipDialog', template: '<div />', props: ['visible', 'familyUnitId', 'members', 'memberData'] },
  Skeleton: true,
  Button: true,
  Tag: true,
  InputText: true,
}

// Stubs with Card rendering only the title slot (for title slot button visibility tests)
// NOTE: We deliberately omit #content to avoid renderig formatDate(undefined) errors
// that occur because the mock returns { value: null } (a truthy plain object) for fullUser.
const componentStubsWithCardSlots = {
  ...componentStubs,
  Card: { template: '<div><slot name="title" /></div>' },
}

describe('ProfilePage — membership management button', () => {
  it('does not show manage-membership buttons for non-board user', () => {
    authMock.isBoard = false
    const wrapper = mount(ProfilePage, {
      global: { plugins: [createPinia()], stubs: componentStubs },
    })
    expect(wrapper.find('[data-testid^="manage-membership-btn-"]').exists()).toBe(false)
  })
})

describe('ProfilePage — MembershipDialog mounting', () => {
  it('does not mount MembershipDialog on initial render (v-if guard)', () => {
    authMock.isBoard = true
    const wrapper = mount(ProfilePage, {
      global: { plugins: [createPinia()], stubs: componentStubs },
    })
    // MembershipDialog is v-if="selectedMemberForMembership" — must be absent initially
    const dialog = wrapper.findComponent({ name: 'MembershipDialog' })
    expect(dialog.exists()).toBe(false)
  })
})

describe('ProfilePage — bulk membership button', () => {
  it('shows bulk-membership-btn for board users when familyUnit exists', () => {
    authMock.isBoard = true
    const wrapper = mount(ProfilePage, {
      global: { plugins: [createPinia()], stubs: componentStubsWithCardSlots },
    })
    expect(wrapper.find('[data-testid="bulk-membership-btn"]').exists()).toBe(true)
  })

  it('does not show bulk-membership-btn for non-board users', () => {
    authMock.isBoard = false
    const wrapper = mount(ProfilePage, {
      global: { plugins: [createPinia()], stubs: componentStubsWithCardSlots },
    })
    expect(wrapper.find('[data-testid="bulk-membership-btn"]').exists()).toBe(false)
  })
})
