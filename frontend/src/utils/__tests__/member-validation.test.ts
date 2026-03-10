import { describe, it, expect } from 'vitest'
import { getMemberDataWarnings, getWarningMessage } from '@/utils/member-validation'
import { FamilyRelationship, type FamilyMemberResponse } from '@/types/family-unit'

const baseMember: FamilyMemberResponse = {
  id: 'member-1',
  familyUnitId: 'unit-1',
  firstName: 'Juan',
  lastName: 'García',
  dateOfBirth: '1990-05-15',
  relationship: FamilyRelationship.Parent,
  documentNumber: '12345678A',
  email: 'juan@example.com',
  phone: '+34612345678',
  hasMedicalNotes: false,
  hasAllergies: false,
  profilePhotoUrl: null,
  userId: null,
  createdAt: '2026-01-01T00:00:00Z',
  updatedAt: '2026-01-01T00:00:00Z'
}

describe('getMemberDataWarnings', () => {
  it('returns null for adult with all data complete', () => {
    expect(getMemberDataWarnings(baseMember, true)).toBeNull()
  })

  it('returns warning for adult missing DNI', () => {
    const member = { ...baseMember, documentNumber: null }
    const result = getMemberDataWarnings(member, true)
    expect(result).toEqual({ missingDni: true, missingEmail: false, invalidBirthDate: false })
  })

  it('returns warning for adult with empty string DNI', () => {
    const member = { ...baseMember, documentNumber: '  ' }
    const result = getMemberDataWarnings(member, true)
    expect(result).toEqual({ missingDni: true, missingEmail: false, invalidBirthDate: false })
  })

  it('returns warning for adult missing email', () => {
    const member = { ...baseMember, email: null }
    const result = getMemberDataWarnings(member, true)
    expect(result).toEqual({ missingDni: false, missingEmail: true, invalidBirthDate: false })
  })

  it('returns warning for adult with empty string email', () => {
    const member = { ...baseMember, email: '' }
    const result = getMemberDataWarnings(member, true)
    expect(result).toEqual({ missingDni: false, missingEmail: true, invalidBirthDate: false })
  })

  it('returns warning for adult missing both DNI and email', () => {
    const member = { ...baseMember, documentNumber: null, email: null }
    const result = getMemberDataWarnings(member, true)
    expect(result).toEqual({ missingDni: true, missingEmail: true, invalidBirthDate: false })
  })

  it('returns warning for invalid birth date year before 1900', () => {
    const member = { ...baseMember, dateOfBirth: '0001-01-01' }
    const result = getMemberDataWarnings(member, true)
    expect(result).toEqual({ missingDni: false, missingEmail: false, invalidBirthDate: true })
  })

  it('returns warning for birth date year in the future', () => {
    const futureYear = new Date().getFullYear() + 1
    const member = { ...baseMember, dateOfBirth: `${futureYear}-01-01` }
    const result = getMemberDataWarnings(member, true)
    expect(result).toEqual({ missingDni: false, missingEmail: false, invalidBirthDate: true })
  })

  it('returns null for minor missing DNI and email', () => {
    const member = { ...baseMember, documentNumber: null, email: null }
    expect(getMemberDataWarnings(member, false)).toBeNull()
  })

  it('returns warning for minor with invalid birth date', () => {
    const member = { ...baseMember, dateOfBirth: '0001-01-01', documentNumber: null, email: null }
    const result = getMemberDataWarnings(member, false)
    expect(result).toEqual({ missingDni: false, missingEmail: false, invalidBirthDate: true })
  })

  it('returns all warnings when adult is missing everything', () => {
    const member = { ...baseMember, documentNumber: null, email: null, dateOfBirth: '0001-01-01' }
    const result = getMemberDataWarnings(member, true)
    expect(result).toEqual({ missingDni: true, missingEmail: true, invalidBirthDate: true })
  })
})

describe('getWarningMessage', () => {
  it('formats single missing field', () => {
    expect(getWarningMessage({ missingDni: true, missingEmail: false, invalidBirthDate: false }))
      .toBe('Falta: DNI')
  })

  it('formats two missing fields', () => {
    expect(getWarningMessage({ missingDni: true, missingEmail: true, invalidBirthDate: false }))
      .toBe('Falta: DNI, Email')
  })

  it('formats all three missing fields', () => {
    expect(getWarningMessage({ missingDni: true, missingEmail: true, invalidBirthDate: true }))
      .toBe('Falta: DNI, Email, Fecha de nacimiento')
  })

  it('formats only invalid birth date', () => {
    expect(getWarningMessage({ missingDni: false, missingEmail: false, invalidBirthDate: true }))
      .toBe('Falta: Fecha de nacimiento')
  })
})
