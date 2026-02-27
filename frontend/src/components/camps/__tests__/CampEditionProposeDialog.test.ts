import { describe, it, expect, vi, beforeEach } from 'vitest'
import { mount, flushPromises } from '@vue/test-utils'
import CampEditionProposeDialog from '@/components/camps/CampEditionProposeDialog.vue'

const mockProposeEdition = vi.fn()
const mockEditions = vi.hoisted(() => ({ value: [] as any[] }))

vi.mock('@/composables/useCampEditions', () => ({
  useCampEditions: () => ({
    proposeEdition: mockProposeEdition,
    loading: { value: false },
    error: { value: null },
    editions: mockEditions,
  }),
}))

vi.mock('primevue/dialog', () => ({
  default: {
    name: 'Dialog',
    props: ['visible', 'header', 'modal'],
    emits: ['update:visible'],
    template: '<div v-if="visible"><slot /><slot name="footer" /></div>',
  },
}))

vi.mock('primevue/button', () => ({
  default: {
    name: 'Button',
    props: ['label', 'loading', 'disabled', 'text', 'icon'],
    emits: ['click'],
    template: '<button :disabled="disabled || loading" :data-testid="$attrs[\'data-testid\']" @click="$emit(\'click\')">{{ label }}</button>',
  },
}))

vi.mock('primevue/inputtext', () => ({
  default: {
    name: 'InputText',
    props: ['modelValue', 'placeholder'],
    emits: ['update:modelValue'],
    template: '<input :value="modelValue" @input="$emit(\'update:modelValue\', $event.target.value)" />',
  },
}))

vi.mock('primevue/inputnumber', () => ({
  default: {
    name: 'InputNumber',
    props: ['modelValue', 'useGrouping', 'min', 'max', 'mode', 'currency', 'locale'],
    emits: ['update:modelValue'],
    template: '<input type="number" :value="modelValue" @input="$emit(\'update:modelValue\', Number($event.target.value))" />',
  },
}))

vi.mock('primevue/datepicker', () => ({
  default: {
    name: 'DatePicker',
    props: ['modelValue', 'dateFormat', 'showIcon'],
    emits: ['update:modelValue'],
    template: '<input type="text" :value="modelValue" />',
  },
}))

vi.mock('primevue/textarea', () => ({
  default: {
    name: 'Textarea',
    props: ['modelValue', 'rows', 'placeholder'],
    emits: ['update:modelValue'],
    template: '<textarea :value="modelValue" @input="$emit(\'update:modelValue\', $event.target.value)">{{ modelValue }}</textarea>',
  },
}))

vi.mock('primevue/message', () => ({
  default: {
    name: 'Message',
    props: ['severity', 'closable'],
    template: '<div><slot /></div>',
  },
}))

const baseCamp = {
  id: 'camp-1',
  name: 'Test Camp',
  description: null,
  location: 'Test Location',
  latitude: 40.0,
  longitude: -3.0,
  googlePlaceId: null,
  formattedAddress: null,
  phoneNumber: null,
  websiteUrl: null,
  googleMapsUrl: null,
  googleRating: null,
  googleRatingCount: null,
  businessStatus: null,
  pricePerAdult: 100,
  pricePerChild: 50,
  pricePerBaby: 0,
  isActive: true,
  createdAt: '2026-01-01',
  updatedAt: '2026-01-01',
}

describe('CampEditionProposeDialog', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    mockEditions.value = []
  })

  const mountAndOpen = async (editions: any[] = []) => {
    mockEditions.value = editions
    const wrapper = mount(CampEditionProposeDialog, {
      props: { visible: false, campId: 'camp-1', camp: baseCamp },
    })
    // Trigger the watch by changing visible to true
    await wrapper.setProps({ visible: true })
    await flushPromises()
    return wrapper
  }

  it('does not render proposalNotes field', async () => {
    const wrapper = await mountAndOpen()
    expect(wrapper.text()).not.toContain('Notas adicionales')
  })

  it('renders proposalReason as optional (no asterisk)', async () => {
    const wrapper = await mountAndOpen()
    const labels = wrapper.findAll('label')
    const proposalLabel = labels.find((l) => l.text().includes('Motivo de la propuesta'))
    expect(proposalLabel).toBeDefined()
    expect(proposalLabel!.text()).not.toContain('*')
  })

  it('submits successfully with empty proposalReason', async () => {
    mockProposeEdition.mockResolvedValue({ id: 'edition-1' })
    const wrapper = await mountAndOpen()

    const submitBtn = wrapper.find('[data-testid="submit-propose-btn"]')
    await submitBtn.trigger('click')
    await flushPromises()

    // proposalReason was empty but form should still submit (no validation error for it)
    expect(wrapper.html()).not.toContain('El motivo de la propuesta es obligatorio')
  })

  it('pre-populates dates from previous year edition', async () => {
    const currentYear = new Date().getFullYear()
    const wrapper = await mountAndOpen([
      {
        id: 'prev-1',
        campId: 'camp-1',
        year: currentYear - 1,
        startDate: `${currentYear - 1}-07-10`,
        endDate: `${currentYear - 1}-07-20`,
        location: 'Test',
        pricePerAdult: 100,
        pricePerChild: 50,
        pricePerBaby: 0,
        maxCapacity: 50,
        status: 'Completed',
        isArchived: false,
        createdAt: '2025-01-01',
        updatedAt: '2025-01-01',
      },
    ])

    const vm = wrapper.vm as any

    expect(vm.form.startDate).not.toBeNull()
    expect(vm.form.startDate.getFullYear()).toBe(currentYear)
    expect(vm.form.startDate.getMonth()).toBe(6) // July (0-indexed)
    expect(vm.form.startDate.getDate()).toBe(10)

    expect(vm.form.endDate).not.toBeNull()
    expect(vm.form.endDate.getFullYear()).toBe(currentYear)
    expect(vm.form.endDate.getMonth()).toBe(6) // July
    expect(vm.form.endDate.getDate()).toBe(20)
  })

  it('defaults dates to Aug 15-22 when no previous edition exists', async () => {
    const wrapper = await mountAndOpen([])

    const vm = wrapper.vm as any
    const currentYear = new Date().getFullYear()

    expect(vm.form.startDate).not.toBeNull()
    expect(vm.form.startDate.getFullYear()).toBe(currentYear)
    expect(vm.form.startDate.getMonth()).toBe(7) // August (0-indexed)
    expect(vm.form.startDate.getDate()).toBe(15)

    expect(vm.form.endDate).not.toBeNull()
    expect(vm.form.endDate.getFullYear()).toBe(currentYear)
    expect(vm.form.endDate.getMonth()).toBe(7) // August
    expect(vm.form.endDate.getDate()).toBe(22)
  })
})
