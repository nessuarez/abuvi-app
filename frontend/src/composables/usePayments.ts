import { ref } from 'vue'
import { api } from '@/utils/api'
import type { ApiResponse } from '@/types/api'
import type {
  PaymentResponse,
  AdminPaymentResponse,
  AdminPaymentsPagedResponse,
  PaymentSettings,
  PaymentFilterParams,
  CreateManualPaymentRequest,
  UpdateManualPaymentRequest
} from '@/types/payment'

type ApiErrorShape = { response?: { data?: { error?: { message?: string } }; status?: number } }

function extractError(err: unknown, fallback: string): string {
  return (err as ApiErrorShape)?.response?.data?.error?.message ?? fallback
}

export function usePayments() {
  const payments = ref<PaymentResponse[]>([])
  const loading = ref(false)
  const error = ref<string | null>(null)

  const getRegistrationPayments = async (registrationId: string): Promise<PaymentResponse[]> => {
    loading.value = true
    error.value = null
    try {
      const response = await api.get<ApiResponse<PaymentResponse[]>>(
        `/registrations/${registrationId}/payments`
      )
      const data = response.data.success ? (response.data.data ?? []) : []
      payments.value = data
      return data
    } catch (err: unknown) {
      error.value = extractError(err, 'Error al cargar los pagos')
      console.error('Failed to fetch registration payments:', err)
      return []
    } finally {
      loading.value = false
    }
  }

  const getPaymentById = async (paymentId: string): Promise<PaymentResponse | null> => {
    loading.value = true
    error.value = null
    try {
      const response = await api.get<ApiResponse<PaymentResponse>>(`/payments/${paymentId}`)
      return response.data.success ? (response.data.data ?? null) : null
    } catch (err: unknown) {
      error.value = extractError(err, 'Error al cargar el pago')
      console.error('Failed to fetch payment:', err)
      return null
    } finally {
      loading.value = false
    }
  }

  const uploadProof = async (paymentId: string, file: File): Promise<PaymentResponse | null> => {
    loading.value = true
    error.value = null
    try {
      const formData = new FormData()
      formData.append('file', file)
      const response = await api.post<ApiResponse<PaymentResponse>>(
        `/payments/${paymentId}/upload-proof`,
        formData,
        { headers: { 'Content-Type': 'multipart/form-data' } }
      )
      if (response.data.success && response.data.data) return response.data.data
      error.value = response.data.error?.message ?? 'Error al subir el justificante'
      return null
    } catch (err: unknown) {
      const apiErr = err as ApiErrorShape
      if (apiErr?.response?.status === 413) {
        error.value = 'El archivo supera el tamaño máximo permitido'
      } else {
        error.value = extractError(err, 'Error al subir el justificante')
      }
      console.error('Failed to upload proof:', err)
      return null
    } finally {
      loading.value = false
    }
  }

  const removeProof = async (paymentId: string): Promise<PaymentResponse | null> => {
    loading.value = true
    error.value = null
    try {
      const response = await api.delete<ApiResponse<PaymentResponse>>(
        `/payments/${paymentId}/proof`
      )
      if (response.data.success && response.data.data) return response.data.data
      error.value = response.data.error?.message ?? 'Error al eliminar el justificante'
      return null
    } catch (err: unknown) {
      error.value = extractError(err, 'Error al eliminar el justificante')
      console.error('Failed to remove proof:', err)
      return null
    } finally {
      loading.value = false
    }
  }

  // Admin methods

  const getPendingReviewPayments = async (): Promise<AdminPaymentResponse[]> => {
    loading.value = true
    error.value = null
    try {
      const response = await api.get<ApiResponse<AdminPaymentResponse[]>>(
        '/admin/payments/pending-review'
      )
      return response.data.success ? (response.data.data ?? []) : []
    } catch (err: unknown) {
      error.value = extractError(err, 'Error al cargar pagos pendientes de revisión')
      console.error('Failed to fetch pending review payments:', err)
      return []
    } finally {
      loading.value = false
    }
  }

  const getAllPayments = async (
    filter: PaymentFilterParams
  ): Promise<AdminPaymentsPagedResponse | null> => {
    loading.value = true
    error.value = null
    try {
      const params = new URLSearchParams()
      if (filter.status) params.append('Status', filter.status)
      if (filter.campEditionId) params.append('CampEditionId', filter.campEditionId)
      if (filter.installmentNumber) params.append('InstallmentNumber', String(filter.installmentNumber))
      if (filter.fromDate) params.append('FromDate', filter.fromDate)
      if (filter.toDate) params.append('ToDate', filter.toDate)
      if (filter.page) params.append('Page', String(filter.page))
      if (filter.pageSize) params.append('PageSize', String(filter.pageSize))

      const response = await api.get<ApiResponse<AdminPaymentsPagedResponse>>(
        `/admin/payments?${params.toString()}`
      )
      return response.data.success ? (response.data.data ?? null) : null
    } catch (err: unknown) {
      error.value = extractError(err, 'Error al cargar los pagos')
      console.error('Failed to fetch all payments:', err)
      return null
    } finally {
      loading.value = false
    }
  }

  const confirmPayment = async (
    paymentId: string,
    notes?: string
  ): Promise<PaymentResponse | null> => {
    loading.value = true
    error.value = null
    try {
      const response = await api.post<ApiResponse<PaymentResponse>>(
        `/admin/payments/${paymentId}/confirm`,
        { notes }
      )
      if (response.data.success && response.data.data) return response.data.data
      error.value = response.data.error?.message ?? 'Error al confirmar el pago'
      return null
    } catch (err: unknown) {
      error.value = extractError(err, 'Error al confirmar el pago')
      console.error('Failed to confirm payment:', err)
      return null
    } finally {
      loading.value = false
    }
  }

  const rejectPayment = async (
    paymentId: string,
    notes: string
  ): Promise<PaymentResponse | null> => {
    loading.value = true
    error.value = null
    try {
      const response = await api.post<ApiResponse<PaymentResponse>>(
        `/admin/payments/${paymentId}/reject`,
        { notes }
      )
      if (response.data.success && response.data.data) return response.data.data
      error.value = response.data.error?.message ?? 'Error al rechazar el pago'
      return null
    } catch (err: unknown) {
      error.value = extractError(err, 'Error al rechazar el pago')
      console.error('Failed to reject payment:', err)
      return null
    } finally {
      loading.value = false
    }
  }

  // Manual payment methods (admin only)

  const createManualPayment = async (
    registrationId: string,
    request: CreateManualPaymentRequest
  ): Promise<AdminPaymentResponse | null> => {
    loading.value = true
    error.value = null
    try {
      const response = await api.post<ApiResponse<AdminPaymentResponse>>(
        `/admin/registrations/${registrationId}/payments/manual`,
        request
      )
      if (response.data.success && response.data.data) return response.data.data
      error.value = response.data.error?.message ?? 'Error al crear el pago manual'
      return null
    } catch (err: unknown) {
      error.value = extractError(err, 'Error al crear el pago manual')
      console.error('Failed to create manual payment:', err)
      return null
    } finally {
      loading.value = false
    }
  }

  const updateManualPayment = async (
    paymentId: string,
    request: UpdateManualPaymentRequest
  ): Promise<AdminPaymentResponse | null> => {
    loading.value = true
    error.value = null
    try {
      const response = await api.put<ApiResponse<AdminPaymentResponse>>(
        `/admin/payments/${paymentId}/manual`,
        request
      )
      if (response.data.success && response.data.data) return response.data.data
      error.value = response.data.error?.message ?? 'Error al actualizar el pago manual'
      return null
    } catch (err: unknown) {
      error.value = extractError(err, 'Error al actualizar el pago manual')
      console.error('Failed to update manual payment:', err)
      return null
    } finally {
      loading.value = false
    }
  }

  const deleteManualPayment = async (paymentId: string): Promise<boolean> => {
    loading.value = true
    error.value = null
    try {
      const response = await api.delete<ApiResponse<object>>(
        `/admin/payments/${paymentId}/manual`
      )
      if (response.data.success) return true
      error.value = response.data.error?.message ?? 'Error al eliminar el pago manual'
      return false
    } catch (err: unknown) {
      error.value = extractError(err, 'Error al eliminar el pago manual')
      console.error('Failed to delete manual payment:', err)
      return false
    } finally {
      loading.value = false
    }
  }

  // Settings

  const getPaymentSettings = async (): Promise<PaymentSettings | null> => {
    try {
      const response = await api.get<ApiResponse<PaymentSettings>>('/settings/payment')
      return response.data.success ? (response.data.data ?? null) : null
    } catch (err: unknown) {
      console.error('Failed to fetch payment settings:', err)
      return null
    }
  }

  const updatePaymentSettings = async (
    settings: PaymentSettings
  ): Promise<PaymentSettings | null> => {
    loading.value = true
    error.value = null
    try {
      const response = await api.put<ApiResponse<PaymentSettings>>('/settings/payment', settings)
      if (response.data.success && response.data.data) return response.data.data
      error.value = response.data.error?.message ?? 'Error al guardar la configuración'
      return null
    } catch (err: unknown) {
      error.value = extractError(err, 'Error al guardar la configuración de pagos')
      console.error('Failed to update payment settings:', err)
      return null
    } finally {
      loading.value = false
    }
  }

  return {
    payments,
    loading,
    error,
    getRegistrationPayments,
    getPaymentById,
    uploadProof,
    removeProof,
    getPendingReviewPayments,
    getAllPayments,
    confirmPayment,
    rejectPayment,
    createManualPayment,
    updateManualPayment,
    deleteManualPayment,
    getPaymentSettings,
    updatePaymentSettings
  }
}
