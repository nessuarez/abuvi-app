import { describe, it, expect, vi, beforeEach } from 'vitest'
import { mount } from '@vue/test-utils'

// Use vi.hoisted to avoid initialization order issues with vi.mock
const mockBindPopup = vi.hoisted(() => vi.fn().mockReturnThis())
const mockOn = vi.hoisted(() => vi.fn().mockReturnThis())
const mockAddTo = vi.hoisted(() => vi.fn().mockReturnValue({ bindPopup: mockBindPopup, on: mockOn, remove: vi.fn() }))
const mockFitBounds = vi.hoisted(() => vi.fn())
const mockRemove = vi.hoisted(() => vi.fn())

vi.mock('leaflet', () => ({
  default: {
    map: vi.fn().mockReturnValue({
      setView: vi.fn().mockReturnValue({
        fitBounds: mockFitBounds,
        remove: mockRemove,
      }),
      fitBounds: mockFitBounds,
      remove: mockRemove,
    }),
    tileLayer: vi.fn().mockReturnValue({ addTo: vi.fn() }),
    marker: vi.fn().mockReturnValue({ addTo: mockAddTo, remove: vi.fn() }),
    latLngBounds: vi.fn().mockReturnValue({}),
    Icon: { Default: { prototype: {}, mergeOptions: vi.fn() } },
  },
}))

vi.mock('leaflet/dist/leaflet.css', () => ({}))

// Import after mocks
import CampLocationMap from '@/components/camps/CampLocationMap.vue'

describe('CampLocationMap', () => {
  beforeEach(() => {
    vi.clearAllMocks()
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
    const popupHtml = mockBindPopup.mock.calls[0][0] as string
    expect(popupHtml).toContain('Madrid, Spain')
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
    const popupHtml = mockBindPopup.mock.calls[0][0] as string
    expect(popupHtml).toContain('Última edición: 2025')
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
    const popupHtml = mockBindPopup.mock.calls[0][0] as string
    expect(popupHtml).toContain('Camp B')
    expect(popupHtml).not.toContain('Última edición')
    expect(popupHtml).not.toContain('undefined')
  })
})
