import { describe, it, expect, vi } from 'vitest'
import { ref } from 'vue'
import { mount } from '@vue/test-utils'
import { createPinia } from 'pinia'
import MembershipDialog from '../MembershipDialog.vue'

const createMembershipMock = vi.fn().mockResolvedValue({ id: 'm1' })
const getMembershipMock = vi.fn().mockResolvedValue(null)

vi.mock('@/composables/useMemberships', () => ({
  useMemberships: () => ({
    membership: ref(null),
    fees: ref([]),
    loading: ref(false),
    error: ref(null),
    getMembership: getMembershipMock,
    createMembership: createMembershipMock,
    deactivateMembership: vi.fn(),
    payFee: vi.fn(),
  }),
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
  Button: { name: 'Button', template: '<button @click="$emit(\'click\')"><slot /></button>', props: ['label', 'icon', 'loading', 'severity', 'outlined', 'size'] },
  Message: { name: 'Message', template: '<div><slot /></div>', props: ['severity'] },
  InputNumber: { name: 'InputNumber', template: '<input />', props: ['modelValue', 'min', 'max', 'useGrouping'], emits: ['update:modelValue'] },
  DataTable: true,
  Column: true,
  Tag: true,
  PayFeeDialog: true,
}

describe('MembershipDialog', () => {
  const defaultProps = {
    visible: true,
    familyUnitId: 'fu1',
    memberId: 'member1',
    memberName: 'Ana García',
  }

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
})
