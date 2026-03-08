export enum FamilyRelationship {
  Parent = 'Parent',
  Child = 'Child',
  Sibling = 'Sibling',
  Spouse = 'Spouse',
  Other = 'Other'
}

// Response from backend
export interface FamilyUnitResponse {
  id: string
  name: string
  representativeUserId: string
  profilePhotoUrl: string | null
  createdAt: string
  updatedAt: string
  // Optional: populated in admin list endpoint (GET /api/family-units)
  representativeName?: string
  membersCount?: number
}

// Request to create family unit
export interface CreateFamilyUnitRequest {
  name: string
}

// Request to update family unit
export interface UpdateFamilyUnitRequest {
  name: string
}

// Response from backend
export interface FamilyMemberResponse {
  id: string
  familyUnitId: string
  userId: string | null
  firstName: string
  lastName: string
  dateOfBirth: string  // ISO 8601 date string (YYYY-MM-DD)
  relationship: FamilyRelationship
  documentNumber: string | null
  email: string | null
  phone: string | null
  hasMedicalNotes: boolean    // NEVER show actual content
  hasAllergies: boolean        // NEVER show actual content
  profilePhotoUrl: string | null
  createdAt: string
  updatedAt: string
}

// Request to create family member
export interface CreateFamilyMemberRequest {
  firstName: string
  lastName: string
  dateOfBirth: string  // ISO 8601 date string (YYYY-MM-DD)
  relationship: FamilyRelationship
  documentNumber?: string | null
  email?: string | null
  phone?: string | null
  medicalNotes?: string | null
  allergies?: string | null
}

// Request to update family member
export interface UpdateFamilyMemberRequest {
  firstName: string
  lastName: string
  dateOfBirth: string
  relationship: FamilyRelationship
  documentNumber?: string | null
  email?: string | null
  phone?: string | null
  medicalNotes?: string | null
  allergies?: string | null
}

// For displaying relationship in Spanish
export const FamilyRelationshipLabels: Record<FamilyRelationship, string> = {
  [FamilyRelationship.Parent]: 'Padre/Madre',
  [FamilyRelationship.Child]: 'Hijo/Hija',
  [FamilyRelationship.Sibling]: 'Hermano/Hermana',
  [FamilyRelationship.Spouse]: 'Cónyuge',
  [FamilyRelationship.Other]: 'Otro'
}
