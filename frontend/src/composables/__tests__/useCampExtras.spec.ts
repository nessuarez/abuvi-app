import { describe, it, expect, beforeEach, vi } from 'vitest'
import { useCampExtras } from '../useCampExtras'
import { api } from '@/utils/api'

vi.mock('@/utils/api', () => ({
  api: {
    get: vi.fn(),
    post: vi.fn(),
    put: vi.fn(),
    patch: vi.fn(),
    delete: vi.fn()
  }
}))

const EDITION_ID = 'edition-123'

describe('useCampExtras', () => {
  beforeEach(() => {
    vi.clearAllMocks()
  })

  describe('reorderExtras', () => {
    it('should reorder extras successfully and return true', async () => {
      vi.mocked(api.put).mockResolvedValueOnce({
        data: { success: true, data: 'Extras reordered successfully' }
      })

      const { reorderExtras, loading } = useCampExtras(EDITION_ID)
      const result = await reorderExtras({ orderedIds: ['id-1', 'id-2'] })

      expect(result).toBe(true)
      expect(loading.value).toBe(false)
      expect(api.put).toHaveBeenCalledWith(
        `/camps/editions/${EDITION_ID}/extras/reorder`,
        { orderedIds: ['id-1', 'id-2'] }
      )
    })

    it('should return false and set error on failure', async () => {
      const mockError = {
        response: {
          data: {
            error: {
              message: 'Ordered IDs must contain exactly all extras'
            }
          }
        }
      }

      vi.mocked(api.put).mockRejectedValueOnce(mockError)

      const { reorderExtras, error } = useCampExtras(EDITION_ID)
      const result = await reorderExtras({ orderedIds: ['id-1'] })

      expect(result).toBe(false)
      expect(error.value).toBe('Ordered IDs must contain exactly all extras')
    })

    it('should call the correct endpoint', async () => {
      vi.mocked(api.put).mockResolvedValueOnce({
        data: { success: true, data: 'ok' }
      })

      const { reorderExtras } = useCampExtras(EDITION_ID)
      await reorderExtras({ orderedIds: ['a', 'b', 'c'] })

      expect(api.put).toHaveBeenCalledWith(
        `/camps/editions/${EDITION_ID}/extras/reorder`,
        { orderedIds: ['a', 'b', 'c'] }
      )
    })
  })
})
