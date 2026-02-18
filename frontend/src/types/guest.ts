export interface GuestResponse {
  id: string
  familyUnitId: string
  firstName: string
  lastName: string
  dateOfBirth: string // ISO 8601 date string (YYYY-MM-DD)
  documentNumber: string | null
  email: string | null
  phone: string | null
  hasMedicalNotes: boolean // Never expose actual content — backend encrypts
  hasAllergies: boolean // Never expose actual content — backend encrypts
  isActive: boolean
  createdAt: string
  updatedAt: string
}

export interface CreateGuestRequest {
  firstName: string
  lastName: string
  dateOfBirth: string // ISO 8601 date string (YYYY-MM-DD)
  documentNumber?: string | null
  email?: string | null
  phone?: string | null
  medicalNotes?: string | null
  allergies?: string | null
}

export type UpdateGuestRequest = CreateGuestRequest
