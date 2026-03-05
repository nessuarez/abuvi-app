import { describe, it, expect, vi, beforeEach } from 'vitest'
import { useMediaItems } from '@/composables/useMediaItems'
import { api } from '@/utils/api'
import type { MediaItem } from '@/types/media-item'

vi.mock('@/utils/api', () => ({
  api: {
    get: vi.fn(),
    post: vi.fn(),
    patch: vi.fn(),
    delete: vi.fn()
  }
}))

const makeMediaItem = (overrides: Partial<MediaItem> = {}): MediaItem => ({
  id: 'item-1',
  uploadedByUserId: 'user-1',
  uploadedByName: 'Test User',
  fileUrl: 'https://example.com/photo.jpg',
  thumbnailUrl: 'https://example.com/thumb.webp',
  type: 'Photo',
  title: 'Beach Day',
  description: 'A fun day at the beach',
  year: 2023,
  decade: '20s',
  memoryId: null,
  context: 'anniversary-50',
  isPublished: false,
  isApproved: false,
  createdAt: '2025-01-01T00:00:00Z',
  ...overrides
})

describe('useMediaItems', () => {
  beforeEach(() => {
    vi.clearAllMocks()
  })

  describe('fetchMediaItems', () => {
    it('should call GET /media-items with query params', async () => {
      vi.mocked(api.get).mockResolvedValue({
        data: { success: true, data: [], error: null }
      })

      const { fetchMediaItems } = useMediaItems()
      await fetchMediaItems({ year: 2023, approved: true, context: 'anniversary-50', type: 'Photo' })

      expect(api.get).toHaveBeenCalledWith(
        '/media-items?year=2023&approved=true&context=anniversary-50&type=Photo'
      )
    })

    it('should call GET /media-items without query params when none provided', async () => {
      vi.mocked(api.get).mockResolvedValue({
        data: { success: true, data: [], error: null }
      })

      const { fetchMediaItems } = useMediaItems()
      await fetchMediaItems()

      expect(api.get).toHaveBeenCalledWith('/media-items')
    })

    it('should update mediaItems ref on success', async () => {
      const items = [makeMediaItem(), makeMediaItem({ id: 'item-2' })]
      vi.mocked(api.get).mockResolvedValue({
        data: { success: true, data: items, error: null }
      })

      const { fetchMediaItems, mediaItems } = useMediaItems()
      await fetchMediaItems()

      expect(mediaItems.value).toEqual(items)
    })

    it('should set error on API failure', async () => {
      vi.mocked(api.get).mockRejectedValue({
        response: { data: { error: { message: 'No autorizado' } } }
      })

      const { fetchMediaItems, error } = useMediaItems()
      await fetchMediaItems()

      expect(error.value).toBe('No autorizado')
    })

    it('should use fallback error message when no API message', async () => {
      vi.mocked(api.get).mockRejectedValue(new Error('Network error'))

      const { fetchMediaItems, error } = useMediaItems()
      await fetchMediaItems()

      expect(error.value).toBe('Error al obtener los elementos multimedia')
    })

    it('should reset loading to false after completion', async () => {
      vi.mocked(api.get).mockResolvedValue({
        data: { success: true, data: [], error: null }
      })

      const { fetchMediaItems, loading } = useMediaItems()
      await fetchMediaItems()

      expect(loading.value).toBe(false)
    })
  })

  describe('createMediaItem', () => {
    it('should call POST /media-items', async () => {
      const newItem = makeMediaItem()
      vi.mocked(api.post).mockResolvedValue({
        data: { success: true, data: newItem, error: null }
      })

      const { createMediaItem } = useMediaItems()
      await createMediaItem({
        fileUrl: 'https://example.com/photo.jpg',
        thumbnailUrl: 'https://example.com/thumb.webp',
        type: 'Photo',
        title: 'Beach Day'
      })

      expect(api.post).toHaveBeenCalledWith('/media-items', expect.any(Object))
    })

    it('should return created item on success', async () => {
      const newItem = makeMediaItem()
      vi.mocked(api.post).mockResolvedValue({
        data: { success: true, data: newItem, error: null }
      })

      const { createMediaItem } = useMediaItems()
      const result = await createMediaItem({
        fileUrl: 'https://example.com/photo.jpg',
        type: 'Photo',
        title: 'Beach Day'
      })

      expect(result).toEqual(newItem)
    })

    it('should add created item to mediaItems array', async () => {
      const newItem = makeMediaItem()
      vi.mocked(api.post).mockResolvedValue({
        data: { success: true, data: newItem, error: null }
      })

      const { createMediaItem, mediaItems } = useMediaItems()
      await createMediaItem({
        fileUrl: 'https://example.com/photo.jpg',
        type: 'Photo',
        title: 'Beach Day'
      })

      expect(mediaItems.value).toContainEqual(newItem)
    })

    it('should return null and set error on failure', async () => {
      vi.mocked(api.post).mockRejectedValue({
        response: { data: { error: { message: 'Validación fallida' } } }
      })

      const { createMediaItem, createError } = useMediaItems()
      const result = await createMediaItem({
        fileUrl: '',
        type: 'Photo',
        title: ''
      })

      expect(result).toBeNull()
      expect(createError.value).toBe('Validación fallida')
    })
  })

  describe('approveMediaItem', () => {
    it('should call PATCH /media-items/{id}/approve', async () => {
      vi.mocked(api.patch).mockResolvedValue({
        data: { success: true, data: makeMediaItem({ isApproved: true }), error: null }
      })

      const { approveMediaItem } = useMediaItems()
      const result = await approveMediaItem('item-1')

      expect(result).toBe(true)
      expect(api.patch).toHaveBeenCalledWith('/media-items/item-1/approve')
    })

    it('should return false and set error on failure', async () => {
      vi.mocked(api.patch).mockRejectedValue(new Error('Forbidden'))

      const { approveMediaItem, error } = useMediaItems()
      const result = await approveMediaItem('item-1')

      expect(result).toBe(false)
      expect(error.value).toBe('Error al aprobar el elemento multimedia')
    })
  })

  describe('rejectMediaItem', () => {
    it('should call PATCH /media-items/{id}/reject', async () => {
      vi.mocked(api.patch).mockResolvedValue({
        data: { success: true, data: makeMediaItem(), error: null }
      })

      const { rejectMediaItem } = useMediaItems()
      const result = await rejectMediaItem('item-1')

      expect(result).toBe(true)
      expect(api.patch).toHaveBeenCalledWith('/media-items/item-1/reject')
    })

    it('should return false on failure', async () => {
      vi.mocked(api.patch).mockRejectedValue(new Error('Forbidden'))

      const { rejectMediaItem } = useMediaItems()
      const result = await rejectMediaItem('item-1')

      expect(result).toBe(false)
    })
  })

  describe('deleteMediaItem', () => {
    it('should call DELETE /media-items/{id}', async () => {
      vi.mocked(api.delete).mockResolvedValue({ data: null })

      const { deleteMediaItem } = useMediaItems()
      const result = await deleteMediaItem('item-1')

      expect(result).toBe(true)
      expect(api.delete).toHaveBeenCalledWith('/media-items/item-1')
    })

    it('should remove item from local array on success', async () => {
      vi.mocked(api.delete).mockResolvedValue({ data: null })

      const { deleteMediaItem, mediaItems } = useMediaItems()
      mediaItems.value = [makeMediaItem({ id: 'item-1' }), makeMediaItem({ id: 'item-2' })]
      await deleteMediaItem('item-1')

      expect(mediaItems.value).toHaveLength(1)
      expect(mediaItems.value[0].id).toBe('item-2')
    })

    it('should return false and set error on failure', async () => {
      vi.mocked(api.delete).mockRejectedValue(new Error('Not found'))

      const { deleteMediaItem, error } = useMediaItems()
      const result = await deleteMediaItem('item-1')

      expect(result).toBe(false)
      expect(error.value).toBe('Error al eliminar el elemento multimedia')
    })
  })
})
