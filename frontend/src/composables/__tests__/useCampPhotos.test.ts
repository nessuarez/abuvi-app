import { describe, it, expect, vi, beforeEach } from 'vitest'
import { useCampPhotos } from '@/composables/useCampPhotos'
import { api } from '@/utils/api'
import type { CampPhoto } from '@/types/camp-photo'

vi.mock('@/utils/api', () => ({
  api: {
    get: vi.fn(),
    post: vi.fn(),
    put: vi.fn(),
    delete: vi.fn()
  }
}))

const campId = 'camp-123'

const makePhoto = (overrides: Partial<CampPhoto> = {}): CampPhoto => ({
  id: 'photo-1',
  campId,
  url: 'https://example.com/photo.jpg',
  description: 'A test photo',
  displayOrder: 0,
  isPrimary: false,
  isOriginal: false,
  createdAt: '2025-01-01T00:00:00Z',
  updatedAt: '2025-01-01T00:00:00Z',
  ...overrides
})

describe('useCampPhotos', () => {
  beforeEach(() => {
    vi.clearAllMocks()
  })

  describe('addPhoto', () => {
    it('should add a photo and return it on success', async () => {
      // Arrange
      const newPhoto = makePhoto({ id: 'photo-new' })
      vi.mocked(api.post).mockResolvedValue({
        data: { success: true, data: newPhoto, error: null }
      })

      // Act
      const { addPhoto, photos } = useCampPhotos()
      const result = await addPhoto(campId, {
        url: newPhoto.url,
        description: newPhoto.description,
        displayOrder: 0,
        isPrimary: false
      })

      // Assert
      expect(result).toEqual(newPhoto)
      expect(photos.value).toContain(newPhoto)
      expect(api.post).toHaveBeenCalledWith(`/camps/${campId}/photos`, expect.any(Object))
    })

    it('should return null and set error on failure', async () => {
      // Arrange
      vi.mocked(api.post).mockRejectedValue({
        response: { data: { error: { message: 'El campamento no se encontró' } } }
      })

      // Act
      const { addPhoto, error } = useCampPhotos()
      const result = await addPhoto(campId, {
        url: 'https://example.com/photo.jpg',
        description: null,
        displayOrder: 0,
        isPrimary: false
      })

      // Assert
      expect(result).toBeNull()
      expect(error.value).toBe('El campamento no se encontró')
    })

    it('should use fallback error message when API provides no message', async () => {
      // Arrange
      vi.mocked(api.post).mockRejectedValue(new Error('Network error'))

      // Act
      const { addPhoto, error } = useCampPhotos()
      await addPhoto(campId, { url: 'https://example.com/photo.jpg', description: null, displayOrder: 0, isPrimary: false })

      // Assert
      expect(error.value).toBe('Error al añadir la foto')
    })

    it('should reset loading to false after completion', async () => {
      // Arrange
      vi.mocked(api.post).mockResolvedValue({
        data: { success: true, data: makePhoto(), error: null }
      })

      // Act
      const { addPhoto, loading } = useCampPhotos()
      await addPhoto(campId, { url: 'https://example.com/photo.jpg', description: null, displayOrder: 0, isPrimary: false })

      // Assert
      expect(loading.value).toBe(false)
    })
  })

  describe('updatePhoto', () => {
    it('should update the photo in the local array on success', async () => {
      // Arrange
      const original = makePhoto({ id: 'photo-1', description: 'Old description' })
      const updated = makePhoto({ id: 'photo-1', description: 'New description' })
      vi.mocked(api.put).mockResolvedValue({
        data: { success: true, data: updated, error: null }
      })

      // Act
      const { updatePhoto, photos } = useCampPhotos()
      photos.value = [original]
      const result = await updatePhoto(campId, 'photo-1', {
        url: updated.url,
        description: 'New description',
        displayOrder: 0,
        isPrimary: false
      })

      // Assert
      expect(result).toEqual(updated)
      expect(photos.value[0].description).toBe('New description')
    })

    it('should return null and set error on failure', async () => {
      // Arrange
      vi.mocked(api.put).mockRejectedValue(new Error('Not found'))

      // Act
      const { updatePhoto, error } = useCampPhotos()
      const result = await updatePhoto(campId, 'photo-1', {
        url: 'https://example.com/photo.jpg',
        description: null,
        displayOrder: 0,
        isPrimary: false
      })

      // Assert
      expect(result).toBeNull()
      expect(error.value).toBe('Error al actualizar la foto')
    })
  })

  describe('deletePhoto', () => {
    it('should remove the photo from the local array on success', async () => {
      // Arrange
      const photo = makePhoto({ id: 'photo-1' })
      vi.mocked(api.delete).mockResolvedValue({ data: null })

      // Act
      const { deletePhoto, photos } = useCampPhotos()
      photos.value = [photo]
      const result = await deletePhoto(campId, 'photo-1')

      // Assert
      expect(result).toBe(true)
      expect(photos.value).toHaveLength(0)
    })

    it('should return false and set error on failure', async () => {
      // Arrange
      vi.mocked(api.delete).mockRejectedValue(new Error('Not found'))

      // Act
      const { deletePhoto, error } = useCampPhotos()
      const result = await deletePhoto(campId, 'photo-1')

      // Assert
      expect(result).toBe(false)
      expect(error.value).toBe('Error al eliminar la foto')
    })
  })

  describe('setPrimaryPhoto', () => {
    it('should set isPrimary=true on the target photo', async () => {
      // Arrange
      const photo1 = makePhoto({ id: 'photo-1', isPrimary: true })
      const photo2 = makePhoto({ id: 'photo-2', isPrimary: false })
      const updatedPhoto2 = makePhoto({ id: 'photo-2', isPrimary: true })

      vi.mocked(api.post).mockResolvedValue({
        data: { success: true, data: updatedPhoto2, error: null }
      })

      // Act
      const { setPrimaryPhoto, photos } = useCampPhotos()
      photos.value = [photo1, photo2]
      const result = await setPrimaryPhoto(campId, 'photo-2')

      // Assert
      expect(result).toEqual(updatedPhoto2)
      expect(photos.value.find((p) => p.id === 'photo-2')?.isPrimary).toBe(true)
    })

    it('should set isPrimary=false on all other photos', async () => {
      // Arrange
      const photo1 = makePhoto({ id: 'photo-1', isPrimary: true })
      const photo2 = makePhoto({ id: 'photo-2', isPrimary: false })

      vi.mocked(api.post).mockResolvedValue({
        data: { success: true, data: makePhoto({ id: 'photo-2', isPrimary: true }), error: null }
      })

      // Act
      const { setPrimaryPhoto, photos } = useCampPhotos()
      photos.value = [photo1, photo2]
      await setPrimaryPhoto(campId, 'photo-2')

      // Assert
      expect(photos.value.find((p) => p.id === 'photo-1')?.isPrimary).toBe(false)
    })

    it('should return null and set error on failure', async () => {
      // Arrange
      vi.mocked(api.post).mockRejectedValue(new Error('Not found'))

      // Act
      const { setPrimaryPhoto, error } = useCampPhotos()
      const result = await setPrimaryPhoto(campId, 'photo-1')

      // Assert
      expect(result).toBeNull()
      expect(error.value).toBe('Error al establecer la foto principal')
    })
  })

  describe('reorderPhotos', () => {
    it('should call the reorder endpoint with correct payload', async () => {
      // Arrange
      vi.mocked(api.put).mockResolvedValue({ data: { success: true, data: null } })
      const request = {
        photos: [
          { id: 'photo-1', displayOrder: 0 },
          { id: 'photo-2', displayOrder: 1 }
        ]
      }

      // Act
      const { reorderPhotos } = useCampPhotos()
      await reorderPhotos(campId, request)

      // Assert
      expect(api.put).toHaveBeenCalledWith(`/camps/${campId}/photos/reorder`, request)
    })

    it('should return true on success', async () => {
      // Arrange
      vi.mocked(api.put).mockResolvedValue({ data: { success: true, data: null } })

      // Act
      const { reorderPhotos } = useCampPhotos()
      const result = await reorderPhotos(campId, { photos: [] })

      // Assert
      expect(result).toBe(true)
    })

    it('should return false and set error on failure', async () => {
      // Arrange
      vi.mocked(api.put).mockRejectedValue(new Error('Server error'))

      // Act
      const { reorderPhotos, error } = useCampPhotos()
      const result = await reorderPhotos(campId, { photos: [] })

      // Assert
      expect(result).toBe(false)
      expect(error.value).toBe('Error al reordenar las fotos')
    })
  })
})
