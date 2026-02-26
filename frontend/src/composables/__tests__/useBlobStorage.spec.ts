import { describe, it, expect, vi, beforeEach } from 'vitest'
import { useBlobStorage } from '@/composables/useBlobStorage'
import { api } from '@/utils/api'
import type { BlobStorageStats, BlobUploadResult } from '@/types/blob-storage'

vi.mock('@/utils/api', () => ({
  api: {
    post: vi.fn(),
    delete: vi.fn(),
    get: vi.fn()
  }
}))

const makeUploadResult = (overrides: Partial<BlobUploadResult> = {}): BlobUploadResult => ({
  fileUrl: 'https://cdn.example.com/photos/abc/photo.jpg',
  thumbnailUrl: 'https://cdn.example.com/photos/abc/thumbs/photo.webp',
  fileName: 'photo.jpg',
  contentType: 'image/jpeg',
  sizeBytes: 1024,
  ...overrides
})

const makeStats = (overrides: Partial<BlobStorageStats> = {}): BlobStorageStats => ({
  totalObjects: 10,
  totalSizeBytes: 1_048_576,
  totalSizeHumanReadable: '1 MB',
  quotaBytes: 107_374_182_400,
  usedPct: 0.001,
  freeBytes: 107_373_133_824,
  byFolder: {
    photos: { objects: 5, sizeBytes: 524_288 },
    'media-items': { objects: 3, sizeBytes: 393_216 },
    'camp-locations': { objects: 2, sizeBytes: 131_072 },
    'camp-photos': { objects: 0, sizeBytes: 0 }
  },
  ...overrides
})

describe('useBlobStorage', () => {
  beforeEach(() => {
    vi.clearAllMocks()
  })

  // ── uploadFile ───────────────────────────────────────────────────────────────

  describe('uploadFile', () => {
    it('uploadFile_withValidImageFile_returnsUploadResult', async () => {
      // Arrange
      const result = makeUploadResult()
      vi.mocked(api.post).mockResolvedValue({
        data: { success: true, data: result, error: null }
      })
      const { uploadFile } = useBlobStorage()
      const file = new File(['content'], 'photo.jpg', { type: 'image/jpeg' })

      // Act
      const returned = await uploadFile({ file, folder: 'photos', generateThumbnail: true })

      // Assert
      expect(returned).toEqual(result)
      expect(returned?.fileUrl).toBe('https://cdn.example.com/photos/abc/photo.jpg')
      expect(returned?.thumbnailUrl).not.toBeNull()
    })

    it('uploadFile_whenApiFails_setsUploadError', async () => {
      // Arrange
      vi.mocked(api.post).mockRejectedValue({
        response: { data: { error: { message: 'Tipo de archivo no permitido' } }, status: 400 }
      })
      const { uploadFile, uploadError } = useBlobStorage()
      const file = new File(['content'], 'photo.jpg', { type: 'image/jpeg' })

      // Act
      const result = await uploadFile({ file, folder: 'photos' })

      // Assert
      expect(result).toBeNull()
      expect(uploadError.value).toBe('Tipo de archivo no permitido')
    })

    it('uploadFile_withGenerateThumbnailFalse_appendsGenerateThumbnailField', async () => {
      // Arrange
      vi.mocked(api.post).mockResolvedValue({
        data: { success: true, data: makeUploadResult({ thumbnailUrl: null }), error: null }
      })
      const { uploadFile } = useBlobStorage()
      const file = new File(['content'], 'photo.jpg', { type: 'image/jpeg' })

      // Act
      await uploadFile({ file, folder: 'photos', generateThumbnail: false })

      // Assert - FormData was sent with generateThumbnail=false
      expect(api.post).toHaveBeenCalledWith(
        '/blobs/upload',
        expect.any(FormData),
        expect.objectContaining({ headers: { 'Content-Type': 'multipart/form-data' } })
      )
      const formData: FormData = vi.mocked(api.post).mock.calls[0][1]
      expect(formData.get('generateThumbnail')).toBe('false')
    })

    it('uploadFile_sendsMultipartFormData', async () => {
      // Arrange
      vi.mocked(api.post).mockResolvedValue({
        data: { success: true, data: makeUploadResult(), error: null }
      })
      const { uploadFile } = useBlobStorage()
      const file = new File(['content'], 'audio.mp3', { type: 'audio/mpeg' })
      const contextId = 'ctx-123'

      // Act
      await uploadFile({ file, folder: 'media-items', contextId })

      // Assert
      const formData: FormData = vi.mocked(api.post).mock.calls[0][1]
      expect(formData.get('file')).toBe(file)
      expect(formData.get('folder')).toBe('media-items')
      expect(formData.get('contextId')).toBe('ctx-123')
    })

    it('uploadFile_uploading_isTrueDuringRequestAndFalseAfter', async () => {
      // Arrange
      let resolveFn!: (value: unknown) => void
      vi.mocked(api.post).mockReturnValue(new Promise((r) => { resolveFn = r }))
      const { uploadFile, uploading } = useBlobStorage()
      const file = new File(['content'], 'photo.jpg', { type: 'image/jpeg' })

      // Act
      const uploadPromise = uploadFile({ file, folder: 'photos' })
      expect(uploading.value).toBe(true)

      resolveFn({ data: { success: true, data: makeUploadResult(), error: null } })
      await uploadPromise

      // Assert
      expect(uploading.value).toBe(false)
    })

    it('uploadFile_when413_setsFileTooLargeError', async () => {
      // Arrange
      vi.mocked(api.post).mockRejectedValue({
        response: { status: 413, data: {} }
      })
      const { uploadFile, uploadError } = useBlobStorage()
      const file = new File(['content'], 'big.jpg', { type: 'image/jpeg' })

      // Act
      await uploadFile({ file, folder: 'photos' })

      // Assert
      expect(uploadError.value).toBe('El archivo supera el tamaño máximo permitido')
    })
  })

  // ── deleteBlobs ───────────────────────────────────────────────────────────────

  describe('deleteBlobs', () => {
    it('deleteBlobs_withValidKeys_callsApiDeleteWithKeys', async () => {
      // Arrange
      vi.mocked(api.delete).mockResolvedValue({ data: null })
      const { deleteBlobs } = useBlobStorage()
      const keys = ['photos/abc/test.jpg', 'photos/abc/thumbs/test.webp']

      // Act
      const result = await deleteBlobs(keys)

      // Assert
      expect(result).toBe(true)
      expect(api.delete).toHaveBeenCalledWith('/blobs', { data: { blobKeys: keys } })
    })

    it('deleteBlobs_whenApiFails_setsDeleteError', async () => {
      // Arrange
      vi.mocked(api.delete).mockRejectedValue({
        response: { data: { error: { message: 'No autorizado' } } }
      })
      const { deleteBlobs, deleteError } = useBlobStorage()

      // Act
      const result = await deleteBlobs(['photos/abc/test.jpg'])

      // Assert
      expect(result).toBe(false)
      expect(deleteError.value).toBe('No autorizado')
    })
  })

  // ── fetchStats ────────────────────────────────────────────────────────────────

  describe('fetchStats', () => {
    it('fetchStats_withQuotaConfigured_populatesStats', async () => {
      // Arrange
      const statsData = makeStats()
      vi.mocked(api.get).mockResolvedValue({
        data: { success: true, data: statsData, error: null }
      })
      const { fetchStats, stats } = useBlobStorage()

      // Act
      await fetchStats()

      // Assert
      expect(stats.value).toEqual(statsData)
      expect(stats.value?.usedPct).not.toBeNull()
      expect(stats.value?.freeBytes).not.toBeNull()
    })

    it('fetchStats_whenApiFails_setsStatsError', async () => {
      // Arrange
      vi.mocked(api.get).mockRejectedValue({
        response: { data: { error: { message: 'Acceso denegado' } } }
      })
      const { fetchStats, stats, statsError } = useBlobStorage()

      // Act
      await fetchStats()

      // Assert
      expect(stats.value).toBeNull()
      expect(statsError.value).toBe('Acceso denegado')
    })

    it('fetchStats_callsCorrectEndpoint', async () => {
      // Arrange
      vi.mocked(api.get).mockResolvedValue({
        data: { success: true, data: makeStats(), error: null }
      })
      const { fetchStats } = useBlobStorage()

      // Act
      await fetchStats()

      // Assert
      expect(api.get).toHaveBeenCalledWith('/blobs/stats')
    })
  })
})
