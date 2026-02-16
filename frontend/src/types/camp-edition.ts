// Camp edition types matching backend DTOs

import type { Camp } from './camp'

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
  maxCapacity: number
  contactEmail?: string
  contactPhone?: string
}

export interface ProposeCampEditionRequest extends CreateCampEditionRequest {
  proposalReason: string
  proposalNotes: string
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
