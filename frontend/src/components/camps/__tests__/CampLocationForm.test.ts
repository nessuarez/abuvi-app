import { describe, it, expect, vi, beforeEach } from 'vitest'
import { mount, flushPromises } from '@vue/test-utils'
import CampLocationForm from '@/components/camps/CampLocationForm.vue'
import { useGooglePlaces } from '@/composables/useGooglePlaces'

// Mock the useGooglePlaces composable
vi.mock('@/composables/useGooglePlaces')

// Mock PrimeVue components to simplify testing
vi.mock('primevue/autocomplete', () => ({
  default: {
    name: 'AutoComplete',
    props: ['modelValue', 'suggestions', 'optionLabel', 'loading', 'invalid', 'placeholder'],
    emits: ['update:modelValue', 'complete', 'item-select'],
    template: `<input
      :id="$attrs.id"
      :value="modelValue"
      :data-invalid="invalid"
      :placeholder="placeholder"
      @input="$emit('update:modelValue', $event.target.value)"
      @keyup="$emit('complete', $event)"
    />`
  }
}))

vi.mock('primevue/message', () => ({
  default: {
    name: 'Message',
    props: ['severity', 'closable'],
    template: '<div :data-severity="severity"><slot /></div>'
  }
}))

vi.mock('primevue/inputtext', () => ({
  default: {
    name: 'InputText',
    props: ['modelValue', 'invalid', 'placeholder'],
    emits: ['update:modelValue'],
    template: `<input
      :id="$attrs.id"
      :value="modelValue"
      @input="$emit('update:modelValue', $event.target.value)"
    />`
  }
}))

vi.mock('primevue/textarea', () => ({
  default: {
    name: 'Textarea',
    props: ['modelValue', 'rows', 'placeholder'],
    emits: ['update:modelValue'],
    template: `<textarea
      :id="$attrs.id"
      :value="modelValue"
      @input="$emit('update:modelValue', $event.target.value)"
    ></textarea>`
  }
}))

vi.mock('primevue/inputnumber', () => ({
  default: {
    name: 'InputNumber',
    props: ['modelValue', 'invalid', 'mode', 'currency', 'locale', 'min', 'max'],
    emits: ['update:modelValue'],
    template: `<input
      :id="$attrs.id"
      type="number"
      :value="modelValue"
      @input="$emit('update:modelValue', Number($event.target.value))"
    />`
  }
}))

vi.mock('primevue/button', () => ({
  default: {
    name: 'Button',
    props: ['label', 'icon', 'text', 'size', 'loading', 'disabled', 'severity', 'outlined'],
    emits: ['click'],
    template: `<button
      :disabled="disabled"
      :data-loading="loading"
      :type="$attrs.type"
      @click="$emit('click')"
    >{{ label }}</button>`
  }
}))

// Mock VueUse debounce
vi.mock('@vueuse/core', () => ({
  useDebounceFn: (fn: Function) => fn
}))

describe('CampLocationForm', () => {
  const defaultMockGooglePlaces = {
    loading: { value: false },
    error: { value: null },
    searchPlaces: vi.fn().mockResolvedValue([]),
    getPlaceDetails: vi.fn().mockResolvedValue(null)
  }

  beforeEach(() => {
    vi.clearAllMocks()
    vi.mocked(useGooglePlaces).mockReturnValue(defaultMockGooglePlaces as any)
  })

  describe('Rendering', () => {
    it('should render the form correctly in create mode', () => {
      const wrapper = mount(CampLocationForm, {
        props: { mode: 'create' }
      })

      expect(wrapper.find('form').exists()).toBe(true)
      expect(wrapper.find('#name').exists()).toBe(true)
      expect(wrapper.find('#description').exists()).toBe(true)
      expect(wrapper.find('#location').exists()).toBe(true)
      expect(wrapper.find('#latitude').exists()).toBe(true)
      expect(wrapper.find('#longitude').exists()).toBe(true)
    })

    it('should not show status checkbox in create mode', () => {
      const wrapper = mount(CampLocationForm, {
        props: { mode: 'create' }
      })

      expect(wrapper.find('#isActive').exists()).toBe(false)
    })

    it('should show status checkbox in edit mode', () => {
      const wrapper = mount(CampLocationForm, {
        props: {
          mode: 'edit',
          camp: {
            id: '1',
            name: 'Test Camp',
            description: null,
            location: null,
            latitude: null,
            longitude: null,
            googlePlaceId: null,
            pricePerAdult: 100,
            pricePerChild: 50,
            pricePerBaby: 0,
            isActive: true,
            createdAt: '2024-01-01T00:00:00Z',
            updatedAt: '2024-01-01T00:00:00Z'
          }
        }
      })

      expect(wrapper.find('#isActive').exists()).toBe(true)
    })

    it('should pre-fill form data when editing an existing camp', () => {
      const camp = {
        id: '1',
        name: 'Mountain Camp',
        description: 'Beautiful camp',
        location: 'Mountains, Spain',
        latitude: 40.416775,
        longitude: -3.703790,
        googlePlaceId: 'ChIJ123',
        pricePerAdult: 100,
        pricePerChild: 50,
        pricePerBaby: 0,
        isActive: true,
        createdAt: '2024-01-01T00:00:00Z',
        updatedAt: '2024-01-01T00:00:00Z'
      }

      const wrapper = mount(CampLocationForm, {
        props: { mode: 'edit', camp }
      })

      expect(wrapper.find('#description').element.value).toBe('Beautiful camp')
    })
  })

  describe('Auto-fill from Google Places', () => {
    it('should auto-fill fields when handlePlaceSelected is called', async () => {
      const mockDetails = {
        placeId: 'ChIJN1t_tDeuEmsRUsoyG83frY4',
        name: 'Camping El Pinar',
        formattedAddress: 'Calle Example, 123, Madrid, España',
        latitude: 40.416775,
        longitude: -3.703790,
        types: ['campground']
      }

      vi.mocked(useGooglePlaces).mockReturnValue({
        ...defaultMockGooglePlaces,
        getPlaceDetails: vi.fn().mockResolvedValue(mockDetails)
      } as any)

      const wrapper = mount(CampLocationForm, {
        props: { mode: 'create' }
      })

      // Call handlePlaceSelected
      await (wrapper.vm as any).handlePlaceSelected({
        value: {
          placeId: 'ChIJN1t_tDeuEmsRUsoyG83frY4',
          description: 'Camping El Pinar, Madrid',
          mainText: 'Camping El Pinar',
          secondaryText: 'Madrid, España'
        }
      })

      await flushPromises()

      // Check form data was updated
      expect((wrapper.vm as any).formData.name).toBe('Camping El Pinar')
      expect((wrapper.vm as any).formData.location).toBe('Calle Example, 123, Madrid, España')
      expect((wrapper.vm as any).formData.latitude).toBe(40.416775)
      expect((wrapper.vm as any).formData.longitude).toBe(-3.703790)
      expect((wrapper.vm as any).formData.googlePlaceId).toBe('ChIJN1t_tDeuEmsRUsoyG83frY4')
    })

    it('should set autoFilledFromPlaces to true after successful place selection', async () => {
      const mockDetails = {
        placeId: 'ChIJ1',
        name: 'Camp Name',
        formattedAddress: 'Address',
        latitude: 40.0,
        longitude: -3.0,
        types: []
      }

      vi.mocked(useGooglePlaces).mockReturnValue({
        ...defaultMockGooglePlaces,
        getPlaceDetails: vi.fn().mockResolvedValue(mockDetails)
      } as any)

      const wrapper = mount(CampLocationForm, {
        props: { mode: 'create' }
      })

      await (wrapper.vm as any).handlePlaceSelected({
        value: { placeId: 'ChIJ1', description: 'Camp', mainText: 'Camp', secondaryText: '' }
      })

      await flushPromises()

      expect((wrapper.vm as any).autoFilledFromPlaces).toBe(true)
    })

    it('should auto-generate description from types when description is empty', async () => {
      const mockDetails = {
        placeId: 'ChIJ1',
        name: 'Camp Park',
        formattedAddress: 'Park Address, Madrid',
        latitude: 40.0,
        longitude: -3.0,
        types: ['campground']
      }

      vi.mocked(useGooglePlaces).mockReturnValue({
        ...defaultMockGooglePlaces,
        getPlaceDetails: vi.fn().mockResolvedValue(mockDetails)
      } as any)

      const wrapper = mount(CampLocationForm, {
        props: { mode: 'create' }
      })

      await (wrapper.vm as any).handlePlaceSelected({
        value: { placeId: 'ChIJ1', description: 'Camp', mainText: 'Camp', secondaryText: '' }
      })

      await flushPromises()

      expect((wrapper.vm as any).formData.description).toContain('Zona de camping')
    })
  })

  describe('Clear Autocomplete', () => {
    it('should clear autocomplete state when clearAutocomplete is called', async () => {
      const mockDetails = {
        placeId: 'ChIJ1',
        name: 'Camp',
        formattedAddress: 'Address',
        latitude: 40.0,
        longitude: -3.0,
        types: []
      }

      vi.mocked(useGooglePlaces).mockReturnValue({
        ...defaultMockGooglePlaces,
        getPlaceDetails: vi.fn().mockResolvedValue(mockDetails)
      } as any)

      const wrapper = mount(CampLocationForm, {
        props: { mode: 'create' }
      })

      // First auto-fill
      await (wrapper.vm as any).handlePlaceSelected({
        value: { placeId: 'ChIJ1', description: 'Camp', mainText: 'Camp', secondaryText: '' }
      })

      await flushPromises()

      expect((wrapper.vm as any).autoFilledFromPlaces).toBe(true)

      // Then clear
      ;(wrapper.vm as any).clearAutocomplete()

      expect((wrapper.vm as any).autoFilledFromPlaces).toBe(false)
      expect((wrapper.vm as any).formData.googlePlaceId).toBeNull()
    })
  })

  describe('Form Validation', () => {
    it('should show validation error when submitting empty form', async () => {
      const wrapper = mount(CampLocationForm, {
        props: { mode: 'create' }
      })

      await wrapper.find('form').trigger('submit')

      expect((wrapper.vm as any).errors.name).toBe('El nombre del campamento es obligatorio')
    })

    it('should emit submit event with form data when form is valid', async () => {
      const wrapper = mount(CampLocationForm, {
        props: { mode: 'create' }
      })

      // Fill required fields
      ;(wrapper.vm as any).formData.name = 'Test Camp'
      ;(wrapper.vm as any).formData.pricePerAdult = 100
      ;(wrapper.vm as any).formData.pricePerChild = 50
      ;(wrapper.vm as any).formData.pricePerBaby = 0

      await wrapper.find('form').trigger('submit')

      expect(wrapper.emitted('submit')).toBeTruthy()
    })
  })
})
