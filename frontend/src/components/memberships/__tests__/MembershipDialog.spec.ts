import { describe, it, expect, vi, beforeEach } from 'vitest'
import { ref } from 'vue'
import { mount } from '@vue/test-utils'
import { createPinia } from 'pinia'
import MembershipDialog from '../MembershipDialog.vue'
import type { MembershipResponse, MembershipFeeResponse } from '@/types/membership'

const useMembershipsMock = vi.hoisted(() => vi.fn())

const createMembershipMock = vi.fn().mockResolvedValue({ id: 'm1' })
const getMembershipMock = vi.fn().mockResolvedValue(null)
const reactivateMembershipMock = vi.fn().mockResolvedValue(null)
const createFeeMock = vi.fn().mockResolvedValue(null)

vi.mock('@/composables/useMemberships', () => ({
  useMemberships: useMembershipsMock,
}))

vi.mock('primevue/usetoast', () => ({
  useToast: () => ({ add: vi.fn() }),
}))

vi.mock('primevue/useconfirm', () => ({
  useConfirm: () => ({ require: vi.fn() }),
}))

const componentStubs = {
  Dialog: { name: 'Dialog', template: '<div><slot /></div>', props: ['visible', 'header', 'modal', 'closable', 'dismissableMask'] },
  ConfirmDialog: true,
  Button: { name: 'Button', template: '<button @click="$emit(\'click\')">{{ label }}</button>', props: ['label', 'icon', 'loading', 'severity', 'outlined', 'size', 'data-testid', 'disabled'] },
  Message: { name: 'Message', template: '<div><slot /></div>', props: ['severity'] },
  InputNumber: { name: 'InputNumber', template: '<input />', props: ['modelValue', 'min', 'max', 'useGrouping', 'id', 'disabled', 'invalid'], emits: ['update:modelValue'] },
  DataTable: true,
  Column: true,
  Tag: true,
  PayFeeDialog: true,
  CreateFeeDialog: true,
}

const makeComposableReturn = (
  membershipVal: MembershipResponse | null,
  feesVal: MembershipFeeResponse[] = [],
) => ({
  membership: ref(membershipVal),
  fees: ref(feesVal),
  loading: ref(false),
  error: ref<string | null>(null),
  getMembership: getMembershipMock,
  createMembership: createMembershipMock,
  deactivateMembership: vi.fn(),
  payFee: vi.fn(),
  createFee: createFeeMock,
  reactivateMembership: reactivateMembershipMock,
  bulkActivateMemberships: vi.fn(),
  getFees: vi.fn(),
  updateMemberNumber: vi.fn(),
})

const inactiveMembership: MembershipResponse = {
  id: 'm1', familyMemberId: 'fm1', memberNumber: null,
  startDate: '2024-01-01', endDate: '2025-06-01', isActive: false,
  fees: [], createdAt: '2024-01-01', updatedAt: '2025-06-01',
}

const activeMembershipNoFees: MembershipResponse = {
  id: 'm1', familyMemberId: 'fm1', memberNumber: null,
  startDate: '2024-01-01', endDate: null, isActive: true,
  fees: [], createdAt: '2024-01-01', updatedAt: '2026-01-01',
}

const activeMembershipOldFeeOnly: MembershipResponse = {
  id: 'm1', familyMemberId: 'fm1', memberNumber: null,
  startDate: '2024-01-01', endDate: null, isActive: true,
  fees: [
    {
      id: 'fee-2024', membershipId: 'm1', year: 2024,
      amount: 50, status: 'Paid' as any,
      paidDate: '2024-02-01', paymentReference: null, createdAt: '2024-01-01',
    },
  ],
  createdAt: '2024-01-01', updatedAt: '2024-01-01',
}

describe('MembershipDialog', () => {
  const defaultProps = {
    visible: true,
    familyUnitId: 'fu1',
    memberId: 'member1',
    memberName: 'Ana García',
  }

  beforeEach(() => {
    vi.clearAllMocks()
    useMembershipsMock.mockReturnValue(makeComposableReturn(null))
  })

  it('renders InputNumber year picker when member has no membership', () => {
    const wrapper = mount(MembershipDialog, {
      props: defaultProps,
      global: { plugins: [createPinia()], stubs: componentStubs },
    })

    const inputNumber = wrapper.findComponent({ name: 'InputNumber' })
    expect(inputNumber.exists()).toBe(true)
  })

  it('InputNumber has max prop set to current year', () => {
    const wrapper = mount(MembershipDialog, {
      props: defaultProps,
      global: { plugins: [createPinia()], stubs: componentStubs },
    })

    const inputNumber = wrapper.findComponent({ name: 'InputNumber' })
    expect(inputNumber.props('max')).toBe(new Date().getFullYear())
  })

  it('InputNumber has useGrouping set to false', () => {
    const wrapper = mount(MembershipDialog, {
      props: defaultProps,
      global: { plugins: [createPinia()], stubs: componentStubs },
    })

    const inputNumber = wrapper.findComponent({ name: 'InputNumber' })
    expect(inputNumber.props('useGrouping')).toBe(false)
  })

  it('does not render DatePicker or Calendar', () => {
    const wrapper = mount(MembershipDialog, {
      props: defaultProps,
      global: { plugins: [createPinia()], stubs: componentStubs },
    })

    expect(wrapper.findComponent({ name: 'DatePicker' }).exists()).toBe(false)
    expect(wrapper.findComponent({ name: 'Calendar' }).exists()).toBe(false)
  })

  it('calls createMembership with { year } when activate button clicked', async () => {
    const wrapper = mount(MembershipDialog, {
      props: defaultProps,
      global: { plugins: [createPinia()], stubs: componentStubs },
    })

    const buttons = wrapper.findAllComponents({ name: 'Button' })
    const activateButton = buttons.find((b) => b.props('label') === 'Activar membresía')
    expect(activateButton).toBeTruthy()
    await activateButton!.trigger('click')

    expect(createMembershipMock).toHaveBeenCalledWith('fu1', 'member1', {
      year: new Date().getFullYear(),
    })
  })

  it('shows reactivate button when membership is inactive', () => {
    useMembershipsMock.mockReturnValue(makeComposableReturn(inactiveMembership))

    const wrapper = mount(MembershipDialog, {
      props: defaultProps,
      global: { plugins: [createPinia()], stubs: componentStubs },
    })

    const buttons = wrapper.findAllComponents({ name: 'Button' })
    const reactivateButton = buttons.find((b) => b.props('label') === 'Reactivar membresía')
    expect(reactivateButton).toBeTruthy()
  })

  it('calls reactivateMembership when reactivate button is clicked', async () => {
    reactivateMembershipMock.mockResolvedValueOnce({ ...inactiveMembership, isActive: true, endDate: null })
    useMembershipsMock.mockReturnValue(makeComposableReturn(inactiveMembership))

    const wrapper = mount(MembershipDialog, {
      props: defaultProps,
      global: { plugins: [createPinia()], stubs: componentStubs },
    })

    const buttons = wrapper.findAllComponents({ name: 'Button' })
    const reactivateButton = buttons.find((b) => b.props('label') === 'Reactivar membresía')
    await reactivateButton!.trigger('click')

    expect(reactivateMembershipMock).toHaveBeenCalledWith('fu1', 'member1', {
      year: new Date().getFullYear(),
    })
  })

  it('shows "Cargar cuota anual" button when membership is active with no fees', () => {
    useMembershipsMock.mockReturnValue(makeComposableReturn(activeMembershipNoFees, []))

    const wrapper = mount(MembershipDialog, {
      props: defaultProps,
      global: { plugins: [createPinia()], stubs: componentStubs },
    })

    const buttons = wrapper.findAllComponents({ name: 'Button' })
    const createFeeButton = buttons.find((b) => b.props('label') === 'Cargar cuota anual')
    expect(createFeeButton).toBeTruthy()
  })

  it('shows "Cargar cuota {year}" button when active membership has no fee for current year', () => {
    const currentYear = new Date().getFullYear()
    useMembershipsMock.mockReturnValue(
      makeComposableReturn(activeMembershipOldFeeOnly, activeMembershipOldFeeOnly.fees),
    )

    const wrapper = mount(MembershipDialog, {
      props: defaultProps,
      global: { plugins: [createPinia()], stubs: componentStubs },
    })

    const buttons = wrapper.findAllComponents({ name: 'Button' })
    const createFeeButton = buttons.find((b) => b.props('label') === `Cargar cuota ${currentYear}`)
    expect(createFeeButton).toBeTruthy()
  })
})
