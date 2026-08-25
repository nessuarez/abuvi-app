import { describe, it, expect, vi, beforeEach } from 'vitest'
import { useCampHistory } from '@/composables/useCampHistory'
import { api } from '@/utils/api'
import type { CampHistoryEntry } from '@/types/camp-history'

vi.mock('@/utils/api', () => ({
  api: {
    get: vi.fn(),
    post: vi.fn(),
    put: vi.fn(),
    patch: vi.fn(),
    delete: vi.fn()
  }
}))

const ESPINOSA = '11111111-1111-1111-1111-111111111111'
const PALANCARES = '22222222-2222-2222-2222-222222222222'

const makeEntry = (overrides: Partial<CampHistoryEntry> = {}): CampHistoryEntry => ({
  year: 1983,
  campId: ESPINOSA,
  campName: 'Espinosa de los Monteros',
  location: 'Burgos',
  latitude: 43.077348,
  longitude: -3.552172,
  editionNumber: 1,
  totalEditionsAtVenue: 1,
  photoCount: 0,
  previewPhotos: [],
  ...overrides
})

const ok = (data: CampHistoryEntry[]) => ({ data: { success: true, data, error: null } })

describe('useCampHistory', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    vi.spyOn(console, 'error').mockImplementation(() => {})
  })

  describe('fetchHistory', () => {
    it('calls the history endpoint once and stores the rows in the order received', async () => {
      const rows = [makeEntry({ year: 1976 }), makeEntry({ year: 1983 })]
      vi.mocked(api.get).mockResolvedValue(ok(rows))

      const { entries, fetchHistory, loading } = useCampHistory()
      await fetchHistory()

      expect(api.get).toHaveBeenCalledTimes(1)
      expect(api.get).toHaveBeenCalledWith('/camps/history')
      expect(entries.value.map((e) => e.year)).toEqual([1976, 1983])
      expect(loading.value).toBe(false)
    })

    it('sets error and leaves entries empty when the API reports failure', async () => {
      vi.mocked(api.get).mockResolvedValue({
        data: { success: false, data: null, error: { message: 'Boom', code: 'ERR' } }
      })

      const { entries, error, fetchHistory } = useCampHistory()
      await fetchHistory()

      expect(error.value).toBe('Boom')
      expect(entries.value).toEqual([])
    })

    it('sets a fallback error message and clears loading when the request throws', async () => {
      vi.mocked(api.get).mockRejectedValue(new Error('network down'))

      const { error, fetchHistory, loading } = useCampHistory()
      await fetchHistory()

      expect(error.value).toBe('No se pudo cargar el histórico de campamentos')
      expect(loading.value).toBe(false)
    })

    it('prefers the API error message over the fallback when the request throws', async () => {
      vi.mocked(api.get).mockRejectedValue({
        response: { data: { error: { message: 'Sin permisos' } }, status: 403 }
      })

      const { error, fetchHistory } = useCampHistory()
      await fetchHistory()

      expect(error.value).toBe('Sin permisos')
    })
  })

  describe('venues', () => {
    it('groups editions by camp, sorting years ascending', async () => {
      vi.mocked(api.get).mockResolvedValue(
        ok([
          makeEntry({ year: 2003, editionNumber: 3, totalEditionsAtVenue: 4 }),
          makeEntry({ year: 1983, editionNumber: 1, totalEditionsAtVenue: 4 }),
          makeEntry({ year: 2015, editionNumber: 4, totalEditionsAtVenue: 4 }),
          makeEntry({ year: 1993, editionNumber: 2, totalEditionsAtVenue: 4 })
        ])
      )

      const { venues, fetchHistory } = useCampHistory()
      await fetchHistory()

      expect(venues.value).toHaveLength(1)
      expect(venues.value[0].years).toEqual([1983, 1993, 2003, 2015])
    })

    it('produces as many years as the server-side totalEditionsAtVenue', async () => {
      vi.mocked(api.get).mockResolvedValue(
        ok([
          makeEntry({ year: 1983, totalEditionsAtVenue: 2 }),
          makeEntry({ year: 1993, totalEditionsAtVenue: 2 })
        ])
      )

      const { venues, fetchHistory } = useCampHistory()
      await fetchHistory()

      expect(venues.value[0].years).toHaveLength(venues.value[0].totalEditionsAtVenue)
    })

    it('orders venues by the year each was first used', async () => {
      vi.mocked(api.get).mockResolvedValue(
        ok([
          makeEntry({ year: 2015, campId: ESPINOSA, campName: 'Espinosa de los Monteros' }),
          makeEntry({ year: 1987, campId: PALANCARES, campName: 'Los Palancares' })
        ])
      )

      const { venues, fetchHistory } = useCampHistory()
      await fetchHistory()

      expect(venues.value.map((v) => v.campName)).toEqual([
        'Los Palancares',
        'Espinosa de los Monteros'
      ])
    })

    it('sums photoCount across a venue editions', async () => {
      vi.mocked(api.get).mockResolvedValue(
        ok([
          makeEntry({ year: 1983, photoCount: 12 }),
          makeEntry({ year: 1993, photoCount: 0 }),
          makeEntry({ year: 2003, photoCount: 25 })
        ])
      )

      const { venues, fetchHistory } = useCampHistory()
      await fetchHistory()

      expect(venues.value[0].photoCount).toBe(37)
    })

    it('keeps venues without coordinates instead of dropping them', async () => {
      vi.mocked(api.get).mockResolvedValue(
        ok([makeEntry({ year: 1976, latitude: null, longitude: null })])
      )

      const { venues, fetchHistory } = useCampHistory()
      await fetchHistory()

      expect(venues.value).toHaveLength(1)
      expect(venues.value[0].latitude).toBeNull()
    })

    it('is empty before any fetch', () => {
      const { venues, years } = useCampHistory()

      expect(venues.value).toEqual([])
      expect(years.value).toEqual([])
    })
  })

  describe('years', () => {
    it('returns every edition year ascending', async () => {
      vi.mocked(api.get).mockResolvedValue(
        ok([
          makeEntry({ year: 2015 }),
          makeEntry({ year: 1976, campId: PALANCARES }),
          makeEntry({ year: 1993 })
        ])
      )

      const { years, fetchHistory } = useCampHistory()
      await fetchHistory()

      expect(years.value).toEqual([1976, 1993, 2015])
    })
  })

  describe('lookups', () => {
    it('finds the entry and the venue for a known year', async () => {
      vi.mocked(api.get).mockResolvedValue(
        ok([
          makeEntry({ year: 1983, editionNumber: 1, totalEditionsAtVenue: 2 }),
          makeEntry({ year: 2015, editionNumber: 2, totalEditionsAtVenue: 2 })
        ])
      )

      const { entryByYear, venueByYear, fetchHistory } = useCampHistory()
      await fetchHistory()

      expect(entryByYear(2015)?.editionNumber).toBe(2)
      expect(venueByYear(2015)?.years).toEqual([1983, 2015])
    })

    it('returns undefined for a year with no edition', async () => {
      vi.mocked(api.get).mockResolvedValue(ok([makeEntry({ year: 1983 })]))

      const { entryByYear, venueByYear, fetchHistory } = useCampHistory()
      await fetchHistory()

      expect(entryByYear(1999)).toBeUndefined()
      expect(venueByYear(1999)).toBeUndefined()
    })
  })
})
