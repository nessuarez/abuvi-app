import type { AttendancePeriod } from '@/types/registration'

export const ATTENDANCE_PERIOD_LABELS: Record<AttendancePeriod, string> = {
  Complete: 'Campamento completo',
  FirstWeek: 'Primera semana',
  SecondWeek: 'Segunda semana',
  WeekendVisit: 'Visita de fin de semana'
}

export const getAttendancePeriodLabel = (period: AttendancePeriod): string =>
  ATTENDANCE_PERIOD_LABELS[period] ?? period

/**
 * Compute the number of days for each attendance period.
 * Uses `halfDate` as the split point when provided; otherwise splits the total at the midpoint.
 */
export function computePeriodDays(
  startDate: string,
  endDate: string,
  halfDate: string | null | undefined
): { firstWeekDays: number; secondWeekDays: number; totalDays: number } {
  const start = new Date(startDate)
  const end = new Date(endDate)
  const totalDays = Math.round((end.getTime() - start.getTime()) / (1000 * 60 * 60 * 24))

  if (halfDate) {
    const half = new Date(halfDate)
    const firstWeekDays = Math.round((half.getTime() - start.getTime()) / (1000 * 60 * 60 * 24))
    const secondWeekDays = totalDays - firstWeekDays
    return { firstWeekDays, secondWeekDays, totalDays }
  }

  const firstWeekDays = Math.floor(totalDays / 2)
  const secondWeekDays = totalDays - firstWeekDays
  return { firstWeekDays, secondWeekDays, totalDays }
}

/**
 * Returns the allowed attendance period options for a camp edition.
 * Always includes 'Complete'. Adds week periods when week pricing is configured.
 * Adds 'WeekendVisit' when weekend dates are configured.
 */
export function getAllowedPeriods(edition: {
  pricePerAdultWeek?: number | null
  weekendStartDate?: string | null
}): AttendancePeriod[] {
  const periods: AttendancePeriod[] = ['Complete']
  if (edition.pricePerAdultWeek != null) {
    periods.push('FirstWeek', 'SecondWeek')
  }
  if (edition.weekendStartDate != null) {
    periods.push('WeekendVisit')
  }
  return periods
}
