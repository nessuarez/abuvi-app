import type { MediaItemYearSource } from './media-item'

export interface YearProposal {
  id: string
  proposedByUserId: string
  proposedByName: string
  proposedYear: number
  proposedCampEditionId: string | null
  rationale: string | null
  createdAt: string
}

export interface YearProposalGroup {
  year: number
  campEditionId: string | null
  campName: string | null
  count: number
  /** Capped at five by the API — the point is who says so, not a roll call. */
  proposerNames: string[]
}

/**
 * Themes as a dating clue: if this item is tagged "San Abuvino" and other San Abuvino
 * items are dated to 1998, 2003 and 2011, those are the years worth considering.
 */
export interface ThemeYearHint {
  themeId: string
  themeName: string
  yearsWithDatedItems: number[]
}

/**
 * Provenance as a dating clue, and usually the strongest one: the person who handed over
 * the material generally knows roughly when it is from.
 */
export interface SourceHint {
  mediaSourceId: string | null
  contributorName: string | null
  /** Non-null means the UI can offer "preguntar a esta persona". */
  contributorUserId: string | null
  yearsFromSameSource: number[]
  /** Already trimmed by the API for the viewer's role. */
  sourcePathDisplay: string | null
}

export interface YearProposalTally {
  mediaItemId: string
  resolvedYear: number | null
  yearSource: MediaItemYearSource
  isResolved: boolean
  /** Ordered by vote count, descending. */
  groups: YearProposalGroup[]
  viewerProposal: YearProposal | null
  themeHints: ThemeYearHint[]
  sourceHint: SourceHint | null
}

export interface UpsertYearProposalRequest {
  proposedYear: number
  proposedCampEditionId?: string | null
  rationale?: string | null
}
