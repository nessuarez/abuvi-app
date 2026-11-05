/**
 * A recurring subject that spans many camp editions — "San Abuvino", "Actuaciones".
 *
 * Themes are a cross-cutting dimension, not a rival to the edition album: an item is
 * edition 1998 AND San Abuvino at once, and an item with no edition can still carry
 * themes, which then become a dating clue.
 */
export interface MediaTheme {
  id: string
  name: string
  /** Kebab-case URL identity. Stable across renames. */
  slug: string
  description: string | null
  isActive: boolean
  itemCount: number
  /** Earliest dated item carrying this theme. */
  firstYear: number | null
  /** Latest. Together with firstYear this is what makes "spans many years" visible. */
  lastYear: number | null
  /** Items with this theme still waiting for an edition. */
  undatedCount: number
}

/** A theme as it appears attached to a media item. */
export interface MediaThemeRef {
  id: string
  name: string
  slug: string
}

export interface CreateMediaThemeRequest {
  name: string
  description?: string | null
}

export interface UpdateMediaThemeRequest {
  name: string
  description?: string | null
  isActive: boolean
}
