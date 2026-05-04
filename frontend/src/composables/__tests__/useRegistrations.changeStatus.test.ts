import { describe, it, expect, vi, beforeEach } from 'vitest'
import { useRegistrations } from '../useRegistrations'
import { api } from '@/utils/api'

vi.mock('@/utils/api', () => ({
  api: { get: vi.fn(), post: vi.fn(), put: vi.fn(), patch: vi.fn(), delete: vi.fn() }
}))

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
  status: 'PartiallyPaid',
  draftTargetStatus: null,
  hasPendingUserAcknowledgement: false,
  statusHistory: [],
  notes: null,
  pricing: {
    members: [],
    baseTotalAmount: 450,
    extras: [],
    extrasAmount: 0,
    totalAmount: 450
  },
  payments: [],
  amountPaid: 100,
  amountRemaining: 350,
  createdAt: '2026-02-01T00:00:00Z',
  updatedAt: '2026-02-01T00:00:00Z'
}

describe('useRegistrations - changeStatus', () => {
  beforeEach(() => vi.clearAllMocks())

  it('should return RegistrationResponse and update registration ref on success', async () => {
    vi.mocked(api.patch).mockResolvedValueOnce({
      data: { success: true, data: mockRegistration, error: null }
    })

    const { registration, error, changeStatus } = useRegistrations()
    const result = await changeStatus('reg-1', {
      newStatus: 'PartiallyPaid',
      notes: 'Primer pago recibido',
      notifyUser: true
    })

    expect(result).toEqual(mockRegistration)
    expect(registration.value).toEqual(mockRegistration)
    expect(error.value).toBeNull()
    expect(api.patch).toHaveBeenCalledWith('/registrations/reg-1/status', {
      newStatus: 'PartiallyPaid',
      notes: 'Primer pago recibido',
      notifyUser: true
    })
  })

  it('should set error and return null on API failure', async () => {
    vi.mocked(api.patch).mockRejectedValueOnce({
      response: { data: { error: { message: 'Transición de estado no permitida' } } }
    })

    const { registration, error, changeStatus } = useRegistrations()
    const result = await changeStatus('reg-1', {
      newStatus: 'Confirmed',
      notes: 'Confirmada',
      notifyUser: false
    })

    expect(result).toBeNull()
    expect(registration.value).toBeNull()
    expect(error.value).toBe('Transición de estado no permitida')
  })

  it('should use fallback error message when API error has no message', async () => {
    vi.mocked(api.patch).mockRejectedValueOnce(new Error('network error'))

    const { error, changeStatus } = useRegistrations()
    await changeStatus('reg-1', { newStatus: 'Confirmed', notes: 'x', notifyUser: false })

    expect(error.value).toBe('Error al cambiar estado')
  })

  it('should set loading during the API call', async () => {
    let resolvePromise!: (value: unknown) => void
    vi.mocked(api.patch).mockReturnValueOnce(
      new Promise((r) => { resolvePromise = r })
    )

    const { loading, changeStatus } = useRegistrations()
    const promise = changeStatus('reg-1', { newStatus: 'Confirmed', notes: 'x', notifyUser: false })
    expect(loading.value).toBe(true)
    resolvePromise({ data: { success: true, data: mockRegistration, error: null } })
    await promise
    expect(loading.value).toBe(false)
  })
})

describe('useRegistrations - confirmChanges', () => {
  beforeEach(() => vi.clearAllMocks())

  it('should return RegistrationResponse and update registration ref on success', async () => {
    const confirmed = { ...mockRegistration, status: 'Confirmed', hasPendingUserAcknowledgement: false }
    vi.mocked(api.post).mockResolvedValueOnce({
      data: { success: true, data: confirmed, error: null }
    })

    const { registration, error, confirmChanges } = useRegistrations()
    const result = await confirmChanges('reg-1')

    expect(result).toEqual(confirmed)
    expect(registration.value).toEqual(confirmed)
    expect(error.value).toBeNull()
    expect(api.post).toHaveBeenCalledWith('/registrations/reg-1/confirm-changes')
  })

  it('should set error and return null on API failure', async () => {
    vi.mocked(api.post).mockRejectedValueOnce({
      response: { data: { error: { message: 'No hay cambios pendientes de confirmar' } } }
    })

    const { error, confirmChanges } = useRegistrations()
    const result = await confirmChanges('reg-1')

    expect(result).toBeNull()
    expect(error.value).toBe('No hay cambios pendientes de confirmar')
  })

  it('should use fallback error message when API error has no message', async () => {
    vi.mocked(api.post).mockRejectedValueOnce(new Error('network error'))

    const { error, confirmChanges } = useRegistrations()
    await confirmChanges('reg-1')

    expect(error.value).toBe('Error al confirmar cambios')
  })
})

describe('useRegistrations - adminUpdateRegistration', () => {
  beforeEach(() => vi.clearAllMocks())

  const adminRequest = {
    members: [{ familyMemberId: 'member-1', attendanceType: 'Full' as const }],
    extras: [],
    specialNeeds: null,
    campatesPreference: null,
    hasPet: false,
    notifyUser: true,
    draftTargetStatus: 'Confirmed' as const
  }

  it('should call PUT /registrations/{id}/admin with correct body and return response', async () => {
    vi.mocked(api.put).mockResolvedValueOnce({
      data: { success: true, data: mockRegistration, error: null }
    })

    const { registration, error, adminUpdateRegistration } = useRegistrations()
    const result = await adminUpdateRegistration('reg-1', adminRequest)

    expect(result).toEqual(mockRegistration)
    expect(registration.value).toEqual(mockRegistration)
    expect(error.value).toBeNull()
    expect(api.put).toHaveBeenCalledWith('/registrations/reg-1/admin', adminRequest)
  })

  it('should pass notifyUser: false when not notifying', async () => {
    vi.mocked(api.put).mockResolvedValueOnce({
      data: { success: true, data: mockRegistration, error: null }
    })

    const { adminUpdateRegistration } = useRegistrations()
    await adminUpdateRegistration('reg-1', { ...adminRequest, notifyUser: false, draftTargetStatus: null })

    expect(vi.mocked(api.put).mock.calls[0][1]).toMatchObject({
      notifyUser: false,
      draftTargetStatus: null
    })
  })

  it('should set error and return null on API failure', async () => {
    vi.mocked(api.put).mockRejectedValueOnce({
      response: { data: { error: { message: 'Inscripción no encontrada' } } }
    })

    const { error, adminUpdateRegistration } = useRegistrations()
    const result = await adminUpdateRegistration('reg-1', adminRequest)

    expect(result).toBeNull()
    expect(error.value).toBe('Inscripción no encontrada')
  })
})
