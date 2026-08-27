import { describe, it, expect, beforeEach, vi } from 'vitest'
import { computed, nextTick, ref } from 'vue'
import { mount } from '@vue/test-utils'
import PrimeVue from 'primevue/config'
import AnniversaryUploadForm from '../AnniversaryUploadForm.vue'
import type { CampEditionOption } from '@/types/camp-history'

const mockToastAdd = vi.fn()
const mockUploadFile = vi.fn()
const mockCreateMediaItem = vi.fn()
const mockCreateMemory = vi.fn()
const mockFetchEditionOptions = vi.fn()

const mockEditionOptions = ref<CampEditionOption[]>([
  { year: 2015, label: '2015 — Espinosa de los Monteros', campName: 'Espinosa de los Monteros', isCurrent: false },
  { year: 2003, label: '2003 — Espinosa de los Monteros', campName: 'Espinosa de los Monteros', isCurrent: false },
])

const mockRoute = ref<{ query: Record<string, string>; hash: string }>({ query: {}, hash: '' })

vi.mock('vue-router', () => ({
  useRoute: () => mockRoute.value,
}))

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

vi.mock('@/composables/useCampHistory', () => ({
  useCampHistory: () => ({
    editionOptions: computed(() => mockEditionOptions.value),
    fetchEditionOptions: mockFetchEditionOptions,
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
    props: ['modelValue', 'options', 'optionLabel', 'optionValue', 'placeholder', 'invalid', 'filter'],
    emits: ['update:modelValue'],
    methods: {
      // A DOM <select> yields strings; PrimeVue emits the option's real value, so resolve
      // it back or numeric years would reach the API as text.
      pick(raw: string) {
        const match = (this.options || []).find(
          (o: Record<string, unknown>) => String(o.value ?? o.year) === raw
        )
        return match ? (match.value ?? match.year) : raw
      },
    },
    template: `<select v-bind="$attrs" :value="modelValue" @change="$emit('update:modelValue', pick($event.target.value))">
      <option value="">Seleccionar</option>
      <option v-for="opt in (options || [])" :key="opt.value ?? opt.year" :value="opt.value ?? opt.year">{{ opt.label }}</option>
    </select>`,
  },
  Checkbox: {
    inheritAttrs: false,
    props: ['modelValue', 'inputId', 'binary', 'invalid'],
    emits: ['update:modelValue'],
    template:
      '<input type="checkbox" :id="inputId" :checked="modelValue" @change="$emit(\'update:modelValue\', $event.target.checked)" />',
  },
  Textarea: {
    inheritAttrs: false,
    props: ['modelValue', 'maxlength', 'rows', 'invalid', 'placeholder'],
    emits: ['update:modelValue'],
    template:
      '<textarea v-bind="$attrs" :value="modelValue" @input="$emit(\'update:modelValue\', $event.target.value)" />',
  },
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

/** Fills everything a written story needs, including the rights declaration. */
async function fillValidStory(wrapper: ReturnType<typeof mountForm>) {
  await wrapper.find('#upload-name').setValue('Test User')
  await wrapper.find('#upload-type').setValue('historia')
  await wrapper.find('#upload-year').setValue('2003')
  await wrapper.find('#upload-description').setValue('A great story about camp')
  await wrapper.find('#upload-rights').setValue(true)
}

describe('AnniversaryUploadForm', () => {
  beforeEach(() => {
    mockToastAdd.mockClear()
    mockUploadFile.mockClear()
    mockCreateMediaItem.mockClear()
    mockCreateMemory.mockClear()
    mockFetchEditionOptions.mockClear()
    mockRoute.value = { query: {}, hash: '' }
    mockEditionOptions.value = [
      { year: 2015, label: '2015 — Espinosa de los Monteros', campName: 'Espinosa de los Monteros', isCurrent: false },
      { year: 2003, label: '2003 — Espinosa de los Monteros', campName: 'Espinosa de los Monteros', isCurrent: false },
    ]
  })

  describe('the form is live', () => {
    it('offers a working submit button, not a coming-soon placeholder', () => {
      const wrapper = mountForm()
      const button = wrapper.find('button[type="submit"]')

      expect(button.text()).toBe('Enviar recuerdo')
      expect(button.attributes('disabled')).toBeUndefined()
      expect(wrapper.text()).not.toContain('Próximamente')
    })

    it('loads the edition options on mount', () => {
      mountForm()
      expect(mockFetchEditionOptions).toHaveBeenCalledTimes(1)
    })
  })

  describe('validation', () => {
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

    it('requires an edition instead of silently defaulting to the current year', async () => {
      const wrapper = mountForm()
      await wrapper.find('#upload-name').setValue('Test User')
      await wrapper.find('#upload-type').setValue('historia')
      await wrapper.find('#upload-description').setValue('A great story about camp')
      await wrapper.find('#upload-rights').setValue(true)
      await wrapper.find('form').trigger('submit')

      expect(wrapper.text()).toContain('Elige la edición a la que pertenece el recuerdo')
      expect(mockCreateMemory).not.toHaveBeenCalled()
    })
  })

  describe('image rights declaration', () => {
    it('refuses to submit until the declaration is ticked', async () => {
      const wrapper = mountForm()
      await wrapper.find('#upload-name').setValue('Test User')
      await wrapper.find('#upload-type').setValue('historia')
      await wrapper.find('#upload-year').setValue('2003')
      await wrapper.find('#upload-description').setValue('A great story about camp')
      await wrapper.find('form').trigger('submit')

      expect(wrapper.text()).toContain('Debes aceptar esta declaración para enviar')
      expect(mockCreateMemory).not.toHaveBeenCalled()
    })

    it('submits once it is ticked', async () => {
      mockCreateMemory.mockResolvedValue({ id: 'memory-1', title: 'Test' })

      const wrapper = mountForm()
      await fillValidStory(wrapper)
      await wrapper.find('form').trigger('submit')

      expect(mockCreateMemory).toHaveBeenCalled()
    })

    it('shows the exact wording agreed for the declaration', () => {
      const wrapper = mountForm()
      expect(wrapper.text()).toContain(
        'Tengo derecho a compartir esta imagen y respeto la privacidad de quienes aparecen.'
      )
    })

    it('clears the declaration after a successful submit', async () => {
      mockCreateMemory.mockResolvedValue({ id: 'memory-1', title: 'Test' })

      const wrapper = mountForm()
      await fillValidStory(wrapper)
      await wrapper.find('form').trigger('submit')

      expect((wrapper.vm as any).form.rightsAccepted).toBe(false)
    })
  })

  describe('review and takedown notice', () => {
    it('says the content is reviewed before publication and how to have it removed', () => {
      const wrapper = mountForm()
      expect(wrapper.text()).toContain('se revisa antes de publicarse')
      expect(wrapper.text()).toContain('retiremos algo en lo que apareces')
    })
  })

  describe('contributing for someone else', () => {
    it('says the name can be the person who remembers', () => {
      const wrapper = mountForm()
      expect(wrapper.text()).toContain('Si subes el recuerdo de otra persona, pon el suyo.')
    })
  })

  describe('edition selector', () => {
    it('lists the editions it was given', () => {
      const wrapper = mountForm()
      const labels = wrapper.findAll('#upload-year option').map((o) => o.text())

      expect(labels).toContain('2015 — Espinosa de los Monteros')
      expect(labels).toContain('2003 — Espinosa de los Monteros')
    })

    it('sends the selected year with the contribution', async () => {
      mockCreateMemory.mockResolvedValue({ id: 'memory-1', title: 'Test' })

      const wrapper = mountForm()
      await fillValidStory(wrapper)
      await wrapper.find('form').trigger('submit')

      expect(mockCreateMemory).toHaveBeenCalledWith(expect.objectContaining({ year: 2003 }))
    })
  })

  describe('QR deep link', () => {
    it('preselects the content type and the year from the query string', async () => {
      mockRoute.value = { query: { tipo: 'audio', anio: '2015' }, hash: '#subir-recuerdo' }

      const wrapper = mountForm()

      expect((wrapper.vm as any).form.contentType).toBe('audio')
      expect((wrapper.vm as any).form.year).toBe(2015)
    })

    it('ignores an unrecognised content type instead of breaking', () => {
      mockRoute.value = { query: { tipo: 'xyz', anio: '2015' }, hash: '' }

      const wrapper = mountForm()

      expect((wrapper.vm as any).form.contentType).toBeNull()
      expect((wrapper.vm as any).form.year).toBe(2015)
    })

    it('ignores a year that is not a plausible one', () => {
      mockRoute.value = { query: { anio: 'hace mucho' }, hash: '' }

      const wrapper = mountForm()

      expect((wrapper.vm as any).form.year).toBeNull()
    })

    it('still offers a year the endpoints do not know about, so the poster keeps working', async () => {
      mockRoute.value = { query: { anio: '2026' }, hash: '' }

      const wrapper = mountForm()
      await nextTick()
      const labels = wrapper.findAll('#upload-year option').map((o) => o.text())

      expect(labels).toContain('2026')
      expect((wrapper.vm as any).form.year).toBe(2026)
    })
  })

  describe('submission', () => {
    it('should call createMemory on historia submission', async () => {
      mockCreateMemory.mockResolvedValue({ id: 'memory-1', title: 'Test' })

      const wrapper = mountForm()
      await fillValidStory(wrapper)
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
      await fillValidStory(wrapper)
      await wrapper.find('form').trigger('submit')

      expect(mockToastAdd).toHaveBeenCalledWith(expect.objectContaining({ severity: 'success' }))
    })

    it('should show error toast on memory creation failure', async () => {
      mockCreateMemory.mockResolvedValue(null)

      const wrapper = mountForm()
      await fillValidStory(wrapper)
      await wrapper.find('form').trigger('submit')

      expect(mockToastAdd).toHaveBeenCalledWith(expect.objectContaining({ severity: 'error' }))
    })

    it('keeps what was typed when the submission fails', async () => {
      mockCreateMemory.mockResolvedValue(null)

      const wrapper = mountForm()
      await fillValidStory(wrapper)
      await wrapper.find('form').trigger('submit')

      // Nobody should have to retype a story because the connection dropped.
      expect((wrapper.vm as any).form.description).toBe('A great story about camp')
    })

    it('should reset form after successful submission', async () => {
      mockCreateMemory.mockResolvedValue({ id: 'memory-1', title: 'Test' })

      const wrapper = mountForm()
      await fillValidStory(wrapper)
      await wrapper.find('form').trigger('submit')

      expect((wrapper.vm as any).form.name).toBe('')
      expect((wrapper.vm as any).form.contentType).toBeNull()
      expect((wrapper.vm as any).form.year).toBeNull()
    })
  })
})
