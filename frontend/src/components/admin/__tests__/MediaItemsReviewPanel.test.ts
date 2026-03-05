import { describe, it, expect, vi, beforeEach } from 'vitest'
import { ref } from 'vue'
import { shallowMount } from '@vue/test-utils'
import PrimeVue from 'primevue/config'
import MediaItemsReviewPanel from '../MediaItemsReviewPanel.vue'
import type { MediaItem } from '@/types/media-item'
import type { Memory } from '@/types/memory'

const mockToastAdd = vi.fn()
const mockFetchMediaItems = vi.fn()
const mockFetchMemories = vi.fn()
const mockApproveMediaItem = vi.fn()
const mockRejectMediaItem = vi.fn()
const mockApproveMemory = vi.fn()
const mockRejectMemory = vi.fn()

const mockMediaItems = ref<MediaItem[]>([])
const mockMediaLoading = ref(false)
const mockMemories = ref<Memory[]>([])
const mockMemoriesLoading = ref(false)

vi.mock('primevue/usetoast', () => ({
  useToast: () => ({ add: mockToastAdd }),
}))

vi.mock('@/composables/useMediaItems', () => ({
  useMediaItems: () => ({
    mediaItems: mockMediaItems,
    loading: mockMediaLoading,
    fetchMediaItems: mockFetchMediaItems,
    approveMediaItem: mockApproveMediaItem,
    rejectMediaItem: mockRejectMediaItem,
  }),
}))

vi.mock('@/composables/useMemories', () => ({
  useMemories: () => ({
    memories: mockMemories,
    loading: mockMemoriesLoading,
    fetchMemories: mockFetchMemories,
    approveMemory: mockApproveMemory,
    rejectMemory: mockRejectMemory,
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
  isPublished: false,
  isApproved: false,
  createdAt: '2025-01-01T00:00:00Z',
  ...overrides,
})

function mountPanel() {
  return shallowMount(MediaItemsReviewPanel, {
    global: {
      plugins: [PrimeVue],
    },
  })
}

describe('MediaItemsReviewPanel', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    mockMediaItems.value = []
    mockMemories.value = []
    mockMediaLoading.value = false
    mockMemoriesLoading.value = false
  })

  it('should fetch unapproved items on mount', () => {
    mountPanel()
    expect(mockFetchMediaItems).toHaveBeenCalledWith({ approved: false })
  })

  it('should fetch unapproved memories on mount', () => {
    mountPanel()
    expect(mockFetchMemories).toHaveBeenCalledWith({ approved: false })
  })

  it('should show empty state when no pending items', () => {
    const wrapper = mountPanel()
    expect(wrapper.text()).toContain('No hay elementos pendientes de revisión')
  })

  it('should show media items section when items exist', () => {
    mockMediaItems.value = [makeMediaItem()]
    const wrapper = mountPanel()
    expect(wrapper.text()).toContain('Elementos multimedia pendientes')
  })

  it('should call approveMediaItem on approve handler', async () => {
    mockApproveMediaItem.mockResolvedValue(true)
    mockMediaItems.value = [makeMediaItem({ id: 'item-1' })]

    const panel = mountPanel()
    const vm = panel.vm as any
    await vm.handleApproveMedia('item-1')

    expect(mockApproveMediaItem).toHaveBeenCalledWith('item-1')
  })

  it('should show toast on successful approve', async () => {
    mockApproveMediaItem.mockResolvedValue(true)
    mockMediaItems.value = [makeMediaItem({ id: 'item-1' })]

    const panel = mountPanel()
    const vm = panel.vm as any
    await vm.handleApproveMedia('item-1')

    expect(mockToastAdd).toHaveBeenCalledWith(
      expect.objectContaining({ severity: 'success', summary: 'Aprobado' })
    )
  })

  it('should remove item from list after approve', async () => {
    mockApproveMediaItem.mockResolvedValue(true)
    mockMediaItems.value = [makeMediaItem({ id: 'item-1' }), makeMediaItem({ id: 'item-2' })]

    const panel = mountPanel()
    const vm = panel.vm as any
    await vm.handleApproveMedia('item-1')

    expect(mockMediaItems.value).toHaveLength(1)
    expect(mockMediaItems.value[0].id).toBe('item-2')
  })

  it('should call rejectMediaItem on reject handler', async () => {
    mockRejectMediaItem.mockResolvedValue(true)
    mockMediaItems.value = [makeMediaItem({ id: 'item-1' })]

    const panel = mountPanel()
    const vm = panel.vm as any
    await vm.handleRejectMedia('item-1')

    expect(mockRejectMediaItem).toHaveBeenCalledWith('item-1')
  })

  it('should call approveMemory for memory approval', async () => {
    mockApproveMemory.mockResolvedValue(true)
    mockMemories.value = [{
      id: 'mem-1',
      authorUserId: 'user-1',
      authorName: 'Author',
      title: 'Test',
      content: 'Content',
      year: 2023,
      campLocationId: null,
      isPublished: false,
      isApproved: false,
      createdAt: '2025-01-01T00:00:00Z',
      updatedAt: '2025-01-01T00:00:00Z',
      mediaItems: [],
    }]

    const panel = mountPanel()
    const vm = panel.vm as any
    await vm.handleApproveMemory('mem-1')

    expect(mockApproveMemory).toHaveBeenCalledWith('mem-1')
    expect(mockToastAdd).toHaveBeenCalledWith(
      expect.objectContaining({ severity: 'success' })
    )
  })
})
