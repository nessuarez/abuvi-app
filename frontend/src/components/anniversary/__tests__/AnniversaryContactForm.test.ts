import { describe, it, expect, beforeEach, vi } from 'vitest'
import { mount } from '@vue/test-utils'
import PrimeVue from 'primevue/config'
import AnniversaryContactForm from '../AnniversaryContactForm.vue'

const mockToastAdd = vi.fn()

vi.mock('primevue/usetoast', () => ({
  useToast: () => ({ add: mockToastAdd }),
}))

const stubs = {
  InputText: {
    inheritAttrs: false,
    props: ['modelValue', 'type', 'invalid', 'placeholder'],
    emits: ['update:modelValue'],
    template:
      '<input v-bind="$attrs" :value="modelValue" @input="$emit(\'update:modelValue\', $event.target.value)" />',
  },
  Textarea: {
    inheritAttrs: false,
    props: ['modelValue', 'maxlength', 'rows', 'invalid', 'placeholder'],
    emits: ['update:modelValue'],
    template:
      '<textarea v-bind="$attrs" :value="modelValue" @input="$emit(\'update:modelValue\', $event.target.value)" />',
  },
  Button: {
    inheritAttrs: false,
    props: ['label', 'type', 'icon'],
    template: '<button v-bind="$attrs" :type="type || \'button\'">{{ label }}</button>',
  },
}

function mountForm() {
  return mount(AnniversaryContactForm, {
    global: {
      plugins: [PrimeVue],
      stubs,
    },
  })
}

describe('AnniversaryContactForm', () => {
  beforeEach(() => {
    mockToastAdd.mockClear()
  })

  it('should show validation error when name is empty', async () => {
    const wrapper = mountForm()
    await wrapper.find('form').trigger('submit')
    expect(wrapper.text()).toContain('El nombre es obligatorio')
  })

  it('should show validation error when email is invalid', async () => {
    const wrapper = mountForm()
    await wrapper.find('#contact-name').setValue('Test User')
    await wrapper.find('#contact-email').setValue('not-an-email')
    await wrapper.find('form').trigger('submit')
    expect(wrapper.text()).toContain('El correo electrónico no es válido')
  })

  it('should show validation error when message is empty', async () => {
    const wrapper = mountForm()
    await wrapper.find('#contact-name').setValue('Test User')
    await wrapper.find('#contact-email').setValue('test@example.com')
    await wrapper.find('form').trigger('submit')
    expect(wrapper.text()).toContain('El mensaje es obligatorio')
  })

  it('should call toast and reset form on valid submission', async () => {
    const wrapper = mountForm()
    await wrapper.find('#contact-name').setValue('Test User')
    await wrapper.find('#contact-email').setValue('test@example.com')
    await wrapper.find('#contact-message').setValue('Este es mi mensaje de prueba')
    await wrapper.find('form').trigger('submit')
    expect(mockToastAdd).toHaveBeenCalledWith(expect.objectContaining({ severity: 'success' }))
    expect((wrapper.vm as any).form.name).toBe('')
    expect((wrapper.vm as any).form.email).toBe('')
    expect((wrapper.vm as any).form.message).toBe('')
  })
})
