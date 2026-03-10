import { describe, it, expect } from 'vitest'
import type { CampEdition, CampEditionExtra } from '@/types/camp-edition'
import type { FamilyMemberResponse } from '@/types/family-unit'
import type { WizardMemberSelection, WizardExtrasSelection } from '@/types/registration'
import {
  calculateAgeAtCamp,
  getAgeCategory,
  getPriceForCategory,
  getPeriodDays,
  calculateExtraAmount,
  calculatePricingPreview
} from '@/utils/registration-pricing'

// --- Helpers ---

const baseEdition: CampEdition = {
  id: 'edition-1',
  campId: 'camp-1',
  year: 2026,
  startDate: '2026-07-01',
  endDate: '2026-07-15',
  location: 'Test Camp',
  pricePerAdult: 450,
  pricePerChild: 300,
  pricePerBaby: 0,
  useCustomAgeRanges: false,
  maxCapacity: 100,
  status: 'Open',
  isArchived: false,
  createdAt: '2026-01-01T00:00:00Z',
  updatedAt: '2026-01-01T00:00:00Z',
  // Week pricing
  halfDate: '2026-07-08',
  pricePerAdultWeek: 250,
  pricePerChildWeek: 170,
  pricePerBabyWeek: 0,
  // Weekend pricing
  weekendStartDate: '2026-07-11',
  weekendEndDate: '2026-07-13',
  pricePerAdultWeekend: 100,
  pricePerChildWeekend: 70,
  pricePerBabyWeekend: 0,
  maxWeekendCapacity: 20
}

const makeMember = (overrides: Partial<FamilyMemberResponse> = {}): FamilyMemberResponse => ({
  id: 'member-1',
  familyUnitId: 'fu-1',
  userId: null,
  firstName: 'Ana',
  lastName: 'García',
  dateOfBirth: '1990-06-15',
  relationship: 'Self',
  documentNumber: null,
  email: null,
  phone: null,
  hasMedicalNotes: false,
  hasAllergies: false,
  profilePhotoUrl: null,
  createdAt: '2026-01-01T00:00:00Z',
  updatedAt: '2026-01-01T00:00:00Z',
  ...overrides
})

// --- Tests ---

describe('calculateAgeAtCamp', () => {
  it('calculates age correctly when birthday is before camp start', () => {
    expect(calculateAgeAtCamp('1990-06-15', '2026-07-01')).toBe(36)
  })

  it('calculates age correctly when birthday is after camp start', () => {
    expect(calculateAgeAtCamp('1990-08-15', '2026-07-01')).toBe(35)
  })

  it('calculates age correctly on exact birthday', () => {
    expect(calculateAgeAtCamp('1990-07-01', '2026-07-01')).toBe(36)
  })

  it('calculates baby age', () => {
    expect(calculateAgeAtCamp('2025-01-01', '2026-07-01')).toBe(1)
  })
})

describe('getAgeCategory', () => {
  it('returns Baby for age 0-2 with default ranges', () => {
    expect(getAgeCategory(0, baseEdition)).toBe('Baby')
    expect(getAgeCategory(2, baseEdition)).toBe('Baby')
  })

  it('returns Child for age 3-14 with default ranges', () => {
    expect(getAgeCategory(3, baseEdition)).toBe('Child')
    expect(getAgeCategory(14, baseEdition)).toBe('Child')
  })

  it('returns Adult for age 15+ with default ranges', () => {
    expect(getAgeCategory(15, baseEdition)).toBe('Adult')
    expect(getAgeCategory(36, baseEdition)).toBe('Adult')
  })

  it('uses custom age ranges when configured', () => {
    const customEdition: CampEdition = {
      ...baseEdition,
      useCustomAgeRanges: true,
      customBabyMaxAge: 3,
      customChildMinAge: 4,
      customChildMaxAge: 12,
      customAdultMinAge: 13
    }
    expect(getAgeCategory(3, customEdition)).toBe('Baby')
    expect(getAgeCategory(4, customEdition)).toBe('Child')
    expect(getAgeCategory(12, customEdition)).toBe('Child')
    expect(getAgeCategory(13, customEdition)).toBe('Adult')
  })
})

describe('getPriceForCategory', () => {
  it('returns complete period prices', () => {
    expect(getPriceForCategory('Adult', 'Complete', baseEdition)).toBe(450)
    expect(getPriceForCategory('Child', 'Complete', baseEdition)).toBe(300)
    expect(getPriceForCategory('Baby', 'Complete', baseEdition)).toBe(0)
  })

  it('returns week period prices', () => {
    expect(getPriceForCategory('Adult', 'FirstWeek', baseEdition)).toBe(250)
    expect(getPriceForCategory('Child', 'SecondWeek', baseEdition)).toBe(170)
  })

  it('returns weekend period prices', () => {
    expect(getPriceForCategory('Adult', 'WeekendVisit', baseEdition)).toBe(100)
    expect(getPriceForCategory('Child', 'WeekendVisit', baseEdition)).toBe(70)
  })
})

describe('getPeriodDays', () => {
  it('calculates complete period days', () => {
    expect(getPeriodDays('Complete', baseEdition)).toBe(14)
  })

  it('calculates first week days using halfDate', () => {
    expect(getPeriodDays('FirstWeek', baseEdition)).toBe(7)
  })

  it('calculates second week days using halfDate', () => {
    expect(getPeriodDays('SecondWeek', baseEdition)).toBe(7)
  })

  it('calculates weekend visit days from edition defaults', () => {
    // weekendStartDate: 2026-07-11, weekendEndDate: 2026-07-13 → 2 days
    expect(getPeriodDays('WeekendVisit', baseEdition)).toBe(2)
  })

  it('calculates weekend visit days from member-specific dates', () => {
    expect(getPeriodDays('WeekendVisit', baseEdition, '2026-07-11', '2026-07-14')).toBe(3)
  })

  it('caps weekend visit days at 3', () => {
    expect(getPeriodDays('WeekendVisit', baseEdition, '2026-07-10', '2026-07-15')).toBe(3)
  })

  it('splits evenly when halfDate is not set', () => {
    const editionNoHalf = { ...baseEdition, halfDate: null }
    expect(getPeriodDays('FirstWeek', editionNoHalf)).toBe(7)
    expect(getPeriodDays('SecondWeek', editionNoHalf)).toBe(7)
  })
})

describe('calculateExtraAmount', () => {
  it('calculates PerPerson OneTime', () => {
    expect(calculateExtraAmount(5, 3, 'PerPerson', 'OneTime', 14)).toBe(15)
  })

  it('calculates PerPerson PerDay', () => {
    expect(calculateExtraAmount(5, 2, 'PerPerson', 'PerDay', 14)).toBe(140)
  })

  it('calculates PerFamily OneTime', () => {
    expect(calculateExtraAmount(20, 1, 'PerFamily', 'OneTime', 14)).toBe(20)
  })

  it('calculates PerFamily PerDay', () => {
    expect(calculateExtraAmount(10, 1, 'PerFamily', 'PerDay', 14)).toBe(140)
  })
})

describe('calculatePricingPreview', () => {
  const adultMember = makeMember({ id: 'adult-1', firstName: 'Ana', lastName: 'García', dateOfBirth: '1990-06-15' })
  const childMember = makeMember({ id: 'child-1', firstName: 'Luis', lastName: 'García', dateOfBirth: '2016-03-10' })
  const babyMember = makeMember({ id: 'baby-1', firstName: 'Sofía', lastName: 'García', dateOfBirth: '2025-01-01' })

  const familyMembers = [adultMember, childMember, babyMember]

  it('calculates pricing for mixed age categories (Complete period)', () => {
    const selectedMembers: WizardMemberSelection[] = [
      { memberId: 'adult-1', attendancePeriod: 'Complete', visitStartDate: null, visitEndDate: null, guardianName: null, guardianDocumentNumber: null },
      { memberId: 'child-1', attendancePeriod: 'Complete', visitStartDate: null, visitEndDate: null, guardianName: null, guardianDocumentNumber: null },
      { memberId: 'baby-1', attendancePeriod: 'Complete', visitStartDate: null, visitEndDate: null, guardianName: null, guardianDocumentNumber: null }
    ]

    const result = calculatePricingPreview(baseEdition, familyMembers, selectedMembers, [], [])

    expect(result.members).toHaveLength(3)
    expect(result.members[0].ageCategory).toBe('Adult')
    expect(result.members[0].individualAmount).toBe(450)
    expect(result.members[1].ageCategory).toBe('Child')
    expect(result.members[1].individualAmount).toBe(300)
    expect(result.members[2].ageCategory).toBe('Baby')
    expect(result.members[2].individualAmount).toBe(0)
    expect(result.baseTotalAmount).toBe(750)
    expect(result.extras).toHaveLength(0)
    expect(result.extrasAmount).toBe(0)
    expect(result.totalAmount).toBe(750)
  })

  it('calculates pricing with week periods', () => {
    const selectedMembers: WizardMemberSelection[] = [
      { memberId: 'adult-1', attendancePeriod: 'FirstWeek', visitStartDate: null, visitEndDate: null, guardianName: null, guardianDocumentNumber: null }
    ]

    const result = calculatePricingPreview(baseEdition, familyMembers, selectedMembers, [], [])

    expect(result.members[0].attendancePeriod).toBe('FirstWeek')
    expect(result.members[0].individualAmount).toBe(250)
    expect(result.members[0].attendanceDays).toBe(7)
    expect(result.totalAmount).toBe(250)
  })

  it('calculates pricing with extras', () => {
    const selectedMembers: WizardMemberSelection[] = [
      { memberId: 'adult-1', attendancePeriod: 'Complete', visitStartDate: null, visitEndDate: null, guardianName: null, guardianDocumentNumber: null }
    ]

    const campExtras: CampEditionExtra[] = [
      {
        id: 'extra-1', campEditionId: 'edition-1', name: 'Camiseta', price: 15,
        pricingType: 'PerPerson', pricingPeriod: 'OneTime', isRequired: false,
        requiresUserInput: false, sortOrder: 1, currentQuantitySold: null,
        isActive: true, createdAt: '', updatedAt: '', maxQuantity: undefined
      },
      {
        id: 'extra-2', campEditionId: 'edition-1', name: 'Seguro extra', price: 3,
        pricingType: 'PerPerson', pricingPeriod: 'PerDay', isRequired: false,
        requiresUserInput: false, sortOrder: 2, currentQuantitySold: null,
        isActive: true, createdAt: '', updatedAt: '', maxQuantity: undefined
      }
    ]

    const extrasSelections: WizardExtrasSelection[] = [
      { campEditionExtraId: 'extra-1', name: 'Camiseta', quantity: 2, unitPrice: 15 },
      { campEditionExtraId: 'extra-2', name: 'Seguro extra', quantity: 1, unitPrice: 3 }
    ]

    const result = calculatePricingPreview(baseEdition, familyMembers, selectedMembers, extrasSelections, campExtras)

    expect(result.extras).toHaveLength(2)
    // Camiseta: 15 × 2 = 30
    expect(result.extras[0].totalAmount).toBe(30)
    expect(result.extras[0].calculation).toBe('€15 × 2 persona(s)')
    // Seguro: 3 × 1 × 14 days = 42
    expect(result.extras[1].totalAmount).toBe(42)
    expect(result.extras[1].calculation).toBe('€3 × 1 persona(s) × 14 días')
    expect(result.extrasAmount).toBe(72)
    expect(result.totalAmount).toBe(450 + 72)
  })

  it('excludes extras with quantity 0', () => {
    const selectedMembers: WizardMemberSelection[] = [
      { memberId: 'adult-1', attendancePeriod: 'Complete', visitStartDate: null, visitEndDate: null, guardianName: null, guardianDocumentNumber: null }
    ]

    const campExtras: CampEditionExtra[] = [
      {
        id: 'extra-1', campEditionId: 'edition-1', name: 'Camiseta', price: 15,
        pricingType: 'PerPerson', pricingPeriod: 'OneTime', isRequired: false,
        requiresUserInput: false, sortOrder: 1, currentQuantitySold: null,
        isActive: true, createdAt: '', updatedAt: '', maxQuantity: undefined
      }
    ]

    const extrasSelections: WizardExtrasSelection[] = [
      { campEditionExtraId: 'extra-1', name: 'Camiseta', quantity: 0, unitPrice: 15 }
    ]

    const result = calculatePricingPreview(baseEdition, familyMembers, selectedMembers, extrasSelections, campExtras)

    expect(result.extras).toHaveLength(0)
    expect(result.extrasAmount).toBe(0)
  })

  it('handles PerFamily extras correctly', () => {
    const selectedMembers: WizardMemberSelection[] = [
      { memberId: 'adult-1', attendancePeriod: 'Complete', visitStartDate: null, visitEndDate: null, guardianName: null, guardianDocumentNumber: null }
    ]

    const campExtras: CampEditionExtra[] = [
      {
        id: 'extra-1', campEditionId: 'edition-1', name: 'Parking', price: 50,
        pricingType: 'PerFamily', pricingPeriod: 'OneTime', isRequired: false,
        requiresUserInput: false, sortOrder: 1, currentQuantitySold: null,
        isActive: true, createdAt: '', updatedAt: '', maxQuantity: undefined
      }
    ]

    const extrasSelections: WizardExtrasSelection[] = [
      { campEditionExtraId: 'extra-1', name: 'Parking', quantity: 1, unitPrice: 50 }
    ]

    const result = calculatePricingPreview(baseEdition, familyMembers, selectedMembers, extrasSelections, campExtras)

    expect(result.extras[0].totalAmount).toBe(50)
    expect(result.extras[0].calculation).toBe('€50 (por familia)')
  })

  it('uses custom age ranges from edition', () => {
    const customEdition: CampEdition = {
      ...baseEdition,
      useCustomAgeRanges: true,
      customBabyMaxAge: 3,
      customChildMinAge: 4,
      customChildMaxAge: 12,
      customAdultMinAge: 13
    }

    // babyMember is ~1.5 years old → Baby (under custom 3)
    // childMember is ~10 years old → Child (4-12 custom range)
    const selectedMembers: WizardMemberSelection[] = [
      { memberId: 'baby-1', attendancePeriod: 'Complete', visitStartDate: null, visitEndDate: null, guardianName: null, guardianDocumentNumber: null },
      { memberId: 'child-1', attendancePeriod: 'Complete', visitStartDate: null, visitEndDate: null, guardianName: null, guardianDocumentNumber: null }
    ]

    const result = calculatePricingPreview(customEdition, familyMembers, selectedMembers, [], [])

    expect(result.members[0].ageCategory).toBe('Baby')
    expect(result.members[1].ageCategory).toBe('Child')
  })
})
