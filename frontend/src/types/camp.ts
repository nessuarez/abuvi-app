// Camp location types matching backend DTOs

export interface Camp {
  id: string
  name: string
  description: string
  latitude: number
  longitude: number
  basePriceAdult: number
  basePriceChild: number
  basePriceBaby: number
  status: CampStatus
  createdAt: string
  updatedAt: string
  editionCount?: number // For display in list view
}

export type CampStatus = 'Active' | 'Inactive' | 'HistoricalArchive'

export interface CreateCampRequest {
  name: string
  description: string
  latitude: number
  longitude: number
  basePriceAdult: number
  basePriceChild: number
  basePriceBaby: number
  status: CampStatus
}

export interface UpdateCampRequest extends CreateCampRequest {
  id: string
}

export interface CampLocation {
  latitude: number
  longitude: number
  name: string
  year?: number
}
