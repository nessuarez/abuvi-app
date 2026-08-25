import { describe, it, expect } from 'vitest'
import {
  getLocationPrecision,
  PRECISION_ORDER,
  type PrecisionLevel,
} from '@/utils/locationPrecision'

describe('getLocationPrecision', () => {
  it('reports missing when there are no coordinates', () => {
    expect(getLocationPrecision('campground', null, null).level).toBe('missing')
    expect(getLocationPrecision('campground', 40, null).level).toBe('missing')
  })

  it('missing takes precedence over any place type', () => {
    const p = getLocationPrecision('campground', null, null)
    expect(p.severity).toBe('danger')
    expect(p.isPrecise).toBe(false)
  })

  it('classifies an actual campsite as exact', () => {
    // Caso real: la unica sede que Google resolvio al campamento
    const p = getLocationPrecision('campground lodging', 42.7, -0.6)
    expect(p.level).toBe('exact')
    expect(p.isPrecise).toBe(true)
    expect(p.severity).toBe('success')
  })

  it('classifies a natural feature as area', () => {
    // Caso real: Valle de Pineta
    const p = getLocationPrecision('establishment natural_feature', 42.6, 0.08)
    expect(p.level).toBe('area')
    expect(p.isPrecise).toBe(false)
  })

  it('prefers the most specific type when several are present', () => {
    expect(getLocationPrecision('campground political', 40, -3).level).toBe('exact')
  })

  it('does not treat the generic establishment type as precise', () => {
    // Google adjunta "establishment"/"point_of_interest" a casi todo
    expect(getLocationPrecision('establishment point_of_interest', 40, -3).level)
      .toBe('unknown')
  })

  it('classifies a municipality as town', () => {
    // Caso real: 20 de las 31 sedes
    const p = getLocationPrecision('locality political', 42.86, -4.49)
    expect(p.level).toBe('town')
    expect(p.severity).toBe('warn')
    expect(p.isPrecise).toBe(false)
  })

  it('classifies a route as town-level', () => {
    // Caso real: Cabaneros
    expect(getLocationPrecision('route', 38.97, -3.91).level).toBe('town')
  })

  it('reports unknown when place types are absent', () => {
    expect(getLocationPrecision(null, 40, -3).level).toBe('unknown')
    expect(getLocationPrecision('', 40, -3).level).toBe('unknown')
    expect(getLocationPrecision('   ', 40, -3).level).toBe('unknown')
  })

  it('reports unknown for types it does not recognise', () => {
    expect(getLocationPrecision('bakery florist', 40, -3).level).toBe('unknown')
  })

  it('accepts comma-separated types and mixed case', () => {
    expect(getLocationPrecision('Locality, Political', 40, -3).level).toBe('town')
  })

  it('accepts zero coordinates as valid', () => {
    // 0,0 es absurdo geograficamente pero no es "sin ubicacion"
    expect(getLocationPrecision('locality', 0, 0).level).toBe('town')
  })
})

describe('PRECISION_ORDER', () => {
  it('sorts the least precise first, which is the review order', () => {
    const levels: PrecisionLevel[] = ['exact', 'town', 'missing', 'area', 'unknown']
    const sorted = [...levels].sort((a, b) => PRECISION_ORDER[a] - PRECISION_ORDER[b])

    expect(sorted).toEqual(['missing', 'town', 'unknown', 'area', 'exact'])
  })
})
