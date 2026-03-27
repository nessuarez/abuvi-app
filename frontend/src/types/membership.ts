import type { FamilyMemberResponse } from '@/types/family-unit'

export enum FeeStatus {
  Pending = 'Pending',
  Paid = 'Paid',
  Overdue = 'Overdue',
}

export const FeeStatusLabels: Record<FeeStatus, string> = {
  [FeeStatus.Pending]: 'Pendiente',
  [FeeStatus.Paid]: 'Pagada',
  [FeeStatus.Overdue]: 'Vencida',
}

export const FeeStatusSeverity: Record<FeeStatus, 'warn' | 'success' | 'danger'> = {
  [FeeStatus.Pending]: 'warn',
  [FeeStatus.Paid]: 'success',
  [FeeStatus.Overdue]: 'danger',
}

export interface MembershipFeeResponse {
  id: string
  membershipId: string
  year: number
  amount: number
  status: FeeStatus
  paidDate: string | null
  paymentReference: string | null
  createdAt: string
}

export interface MembershipResponse {
  id: string
  familyMemberId: string
  memberNumber: number | null
  startDate: string // ISO 8601 date string (YYYY-MM-DD)
  endDate: string | null
  isActive: boolean
  fees: MembershipFeeResponse[]
  createdAt: string
  updatedAt: string
}

export interface CreateMembershipRequest {
  year: number // Calendar year — must not be in the future (≤ current year)
}

export interface PayFeeRequest {
  paidDate: string // ISO 8601 date string — must not be in the future
  paymentReference?: string | null
}

// Request to update member number (Admin/Board only)
export interface UpdateMemberNumberRequest {
  memberNumber: number
}

export type BulkMembershipResultStatus = 'Activated' | 'Skipped' | 'Failed'

export interface BulkMembershipMemberResult {
  memberId: string
  memberName: string
  status: BulkMembershipResultStatus
  reason?: string | null
}

export interface BulkActivateMembershipResponse {
  activated: number
  skipped: number
  results: BulkMembershipMemberResult[]
}

export interface BulkActivateMembershipRequest {
  year: number
}

/** Admin/Board: manually create an annual fee for an existing membership. */
export interface CreateMembershipFeeRequest {
  year: number // > 2000 and <= current year
  amount: number // >= 0
}

/** Admin/Board: reactivate a previously deactivated membership. */
export interface ReactivateMembershipRequest {
  year: number // > 2000 and <= current year
}

export type MembershipStatus = 'none' | 'active' | 'activeFeePending' | 'inactive'

export interface MemberMembershipData {
  member: FamilyMemberResponse
  membershipId: string | null
  isActiveMembership: boolean
  currentFee: MembershipFeeResponse | null
  feeLoading: boolean
  /** Derived status for display. */
  membershipStatus: MembershipStatus
}
