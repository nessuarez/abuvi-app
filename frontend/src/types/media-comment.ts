export type MediaCommentReportReason =
  | 'Offensive'
  | 'PrivacyConcern'
  | 'Incorrect'
  | 'Other'

export type MediaCommentReportStatus = 'Pending' | 'Actioned' | 'Dismissed'

/** Spanish labels for the report dialog. */
export const REPORT_REASON_LABELS: Record<MediaCommentReportReason, string> = {
  Offensive: 'Contenido ofensivo',
  PrivacyConcern: 'Problema de privacidad',
  Incorrect: 'Información incorrecta',
  Other: 'Otro motivo',
}

export interface MediaComment {
  id: string
  mediaItemId: string
  authorUserId: string
  authorName: string
  body: string
  /** Viewer is the author AND still inside the 15-minute window. */
  canEdit: boolean
  /** That, or the viewer is Admin/Board. */
  canDelete: boolean
  viewerReported: boolean
  createdAt: string
  updatedAt: string
}

export interface MediaCommentReport {
  id: string
  mediaCommentId: string
  commentBody: string
  mediaItemId: string
  reportedByUserId: string
  reportedByName: string
  reason: MediaCommentReportReason
  notes: string | null
  status: MediaCommentReportStatus
  createdAt: string
  reviewedAt: string | null
}

export interface ReportMediaCommentRequest {
  reason: MediaCommentReportReason
  notes?: string | null
}
