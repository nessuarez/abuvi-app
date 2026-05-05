import { describe, it, expect, vi, beforeEach } from 'vitest'
import { useRegistrationAccommodationTagging } from '../useRegistrationAccommodationTagging'
import { api } from '@/utils/api'

vi.mock('@/utils/api', () => ({
  api: { get: vi.fn(), post: vi.fn(), put: vi.fn(), patch: vi.fn(), delete: vi.fn() }
}))

const REG_ID = 'reg-abc'

const mockNeed = {
  featureId: 'feat-1',
  featureName: 'Habitación privada',
  featureCategory: 'Accessibility',
  taggedByUserId: 'user-1',
  createdAt: '2026-05-01T10:00:00Z',
}

const mockFriendLink = {
  linkedRegistrationId: 'reg-xyz',
  linkedFamilyName: 'Martínez Family',
  createdByUserId: 'user-1',
  createdAt: '2026-05-01T10:00:00Z',
}

describe('useRegistrationAccommodationTagging - fetchNeeds', () => {
  beforeEach(() => vi.clearAllMocks())

  it('populates needs on success', async () => {
    vi.mocked(api.get).mockResolvedValueOnce({
      data: { success: true, data: [mockNeed], error: null }
    })

    const { needs, fetchNeeds } = useRegistrationAccommodationTagging()
    await fetchNeeds(REG_ID)

    expect(api.get).toHaveBeenCalledWith(`/registrations/${REG_ID}/accommodation-needs`)
    expect(needs.value).toHaveLength(1)
    expect(needs.value[0].featureId).toBe('feat-1')
  })

  it('sets error on failure', async () => {
    vi.mocked(api.get).mockRejectedValueOnce({
      response: { data: { error: { message: 'Not found' } } }
    })

    const { error, fetchNeeds } = useRegistrationAccommodationTagging()
    await fetchNeeds(REG_ID)

    expect(error.value).toBe('Not found')
  })
})

describe('useRegistrationAccommodationTagging - updateNeeds', () => {
  beforeEach(() => vi.clearAllMocks())

  it('calls PUT and updates needs ref', async () => {
    const responseData = { registrationId: REG_ID, needs: [mockNeed] }
    vi.mocked(api.put).mockResolvedValueOnce({
      data: { success: true, data: responseData, error: null }
    })

    const { needs, updateNeeds } = useRegistrationAccommodationTagging()
    const result = await updateNeeds(REG_ID, ['feat-1'])

    expect(api.put).toHaveBeenCalledWith(
      `/registrations/${REG_ID}/accommodation-needs`,
      { featureIds: ['feat-1'] }
    )
    expect(result).toEqual(responseData)
    expect(needs.value).toHaveLength(1)
  })

  it('sets saveError and returns null on failure', async () => {
    vi.mocked(api.put).mockRejectedValueOnce({
      response: { data: { error: { message: 'Validation failed' } } }
    })

    const { saveError, updateNeeds } = useRegistrationAccommodationTagging()
    const result = await updateNeeds(REG_ID, ['invalid-id'])

    expect(result).toBeNull()
    expect(saveError.value).toBe('Validation failed')
  })
})

describe('useRegistrationAccommodationTagging - updateNotes', () => {
  beforeEach(() => vi.clearAllMocks())

  it('calls PATCH and updates internalNotes ref', async () => {
    const responseData = {
      registrationId: REG_ID,
      accommodationInternalNotes: 'Familia necesita planta baja',
      updatedAt: '2026-05-01T10:00:00Z',
    }
    vi.mocked(api.patch).mockResolvedValueOnce({
      data: { success: true, data: responseData, error: null }
    })

    const { internalNotes, updateNotes } = useRegistrationAccommodationTagging()
    const result = await updateNotes(REG_ID, 'Familia necesita planta baja')

    expect(api.patch).toHaveBeenCalledWith(
      `/registrations/${REG_ID}/accommodation-notes`,
      { accommodationInternalNotes: 'Familia necesita planta baja' }
    )
    expect(result).toEqual(responseData)
    expect(internalNotes.value).toBe('Familia necesita planta baja')
  })

  it('sets saveError and returns null on failure', async () => {
    vi.mocked(api.patch).mockRejectedValueOnce({
      response: { data: { error: { message: 'Too long' } } }
    })

    const { saveError, updateNotes } = useRegistrationAccommodationTagging()
    const result = await updateNotes(REG_ID, 'x'.repeat(5000))

    expect(result).toBeNull()
    expect(saveError.value).toBe('Too long')
  })
})

describe('useRegistrationAccommodationTagging - fetchFriendLinks', () => {
  beforeEach(() => vi.clearAllMocks())

  it('populates friendLinks on success', async () => {
    vi.mocked(api.get).mockResolvedValueOnce({
      data: { success: true, data: [mockFriendLink], error: null }
    })

    const { friendLinks, fetchFriendLinks } = useRegistrationAccommodationTagging()
    await fetchFriendLinks(REG_ID)

    expect(api.get).toHaveBeenCalledWith(`/registrations/${REG_ID}/friend-links`)
    expect(friendLinks.value).toHaveLength(1)
    expect(friendLinks.value[0].linkedFamilyName).toBe('Martínez Family')
  })

  it('sets error on failure', async () => {
    vi.mocked(api.get).mockRejectedValueOnce({
      response: { data: { error: { message: 'Server error' } } }
    })

    const { error, fetchFriendLinks } = useRegistrationAccommodationTagging()
    await fetchFriendLinks(REG_ID)

    expect(error.value).toBe('Server error')
  })
})

describe('useRegistrationAccommodationTagging - updateFriendLinks', () => {
  beforeEach(() => vi.clearAllMocks())

  it('calls PUT and updates friendLinks ref', async () => {
    const responseData = { registrationId: REG_ID, friendLinks: [mockFriendLink] }
    vi.mocked(api.put).mockResolvedValueOnce({
      data: { success: true, data: responseData, error: null }
    })

    const { friendLinks, updateFriendLinks } = useRegistrationAccommodationTagging()
    const result = await updateFriendLinks(REG_ID, ['reg-xyz'])

    expect(api.put).toHaveBeenCalledWith(
      `/registrations/${REG_ID}/friend-links`,
      { linkedRegistrationIds: ['reg-xyz'] }
    )
    expect(result).toEqual(responseData)
    expect(friendLinks.value).toHaveLength(1)
  })

  it('sets saveError and returns null on failure', async () => {
    vi.mocked(api.put).mockRejectedValueOnce({
      response: { data: { error: { message: 'SAME_EDITION_REQUIRED' } } }
    })

    const { saveError, updateFriendLinks } = useRegistrationAccommodationTagging()
    const result = await updateFriendLinks(REG_ID, ['other-edition-reg'])

    expect(result).toBeNull()
    expect(saveError.value).toBe('SAME_EDITION_REQUIRED')
  })
})
