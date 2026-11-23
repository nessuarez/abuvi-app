export type MediaItemType = 'Photo' | 'Video' | 'Audio' | 'Interview' | 'Document'

export interface MediaItem {
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
  memoryId: string | null
  accommodationId: string | null
  zoneId: string | null
  context: string | null
  isPublished: boolean
  isApproved: boolean
  createdAt: string
}

/** How a media item's year was established. Admin is final and never overwritten. */
export type MediaItemYearSource =
  | 'Unknown'
  | 'Exif'
  | 'FolderName'
  | 'Uploader'
  | 'Community'
  | 'Admin'

/** Contributor details captured inline while uploading, for a first-time donor. */
export interface NewMediaSource {
  contributorName: string
  contributorUserId?: string | null
  contributorContact?: string | null
  notes?: string | null
  receivedAt?: string | null
}

export interface CreateMediaItemRequest {
  fileUrl: string
  thumbnailUrl?: string | null
  type: MediaItemType
  title: string
  description?: string
  year?: number
  memoryId?: string
  campLocationId?: string
  context?: string
  accommodationId?: string
  zoneId?: string
  /**
   * The camp edition. `null` means "I don't know" and is a VALID submission: the item
   * lands in the unplaced pile and becomes eligible for collaborative dating. Never
   * substitute a default here.
   */
  campEditionId?: string | null
  themeIds?: string[]
  /** An existing contributor. Mutually exclusive with `newSource`. */
  mediaSourceId?: string
  /** Creates a contributor inline. Mutually exclusive with `mediaSourceId`. */
  newSource?: NewMediaSource
  /** Original folder path, when the browser exposes one (directory uploads). */
  sourcePath?: string
}
