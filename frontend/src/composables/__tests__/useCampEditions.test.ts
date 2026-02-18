import { describe, it, expect, vi, beforeEach } from 'vitest'
import { useCampEditions } from '../useCampEditions'
import { api } from '@/utils/api'

vi.mock('@/utils/api', () => ({
  api: { get: vi.fn(), post: vi.fn(), put: vi.fn(), delete: vi.fn() }
}))

describe('useCampEditions - fetchCurrentCampEdition', () => {
  beforeEach(() => vi.clearAllMocks())

  it('should set currentCampEdition when API returns a camp edition', async () => {
    const mockEdition = {
      id: 'edition-1', campId: 'camp-1', year: 2026,
      status: 'Open', startDate: '2026-07-01', endDate: '2026-07-15',
      location: 'Montaña Norte', pricePerAdult: 450, pricePerChild: 300,
      pricePerBaby: 0, useCustomAgeRanges: false, maxCapacity: 120,
      isArchived: false, createdAt: '2026-01-01T00:00:00Z', updatedAt: '2026-01-01T00:00:00Z',
      registrationCount: 45, availableSpots: 75,
      camp: { id: 'camp-1', name: 'Mountain Camp', latitude: 46.8, longitude: 8.2 }
    }
    vi.mocked(api.get).mockResolvedValueOnce({
      data: { success: true, data: mockEdition, error: null }
    })

    const { currentCampEdition, loading, error, fetchCurrentCampEdition } = useCampEditions()
    await fetchCurrentCampEdition()

    expect(currentCampEdition.value).toEqual(mockEdition)
    expect(loading.value).toBe(false)
    expect(error.value).toBeNull()
    expect(api.get).toHaveBeenCalledWith('/camps/current')
  })

  it('should set currentCampEdition to null and no error on 404', async () => {
    vi.mocked(api.get).mockRejectedValueOnce({ response: { status: 404 } })

    const { currentCampEdition, error, fetchCurrentCampEdition } = useCampEditions()
    await fetchCurrentCampEdition()

    expect(currentCampEdition.value).toBeNull()
    expect(error.value).toBeNull()
  })

  it('should set error on non-404 network failure', async () => {
    vi.mocked(api.get).mockRejectedValueOnce({
      response: { status: 500, data: { error: { message: 'Server error' } } }
    })

    const { error, fetchCurrentCampEdition } = useCampEditions()
    await fetchCurrentCampEdition()

    expect(error.value).toBe('Server error')
  })

  it('should set loading to true during fetch and false after', async () => {
    let resolvePromise!: (value: unknown) => void
    vi.mocked(api.get).mockReturnValueOnce(
      new Promise((r) => { resolvePromise = r })
    )

    const { loading, fetchCurrentCampEdition } = useCampEditions()
    const promise = fetchCurrentCampEdition()
    expect(loading.value).toBe(true)
    resolvePromise({ data: { success: true, data: null, error: null } })
    await promise
    expect(loading.value).toBe(false)
  })
})
