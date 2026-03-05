export type MediaItemType = 'Photo' | 'Video' | 'Audio' | 'Interview' | 'Document'

export interface MediaItem {
  id: string
  uploadedByUserId: string
  uploadedByName: string
  fileUrl: string
  thumbnailUrl: string | null
  type: MediaItemType
  title: string
  description: string | null
  year: number | null
  decade: string | null
  memoryId: string | null
  context: string | null
  isPublished: boolean
  isApproved: boolean
  createdAt: string
}

export interface CreateMediaItemRequest {
  fileUrl: string
  thumbnailUrl?: string | null
  type: MediaItemType
  title: string
  description?: string
  year?: number
  memoryId?: string
  campLocationId?: string
  context?: string
}
