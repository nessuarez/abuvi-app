import { ref } from 'vue'
import { api } from '@/utils/api'
import type { ApiResponse } from '@/types/api'
import type {
  BlobUploadResult,
  BlobStorageStats,
  UploadBlobRequest
} from '@/types/blob-storage'

type ApiErrorShape = { response?: { data?: { error?: { message?: string } }; status?: number } }

export function useBlobStorage() {
  // Upload state
  const uploading = ref(false)
  const uploadError = ref<string | null>(null)

  // Delete state
  const deleting = ref(false)
  const deleteError = ref<string | null>(null)

  // Stats state
  const stats = ref<BlobStorageStats | null>(null)
  const statsLoading = ref(false)
  const statsError = ref<string | null>(null)

  const uploadFile = async (request: UploadBlobRequest): Promise<BlobUploadResult | null> => {
    uploading.value = true
    uploadError.value = null
    try {
      const formData = new FormData()
      formData.append('file', request.file)
      formData.append('folder', request.folder)
      if (request.contextId) formData.append('contextId', request.contextId)
      if (request.generateThumbnail != null)
        formData.append('generateThumbnail', String(request.generateThumbnail))

      const response = await api.post<ApiResponse<BlobUploadResult>>(
        '/blobs/upload',
        formData,
        { headers: { 'Content-Type': 'multipart/form-data' } }
      )
      if (response.data.success && response.data.data) return response.data.data
      uploadError.value = response.data.error?.message ?? 'Error al subir el archivo'
      return null
    } catch (err: unknown) {
      const apiErr = err as ApiErrorShape
      if (apiErr?.response?.status === 413) {
        uploadError.value = 'El archivo supera el tamaño máximo permitido'
      } else {
        uploadError.value =
          apiErr?.response?.data?.error?.message ?? 'Error al subir el archivo'
      }
      console.error('Failed to upload blob:', err)
      return null
    } finally {
      uploading.value = false
    }
  }

  const deleteBlobs = async (keys: string[]): Promise<boolean> => {
    deleting.value = true
    deleteError.value = null
    try {
      await api.delete('/blobs', { data: { blobKeys: keys } })
      return true
    } catch (err: unknown) {
      deleteError.value =
        (err as ApiErrorShape)?.response?.data?.error?.message ?? 'Error al eliminar los archivos'
      console.error('Failed to delete blobs:', err)
      return false
    } finally {
      deleting.value = false
    }
  }

  const fetchStats = async (): Promise<void> => {
    statsLoading.value = true
    statsError.value = null
    try {
      const response = await api.get<ApiResponse<BlobStorageStats>>('/blobs/stats')
      if (response.data.success && response.data.data) {
        stats.value = response.data.data
      } else {
        statsError.value = response.data.error?.message ?? 'Error al obtener estadísticas'
      }
    } catch (err: unknown) {
      statsError.value =
        (err as ApiErrorShape)?.response?.data?.error?.message ?? 'Error al obtener estadísticas'
      console.error('Failed to fetch blob stats:', err)
    } finally {
      statsLoading.value = false
    }
  }

  return {
    uploading,
    uploadError,
    uploadFile,
    deleting,
    deleteError,
    deleteBlobs,
    stats,
    statsLoading,
    statsError,
    fetchStats
  }
}
