import { describe, it, expect, beforeEach, vi } from 'vitest'
import { useMemberships } from '../useMemberships'
import { api } from '@/utils/api'

vi.mock('@/utils/api', () => ({
  api: {
    get: vi.fn(),
    post: vi.fn(),
    put: vi.fn(),
    delete: vi.fn(),
  },
}))

describe('useMemberships', () => {
  beforeEach(() => {
    vi.clearAllMocks()
  })

  describe('bulkActivateMemberships', () => {
    it('should call the correct endpoint and return the response', async () => {
      const mockResponse = {
        data: {
          success: true,
          data: { activated: 2, skipped: 1, results: [] },
        },
      }
      vi.mocked(api.post).mockResolvedValueOnce(mockResponse)

      const { bulkActivateMemberships } = useMemberships()
      const result = await bulkActivateMemberships('family-unit-1', { year: 2025 })

      expect(result).toEqual(mockResponse.data.data)
      expect(api.post).toHaveBeenCalledWith(
        '/family-units/family-unit-1/membership/bulk',
        { year: 2025 },
      )
    })

    it('should set error and return null on API failure', async () => {
      vi.mocked(api.post).mockRejectedValueOnce({
        response: { data: { error: { message: 'Error al activar las membresías' } } },
      })

      const { bulkActivateMemberships, error } = useMemberships()
      const result = await bulkActivateMemberships('family-unit-1', { year: 2025 })

      expect(result).toBeNull()
      expect(error.value).toBe('Error al activar las membresías')
    })

    it('should set loading to false after completion', async () => {
      vi.mocked(api.post).mockResolvedValueOnce({
        data: { success: true, data: { activated: 0, skipped: 0, results: [] } },
      })

      const { bulkActivateMemberships, loading } = useMemberships()
      const promise = bulkActivateMemberships('family-unit-1', { year: 2025 })
      await promise
      expect(loading.value).toBe(false)
    })
  })

  describe('createMembership', () => {
    it('should send { year } payload to the correct endpoint', async () => {
      vi.mocked(api.post).mockResolvedValueOnce({
        data: {
          success: true,
          data: {
            id: 'm1',
            familyMemberId: 'fm1',
            startDate: '2025-01-01T00:00:00Z',
            isActive: true,
            fees: [],
            createdAt: '2025-01-01',
            updatedAt: '2025-01-01',
          },
        },
      })

      const { createMembership } = useMemberships()
      await createMembership('fu1', 'fm1', { year: 2025 })

      expect(api.post).toHaveBeenCalledWith(
        '/family-units/fu1/members/fm1/membership',
        { year: 2025 },
      )
    })
  })

  describe('updateMemberNumber', () => {
    it('should call PUT endpoint with correct params', async () => {
      const mockResponse = {
        data: {
          success: true,
          data: {
            id: 'm1',
            familyMemberId: 'fm1',
            memberNumber: 7,
            startDate: '2025-01-01T00:00:00Z',
            isActive: true,
            fees: [],
            createdAt: '2025-01-01',
            updatedAt: '2025-01-01',
          },
        },
      }

      vi.mocked(api.put).mockResolvedValueOnce(mockResponse)

      const { updateMemberNumber, membership } = useMemberships()
      const result = await updateMemberNumber('m1', 7)

      expect(result).toEqual(mockResponse.data.data)
      expect(membership.value).toEqual(mockResponse.data.data)
      expect(api.put).toHaveBeenCalledWith('/memberships/m1/member-number', { memberNumber: 7 })
    })

    it('should set error and return null on failure', async () => {
      vi.mocked(api.put).mockRejectedValueOnce({
        response: { data: { error: { message: 'Número de socio/a duplicado' } } },
      })

      const { updateMemberNumber, error } = useMemberships()
      const result = await updateMemberNumber('m1', 7)

      expect(result).toBeNull()
      expect(error.value).toBe('Número de socio/a duplicado')
    })
  })
})
