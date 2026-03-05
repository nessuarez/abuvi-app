import { ref } from 'vue'
import { api } from '@/utils/api'
import type { ApiResponse } from '@/types/api'
import type { Memory, CreateMemoryRequest } from '@/types/memory'

type ApiErrorShape = { response?: { data?: { error?: { message?: string } }; status?: number } }

export function useMemories() {
  const memories = ref<Memory[]>([])
  const loading = ref(false)
  const error = ref<string | null>(null)
  const creating = ref(false)
  const createError = ref<string | null>(null)

  const fetchMemories = async (params?: {
    year?: number
    approved?: boolean
  }): Promise<void> => {
    loading.value = true
    error.value = null
    try {
      const query = new URLSearchParams()
      if (params?.year != null) query.set('year', String(params.year))
      if (params?.approved != null) query.set('approved', String(params.approved))

      const qs = query.toString()
      const url = `/memories${qs ? `?${qs}` : ''}`
      const response = await api.get<ApiResponse<Memory[]>>(url)

      if (response.data.success && response.data.data) {
        memories.value = response.data.data
      } else {
        error.value = response.data.error?.message ?? 'Error al obtener los recuerdos'
      }
    } catch (err: unknown) {
      error.value =
        (err as ApiErrorShape)?.response?.data?.error?.message ??
        'Error al obtener los recuerdos'
      console.error('Failed to fetch memories:', err)
    } finally {
      loading.value = false
    }
  }

  const createMemory = async (request: CreateMemoryRequest): Promise<Memory | null> => {
    creating.value = true
    createError.value = null
    try {
      const response = await api.post<ApiResponse<Memory>>('/memories', request)
      if (response.data.success && response.data.data) {
        memories.value.push(response.data.data)
        return response.data.data
      }
      createError.value = response.data.error?.message ?? 'Error al crear el recuerdo'
      return null
    } catch (err: unknown) {
      createError.value =
        (err as ApiErrorShape)?.response?.data?.error?.message ?? 'Error al crear el recuerdo'
      console.error('Failed to create memory:', err)
      return null
    } finally {
      creating.value = false
    }
  }

  const approveMemory = async (id: string): Promise<boolean> => {
    loading.value = true
    error.value = null
    try {
      await api.patch<ApiResponse<Memory>>(`/memories/${id}/approve`)
      return true
    } catch (err: unknown) {
      error.value =
        (err as ApiErrorShape)?.response?.data?.error?.message ?? 'Error al aprobar el recuerdo'
      console.error('Failed to approve memory:', err)
      return false
    } finally {
      loading.value = false
    }
  }

  const rejectMemory = async (id: string): Promise<boolean> => {
    loading.value = true
    error.value = null
    try {
      await api.patch<ApiResponse<Memory>>(`/memories/${id}/reject`)
      return true
    } catch (err: unknown) {
      error.value =
        (err as ApiErrorShape)?.response?.data?.error?.message ?? 'Error al rechazar el recuerdo'
      console.error('Failed to reject memory:', err)
      return false
    } finally {
      loading.value = false
    }
  }

  return {
    memories,
    loading,
    error,
    creating,
    createError,
    fetchMemories,
    createMemory,
    approveMemory,
    rejectMemory
  }
}
