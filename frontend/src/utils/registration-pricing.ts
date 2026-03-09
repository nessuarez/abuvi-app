/**
 * Client-side pricing calculation for the registration wizard confirm step.
 *
 * IMPORTANT: This logic mirrors the backend RegistrationPricingService.cs.
 * If backend pricing logic changes, update this file accordingly.
 */
import type { CampEdition, CampEditionExtra } from '@/types/camp-edition'
import type { FamilyMemberResponse } from '@/types/family-unit'
import type {
  AgeCategory,
  AttendancePeriod,
  ExtraPricingDetail,
  MemberPricingDetail,
  PricingBreakdown,
  WizardExtrasSelection,
  WizardMemberSelection
} from '@/types/registration'
import { parseDateSafe } from '@/utils/date'

// Default age ranges matching backend association_settings defaults
const DEFAULT_BABY_MAX_AGE = 2
const DEFAULT_CHILD_MIN_AGE = 3
const DEFAULT_CHILD_MAX_AGE = 14
const DEFAULT_ADULT_MIN_AGE = 15

/**
 * Calculates age as of campStartDate.
 * Mirrors RegistrationPricingService.CalculateAge.
 */
export function calculateAgeAtCamp(dateOfBirth: string, campStartDate: string): number {
  const dob = parseDateSafe(dateOfBirth)
  const campDate = parseDateSafe(campStartDate)
  let age = campDate.getFullYear() - dob.getFullYear()
  const monthDiff = campDate.getMonth() - dob.getMonth()
  if (monthDiff < 0 || (monthDiff === 0 && campDate.getDate() < dob.getDate())) {
    age--
  }
  return age
}

/**
 * Determines AgeCategory from age and edition's configured ranges.
 * Mirrors RegistrationPricingService.GetAgeCategoryAsync.
 */
export function getAgeCategory(age: number, edition: CampEdition): AgeCategory {
  const babyMax = edition.useCustomAgeRanges && edition.customBabyMaxAge != null
    ? edition.customBabyMaxAge
    : DEFAULT_BABY_MAX_AGE
  const childMin = edition.useCustomAgeRanges && edition.customChildMinAge != null
    ? edition.customChildMinAge
    : DEFAULT_CHILD_MIN_AGE
  const childMax = edition.useCustomAgeRanges && edition.customChildMaxAge != null
    ? edition.customChildMaxAge
    : DEFAULT_CHILD_MAX_AGE
  const adultMin = edition.useCustomAgeRanges && edition.customAdultMinAge != null
    ? edition.customAdultMinAge
    : DEFAULT_ADULT_MIN_AGE

  if (age >= 0 && age <= babyMax) return 'Baby'
  if (age >= childMin && age <= childMax) return 'Child'
  if (age >= adultMin) return 'Adult'

  // Fallback: shouldn't happen with well-configured ranges
  return 'Adult'
}

/**
 * Returns the price for a given AgeCategory and AttendancePeriod.
 * Mirrors RegistrationPricingService.GetPriceForCategory.
 */
export function getPriceForCategory(
  category: AgeCategory,
  period: AttendancePeriod,
  edition: CampEdition
): number {
  if (period === 'FirstWeek' || period === 'SecondWeek') {
    const prices: Record<AgeCategory, number | null | undefined> = {
      Adult: edition.pricePerAdultWeek,
      Child: edition.pricePerChildWeek,
      Baby: edition.pricePerBabyWeek
    }
    return prices[category] ?? 0
  }

  if (period === 'WeekendVisit') {
    const prices: Record<AgeCategory, number | null | undefined> = {
      Adult: edition.pricePerAdultWeekend,
      Child: edition.pricePerChildWeekend,
      Baby: edition.pricePerBabyWeekend
    }
    return prices[category] ?? 0
  }

  // Complete
  const prices: Record<AgeCategory, number> = {
    Adult: edition.pricePerAdult,
    Child: edition.pricePerChild,
    Baby: edition.pricePerBaby
  }
  return prices[category]
}

/**
 * Computes attendance days for a given period.
 * Mirrors RegistrationPricingService.GetPeriodDays.
 */
export function getPeriodDays(
  period: AttendancePeriod,
  edition: CampEdition,
  visitStart?: string | null,
  visitEnd?: string | null
): number {
  const start = parseDateSafe(edition.startDate)
  const end = parseDateSafe(edition.endDate)
  const totalDays = Math.round((end.getTime() - start.getTime()) / (1000 * 60 * 60 * 24))

  if (period === 'Complete') return totalDays

  const halfDate = edition.halfDate
    ? parseDateSafe(edition.halfDate)
    : new Date(start.getTime() + Math.floor(totalDays / 2) * 24 * 60 * 60 * 1000)

  if (period === 'FirstWeek') {
    return Math.round((halfDate.getTime() - start.getTime()) / (1000 * 60 * 60 * 24))
  }

  if (period === 'SecondWeek') {
    return Math.round((end.getTime() - halfDate.getTime()) / (1000 * 60 * 60 * 24))
  }

  // WeekendVisit
  const wkStart = visitStart
    ? parseDateSafe(visitStart)
    : (edition.weekendStartDate ? parseDateSafe(edition.weekendStartDate) : null)
  if (!wkStart) return 0
  const wkEnd = visitEnd
    ? parseDateSafe(visitEnd)
    : (edition.weekendEndDate ? parseDateSafe(edition.weekendEndDate) : new Date(wkStart.getTime() + 2 * 24 * 60 * 60 * 1000))
  const days = Math.round((wkEnd.getTime() - wkStart.getTime()) / (1000 * 60 * 60 * 24))
  return Math.min(3, Math.max(0, days))
}

/**
 * Calculates the total amount for an extra selection.
 * Mirrors RegistrationPricingService.CalculateExtraAmount.
 */
export function calculateExtraAmount(
  unitPrice: number,
  quantity: number,
  pricingType: string,
  pricingPeriod: string,
  campDurationDays: number
): number {
  const baseAmount = pricingType === 'PerPerson' ? unitPrice * quantity : unitPrice
  return pricingPeriod === 'PerDay' ? baseAmount * campDurationDays : baseAmount
}

/**
 * Builds a human-readable calculation string for an extra.
 * Mirrors RegistrationsModels.BuildCalculation.
 */
function buildCalculation(
  unitPrice: number,
  quantity: number,
  pricingType: string,
  pricingPeriod: string,
  campDurationDays: number | null
): string {
  const price = `€${unitPrice}`
  if (pricingType === 'PerPerson' && pricingPeriod === 'OneTime') {
    return `${price} × ${quantity} persona(s)`
  }
  if (pricingType === 'PerPerson' && pricingPeriod === 'PerDay') {
    return `${price} × ${quantity} persona(s) × ${campDurationDays} días`
  }
  if (pricingType === 'PerFamily' && pricingPeriod === 'OneTime') {
    return `${price} (por familia)`
  }
  if (pricingType === 'PerFamily' && pricingPeriod === 'PerDay') {
    return `${price} × ${campDurationDays} días`
  }
  return ''
}

/**
 * Generates a complete PricingBreakdown from wizard state.
 * This is the main entry point used by the confirm step.
 */
export function calculatePricingPreview(
  edition: CampEdition,
  familyMembers: FamilyMemberResponse[],
  selectedMembers: WizardMemberSelection[],
  extrasSelections: WizardExtrasSelection[],
  campExtras: CampEditionExtra[]
): PricingBreakdown {
  const campDurationDays = getPeriodDays('Complete', edition)

  // Build member pricing details
  const members: MemberPricingDetail[] = selectedMembers.map((sel) => {
    const member = familyMembers.find((m) => m.id === sel.memberId)
    const fullName = member ? `${member.firstName} ${member.lastName}` : 'Desconocido'
    const ageAtCamp = member ? calculateAgeAtCamp(member.dateOfBirth, edition.startDate) : 0
    const ageCategory = getAgeCategory(ageAtCamp, edition)
    const individualAmount = getPriceForCategory(ageCategory, sel.attendancePeriod, edition)
    const attendanceDays = getPeriodDays(
      sel.attendancePeriod, edition, sel.visitStartDate, sel.visitEndDate
    )

    return {
      familyMemberId: sel.memberId,
      fullName,
      ageAtCamp,
      ageCategory,
      attendancePeriod: sel.attendancePeriod,
      attendanceDays,
      visitStartDate: sel.visitStartDate ?? null,
      visitEndDate: sel.visitEndDate ?? null,
      individualAmount,
      guardianName: sel.guardianName ?? null,
      guardianDocumentNumber: sel.guardianDocumentNumber ?? null
    }
  })

  const baseTotalAmount = members.reduce((sum, m) => sum + m.individualAmount, 0)

  // Build extras pricing details
  const activeExtras = extrasSelections.filter((e) => e.quantity > 0)
  const extras: ExtraPricingDetail[] = activeExtras.map((sel) => {
    const extra = campExtras.find((e) => e.id === sel.campEditionExtraId)
    const pricingType = extra?.pricingType ?? 'PerPerson'
    const pricingPeriod = extra?.pricingPeriod ?? 'OneTime'
    const unitPrice = extra?.price ?? sel.unitPrice
    const totalAmount = calculateExtraAmount(
      unitPrice, sel.quantity, pricingType, pricingPeriod, campDurationDays
    )
    const durationForCalc = pricingPeriod === 'PerDay' ? campDurationDays : null

    return {
      campEditionExtraId: sel.campEditionExtraId,
      name: sel.name,
      unitPrice,
      pricingType,
      pricingPeriod,
      quantity: sel.quantity,
      campDurationDays: durationForCalc,
      calculation: buildCalculation(unitPrice, sel.quantity, pricingType, pricingPeriod, durationForCalc),
      totalAmount,
      userInput: sel.userInput
    }
  })

  const extrasAmount = extras.reduce((sum, e) => sum + e.totalAmount, 0)

  return {
    members,
    baseTotalAmount,
    extras,
    extrasAmount,
    totalAmount: baseTotalAmount + extrasAmount
  }
}
