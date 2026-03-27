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

  describe('createFee', () => {
    it('should call the correct endpoint and return the fee', async () => {
      const mockFee = {
        id: 'fee-1',
        membershipId: 'm1',
        year: 2026,
        amount: 50,
        status: 'Pending',
        paidDate: null,
        paymentReference: null,
        createdAt: '2026-01-01',
      }
      vi.mocked(api.post).mockResolvedValueOnce({ data: { success: true, data: mockFee } })

      const { createFee, fees } = useMemberships()
      const result = await createFee('m1', { year: 2026, amount: 50 })

      expect(result).toEqual(mockFee)
      expect(fees.value).toContainEqual(mockFee)
      expect(api.post).toHaveBeenCalledWith('/memberships/m1/fees', { year: 2026, amount: 50 })
    })

    it('should set error and return null on API error', async () => {
      vi.mocked(api.post).mockRejectedValueOnce({
        response: { data: { error: { message: 'Ya existe una cuota para este año' } } },
      })

      const { createFee, error } = useMemberships()
      const result = await createFee('m1', { year: 2026, amount: 50 })

      expect(result).toBeNull()
      expect(error.value).toBe('Ya existe una cuota para este año')
    })
  })

  describe('reactivateMembership', () => {
    const mockMembership = {
      id: 'm1',
      familyMemberId: 'fm1',
      memberNumber: null,
      startDate: '2024-01-01',
      endDate: null,
      isActive: true,
      fees: [],
      createdAt: '2024-01-01',
      updatedAt: '2026-01-01',
    }

    it('should call the correct endpoint and return the membership', async () => {
      vi.mocked(api.post).mockResolvedValueOnce({ data: { success: true, data: mockMembership } })

      const { reactivateMembership, membership } = useMemberships()
      const result = await reactivateMembership('fu1', 'fm1', { year: 2026 })

      expect(result).toEqual(mockMembership)
      expect(membership.value).toEqual(mockMembership)
      expect(api.post).toHaveBeenCalledWith(
        '/family-units/fu1/members/fm1/membership/reactivate',
        { year: 2026 },
      )
    })

    it('should set error and return null when membership is already active (409)', async () => {
      vi.mocked(api.post).mockRejectedValueOnce({
        response: { data: { error: { message: 'El miembro ya tiene una membresía activa' } } },
      })

      const { reactivateMembership, error } = useMemberships()
      const result = await reactivateMembership('fu1', 'fm1', { year: 2026 })

      expect(result).toBeNull()
      expect(error.value).toBe('El miembro ya tiene una membresía activa')
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
