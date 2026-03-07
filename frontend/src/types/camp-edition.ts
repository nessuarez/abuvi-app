// Camp edition types matching backend DTOs

import type { Camp, AccommodationCapacity, CampPlacesPhoto } from './camp'

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
  customBabyMaxAge?: number
  customChildMinAge?: number
  customChildMaxAge?: number
  customAdultMinAge?: number
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
  customBabyMaxAge?: number
  customChildMinAge?: number
  customChildMaxAge?: number
  customAdultMinAge?: number
  maxCapacity?: number | null
  contactEmail?: string
  contactPhone?: string
}

export interface ProposeCampEditionRequest extends CreateCampEditionRequest {
  proposalReason?: string
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
  requiresUserInput: boolean
  userInputLabel?: string
  maxQuantity?: number
  sortOrder: number
  currentQuantitySold: number | null
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
  requiresUserInput?: boolean
  userInputLabel?: string
  maxQuantity?: number
  sortOrder?: number
}

export interface UpdateCampExtraRequest {
  name: string
  description?: string
  price: number
  isRequired: boolean
  isActive: boolean
  requiresUserInput?: boolean
  userInputLabel?: string
  maxQuantity?: number
  sortOrder?: number
}

export interface ReorderCampExtrasRequest {
  orderedIds: string[]
}

// === Accommodation Types ===

export type AccommodationType = 'Lodge' | 'Caravan' | 'Tent' | 'Bungalow' | 'Motorhome'

export interface CampEditionAccommodation {
  id: string
  campEditionId: string
  name: string
  accommodationType: AccommodationType
  description?: string
  capacity?: number
  isActive: boolean
  sortOrder: number
  currentPreferenceCount: number
  firstChoiceCount: number
  createdAt: string
  updatedAt: string
}

export interface CreateCampEditionAccommodationRequest {
  name: string
  accommodationType: AccommodationType
  description?: string
  capacity?: number
  sortOrder?: number
}

export interface UpdateCampEditionAccommodationRequest {
  name: string
  accommodationType: AccommodationType
  description?: string
  capacity?: number
  isActive: boolean
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
  description?: string
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
  description?: string
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

export interface CurrentCampEditionResponse {
  id: string
  campId: string
  campName: string
  campLocation: string | null
  campFormattedAddress: string | null
  campLatitude: number | null
  campLongitude: number | null
  year: number
  startDate: string
  endDate: string
  pricePerAdult: number
  pricePerChild: number
  pricePerBaby: number
  useCustomAgeRanges: boolean
  customBabyMaxAge?: number | null
  customChildMinAge?: number | null
  customChildMaxAge?: number | null
  customAdultMinAge?: number | null
  status: CampEditionStatus
  maxCapacity?: number | null
  registrationCount: number
  availableSpots?: number | null
  notes?: string | null
  description?: string | null
  createdAt: string
  updatedAt: string
  campDescription: string | null
  campPhoneNumber: string | null
  campNationalPhoneNumber: string | null
  campWebsiteUrl: string | null
  campGoogleMapsUrl: string | null
  campGoogleRating: number | null
  campGoogleRatingCount: number | null
  campPhotos: CampPlacesPhoto[]
  accommodationCapacity: AccommodationCapacity | null
  calculatedTotalBedCapacity: number | null
  extras: CampEditionExtra[]
}
