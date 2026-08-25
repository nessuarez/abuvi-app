import { describe, it, expect, vi, beforeEach } from 'vitest'
import { mount } from '@vue/test-utils'
import { nextTick } from 'vue'

// Use vi.hoisted to avoid initialization order issues with vi.mock
const mockBindPopup = vi.hoisted(() => vi.fn().mockReturnThis())
const mockOn = vi.hoisted(() => vi.fn().mockReturnThis())
const mockOpenPopup = vi.hoisted(() => vi.fn())
const mockFitBounds = vi.hoisted(() => vi.fn())
const mockRemove = vi.hoisted(() => vi.fn())
const mockPanTo = vi.hoisted(() => vi.fn())
const mockInvalidateSize = vi.hoisted(() => vi.fn())
const mockDivIcon = vi.hoisted(() => vi.fn((options: unknown) => ({ options })))
const mockMarkerFactory = vi.hoisted(() => vi.fn())

// Markers created during a mount, in order, so tests can inspect an individual one.
const createdMarkers = vi.hoisted(() => [] as Record<string, unknown>[])

const makeMarker = vi.hoisted(() => () => {
  const element = document.createElement('div')
  const marker = {
    bindPopup: mockBindPopup,
    on: mockOn,
    remove: vi.fn(),
    openPopup: mockOpenPopup,
    getElement: () => element,
    getLatLng: () => ({ lat: 40, lng: -3 })
  }
  createdMarkers.push(marker)
  return marker
})

vi.mock('leaflet', () => ({
  default: {
    map: vi.fn().mockReturnValue({
      setView: vi.fn().mockReturnValue({
        fitBounds: mockFitBounds,
        remove: mockRemove,
        panTo: mockPanTo,
        invalidateSize: mockInvalidateSize
      }),
      fitBounds: mockFitBounds,
      remove: mockRemove,
      panTo: mockPanTo,
      invalidateSize: mockInvalidateSize
    }),
    tileLayer: vi.fn().mockReturnValue({ addTo: vi.fn() }),
    marker: vi.fn((...args: unknown[]) => {
      mockMarkerFactory(...args)
      const marker = makeMarker()
      return { addTo: () => marker, remove: marker.remove }
    }),
    divIcon: mockDivIcon,
    latLngBounds: vi.fn().mockReturnValue({}),
    Icon: { Default: { prototype: {}, mergeOptions: vi.fn() } }
  }
}))

vi.mock('leaflet/dist/leaflet.css', () => ({}))

// Import after mocks
import CampLocationMap from '@/components/camps/CampLocationMap.vue'

/** The popup is a DOM node, so venue names can never inject markup. */
const popupNode = (call = 0) => mockBindPopup.mock.calls[call][0] as HTMLElement

describe('CampLocationMap', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    createdMarkers.length = 0
  })

  it('has 500px height class on the map container', () => {
    const wrapper = mount(CampLocationMap, {
      props: {
        locations: [{ latitude: 40.0, longitude: -3.0, name: 'Test' }],
      },
    })
    const container = wrapper.find('[class*="h-\\[500px\\]"]')
    // Also check via the element's className directly
    expect(wrapper.find('div').classes()).toContain('h-[500px]')
  })

  it('popup includes location when provided', () => {
    mount(CampLocationMap, {
      props: {
        locations: [
          { latitude: 40.0, longitude: -3.0, name: 'Camp A', location: 'Madrid, Spain' },
        ],
      },
    })

    expect(mockBindPopup).toHaveBeenCalled()
    expect(popupNode().textContent).toContain('Madrid, Spain')
  })

  it('popup includes lastEditionYear when provided', () => {
    mount(CampLocationMap, {
      props: {
        locations: [
          { latitude: 40.0, longitude: -3.0, name: 'Camp A', lastEditionYear: 2025 },
        ],
      },
    })

    expect(mockBindPopup).toHaveBeenCalled()
    expect(popupNode().textContent).toContain('Última edición: 2025')
  })

  it('popup gracefully omits optional fields when not provided', () => {
    mount(CampLocationMap, {
      props: {
        locations: [
          { latitude: 40.0, longitude: -3.0, name: 'Camp B' },
        ],
      },
    })

    expect(mockBindPopup).toHaveBeenCalled()
    const text = popupNode().textContent ?? ''
    expect(text).toContain('Camp B')
    expect(text).not.toContain('Última edición')
    expect(text).not.toContain('undefined')
  })

  describe('backwards compatibility', () => {
    it('uses the default Leaflet pin when no editionCount is given', () => {
      mount(CampLocationMap, {
        props: { locations: [{ latitude: 40.0, longitude: -3.0, name: 'Camp A' }] },
      })

      expect(mockDivIcon).not.toHaveBeenCalled()
      expect(mockMarkerFactory).toHaveBeenCalledWith([40.0, -3.0], undefined)
    })

    it('emits the name as the identifier when no id is given', () => {
      const wrapper = mount(CampLocationMap, {
        props: { locations: [{ latitude: 40.0, longitude: -3.0, name: 'Camp A' }] },
      })

      const onClick = mockOn.mock.calls[0][1] as () => void
      onClick()

      expect(wrapper.emitted('selectLocation')?.[0]).toEqual(['Camp A'])
    })
  })

  describe('anniversary extensions', () => {
    it('emits the id as the identifier when one is given', () => {
      const wrapper = mount(CampLocationMap, {
        props: {
          locations: [{ latitude: 40.0, longitude: -3.0, name: 'Camp A', id: 'camp-1' }],
        },
      })

      const onClick = mockOn.mock.calls[0][1] as () => void
      onClick()

      expect(wrapper.emitted('selectLocation')?.[0]).toEqual(['camp-1'])
    })

    it('renders a bigger numbered icon for venues with several editions', () => {
      mount(CampLocationMap, {
        props: {
          locations: [
            { latitude: 40.0, longitude: -3.0, name: 'Espinosa', editionCount: 4 },
          ],
        },
      })

      expect(mockDivIcon).toHaveBeenCalledTimes(1)
      const options = mockDivIcon.mock.calls[0][0] as { iconSize: number[]; html: string }
      expect(options.iconSize).toEqual([52, 52])
      expect(options.html).toContain('>4<')
    })

    it('scales the icon with the edition count', () => {
      mount(CampLocationMap, {
        props: {
          locations: [{ latitude: 40.0, longitude: -3.0, name: 'Once', editionCount: 1 }],
        },
      })

      const options = mockDivIcon.mock.calls[0][0] as { iconSize: number[]; html: string }
      expect(options.iconSize).toEqual([34, 34])
      // A single edition carries no number: the count only matters where it repeats.
      expect(options.html).toContain('><')
    })

    it('renders a clickable chip per edition year and emits the chosen one', () => {
      const wrapper = mount(CampLocationMap, {
        props: {
          locations: [
            {
              latitude: 40.0,
              longitude: -3.0,
              name: 'Espinosa',
              editionYears: [1983, 1993, 2003, 2015],
            },
          ],
        },
      })

      const chips = popupNode().querySelectorAll('button')
      expect(chips).toHaveLength(4)
      expect(chips[3].textContent).toBe('2015')
      expect(chips[3].getAttribute('aria-label')).toBe('Edición de 2015 en Espinosa')

      chips[2].dispatchEvent(new Event('click'))
      expect(wrapper.emitted('selectYear')?.[0]).toEqual([2003])
    })

    it('centres the map and opens the popup when selectedId changes', async () => {
      const wrapper = mount(CampLocationMap, {
        props: {
          locations: [
            { latitude: 40.0, longitude: -3.0, name: 'Camp A', id: 'camp-1' },
            { latitude: 41.0, longitude: -4.0, name: 'Camp B', id: 'camp-2' },
          ],
        },
      })

      await wrapper.setProps({ selectedId: 'camp-2' })
      await nextTick()

      expect(mockPanTo).toHaveBeenCalled()
      expect(mockOpenPopup).toHaveBeenCalledTimes(1)
      expect(createdMarkers[1].getElement).toBeDefined()
    })

    it('marks only the selected marker as selected', async () => {
      const wrapper = mount(CampLocationMap, {
        props: {
          locations: [
            { latitude: 40.0, longitude: -3.0, name: 'Camp A', id: 'camp-1' },
            { latitude: 41.0, longitude: -4.0, name: 'Camp B', id: 'camp-2' },
          ],
        },
      })

      await wrapper.setProps({ selectedId: 'camp-2' })
      await nextTick()

      const first = (createdMarkers[0].getElement as () => HTMLElement)()
      const second = (createdMarkers[1].getElement as () => HTMLElement)()
      expect(first.classList.contains('camp-marker--selected')).toBe(false)
      expect(second.classList.contains('camp-marker--selected')).toBe(true)
    })

    it('does not move the map when the selection is cleared', async () => {
      const wrapper = mount(CampLocationMap, {
        props: {
          locations: [{ latitude: 40.0, longitude: -3.0, name: 'Camp A', id: 'camp-1' }],
          selectedId: 'camp-1',
        },
      })
      mockPanTo.mockClear()

      await wrapper.setProps({ selectedId: undefined })
      await nextTick()

      expect(mockPanTo).not.toHaveBeenCalled()
    })

    it('applies a custom height class', () => {
      const wrapper = mount(CampLocationMap, {
        props: {
          locations: [{ latitude: 40.0, longitude: -3.0, name: 'Camp A' }],
          heightClass: 'h-[55vh] lg:h-[600px]',
        },
      })

      const classes = wrapper.find('div').classes()
      expect(classes).toContain('h-[55vh]')
      expect(classes).not.toContain('h-[500px]')
    })

    it('recalculates its size when the viewport changes, and stops once unmounted', async () => {
      const wrapper = mount(CampLocationMap, {
        props: { locations: [{ latitude: 40.0, longitude: -3.0, name: 'Camp A' }] },
      })
      await nextTick()
      await nextTick()
      // Earlier tests leave their components mounted, so count the delta this one adds
      // rather than the absolute number of calls.
      mockInvalidateSize.mockClear()
      window.dispatchEvent(new Event('resize'))
      const whileMounted = mockInvalidateSize.mock.calls.length

      wrapper.unmount()
      mockInvalidateSize.mockClear()
      window.dispatchEvent(new Event('resize'))
      const afterUnmount = mockInvalidateSize.mock.calls.length

      expect(whileMounted - afterUnmount).toBe(1)
    })

    it('recalculates its size after mount so a grid-sized container is not grey', async () => {
      mount(CampLocationMap, {
        props: { locations: [{ latitude: 40.0, longitude: -3.0, name: 'Camp A' }] },
      })

      await nextTick()
      await nextTick()

      expect(mockInvalidateSize).toHaveBeenCalled()
    })
  })
})
