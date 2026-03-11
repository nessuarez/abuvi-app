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

// Mutable family units state — hoisted so the vi.mock factory below can close over it
const familyUnitMock = vi.hoisted(() => ({
  familyUnit: {
    value: {
      id: 'unit-1',
      name: 'Test Family',
      representativeUserId: 'u1', // matches auth.user.id by default
      profilePhotoUrl: null,
      createdAt: '2026-01-01T00:00:00Z',
      updatedAt: '2026-01-01T00:00:00Z',
    },
  },
  familyMembers: { value: [] },
  loading: { value: false },
  error: { value: null },
  getCurrentUserFamilyUnit: vi.fn().mockResolvedValue(null),
  getFamilyMembers: vi.fn().mockResolvedValue(undefined),
  getFamilyUnitById: vi.fn().mockResolvedValue(null),
  createFamilyUnit: vi.fn(),
  updateFamilyUnit: vi.fn(),
  deleteFamilyUnit: vi.fn(),
  createFamilyMember: vi.fn(),
  updateFamilyMember: vi.fn(),
  deleteFamilyMember: vi.fn(),
  uploadMemberProfilePhoto: vi.fn(),
  removeMemberProfilePhoto: vi.fn(),
  uploadUnitProfilePhoto: vi.fn(),
  removeUnitProfilePhoto: vi.fn(),
}))

vi.mock('@/composables/useFamilyUnits', () => ({
  useFamilyUnits: () => familyUnitMock,
}))

vi.mock('vue-router', async (importOriginal) => {
  const actual = await importOriginal<typeof import('vue-router')>()
  return {
    ...actual,
    useRoute: () => ({ params: {} }),
    useRouter: () => ({ push: vi.fn() }),
  }
})

vi.mock('primevue/useconfirm', () => ({
  useConfirm: () => ({ require: vi.fn() }),
}))

vi.mock('primevue/usetoast', () => ({
  useToast: () => ({ add: vi.fn() }),
}))

const componentStubs = {
  FamilyMemberList: { name: 'FamilyMemberList', template: '<div />', props: ['members', 'loading', 'canManageMemberships', 'readOnly', 'uploadingMemberId'] },
  ProfilePhotoAvatar: { name: 'ProfilePhotoAvatar', template: '<div />', props: ['photoUrl', 'initials', 'size', 'editable', 'loading'] },
  MembershipDialog: { name: 'MembershipDialog', template: '<div />', props: ['visible', 'familyUnitId', 'memberId', 'memberName'] },
  BulkMembershipDialog: { name: 'BulkMembershipDialog', template: '<div />', props: ['visible', 'familyUnitId', 'members', 'memberData'] },
  ConfirmDialog: true,
  Dialog: true,
  // Card must render its named slots so FamilyMemberList (inside #content) is visible to tests
  Card: { template: '<div><slot name="title" /><slot name="content" /></div>' },
  Button: { name: 'Button', template: '<button />', props: ['label', 'icon', 'severity', 'outlined', 'text', 'size'] },
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
  profilePhotoUrl: null,
  userId: null,
  createdAt: '2026-01-01T00:00:00Z',
  updatedAt: '2026-01-01T00:00:00Z',
}

describe('FamilyUnitPage — membership dialog', () => {
  beforeEach(() => {
    authMock.isBoard = false
    authMock.isAdmin = false
    familyUnitMock.familyUnit.value.representativeUserId = 'u1'
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

describe('FamilyUnitPage — isViewingOther', () => {
  beforeEach(() => {
    authMock.isBoard = false
    authMock.isAdmin = false
    // Reset to representative user by default
    familyUnitMock.familyUnit.value.representativeUserId = 'u1'
  })

  it('shows edit controls when current user is the representative and no route id', async () => {
    // representativeUserId: 'u1', auth.user.id: 'u1' → isViewingOther = false
    const wrapper = mount(FamilyUnitPage, {
      global: { plugins: [createPinia()], stubs: componentStubs },
    })
    await nextTick()
    // No "Volver a Administración" button when isViewingOther is false
    const buttons = wrapper.findAllComponents({ name: 'Button' })
    const backButton = buttons.find(b => b.props('label') === 'Volver a Administración')
    expect(backButton).toBeUndefined()
  })

  it('hides edit controls when current user is NOT the representative (linked member)', async () => {
    // representative is someone else → isViewingOther = true
    familyUnitMock.familyUnit.value.representativeUserId = 'other-user'
    authMock.isAdmin = true // admin sees the back button

    const wrapper = mount(FamilyUnitPage, {
      global: { plugins: [createPinia()], stubs: componentStubs },
    })
    await nextTick()
    // isViewingOther === true and user isAdmin → "Volver a Administración" button appears
    const buttons = wrapper.findAllComponents({ name: 'Button' })
    const backButton = buttons.find(b => b.props('label') === 'Volver a Administración')
    expect(backButton).toBeDefined()
  })

  it('hides edit controls when accessed via /mi-familia and user is NOT representative', async () => {
    // Key regression test: no route.params.id, but user is NOT representative
    // Previous bug: isViewingOther was false because !!route.params.id was false
    familyUnitMock.familyUnit.value.representativeUserId = 'representative-user'

    const wrapper = mount(FamilyUnitPage, {
      global: {
        plugins: [createPinia()],
        stubs: componentStubs,
        // vue-router not mounted → route.params.id is undefined (simulates /mi-familia route)
      },
    })
    await nextTick()
    // FamilyMemberList should receive read-only=true
    const list = wrapper.findComponent({ name: 'FamilyMemberList' })
    expect(list.props('readOnly')).toBe(true)
  })

  it('hides "Volver a Administración" button for non-admin linked members', async () => {
    // Linked member (not representative, not admin) should NOT see the admin back button
    familyUnitMock.familyUnit.value.representativeUserId = 'representative-user'
    authMock.isAdmin = false
    authMock.isBoard = false

    const wrapper = mount(FamilyUnitPage, {
      global: { plugins: [createPinia()], stubs: componentStubs },
    })
    await nextTick()
    const buttons = wrapper.findAllComponents({ name: 'Button' })
    const backButton = buttons.find(b => b.props('label') === 'Volver a Administración')
    expect(backButton).toBeUndefined()
  })
})
