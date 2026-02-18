// Camp location types matching backend DTOs
import type { CampPhoto } from './camp-photo'

export interface SharedRoomInfo {
  quantity: number
  bedsPerRoom: number
  hasBathroom: boolean
  hasShower: boolean
  notes?: string | null
}

export interface AccommodationCapacity {
  privateRoomsWithBathroom?: number | null
  privateRoomsSharedBathroom?: number | null
  sharedRooms?: SharedRoomInfo[] | null
  bungalows?: number | null
  campOwnedTents?: number | null
  memberTentAreaSquareMeters?: number | null
  memberTentCapacityEstimate?: number | null
  motorhomeSpots?: number | null
  notes?: string | null
}

export interface Camp {
  id: string
  name: string
  description: string | null
  location: string | null
  latitude: number | null
  longitude: number | null
  googlePlaceId: string | null
  pricePerAdult: number
  pricePerChild: number
  pricePerBaby: number
  isActive: boolean
  createdAt: string
  updatedAt: string
  editionCount?: number // For display in list view
  accommodationCapacity?: AccommodationCapacity | null
  calculatedTotalBedCapacity?: number | null
  photos?: CampPhoto[]
}

export interface CreateCampRequest {
  name: string
  description: string | null
  location: string | null
  latitude: number | null
  longitude: number | null
  googlePlaceId: string | null
  pricePerAdult: number
  pricePerChild: number
  pricePerBaby: number
  accommodationCapacity?: AccommodationCapacity | null
}

export interface UpdateCampRequest extends CreateCampRequest {
  isActive: boolean
}

export interface CampLocation {
  latitude: number
  longitude: number
  name: string
  year?: number
}
