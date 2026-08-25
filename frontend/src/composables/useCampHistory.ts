import { computed, ref } from 'vue'
import { api } from '@/utils/api'
import { useCampEditions } from '@/composables/useCampEditions'
import type { ApiResponse } from '@/types/api'
import type {
  CampEditionOption,
  CampHistoryEntry,
  CampHistoryVenue
} from '@/types/camp-history'

type ApiErrorShape = { response?: { data?: { error?: { message?: string } }; status?: number } }

const FETCH_ERROR = 'No se pudo cargar el histórico de campamentos'

/**
 * Reads the association's camp history for the 50th anniversary section.
 *
 * The endpoint returns every completed edition in one call (50 rows today), which is what
 * makes the client-side grouping below legitimate: there is no pagination to reconcile.
 *
 * Assumes one edition per year — true for 1976-2025, and what the year-keyed lookups rest on.
 */
export function useCampHistory() {
  const entries = ref<CampHistoryEntry[]>([])
  const loading = ref(false)
  const error = ref<string | null>(null)

  // Its own instance, so a failure fetching the current edition cannot leak into the
  // history's own error state. See fetchEditionOptions.
  const { currentCampEdition, fetchCurrentCampEdition } = useCampEditions()

  const fetchHistory = async (): Promise<void> => {
    loading.value = true
    error.value = null
    try {
      const response = await api.get<ApiResponse<CampHistoryEntry[]>>('/camps/history')

      if (response.data.success && response.data.data) {
        entries.value = response.data.data
      } else {
        error.value = response.data.error?.message ?? FETCH_ERROR
      }
    } catch (err: unknown) {
      error.value = (err as ApiErrorShape)?.response?.data?.error?.message ?? FETCH_ERROR
      console.error('Failed to fetch camp history:', err)
    } finally {
      loading.value = false
    }
  }

  /** Editions grouped by venue, ordered by the year each venue was first used. */
  const venues = computed<CampHistoryVenue[]>(() => {
    const byCamp = new Map<string, CampHistoryVenue>()

    for (const entry of entries.value) {
      const existing = byCamp.get(entry.campId)
      if (existing) {
        existing.years.push(entry.year)
        existing.photoCount += entry.photoCount
        continue
      }

      byCamp.set(entry.campId, {
        campId: entry.campId,
        campName: entry.campName,
        location: entry.location,
        latitude: entry.latitude,
        longitude: entry.longitude,
        years: [entry.year],
        totalEditionsAtVenue: entry.totalEditionsAtVenue,
        photoCount: entry.photoCount
      })
    }

    return [...byCamp.values()]
      .map((venue) => ({ ...venue, years: [...venue.years].sort((a, b) => a - b) }))
      .sort((a, b) => a.years[0] - b.years[0])
  })

  /** Every year with an edition, ascending. Drives the year strip and presentation mode. */
  const years = computed<number[]>(() =>
    entries.value.map((entry) => entry.year).sort((a, b) => a - b)
  )

  // Lookups are hit on every selection change and on every presentation-mode tick,
  // so they go through a map rather than a linear scan.
  const entriesByYear = computed(
    () => new Map(entries.value.map((entry) => [entry.year, entry] as const))
  )

  const venuesByCampId = computed(
    () => new Map(venues.value.map((venue) => [venue.campId, venue] as const))
  )

  const entryByYear = (year: number): CampHistoryEntry | undefined => entriesByYear.value.get(year)

  const venueByYear = (year: number): CampHistoryVenue | undefined => {
    const entry = entryByYear(year)
    return entry ? venuesByCampId.value.get(entry.campId) : undefined
  }

  /**
   * Editions somebody can anchor a contribution to: the whole history, plus the camp that
   * is running now — which the history endpoint deliberately excludes, since it only
   * returns completed editions.
   */
  const editionOptions = computed<CampEditionOption[]>(() => {
    // Most recent first: whoever is standing at a camp wants this year, not 1976.
    const options: CampEditionOption[] = [...entries.value]
      .sort((a, b) => b.year - a.year)
      .map((entry) => ({
        year: entry.year,
        label: `${entry.year} — ${entry.campName}`,
        campName: entry.campName,
        isCurrent: false
      }))

    const current = currentCampEdition.value
    if (current && !options.some((option) => option.year === current.year)) {
      options.unshift({
        year: current.year,
        label: `${current.year} — ${current.campName} (este campamento)`,
        campName: current.campName,
        isCurrent: true
      })
    }

    return options
  })

  /**
   * Loads both sources. The current edition is optional: there may be no open camp, and
   * today there is none, so its absence must never empty the list or raise an error.
   */
  const fetchEditionOptions = async (): Promise<void> => {
    await Promise.all([
      fetchHistory(),
      fetchCurrentCampEdition().catch(() => {
        /* no current camp is a normal state, not a failure */
      })
    ])
  }

  return {
    entries,
    venues,
    years,
    loading,
    error,
    fetchHistory,
    entryByYear,
    venueByYear,
    editionOptions,
    fetchEditionOptions
  }
}
