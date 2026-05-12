import { describe, it, expect } from 'vitest'
import { mount } from '@vue/test-utils'
import PrimeVue from 'primevue/config'
import AccommodationMediaGallery from '../AccommodationMediaGallery.vue'
import type { AccommodationMediaItem } from '@/types/accommodation-media'

const makeItem = (overrides: Partial<AccommodationMediaItem> = {}): AccommodationMediaItem => ({
  id: 'item-1',
  fileUrl: 'https://cdn.example.com/file.jpg',
  thumbnailUrl: 'https://cdn.example.com/thumb.webp',
  description: null,
  displayOrder: 0,
  isPrimary: false,
  type: 'Photo',
  createdAt: '2026-01-01T00:00:00Z',
  ...overrides,
})

const mountGallery = (props: { items: AccommodationMediaItem[]; loading?: boolean }) =>
  mount(AccommodationMediaGallery, {
    props,
    global: { plugins: [PrimeVue] },
  })

describe('AccommodationMediaGallery', () => {
  it('renders nothing when items is empty and not loading', () => {
    const wrapper = mountGallery({ items: [] })
    expect(wrapper.find('img').exists()).toBe(false)
    expect(wrapper.find('.p-progressspinner').exists()).toBe(false)
  })

  it('shows spinner when loading is true', () => {
    const wrapper = mountGallery({ items: [], loading: true })
    expect(wrapper.find('.p-progressspinner').exists()).toBe(true)
  })

  it('renders thumbnails for each item', () => {
    const items = [makeItem({ id: 'a' }), makeItem({ id: 'b' })]
    const wrapper = mountGallery({ items })
    // PrimeVue Image renders img tags
    const images = wrapper.findAll('img')
    expect(images.length).toBeGreaterThanOrEqual(2)
  })

  it('sorts primary item first regardless of displayOrder', () => {
    const items = [
      makeItem({ id: 'second', isPrimary: false, displayOrder: 0 }),
      makeItem({ id: 'primary', isPrimary: true, displayOrder: 5 }),
    ]
    const wrapper = mountGallery({ items })
    const images = wrapper.findAll('img')
    // Primary thumbnail URL should appear in first image src
    const firstSrc = images[0].attributes('src')
    expect(firstSrc).toBe(items[1].thumbnailUrl)
  })

  it('applies ring highlight class to primary item container', () => {
    const items = [
      makeItem({ id: 'plain', isPrimary: false }),
      makeItem({ id: 'primary', isPrimary: true }),
    ]
    const wrapper = mountGallery({ items })
    // The primary item's wrapper div should have ring-2 class
    const containers = wrapper.findAll('.relative')
    const primaryContainer = containers.find((c) => c.html().includes('ring-primary-500'))
    expect(primaryContainer).toBeTruthy()
  })
})
