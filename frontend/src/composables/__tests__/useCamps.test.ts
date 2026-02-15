import { describe, it, expect, vi, beforeEach } from 'vitest'
import { useCamps } from '@/composables/useCamps'
import { api } from '@/utils/api'
import type { Camp } from '@/types/camp'

// Mock the API module
vi.mock('@/utils/api', () => ({
  api: {
    get: vi.fn(),
    post: vi.fn(),
    put: vi.fn(),
    delete: vi.fn()
  }
}))

describe('useCamps', () => {
  beforeEach(() => {
    vi.clearAllMocks()
  })

  describe('fetchCamps', () => {
    it('should fetch camps successfully', async () => {
      // Arrange
      const mockCamps: Camp[] = [
        {
          id: '1',
          name: 'Mountain Camp',
          description: 'Beautiful mountain location',
          latitude: 46.5833,
          longitude: 7.9833,
          basePriceAdult: 180,
          basePriceChild: 120,
          basePriceBaby: 60,
          status: 'Active',
          createdAt: '2024-01-01T00:00:00Z',
          updatedAt: '2024-01-01T00:00:00Z'
        },
        {
          id: '2',
          name: 'Beach Camp',
          description: 'Relaxing beach location',
          latitude: 40.4168,
          longitude: -3.7038,
          basePriceAdult: 150,
          basePriceChild: 100,
          basePriceBaby: 50,
          status: 'Active',
          createdAt: '2024-01-02T00:00:00Z',
          updatedAt: '2024-01-02T00:00:00Z'
        }
      ]

      vi.mocked(api.get).mockResolvedValue({
        data: {
          success: true,
          data: {
            items: mockCamps,
            totalCount: 2,
            page: 1,
            pageSize: 10,
            totalPages: 1,
            hasNextPage: false,
            hasPreviousPage: false
          },
          error: null
        }
      })

      // Act
      const { camps, loading, error, fetchCamps } = useCamps()
      await fetchCamps()

      // Assert
      expect(camps.value).toEqual(mockCamps)
      expect(loading.value).toBe(false)
      expect(error.value).toBeNull()
      expect(api.get).toHaveBeenCalledWith('/camps')
    })

    it('should handle fetch error', async () => {
      // Arrange
      vi.mocked(api.get).mockRejectedValue(new Error('Network error'))

      // Act
      const { camps, error, fetchCamps } = useCamps()
      await fetchCamps()

      // Assert
      expect(camps.value).toEqual([])
      expect(error.value).toBe('Error al cargar campamentos')
    })
  })

  describe('createCamp', () => {
    it('should create camp successfully', async () => {
      // Arrange
      const newCamp: Camp = {
        id: '3',
        name: 'New Camp',
        description: 'Test camp',
        latitude: 40.0,
        longitude: -3.0,
        basePriceAdult: 200,
        basePriceChild: 150,
        basePriceBaby: 75,
        status: 'Active',
        createdAt: '2024-01-03T00:00:00Z',
        updatedAt: '2024-01-03T00:00:00Z'
      }

      vi.mocked(api.post).mockResolvedValue({
        data: {
          success: true,
          data: newCamp,
          error: null
        }
      })

      // Act
      const { camps, createCamp } = useCamps()
      const result = await createCamp({
        name: 'New Camp',
        description: 'Test camp',
        latitude: 40.0,
        longitude: -3.0,
        basePriceAdult: 200,
        basePriceChild: 150,
        basePriceBaby: 75,
        status: 'Active'
      })

      // Assert
      expect(result).toEqual(newCamp)
      expect(camps.value).toContainEqual(newCamp)
      expect(api.post).toHaveBeenCalledWith('/camps', expect.objectContaining({
        name: 'New Camp'
      }))
    })
  })

  describe('deleteCamp', () => {
    it('should delete camp successfully', async () => {
      // Arrange
      vi.mocked(api.delete).mockResolvedValue({})

      // Act
      const { camps, deleteCamp } = useCamps()
      camps.value = [
        {
          id: '1',
          name: 'Test Camp',
          description: '',
          latitude: 0,
          longitude: 0,
          basePriceAdult: 0,
          basePriceChild: 0,
          basePriceBaby: 0,
          status: 'Active',
          createdAt: '',
          updatedAt: ''
        }
      ]

      const result = await deleteCamp('1')

      // Assert
      expect(result).toBe(true)
      expect(camps.value).toHaveLength(0)
      expect(api.delete).toHaveBeenCalledWith('/camps/1')
    })
  })
})
