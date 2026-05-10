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
  status: 'Draft',
  draftTargetStatus: 'Confirmed',
  hasPendingUserAcknowledgement: true,
  familyNotifiedOfDraft: false,
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
  updatedAt: '2026-02-01T00:00:00Z',
  specialNeeds: null,
  campatesPreference: null,
  hasPet: false
}

describe('useRegistrations - notifyDraft', () => {
  beforeEach(() => vi.clearAllMocks())

  it('should return true and set familyNotifiedOfDraft to true on success', async () => {
    vi.mocked(api.post).mockResolvedValueOnce({ data: null })
    vi.mocked(api.get).mockResolvedValueOnce({
      data: { success: true, data: mockRegistration, error: null }
    })

    const { registration, getRegistrationById, notifyDraft } = useRegistrations()
    await getRegistrationById('reg-1')

    const result = await notifyDraft('reg-1')

    expect(result).toBe(true)
    expect(api.post).toHaveBeenCalledWith('/registrations/reg-1/notify-draft')
    expect(registration.value?.familyNotifiedOfDraft).toBe(true)
  })

  it('should return false and set error on API failure', async () => {
    vi.mocked(api.post).mockRejectedValueOnce({
      response: { data: { error: { message: 'La familia ya ha sido notificada.' } } }
    })

    const { error, notifyDraft } = useRegistrations()
    const result = await notifyDraft('reg-1')

    expect(result).toBe(false)
    expect(error.value).toBe('La familia ya ha sido notificada.')
  })

  it('should use fallback error message when API error has no message', async () => {
    vi.mocked(api.post).mockRejectedValueOnce(new Error('network error'))

    const { error, notifyDraft } = useRegistrations()
    await notifyDraft('reg-1')

    expect(error.value).toBe('Error al notificar a la familia')
  })

  it('should set loading true during request and false after', async () => {
    let resolvePromise!: (v: unknown) => void
    vi.mocked(api.post).mockReturnValueOnce(
      new Promise((res) => {
        resolvePromise = res
      })
    )

    const { loading, notifyDraft } = useRegistrations()
    const promise = notifyDraft('reg-1')
    expect(loading.value).toBe(true)
    resolvePromise({ data: null })
    await promise
    expect(loading.value).toBe(false)
  })

  it('should not update registration ref when id does not match', async () => {
    vi.mocked(api.post).mockResolvedValueOnce({ data: null })

    const { registration, notifyDraft } = useRegistrations()
    await notifyDraft('reg-other')

    expect(registration.value).toBeNull()
  })
})
