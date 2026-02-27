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

const makeCamp = (overrides: Partial<Camp> = {}): Camp => ({
  id: '1',
  name: 'Mountain Camp',
  description: 'Beautiful mountain location',
  rawAddress: 'Pyrenees, Spain',
  latitude: 46.5833,
  longitude: 7.9833,
  googlePlaceId: null,
  pricePerAdult: 180,
  pricePerChild: 120,
  pricePerBaby: 60,
  isActive: true,
  createdAt: '2024-01-01T00:00:00Z',
  updatedAt: '2024-01-01T00:00:00Z',
  formattedAddress: null,
  phoneNumber: null,
  websiteUrl: null,
  googleMapsUrl: null,
  googleRating: null,
  googleRatingCount: null,
  businessStatus: null,
  province: null,
  contactEmail: null,
  contactPerson: null,
  contactCompany: null,
  secondaryWebsiteUrl: null,
  basePrice: null,
  vatIncluded: null,
  externalSourceId: null,
  abuviManagedByUserId: null,
  abuviContactedAt: null,
  abuviPossibility: null,
  abuviLastVisited: null,
  abuviHasDataErrors: null,
  lastModifiedByUserId: null,
  ...overrides
})

describe('useCamps', () => {
  beforeEach(() => {
    vi.clearAllMocks()
  })

  describe('fetchCamps', () => {
    it('should fetch camps successfully', async () => {
      // Arrange
      const mockCamps: Camp[] = [
        makeCamp({ id: '1', name: 'Mountain Camp' }),
        makeCamp({ id: '2', name: 'Beach Camp', latitude: 40.4168, longitude: -3.7038 })
      ]

      vi.mocked(api.get).mockResolvedValue({
        data: { success: true, data: mockCamps, error: null }
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

    it('should append isActive query param when provided', async () => {
      vi.mocked(api.get).mockResolvedValue({
        data: { success: true, data: [], error: null }
      })

      const { fetchCamps } = useCamps()
      await fetchCamps({ isActive: true })

      expect(api.get).toHaveBeenCalledWith('/camps?isActive=true')
    })

    it('should append skip and take query params when provided', async () => {
      vi.mocked(api.get).mockResolvedValue({
        data: { success: true, data: [], error: null }
      })

      const { fetchCamps } = useCamps()
      await fetchCamps({ skip: 10, take: 20 })

      expect(api.get).toHaveBeenCalledWith('/camps?skip=10&take=20')
    })

    it('should set camps to empty array when success is false', async () => {
      vi.mocked(api.get).mockResolvedValue({
        data: { success: false, data: null, error: { message: 'Not found' } }
      })

      const { camps, fetchCamps } = useCamps()
      await fetchCamps()

      expect(camps.value).toEqual([])
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
    it('should create camp successfully and add it to the list', async () => {
      // Arrange
      const newCamp = makeCamp({ id: '3', name: 'New Camp' })

      vi.mocked(api.post).mockResolvedValue({
        data: { success: true, data: newCamp, error: null }
      })

      // Act
      const { camps, createCamp } = useCamps()
      const result = await createCamp({
        name: 'New Camp',
        description: null,
        rawAddress: null,
        latitude: null,
        longitude: null,
        googlePlaceId: null,
        pricePerAdult: 180,
        pricePerChild: 120,
        pricePerBaby: 60
      })

      // Assert
      expect(result).toEqual(newCamp)
      expect(camps.value).toContainEqual(newCamp)
      expect(api.post).toHaveBeenCalledWith('/camps', expect.objectContaining({ name: 'New Camp' }))
    })

    it('should return null and set error when create fails', async () => {
      vi.mocked(api.post).mockRejectedValue(new Error('Server error'))

      const { createCamp, error } = useCamps()
      const result = await createCamp({
        name: 'New Camp',
        description: null,
        rawAddress: null,
        latitude: null,
        longitude: null,
        googlePlaceId: null,
        pricePerAdult: 0,
        pricePerChild: 0,
        pricePerBaby: 0
      })

      expect(result).toBeNull()
      expect(error.value).toBe('Error al crear campamento')
    })
  })

  describe('updateCamp', () => {
    it('should update camp and replace it in the list', async () => {
      const original = makeCamp({ id: '1', name: 'Old Name' })
      const updated = makeCamp({ id: '1', name: 'Updated Name' })

      vi.mocked(api.put).mockResolvedValue({
        data: { success: true, data: updated, error: null }
      })

      const { camps, updateCamp } = useCamps()
      camps.value = [original]

      const result = await updateCamp('1', { ...updated, isActive: true })

      expect(result).toEqual(updated)
      expect(camps.value[0].name).toBe('Updated Name')
      expect(api.put).toHaveBeenCalledWith('/camps/1', expect.objectContaining({ name: 'Updated Name' }))
    })
  })

  describe('deleteCamp', () => {
    it('should delete camp successfully and remove it from the list', async () => {
      // Arrange
      vi.mocked(api.delete).mockResolvedValue({})

      // Act
      const { camps, deleteCamp } = useCamps()
      camps.value = [makeCamp({ id: '1' })]

      const result = await deleteCamp('1')

      // Assert
      expect(result).toBe(true)
      expect(camps.value).toHaveLength(0)
      expect(api.delete).toHaveBeenCalledWith('/camps/1')
    })

    it('should return false and keep the camp when delete fails', async () => {
      vi.mocked(api.delete).mockRejectedValue(new Error('Server error'))

      const { camps, deleteCamp, error } = useCamps()
      camps.value = [makeCamp({ id: '1' })]

      const result = await deleteCamp('1')

      expect(result).toBe(false)
      expect(camps.value).toHaveLength(1)
      expect(error.value).toBe('Error al eliminar campamento')
    })
  })

  describe('fetchCampObservations', () => {
    it('should set campObservations on success', async () => {
      const mockData = [
        { id: '1', campId: 'camp-1', text: 'Test observation', season: '2024', createdByUserId: 'user-1', createdAt: '2024-01-01' }
      ]
      vi.mocked(api.get).mockResolvedValue({ data: { success: true, data: mockData } })

      const { fetchCampObservations, campObservations } = useCamps()
      await fetchCampObservations('camp-1')

      expect(campObservations.value).toEqual(mockData)
      expect(api.get).toHaveBeenCalledWith('/camps/camp-1/observations')
    })

    it('should set observationsError on failure', async () => {
      vi.mocked(api.get).mockRejectedValue({
        response: { data: { error: { message: 'Not found' } } }
      })

      const { fetchCampObservations, observationsError } = useCamps()
      await fetchCampObservations('camp-1')

      expect(observationsError.value).toBe('Not found')
    })
  })

  describe('addCampObservation', () => {
    it('should return observation on success and prepend to list', async () => {
      const newObs = {
        id: '2', campId: 'camp-1', text: 'New observation', season: '2025',
        createdByUserId: 'user-1', createdAt: '2025-01-01'
      }
      vi.mocked(api.post).mockResolvedValue({ data: { success: true, data: newObs } })

      const { addCampObservation, campObservations } = useCamps()
      const result = await addCampObservation('camp-1', { text: 'New observation', season: '2025' })

      expect(result).toEqual(newObs)
      expect(campObservations.value[0]).toEqual(newObs)
      expect(api.post).toHaveBeenCalledWith('/camps/camp-1/observations', {
        text: 'New observation', season: '2025'
      })
    })

    it('should return null and set error on failure', async () => {
      vi.mocked(api.post).mockRejectedValue({
        response: { data: { error: { message: 'Validation error' } } }
      })

      const { addCampObservation, observationsError } = useCamps()
      const result = await addCampObservation('camp-1', { text: '', season: null })

      expect(result).toBeNull()
      expect(observationsError.value).toBe('Validation error')
    })
  })

  describe('fetchCampAuditLog', () => {
    it('should set campAuditLog on success', async () => {
      const mockData = [
        { id: '1', fieldName: 'BasePrice', oldValue: '100', newValue: '200', changedByUserId: 'user-1', changedAt: '2025-01-01' }
      ]
      vi.mocked(api.get).mockResolvedValue({ data: { success: true, data: mockData } })

      const { fetchCampAuditLog, campAuditLog } = useCamps()
      await fetchCampAuditLog('camp-1')

      expect(campAuditLog.value).toEqual(mockData)
      expect(api.get).toHaveBeenCalledWith('/camps/camp-1/audit-log')
    })

    it('should set auditLogError on failure', async () => {
      vi.mocked(api.get).mockRejectedValue({
        response: { data: { error: { message: 'Forbidden' } } }
      })

      const { fetchCampAuditLog, auditLogError } = useCamps()
      await fetchCampAuditLog('camp-1')

      expect(auditLogError.value).toBe('Forbidden')
    })
  })

})
