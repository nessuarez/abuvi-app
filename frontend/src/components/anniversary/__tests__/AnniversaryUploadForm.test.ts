import { describe, it, expect, beforeEach, vi } from 'vitest'
import { mount } from '@vue/test-utils'
import PrimeVue from 'primevue/config'
import AnniversaryUploadForm from '../AnniversaryUploadForm.vue'

const mockToastAdd = vi.fn()

vi.mock('primevue/usetoast', () => ({
  useToast: () => ({ add: mockToastAdd }),
}))

const stubs = {
  InputText: {
    inheritAttrs: false,
    props: ['modelValue', 'invalid', 'placeholder'],
    emits: ['update:modelValue'],
    template:
      '<input v-bind="$attrs" :value="modelValue" @input="$emit(\'update:modelValue\', $event.target.value)" />',
  },
  Select: {
    inheritAttrs: false,
    props: ['modelValue', 'options', 'optionLabel', 'optionValue', 'placeholder', 'invalid'],
    emits: ['update:modelValue'],
    template: `<select v-bind="$attrs" :value="modelValue" @change="$emit('update:modelValue', $event.target.value)">
      <option value="">Seleccionar</option>
      <option v-for="opt in (options || [])" :key="opt.value" :value="opt.value">{{ opt.label }}</option>
    </select>`,
  },
  Textarea: {
    inheritAttrs: false,
    props: ['modelValue', 'maxlength', 'rows', 'invalid', 'placeholder'],
    emits: ['update:modelValue'],
    template:
      '<textarea v-bind="$attrs" :value="modelValue" @input="$emit(\'update:modelValue\', $event.target.value)" />',
  },
  InputNumber: true,
  FileUpload: true,
  Button: {
    inheritAttrs: false,
    props: ['label', 'type', 'icon', 'disabled'],
    template: '<button v-bind="$attrs" :type="type || \'button\'" :disabled="disabled">{{ label }}</button>',
  },
}

function mountForm() {
  return mount(AnniversaryUploadForm, {
    global: {
      plugins: [PrimeVue],
      stubs,
    },
  })
}

describe('AnniversaryUploadForm', () => {
  beforeEach(() => {
    mockToastAdd.mockClear()
  })

  it('should show validation error when name is empty', async () => {
    const wrapper = mountForm()
    await wrapper.find('form').trigger('submit')
    expect(wrapper.text()).toContain('El nombre es obligatorio')
  })

  it('should show validation error when content type not selected', async () => {
    const wrapper = mountForm()
    await wrapper.find('#upload-name').setValue('Test User')
    await wrapper.find('form').trigger('submit')
    expect(wrapper.text()).toContain('El tipo de contenido es obligatorio')
  })

  it('should call toast and reset form on valid submission', async () => {
    const wrapper = mountForm()
    await wrapper.find('#upload-name').setValue('Test User')
    await wrapper.find('#upload-type').setValue('foto')
    await wrapper.find('form').trigger('submit')
    expect(mockToastAdd).toHaveBeenCalledWith(expect.objectContaining({ severity: 'success' }))
    expect((wrapper.vm as any).form.name).toBe('')
    expect((wrapper.vm as any).form.contentType).toBeNull()
  })
})
