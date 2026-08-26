import { describe, it, expect, vi, beforeEach } from 'vitest'
import { mount } from '@vue/test-utils'

const mapHandlers = vi.hoisted(() => ({}) as Record<string, (e: unknown) => void>)
const markerHandlers = vi.hoisted(() => ({}) as Record<string, () => void>)

const mockSetLatLng = vi.hoisted(() => vi.fn())
const mockGetLatLng = vi.hoisted(() => vi.fn(() => ({ lat: 42.123456789, lng: -1.987654321 })))
const mockBindTooltip = vi.hoisted(() => vi.fn().mockReturnThis())
const mockSetView = vi.hoisted(() => vi.fn())
const mockRemoveLayer = vi.hoisted(() => vi.fn())
const mockLayerAddTo = vi.hoisted(() => vi.fn())
const mockInvalidateSize = vi.hoisted(() => vi.fn())

const mockMarker = vi.hoisted(() => ({
  addTo: vi.fn().mockReturnThis(),
  bindTooltip: mockBindTooltip,
  on: vi.fn((evt: string, cb: () => void) => {
    markerHandlers[evt] = cb
  }),
  setLatLng: mockSetLatLng,
  getLatLng: mockGetLatLng,
  dragging: { enable: vi.fn(), disable: vi.fn() },
}))

// Leaflet's setView returns the map so calls can be chained; the mock must too.
const mockMap: Record<string, unknown> = vi.hoisted(() => ({}))

vi.mock('leaflet', () => {
  Object.assign(mockMap, {
    setView: mockSetView.mockImplementation(() => mockMap),
    getZoom: vi.fn(() => 15),
    on: vi.fn((evt: string, cb: (e: unknown) => void) => {
      mapHandlers[evt] = cb
    }),
    removeLayer: mockRemoveLayer,
    remove: vi.fn(),
    invalidateSize: mockInvalidateSize,
  })

  return {
    default: {
      map: vi.fn(() => mockMap),
      tileLayer: vi.fn(() => ({ addTo: mockLayerAddTo })),
      marker: vi.fn(() => mockMarker),
      Icon: { Default: { prototype: {}, mergeOptions: vi.fn() } },
    },
  }
})
vi.mock('leaflet/dist/leaflet.css', () => ({}))

import CampLocationPicker from '@/components/camps/CampLocationPicker.vue'

const mountPicker = (props = {}) =>
  mount(CampLocationPicker, {
    props: { latitude: 42.7833, longitude: -0.6833, ...props },
    global: { stubs: { Button: { template: '<button>{{ label }}<slot /></button>', props: ['label', 'icon', 'disabled'] } } },
  })

/** Presses the unlock button, which guards every position change. */
const unlock = async (wrapper: ReturnType<typeof mountPicker>) => {
  await wrapper.findAll('button')[0].trigger('click')
}

describe('CampLocationPicker', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    mockSetView.mockImplementation(() => mockMap)
    for (const k of Object.keys(mapHandlers)) delete mapHandlers[k]
    for (const k of Object.keys(markerHandlers)) delete markerHandlers[k]
  })

  it('does not place a marker when there are no coordinates', async () => {
    const L = (await import('leaflet')).default
    mountPicker({ latitude: null, longitude: null })

    expect(L.marker).not.toHaveBeenCalled()
  })

  it('ignores map clicks while the location is locked', async () => {
    const wrapper = mountPicker()

    mapHandlers.click({ latlng: { lat: 40.5, lng: -3.25 } })
    await wrapper.vm.$nextTick()

    expect(wrapper.emitted('update:latitude')).toBeUndefined()
  })

  it('emits new coordinates when the map is clicked after unlocking', async () => {
    const wrapper = mountPicker()
    await unlock(wrapper)

    mapHandlers.click({ latlng: { lat: 40.5, lng: -3.25 } })
    await wrapper.vm.$nextTick()

    expect(wrapper.emitted('update:latitude')?.[0]).toEqual([40.5])
    expect(wrapper.emitted('update:longitude')?.[0]).toEqual([-3.25])
  })

  it('creates the marker locked and enables dragging only on unlock', async () => {
    const L = (await import('leaflet')).default
    const wrapper = mountPicker()

    expect(L.marker).toHaveBeenCalledWith(
      [42.7833, -0.6833],
      expect.objectContaining({ draggable: false })
    )

    await unlock(wrapper)
    expect(mockMarker.dragging.enable).toHaveBeenCalled()
  })

  it('warns while editing is active', async () => {
    const wrapper = mountPicker({ name: 'Selva de Oza' })
    expect(wrapper.find('[role="alert"]').exists()).toBe(false)

    await unlock(wrapper)

    const alert = wrapper.find('[role="alert"]')
    expect(alert.exists()).toBe(true)
    expect(alert.text()).toContain('Selva de Oza')
  })

  it('locks again on a second press', async () => {
    const wrapper = mountPicker()
    await unlock(wrapper)
    await unlock(wrapper)

    expect(mockMarker.dragging.disable).toHaveBeenCalled()
    expect(wrapper.find('[role="alert"]').exists()).toBe(false)
  })

  it('emits new coordinates when the marker is dragged', async () => {
    const wrapper = mountPicker()

    markerHandlers.dragend()
    await wrapper.vm.$nextTick()

    expect(wrapper.emitted('update:latitude')?.[0]).toEqual([42.123457])
    expect(wrapper.emitted('update:longitude')?.[0]).toEqual([-1.987654])
  })

  it('rounds emitted coordinates to six decimals', async () => {
    const wrapper = mountPicker()
    await unlock(wrapper)

    mapHandlers.click({ latlng: { lat: 40.12345678901, lng: -3.98765432109 } })
    await wrapper.vm.$nextTick()

    expect(wrapper.emitted('update:latitude')?.[0]).toEqual([40.123457])
  })

  it('starts on satellite imagery, which is what makes a campsite visible', () => {
    const wrapper = mountPicker()

    expect(wrapper.text()).toContain('Callejero')
  })

  it('shows the current coordinates', () => {
    expect(mountPicker().text()).toContain('42.783300, -0.683300')
  })

  it('warns when there are no coordinates yet', () => {
    expect(mountPicker({ latitude: null, longitude: null }).text()).toContain('Sin coordenadas')
  })


  it('moves the existing marker when coordinates change from outside', async () => {
    const wrapper = mountPicker()
    mockSetLatLng.mockClear()

    await wrapper.setProps({ latitude: 41.0, longitude: -2.0 })

    expect(mockSetLatLng).toHaveBeenCalledWith([41.0, -2.0])
  })
})
