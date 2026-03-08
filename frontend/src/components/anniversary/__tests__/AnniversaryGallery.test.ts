import { describe, it, expect, vi, beforeEach } from 'vitest'
import { ref } from 'vue'
import { mount } from '@vue/test-utils'
import PrimeVue from 'primevue/config'
import AnniversaryGallery from '../AnniversaryGallery.vue'
import type { MediaItem } from '@/types/media-item'

const mockFetchMediaItems = vi.fn()
const mockMediaItems = ref<MediaItem[]>([])
const mockLoading = ref(false)
const mockError = ref<string | null>(null)

vi.mock('@/composables/useMediaItems', () => ({
  useMediaItems: () => ({
    mediaItems: mockMediaItems,
    loading: mockLoading,
    error: mockError,
    fetchMediaItems: mockFetchMediaItems,
  }),
}))

const makeMediaItem = (overrides: Partial<MediaItem> = {}): MediaItem => ({
  id: 'item-1',
  uploadedByUserId: 'user-1',
  uploadedByName: 'Test User',
  fileUrl: 'https://example.com/photo.jpg',
  thumbnailUrl: 'https://example.com/thumb.webp',
  type: 'Photo',
  title: 'Beach Day',
  description: null,
  year: 2023,
  decade: '20s',
  memoryId: null,
  context: 'anniversary-50',
  isPublished: true,
  isApproved: true,
  createdAt: '2025-01-01T00:00:00Z',
  ...overrides,
})

const stubs = {
  Image: {
    props: ['src', 'alt', 'preview', 'imageClass'],
    template: '<img :src="src" :alt="alt" />',
  },
  Skeleton: true,
}

function mountGallery() {
  return mount(AnniversaryGallery, {
    global: {
      plugins: [PrimeVue],
      stubs,
    },
  })
}

describe('AnniversaryGallery', () => {
  beforeEach(() => {
    mockFetchMediaItems.mockClear()
    mockMediaItems.value = []
    mockLoading.value = false
    mockError.value = null
  })

  it('should call fetchMediaItems with approved=true and context on mount', () => {
    mountGallery()
    expect(mockFetchMediaItems).toHaveBeenCalledWith({ approved: true, context: 'anniversary-50' })
  })

  it('should show loading skeletons while fetching', () => {
    mockLoading.value = true
    const wrapper = mountGallery()
    const skeletons = wrapper.findAllComponents({ name: 'Skeleton' })
    expect(skeletons.length).toBeGreaterThan(0)
  })

  it('should show empty state when no items returned', () => {
    mockMediaItems.value = []
    const wrapper = mountGallery()
    expect(wrapper.text()).toContain('Aún no hay recuerdos con aprobación')
  })

  it('should render photo items with Image component', () => {
    mockMediaItems.value = [makeMediaItem({ type: 'Photo' })]
    const wrapper = mountGallery()
    const img = wrapper.find('img')
    expect(img.exists()).toBe(true)
    expect(img.attributes('src')).toBe('https://example.com/photo.jpg')
  })

  it('should render audio items with audio element', () => {
    mockMediaItems.value = [makeMediaItem({ id: 'audio-1', type: 'Audio', fileUrl: 'https://example.com/audio.mp3' })]
    const wrapper = mountGallery()
    const audio = wrapper.find('audio')
    expect(audio.exists()).toBe(true)
    expect(audio.attributes('src')).toBe('https://example.com/audio.mp3')
  })

  it('should render video items with video element', () => {
    mockMediaItems.value = [makeMediaItem({ id: 'video-1', type: 'Video', fileUrl: 'https://example.com/video.mp4' })]
    const wrapper = mountGallery()
    const video = wrapper.find('video')
    expect(video.exists()).toBe(true)
    expect(video.attributes('src')).toBe('https://example.com/video.mp4')
  })

  it('should show error state when error occurs', () => {
    mockError.value = 'Something went wrong'
    const wrapper = mountGallery()
    expect(wrapper.text()).toContain('No se pudo cargar la galería')
    expect(wrapper.text()).toContain('Something went wrong')
  })
})
