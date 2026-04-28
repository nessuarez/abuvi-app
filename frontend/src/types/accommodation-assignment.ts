import type { AccommodationFeature } from './accommodation-feature'
import type { MediaItem } from './media-item'

export type AccommodationTypeValue = 'Lodge' | 'Bungalow' | 'Motorhome' | 'Caravan' | 'Tent'

export const ACCOMMODATION_TYPE_LABELS: Record<AccommodationTypeValue, string> = {
  Lodge: 'Albergue',
  Bungalow: 'Bungalow',
  Motorhome: 'Autocaravana',
  Caravan: 'Caravana',
  Tent: 'Tienda'
}

export interface AccommodationZoneResponse {
  id: string
  campEditionId: string
  accommodationType: AccommodationTypeValue
  name: string
  maxCapacity: number | null
  distributionNotes: string | null
  sortOrder: number
  isActive: boolean
  accommodationIds: string[]
  features: AccommodationFeature[]
  mediaItems: MediaItem[]
  createdAt: string
  updatedAt: string
}

export interface CreateAccommodationZoneRequest {
  accommodationType: AccommodationTypeValue
  name: string
  maxCapacity: number | null
  distributionNotes: string | null
  sortOrder: number
}

export interface UpdateAccommodationZoneRequest {
  name: string
  maxCapacity: number | null
  distributionNotes: string | null
  sortOrder: number
}

export interface AccommodationAssignmentProposalSummaryResponse {
  id: string
  campEditionId: string
  name: string
  notes: string | null
  isActive: boolean
  assignmentCount: number
  unassignedCount: number
  createdByUserId: string
  createdAt: string
  updatedAt: string
}

export interface AccommodationPreferenceItem {
  accommodationId: string
  preferenceOrder: number
}

export interface AssignmentFamilyResponse {
  registrationId: string
  familyUnitId: string
  familyName: string
  representativeName: string
  memberCount: number
  adultCount: number
  childCount: number
  hasPet: boolean
  specialNeeds: string | null
  campatesPreference: string | null
  accommodationPreferences: AccommodationPreferenceItem[]
}

export interface AssignmentAccommodationResponse {
  id: string
  name: string
  type: AccommodationTypeValue
  capacity: number | null
  countByFamily: boolean
  zoneId: string | null
  zoneName: string | null
  sortOrder: number
}

export interface AssignmentEntry {
  registrationId: string
  accommodationId: string
}

export interface ProposalAssignmentStateResponse {
  proposalId: string
  families: AssignmentFamilyResponse[]
  accommodations: AssignmentAccommodationResponse[]
  assignments: AssignmentEntry[]
}

export interface AssignmentReportFamilyRow {
  registrationId: string
  familyName: string
  representativeName: string
  memberCount: number
  accommodationName: string | null
  zoneName: string | null
}

export interface AssignmentReportGroupResponse {
  groupKey: string
  groupLabel: string
  totalCapacity: number
  usedCapacity: number
  families: AssignmentReportFamilyRow[]
}
