import { describe, it, expect } from 'vitest'
import { mount } from '@vue/test-utils'
import PrimeVue from 'primevue/config'
import PhoneInput from '../PhoneInput.vue'

const mountComponent = (props: Record<string, unknown> = {}) =>
  mount(PhoneInput, {
    props: {
      modelValue: null,
      ...props,
    },
    global: {
      plugins: [PrimeVue],
    },
  })

describe('PhoneInput', () => {
  describe('rendering', () => {
    it('renders with default country code +34', () => {
      const wrapper = mountComponent()
      const codeInput = wrapper.find('[data-testid="phone-country-code"]')
      expect(codeInput.exists()).toBe(true)
      expect((codeInput.element as HTMLInputElement).value).toBe('34')
    })

    it('renders phone number input with placeholder', () => {
      const wrapper = mountComponent()
      const numberInput = wrapper.find('[data-testid="phone-number"]')
      expect(numberInput.exists()).toBe(true)
      expect(numberInput.attributes('placeholder')).toBe('612345678')
    })

    it('passes id prop to number input', () => {
      const wrapper = mountComponent({ id: 'my-phone' })
      const numberInput = wrapper.find('[data-testid="my-phone-number"]')
      expect(numberInput.attributes('id')).toBe('my-phone')
    })

    it('applies disabled to both inputs', () => {
      const wrapper = mountComponent({ disabled: true })
      const codeInput = wrapper.find('[data-testid="phone-country-code"]')
      const numberInput = wrapper.find('[data-testid="phone-number"]')
      expect(codeInput.attributes('disabled')).toBeDefined()
      expect(numberInput.attributes('disabled')).toBeDefined()
    })

    it('applies invalid state to both inputs', () => {
      const wrapper = mountComponent({ invalid: true })
      const codeInput = wrapper.find('[data-testid="phone-country-code"]')
      const numberInput = wrapper.find('[data-testid="phone-number"]')
      expect(codeInput.classes()).toContain('p-invalid')
    })
  })

  describe('parsing E.164 values', () => {
    it('parses +34612345678 into code=34 and number=612345678', () => {
      const wrapper = mountComponent({ modelValue: '+34612345678' })
      const codeInput = wrapper.find('[data-testid="phone-country-code"]')
      const numberInput = wrapper.find('[data-testid="phone-number"]')
      expect((codeInput.element as HTMLInputElement).value).toBe('34')
      expect((numberInput.element as HTMLInputElement).value).toBe('612345678')
    })

    it('parses +12025551234 into code=1 and number=2025551234', () => {
      const wrapper = mountComponent({ modelValue: '+12025551234' })
      const codeInput = wrapper.find('[data-testid="phone-country-code"]')
      const numberInput = wrapper.find('[data-testid="phone-number"]')
      expect((codeInput.element as HTMLInputElement).value).toBe('1')
      expect((numberInput.element as HTMLInputElement).value).toBe('2025551234')
    })

    it('parses +442071234567 into code=44 and number=2071234567', () => {
      const wrapper = mountComponent({ modelValue: '+442071234567' })
      const codeInput = wrapper.find('[data-testid="phone-country-code"]')
      const numberInput = wrapper.find('[data-testid="phone-number"]')
      expect((codeInput.element as HTMLInputElement).value).toBe('44')
      expect((numberInput.element as HTMLInputElement).value).toBe('2071234567')
    })

    it('parses +351912345678 into code=351 and number=912345678', () => {
      const wrapper = mountComponent({ modelValue: '+351912345678' })
      const codeInput = wrapper.find('[data-testid="phone-country-code"]')
      const numberInput = wrapper.find('[data-testid="phone-number"]')
      expect((codeInput.element as HTMLInputElement).value).toBe('351')
      expect((numberInput.element as HTMLInputElement).value).toBe('912345678')
    })

    it('defaults to code=34 when modelValue is null', () => {
      const wrapper = mountComponent({ modelValue: null })
      const codeInput = wrapper.find('[data-testid="phone-country-code"]')
      const numberInput = wrapper.find('[data-testid="phone-number"]')
      expect((codeInput.element as HTMLInputElement).value).toBe('34')
      expect((numberInput.element as HTMLInputElement).value).toBe('')
    })
  })

  describe('emitting values', () => {
    it('emits null when phone number is empty', async () => {
      const wrapper = mountComponent({ modelValue: '+34612345678' })
      const numberInput = wrapper.find('[data-testid="phone-number"]')
      await numberInput.setValue('')
      await numberInput.trigger('input')
      const emitted = wrapper.emitted('update:modelValue')
      expect(emitted).toBeTruthy()
      const lastEmit = emitted![emitted!.length - 1]
      expect(lastEmit[0]).toBeNull()
    })

    it('emits blur event when number input loses focus', async () => {
      const wrapper = mountComponent()
      const numberInput = wrapper.find('[data-testid="phone-number"]')
      await numberInput.trigger('blur')
      expect(wrapper.emitted('blur')).toBeTruthy()
    })
  })

  describe('updates from external modelValue changes', () => {
    it('updates fields when modelValue changes externally', async () => {
      const wrapper = mountComponent({ modelValue: null })
      await wrapper.setProps({ modelValue: '+44123456789' })
      const codeInput = wrapper.find('[data-testid="phone-country-code"]')
      const numberInput = wrapper.find('[data-testid="phone-number"]')
      expect((codeInput.element as HTMLInputElement).value).toBe('44')
      expect((numberInput.element as HTMLInputElement).value).toBe('123456789')
    })
  })
})
