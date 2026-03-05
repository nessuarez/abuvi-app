import { ref } from 'vue'
import { api } from '@/utils/api'
import type { ApiResponse } from '@/types/api'
import type {
  AdminRegistrationListItem,
  AdminRegistrationTotals,
  AdminRegistrationListResponse
} from '@/types/registration'

export function useAdminRegistrations() {
  const registrations = ref<AdminRegistrationListItem[]>([])
  const totals = ref<AdminRegistrationTotals | null>(null)
  const totalCount = ref(0)
  const loading = ref(false)
  const error = ref<string | null>(null)
  const pagination = ref({
    totalCount: 0,
    page: 1,
    pageSize: 20
  })

  const fetchAdminRegistrations = async (
    campEditionId: string,
    params: {
      page?: number
      pageSize?: number
      search?: string
      status?: string
    } = {}
  ): Promise<void> => {
    loading.value = true
    error.value = null
    try {
      const queryParams = new URLSearchParams({
        page: String(params.page ?? 1),
        pageSize: String(params.pageSize ?? 20)
      })
      if (params.search) queryParams.set('search', params.search)
      if (params.status) queryParams.set('status', params.status)

      const response = await api.get<ApiResponse<AdminRegistrationListResponse>>(
        `/camp-editions/${campEditionId}/registrations?${queryParams.toString()}`
      )
      if (response.data.success && response.data.data) {
        registrations.value = response.data.data.items
        totalCount.value = response.data.data.totalCount
        totals.value = response.data.data.totals
        pagination.value = {
          totalCount: response.data.data.totalCount,
          page: params.page ?? 1,
          pageSize: params.pageSize ?? 20
        }
      }
    } catch (err: unknown) {
      error.value = (err as { response?: { data?: { error?: { message?: string } } } })
        ?.response?.data?.error?.message || 'Error al cargar inscripciones'
      console.error('Failed to fetch admin registrations:', err)
      registrations.value = []
      totals.value = null
      totalCount.value = 0
    } finally {
      loading.value = false
    }
  }

  return {
    registrations,
    totals,
    totalCount,
    pagination,
    loading,
    error,
    fetchAdminRegistrations
  }
}
