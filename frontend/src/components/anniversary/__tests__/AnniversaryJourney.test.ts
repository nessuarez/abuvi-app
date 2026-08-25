import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest'
import { computed, ref } from 'vue'
import { mount, flushPromises } from '@vue/test-utils'
import PrimeVue from 'primevue/config'
import AnniversaryJourney from '../AnniversaryJourney.vue'
import type { CampHistoryEntry, CampHistoryVenue } from '@/types/camp-history'

const mockEntries = ref<CampHistoryEntry[]>([])
const mockLoading = ref(false)
const mockError = ref<string | null>(null)
const mockFetchHistory = vi.fn()

vi.mock('@/composables/useCampHistory', () => ({
  useCampHistory: () => {
    const venues = computed<CampHistoryVenue[]>(() => {
      const byCamp = new Map<string, CampHistoryVenue>()
      for (const entry of mockEntries.value) {
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
    })

    return {
      entries: mockEntries,
      venues,
      years: computed(() => mockEntries.value.map((e) => e.year).sort((a, b) => a - b)),
      loading: mockLoading,
      error: mockError,
      fetchHistory: mockFetchHistory,
      entryByYear: (year: number) => mockEntries.value.find((e) => e.year === year),
      venueByYear: (year: number) =>
        venues.value.find((v) => v.years.includes(year))
    }
  }
}))

const makeEntry = (overrides: Partial<CampHistoryEntry> = {}): CampHistoryEntry => ({
  year: 1983,
  campId: 'camp-1',
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

const stubs = {
  CampLocationMap: {
    name: 'CampLocationMap',
    props: ['locations', 'selectedId', 'heightClass'],
    emits: ['selectLocation', 'selectYear'],
    template: '<div class="map-stub" />'
  }
}

const mountJourney = () =>
  mount(AnniversaryJourney, { global: { plugins: [PrimeVue], stubs } })

/** PrimeVue renders the label inside the button, so match on text rather than an attribute. */
const buttonWithText = (wrapper: ReturnType<typeof mountJourney>, text: string) => {
  const button = wrapper.findAll('button').find((b) => b.text().includes(text))
  if (!button) throw new Error(`No button found with text: ${text}`)
  return button
}

describe('AnniversaryJourney', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    mockEntries.value = []
    mockLoading.value = false
    mockError.value = null
    // jsdom does not implement scrollIntoView; the child components call it on selection.
    Element.prototype.scrollIntoView = vi.fn()
  })

  afterEach(() => {
    vi.useRealTimers()
  })

  it('loads the history once on mount', () => {
    mountJourney()

    expect(mockFetchHistory).toHaveBeenCalledTimes(1)
  })

  it('shows the error state instead of an empty map when the fetch fails', () => {
    mockError.value = 'Boom'
    const wrapper = mountJourney()

    expect(wrapper.text()).toContain('No se pudo cargar el recorrido.')
    expect(wrapper.find('.map-stub').exists()).toBe(false)
  })

  it('starts with nothing selected so the whole history is visible at once', () => {
    mockEntries.value = [makeEntry({ year: 1976 }), makeEntry({ year: 1983 })]
    const wrapper = mountJourney()

    expect(wrapper.findComponent({ name: 'CampLocationMap' }).props('selectedId')).toBeUndefined()
    expect(wrapper.emitted('update:year')).toBeUndefined()
  })

  it('selects a year from the venue list and reports it upwards', async () => {
    mockEntries.value = [makeEntry({ year: 1983 }), makeEntry({ year: 2015, editionNumber: 2 })]
    const wrapper = mountJourney()

    await wrapper.findAll('button[aria-label^="Edición de"]')[1].trigger('click')

    expect(wrapper.emitted('update:year')?.[0]).toEqual([2015])
    expect(wrapper.findComponent({ name: 'CampLocationMap' }).props('selectedId')).toBe('camp-1')
  })

  it('selecting a venue on the map picks its most recent edition', async () => {
    mockEntries.value = [
      makeEntry({ year: 1983, totalEditionsAtVenue: 3 }),
      makeEntry({ year: 1993, totalEditionsAtVenue: 3 }),
      makeEntry({ year: 2003, totalEditionsAtVenue: 3 })
    ]
    const wrapper = mountJourney()

    await wrapper.findComponent({ name: 'CampLocationMap' }).vm.$emit('selectLocation', 'camp-1')

    expect(wrapper.emitted('update:year')?.[0]).toEqual([2003])
  })

  it('leaves venues without coordinates off the map but keeps them in the list', async () => {
    mockEntries.value = [
      makeEntry({ year: 1976, campId: 'camp-1', campName: 'Con coordenadas' }),
      makeEntry({
        year: 1980,
        campId: 'camp-2',
        campName: 'Sin coordenadas',
        latitude: null,
        longitude: null
      })
    ]
    const wrapper = mountJourney()
    await flushPromises()

    const locations = wrapper.findComponent({ name: 'CampLocationMap' }).props('locations') as {
      name: string
    }[]
    expect(locations).toHaveLength(1)
    expect(locations[0].name).toBe('Con coordenadas')
    expect(wrapper.text()).toContain('Sin coordenadas')
    expect(wrapper.text()).toContain('1 sin ubicar en el mapa')
  })

  it('passes the edition years and count through to the map', () => {
    mockEntries.value = [
      makeEntry({ year: 1983, totalEditionsAtVenue: 2 }),
      makeEntry({ year: 2015, totalEditionsAtVenue: 2 })
    ]
    const wrapper = mountJourney()

    const locations = wrapper.findComponent({ name: 'CampLocationMap' }).props('locations') as {
      editionYears: number[]
      editionCount: number
    }[]
    expect(locations[0].editionYears).toEqual([1983, 2015])
    expect(locations[0].editionCount).toBe(2)
  })

  describe('selected edition panel', () => {
    it('turns an empty year into a call to action naming the year and venue', async () => {
      mockEntries.value = [makeEntry({ year: 1987, campName: 'Los Palancares', photoCount: 0 })]
      const wrapper = mountJourney()

      await wrapper.find('button[aria-label^="Edición de"]').trigger('click')

      expect(wrapper.text()).toContain(
        'De 1987 en Los Palancares no conservamos nada todavía. ¿Tienes algo?'
      )
      expect(wrapper.text()).toContain('Comparte tu recuerdo')
    })

    it('shows the preview photos that came with the same request', async () => {
      mockEntries.value = [
        makeEntry({
          year: 2003,
          photoCount: 37,
          previewPhotos: [
            { id: 'p1', thumbnailUrl: 'https://example.com/1.webp', title: 'Llegada' },
            { id: 'p2', thumbnailUrl: 'https://example.com/2.webp', title: 'Comedor' }
          ]
        })
      ]
      const wrapper = mountJourney()

      await wrapper.find('button[aria-label^="Edición de"]').trigger('click')

      expect(wrapper.text()).toContain('37 recuerdos de este año.')
      expect(wrapper.findAll('img')).toHaveLength(2)
    })

    it('shows which visit to the venue this edition was', async () => {
      mockEntries.value = [
        makeEntry({ year: 2015, editionNumber: 4, totalEditionsAtVenue: 4, photoCount: 1 })
      ]
      const wrapper = mountJourney()

      await wrapper.find('button[aria-label^="Edición de"]').trigger('click')

      expect(wrapper.text()).toContain('edición 4 de 4 aquí')
    })
  })

  describe('presentation mode', () => {
    const threeYears = () => [
      makeEntry({ year: 1976, campId: 'camp-1' }),
      makeEntry({ year: 1983, campId: 'camp-2' }),
      makeEntry({ year: 1993, campId: 'camp-3' })
    ]

    it('walks the years and wraps around', async () => {
      vi.useFakeTimers()
      mockEntries.value = threeYears()
      const wrapper = mountJourney()

      await buttonWithText(wrapper, 'Recorrer los 50 años').trigger('click')
      expect(wrapper.emitted('update:year')?.at(-1)).toEqual([1976])

      vi.advanceTimersByTime(2000)
      await flushPromises()
      expect(wrapper.emitted('update:year')?.at(-1)).toEqual([1983])

      vi.advanceTimersByTime(4000)
      await flushPromises()
      expect(wrapper.emitted('update:year')?.at(-1)).toEqual([1976])
    })

    it('stops when the user picks a year themselves', async () => {
      vi.useFakeTimers()
      mockEntries.value = threeYears()
      const wrapper = mountJourney()

      await buttonWithText(wrapper, 'Recorrer los 50 años').trigger('click')
      await wrapper.findAll('button[aria-label^="Edición de"]')[2].trigger('click')
      const afterManualPick = wrapper.emitted('update:year')?.length

      vi.advanceTimersByTime(10000)
      await flushPromises()

      expect(wrapper.emitted('update:year')?.length).toBe(afterManualPick)
      expect(wrapper.text()).toContain('Recorrer los 50 años')
    })

    it('clears its interval on unmount so it cannot pan a destroyed map', async () => {
      vi.useFakeTimers()
      const clearIntervalSpy = vi.spyOn(globalThis, 'clearInterval')
      mockEntries.value = threeYears()
      const wrapper = mountJourney()

      await buttonWithText(wrapper, 'Recorrer los 50 años').trigger('click')
      wrapper.unmount()

      expect(clearIntervalSpy).toHaveBeenCalled()
    })
  })
})
