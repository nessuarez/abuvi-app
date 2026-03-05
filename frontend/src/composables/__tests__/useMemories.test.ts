import { describe, it, expect, vi, beforeEach } from 'vitest'
import { useMemories } from '@/composables/useMemories'
import { api } from '@/utils/api'
import type { Memory } from '@/types/memory'

vi.mock('@/utils/api', () => ({
  api: {
    get: vi.fn(),
    post: vi.fn(),
    patch: vi.fn()
  }
}))

const makeMemory = (overrides: Partial<Memory> = {}): Memory => ({
  id: 'memory-1',
  authorUserId: 'user-1',
  authorName: 'Test Author',
  title: 'Camp Memory',
  content: 'A great time at camp',
  year: 2023,
  campLocationId: null,
  isPublished: false,
  isApproved: false,
  createdAt: '2025-01-01T00:00:00Z',
  updatedAt: '2025-01-01T00:00:00Z',
  mediaItems: [],
  ...overrides
})

describe('useMemories', () => {
  beforeEach(() => {
    vi.clearAllMocks()
  })

  describe('fetchMemories', () => {
    it('should call GET /memories with query params', async () => {
      vi.mocked(api.get).mockResolvedValue({
        data: { success: true, data: [], error: null }
      })

      const { fetchMemories } = useMemories()
      await fetchMemories({ year: 2023, approved: false })

      expect(api.get).toHaveBeenCalledWith('/memories?year=2023&approved=false')
    })

    it('should update memories ref on success', async () => {
      const items = [makeMemory(), makeMemory({ id: 'memory-2' })]
      vi.mocked(api.get).mockResolvedValue({
        data: { success: true, data: items, error: null }
      })

      const { fetchMemories, memories } = useMemories()
      await fetchMemories()

      expect(memories.value).toEqual(items)
    })

    it('should set error on API failure', async () => {
      vi.mocked(api.get).mockRejectedValue({
        response: { data: { error: { message: 'No autorizado' } } }
      })

      const { fetchMemories, error } = useMemories()
      await fetchMemories()

      expect(error.value).toBe('No autorizado')
    })

    it('should use fallback error message when no API message', async () => {
      vi.mocked(api.get).mockRejectedValue(new Error('Network error'))

      const { fetchMemories, error } = useMemories()
      await fetchMemories()

      expect(error.value).toBe('Error al obtener los recuerdos')
    })
  })

  describe('createMemory', () => {
    it('should call POST /memories', async () => {
      const newMemory = makeMemory()
      vi.mocked(api.post).mockResolvedValue({
        data: { success: true, data: newMemory, error: null }
      })

      const { createMemory } = useMemories()
      await createMemory({ title: 'Camp Memory', content: 'Content', year: 2023 })

      expect(api.post).toHaveBeenCalledWith('/memories', expect.any(Object))
    })

    it('should return created memory on success', async () => {
      const newMemory = makeMemory()
      vi.mocked(api.post).mockResolvedValue({
        data: { success: true, data: newMemory, error: null }
      })

      const { createMemory } = useMemories()
      const result = await createMemory({ title: 'Camp Memory', content: 'Content' })

      expect(result).toEqual(newMemory)
    })

    it('should return null and set error on failure', async () => {
      vi.mocked(api.post).mockRejectedValue({
        response: { data: { error: { message: 'Validación fallida' } } }
      })

      const { createMemory, createError } = useMemories()
      const result = await createMemory({ title: '', content: '' })

      expect(result).toBeNull()
      expect(createError.value).toBe('Validación fallida')
    })
  })

  describe('approveMemory', () => {
    it('should call PATCH /memories/{id}/approve', async () => {
      vi.mocked(api.patch).mockResolvedValue({
        data: { success: true, data: makeMemory({ isApproved: true }), error: null }
      })

      const { approveMemory } = useMemories()
      const result = await approveMemory('memory-1')

      expect(result).toBe(true)
      expect(api.patch).toHaveBeenCalledWith('/memories/memory-1/approve')
    })

    it('should return false on failure', async () => {
      vi.mocked(api.patch).mockRejectedValue(new Error('Forbidden'))

      const { approveMemory } = useMemories()
      const result = await approveMemory('memory-1')

      expect(result).toBe(false)
    })
  })

  describe('rejectMemory', () => {
    it('should call PATCH /memories/{id}/reject', async () => {
      vi.mocked(api.patch).mockResolvedValue({
        data: { success: true, data: makeMemory(), error: null }
      })

      const { rejectMemory } = useMemories()
      const result = await rejectMemory('memory-1')

      expect(result).toBe(true)
      expect(api.patch).toHaveBeenCalledWith('/memories/memory-1/reject')
    })

    it('should return false and set error on failure', async () => {
      vi.mocked(api.patch).mockRejectedValue(new Error('Forbidden'))

      const { rejectMemory, error } = useMemories()
      const result = await rejectMemory('memory-1')

      expect(result).toBe(false)
      expect(error.value).toBe('Error al rechazar el recuerdo')
    })
  })
})
