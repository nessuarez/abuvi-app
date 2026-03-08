import { describe, it, expect, vi, beforeEach } from 'vitest'
import { usePayments } from '../usePayments'
import { api } from '@/utils/api'

vi.mock('@/utils/api', () => ({
  api: { get: vi.fn(), post: vi.fn(), put: vi.fn(), delete: vi.fn() }
}))

const mockPayment = {
  id: 'pay-1',
  registrationId: 'reg-1',
  installmentNumber: 1,
  amount: 225,
  dueDate: '2026-03-06T00:00:00Z',
  method: 'Transfer',
  status: 'Pending',
  transferConcept: 'CAMP-2026-GARCIA-1',
  proofFileUrl: null,
  proofFileName: null,
  proofUploadedAt: null,
  adminNotes: null,
  createdAt: '2026-03-06T00:00:00Z'
}

const mockPaymentWithProof = {
  ...mockPayment,
  status: 'PendingReview',
  proofFileUrl: 'https://storage.example.com/payment-proofs/proof.jpg',
  proofFileName: 'proof.jpg',
  proofUploadedAt: '2026-03-06T10:00:00Z'
}

describe('usePayments', () => {
  beforeEach(() => vi.clearAllMocks())

  describe('getRegistrationPayments', () => {
    it('should return payments for a registration', async () => {
      vi.mocked(api.get).mockResolvedValueOnce({
        data: { success: true, data: [mockPayment], error: null }
      })

      const { payments, getRegistrationPayments } = usePayments()
      const result = await getRegistrationPayments('reg-1')

      expect(result).toHaveLength(1)
      expect(result[0].id).toBe('pay-1')
      expect(payments.value).toHaveLength(1)
      expect(api.get).toHaveBeenCalledWith('/registrations/reg-1/payments')
    })

    it('should set error on failure', async () => {
      vi.mocked(api.get).mockRejectedValueOnce({
        response: { data: { error: { message: 'No encontrado' } } }
      })

      const { error, getRegistrationPayments } = usePayments()
      const result = await getRegistrationPayments('reg-1')

      expect(result).toHaveLength(0)
      expect(error.value).toBe('No encontrado')
    })
  })

  describe('getPaymentById', () => {
    it('should return a single payment', async () => {
      vi.mocked(api.get).mockResolvedValueOnce({
        data: { success: true, data: mockPayment, error: null }
      })

      const { getPaymentById } = usePayments()
      const result = await getPaymentById('pay-1')

      expect(result).not.toBeNull()
      expect(result!.id).toBe('pay-1')
      expect(api.get).toHaveBeenCalledWith('/payments/pay-1')
    })
  })

  describe('uploadProof', () => {
    it('should upload file and return updated payment', async () => {
      vi.mocked(api.post).mockResolvedValueOnce({
        data: { success: true, data: mockPaymentWithProof, error: null }
      })

      const { uploadProof } = usePayments()
      const file = new File(['test'], 'proof.jpg', { type: 'image/jpeg' })
      const result = await uploadProof('pay-1', file)

      expect(result).not.toBeNull()
      expect(result!.status).toBe('PendingReview')
      expect(result!.proofFileUrl).toBeTruthy()
      expect(api.post).toHaveBeenCalledWith(
        '/payments/pay-1/upload-proof',
        expect.any(FormData),
        { headers: { 'Content-Type': 'multipart/form-data' } }
      )
    })

    it('should handle 413 error for large files', async () => {
      vi.mocked(api.post).mockRejectedValueOnce({
        response: { status: 413, data: { error: { message: 'File too large' } } }
      })

      const { error, uploadProof } = usePayments()
      const file = new File(['test'], 'big.pdf', { type: 'application/pdf' })
      const result = await uploadProof('pay-1', file)

      expect(result).toBeNull()
      expect(error.value).toBe('El archivo supera el tamaño máximo permitido')
    })
  })

  describe('removeProof', () => {
    it('should remove proof and return updated payment', async () => {
      vi.mocked(api.delete).mockResolvedValueOnce({
        data: { success: true, data: mockPayment, error: null }
      })

      const { removeProof } = usePayments()
      const result = await removeProof('pay-1')

      expect(result).not.toBeNull()
      expect(result!.status).toBe('Pending')
      expect(api.delete).toHaveBeenCalledWith('/payments/pay-1/proof')
    })
  })

  describe('confirmPayment', () => {
    it('should confirm payment with optional notes', async () => {
      const confirmedPayment = { ...mockPayment, status: 'Completed' }
      vi.mocked(api.post).mockResolvedValueOnce({
        data: { success: true, data: confirmedPayment, error: null }
      })

      const { confirmPayment } = usePayments()
      const result = await confirmPayment('pay-1', 'Transferencia verificada')

      expect(result).not.toBeNull()
      expect(result!.status).toBe('Completed')
      expect(api.post).toHaveBeenCalledWith('/admin/payments/pay-1/confirm', {
        notes: 'Transferencia verificada'
      })
    })
  })

  describe('rejectPayment', () => {
    it('should reject payment with required notes', async () => {
      const rejectedPayment = { ...mockPayment, adminNotes: 'Importe incorrecto' }
      vi.mocked(api.post).mockResolvedValueOnce({
        data: { success: true, data: rejectedPayment, error: null }
      })

      const { rejectPayment } = usePayments()
      const result = await rejectPayment('pay-1', 'Importe incorrecto')

      expect(result).not.toBeNull()
      expect(api.post).toHaveBeenCalledWith('/admin/payments/pay-1/reject', {
        notes: 'Importe incorrecto'
      })
    })
  })

  describe('getPaymentSettings', () => {
    it('should return payment settings', async () => {
      const settings = {
        iban: 'ES1234567890123456789012',
        bankName: 'Banco Test',
        accountHolder: 'Asociación ABUVI',
        transferConceptPrefix: 'CAMP'
      }
      vi.mocked(api.get).mockResolvedValueOnce({
        data: { success: true, data: settings, error: null }
      })

      const { getPaymentSettings } = usePayments()
      const result = await getPaymentSettings()

      expect(result).not.toBeNull()
      expect(result!.iban).toBe('ES1234567890123456789012')
      expect(api.get).toHaveBeenCalledWith('/settings/payment')
    })
  })

  describe('updatePaymentSettings', () => {
    it('should update and return settings', async () => {
      const settings = {
        iban: 'ES1234567890123456789012',
        bankName: 'Banco Test',
        accountHolder: 'Asociación ABUVI',
        transferConceptPrefix: 'CAMP'
      }
      vi.mocked(api.put).mockResolvedValueOnce({
        data: { success: true, data: settings, error: null }
      })

      const { updatePaymentSettings } = usePayments()
      const result = await updatePaymentSettings(settings)

      expect(result).not.toBeNull()
      expect(api.put).toHaveBeenCalledWith('/settings/payment', settings)
    })
  })

  describe('loading state', () => {
    it('should set loading during API calls', async () => {
      let resolvePromise: (value: unknown) => void
      vi.mocked(api.get).mockReturnValueOnce(
        new Promise((resolve) => {
          resolvePromise = resolve
        })
      )

      const { loading, getRegistrationPayments } = usePayments()
      expect(loading.value).toBe(false)

      const promise = getRegistrationPayments('reg-1')
      expect(loading.value).toBe(true)

      resolvePromise!({ data: { success: true, data: [], error: null } })
      await promise

      expect(loading.value).toBe(false)
    })
  })

  describe('getPendingReviewPayments', () => {
    it('should return admin payments pending review', async () => {
      const adminPayment = {
        ...mockPaymentWithProof,
        familyUnitName: 'Familia García',
        campEditionName: 'Campamento 2026',
        confirmedByUserName: null,
        confirmedAt: null
      }
      vi.mocked(api.get).mockResolvedValueOnce({
        data: { success: true, data: [adminPayment], error: null }
      })

      const { getPendingReviewPayments } = usePayments()
      const result = await getPendingReviewPayments()

      expect(result).toHaveLength(1)
      expect(result[0].familyUnitName).toBe('Familia García')
      expect(api.get).toHaveBeenCalledWith('/admin/payments/pending-review')
    })
  })

  describe('getAllPayments', () => {
    it('should send filter params as query string', async () => {
      vi.mocked(api.get).mockResolvedValueOnce({
        data: {
          success: true,
          data: { items: [], totalCount: 0, page: 1, pageSize: 20 },
          error: null
        }
      })

      const { getAllPayments } = usePayments()
      await getAllPayments({ status: 'Completed', page: 2, pageSize: 20 })

      const calledUrl = vi.mocked(api.get).mock.calls[0][0]
      expect(calledUrl).toContain('Status=Completed')
      expect(calledUrl).toContain('Page=2')
    })
  })
})
