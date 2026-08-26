/** One published anniversary photo, as returned by GET /api/camps/history. */
export interface CampHistoryPhoto {
  id: string
  /** Never empty: the API falls back to the full image when no thumbnail exists. */
  thumbnailUrl: string
  title: string
}

/** One completed camp edition. Mirrors CampHistoryResponse in the API. */
export interface CampHistoryEntry {
  year: number
  campId: string
  campName: string
  location: string | null
  latitude: number | null
  longitude: number | null
  /** How many times the association had camped at this venue up to and including this year. */
  editionNumber: number
  /** The venue's full tally across the whole history. */
  totalEditionsAtVenue: number
  /** Approved and published photos for this year. 0 means "nothing survives", not "not loaded". */
  photoCount: number
  previewPhotos: CampHistoryPhoto[]
}

/** One choice in the edition selector of the anniversary upload form. */
export interface CampEditionOption {
  year: number
  /** "2003 — Espinosa de los Monteros" */
  label: string
  campName: string | null
  /** The camp running now. It is not part of the history yet, so it comes from elsewhere. */
  isCurrent: boolean
}

/** A venue with all its editions, grouped client-side from the flat history rows. */
export interface CampHistoryVenue {
  campId: string
  campName: string
  location: string | null
  latitude: number | null
  longitude: number | null
  /** Ascending. Length always equals totalEditionsAtVenue. */
  years: number[]
  totalEditionsAtVenue: number
  /** Sum of photoCount across this venue's editions. */
  photoCount: number
}
