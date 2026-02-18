import { ref } from 'vue'
import type { Ref } from 'vue'
import { api } from '@/utils/api'
import type { ApiResponse } from '@/types/api'
import type {
  MembershipResponse,
  MembershipFeeResponse,
  CreateMembershipRequest,
  PayFeeRequest,
} from '@/types/membership'

export function useMemberships() {
  const membership: Ref<MembershipResponse | null> = ref(null)
  const fees: Ref<MembershipFeeResponse[]> = ref([])
  const loading = ref(false)
  const error = ref<string | null>(null)

  /**
   * Fetch the active membership for a specific family member.
   * Returns null silently when the member has no membership (404).
   */
  const getMembership = async (
    familyUnitId: string,
    memberId: string,
  ): Promise<MembershipResponse | null> => {
    loading.value = true
    error.value = null
    try {
      const response = await api.get<ApiResponse<MembershipResponse>>(
        `/family-units/${familyUnitId}/members/${memberId}/membership`,
      )
      membership.value = response.data.data
      fees.value = response.data.data?.fees ?? []
      return response.data.data
    } catch (err: any) {
      if (err.response?.status === 404) {
        membership.value = null
        fees.value = []
        return null
      }
      error.value = err.response?.data?.error?.message || 'Error al obtener la membresía'
      return null
    } finally {
      loading.value = false
    }
  }

  /**
   * Create a new membership for a family member.
   */
  const createMembership = async (
    familyUnitId: string,
    memberId: string,
    request: CreateMembershipRequest,
  ): Promise<MembershipResponse | null> => {
    loading.value = true
    error.value = null
    try {
      const response = await api.post<ApiResponse<MembershipResponse>>(
        `/family-units/${familyUnitId}/members/${memberId}/membership`,
        request,
      )
      membership.value = response.data.data
      fees.value = response.data.data?.fees ?? []
      return response.data.data
    } catch (err: any) {
      error.value = err.response?.data?.error?.message || 'Error al crear la membresía'
      return null
    } finally {
      loading.value = false
    }
  }

  /**
   * Deactivate the membership for a family member.
   */
  const deactivateMembership = async (
    familyUnitId: string,
    memberId: string,
  ): Promise<boolean> => {
    loading.value = true
    error.value = null
    try {
      await api.delete(`/family-units/${familyUnitId}/members/${memberId}/membership`)
      membership.value = null
      fees.value = []
      return true
    } catch (err: any) {
      error.value = err.response?.data?.error?.message || 'Error al desactivar la membresía'
      return false
    } finally {
      loading.value = false
    }
  }

  /**
   * List all fees for a membership.
   */
  const getFees = async (membershipId: string): Promise<MembershipFeeResponse[]> => {
    loading.value = true
    error.value = null
    try {
      const response = await api.get<ApiResponse<MembershipFeeResponse[]>>(
        `/memberships/${membershipId}/fees`,
      )
      fees.value = response.data.data
      return response.data.data
    } catch (err: any) {
      error.value = err.response?.data?.error?.message || 'Error al obtener las cuotas'
      return []
    } finally {
      loading.value = false
    }
  }

  /**
   * Mark a specific fee as paid.
   */
  const payFee = async (
    membershipId: string,
    feeId: string,
    request: PayFeeRequest,
  ): Promise<MembershipFeeResponse | null> => {
    loading.value = true
    error.value = null
    try {
      const response = await api.post<ApiResponse<MembershipFeeResponse>>(
        `/memberships/${membershipId}/fees/${feeId}/pay`,
        request,
      )
      // Update fee in local array
      const idx = fees.value.findIndex((f) => f.id === feeId)
      if (idx !== -1) fees.value[idx] = response.data.data
      return response.data.data
    } catch (err: any) {
      error.value = err.response?.data?.error?.message || 'Error al registrar el pago'
      return null
    } finally {
      loading.value = false
    }
  }

  return {
    membership,
    fees,
    loading,
    error,
    getMembership,
    createMembership,
    deactivateMembership,
    getFees,
    payFee,
  }
}
