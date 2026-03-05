import { describe, it, expect, beforeEach, vi } from 'vitest'
import { ref } from 'vue'
import { mount } from '@vue/test-utils'
import PrimeVue from 'primevue/config'
import AnniversaryUploadForm from '../AnniversaryUploadForm.vue'

const mockToastAdd = vi.fn()
const mockUploadFile = vi.fn()
const mockCreateMediaItem = vi.fn()
const mockCreateMemory = vi.fn()

vi.mock('primevue/usetoast', () => ({
  useToast: () => ({ add: mockToastAdd }),
}))

vi.mock('@/composables/useBlobStorage', () => ({
  useBlobStorage: () => ({
    uploadFile: mockUploadFile,
    uploading: ref(false),
    uploadError: ref(null),
  }),
}))

vi.mock('@/composables/useMediaItems', () => ({
  useMediaItems: () => ({
    createMediaItem: mockCreateMediaItem,
    creating: ref(false),
    createError: ref(null),
  }),
}))

vi.mock('@/composables/useMemories', () => ({
  useMemories: () => ({
    createMemory: mockCreateMemory,
    creating: ref(false),
    createError: ref(null),
  }),
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
  ProgressBar: true,
  Button: {
    inheritAttrs: false,
    props: ['label', 'type', 'icon', 'disabled', 'loading'],
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
    mockUploadFile.mockClear()
    mockCreateMediaItem.mockClear()
    mockCreateMemory.mockClear()
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

  it('should show file required error for photo without file', async () => {
    const wrapper = mountForm()
    await wrapper.find('#upload-name').setValue('Test User')
    await wrapper.find('#upload-type').setValue('foto')
    await wrapper.find('form').trigger('submit')
    expect(wrapper.text()).toContain('Debes seleccionar un archivo')
  })

  it('should show description required error for historia without description', async () => {
    const wrapper = mountForm()
    await wrapper.find('#upload-name').setValue('Test User')
    await wrapper.find('#upload-type').setValue('historia')
    await wrapper.find('form').trigger('submit')
    expect(wrapper.text()).toContain('La descripción es obligatoria para historias escritas')
  })

  it('should call createMemory on historia submission', async () => {
    mockCreateMemory.mockResolvedValue({ id: 'memory-1', title: 'Test' })

    const wrapper = mountForm()
    await wrapper.find('#upload-name').setValue('Test User')
    await wrapper.find('#upload-type').setValue('historia')
    await wrapper.find('#upload-description').setValue('A great story about camp')
    await wrapper.find('form').trigger('submit')

    expect(mockCreateMemory).toHaveBeenCalledWith(
      expect.objectContaining({
        title: 'Test User — Historia 50 aniversario',
        content: 'A great story about camp',
      })
    )
  })

  it('should show success toast after successful historia submission', async () => {
    mockCreateMemory.mockResolvedValue({ id: 'memory-1', title: 'Test' })

    const wrapper = mountForm()
    await wrapper.find('#upload-name').setValue('Test User')
    await wrapper.find('#upload-type').setValue('historia')
    await wrapper.find('#upload-description').setValue('A great story about camp')
    await wrapper.find('form').trigger('submit')

    expect(mockToastAdd).toHaveBeenCalledWith(
      expect.objectContaining({ severity: 'success' })
    )
  })

  it('should show error toast on memory creation failure', async () => {
    mockCreateMemory.mockResolvedValue(null)

    const wrapper = mountForm()
    await wrapper.find('#upload-name').setValue('Test User')
    await wrapper.find('#upload-type').setValue('historia')
    await wrapper.find('#upload-description').setValue('A great story about camp')
    await wrapper.find('form').trigger('submit')

    expect(mockToastAdd).toHaveBeenCalledWith(
      expect.objectContaining({ severity: 'error' })
    )
  })

  it('should reset form after successful submission', async () => {
    mockCreateMemory.mockResolvedValue({ id: 'memory-1', title: 'Test' })

    const wrapper = mountForm()
    await wrapper.find('#upload-name').setValue('Test User')
    await wrapper.find('#upload-type').setValue('historia')
    await wrapper.find('#upload-description').setValue('A great story about camp')
    await wrapper.find('form').trigger('submit')

    expect((wrapper.vm as any).form.name).toBe('')
    expect((wrapper.vm as any).form.contentType).toBeNull()
  })
})
