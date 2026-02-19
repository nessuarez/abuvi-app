import { describe, it, expect, vi } from 'vitest'
import { mount } from '@vue/test-utils'
import CampPlacesGallery from '../CampPlacesGallery.vue'
import type { CampPlacesPhoto } from '@/types/camp'

vi.stubEnv('VITE_API_URL', 'http://localhost:5000')

vi.mock('primevue/image', () => ({
  default: {
    name: 'Image',
    props: ['src', 'alt', 'preview', 'imageClass'],
    template: '<img :src="src" :alt="alt" />'
  }
}))

const makePhoto = (overrides: Partial<CampPlacesPhoto> = {}): CampPlacesPhoto => ({
  id: '1',
  photoReference: 'ref_abc123',
  photoUrl: null,
  width: 1200,
  height: 900,
  attributionName: 'John Doe',
  attributionUrl: 'https://maps.google.com/maps/contrib/123',
  isPrimary: true,
  displayOrder: 1,
  ...overrides
})

describe('CampPlacesGallery', () => {
  it('renders nothing when photos array is empty', () => {
    const wrapper = mount(CampPlacesGallery, { props: { photos: [] } })
    expect(wrapper.find('div').exists()).toBe(false)
  })

  it('renders primary photo with correct proxy URL', () => {
    const wrapper = mount(CampPlacesGallery, { props: { photos: [makePhoto()] } })
    expect(wrapper.html()).toContain('reference=ref_abc123')
    expect(wrapper.html()).toContain('maxwidth=800')
  })

  it('renders thumbnail with smaller maxwidth', () => {
    const photos = [
      makePhoto({ isPrimary: true, id: '1' }),
      makePhoto({ isPrimary: false, id: '2', photoReference: 'ref_thumb', displayOrder: 2 })
    ]
    const wrapper = mount(CampPlacesGallery, { props: { photos } })
    expect(wrapper.html()).toContain('maxwidth=200')
  })

  it('uses photoUrl directly when available', () => {
    const photo = makePhoto({ photoUrl: 'https://cdn.example.com/photo.jpg', photoReference: null })
    const wrapper = mount(CampPlacesGallery, { props: { photos: [photo] } })
    expect(wrapper.html()).toContain('https://cdn.example.com/photo.jpg')
  })

  it('renders attribution name with link', () => {
    const wrapper = mount(CampPlacesGallery, { props: { photos: [makePhoto()] } })
    expect(wrapper.text()).toContain('John Doe')
    expect(wrapper.find('a[href="https://maps.google.com/maps/contrib/123"]').exists()).toBe(true)
  })

  it('renders attribution name as plain text when no URL', () => {
    const photo = makePhoto({ attributionUrl: null })
    const wrapper = mount(CampPlacesGallery, { props: { photos: [photo] } })
    expect(wrapper.text()).toContain('John Doe')
  })

  it('limits thumbnails to 8 even with more photos', () => {
    const photos = Array.from({ length: 12 }, (_, i) =>
      makePhoto({ id: String(i), isPrimary: i === 0, displayOrder: i + 1, photoReference: `ref_${i}` })
    )
    const wrapper = mount(CampPlacesGallery, { props: { photos } })
    const thumbnailButtons = wrapper.findAll('button[type="button"]')
    expect(thumbnailButtons.length).toBeLessThanOrEqual(8)
  })

  it('shows photo count footer when multiple photos', () => {
    const photos = [makePhoto({ id: '1' }), makePhoto({ id: '2', isPrimary: false, displayOrder: 2 })]
    const wrapper = mount(CampPlacesGallery, { props: { photos } })
    expect(wrapper.text()).toContain('2 fotos')
  })

  it('renders "Google Maps" attribution footer', () => {
    const wrapper = mount(CampPlacesGallery, { props: { photos: [makePhoto()] } })
    expect(wrapper.text()).toContain('Google Maps')
  })
})
