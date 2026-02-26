// Camp edition types matching backend DTOs

import type { Camp, AccommodationCapacity } from './camp'

export interface CampEdition {
  id: string
  campId: string
  year: number
  name?: string
  startDate: string
  endDate: string
  location: string
  description?: string
  pricePerAdult: number
  pricePerChild: number
  pricePerBaby: number
  useCustomAgeRanges: boolean
  babyMaxAge?: number
  childMinAge?: number
  childMaxAge?: number
  adultMinAge?: number
  maxCapacity: number
  contactEmail?: string
  contactPhone?: string
  status: CampEditionStatus
  isArchived: boolean
  proposalReason?: string
  proposalNotes?: string
  createdAt: string
  updatedAt: string
  camp?: Camp
  // Computed fields from backend (present in GET /api/camps/current response)
  registrationCount?: number
  availableSpots?: number
  accommodationCapacity?: AccommodationCapacity | null
  calculatedTotalBedCapacity?: number | null
  // Partial attendance (week pricing):
  halfDate?: string | null // YYYY-MM-DD
  pricePerAdultWeek?: number | null
  pricePerChildWeek?: number | null
  pricePerBabyWeek?: number | null
  // Weekend visit:
  weekendStartDate?: string | null // YYYY-MM-DD
  weekendEndDate?: string | null // YYYY-MM-DD
  pricePerAdultWeekend?: number | null
  pricePerChildWeekend?: number | null
  pricePerBabyWeekend?: number | null
  maxWeekendCapacity?: number | null
}

export type CampEditionStatus = 'Proposed' | 'Draft' | 'Open' | 'Closed' | 'Completed'

export interface CreateCampEditionRequest {
  campId: string
  year: number
  name?: string
  startDate: string
  endDate: string
  location: string
  description?: string
  pricePerAdult: number
  pricePerChild: number
  pricePerBaby: number
  useCustomAgeRanges?: boolean
  babyMaxAge?: number
  childMinAge?: number
  childMaxAge?: number
  adultMinAge?: number
  maxCapacity?: number | null
  contactEmail?: string
  contactPhone?: string
}

export interface ProposeCampEditionRequest extends CreateCampEditionRequest {
  proposalReason: string
  proposalNotes: string
  accommodationCapacity?: AccommodationCapacity | null
  // Partial attendance (week pricing):
  halfDate?: string | null
  pricePerAdultWeek?: number | null
  pricePerChildWeek?: number | null
  pricePerBabyWeek?: number | null
  // Weekend visit:
  weekendStartDate?: string | null
  weekendEndDate?: string | null
  pricePerAdultWeekend?: number | null
  pricePerChildWeekend?: number | null
  pricePerBabyWeekend?: number | null
  maxWeekendCapacity?: number | null
}

export interface CampEditionExtra {
  id: string
  campEditionId: string
  name: string
  description?: string
  price: number
  pricingType: 'PerPerson' | 'PerFamily'
  pricingPeriod: 'OneTime' | 'PerDay'
  isRequired: boolean
  maxQuantity?: number
  currentQuantity: number
  sortOrder: number
  isActive: boolean
  createdAt: string
  updatedAt: string
}

export interface CreateCampExtraRequest {
  name: string
  description?: string
  price: number
  pricingType: 'PerPerson' | 'PerFamily'
  pricingPeriod: 'OneTime' | 'PerDay'
  isRequired: boolean
  maxQuantity?: number
  sortOrder: number
}

export interface UpdateCampEditionRequest {
  startDate: string
  endDate: string
  pricePerAdult: number
  pricePerChild: number
  pricePerBaby: number
  useCustomAgeRanges: boolean
  customBabyMaxAge?: number
  customChildMinAge?: number
  customChildMaxAge?: number
  customAdultMinAge?: number
  maxCapacity?: number
  notes?: string
  // Partial attendance (week pricing):
  halfDate?: string | null
  pricePerAdultWeek?: number | null
  pricePerChildWeek?: number | null
  pricePerBabyWeek?: number | null
  // Weekend visit:
  weekendStartDate?: string | null
  weekendEndDate?: string | null
  pricePerAdultWeekend?: number | null
  pricePerChildWeekend?: number | null
  pricePerBabyWeekend?: number | null
  maxWeekendCapacity?: number | null
}

export interface ChangeEditionStatusRequest {
  status: CampEditionStatus
  force?: boolean // Admin-only: bypasses startDate < today when re-opening Draft → Open
}

export interface ActiveCampEditionResponse {
  id: string
  campId: string
  campName: string
  campLocation: string | null
  campFormattedAddress: string | null
  year: number
  startDate: string
  endDate: string
  pricePerAdult: number
  pricePerChild: number
  pricePerBaby: number
  useCustomAgeRanges: boolean
  customBabyMaxAge?: number
  customChildMinAge?: number
  customChildMaxAge?: number
  customAdultMinAge?: number
  status: CampEditionStatus
  maxCapacity?: number
  registrationCount: number
  notes?: string
  createdAt: string
  updatedAt: string
  // Partial attendance (week pricing):
  halfDate?: string | null
  pricePerAdultWeek?: number | null
  pricePerChildWeek?: number | null
  pricePerBabyWeek?: number | null
  // Weekend visit:
  weekendStartDate?: string | null
  weekendEndDate?: string | null
  pricePerAdultWeekend?: number | null
  pricePerChildWeekend?: number | null
  pricePerBabyWeekend?: number | null
  maxWeekendCapacity?: number | null
}

export interface CampEditionFilters {
  year?: number
  status?: CampEditionStatus
  campId?: string
}
