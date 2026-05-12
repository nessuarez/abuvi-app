export type AccommodationMediaOwnerType = 'zone' | 'accommodation' | 'type'

export interface AccommodationMediaItem {
  id: string
  fileUrl: string
  thumbnailUrl: string | null
  description: string | null
  displayOrder: number
  isPrimary: boolean
  type: string
  createdAt: string
}

export interface AddAccommodationMediaRequest {
  fileUrl: string
  thumbnailUrl: string | null
  description: string | null
  displayOrder?: number
}

export interface AccommodationTypeMediaItem {
  id: string
  accommodationType: string
  fileUrl: string
  thumbnailUrl: string | null
  description: string | null
  displayOrder: number
  isPrimary: boolean
  createdAt: string
}

export const ACCOMMODATION_TYPE_VALUES = ['Lodge', 'Caravan', 'Tent', 'Bungalow', 'Motorhome'] as const
export type AccommodationTypeValue = (typeof ACCOMMODATION_TYPE_VALUES)[number]
