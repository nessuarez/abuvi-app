/**
 * Who provided a batch of historical material.
 *
 * Distinct from the account that uploaded it: donors are frequently not registered users,
 * which is why the name is free text. One row per donation, not per file.
 */
export interface MediaSource {
  id: string
  contributorName: string
  /** Non-null means the contributor is a member, so the UI can offer "ask them". */
  contributorUserId: string | null
  /**
   * Personal data of someone who may not be a member and never agreed to be listed.
   * The API returns `null` for anyone below Admin/Board — render it in the admin panel
   * and nowhere else.
   */
  contributorContact: string | null
  notes: string | null
  receivedAt: string | null
  registeredByUserId: string
  registeredByName: string
  itemCount: number
  /** How much of this person's material still needs dating. */
  undatedItemCount: number
  firstYear: number | null
  lastYear: number | null
  createdAt: string
}

export interface CreateMediaSourceRequest {
  contributorName: string
  contributorUserId?: string | null
  contributorContact?: string | null
  notes?: string | null
  receivedAt?: string | null
}

export type UpdateMediaSourceRequest = CreateMediaSourceRequest
