import { describe, it, expect, vi, beforeEach } from 'vitest'
import { useAdminRegistrations } from '../useAdminRegistrations'
import { api } from '@/utils/api'

vi.mock('@/utils/api', () => ({
  api: { get: vi.fn() }
}))

const EDITION_ID = 'edition-1'

const mockListResponse = {
  data: {
    success: true,
    data: {
      items: [
        {
          id: 'reg-1',
          familyUnit: { id: 'fu-1', name: 'Familia García' },
          representative: { id: 'u-1', firstName: 'Ana', lastName: 'García', email: 'ana@example.com' },
          status: 'Pending',
          memberCount: 3,
          totalAmount: 900,
          amountPaid: 0,
          amountRemaining: 900,
          createdAt: '2026-02-01T00:00:00Z'
        }
      ],
      totalCount: 1,
      totals: {
        totalRegistrations: 1,
        totalMembers: 3,
        totalAmount: 900,
        totalPaid: 0,
        totalRemaining: 900
      }
    }
  }
}

const mockExtrasResponse = {
  data: {
    success: true,
    data: [
      { id: 'extra-1', name: 'Kayak', isActive: true, sortOrder: 1 },
      { id: 'extra-2', name: 'Senderismo', isActive: true, sortOrder: 2 }
    ]
  }
}

const mockAccommodationsResponse = {
  data: {
    success: true,
    data: [
      { id: 'acc-1', name: 'Albergue Principal', accommodationType: 'Lodge', isActive: true },
      { id: 'acc-2', name: 'Zona Tiendas', accommodationType: 'Tent', isActive: true },
      { id: 'acc-3', name: 'Caravanas Inactivas', accommodationType: 'Caravan', isActive: false }
    ]
  }
}

describe('useAdminRegistrations', () => {
  beforeEach(() => {
    vi.clearAllMocks()
  })

  describe('fetchAdminRegistrations', () => {
    it('fetches and populates registrations on success', async () => {
      vi.mocked(api.get).mockResolvedValueOnce(mockListResponse)

      const { registrations, totals, totalCount, loading, error, fetchAdminRegistrations } =
        useAdminRegistrations()

      await fetchAdminRegistrations(EDITION_ID)

      expect(loading.value).toBe(false)
      expect(error.value).toBeNull()
      expect(registrations.value).toHaveLength(1)
      expect(registrations.value[0].id).toBe('reg-1')
      expect(totalCount.value).toBe(1)
      expect(totals.value?.totalAmount).toBe(900)
    })

    it('appends accommodationTypes and extraIds as repeated query params', async () => {
      vi.mocked(api.get).mockResolvedValueOnce(mockListResponse)

      const { fetchAdminRegistrations } = useAdminRegistrations()

      await fetchAdminRegistrations(EDITION_ID, {
        accommodationTypes: ['Lodge', 'Tent'],
        extraIds: ['extra-1', 'extra-2']
      })

      const url = vi.mocked(api.get).mock.calls[0][0] as string
      expect(url).toContain('accommodationTypes=Lodge')
      expect(url).toContain('accommodationTypes=Tent')
      expect(url).toContain('extraIds=extra-1')
      expect(url).toContain('extraIds=extra-2')
    })

    it('updates pagination state from params', async () => {
      vi.mocked(api.get).mockResolvedValueOnce(mockListResponse)

      const { pagination, fetchAdminRegistrations } = useAdminRegistrations()

      await fetchAdminRegistrations(EDITION_ID, { page: 3, pageSize: 10 })

      expect(pagination.value.page).toBe(3)
      expect(pagination.value.pageSize).toBe(10)
    })

    it('sets error and clears registrations on API failure', async () => {
      vi.mocked(api.get).mockRejectedValueOnce(new Error('Network error'))

      const { registrations, totals, error, loading, fetchAdminRegistrations } =
        useAdminRegistrations()

      await fetchAdminRegistrations(EDITION_ID)

      expect(loading.value).toBe(false)
      expect(error.value).toBeTruthy()
      expect(registrations.value).toHaveLength(0)
      expect(totals.value).toBeNull()
    })
  })

  describe('fetchEditionFilterOptions', () => {
    it('fetches extras and accommodations in parallel', async () => {
      vi.mocked(api.get)
        .mockResolvedValueOnce(mockExtrasResponse)
        .mockResolvedValueOnce(mockAccommodationsResponse)

      const { editionExtras, editionAccommodations, filterOptionsLoading, fetchEditionFilterOptions } =
        useAdminRegistrations()

      await fetchEditionFilterOptions(EDITION_ID)

      expect(filterOptionsLoading.value).toBe(false)
      expect(editionExtras.value).toHaveLength(2)
      expect(editionExtras.value[0].name).toBe('Kayak')
      expect(editionAccommodations.value).toHaveLength(2)
    })

    it('filters out inactive accommodations', async () => {
      vi.mocked(api.get)
        .mockResolvedValueOnce(mockExtrasResponse)
        .mockResolvedValueOnce(mockAccommodationsResponse)

      const { editionAccommodations, fetchEditionFilterOptions } = useAdminRegistrations()

      await fetchEditionFilterOptions(EDITION_ID)

      expect(editionAccommodations.value.every(a => a.isActive)).toBe(true)
      expect(editionAccommodations.value.find(a => a.accommodationType === 'Caravan')).toBeUndefined()
    })

    it('does not throw on fetch error, leaves arrays empty', async () => {
      vi.mocked(api.get).mockRejectedValue(new Error('fail'))

      const { editionExtras, editionAccommodations, fetchEditionFilterOptions } =
        useAdminRegistrations()

      await expect(fetchEditionFilterOptions(EDITION_ID)).resolves.not.toThrow()
      expect(editionExtras.value).toHaveLength(0)
      expect(editionAccommodations.value).toHaveLength(0)
    })
  })

  describe('exportToCsv', () => {
    it('passes active filters as query params', async () => {
      const mockBlob = new Blob(['col1,col2'], { type: 'text/csv' })
      vi.mocked(api.get).mockResolvedValueOnce({
        data: mockBlob,
        headers: {}
      })

      const { exportToCsv } = useAdminRegistrations()

      await exportToCsv(EDITION_ID, {
        status: 'Confirmed',
        accommodationTypes: ['Lodge'],
        extraIds: ['extra-1']
      })

      const [url, config] = vi.mocked(api.get).mock.calls[0]
      expect(url).toContain('status=Confirmed')
      expect(url).toContain('accommodationTypes=Lodge')
      expect(url).toContain('extraIds=extra-1')
      expect(config).toMatchObject({ responseType: 'blob' })
    })

    it('sets exportError on failure', async () => {
      vi.mocked(api.get).mockRejectedValueOnce(new Error('Server error'))

      const { exportError, exportLoading, exportToCsv } = useAdminRegistrations()

      await exportToCsv(EDITION_ID)

      expect(exportLoading.value).toBe(false)
      expect(exportError.value).toBeTruthy()
    })
  })
})
