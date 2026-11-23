import type { MediaItemType, MediaItemYearSource } from './media-item'
import type { MediaThemeRef } from './media-theme'

/** One camp edition's album, as shown in the index. */
export interface AlbumSummary {
  campEditionId: string
  year: number
  campId: string
  campName: string
  campLocality: string | null
  latitude: number | null
  longitude: number | null
  photoCount: number
  videoCount: number
  /** Includes items of type Interview — the distinction matters for playback, not volume. */
  audioCount: number
  documentCount: number
  memoryCount: number
  coverThumbnailUrl: string | null
  /** Declared attendance unioned with attendance derived from registrations. */
  viewerAttended: boolean
}

/**
 * A media item inside an album, theme page or the unplaced pile.
 *
 * Carries the placement, provenance and theme context that the plain MediaItem does not.
 */
export interface AlbumMediaItem {
  id: string
  uploadedByUserId: string
  uploadedByName: string
  fileUrl: string
  thumbnailUrl: string | null
  type: MediaItemType
  title: string
  description: string | null
  year: number | null
  decade: string | null
  /** `null` means the edition is unknown — temporary, never "not a camp". */
  campEditionId: string | null
  yearSource: MediaItemYearSource
  commentCount: number
  mediaSourceId: string | null
  mediaSourceName: string | null
  /**
   * Already trimmed by the API according to the viewer's role. Render as received —
   * never attempt to reconstruct the full path client-side.
   */
  sourcePathDisplay: string | null
  themes: MediaThemeRef[]
  isApproved: boolean
  isPublished: boolean
  displayOrder: number
  isPrimary: boolean
  createdAt: string
}

export interface AlbumDetail {
  edition: AlbumSummary
  items: AlbumMediaItem[]
  totalCount: number
  page: number
  pageSize: number
}

/** The "sin ubicar" pile, and the shape reused by theme and contributor listings. */
export interface PagedMedia {
  items: AlbumMediaItem[]
  totalCount: number
  page: number
  pageSize: number
}

export interface ThemeItems extends PagedMedia {
  theme: import('./media-theme').MediaTheme
}
