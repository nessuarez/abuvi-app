// Camp location types matching backend DTOs

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
  // Lightweight extended fields (present in list AND detail)
  formattedAddress: string | null
  phoneNumber: string | null
  websiteUrl: string | null
  googleMapsUrl: string | null
  googleRating: number | null
  googleRatingCount: number | null
  businessStatus: string | null
  pricePerAdult: number
  pricePerChild: number
  pricePerBaby: number
  isActive: boolean
  createdAt: string
  updatedAt: string
  editionCount?: number // For display in list view
  accommodationCapacity?: AccommodationCapacity | null
  calculatedTotalBedCapacity?: number | null
}

export interface CampPlacesPhoto {
  id: string
  photoReference: string | null
  photoUrl: string | null
  width: number
  height: number
  attributionName: string
  attributionUrl: string | null
  isPrimary: boolean
  displayOrder: number
}

export interface CampDetailResponse extends Camp {
  // Full address breakdown (detail-only)
  streetAddress: string | null
  locality: string | null
  administrativeArea: string | null
  postalCode: string | null
  country: string | null
  nationalPhoneNumber: string | null
  // Metadata
  placeTypes: string | null
  lastGoogleSyncAt: string | null
  // Photos from Google Places
  photos: CampPlacesPhoto[]
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
