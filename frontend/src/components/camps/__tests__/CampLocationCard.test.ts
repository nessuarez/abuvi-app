import { describe, it, expect, vi } from 'vitest'
import { mount } from '@vue/test-utils'
import CampLocationCard from '@/components/camps/CampLocationCard.vue'
import type { Camp } from '@/types/camp'
import type { CampPhoto } from '@/types/camp-photo'

vi.mock('primevue/button', () => ({
  default: {
    name: 'Button',
    props: ['label', 'icon', 'size', 'text', 'outlined', 'severity'],
    emits: ['click'],
    template: '<button @click="$emit(\'click\')">{{ label }}</button>',
  },
}))

vi.mock('primevue/galleria', () => ({
  default: {
    name: 'Galleria',
    props: ['value', 'showThumbnails', 'showItemNavigators', 'showIndicators', 'circular', 'autoPlay'],
    template: '<div data-testid="galleria"><slot name="item" v-for="item in value" :item="item" /></div>',
  },
}))

const baseCamp: Camp = {
  id: 'camp-1',
  name: 'Test Camp',
  description: 'A test camp',
  location: 'Madrid',
  latitude: 40.0,
  longitude: -3.0,
  googlePlaceId: null,
  formattedAddress: null,
  phoneNumber: null,
  websiteUrl: null,
  googleMapsUrl: null,
  googleRating: 4.5,
  googleRatingCount: 100,
  businessStatus: null,
  pricePerAdult: 100,
  pricePerChild: 50,
  pricePerBaby: 0,
  isActive: true,
  createdAt: '2026-01-01',
  updatedAt: '2026-01-01',
}

const makePhoto = (overrides: Partial<CampPhoto> = {}): CampPhoto => ({
  id: 'photo-1',
  campId: 'camp-1',
  url: 'https://example.com/photo.jpg',
  description: 'A photo',
  displayOrder: 1,
  isPrimary: true,
  isOriginal: true,
  createdAt: '2026-01-01',
  updatedAt: '2026-01-01',
  ...overrides,
})

describe('CampLocationCard — photo carousel', () => {
  it('renders Galleria when photos are provided', () => {
    const photos = [makePhoto(), makePhoto({ id: 'photo-2', displayOrder: 2 })]
    const wrapper = mount(CampLocationCard, {
      props: { camp: baseCamp, photos },
    })
    expect(wrapper.find('[data-testid="galleria"]').exists()).toBe(true)
  })

  it('renders placeholder with camp initial when no photos', () => {
    const wrapper = mount(CampLocationCard, {
      props: { camp: baseCamp },
    })
    expect(wrapper.find('[data-testid="galleria"]').exists()).toBe(false)
    // Placeholder should show first letter of camp name
    const placeholder = wrapper.find('.bg-gray-100')
    expect(placeholder.exists()).toBe(true)
    expect(placeholder.text()).toBe('T') // First char of "Test Camp"
  })

  it('sorts photos by displayOrder', () => {
    const photos = [
      makePhoto({ id: 'photo-3', displayOrder: 3, url: 'https://example.com/3.jpg' }),
      makePhoto({ id: 'photo-1', displayOrder: 1, url: 'https://example.com/1.jpg' }),
      makePhoto({ id: 'photo-2', displayOrder: 2, url: 'https://example.com/2.jpg' }),
    ]
    const wrapper = mount(CampLocationCard, {
      props: { camp: baseCamp, photos },
    })
    const images = wrapper.findAll('img')
    expect(images[0].attributes('src')).toBe('https://example.com/1.jpg')
    expect(images[1].attributes('src')).toBe('https://example.com/2.jpg')
    expect(images[2].attributes('src')).toBe('https://example.com/3.jpg')
  })
})
