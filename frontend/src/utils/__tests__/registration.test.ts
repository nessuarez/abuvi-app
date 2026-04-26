import { describe, it, expect } from 'vitest'
import { formatAttendancePeriods } from '@/utils/registration'

describe('formatAttendancePeriods', () => {
  it('should return T for Complete', () => {
    expect(formatAttendancePeriods(['Complete'])).toBe('T')
  })

  it('should return S1 for FirstWeek', () => {
    expect(formatAttendancePeriods(['FirstWeek'])).toBe('S1')
  })

  it('should return S2 for SecondWeek', () => {
    expect(formatAttendancePeriods(['SecondWeek'])).toBe('S2')
  })

  it('should return V for WeekendVisit', () => {
    expect(formatAttendancePeriods(['WeekendVisit'])).toBe('V')
  })

  it('should join multiple periods with ·', () => {
    expect(formatAttendancePeriods(['FirstWeek', 'SecondWeek'])).toBe('S1 · S2')
  })

  it('should return empty string for empty array', () => {
    expect(formatAttendancePeriods([])).toBe('')
  })
})
