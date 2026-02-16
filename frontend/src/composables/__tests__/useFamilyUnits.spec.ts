import { describe, it, expect, beforeEach, vi } from 'vitest'
import { useFamilyUnits } from '../useFamilyUnits'
import { api } from '@/utils/api'

vi.mock('@/utils/api', () => ({
  api: {
    get: vi.fn(),
    post: vi.fn(),
    put: vi.fn(),
    delete: vi.fn()
  }
}))

describe('useFamilyUnits', () => {
  beforeEach(() => {
    vi.clearAllMocks()
  })

  describe('createFamilyUnit', () => {
    it('should create family unit successfully', async () => {
      const mockResponse = {
        data: {
          success: true,
          data: {
            id: '123',
            name: 'Garcia Family',
            representativeUserId: 'user-1',
            createdAt: '2026-02-15T10:00:00Z',
            updatedAt: '2026-02-15T10:00:00Z'
          }
        }
      }

      vi.mocked(api.post).mockResolvedValueOnce(mockResponse)

      const { createFamilyUnit, familyUnit } = useFamilyUnits()
      const result = await createFamilyUnit({ name: 'Garcia Family' })

      expect(result).toEqual(mockResponse.data.data)
      expect(familyUnit.value).toEqual(mockResponse.data.data)
      expect(api.post).toHaveBeenCalledWith('/api/family-units', { name: 'Garcia Family' })
    })

    it('should handle error when creating family unit', async () => {
      const mockError = {
        response: {
          data: {
            error: {
              message: 'Ya tienes una unidad familiar'
            }
          }
        }
      }

      vi.mocked(api.post).mockRejectedValueOnce(mockError)

      const { createFamilyUnit, error } = useFamilyUnits()
      const result = await createFamilyUnit({ name: 'Garcia Family' })

      expect(result).toBeNull()
      expect(error.value).toBe('Ya tienes una unidad familiar')
    })
  })

  describe('getCurrentUserFamilyUnit', () => {
    it('should get current user family unit successfully', async () => {
      const mockResponse = {
        data: {
          success: true,
          data: {
            id: '123',
            name: 'Garcia Family',
            representativeUserId: 'user-1',
            createdAt: '2026-02-15T10:00:00Z',
            updatedAt: '2026-02-15T10:00:00Z'
          }
        }
      }

      vi.mocked(api.get).mockResolvedValueOnce(mockResponse)

      const { getCurrentUserFamilyUnit, familyUnit } = useFamilyUnits()
      const result = await getCurrentUserFamilyUnit()

      expect(result).toEqual(mockResponse.data.data)
      expect(familyUnit.value).toEqual(mockResponse.data.data)
    })

    it('should handle 404 when user has no family unit', async () => {
      const mockError = {
        response: {
          status: 404
        }
      }

      vi.mocked(api.get).mockRejectedValueOnce(mockError)

      const { getCurrentUserFamilyUnit, familyUnit } = useFamilyUnits()
      const result = await getCurrentUserFamilyUnit()

      expect(result).toBeNull()
      expect(familyUnit.value).toBeNull()
    })
  })

  describe('updateFamilyUnit', () => {
    it('should update family unit successfully', async () => {
      const mockResponse = {
        data: {
          success: true,
          data: {
            id: '123',
            name: 'Updated Family',
            representativeUserId: 'user-1',
            createdAt: '2026-02-15T10:00:00Z',
            updatedAt: '2026-02-15T11:00:00Z'
          }
        }
      }

      vi.mocked(api.put).mockResolvedValueOnce(mockResponse)

      const { updateFamilyUnit, familyUnit } = useFamilyUnits()
      const result = await updateFamilyUnit('123', { name: 'Updated Family' })

      expect(result).toEqual(mockResponse.data.data)
      expect(familyUnit.value).toEqual(mockResponse.data.data)
    })
  })

  describe('deleteFamilyUnit', () => {
    it('should delete family unit successfully', async () => {
      vi.mocked(api.delete).mockResolvedValueOnce({})

      const { deleteFamilyUnit, familyUnit, familyMembers } = useFamilyUnits()

      // Set initial state
      familyUnit.value = {
        id: '123',
        name: 'Garcia Family',
        representativeUserId: 'user-1',
        createdAt: '2026-02-15T10:00:00Z',
        updatedAt: '2026-02-15T10:00:00Z'
      }
      familyMembers.value = [
        {
          id: 'member-1',
          familyUnitId: '123',
          userId: null,
          firstName: 'Maria',
          lastName: 'Garcia',
          dateOfBirth: '2015-06-15',
          relationship: 'Child' as any,
          documentNumber: null,
          email: null,
          phone: null,
          hasMedicalNotes: false,
          hasAllergies: false,
          createdAt: '2026-02-15T11:00:00Z',
          updatedAt: '2026-02-15T11:00:00Z'
        }
      ]

      const result = await deleteFamilyUnit('123')

      expect(result).toBe(true)
      expect(familyUnit.value).toBeNull()
      expect(familyMembers.value).toHaveLength(0)
    })
  })

  describe('createFamilyMember', () => {
    it('should create family member successfully', async () => {
      const mockResponse = {
        data: {
          success: true,
          data: {
            id: 'member-1',
            familyUnitId: 'unit-1',
            userId: null,
            firstName: 'Maria',
            lastName: 'Garcia',
            dateOfBirth: '2015-06-15',
            relationship: 'Child',
            documentNumber: 'ABC123',
            email: 'maria@example.com',
            phone: '+34612345678',
            hasMedicalNotes: true,
            hasAllergies: true,
            createdAt: '2026-02-15T11:00:00Z',
            updatedAt: '2026-02-15T11:00:00Z'
          }
        }
      }

      vi.mocked(api.post).mockResolvedValueOnce(mockResponse)

      const { createFamilyMember, familyMembers } = useFamilyUnits()
      const request = {
        firstName: 'Maria',
        lastName: 'Garcia',
        dateOfBirth: '2015-06-15',
        relationship: 'Child' as any,
        documentNumber: 'ABC123',
        email: 'maria@example.com',
        phone: '+34612345678',
        medicalNotes: 'Asthma',
        allergies: 'Peanuts'
      }

      const result = await createFamilyMember('unit-1', request)

      expect(result).toEqual(mockResponse.data.data)
      expect(familyMembers.value).toContainEqual(mockResponse.data.data)
    })
  })

  describe('getFamilyMembers', () => {
    it('should get all family members successfully', async () => {
      const mockMembers = [
        {
          id: 'member-1',
          familyUnitId: 'unit-1',
          userId: null,
          firstName: 'Maria',
          lastName: 'Garcia',
          dateOfBirth: '2015-06-15',
          relationship: 'Child',
          documentNumber: null,
          email: null,
          phone: null,
          hasMedicalNotes: false,
          hasAllergies: false,
          createdAt: '2026-02-15T11:00:00Z',
          updatedAt: '2026-02-15T11:00:00Z'
        }
      ]

      const mockResponse = {
        data: {
          success: true,
          data: mockMembers
        }
      }

      vi.mocked(api.get).mockResolvedValueOnce(mockResponse)

      const { getFamilyMembers, familyMembers } = useFamilyUnits()
      const result = await getFamilyMembers('unit-1')

      expect(result).toEqual(mockMembers)
      expect(familyMembers.value).toEqual(mockMembers)
    })
  })

  describe('updateFamilyMember', () => {
    it('should update family member successfully', async () => {
      const initialMember = {
        id: 'member-1',
        familyUnitId: 'unit-1',
        userId: null,
        firstName: 'Maria',
        lastName: 'Garcia',
        dateOfBirth: '2015-06-15',
        relationship: 'Child' as any,
        documentNumber: 'ABC123',
        email: 'maria@example.com',
        phone: '+34612345678',
        hasMedicalNotes: false,
        hasAllergies: false,
        createdAt: '2026-02-15T11:00:00Z',
        updatedAt: '2026-02-15T11:00:00Z'
      }

      const updatedMember = { ...initialMember, lastName: 'Garcia Lopez' }

      const mockResponse = {
        data: {
          success: true,
          data: updatedMember
        }
      }

      vi.mocked(api.put).mockResolvedValueOnce(mockResponse)

      const { updateFamilyMember, familyMembers } = useFamilyUnits()
      familyMembers.value = [initialMember]

      const result = await updateFamilyMember('unit-1', 'member-1', {
        ...initialMember,
        lastName: 'Garcia Lopez'
      })

      expect(result).toEqual(updatedMember)
      expect(familyMembers.value[0]).toEqual(updatedMember)
    })
  })

  describe('deleteFamilyMember', () => {
    it('should delete family member successfully', async () => {
      vi.mocked(api.delete).mockResolvedValueOnce({})

      const { deleteFamilyMember, familyMembers } = useFamilyUnits()
      familyMembers.value = [
        {
          id: 'member-1',
          familyUnitId: 'unit-1',
          userId: null,
          firstName: 'Maria',
          lastName: 'Garcia',
          dateOfBirth: '2015-06-15',
          relationship: 'Child' as any,
          documentNumber: null,
          email: null,
          phone: null,
          hasMedicalNotes: false,
          hasAllergies: false,
          createdAt: '2026-02-15T11:00:00Z',
          updatedAt: '2026-02-15T11:00:00Z'
        }
      ]

      const result = await deleteFamilyMember('unit-1', 'member-1')

      expect(result).toBe(true)
      expect(familyMembers.value).toHaveLength(0)
    })
  })

  describe('getFamilyUnitById', () => {
    it('should get family unit by ID successfully', async () => {
      const mockResponse = {
        data: {
          success: true,
          data: {
            id: '123',
            name: 'Garcia Family',
            representativeUserId: 'user-1',
            createdAt: '2026-02-15T10:00:00Z',
            updatedAt: '2026-02-15T10:00:00Z'
          }
        }
      }

      vi.mocked(api.get).mockResolvedValueOnce(mockResponse)

      const { getFamilyUnitById, familyUnit } = useFamilyUnits()
      const result = await getFamilyUnitById('123')

      expect(result).toEqual(mockResponse.data.data)
      expect(familyUnit.value).toEqual(mockResponse.data.data)
    })
  })

  describe('getFamilyMemberById', () => {
    it('should get family member by ID successfully', async () => {
      const mockMember = {
        id: 'member-1',
        familyUnitId: 'unit-1',
        userId: null,
        firstName: 'Maria',
        lastName: 'Garcia',
        dateOfBirth: '2015-06-15',
        relationship: 'Child',
        documentNumber: null,
        email: null,
        phone: null,
        hasMedicalNotes: false,
        hasAllergies: false,
        createdAt: '2026-02-15T11:00:00Z',
        updatedAt: '2026-02-15T11:00:00Z'
      }

      const mockResponse = {
        data: {
          success: true,
          data: mockMember
        }
      }

      vi.mocked(api.get).mockResolvedValueOnce(mockResponse)

      const { getFamilyMemberById } = useFamilyUnits()
      const result = await getFamilyMemberById('unit-1', 'member-1')

      expect(result).toEqual(mockMember)
    })
  })
})
