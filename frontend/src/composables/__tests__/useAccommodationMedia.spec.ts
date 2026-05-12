import { describe, it, expect, vi, beforeEach } from 'vitest'
import { useAccommodationMedia } from '@/composables/useAccommodationMedia'
import { api } from '@/utils/api'
import type { AccommodationMediaItem, AccommodationTypeMediaItem } from '@/types/accommodation-media'

vi.mock('@/utils/api', () => ({
  api: {
    get: vi.fn(),
    post: vi.fn(),
    delete: vi.fn(),
    patch: vi.fn(),
  },
}))

const makeMediaItem = (overrides: Partial<AccommodationMediaItem> = {}): AccommodationMediaItem => ({
  id: 'item-1',
  fileUrl: 'https://cdn.example.com/accommodation-media/photo.jpg',
  thumbnailUrl: 'https://cdn.example.com/accommodation-media/thumb.webp',
  description: null,
  displayOrder: 0,
  isPrimary: false,
  type: 'Photo',
  createdAt: '2026-01-01T00:00:00Z',
  ...overrides,
})

const makeTypeMediaItem = (overrides: Partial<AccommodationTypeMediaItem> = {}): AccommodationTypeMediaItem => ({
  id: 'type-item-1',
  accommodationType: 'Lodge',
  fileUrl: 'https://cdn.example.com/accommodation-media/lodge.jpg',
  thumbnailUrl: null,
  description: null,
  displayOrder: 0,
  isPrimary: false,
  createdAt: '2026-01-01T00:00:00Z',
  ...overrides,
})

const successResponse = <T>(data: T) => ({
  data: { success: true, data },
})

describe('useAccommodationMedia', () => {
  beforeEach(() => {
    vi.clearAllMocks()
  })

  // ── Zone media ────────────────────────────────────────────────────────────

  describe('fetchZoneMedia', () => {
    it('fetchZoneMedia_onSuccess_populatesItems', async () => {
      const items = [makeMediaItem({ id: 'z1' }), makeMediaItem({ id: 'z2' })]
      vi.mocked(api.get).mockResolvedValue(successResponse(items))

      const { fetchZoneMedia, items: result } = useAccommodationMedia()
      await fetchZoneMedia('edition-1', 'zone-1')

      expect(result.value).toEqual(items)
      expect(api.get).toHaveBeenCalledWith(
        '/camps/editions/edition-1/accommodation-zones/zone-1/media'
      )
    })

    it('fetchZoneMedia_onFailure_setsSpanishError', async () => {
      vi.mocked(api.get).mockRejectedValue(new Error('Network error'))

      const { fetchZoneMedia, error } = useAccommodationMedia()
      await fetchZoneMedia('edition-1', 'zone-1')

      expect(error.value).toBe('Error al cargar los archivos')
    })
  })

  describe('addZoneMedia', () => {
    it('addZoneMedia_onSuccess_pushesToItems', async () => {
      const newItem = makeMediaItem({ id: 'new-1' })
      vi.mocked(api.post).mockResolvedValue(successResponse(newItem))

      const { addZoneMedia, items } = useAccommodationMedia()
      const result = await addZoneMedia('edition-1', 'zone-1', {
        fileUrl: newItem.fileUrl,
        thumbnailUrl: null,
        description: null,
      })

      expect(result).toEqual(newItem)
      expect(items.value).toContainEqual(newItem)
    })

    it('addZoneMedia_onFailure_setsSpanishError', async () => {
      vi.mocked(api.post).mockRejectedValue(new Error('Server error'))

      const { addZoneMedia, error } = useAccommodationMedia()
      const result = await addZoneMedia('edition-1', 'zone-1', {
        fileUrl: 'https://cdn.example.com/file.jpg',
        thumbnailUrl: null,
        description: null,
      })

      expect(result).toBeNull()
      expect(error.value).toBe('Error al añadir el archivo')
    })
  })

  describe('deleteZoneMedia', () => {
    it('deleteZoneMedia_onSuccess_removesFromItems', async () => {
      vi.mocked(api.delete).mockResolvedValue({})

      const { deleteZoneMedia, items } = useAccommodationMedia()
      items.value = [makeMediaItem({ id: 'keep' }), makeMediaItem({ id: 'remove' })]

      await deleteZoneMedia('edition-1', 'zone-1', 'remove')

      expect(items.value).toHaveLength(1)
      expect(items.value[0].id).toBe('keep')
    })
  })

  describe('setZonePrimary', () => {
    it('setZonePrimary_onSuccess_updatesIsPrimaryOptimistically', async () => {
      vi.mocked(api.patch).mockResolvedValue({})

      const { setZonePrimary, items } = useAccommodationMedia()
      items.value = [
        makeMediaItem({ id: 'a', isPrimary: true }),
        makeMediaItem({ id: 'b', isPrimary: false }),
      ]

      await setZonePrimary('edition-1', 'zone-1', 'b')

      expect(items.value.find((i) => i.id === 'a')?.isPrimary).toBe(false)
      expect(items.value.find((i) => i.id === 'b')?.isPrimary).toBe(true)
    })
  })

  // ── Accommodation media ───────────────────────────────────────────────────

  describe('fetchAccommodationMedia', () => {
    it('fetchAccommodationMedia_onSuccess_populatesItems', async () => {
      const items = [makeMediaItem({ id: 'acc-1' })]
      vi.mocked(api.get).mockResolvedValue(successResponse(items))

      const { fetchAccommodationMedia, items: result } = useAccommodationMedia()
      await fetchAccommodationMedia('edition-1', 'accommodation-1')

      expect(result.value).toEqual(items)
    })
  })

  describe('addAccommodationMedia', () => {
    it('addAccommodationMedia_onSuccess_pushesToItems', async () => {
      const newItem = makeMediaItem({ id: 'acc-new' })
      vi.mocked(api.post).mockResolvedValue(successResponse(newItem))

      const { addAccommodationMedia, items } = useAccommodationMedia()
      const result = await addAccommodationMedia('edition-1', 'accommodation-1', {
        fileUrl: newItem.fileUrl,
        thumbnailUrl: null,
        description: null,
      })

      expect(result).toEqual(newItem)
      expect(items.value).toContainEqual(newItem)
    })
  })

  describe('deleteAccommodationMedia', () => {
    it('deleteAccommodationMedia_onSuccess_removesFromItems', async () => {
      vi.mocked(api.delete).mockResolvedValue({})

      const { deleteAccommodationMedia, items } = useAccommodationMedia()
      items.value = [makeMediaItem({ id: 'stay' }), makeMediaItem({ id: 'gone' })]

      await deleteAccommodationMedia('edition-1', 'accommodation-1', 'gone')

      expect(items.value.map((i) => i.id)).toEqual(['stay'])
    })
  })

  describe('setAccommodationPrimary', () => {
    it('setAccommodationPrimary_onSuccess_updatesIsPrimaryOptimistically', async () => {
      vi.mocked(api.patch).mockResolvedValue({})

      const { setAccommodationPrimary, items } = useAccommodationMedia()
      items.value = [
        makeMediaItem({ id: 'x', isPrimary: true }),
        makeMediaItem({ id: 'y', isPrimary: false }),
      ]

      await setAccommodationPrimary('edition-1', 'accommodation-1', 'y')

      expect(items.value.find((i) => i.id === 'x')?.isPrimary).toBe(false)
      expect(items.value.find((i) => i.id === 'y')?.isPrimary).toBe(true)
    })
  })

  // ── Type media ────────────────────────────────────────────────────────────

  describe('fetchTypeMedia', () => {
    it('fetchTypeMedia_withoutType_fetchesAllMedia', async () => {
      const data = [makeTypeMediaItem()]
      vi.mocked(api.get).mockResolvedValue(successResponse(data))

      const { fetchTypeMedia, typeItems } = useAccommodationMedia()
      await fetchTypeMedia()

      expect(api.get).toHaveBeenCalledWith('/accommodation-types/media')
      expect(typeItems.value).toEqual(data)
    })

    it('fetchTypeMedia_withType_fetchesTypeSpecificMedia', async () => {
      vi.mocked(api.get).mockResolvedValue(successResponse([]))

      const { fetchTypeMedia } = useAccommodationMedia()
      await fetchTypeMedia('Lodge')

      expect(api.get).toHaveBeenCalledWith('/accommodation-types/Lodge/media')
    })
  })

  describe('addTypeMedia', () => {
    it('addTypeMedia_onSuccess_pushesToTypeItems', async () => {
      const newItem = makeTypeMediaItem({ id: 'type-new' })
      vi.mocked(api.post).mockResolvedValue(successResponse(newItem))

      const { addTypeMedia, typeItems } = useAccommodationMedia()
      const result = await addTypeMedia('Lodge', {
        fileUrl: newItem.fileUrl,
        thumbnailUrl: null,
        description: null,
      })

      expect(result).toEqual(newItem)
      expect(typeItems.value).toContainEqual(newItem)
    })
  })

  describe('deleteTypeMedia', () => {
    it('deleteTypeMedia_onSuccess_removesFromTypeItems', async () => {
      vi.mocked(api.delete).mockResolvedValue({})

      const { deleteTypeMedia, typeItems } = useAccommodationMedia()
      typeItems.value = [makeTypeMediaItem({ id: 'keep' }), makeTypeMediaItem({ id: 'drop' })]

      await deleteTypeMedia('drop')

      expect(typeItems.value.map((i) => i.id)).toEqual(['keep'])
    })
  })

  describe('setTypePrimary', () => {
    it('setTypePrimary_onSuccess_updatesIsPrimaryOptimistically', async () => {
      vi.mocked(api.patch).mockResolvedValue({})

      const { setTypePrimary, typeItems } = useAccommodationMedia()
      typeItems.value = [
        makeTypeMediaItem({ id: 'p', isPrimary: true }),
        makeTypeMediaItem({ id: 'q', isPrimary: false }),
      ]

      await setTypePrimary('q')

      expect(typeItems.value.find((i) => i.id === 'p')?.isPrimary).toBe(false)
      expect(typeItems.value.find((i) => i.id === 'q')?.isPrimary).toBe(true)
    })
  })
})
