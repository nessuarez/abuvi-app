import { describe, it, expect, vi, beforeEach } from 'vitest'
import { useCampEditions } from '../useCampEditions'
import { api } from '@/utils/api'

vi.mock('@/utils/api', () => ({
  api: { get: vi.fn(), post: vi.fn(), put: vi.fn(), patch: vi.fn(), delete: vi.fn() }
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

describe('useCampEditions - getActiveEdition', () => {
  beforeEach(() => vi.clearAllMocks())

  const mockActiveEdition = {
    id: 'edition-1',
    campId: 'camp-1',
    campName: 'Mountain Camp',
    campLocation: 'Montaña Norte',
    campFormattedAddress: 'Calle Mayor 1, Madrid',
    year: 2026,
    startDate: '2026-07-01',
    endDate: '2026-07-15',
    pricePerAdult: 450,
    pricePerChild: 300,
    pricePerBaby: 0,
    useCustomAgeRanges: false,
    status: 'Open',
    registrationCount: 0,
    createdAt: '2026-01-01T00:00:00Z',
    updatedAt: '2026-01-01T00:00:00Z'
  }

  it('should set activeEdition to ActiveCampEditionResponse on success', async () => {
    vi.mocked(api.get).mockResolvedValueOnce({
      data: { success: true, data: mockActiveEdition, error: null }
    })

    const { activeEdition, loading, error, getActiveEdition } = useCampEditions()
    await getActiveEdition()

    expect(activeEdition.value).toEqual(mockActiveEdition)
    expect(loading.value).toBe(false)
    expect(error.value).toBeNull()
    expect(api.get).toHaveBeenCalledWith('/camps/editions/active')
  })

  it('should append year query param when year is provided', async () => {
    vi.mocked(api.get).mockResolvedValueOnce({
      data: { success: true, data: mockActiveEdition, error: null }
    })

    const { getActiveEdition } = useCampEditions()
    await getActiveEdition(2026)

    expect(api.get).toHaveBeenCalledWith('/camps/editions/active?year=2026')
  })

  it('should set activeEdition to null when data is null', async () => {
    vi.mocked(api.get).mockResolvedValueOnce({
      data: { success: true, data: null, error: null }
    })

    const { activeEdition, error, getActiveEdition } = useCampEditions()
    await getActiveEdition()

    expect(activeEdition.value).toBeNull()
    expect(error.value).toBeNull()
  })

  it('should set error and null activeEdition on API failure', async () => {
    vi.mocked(api.get).mockRejectedValueOnce({
      response: { data: { error: { message: 'No active edition' } } }
    })

    const { activeEdition, error, getActiveEdition } = useCampEditions()
    await getActiveEdition()

    expect(activeEdition.value).toBeNull()
    expect(error.value).toBe('No active edition')
  })
})

describe('useCampEditions - changeStatus', () => {
  beforeEach(() => vi.clearAllMocks())

  const mockUpdatedEdition = {
    id: 'edition-1',
    campId: 'camp-1',
    year: 2026,
    status: 'Open',
    startDate: '2026-07-01',
    endDate: '2026-07-15',
    location: 'Montaña Norte',
    pricePerAdult: 450,
    pricePerChild: 300,
    pricePerBaby: 0,
    useCustomAgeRanges: false,
    maxCapacity: 120,
    isArchived: false,
    createdAt: '2026-01-01T00:00:00Z',
    updatedAt: '2026-01-01T00:00:00Z'
  }

  it('should call api.patch (not api.post) when changing status', async () => {
    vi.mocked(api.patch).mockResolvedValueOnce({
      data: { success: true, data: mockUpdatedEdition, error: null }
    })

    const { changeStatus } = useCampEditions()
    await changeStatus('edition-1', 'Open')

    expect(api.patch).toHaveBeenCalled()
    expect(api.post).not.toHaveBeenCalled()
  })

  it('should send { status } not { newStatus } in the request body', async () => {
    vi.mocked(api.patch).mockResolvedValueOnce({
      data: { success: true, data: mockUpdatedEdition, error: null }
    })

    const { changeStatus } = useCampEditions()
    await changeStatus('edition-1', 'Open')

    expect(api.patch).toHaveBeenCalledWith(
      '/camps/editions/edition-1/status',
      { status: 'Open' }
    )
  })

  it('should update the allEditions array on success', async () => {
    const existingEdition = { ...mockUpdatedEdition, status: 'Draft' }
    vi.mocked(api.patch).mockResolvedValueOnce({
      data: { success: true, data: mockUpdatedEdition, error: null }
    })

    const { allEditions, changeStatus } = useCampEditions()
    allEditions.value = [existingEdition as never]

    await changeStatus('edition-1', 'Open')

    expect(allEditions.value[0].status).toBe('Open')
  })

  it('should return null and set error when API fails', async () => {
    vi.mocked(api.patch).mockRejectedValueOnce({
      response: { data: { error: { message: 'Transición no válida' } } }
    })

    const { error, changeStatus } = useCampEditions()
    const result = await changeStatus('edition-1', 'Open')

    expect(result).toBeNull()
    expect(error.value).toBe('Transición no válida')
  })

  it('sends force=true in request body when force param is true', async () => {
    vi.mocked(api.patch).mockResolvedValueOnce({
      data: { success: true, data: { ...mockUpdatedEdition, status: 'Open' }, error: null }
    })

    const { changeStatus } = useCampEditions()
    await changeStatus('edition-1', 'Open', true)

    expect(api.patch).toHaveBeenCalledWith(
      '/camps/editions/edition-1/status',
      { status: 'Open', force: true }
    )
  })

  it('omits force from request body when force param is false/undefined', async () => {
    vi.mocked(api.patch).mockResolvedValueOnce({
      data: { success: true, data: mockUpdatedEdition, error: null }
    })

    const { changeStatus } = useCampEditions()
    await changeStatus('edition-1', 'Open')

    expect(api.patch).toHaveBeenCalledWith(
      '/camps/editions/edition-1/status',
      { status: 'Open' }
    )
  })

  it('sends Draft status without force for normal Open→Draft rollback', async () => {
    vi.mocked(api.patch).mockResolvedValueOnce({
      data: { success: true, data: { ...mockUpdatedEdition, status: 'Draft' }, error: null }
    })

    const { changeStatus } = useCampEditions()
    await changeStatus('edition-1', 'Draft')

    expect(api.patch).toHaveBeenCalledWith(
      '/camps/editions/edition-1/status',
      { status: 'Draft' }
    )
  })
})

describe('useCampEditions - updateEdition', () => {
  beforeEach(() => vi.clearAllMocks())

  const mockUpdatedEdition = {
    id: 'edition-1',
    campId: 'camp-1',
    year: 2026,
    status: 'Draft',
    startDate: '2026-07-01',
    endDate: '2026-07-20',
    location: 'Montaña Norte',
    pricePerAdult: 500,
    pricePerChild: 350,
    pricePerBaby: 0,
    useCustomAgeRanges: false,
    maxCapacity: 100,
    isArchived: false,
    createdAt: '2026-01-01T00:00:00Z',
    updatedAt: '2026-01-15T00:00:00Z'
  }

  const updateRequest = {
    startDate: '2026-07-01',
    endDate: '2026-07-20',
    pricePerAdult: 500,
    pricePerChild: 350,
    pricePerBaby: 0,
    useCustomAgeRanges: false,
    maxCapacity: 100
  }

  it('should call api.put and return updated edition on success', async () => {
    vi.mocked(api.put).mockResolvedValueOnce({
      data: { success: true, data: mockUpdatedEdition, error: null }
    })

    const { updateEdition } = useCampEditions()
    const result = await updateEdition('edition-1', updateRequest)

    expect(result).toEqual(mockUpdatedEdition)
    expect(api.put).toHaveBeenCalledWith('/camps/editions/edition-1', updateRequest)
  })

  it('should update the allEditions array on success', async () => {
    const existingEdition = { ...mockUpdatedEdition, pricePerAdult: 450 }
    vi.mocked(api.put).mockResolvedValueOnce({
      data: { success: true, data: mockUpdatedEdition, error: null }
    })

    const { allEditions, updateEdition } = useCampEditions()
    allEditions.value = [existingEdition as never]

    await updateEdition('edition-1', updateRequest)

    expect(allEditions.value[0].pricePerAdult).toBe(500)
  })
})

describe('useCampEditions - fetchAllEditions', () => {
  beforeEach(() => vi.clearAllMocks())

  const mockEditionsList = [
    {
      id: 'edition-1', campId: 'camp-1', year: 2026, status: 'Open',
      startDate: '2026-07-01', endDate: '2026-07-15', location: 'Montaña Norte',
      pricePerAdult: 450, pricePerChild: 300, pricePerBaby: 0,
      useCustomAgeRanges: false, maxCapacity: 120, isArchived: false,
      createdAt: '2026-01-01T00:00:00Z', updatedAt: '2026-01-01T00:00:00Z'
    },
    {
      id: 'edition-2', campId: 'camp-1', year: 2025, status: 'Completed',
      startDate: '2025-07-01', endDate: '2025-07-15', location: 'Montaña Norte',
      pricePerAdult: 400, pricePerChild: 250, pricePerBaby: 0,
      useCustomAgeRanges: false, maxCapacity: 100, isArchived: false,
      createdAt: '2025-01-01T00:00:00Z', updatedAt: '2025-08-01T00:00:00Z'
    }
  ]

  it('should call GET /camps/editions with no query string when no filters', async () => {
    vi.mocked(api.get).mockResolvedValueOnce({
      data: { success: true, data: mockEditionsList, error: null }
    })

    const { fetchAllEditions } = useCampEditions()
    await fetchAllEditions()

    expect(api.get).toHaveBeenCalledWith('/camps/editions')
  })

  it('should include year filter in query string', async () => {
    vi.mocked(api.get).mockResolvedValueOnce({
      data: { success: true, data: mockEditionsList, error: null }
    })

    const { fetchAllEditions } = useCampEditions()
    await fetchAllEditions({ year: 2025 })

    expect(api.get).toHaveBeenCalledWith('/camps/editions?year=2025')
  })

  it('should include all filters in query string when multiple are provided', async () => {
    vi.mocked(api.get).mockResolvedValueOnce({
      data: { success: true, data: mockEditionsList, error: null }
    })

    const { fetchAllEditions } = useCampEditions()
    await fetchAllEditions({ year: 2026, status: 'Open', campId: 'camp-1' })

    const call = vi.mocked(api.get).mock.calls[0][0] as string
    expect(call).toContain('year=2026')
    expect(call).toContain('status=Open')
    expect(call).toContain('campId=camp-1')
  })

  it('should set allEditions from response data', async () => {
    vi.mocked(api.get).mockResolvedValueOnce({
      data: { success: true, data: mockEditionsList, error: null }
    })

    const { allEditions, fetchAllEditions } = useCampEditions()
    await fetchAllEditions()

    expect(allEditions.value).toEqual(mockEditionsList)
  })

  it('should set error and empty allEditions on API failure', async () => {
    vi.mocked(api.get).mockRejectedValueOnce({
      response: { data: { error: { message: 'Error del servidor' } } }
    })

    const { allEditions, error, fetchAllEditions } = useCampEditions()
    await fetchAllEditions()

    expect(allEditions.value).toEqual([])
    expect(error.value).toBe('Error del servidor')
  })

  it('should set loading to true during fetch and false after', async () => {
    let resolvePromise!: (value: unknown) => void
    vi.mocked(api.get).mockReturnValueOnce(
      new Promise((r) => { resolvePromise = r })
    )

    const { loading, fetchAllEditions } = useCampEditions()
    const promise = fetchAllEditions()
    expect(loading.value).toBe(true)
    resolvePromise({ data: { success: true, data: [], error: null } })
    await promise
    expect(loading.value).toBe(false)
  })
})
