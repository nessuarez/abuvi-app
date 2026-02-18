// Camp photo types matching backend DTOs

export interface CampPhoto {
  id: string
  campId: string
  url: string
  description?: string | null
  displayOrder: number
  isPrimary: boolean
  isOriginal: boolean
  createdAt: string
  updatedAt: string
}

export interface AddCampPhotoRequest {
  url: string
  description?: string | null
  displayOrder: number
  isPrimary: boolean
}

export interface UpdateCampPhotoRequest {
  url: string
  description?: string | null
  displayOrder: number
  isPrimary: boolean
}

export interface PhotoOrderItem {
  id: string
  displayOrder: number
}

export interface ReorderCampPhotosRequest {
  photos: PhotoOrderItem[]
}
