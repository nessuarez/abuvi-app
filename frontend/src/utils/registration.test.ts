import { describe, it, expect } from 'vitest'
import { computePeriodDays, getAllowedPeriods, ATTENDANCE_PERIOD_LABELS } from '@/utils/registration'

describe('computePeriodDays', () => {
  it('returns correct total days', () => {
    const result = computePeriodDays('2025-07-01', '2025-07-15', null)
    expect(result.totalDays).toBe(14)
  })

  it('splits evenly when no halfDate', () => {
    const result = computePeriodDays('2025-07-01', '2025-07-15', null)
    expect(result.firstWeekDays).toBe(7)
    expect(result.secondWeekDays).toBe(7)
  })

  it('splits at halfDate when provided', () => {
    const result = computePeriodDays('2025-07-01', '2025-07-14', '2025-07-05')
    expect(result.firstWeekDays).toBe(4) // Jul 1 → Jul 5
    expect(result.secondWeekDays).toBe(9) // Jul 5 → Jul 14
    expect(result.totalDays).toBe(13)
  })

  it('handles odd total days: first week gets fewer days', () => {
    const result = computePeriodDays('2025-07-01', '2025-07-14', null)
    expect(result.totalDays).toBe(13)
    expect(result.firstWeekDays).toBe(6) // Math.floor(13 / 2)
    expect(result.secondWeekDays).toBe(7)
  })
})

describe('getAllowedPeriods', () => {
  it('returns only Complete when no week price or weekend dates', () => {
    expect(getAllowedPeriods({ pricePerAdultWeek: null, weekendStartDate: null })).toEqual([
      'Complete'
    ])
  })

  it('returns only Complete when fields are undefined', () => {
    expect(getAllowedPeriods({})).toEqual(['Complete'])
  })

  it('includes FirstWeek and SecondWeek when pricePerAdultWeek is set', () => {
    const periods = getAllowedPeriods({ pricePerAdultWeek: 110, weekendStartDate: null })
    expect(periods).toContain('FirstWeek')
    expect(periods).toContain('SecondWeek')
    expect(periods[0]).toBe('Complete')
  })

  it('includes WeekendVisit when weekendStartDate is set', () => {
    const periods = getAllowedPeriods({ pricePerAdultWeek: null, weekendStartDate: '2025-07-05' })
    expect(periods).toContain('WeekendVisit')
    expect(periods).not.toContain('FirstWeek')
  })

  it('includes all four periods when both week prices and weekend are set', () => {
    const periods = getAllowedPeriods({
      pricePerAdultWeek: 110,
      weekendStartDate: '2025-07-05'
    })
    expect(periods).toEqual(['Complete', 'FirstWeek', 'SecondWeek', 'WeekendVisit'])
  })
})

describe('ATTENDANCE_PERIOD_LABELS', () => {
  it('has a Spanish label for all four periods', () => {
    expect(ATTENDANCE_PERIOD_LABELS.Complete).toBe('Campamento completo')
    expect(ATTENDANCE_PERIOD_LABELS.FirstWeek).toBe('Primera semana')
    expect(ATTENDANCE_PERIOD_LABELS.SecondWeek).toBe('Segunda semana')
    expect(ATTENDANCE_PERIOD_LABELS.WeekendVisit).toBe('Visita de fin de semana')
  })
})
