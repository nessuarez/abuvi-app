/**
 * Where an attendance record came from.
 *
 * `Registration` entries are real but derived at read time from the family's
 * registrations — they are never stored, and so cannot be withdrawn. Render them
 * without a remove affordance.
 */
export type AttendanceSource = 'Declared' | 'Registration' | 'None'

export interface AttendanceEntry {
  campEditionId: string
  userId: string
  userName: string
  familyMemberId: string | null
  familyMemberName: string | null
  source: Exclude<AttendanceSource, 'None'>
}

export interface CampTimelineEntry {
  campEditionId: string
  year: number
  campName: string
  latitude: number | null
  longitude: number | null
  attended: boolean
  attendanceSource: AttendanceSource
  mediaCount: number
}

/** Every edition, attended or not, so the map can be painted in one call. */
export interface CampTimeline {
  totalEditionsAttended: number
  entries: CampTimelineEntry[]
}

export interface DeclareAttendanceRequest {
  /** `null` means the caller themselves. */
  familyMemberId?: string | null
}
