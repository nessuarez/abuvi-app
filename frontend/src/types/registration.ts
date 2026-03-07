// Registration status and related enums
export type RegistrationStatus = 'Pending' | 'Confirmed' | 'Cancelled'
export type AgeCategory = 'Baby' | 'Child' | 'Adult'
export type PaymentMethod = 'Card' | 'Transfer' | 'Cash'
export type PaymentStatus = 'Pending' | 'PendingReview' | 'Completed' | 'Failed' | 'Refunded'

// Complete is first to match the backend enum CLR default (0)
export type AttendancePeriod = 'Complete' | 'FirstWeek' | 'SecondWeek' | 'WeekendVisit'

// API request shape per member (sent to backend)
export interface MemberAttendanceRequest {
  memberId: string
  attendancePeriod: AttendancePeriod
  visitStartDate?: string | null // YYYY-MM-DD, required when WeekendVisit
  visitEndDate?: string | null // YYYY-MM-DD, required when WeekendVisit
  guardianName?: string | null
  guardianDocumentNumber?: string | null
}

// Wizard-local state: richer than MemberAttendanceRequest, not sent directly to API
export interface WizardMemberSelection {
  memberId: string
  attendancePeriod: AttendancePeriod
  visitStartDate: string | null // ISO date string YYYY-MM-DD
  visitEndDate: string | null
  guardianName: string | null
  guardianDocumentNumber: string | null
}

// Embedded summaries in RegistrationResponse
export interface RegistrationFamilyUnitSummary {
  id: string
  name: string
  representativeUserId: string
}

export interface RegistrationCampEditionSummary {
  id: string
  campName: string
  year: number
  startDate: string
  endDate: string
  location: string | null
}

// Pricing breakdown (returned in RegistrationResponse)
export interface MemberPricingDetail {
  familyMemberId: string
  fullName: string
  ageAtCamp: number
  ageCategory: AgeCategory
  attendancePeriod: AttendancePeriod
  attendanceDays: number
  visitStartDate: string | null // only populated for WeekendVisit
  visitEndDate: string | null
  individualAmount: number
  guardianName: string | null
  guardianDocumentNumber: string | null
}

export interface ExtraPricingDetail {
  campEditionExtraId: string
  name: string
  unitPrice: number
  pricingType: 'PerPerson' | 'PerFamily'
  pricingPeriod: 'OneTime' | 'PerDay'
  quantity: number
  campDurationDays: number | null
  calculation: string
  totalAmount: number
}

export interface PricingBreakdown {
  members: MemberPricingDetail[]
  baseTotalAmount: number
  extras: ExtraPricingDetail[]
  extrasAmount: number
  totalAmount: number
}

export interface PaymentSummary {
  id: string
  amount: number
  paymentDate: string
  method: PaymentMethod
  status: PaymentStatus
}

// Main registration response (used for list and detail)
export interface RegistrationResponse {
  id: string
  familyUnit: RegistrationFamilyUnitSummary
  campEdition: RegistrationCampEditionSummary
  status: RegistrationStatus
  notes: string | null
  pricing: PricingBreakdown
  payments: PaymentSummary[]
  amountPaid: number
  amountRemaining: number
  createdAt: string
  updatedAt: string
  specialNeeds: string | null
  campatesPreference: string | null
}

// Available camp edition for registration wizard
export interface AgeRangesInfo {
  babyMaxAge: number
  childMinAge: number
  childMaxAge: number
  adultMinAge: number
}

export interface AvailableCampEditionResponse {
  id: string
  campName: string
  year: number
  startDate: string
  endDate: string
  location: string | null
  pricePerAdult: number
  pricePerChild: number
  pricePerBaby: number
  maxCapacity: number | null
  currentRegistrations: number
  spotsRemaining: number | null
  status: string
  ageRanges: AgeRangesInfo
  // Partial attendance (week periods):
  allowsPartialAttendance: boolean
  pricePerAdultWeek: number | null
  pricePerChildWeek: number | null
  pricePerBabyWeek: number | null
  halfDate: string | null // YYYY-MM-DD
  firstWeekDays: number
  secondWeekDays: number
  // Weekend visit:
  allowsWeekendVisit: boolean
  pricePerAdultWeekend: number | null
  pricePerChildWeekend: number | null
  pricePerBabyWeekend: number | null
  weekendStartDate: string | null // YYYY-MM-DD
  weekendEndDate: string | null // YYYY-MM-DD
  weekendDays: number
  maxWeekendCapacity: number | null
  weekendSpotsRemaining: number | null
}

// Request types
export interface CreateRegistrationRequest {
  campEditionId: string
  familyUnitId: string
  members: MemberAttendanceRequest[]
  notes?: string | null
  specialNeeds: string | null
  campatesPreference: string | null
}

export interface UpdateRegistrationMembersRequest {
  members: MemberAttendanceRequest[]
}

export interface ExtraSelectionRequest {
  campEditionExtraId: string
  quantity: number
}

export interface UpdateRegistrationExtrasRequest {
  extras: ExtraSelectionRequest[]
}

// Wizard-local state (not sent to API)
export interface WizardExtrasSelection {
  campEditionExtraId: string
  name: string
  quantity: number
  unitPrice: number
}

// === Accommodation Preferences ===

import type { AccommodationType } from './camp-edition'

// Wizard-local state for accommodation preferences
export interface WizardAccommodationPreference {
  campEditionAccommodationId: string
  accommodationName: string
  accommodationType: AccommodationType
  preferenceOrder: number
}

// API request/response shapes
export interface AccommodationPreferenceRequest {
  campEditionAccommodationId: string
  preferenceOrder: number
}

export interface UpdateAccommodationPreferencesRequest {
  preferences: AccommodationPreferenceRequest[]
}

export interface AccommodationPreferenceResponse {
  campEditionAccommodationId: string
  accommodationName: string
  accommodationType: AccommodationType
  preferenceOrder: number
}
