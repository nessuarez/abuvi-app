import { describe, it, expect, vi, beforeEach } from 'vitest'
import { useGooglePlaces } from '@/composables/useGooglePlaces'
import { api } from '@/utils/api'

// Mock the API module
vi.mock('@/utils/api', () => ({
  api: {
    post: vi.fn()
  }
}))

describe('useGooglePlaces', () => {
  beforeEach(() => {
    vi.clearAllMocks()
  })

  describe('searchPlaces', () => {
    it('should search places successfully', async () => {
      // Arrange
      const mockPlaces = [
        {
          placeId: 'ChIJN1t_tDeuEmsRUsoyG83frY4',
          description: 'Camping El Pinar, Madrid',
          mainText: 'Camping El Pinar',
          secondaryText: 'Madrid, España'
        }
      ]
      vi.mocked(api.post).mockResolvedValue({
        data: { success: true, data: mockPlaces, error: null }
      })

      // Act
      const { loading, error, searchPlaces } = useGooglePlaces()
      const result = await searchPlaces('Camping')

      // Assert
      expect(result).toEqual(mockPlaces)
      expect(loading.value).toBe(false)
      expect(error.value).toBeNull()
      expect(api.post).toHaveBeenCalledWith('/places/autocomplete', { input: 'Camping' })
    })

    it('should return empty array for input less than 3 characters', async () => {
      // Act
      const { searchPlaces } = useGooglePlaces()
      const result = await searchPlaces('Ca')

      // Assert
      expect(result).toEqual([])
      expect(api.post).not.toHaveBeenCalled()
    })

    it('should return empty array for empty input', async () => {
      // Act
      const { searchPlaces } = useGooglePlaces()
      const result = await searchPlaces('')

      // Assert
      expect(result).toEqual([])
      expect(api.post).not.toHaveBeenCalled()
    })

    it('should set error and return empty array when API call fails', async () => {
      // Arrange
      vi.mocked(api.post).mockRejectedValue({
        response: { data: { error: { message: 'Servicio no disponible' } } }
      })

      // Act
      const { error, searchPlaces } = useGooglePlaces()
      const result = await searchPlaces('Camping')

      // Assert
      expect(result).toEqual([])
      expect(error.value).toBe('Servicio no disponible')
    })

    it('should use fallback error message when API error has no message', async () => {
      // Arrange
      vi.mocked(api.post).mockRejectedValue(new Error('Network error'))

      // Act
      const { error, searchPlaces } = useGooglePlaces()
      await searchPlaces('Camping')

      // Assert
      expect(error.value).toBe('Error al buscar lugares')
    })

    it('should set error when API returns unsuccessful response', async () => {
      // Arrange
      vi.mocked(api.post).mockResolvedValue({
        data: {
          success: false,
          data: null,
          error: { message: 'Servicio de lugares no disponible', code: 'PLACES_SERVICE_UNAVAILABLE' }
        }
      })

      // Act
      const { error, searchPlaces } = useGooglePlaces()
      const result = await searchPlaces('Camping')

      // Assert
      expect(result).toEqual([])
      expect(error.value).toBe('Servicio de lugares no disponible')
    })

    it('should manage loading state correctly during search', async () => {
      // Arrange
      let resolveFn: (value: unknown) => void
      const pendingPromise = new Promise(resolve => { resolveFn = resolve })

      vi.mocked(api.post).mockReturnValue(pendingPromise as any)

      // Act
      const { loading, searchPlaces } = useGooglePlaces()
      const searchPromise = searchPlaces('Camping')

      // Assert loading is true while pending
      expect(loading.value).toBe(true)

      // Resolve the promise
      resolveFn!({ data: { success: true, data: [], error: null } })
      await searchPromise

      // Assert loading is false after completion
      expect(loading.value).toBe(false)
    })
  })

  describe('getPlaceDetails', () => {
    it('should get place details successfully', async () => {
      // Arrange
      const mockDetails = {
        placeId: 'ChIJN1t_tDeuEmsRUsoyG83frY4',
        name: 'Camping El Pinar',
        formattedAddress: 'Calle Example, 123, Madrid, España',
        latitude: 40.416775,
        longitude: -3.703790,
        types: ['campground', 'lodging']
      }
      vi.mocked(api.post).mockResolvedValue({
        data: { success: true, data: mockDetails, error: null }
      })

      // Act
      const { loading, error, getPlaceDetails } = useGooglePlaces()
      const result = await getPlaceDetails('ChIJN1t_tDeuEmsRUsoyG83frY4')

      // Assert
      expect(result).toEqual(mockDetails)
      expect(loading.value).toBe(false)
      expect(error.value).toBeNull()
      expect(api.post).toHaveBeenCalledWith('/places/details', { placeId: 'ChIJN1t_tDeuEmsRUsoyG83frY4' })
    })

    it('should return null when API call fails', async () => {
      // Arrange
      vi.mocked(api.post).mockRejectedValue({
        response: { data: { error: { message: 'Lugar no encontrado' } } }
      })

      // Act
      const { error, getPlaceDetails } = useGooglePlaces()
      const result = await getPlaceDetails('invalid-place-id')

      // Assert
      expect(result).toBeNull()
      expect(error.value).toBe('Lugar no encontrado')
    })

    it('should return null when API returns unsuccessful response', async () => {
      // Arrange
      vi.mocked(api.post).mockResolvedValue({
        data: {
          success: false,
          data: null,
          error: { message: 'No se encontró información para este lugar', code: 'NOT_FOUND' }
        }
      })

      // Act
      const { error, getPlaceDetails } = useGooglePlaces()
      const result = await getPlaceDetails('nonexistent-place-id')

      // Assert
      expect(result).toBeNull()
      expect(error.value).toBe('No se encontró información para este lugar')
    })

    it('should use fallback error message when API error has no message', async () => {
      // Arrange
      vi.mocked(api.post).mockRejectedValue(new Error('Network error'))

      // Act
      const { error, getPlaceDetails } = useGooglePlaces()
      await getPlaceDetails('some-place-id')

      // Assert
      expect(error.value).toBe('Error al obtener detalles del lugar')
    })

    it('should manage loading state correctly during details fetch', async () => {
      // Arrange
      let resolveFn: (value: unknown) => void
      const pendingPromise = new Promise(resolve => { resolveFn = resolve })

      vi.mocked(api.post).mockReturnValue(pendingPromise as any)

      // Act
      const { loading, getPlaceDetails } = useGooglePlaces()
      const detailsPromise = getPlaceDetails('some-place-id')

      // Assert loading is true while pending
      expect(loading.value).toBe(true)

      // Resolve the promise
      resolveFn!({ data: { success: true, data: null, error: null } })
      await detailsPromise

      // Assert loading is false after completion
      expect(loading.value).toBe(false)
    })
  })
})
