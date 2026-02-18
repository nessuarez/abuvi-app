// Camp location types matching backend DTOs

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
