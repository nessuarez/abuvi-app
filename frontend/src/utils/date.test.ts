import { describe, it, expect } from 'vitest'
import { formatDateLocal, parseDateLocal, parseDateSafe } from './date'

describe('formatDateLocal', () => {
  it('should format date using local timezone components, not UTC', () => {
    // new Date(year, month, day) always creates local midnight — never UTC
    const date = new Date(2020, 8, 20) // September 20, 2020 local midnight
    expect(formatDateLocal(date)).toBe('2020-09-20')
  })

  it('should zero-pad single-digit month and day', () => {
    const date = new Date(2020, 0, 5) // January 5, 2020
    expect(formatDateLocal(date)).toBe('2020-01-05')
  })

  it('should format December 31 correctly', () => {
    const date = new Date(2023, 11, 31) // December 31, 2023
    expect(formatDateLocal(date)).toBe('2023-12-31')
  })
})

describe('parseDateLocal', () => {
  it('should parse YYYY-MM-DD as local midnight, not UTC midnight', () => {
    const date = parseDateLocal('2020-09-20')
    expect(date.getFullYear()).toBe(2020)
    expect(date.getMonth()).toBe(8) // 0-indexed: 8 = September
    expect(date.getDate()).toBe(20)
  })

  it('should handle single-digit months and days', () => {
    const date = parseDateLocal('2020-01-05')
    expect(date.getFullYear()).toBe(2020)
    expect(date.getMonth()).toBe(0) // January
    expect(date.getDate()).toBe(5)
  })

  it('should round-trip correctly with formatDateLocal', () => {
    const original = '2020-09-20'
    const parsed = parseDateLocal(original)
    expect(formatDateLocal(parsed)).toBe(original)
  })
})

describe('parseDateSafe', () => {
  it('should parse date-only strings as local midnight (like parseDateLocal)', () => {
    const date = parseDateSafe('2020-09-20')
    expect(date.getFullYear()).toBe(2020)
    expect(date.getMonth()).toBe(8)
    expect(date.getDate()).toBe(20)
  })

  it('should parse full ISO timestamps with new Date()', () => {
    const date = parseDateSafe('2020-09-20T14:30:00Z')
    expect(date.getFullYear()).toBeDefined()
    expect(date.getTime()).toBe(new Date('2020-09-20T14:30:00Z').getTime())
  })

  it('should round-trip date-only strings correctly with formatDateLocal', () => {
    const original = '2020-09-20'
    const parsed = parseDateSafe(original)
    expect(formatDateLocal(parsed)).toBe(original)
  })
})
