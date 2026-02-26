import { describe, it, expect, vi, beforeEach } from 'vitest'
import { mount } from '@vue/test-utils'
import { createPinia } from 'pinia'
import { nextTick } from 'vue'
import BulkMembershipDialog from '../BulkMembershipDialog.vue'
import type { FamilyMemberResponse } from '@/types/family-unit'
import { FamilyRelationship } from '@/types/family-unit'
import type { MemberMembershipData } from '@/types/membership'

const bulkActivateMock = vi.fn()

vi.mock('@/composables/useMemberships', () => ({
  useMemberships: () => ({
    loading: { value: false },
    error: { value: null },
    bulkActivateMemberships: bulkActivateMock,
  }),
}))

vi.mock('primevue/usetoast', () => ({
  useToast: () => ({ add: vi.fn() }),
}))

const componentStubs = {
  Dialog: { template: '<div><slot /></div>', props: ['visible', 'header', 'modal', 'closable', 'dismissableMask'] },
  Button: {
    template: '<button v-if="!$props.vIf" @click="$emit(\'click\')">{{ label }}</button>',
    props: ['label', 'icon', 'loading', 'severity', 'outlined', 'size', 'disabled'],
  },
  Message: { template: '<div class="message"><slot /></div>', props: ['severity'] },
  InputNumber: { template: '<input />', props: ['modelValue', 'min', 'max', 'useGrouping'], emits: ['update:modelValue'] },
  Tag: { template: '<span>{{ value }}</span>', props: ['value', 'severity'] },
}

const makeMember = (id: string): FamilyMemberResponse => ({
  id,
  familyUnitId: 'unit-1',
  firstName: 'Test',
  lastName: `Member ${id}`,
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
})

const makeMemberData = (id: string, hasActiveMembership: boolean): MemberMembershipData => ({
  member: makeMember(id),
  membershipId: hasActiveMembership ? `membership-${id}` : null,
  isActiveMembership: hasActiveMembership,
  currentFee: null,
  feeLoading: false,
})

describe('BulkMembershipDialog', () => {
  beforeEach(() => {
    vi.clearAllMocks()
  })

  const defaultProps = {
    visible: true,
    familyUnitId: 'unit-1',
    members: [makeMember('m1'), makeMember('m2')],
    memberData: [],
  }

  it('falls back to all members when memberData is empty (FamilyUnitPage context)', () => {
    const wrapper = mount(BulkMembershipDialog, {
      props: defaultProps,
      global: { plugins: [createPinia()], stubs: componentStubs },
    })

    expect(wrapper.text()).toContain('2')
  })

  it('shows correct count from memberData when provided', () => {
    const wrapper = mount(BulkMembershipDialog, {
      props: {
        ...defaultProps,
        memberData: [makeMemberData('m1', false), makeMemberData('m2', true)],
      },
      global: { plugins: [createPinia()], stubs: componentStubs },
    })

    // 1 without membership
    expect(wrapper.text()).toContain('1')
  })

  it('shows info message when all members already have membership', () => {
    const wrapper = mount(BulkMembershipDialog, {
      props: {
        ...defaultProps,
        members: [makeMember('m1')],
        memberData: [makeMemberData('m1', true)],
      },
      global: { plugins: [createPinia()], stubs: componentStubs },
    })

    expect(wrapper.text()).toContain('Todos los miembros de esta familia ya tienen una membresía activa.')
  })

  it('calls bulkActivateMemberships with correct familyUnitId and year', async () => {
    bulkActivateMock.mockResolvedValueOnce({ activated: 1, skipped: 0, results: [] })

    const wrapper = mount(BulkMembershipDialog, {
      props: defaultProps,
      global: { plugins: [createPinia()], stubs: componentStubs },
    })

    await wrapper.find('[data-testid="activate-btn"]').trigger('click')

    expect(bulkActivateMock).toHaveBeenCalledWith('unit-1', { year: new Date().getFullYear() })
  })

  it('emits done when dialog is closed after successful activation', async () => {
    bulkActivateMock.mockResolvedValueOnce({ activated: 2, skipped: 0, results: [] })

    const wrapper = mount(BulkMembershipDialog, {
      props: defaultProps,
      global: { plugins: [createPinia()], stubs: componentStubs },
    })

    await wrapper.find('[data-testid="activate-btn"]').trigger('click')
    await nextTick()

    await wrapper.find('[data-testid="cancel-btn"]').trigger('click')

    expect(wrapper.emitted('done')).toBeTruthy()
    expect(wrapper.emitted('update:visible')).toBeTruthy()
  })

  it('does not emit done when closed with 0 activations', async () => {
    bulkActivateMock.mockResolvedValueOnce({ activated: 0, skipped: 2, results: [] })

    const wrapper = mount(BulkMembershipDialog, {
      props: defaultProps,
      global: { plugins: [createPinia()], stubs: componentStubs },
    })

    await wrapper.find('[data-testid="activate-btn"]').trigger('click')
    await nextTick()

    await wrapper.find('[data-testid="cancel-btn"]').trigger('click')

    expect(wrapper.emitted('done')).toBeFalsy()
  })

  it('emits update:visible false when cancel is clicked', async () => {
    const wrapper = mount(BulkMembershipDialog, {
      props: defaultProps,
      global: { plugins: [createPinia()], stubs: componentStubs },
    })

    await wrapper.find('[data-testid="cancel-btn"]').trigger('click')

    expect(wrapper.emitted('update:visible')).toBeTruthy()
    expect(wrapper.emitted('update:visible')![0]).toEqual([false])
  })
})
