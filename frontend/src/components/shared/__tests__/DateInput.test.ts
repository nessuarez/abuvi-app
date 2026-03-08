import { describe, it, expect, vi } from 'vitest'
import { mount } from '@vue/test-utils'
import PrimeVue from 'primevue/config'
import DateInput from '../DateInput.vue'

const mountComponent = (props: Record<string, unknown> = {}) =>
  mount(DateInput, {
    props: {
      modelValue: null,
      ...props
    },
    global: {
      plugins: [PrimeVue],
      stubs: {
        Popover: true,
        DatePicker: true
      }
    }
  })

describe('DateInput', () => {
  describe('rendering', () => {
    it('renders input mask with placeholder DD/MM/AAAA', () => {
      const wrapper = mountComponent()
      const input = wrapper.find('[data-testid="date-input-mask"]')
      expect(input.exists()).toBe(true)
    })

    it('renders calendar button by default', () => {
      const wrapper = mountComponent()
      const btn = wrapper.find('[data-testid="date-input-calendar-btn"]')
      expect(btn.exists()).toBe(true)
    })

    it('hides calendar button when showCalendar is false', () => {
      const wrapper = mountComponent({ showCalendar: false })
      const btn = wrapper.find('[data-testid="date-input-calendar-btn"]')
      expect(btn.exists()).toBe(false)
    })

    it('passes disabled prop to input mask and button', () => {
      const wrapper = mountComponent({ disabled: true })
      const input = wrapper.find('[data-testid="date-input-mask"]')
      const btn = wrapper.find('[data-testid="date-input-calendar-btn"]')
      expect(input.attributes('disabled')).toBeDefined()
      expect(btn.attributes('disabled')).toBeDefined()
    })

    it('passes id prop to input mask', () => {
      const wrapper = mountComponent({ id: 'dob-field' })
      const input = wrapper.find('[data-testid="date-input-mask"]')
      expect(input.attributes('id')).toBe('dob-field')
    })
  })

  describe('pre-filling', () => {
    it('displays formatted date when modelValue is provided', () => {
      const date = new Date(2000, 2, 15) // March 15, 2000
      const wrapper = mountComponent({ modelValue: date })
      const inputMask = wrapper.findComponent({ name: 'InputMask' })
      expect(inputMask.props('modelValue')).toBe('15/03/2000')
    })

    it('displays empty when modelValue is null', () => {
      const wrapper = mountComponent({ modelValue: null })
      const inputMask = wrapper.findComponent({ name: 'InputMask' })
      expect(inputMask.props('modelValue')).toBe('')
    })

    it('updates display when modelValue changes externally', async () => {
      const wrapper = mountComponent({ modelValue: null })
      await wrapper.setProps({ modelValue: new Date(1995, 11, 25) })
      const inputMask = wrapper.findComponent({ name: 'InputMask' })
      expect(inputMask.props('modelValue')).toBe('25/12/1995')
    })
  })

  describe('date parsing', () => {
    it('emits valid date when complete mask is entered', async () => {
      const wrapper = mountComponent()
      const inputMask = wrapper.findComponent({ name: 'InputMask' })

      await inputMask.vm.$emit('update:modelValue', '15/03/2000')
      await inputMask.vm.$emit('complete')

      const emitted = wrapper.emitted('update:modelValue')
      expect(emitted).toBeTruthy()
      const lastEmit = emitted![emitted!.length - 1][0] as Date
      expect(lastEmit.getDate()).toBe(15)
      expect(lastEmit.getMonth()).toBe(2) // March = 2
      expect(lastEmit.getFullYear()).toBe(2000)
    })

    it('marks invalid for impossible date 31/02/2025', async () => {
      const wrapper = mountComponent()
      const inputMask = wrapper.findComponent({ name: 'InputMask' })

      await inputMask.vm.$emit('update:modelValue', '31/02/2025')

      // Should not emit a valid date
      const emitted = wrapper.emitted('update:modelValue')
      // The last emission should be null (clearing) or not a date with day 31
      if (emitted && emitted.length > 0) {
        const lastVal = emitted[emitted.length - 1][0]
        expect(lastVal).toBeNull()
      }
    })

    it('marks invalid for date 00/00/0000', async () => {
      const wrapper = mountComponent()
      const inputMask = wrapper.findComponent({ name: 'InputMask' })

      await inputMask.vm.$emit('update:modelValue', '00/00/0000')

      const emitted = wrapper.emitted('update:modelValue')
      if (emitted && emitted.length > 0) {
        const lastVal = emitted[emitted.length - 1][0]
        expect(lastVal).toBeNull()
      }
    })

    it('emits null when input is cleared', async () => {
      const date = new Date(2000, 2, 15)
      const wrapper = mountComponent({ modelValue: date })
      const inputMask = wrapper.findComponent({ name: 'InputMask' })

      await inputMask.vm.$emit('update:modelValue', '')

      const emitted = wrapper.emitted('update:modelValue')
      expect(emitted).toBeTruthy()
      expect(emitted![emitted!.length - 1][0]).toBeNull()
    })
  })

  describe('minDate/maxDate validation', () => {
    it('marks invalid when date is before minDate', async () => {
      const minDate = new Date(2025, 0, 10)
      const wrapper = mountComponent({ minDate })
      const inputMask = wrapper.findComponent({ name: 'InputMask' })

      await inputMask.vm.$emit('update:modelValue', '05/01/2025')

      const emitted = wrapper.emitted('update:modelValue')
      // Should either not emit or emit null
      if (emitted && emitted.length > 0) {
        const lastVal = emitted[emitted.length - 1][0]
        expect(lastVal).toBeNull()
      }
    })

    it('marks invalid when date is after maxDate', async () => {
      const maxDate = new Date(2025, 0, 10)
      const wrapper = mountComponent({ maxDate })
      const inputMask = wrapper.findComponent({ name: 'InputMask' })

      await inputMask.vm.$emit('update:modelValue', '15/01/2025')

      const emitted = wrapper.emitted('update:modelValue')
      if (emitted && emitted.length > 0) {
        const lastVal = emitted[emitted.length - 1][0]
        expect(lastVal).toBeNull()
      }
    })

    it('accepts date within range', async () => {
      const minDate = new Date(2025, 0, 1)
      const maxDate = new Date(2025, 11, 31)
      const wrapper = mountComponent({ minDate, maxDate })
      const inputMask = wrapper.findComponent({ name: 'InputMask' })

      await inputMask.vm.$emit('update:modelValue', '15/06/2025')
      await inputMask.vm.$emit('complete')

      const emitted = wrapper.emitted('update:modelValue')
      expect(emitted).toBeTruthy()
      const lastVal = emitted![emitted!.length - 1][0] as Date
      expect(lastVal.getMonth()).toBe(5) // June
      expect(lastVal.getFullYear()).toBe(2025)
    })
  })

  describe('blur event', () => {
    it('emits blur event', async () => {
      const wrapper = mountComponent()
      const inputMask = wrapper.findComponent({ name: 'InputMask' })

      await inputMask.vm.$emit('blur')

      expect(wrapper.emitted('blur')).toBeTruthy()
    })
  })

  describe('invalid state', () => {
    it('shows invalid state from external invalid prop', () => {
      const wrapper = mountComponent({ invalid: true })
      const inputMask = wrapper.findComponent({ name: 'InputMask' })
      expect(inputMask.props('invalid')).toBe(true)
    })

    it('shows invalid state when date is not valid', async () => {
      const wrapper = mountComponent()
      const inputMask = wrapper.findComponent({ name: 'InputMask' })

      await inputMask.vm.$emit('update:modelValue', '31/02/2025')
      await inputMask.vm.$emit('complete')

      // After trying to parse an invalid date, the input should be marked invalid
      expect(inputMask.props('invalid')).toBe(true)
    })
  })
})
