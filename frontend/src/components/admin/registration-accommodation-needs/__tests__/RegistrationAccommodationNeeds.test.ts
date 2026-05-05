import { describe, it, expect, vi, beforeEach } from 'vitest'
import { ref } from 'vue'
import { shallowMount } from '@vue/test-utils'
import PrimeVue from 'primevue/config'
import RegistrationAccommodationNeeds from '../RegistrationAccommodationNeeds.vue'
import type { AccommodationNeedResponse } from '@/types/registration'
import type { AccommodationFeature } from '@/types/accommodation-feature'

const mockToastAdd = vi.fn()
const mockFetchFeatures = vi.fn()
const mockUpdateNeeds = vi.fn()
const mockUpdateNotes = vi.fn()

const mockFeatures = ref<AccommodationFeature[]>([])
const mockNeeds = ref<AccommodationNeedResponse[]>([])
const mockInternalNotes = ref<string | null>(null)
const mockSaving = ref(false)
const mockSaveError = ref<string | null>(null)

vi.mock('primevue/usetoast', () => ({
  useToast: () => ({ add: mockToastAdd }),
}))

vi.mock('@/composables/useAccommodationFeatures', () => ({
  useAccommodationFeatures: () => ({
    features: mockFeatures,
    loading: ref(false),
    fetchFeatures: mockFetchFeatures,
  }),
}))

vi.mock('@/composables/useRegistrationAccommodationTagging', () => ({
  useRegistrationAccommodationTagging: () => ({
    needs: mockNeeds,
    internalNotes: mockInternalNotes,
    saving: mockSaving,
    saveError: mockSaveError,
    updateNeeds: mockUpdateNeeds,
    updateNotes: mockUpdateNotes,
  }),
}))

const defaultProps = {
  registrationId: 'reg-1',
  initialNeeds: [] as AccommodationNeedResponse[],
  initialNotes: null as string | null,
  specialNeeds: 'Sin gluten',
  campatesPreference: 'Familia García',
}

function mountComponent(props = defaultProps) {
  return shallowMount(RegistrationAccommodationNeeds, {
    props,
    global: { plugins: [PrimeVue] },
  })
}

describe('RegistrationAccommodationNeeds', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    mockNeeds.value = []
    mockInternalNotes.value = null
    mockSaving.value = false
    mockSaveError.value = null
    mockFeatures.value = []
  })

  it('renders family free-text fields in read mode', () => {
    const wrapper = mountComponent()

    expect(wrapper.text()).toContain('Sin gluten')
    expect(wrapper.text()).toContain('Familia García')
  })

  it('calls fetchFeatures(true) on mount', () => {
    mountComponent()

    expect(mockFetchFeatures).toHaveBeenCalledWith(true)
  })

  it('shows "Sin etiquetas" when needs are empty', () => {
    const wrapper = mountComponent()

    expect(wrapper.text()).toContain('Sin etiquetas')
  })

  it('shows existing need chips in read mode', async () => {
    mockNeeds.value = [
      {
        featureId: 'feat-1',
        featureName: 'Habitación privada',
        featureCategory: 'Accessibility',
        taggedByUserId: null,
        createdAt: '2026-05-01T10:00:00Z',
      },
    ]
    const wrapper = mountComponent({
      ...defaultProps,
      initialNeeds: mockNeeds.value,
    })

    expect(wrapper.text()).toContain('Habitación privada')
  })

  it('shows edit button for tags and enters edit mode on click', async () => {
    const wrapper = mountComponent()

    const editBtn = wrapper.find('[data-testid="edit-tags-btn"]')
    expect(editBtn.exists()).toBe(true)
    await editBtn.trigger('click')

    expect(wrapper.find('[data-testid="features-multiselect"]').exists()).toBe(true)
  })

  it('calls updateNeeds with selected IDs on save', async () => {
    mockUpdateNeeds.mockResolvedValueOnce({ registrationId: 'reg-1', needs: [] })
    const wrapper = mountComponent()

    await wrapper.find('[data-testid="edit-tags-btn"]').trigger('click')
    await wrapper.find('[data-testid="save-tags-btn"]').trigger('click')

    expect(mockUpdateNeeds).toHaveBeenCalledWith('reg-1', [])
  })

  it('emits updated event with new needs after successful save', async () => {
    const updatedNeeds = [
      {
        featureId: 'feat-2',
        featureName: 'Planta baja',
        featureCategory: 'Mobility',
        taggedByUserId: 'user-1',
        createdAt: '2026-05-01T10:00:00Z',
      },
    ]
    mockUpdateNeeds.mockResolvedValueOnce({ registrationId: 'reg-1', needs: updatedNeeds })
    const wrapper = mountComponent()

    await wrapper.find('[data-testid="edit-tags-btn"]').trigger('click')
    await wrapper.find('[data-testid="save-tags-btn"]').trigger('click')
    await wrapper.vm.$nextTick()

    expect(wrapper.emitted('updated')).toBeTruthy()
    expect(wrapper.emitted('updated')![0]).toEqual([updatedNeeds])
  })

  it('shows "Sin notas internas" when no notes', () => {
    const wrapper = mountComponent()

    expect(wrapper.text()).toContain('Sin notas internas')
  })

  it('shows existing notes in read mode', () => {
    mockInternalNotes.value = 'Familia necesita planta baja'
    const wrapper = mountComponent({ ...defaultProps, initialNotes: 'Familia necesita planta baja' })

    expect(wrapper.text()).toContain('Familia necesita planta baja')
  })

  it('enters notes edit mode and calls updateNotes on save', async () => {
    mockUpdateNotes.mockResolvedValueOnce({
      registrationId: 'reg-1',
      accommodationInternalNotes: 'Nueva nota',
      updatedAt: '2026-05-01T10:00:00Z',
    })
    const wrapper = mountComponent()

    await wrapper.find('[data-testid="edit-notes-btn"]').trigger('click')
    await wrapper.find('[data-testid="save-notes-btn"]').trigger('click')

    expect(mockUpdateNotes).toHaveBeenCalled()
  })
})
