import { ref } from 'vue'
import { api } from '@/utils/api'
import type { ApiResponse } from '@/types/api'
import type {
  AdminRegistrationListItem,
  AdminRegistrationTotals,
  AdminRegistrationListResponse,
  AdminRegistrationFilters,
  AccommodationPreferenceFilter
} from '@/types/registration'
import type { CampEditionExtra, CampEditionAccommodation } from '@/types/camp-edition'

export function useAdminRegistrations() {
  const registrations = ref<AdminRegistrationListItem[]>([])
  const totals = ref<AdminRegistrationTotals | null>(null)
  const totalCount = ref(0)
  const loading = ref(false)
  const error = ref<string | null>(null)
  const pagination = ref({ totalCount: 0, page: 1, pageSize: 20 })

  const editionExtras = ref<CampEditionExtra[]>([])
  const editionAccommodations = ref<CampEditionAccommodation[]>([])
  const filterOptionsLoading = ref(false)

  const exportLoading = ref(false)
  const exportError = ref<string | null>(null)

  const fetchAdminRegistrations = async (
    campEditionId: string,
    params: AdminRegistrationFilters = {}
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
      params.accommodationPreferences?.forEach((f: AccommodationPreferenceFilter) => {
        queryParams.append('accommodationIds', f.accommodationId)
        queryParams.append('accommodationPreferenceOrders', String(f.preferenceOrder))
      })
      params.extraIds?.forEach(id => queryParams.append('extraIds', id))
      params.attendancePeriods?.forEach(p => queryParams.append('attendancePeriods', p))
      params.ageCategories?.forEach(c => queryParams.append('ageCategories', c))

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

  const fetchEditionFilterOptions = async (campEditionId: string): Promise<void> => {
    filterOptionsLoading.value = true
    editionExtras.value = []
    editionAccommodations.value = []
    try {
      const [extrasRes, accommodationsRes] = await Promise.all([
        api.get<ApiResponse<CampEditionExtra[]>>(
          `/camps/editions/${campEditionId}/extras`,
          { params: { activeOnly: true } }
        ),
        api.get<ApiResponse<CampEditionAccommodation[]>>(
          `/camps/editions/${campEditionId}/accommodations`
        )
      ])
      if (extrasRes.data.success && extrasRes.data.data) {
        editionExtras.value = extrasRes.data.data
      }
      if (accommodationsRes.data.success && accommodationsRes.data.data) {
        editionAccommodations.value = accommodationsRes.data.data.filter(a => a.isActive)
      }
    } catch (err: unknown) {
      console.error('Failed to fetch edition filter options:', err)
    } finally {
      filterOptionsLoading.value = false
    }
  }

  const exportToCsv = async (
    campEditionId: string,
    filters: Omit<AdminRegistrationFilters, 'page' | 'pageSize'> = {}
  ): Promise<void> => {
    exportLoading.value = true
    exportError.value = null
    try {
      const queryParams = new URLSearchParams()
      if (filters.search) queryParams.set('search', filters.search)
      if (filters.status) queryParams.set('status', filters.status)
      filters.accommodationPreferences?.forEach((f: AccommodationPreferenceFilter) => {
        queryParams.append('accommodationIds', f.accommodationId)
        queryParams.append('accommodationPreferenceOrders', String(f.preferenceOrder))
      })
      filters.extraIds?.forEach(id => queryParams.append('extraIds', id))
      filters.attendancePeriods?.forEach(p => queryParams.append('attendancePeriods', p))
      filters.ageCategories?.forEach(c => queryParams.append('ageCategories', c))

      const qs = queryParams.toString()
      const response = await api.get(
        `/camp-editions/${campEditionId}/registrations/export/csv${qs ? `?${qs}` : ''}`,
        { responseType: 'blob' }
      )

      const contentDisposition = response.headers['content-disposition'] as string | undefined
      const fileNameMatch = contentDisposition?.match(/filename="([^"]+)"/)
      const fileName =
        fileNameMatch?.[1] ?? `inscripciones-${new Date().toISOString().split('T')[0]}.csv`

      const blob = new Blob([response.data as BlobPart], { type: 'text/csv;charset=utf-8;' })
      const url = URL.createObjectURL(blob)
      const link = document.createElement('a')
      link.href = url
      link.download = fileName
      document.body.appendChild(link)
      link.click()
      document.body.removeChild(link)
      URL.revokeObjectURL(url)
    } catch (err: unknown) {
      exportError.value = 'Error al exportar las inscripciones'
      console.error('Failed to export registrations:', err)
    } finally {
      exportLoading.value = false
    }
  }

  return {
    registrations,
    totals,
    totalCount,
    pagination,
    loading,
    error,
    editionExtras,
    editionAccommodations,
    filterOptionsLoading,
    exportLoading,
    exportError,
    fetchAdminRegistrations,
    fetchEditionFilterOptions,
    exportToCsv
  }
}
