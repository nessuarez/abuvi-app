import { describe, it, expect, vi, beforeEach } from 'vitest'
import { useRegistrations } from '../useRegistrations'
import { api } from '@/utils/api'

vi.mock('@/utils/api', () => ({
  api: { get: vi.fn(), post: vi.fn(), put: vi.fn(), patch: vi.fn(), delete: vi.fn() }
}))

const mockListItem = {
  id: 'reg-1',
  familyUnit: { id: 'fu-1', name: 'Familia García', representativeUserId: 'user-1' },
  campEdition: {
    id: 'edition-1',
    campName: 'Campamento ABUVI',
    year: 2026,
    startDate: '2026-07-01',
    endDate: '2026-07-15',
    location: 'Montaña Norte',
    duration: 14
  },
  status: 'Pending',
  totalAmount: 450,
  amountPaid: 0,
  amountRemaining: 450,
  createdAt: '2026-02-01T00:00:00Z'
}

const mockRegistration = {
  id: 'reg-1',
  familyUnit: { id: 'fu-1', name: 'Familia García', representativeUserId: 'user-1' },
  campEdition: {
    id: 'edition-1',
    campName: 'Campamento ABUVI',
    year: 2026,
    startDate: '2026-07-01',
    endDate: '2026-07-15',
    location: 'Montaña Norte',
    duration: 14
  },
  status: 'Pending',
  notes: null,
  pricing: {
    members: [
      { familyMemberId: 'member-1', fullName: 'Juan García', ageAtCamp: 35, ageCategory: 'Adult', individualAmount: 450 }
    ],
    baseTotalAmount: 450,
    extras: [],
    extrasAmount: 0,
    totalAmount: 450
  },
  payments: [],
  amountPaid: 0,
  amountRemaining: 450,
  createdAt: '2026-02-01T00:00:00Z',
  updatedAt: '2026-02-01T00:00:00Z'
}

describe('useRegistrations - fetchMyRegistrations', () => {
  beforeEach(() => vi.clearAllMocks())

  it('should load registrations successfully', async () => {
    vi.mocked(api.get).mockResolvedValueOnce({
      data: { success: true, data: [mockListItem], error: null }
    })

    const { registrations, loading, error, fetchMyRegistrations } = useRegistrations()
    await fetchMyRegistrations()

    expect(registrations.value).toHaveLength(1)
    expect(registrations.value[0].id).toBe('reg-1')
    expect(registrations.value[0].totalAmount).toBe(450)
    expect(loading.value).toBe(false)
    expect(error.value).toBeNull()
    expect(api.get).toHaveBeenCalledWith('/registrations')
  })

  it('should set error message when fetch fails', async () => {
    vi.mocked(api.get).mockRejectedValueOnce({
      response: { data: { error: { message: 'Error del servidor' } } }
    })

    const { registrations, error, fetchMyRegistrations } = useRegistrations()
    await fetchMyRegistrations()

    expect(error.value).toBe('Error del servidor')
    expect(registrations.value).toEqual([])
  })

  it('should set loading to true during fetch and false after', async () => {
    let resolvePromise!: (value: unknown) => void
    vi.mocked(api.get).mockReturnValueOnce(
      new Promise((r) => { resolvePromise = r })
    )

    const { loading, fetchMyRegistrations } = useRegistrations()
    const promise = fetchMyRegistrations()
    expect(loading.value).toBe(true)
    resolvePromise({ data: { success: true, data: [], error: null } })
    await promise
    expect(loading.value).toBe(false)
  })
})

describe('useRegistrations - getRegistrationById', () => {
  beforeEach(() => vi.clearAllMocks())

  it('should return registration with full pricing breakdown', async () => {
    vi.mocked(api.get).mockResolvedValueOnce({
      data: { success: true, data: mockRegistration, error: null }
    })

    const { registration, error, getRegistrationById } = useRegistrations()
    const result = await getRegistrationById('reg-1')

    expect(result).toEqual(mockRegistration)
    expect(registration.value).toEqual(mockRegistration)
    expect(error.value).toBeNull()
    expect(api.get).toHaveBeenCalledWith('/registrations/reg-1')
  })

  it('should return null when registration not found', async () => {
    vi.mocked(api.get).mockRejectedValueOnce({
      response: { status: 404, data: { error: { message: 'No encontrado' } } }
    })

    const { registration, error, getRegistrationById } = useRegistrations()
    const result = await getRegistrationById('nonexistent')

    expect(result).toBeNull()
    expect(registration.value).toBeNull()
    expect(error.value).toBe('No encontrado')
  })
})

describe('useRegistrations - createRegistration', () => {
  beforeEach(() => vi.clearAllMocks())

  const createRequest = {
    campEditionId: 'edition-1',
    familyUnitId: 'fu-1',
    memberIds: ['member-1'],
    notes: null
  }

  it('should create registration and return response', async () => {
    vi.mocked(api.post).mockResolvedValueOnce({
      data: { success: true, data: mockRegistration, error: null }
    })

    const { registrations, error, createRegistration } = useRegistrations()
    const result = await createRegistration(createRequest)

    expect(result).toEqual(mockRegistration)
    expect(registrations.value).toHaveLength(1)
    expect(registrations.value[0].id).toBe('reg-1')
    expect(registrations.value[0].totalAmount).toBe(450)
    expect(error.value).toBeNull()
    expect(api.post).toHaveBeenCalledWith('/registrations', createRequest)
  })

  it('should set error and return null when API returns CAMP_FULL error', async () => {
    vi.mocked(api.post).mockRejectedValueOnce({
      response: { data: { error: { message: 'El campamento ha alcanzado su capacidad máxima', code: 'CAMP_FULL' } } }
    })

    const { registrations, error, createRegistration } = useRegistrations()
    const result = await createRegistration(createRequest)

    expect(result).toBeNull()
    expect(registrations.value).toHaveLength(0)
    expect(error.value).toBe('El campamento ha alcanzado su capacidad máxima')
  })

  it('should set error and return null when edition is not open', async () => {
    vi.mocked(api.post).mockRejectedValueOnce({
      response: { data: { error: { message: 'Esta edición no está disponible para inscripciones', code: 'EDITION_NOT_OPEN' } } }
    })

    const { error, createRegistration } = useRegistrations()
    const result = await createRegistration(createRequest)

    expect(result).toBeNull()
    expect(error.value).toBe('Esta edición no está disponible para inscripciones')
  })
})

describe('useRegistrations - setExtras', () => {
  beforeEach(() => vi.clearAllMocks())

  const extrasRequest = {
    extras: [{ campEditionExtraId: 'extra-1', quantity: 2 }]
  }

  const updatedRegistration = {
    ...mockRegistration,
    pricing: {
      ...mockRegistration.pricing,
      extras: [{
        campEditionExtraId: 'extra-1',
        name: 'Camiseta',
        unitPrice: 15,
        pricingType: 'PerPerson',
        pricingPeriod: 'OneTime',
        quantity: 2,
        campDurationDays: null,
        calculation: '15 × 2 personas = 30 €',
        totalAmount: 30
      }],
      extrasAmount: 30,
      totalAmount: 480
    }
  }

  it('should set extras and return updated registration', async () => {
    vi.mocked(api.post).mockResolvedValueOnce({
      data: { success: true, data: updatedRegistration, error: null }
    })

    const { setExtras, error } = useRegistrations()
    const result = await setExtras('reg-1', extrasRequest)

    expect(result).toEqual(updatedRegistration)
    expect(error.value).toBeNull()
    expect(api.post).toHaveBeenCalledWith('/registrations/reg-1/extras', extrasRequest)
  })

  it('should update registrations list and registration ref on success', async () => {
    vi.mocked(api.post).mockResolvedValueOnce({
      data: { success: true, data: updatedRegistration, error: null }
    })

    const { registrations, registration, setExtras } = useRegistrations()
    registrations.value = [mockListItem as never]
    registration.value = mockRegistration as never

    await setExtras('reg-1', extrasRequest)

    expect(registrations.value[0].totalAmount).toBe(480)
    expect(registration.value?.pricing.extrasAmount).toBe(30)
  })

  it('should set error when extra does not belong to edition', async () => {
    vi.mocked(api.post).mockRejectedValueOnce({
      response: { data: { error: { message: 'Algunos extras no pertenecen a esta edición', code: 'EXTRA_NOT_IN_EDITION' } } }
    })

    const { error, setExtras } = useRegistrations()
    const result = await setExtras('reg-1', extrasRequest)

    expect(result).toBeNull()
    expect(error.value).toBe('Algunos extras no pertenecen a esta edición')
  })
})

describe('useRegistrations - cancelRegistration', () => {
  beforeEach(() => vi.clearAllMocks())

  it('should return true when cancellation succeeds', async () => {
    vi.mocked(api.post).mockResolvedValueOnce({ status: 204, data: null })

    const { registration, cancelRegistration } = useRegistrations()
    registration.value = { ...mockRegistration, status: 'Pending' } as never

    const result = await cancelRegistration('reg-1')

    expect(result).toBe(true)
    expect(registration.value?.status).toBe('Cancelled')
    expect(api.post).toHaveBeenCalledWith('/registrations/reg-1/cancel')
  })

  it('should return false and set error when registration not cancellable', async () => {
    vi.mocked(api.post).mockRejectedValueOnce({
      response: { data: { error: { message: 'La inscripción no se puede modificar en su estado actual', code: 'REGISTRATION_NOT_EDITABLE' } } }
    })

    const { error, cancelRegistration } = useRegistrations()
    const result = await cancelRegistration('reg-1')

    expect(result).toBe(false)
    expect(error.value).toBe('La inscripción no se puede modificar en su estado actual')
  })
})
