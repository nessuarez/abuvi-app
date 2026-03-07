/**
 * Format a Date to YYYY-MM-DD using LOCAL timezone components.
 * Do NOT use .toISOString() — it converts to UTC first, causing an off-by-one-day
 * error for users in UTC+ timezones.
 */
export function formatDateLocal(date: Date): string {
  const year = date.getFullYear()
  const month = String(date.getMonth() + 1).padStart(2, '0')
  const day = String(date.getDate()).padStart(2, '0')
  return `${year}-${month}-${day}`
}

/**
 * Parse a YYYY-MM-DD string as LOCAL midnight.
 * Do NOT use new Date(str) for date-only strings — the ECMAScript spec parses them
 * as UTC midnight, which causes an off-by-one-day error in UTC+ timezones.
 */
export function parseDateLocal(dateStr: string): Date {
  const [year, month, day] = dateStr.split('-').map(Number)
  return new Date(year, month - 1, day)
}

/**
 * Safe date parser that handles both date-only strings ("YYYY-MM-DD") and
 * full ISO timestamps ("2025-08-15T00:00:00Z").
 *
 * - Date-only strings are parsed as LOCAL midnight via parseDateLocal (avoids UTC shift).
 * - Full timestamps (containing "T") are parsed with new Date() which handles them correctly.
 */
export function parseDateSafe(dateStr: string): Date {
  if (dateStr.includes('T')) {
    return new Date(dateStr)
  }
  return parseDateLocal(dateStr)
}
