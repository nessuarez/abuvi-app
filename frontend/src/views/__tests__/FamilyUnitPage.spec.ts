import { describe, it, expect, vi, beforeEach } from 'vitest'
import { mount } from '@vue/test-utils'
import { createPinia } from 'pinia'
import { nextTick } from 'vue'
import FamilyUnitPage from '../FamilyUnitPage.vue'
import type { FamilyMemberResponse } from '@/types/family-unit'
import { FamilyRelationship } from '@/types/family-unit'

// Mutable auth state — hoisted so the vi.mock factory below can close over it
const authMock = vi.hoisted(() => ({
  isBoard: false,
  isAdmin: false,
  user: { id: 'u1', role: 'Member', email: 'm@test.com', firstName: 'Test', lastName: 'User' },
  fullName: 'Test User',
}))

vi.mock('@/stores/auth', () => ({
  useAuthStore: () => authMock,
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
    loading: { value: false },
    error: { value: null },
    getCurrentUserFamilyUnit: vi.fn().mockResolvedValue(null),
    getFamilyMembers: vi.fn().mockResolvedValue(undefined),
    createFamilyUnit: vi.fn(),
    updateFamilyUnit: vi.fn(),
    deleteFamilyUnit: vi.fn(),
    createFamilyMember: vi.fn(),
    updateFamilyMember: vi.fn(),
    deleteFamilyMember: vi.fn(),
  }),
}))

vi.mock('primevue/useconfirm', () => ({
  useConfirm: () => ({ require: vi.fn() }),
}))

vi.mock('primevue/usetoast', () => ({
  useToast: () => ({ add: vi.fn() }),
}))

const componentStubs = {
  FamilyMemberList: { name: 'FamilyMemberList', template: '<div />', props: ['members', 'loading', 'canManageMemberships'] },
  MembershipDialog: { name: 'MembershipDialog', template: '<div />', props: ['visible', 'familyUnitId', 'memberId', 'memberName'] },
  BulkMembershipDialog: { name: 'BulkMembershipDialog', template: '<div />', props: ['visible', 'familyUnitId', 'members', 'memberData'] },
  ConfirmDialog: true,
  Dialog: true,
  // Card must render its named slots so FamilyMemberList (inside #content) is visible to tests
  Card: { template: '<div><slot name="title" /><slot name="content" /></div>' },
  Button: true,
  FamilyUnitForm: true,
  FamilyMemberForm: true,
}

const testMember: FamilyMemberResponse = {
  id: 'member-1',
  familyUnitId: 'unit-1',
  firstName: 'Ana',
  lastName: 'García',
  dateOfBirth: '1990-01-01',
  relationship: FamilyRelationship.Spouse,
  documentNumber: null,
  email: null,
  phone: null,
  hasMedicalNotes: false,
  hasAllergies: false,
  userId: null,
  createdAt: '2026-01-01T00:00:00Z',
  updatedAt: '2026-01-01T00:00:00Z',
}

describe('FamilyUnitPage — membership dialog', () => {
  beforeEach(() => {
    authMock.isBoard = false
  })

  it('passes canManageMemberships=true to FamilyMemberList when user isBoard', async () => {
    authMock.isBoard = true
    const wrapper = mount(FamilyUnitPage, {
      global: { plugins: [createPinia()], stubs: componentStubs },
    })
    await nextTick()
    const list = wrapper.findComponent({ name: 'FamilyMemberList' })
    expect(list.props('canManageMemberships')).toBe(true)
  })

  it('passes canManageMemberships=false to FamilyMemberList when user is Member', async () => {
    authMock.isBoard = false
    const wrapper = mount(FamilyUnitPage, {
      global: { plugins: [createPinia()], stubs: componentStubs },
    })
    await nextTick()
    const list = wrapper.findComponent({ name: 'FamilyMemberList' })
    expect(list.props('canManageMemberships')).toBe(false)
  })

  it('opens MembershipDialog when manageMembership event is emitted from list', async () => {
    authMock.isBoard = true
    const wrapper = mount(FamilyUnitPage, {
      global: { plugins: [createPinia()], stubs: componentStubs },
    })
    await nextTick()

    const list = wrapper.findComponent({ name: 'FamilyMemberList' })
    await list.vm.$emit('manageMembership', testMember)
    await nextTick()

    const dialog = wrapper.findComponent({ name: 'MembershipDialog' })
    expect(dialog.exists()).toBe(true)
    expect(dialog.props('visible')).toBe(true)
    expect(dialog.props('memberId')).toBe('member-1')
  })

  it('shows bulk-membership-btn for board users', async () => {
    authMock.isBoard = true
    const wrapper = mount(FamilyUnitPage, {
      global: { plugins: [createPinia()], stubs: componentStubs },
    })
    await nextTick()
    expect(wrapper.find('[data-testid="bulk-membership-btn"]').exists()).toBe(true)
  })

  it('does not show bulk-membership-btn for non-board users', async () => {
    authMock.isBoard = false
    const wrapper = mount(FamilyUnitPage, {
      global: { plugins: [createPinia()], stubs: componentStubs },
    })
    await nextTick()
    expect(wrapper.find('[data-testid="bulk-membership-btn"]').exists()).toBe(false)
  })
})
