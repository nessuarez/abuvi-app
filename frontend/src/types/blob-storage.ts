export type BlobFolder = 'photos' | 'media-items' | 'camp-locations' | 'camp-photos'

export interface UploadBlobRequest {
  file: File
  folder: BlobFolder
  contextId?: string
  generateThumbnail?: boolean
}

export interface BlobUploadResult {
  fileUrl: string
  thumbnailUrl: string | null
  fileName: string
  contentType: string
  sizeBytes: number
}

export interface DeleteBlobsRequest {
  blobKeys: string[]
}

export interface FolderStats {
  objects: number
  sizeBytes: number
}

export interface BlobStorageStats {
  totalObjects: number
  totalSizeBytes: number
  totalSizeHumanReadable: string
  quotaBytes: number | null
  usedPct: number | null
  freeBytes: number | null
  byFolder: Record<BlobFolder, FolderStats>
}
