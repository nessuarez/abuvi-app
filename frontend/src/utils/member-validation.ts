import type { FamilyMemberResponse } from '@/types/family-unit'

export interface MemberDataWarning {
  missingDni: boolean
  missingEmail: boolean
  invalidBirthDate: boolean
}

export function getMemberDataWarnings(
  member: FamilyMemberResponse,
  isAdult: boolean
): MemberDataWarning | null {
  const missingDni = isAdult && (!member.documentNumber || member.documentNumber.trim() === '')
  const missingEmail = isAdult && (!member.email || member.email.trim() === '')

  const birthYear = parseInt(member.dateOfBirth.split('-')[0], 10)
  const invalidBirthDate = isNaN(birthYear) || birthYear < 1920 || birthYear > new Date().getFullYear()

  if (!missingDni && !missingEmail && !invalidBirthDate) {
    return null
  }

  return { missingDni, missingEmail, invalidBirthDate }
}

export function getWarningMessage(warnings: MemberDataWarning): string {
  const missing: string[] = []
  if (warnings.missingDni) missing.push('DNI')
  if (warnings.missingEmail) missing.push('Email')
  if (warnings.invalidBirthDate) missing.push('Fecha de nacimiento')
  return `Falta: ${missing.join(', ')}`
}
