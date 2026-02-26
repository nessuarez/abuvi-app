import { describe, it, expect, vi, beforeEach } from 'vitest'
import { mount } from '@vue/test-utils'
import PrimeVue from 'primevue/config'
import BlobUploadButton from '../BlobUploadButton.vue'
import type { BlobUploadResult } from '@/types/blob-storage'

// ── Mocks ─────────────────────────────────────────────────────────────────────

const mockToastAdd = vi.fn()
vi.mock('primevue/usetoast', () => ({
  useToast: () => ({ add: mockToastAdd })
}))

// Use vi.hoisted() to create Vue-reactive refs BEFORE vi.mock hoisting runs.
// This is required so that template auto-unwrapping (which checks __v_isRef)
// works correctly in the mounted component.
const { mockUploadingRef, mockUploadErrorRef, mockUploadFile } = vi.hoisted(() => {
  // eslint-disable-next-line @typescript-eslint/no-require-imports
  const { ref } = require('vue') as typeof import('vue')
  return {
    mockUploadingRef: ref(false),
    mockUploadErrorRef: ref<string | null>(null),
    mockUploadFile: vi.fn()
  }
})

vi.mock('@/composables/useBlobStorage', () => ({
  useBlobStorage: () => ({
    uploading: mockUploadingRef,
    uploadError: mockUploadErrorRef,
    uploadFile: mockUploadFile
  })
}))

// ── Helpers ───────────────────────────────────────────────────────────────────

const makeResult = (): BlobUploadResult => ({
  fileUrl: 'https://cdn.example.com/photos/abc/photo.jpg',
  thumbnailUrl: null,
  fileName: 'photo.jpg',
  contentType: 'image/jpeg',
  sizeBytes: 1024
})

const stubs = {
  Button: {
    inheritAttrs: false,
    props: ['label', 'icon', 'loading', 'disabled'],
    template: `<button v-bind="$attrs" :disabled="disabled" :data-loading="loading">{{ label }}</button>`
  }
}

function mountComponent(props: Record<string, unknown> = {}) {
  return mount(BlobUploadButton, {
    props: {
      folder: 'photos',
      ...props
    },
    global: {
      plugins: [PrimeVue],
      stubs
    }
  })
}

// ── Tests ─────────────────────────────────────────────────────────────────────

describe('BlobUploadButton', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    mockUploadingRef.value = false
    mockUploadErrorRef.value = null
  })

  it('renders upload button with default label', () => {
    const wrapper = mountComponent()
    expect(wrapper.find('[data-testid="blob-upload-btn"]').text()).toBe('Subir archivo')
  })

  it('renders upload button with custom label prop', () => {
    const wrapper = mountComponent({ label: 'Subir foto' })
    expect(wrapper.find('[data-testid="blob-upload-btn"]').text()).toBe('Subir foto')
  })

  it('clicking button triggers file input click', async () => {
    const wrapper = mountComponent()
    // Spy on the prototype so we catch whichever instance gets clicked
    const clickSpy = vi.spyOn(HTMLInputElement.prototype, 'click').mockImplementation(() => {})

    await wrapper.find('[data-testid="blob-upload-btn"]').trigger('click')

    expect(clickSpy).toHaveBeenCalled()
    clickSpy.mockRestore()
  })

  it('selecting a file calls uploadFile with correct folder and file', async () => {
    mockUploadFile.mockResolvedValue(makeResult())
    const wrapper = mountComponent({ folder: 'media-items' })

    const file = new File(['content'], 'audio.mp3', { type: 'audio/mpeg' })
    const fileInput = wrapper.find('[data-testid="blob-file-input"]')

    Object.defineProperty(fileInput.element, 'files', {
      value: [file],
      configurable: true
    })
    await fileInput.trigger('change')

    expect(mockUploadFile).toHaveBeenCalledWith(
      expect.objectContaining({ file, folder: 'media-items' })
    )
  })

  it('emits uploaded event on success', async () => {
    const result = makeResult()
    mockUploadFile.mockResolvedValue(result)
    const wrapper = mountComponent()

    const file = new File(['content'], 'photo.jpg', { type: 'image/jpeg' })
    const fileInput = wrapper.find('[data-testid="blob-file-input"]')
    Object.defineProperty(fileInput.element, 'files', {
      value: [file],
      configurable: true
    })
    await fileInput.trigger('change')

    expect(wrapper.emitted('uploaded')).toBeTruthy()
    expect(wrapper.emitted('uploaded')![0]).toEqual([result])
  })

  it('emits error event on upload failure', async () => {
    mockUploadFile.mockResolvedValue(null)
    mockUploadErrorRef.value = 'Tipo de archivo no permitido'
    const wrapper = mountComponent()

    const file = new File(['content'], 'photo.jpg', { type: 'image/jpeg' })
    const fileInput = wrapper.find('[data-testid="blob-file-input"]')
    Object.defineProperty(fileInput.element, 'files', {
      value: [file],
      configurable: true
    })
    await fileInput.trigger('change')

    expect(wrapper.emitted('error')).toBeTruthy()
    expect(wrapper.emitted('error')![0]).toEqual(['Tipo de archivo no permitido'])
    expect(mockToastAdd).toHaveBeenCalledWith(
      expect.objectContaining({ severity: 'error' })
    )
  })

  it('button is disabled while uploading', async () => {
    mockUploadingRef.value = true
    const wrapper = mountComponent()
    await wrapper.vm.$nextTick()
    const btn = wrapper.find('[data-testid="blob-upload-btn"]')
    expect((btn.element as HTMLButtonElement).disabled).toBe(true)
  })

  it('button is disabled when disabled prop is true', () => {
    const wrapper = mountComponent({ disabled: true })
    const btn = wrapper.find('[data-testid="blob-upload-btn"]')
    expect((btn.element as HTMLButtonElement).disabled).toBe(true)
  })
})
