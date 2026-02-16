import { ref } from 'vue'
import type { Ref } from 'vue'
import { api } from '@/utils/api'
import type { ApiResponse } from '@/types/api'
import type {
  FamilyUnitResponse,
  CreateFamilyUnitRequest,
  UpdateFamilyUnitRequest,
  FamilyMemberResponse,
  CreateFamilyMemberRequest,
  UpdateFamilyMemberRequest
} from '@/types/family-unit'

export function useFamilyUnits() {
  // State
  const familyUnit: Ref<FamilyUnitResponse | null> = ref(null)
  const familyMembers: Ref<FamilyMemberResponse[]> = ref([])
  const loading = ref(false)
  const error = ref<string | null>(null)

  // Family Unit Operations

  /**
   * Create a new family unit for the current user
   */
  const createFamilyUnit = async (request: CreateFamilyUnitRequest): Promise<FamilyUnitResponse | null> => {
    loading.value = true
    error.value = null

    try {
      const response = await api.post<ApiResponse<FamilyUnitResponse>>('/family-units', request)
      familyUnit.value = response.data.data
      return response.data.data
    } catch (err: any) {
      error.value = err.response?.data?.error?.message || 'Error al crear la unidad familiar'
      return null
    } finally {
      loading.value = false
    }
  }

  /**
   * Get current user's family unit
   */
  const getCurrentUserFamilyUnit = async (): Promise<FamilyUnitResponse | null> => {
    loading.value = true
    error.value = null

    try {
      const response = await api.get<ApiResponse<FamilyUnitResponse>>('/family-units/me')
      familyUnit.value = response.data.data
      return response.data.data
    } catch (err: any) {
      if (err.response?.status === 404) {
        familyUnit.value = null
        return null
      }
      error.value = err.response?.data?.error?.message || 'Error al obtener la unidad familiar'
      return null
    } finally {
      loading.value = false
    }
  }

  /**
   * Get family unit by ID (Admin/Board or representative)
   */
  const getFamilyUnitById = async (id: string): Promise<FamilyUnitResponse | null> => {
    loading.value = true
    error.value = null

    try {
      const response = await api.get<ApiResponse<FamilyUnitResponse>>(`/family-units/${id}`)
      familyUnit.value = response.data.data
      return response.data.data
    } catch (err: any) {
      error.value = err.response?.data?.error?.message || 'Error al obtener la unidad familiar'
      return null
    } finally {
      loading.value = false
    }
  }

  /**
   * Update family unit name
   */
  const updateFamilyUnit = async (
    id: string,
    request: UpdateFamilyUnitRequest
  ): Promise<FamilyUnitResponse | null> => {
    loading.value = true
    error.value = null

    try {
      const response = await api.put<ApiResponse<FamilyUnitResponse>>(
        `/family-units/${id}`,
        request
      )
      familyUnit.value = response.data.data
      return response.data.data
    } catch (err: any) {
      error.value = err.response?.data?.error?.message || 'Error al actualizar la unidad familiar'
      return null
    } finally {
      loading.value = false
    }
  }

  /**
   * Delete family unit (and all members)
   */
  const deleteFamilyUnit = async (id: string): Promise<boolean> => {
    loading.value = true
    error.value = null

    try {
      await api.delete(`/family-units/${id}`)
      familyUnit.value = null
      familyMembers.value = []
      return true
    } catch (err: any) {
      error.value = err.response?.data?.error?.message || 'Error al eliminar la unidad familiar'
      return false
    } finally {
      loading.value = false
    }
  }

  // Family Member Operations

  /**
   * Create a new family member
   */
  const createFamilyMember = async (
    familyUnitId: string,
    request: CreateFamilyMemberRequest
  ): Promise<FamilyMemberResponse | null> => {
    loading.value = true
    error.value = null

    try {
      const response = await api.post<ApiResponse<FamilyMemberResponse>>(
        `/family-units/${familyUnitId}/members`,
        request
      )
      familyMembers.value.push(response.data.data)
      return response.data.data
    } catch (err: any) {
      error.value = err.response?.data?.error?.message || 'Error al crear el miembro familiar'
      return null
    } finally {
      loading.value = false
    }
  }

  /**
   * Get all family members for a family unit
   */
  const getFamilyMembers = async (familyUnitId: string): Promise<FamilyMemberResponse[]> => {
    loading.value = true
    error.value = null

    try {
      const response = await api.get<ApiResponse<FamilyMemberResponse[]>>(
        `/family-units/${familyUnitId}/members`
      )
      familyMembers.value = response.data.data
      return response.data.data
    } catch (err: any) {
      error.value = err.response?.data?.error?.message || 'Error al obtener los miembros familiares'
      return []
    } finally {
      loading.value = false
    }
  }

  /**
   * Get single family member by ID
   */
  const getFamilyMemberById = async (
    familyUnitId: string,
    memberId: string
  ): Promise<FamilyMemberResponse | null> => {
    loading.value = true
    error.value = null

    try {
      const response = await api.get<ApiResponse<FamilyMemberResponse>>(
        `/family-units/${familyUnitId}/members/${memberId}`
      )
      return response.data.data
    } catch (err: any) {
      error.value = err.response?.data?.error?.message || 'Error al obtener el miembro familiar'
      return null
    } finally {
      loading.value = false
    }
  }

  /**
   * Update family member
   */
  const updateFamilyMember = async (
    familyUnitId: string,
    memberId: string,
    request: UpdateFamilyMemberRequest
  ): Promise<FamilyMemberResponse | null> => {
    loading.value = true
    error.value = null

    try {
      const response = await api.put<ApiResponse<FamilyMemberResponse>>(
        `/family-units/${familyUnitId}/members/${memberId}`,
        request
      )

      // Update in local array
      const index = familyMembers.value.findIndex((m) => m.id === memberId)
      if (index !== -1) {
        familyMembers.value[index] = response.data.data
      }

      return response.data.data
    } catch (err: any) {
      error.value = err.response?.data?.error?.message || 'Error al actualizar el miembro familiar'
      return null
    } finally {
      loading.value = false
    }
  }

  /**
   * Delete family member
   */
  const deleteFamilyMember = async (
    familyUnitId: string,
    memberId: string
  ): Promise<boolean> => {
    loading.value = true
    error.value = null

    try {
      await api.delete(`/family-units/${familyUnitId}/members/${memberId}`)

      // Remove from local array
      familyMembers.value = familyMembers.value.filter((m) => m.id !== memberId)

      return true
    } catch (err: any) {
      error.value = err.response?.data?.error?.message || 'Error al eliminar el miembro familiar'
      return false
    } finally {
      loading.value = false
    }
  }

  return {
    // State
    familyUnit,
    familyMembers,
    loading,
    error,

    // Family Unit Methods
    createFamilyUnit,
    getCurrentUserFamilyUnit,
    getFamilyUnitById,
    updateFamilyUnit,
    deleteFamilyUnit,

    // Family Member Methods
    createFamilyMember,
    getFamilyMembers,
    getFamilyMemberById,
    updateFamilyMember,
    deleteFamilyMember
  }
}
