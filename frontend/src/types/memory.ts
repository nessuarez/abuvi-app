import type { MediaItem } from './media-item'

export interface Memory {
  id: string
  authorUserId: string
  authorName: string
  title: string
  content: string
  year: number | null
  campLocationId: string | null
  isPublished: boolean
  isApproved: boolean
  createdAt: string
  updatedAt: string
  mediaItems: MediaItem[]
}

export interface CreateMemoryRequest {
  title: string
  content: string
  year?: number
  campLocationId?: string
}
