// Registration status and related enums
export type RegistrationStatus = 'Pending' | 'Confirmed' | 'Cancelled'
export type AgeCategory = 'Baby' | 'Child' | 'Adult'
export type PaymentMethod = 'Card' | 'Transfer' | 'Cash'
export type PaymentStatus = 'Pending' | 'Completed' | 'Failed' | 'Refunded'

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
  individualAmount: number
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
}

// Request types
export interface CreateRegistrationRequest {
  campEditionId: string
  familyUnitId: string
  memberIds: string[]
  notes?: string | null
}

export interface UpdateRegistrationMembersRequest {
  memberIds: string[]
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
