import type { PaymentMethod, PaymentStatus } from './registration'

export interface PaymentConceptLine {
  personFullName: string
  ageCategory: string
  attendancePeriod: string
  individualAmount: number
  amountInPayment: number
  percentage: number
}

export interface PaymentExtraConceptLine {
  extraName: string
  quantity: number
  unitPrice: number
  totalAmount: number
  userInput: string | null
  pricingType: string
}

export interface PaymentResponse {
  id: string
  registrationId: string
  installmentNumber: number
  amount: number
  dueDate: string | null
  method: PaymentMethod
  status: PaymentStatus
  transferConcept: string | null
  proofFileUrl: string | null
  proofFileName: string | null
  proofUploadedAt: string | null
  adminNotes: string | null
  createdAt: string
  conceptLines: PaymentConceptLine[] | null
  extraConceptLines: PaymentExtraConceptLine[] | null
}

export interface AdminPaymentResponse extends PaymentResponse {
  familyUnitName: string
  campEditionName: string
  confirmedByUserName: string | null
  confirmedAt: string | null
}

export interface PaymentSettings {
  iban: string
  bankName: string
  accountHolder: string
  firstInstallmentDaysBefore: number
  secondInstallmentDaysBefore: number
  extrasInstallmentDaysFromCampStart: number
  transferConceptPrefix: string
}

export interface ConfirmPaymentRequest {
  notes?: string
}

export interface RejectPaymentRequest {
  notes: string
}

export interface PaymentFilterParams {
  status?: PaymentStatus
  campEditionId?: string
  fromDate?: string
  toDate?: string
  page?: number
  pageSize?: number
}

export interface AdminPaymentsPagedResponse {
  items: AdminPaymentResponse[]
  totalCount: number
  page: number
  pageSize: number
}
