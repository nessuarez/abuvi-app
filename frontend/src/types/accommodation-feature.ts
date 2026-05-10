export type FeatureApplicabilityLevel =
  | 'Zone'
  | 'Accommodation'
  | 'AccommodationType'
  | 'Any'

export const FEATURE_APPLICABILITY_LABELS: Record<FeatureApplicabilityLevel, string> = {
  Zone: 'Solo zonas',
  Accommodation: 'Solo alojamientos',
  AccommodationType: 'Por tipo de alojamiento',
  Any: 'Cualquiera',
}

export interface AccommodationFeature {
  id: string
  name: string
  icon: string
  description: string | null
  applicabilityLevel: FeatureApplicabilityLevel
  isActive: boolean
  sortOrder: number
  createdAt: string
  updatedAt: string
}

export interface CreateAccommodationFeatureRequest {
  name: string
  icon: string
  description?: string | null
  applicabilityLevel: FeatureApplicabilityLevel
  sortOrder?: number
}

export interface UpdateAccommodationFeatureRequest {
  name: string
  icon: string
  description?: string | null
  applicabilityLevel: FeatureApplicabilityLevel
  isActive: boolean
  sortOrder: number
}

export interface SetFeatureAssignmentsRequest {
  featureIds: string[]
}
