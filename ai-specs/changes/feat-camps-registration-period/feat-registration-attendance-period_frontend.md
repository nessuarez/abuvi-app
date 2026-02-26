# Frontend Implementation Plan: feat-registration-attendance-period

## Overview

This plan extends the camp registration wizard and related components to support per-member **attendance periods**: `Complete` (full camp), `FirstWeek`, `SecondWeek`, and `WeekendVisit` (max 3 days). Each family member independently selects their period. The backend already ships these changes in `feature/feat-registration-attendance-period-backend`.

**Architecture**: Vue 3 Composition API, PrimeVue v4, Tailwind CSS, no Pinia (registration flow uses local composable state).

**Key breaking changes from the backend**:
- `POST /api/registrations` — `memberIds: string[]` → `members: MemberAttendanceRequest[]`
- `PUT /api/registrations/{id}/members` — same shape change
- `MemberPricingDetail` in pricing response now includes `attendancePeriod`, `attendanceDays`, `visitStartDate`, `visitEndDate`
- `GET /api/registrations/editions/available` — response includes week/weekend pricing, `allowsPartialAttendance`, `allowsWeekendVisit`, computed day counts

---

## Architecture Context

### Files to Modify
| File | Change Type |
|------|-------------|
| `frontend/src/types/registration.ts` | Additive + breaking type update |
| `frontend/src/types/camp-edition.ts` | Additive (new optional fields) |
| `frontend/src/components/registrations/RegistrationMemberSelector.vue` | Major refactor |
| `frontend/src/views/registrations/RegisterForCampPage.vue` | State + confirm handler |
| `frontend/src/components/registrations/RegistrationPricingBreakdown.vue` | Additive column |
| `frontend/src/components/registrations/__tests__/RegistrationMemberSelector.test.ts` | Update + new cases |
| `frontend/src/components/registrations/__tests__/RegistrationPricingBreakdown.test.ts` | Update + new cases |

### Files to Create
| File | Purpose |
|------|---------|
| `frontend/src/utils/registration.ts` | Period labels, day computation helpers |
| `frontend/src/utils/__tests__/registration.test.ts` | Unit tests for helpers |

### State Management
- No Pinia store needed — all state is local to `RegisterForCampPage.vue`
- The wizard `selectedMembers` ref replaces `selectedMemberIds` ref

### Routing
- No routing changes — the wizard is still at the existing `register-for-camp` route with `editionId` param

---

## Implementation Steps

### Step 0: Create Feature Branch

- **Action**: Switch to a new feature branch
- **Branch**: `feature/feat-registration-attendance-period-frontend`
- **Steps**:
  1. `git checkout main && git pull origin main`
  2. `git checkout -b feature/feat-registration-attendance-period-frontend`
  3. `git branch` — verify you are on the new branch

---

### Step 1: Update TypeScript Types — `registration.ts`

- **File**: `frontend/src/types/registration.ts`
- **Action**: Add `AttendancePeriod`, `MemberAttendanceRequest`, `WizardMemberSelection`; update `CreateRegistrationRequest`, `UpdateRegistrationMembersRequest`, `MemberPricingDetail`, `AvailableCampEditionResponse`

**Implementation Steps**:

1. **Add `AttendancePeriod` type** after the existing enums at the top of the file:
   ```typescript
   export type AttendancePeriod = 'Complete' | 'FirstWeek' | 'SecondWeek' | 'WeekendVisit'
   ```
   > Note: `Complete` is first to match the backend enum CLR default (0).

2. **Add `MemberAttendanceRequest`** — the API request shape per member:
   ```typescript
   export interface MemberAttendanceRequest {
     memberId: string
     attendancePeriod: AttendancePeriod
     visitStartDate?: string | null  // YYYY-MM-DD, required when WeekendVisit
     visitEndDate?: string | null    // YYYY-MM-DD, required when WeekendVisit
   }
   ```

3. **Add `WizardMemberSelection`** — wizard-local state (richer than `MemberAttendanceRequest`):
   ```typescript
   export interface WizardMemberSelection {
     memberId: string
     attendancePeriod: AttendancePeriod
     visitStartDate: string | null  // ISO date string YYYY-MM-DD
     visitEndDate: string | null
   }
   ```

4. **Update `CreateRegistrationRequest`** — replace `memberIds`:
   ```typescript
   export interface CreateRegistrationRequest {
     campEditionId: string
     familyUnitId: string
     members: MemberAttendanceRequest[]   // CHANGED from memberIds: string[]
     notes?: string | null
   }
   ```

5. **Update `UpdateRegistrationMembersRequest`** — replace `memberIds`:
   ```typescript
   export interface UpdateRegistrationMembersRequest {
     members: MemberAttendanceRequest[]   // CHANGED from memberIds: string[]
   }
   ```

6. **Update `MemberPricingDetail`** — add period fields returned by backend:
   ```typescript
   export interface MemberPricingDetail {
     familyMemberId: string
     fullName: string
     ageAtCamp: number
     ageCategory: AgeCategory
     attendancePeriod: AttendancePeriod    // NEW
     attendanceDays: number                // NEW
     visitStartDate: string | null         // NEW, only for WeekendVisit
     visitEndDate: string | null           // NEW, only for WeekendVisit
     individualAmount: number
   }
   ```

7. **Update `AvailableCampEditionResponse`** — add all new backend fields:
   ```typescript
   export interface AvailableCampEditionResponse {
     id: string
     campName: string
     year: number
     startDate: string
     endDate: string
     location: string | null
     pricePerAdult: number
     pricePerChild: number
     pricePerBaby: number
     maxCapacity: number | null
     currentRegistrations: number
     spotsRemaining: number | null
     status: string
     ageRanges: AgeRangesInfo
     // Partial attendance (week periods):
     allowsPartialAttendance: boolean      // NEW
     pricePerAdultWeek: number | null      // NEW
     pricePerChildWeek: number | null      // NEW
     pricePerBabyWeek: number | null       // NEW
     halfDate: string | null               // NEW (YYYY-MM-DD)
     firstWeekDays: number                 // NEW
     secondWeekDays: number                // NEW
     // Weekend visit:
     allowsWeekendVisit: boolean           // NEW
     pricePerAdultWeekend: number | null   // NEW
     pricePerChildWeekend: number | null   // NEW
     pricePerBabyWeekend: number | null    // NEW
     weekendStartDate: string | null       // NEW (YYYY-MM-DD)
     weekendEndDate: string | null         // NEW (YYYY-MM-DD)
     weekendDays: number                   // NEW
     maxWeekendCapacity: number | null     // NEW
     weekendSpotsRemaining: number | null  // NEW
   }
   ```

---

### Step 2: Update TypeScript Types — `camp-edition.ts`

- **File**: `frontend/src/types/camp-edition.ts`
- **Action**: Add new optional fields to `CampEdition`, `UpdateCampEditionRequest`, `ProposeCampEditionRequest` so the registration wizard and admin views have access to the new fields

**Implementation Steps**:

1. **Update `CampEdition`** — add optional new fields after `calculatedTotalBedCapacity`:
   ```typescript
   // Partial attendance (week pricing):
   halfDate?: string | null           // YYYY-MM-DD
   pricePerAdultWeek?: number | null
   pricePerChildWeek?: number | null
   pricePerBabyWeek?: number | null
   // Weekend visit:
   weekendStartDate?: string | null   // YYYY-MM-DD
   weekendEndDate?: string | null     // YYYY-MM-DD
   pricePerAdultWeekend?: number | null
   pricePerChildWeekend?: number | null
   pricePerBabyWeekend?: number | null
   maxWeekendCapacity?: number | null
   ```

2. **Update `UpdateCampEditionRequest`** — add new optional fields:
   ```typescript
   halfDate?: string | null
   pricePerAdultWeek?: number | null
   pricePerChildWeek?: number | null
   pricePerBabyWeek?: number | null
   weekendStartDate?: string | null
   weekendEndDate?: string | null
   pricePerAdultWeekend?: number | null
   pricePerChildWeekend?: number | null
   pricePerBabyWeekend?: number | null
   maxWeekendCapacity?: number | null
   ```

3. **Update `ProposeCampEditionRequest`** — same new optional fields as `UpdateCampEditionRequest` (in addition to existing fields).

4. **Update `ActiveCampEditionResponse`** — add the same week/weekend optional fields for completeness (the backend response now includes them).

---

### Step 3: Create Utility File — `frontend/src/utils/registration.ts`

- **File**: `frontend/src/utils/registration.ts` (new file)
- **Action**: Shared utilities for period labels and day computation, avoiding duplication across components

**Implementation Steps**:

1. **Create the file** with the following exports:

```typescript
import type { AttendancePeriod } from '@/types/registration'

export const ATTENDANCE_PERIOD_LABELS: Record<AttendancePeriod, string> = {
  Complete: 'Campamento completo',
  FirstWeek: 'Primera semana',
  SecondWeek: 'Segunda semana',
  WeekendVisit: 'Visita de fin de semana',
}

export const getAttendancePeriodLabel = (period: AttendancePeriod): string =>
  ATTENDANCE_PERIOD_LABELS[period] ?? period

/**
 * Compute the number of days in each period from a CampEdition.
 * Uses `halfDate` if set; otherwise splits the total duration at the midpoint.
 * Returns 0 for all if startDate/endDate are not available.
 */
export function computePeriodDays(
  startDate: string,
  endDate: string,
  halfDate: string | null | undefined
): { firstWeekDays: number; secondWeekDays: number; totalDays: number } {
  const start = new Date(startDate)
  const end = new Date(endDate)
  const totalDays = Math.round((end.getTime() - start.getTime()) / (1000 * 60 * 60 * 24))

  if (halfDate) {
    const half = new Date(halfDate)
    const firstWeekDays = Math.round((half.getTime() - start.getTime()) / (1000 * 60 * 60 * 24))
    const secondWeekDays = totalDays - firstWeekDays
    return { firstWeekDays, secondWeekDays, totalDays }
  }

  const firstWeekDays = Math.floor(totalDays / 2)
  const secondWeekDays = totalDays - firstWeekDays
  return { firstWeekDays, secondWeekDays, totalDays }
}

/**
 * Returns the allowed period options for a given edition.
 * Always includes 'Complete'. Adds week periods if pricePerAdultWeek is set.
 * Adds 'WeekendVisit' if weekendStartDate is set.
 */
export function getAllowedPeriods(edition: {
  pricePerAdultWeek?: number | null
  weekendStartDate?: string | null
}): AttendancePeriod[] {
  const periods: AttendancePeriod[] = ['Complete']
  if (edition.pricePerAdultWeek != null) {
    periods.push('FirstWeek', 'SecondWeek')
  }
  if (edition.weekendStartDate != null) {
    periods.push('WeekendVisit')
  }
  return periods
}
```

---

### Step 4: Refactor `RegistrationMemberSelector.vue`

- **File**: `frontend/src/components/registrations/RegistrationMemberSelector.vue`
- **Action**: Change `modelValue` from `string[]` to `WizardMemberSelection[]`. Add per-member period selector. Add date pickers for `WeekendVisit`.
- **Dependencies**: `Select` from PrimeVue, `DatePicker` from PrimeVue (v4 name), `WizardMemberSelection`, `AttendancePeriod`, utility functions from Step 3

**Implementation Steps**:

1. **Update props and emits**:
   ```typescript
   import type { FamilyMemberResponse, FamilyRelationship } from '@/types/family-unit'
   import type { CampEdition } from '@/types/camp-edition'
   import type { WizardMemberSelection, AttendancePeriod } from '@/types/registration'
   import { FamilyRelationshipLabels } from '@/types/family-unit'
   import { ATTENDANCE_PERIOD_LABELS, getAllowedPeriods } from '@/utils/registration'
   import Select from 'primevue/select'
   import DatePicker from 'primevue/datepicker'
   import Checkbox from 'primevue/checkbox'

   const props = defineProps<{
     members: FamilyMemberResponse[]
     modelValue: WizardMemberSelection[]
     edition: CampEdition    // NEW — needed to know which periods are allowed
   }>()

   const emit = defineEmits<{
     'update:modelValue': [selections: WizardMemberSelection[]]
   }>()
   ```

2. **Compute allowed periods and period options**:
   ```typescript
   const allowedPeriods = computed(() => getAllowedPeriods(props.edition))

   const periodOptions = computed(() =>
     allowedPeriods.value.map((p) => ({
       label: ATTENDANCE_PERIOD_LABELS[p],
       value: p
     }))
   )

   const isSelected = (memberId: string): boolean =>
     props.modelValue.some((s) => s.memberId === memberId)

   const getSelection = (memberId: string): WizardMemberSelection | undefined =>
     props.modelValue.find((s) => s.memberId === memberId)
   ```

3. **Toggle member logic** — default to `Complete` when first checked:
   ```typescript
   const toggleMember = (memberId: string) => {
     if (isSelected(memberId)) {
       emit('update:modelValue', props.modelValue.filter((s) => s.memberId !== memberId))
     } else {
       emit('update:modelValue', [
         ...props.modelValue,
         { memberId, attendancePeriod: 'Complete', visitStartDate: null, visitEndDate: null }
       ])
     }
   }
   ```

4. **Update period for a member**:
   ```typescript
   const updatePeriod = (memberId: string, period: AttendancePeriod) => {
     emit('update:modelValue', props.modelValue.map((s) =>
       s.memberId === memberId
         ? { ...s, attendancePeriod: period, visitStartDate: null, visitEndDate: null }
         : s
     ))
   }

   const updateVisitDate = (memberId: string, field: 'visitStartDate' | 'visitEndDate', value: Date | null) => {
     const dateStr = value ? value.toISOString().split('T')[0] : null
     emit('update:modelValue', props.modelValue.map((s) =>
       s.memberId === memberId ? { ...s, [field]: dateStr } : s
     ))
   }
   ```

5. **Weekend date constraints** (for DatePicker `minDate`/`maxDate`):
   ```typescript
   const weekendMinDate = computed(() =>
     props.edition.weekendStartDate ? new Date(props.edition.weekendStartDate) : undefined
   )
   const weekendMaxDate = computed(() =>
     props.edition.weekendEndDate ? new Date(props.edition.weekendEndDate) : undefined
   )
   ```

6. **Template structure** — for each member card:
   ```html
   <label ...> <!-- existing member card wrapper -->
     <!-- existing checkbox + name/info row (unchanged) -->

     <!-- NEW: period selector (only when member is selected) -->
     <div v-if="isSelected(member.id)" class="mt-2 space-y-2" @click.stop>
       <Select
         :model-value="getSelection(member.id)?.attendancePeriod"
         :options="periodOptions"
         option-label="label"
         option-value="value"
         placeholder="Periodo"
         class="w-full text-sm"
         :data-testid="`period-select-${member.id}`"
         @update:model-value="(p) => updatePeriod(member.id, p)"
       />

       <!-- Weekend date pickers (only when WeekendVisit is selected) -->
       <template v-if="getSelection(member.id)?.attendancePeriod === 'WeekendVisit'">
         <div class="flex gap-2">
           <div class="flex-1">
             <label class="mb-1 block text-xs text-gray-500">Llegada</label>
             <DatePicker
               :model-value="getSelection(member.id)?.visitStartDate
                 ? new Date(getSelection(member.id)!.visitStartDate!)
                 : null"
               :min-date="weekendMinDate"
               :max-date="weekendMaxDate"
               date-format="dd/mm/yy"
               show-icon
               class="w-full text-sm"
               :data-testid="`visit-start-${member.id}`"
               @update:model-value="(d) => updateVisitDate(member.id, 'visitStartDate', d)"
             />
           </div>
           <div class="flex-1">
             <label class="mb-1 block text-xs text-gray-500">Salida</label>
             <DatePicker
               :model-value="getSelection(member.id)?.visitEndDate
                 ? new Date(getSelection(member.id)!.visitEndDate!)
                 : null"
               :min-date="getSelection(member.id)?.visitStartDate
                 ? new Date(getSelection(member.id)!.visitStartDate!)
                 : weekendMinDate"
               :max-date="weekendMaxDate"
               date-format="dd/mm/yy"
               show-icon
               class="w-full text-sm"
               :data-testid="`visit-end-${member.id}`"
               @update:model-value="(d) => updateVisitDate(member.id, 'visitEndDate', d)"
             />
           </div>
         </div>
         <p class="text-xs text-orange-600">
           Máximo 3 días. Dentro del periodo {{ formatDate(edition.weekendStartDate!) }} — {{ formatDate(edition.weekendEndDate!) }}
         </p>
       </template>
     </div>
   </label>
   ```

   **Important notes for template**:
   - The `@click.stop` on the inner div prevents the outer `<label>` click from re-toggling the checkbox when clicking the Select/DatePicker
   - The card border highlight should use `modelValue.some(s => s.memberId === member.id)` instead of `modelValue.includes(member.id)`
   - Only show period selector when `allowedPeriods.value.length > 1` (i.e., not just `Complete`); if only `Complete` is allowed, no selector needed

---

### Step 5: Update `RegisterForCampPage.vue`

- **File**: `frontend/src/views/registrations/RegisterForCampPage.vue`
- **Action**: Rename `selectedMemberIds` → `selectedMembers` (WizardMemberSelection[]), update review step, update `handleConfirm`, pass edition to selector

**Implementation Steps**:

1. **Update imports and state**:
   ```typescript
   import type { WizardMemberSelection } from '@/types/registration'
   import { computePeriodDays, ATTENDANCE_PERIOD_LABELS } from '@/utils/registration'

   // CHANGE: was selectedMemberIds = ref<string[]>([])
   const selectedMembers = ref<WizardMemberSelection[]>([])
   ```

2. **Rename existing computed `selectedMembers` to avoid collision**:
   The current code has a computed `selectedMembers` (the FamilyMemberResponse objects). Rename it:
   ```typescript
   // RENAME: was 'selectedMembers', now avoids collision with the new ref
   const selectedMemberDetails = computed(() =>
     familyMembers.value.filter((m) => selectedMembers.value.some((s) => s.memberId === m.id))
   )
   ```

3. **Add computed `allowsPartialAttendance` and `allowsWeekendVisit`**:
   ```typescript
   const allowsPartialAttendance = computed(
     () => !!edition.value?.pricePerAdultWeek
   )
   const allowsWeekendVisit = computed(
     () => !!edition.value?.weekendStartDate
   )
   const periodDays = computed(() => {
     if (!edition.value) return { firstWeekDays: 0, secondWeekDays: 0, totalDays: 0 }
     return computePeriodDays(
       edition.value.startDate,
       edition.value.endDate,
       edition.value.halfDate ?? null
     )
   })
   ```

4. **Validate WeekendVisit completeness** (for "Siguiente" button):
   ```typescript
   const weekendVisitIsValid = computed(() =>
     selectedMembers.value
       .filter((s) => s.attendancePeriod === 'WeekendVisit')
       .every((s) => s.visitStartDate != null && s.visitEndDate != null)
   )

   const canProceedFromStep1 = computed(
     () => selectedMembers.value.length > 0 && isRepresentative.value && weekendVisitIsValid.value
   )
   ```

5. **Update `RegistrationMemberSelector` usage in template** — pass `edition`:
   ```html
   <RegistrationMemberSelector
     v-else
     v-model="selectedMembers"
     :members="familyMembers"
     :edition="edition!"
   />
   ```

6. **Update "Siguiente" button disabled condition**:
   ```html
   :disabled="!canProceedFromStep1"
   ```

7. **Update step 3 review — selected participants list** (show periods):
   ```html
   <li
     v-for="member in selectedMemberDetails"
     :key="member.id"
     class="text-sm text-gray-800"
   >
     {{ member.firstName }} {{ member.lastName }}
     <span class="ml-1 text-xs text-gray-500">
       · {{ ATTENDANCE_PERIOD_LABELS[
         selectedMembers.find(s => s.memberId === member.id)!.attendancePeriod
       ] }}
     </span>
   </li>
   ```

8. **Update step 3 pricing reference** — show week/weekend prices if available:
   ```html
   <div v-if="edition" class="mb-4 rounded-lg border border-blue-100 bg-blue-50 p-4">
     <h3 class="mb-2 text-sm font-semibold text-blue-800">Precios de referencia</h3>
     <div class="overflow-x-auto">
       <table class="text-sm text-blue-700 w-full">
         <thead>
           <tr>
             <th class="text-left font-medium pb-1">Categoría</th>
             <th class="text-right font-medium pb-1">Completo</th>
             <th v-if="allowsPartialAttendance" class="text-right font-medium pb-1">
               1ª sem. ({{ periodDays.firstWeekDays }}d)
             </th>
             <th v-if="allowsPartialAttendance" class="text-right font-medium pb-1">
               2ª sem. ({{ periodDays.secondWeekDays }}d)
             </th>
             <th v-if="allowsWeekendVisit" class="text-right font-medium pb-1">Fin de semana</th>
           </tr>
         </thead>
         <tbody>
           <tr>
             <td>Adulto/a</td>
             <td class="text-right">{{ formatCurrency(edition.pricePerAdult) }}</td>
             <td v-if="allowsPartialAttendance" class="text-right">
               {{ edition.pricePerAdultWeek ? formatCurrency(edition.pricePerAdultWeek) : '—' }}
             </td>
             <td v-if="allowsPartialAttendance" class="text-right">
               {{ edition.pricePerAdultWeek ? formatCurrency(edition.pricePerAdultWeek) : '—' }}
             </td>
             <td v-if="allowsWeekendVisit" class="text-right">
               {{ edition.pricePerAdultWeekend ? formatCurrency(edition.pricePerAdultWeekend) : '—' }}
             </td>
           </tr>
           <!-- repeat for Niño/Niña and Bebé -->
         </tbody>
       </table>
     </div>
     <p class="mt-2 text-xs text-blue-600">
       El precio final se calculará al confirmar según las categorías de edad de cada persona.
     </p>
   </div>
   ```

9. **Update `handleConfirm`** — map `WizardMemberSelection[]` to `MemberAttendanceRequest[]`:
   ```typescript
   const handleConfirm = async () => {
     if (!familyUnit.value) return

     const created = await createRegistration({
       campEditionId: editionId.value,
       familyUnitId: familyUnit.value.id,
       members: selectedMembers.value.map((s) => ({
         memberId: s.memberId,
         attendancePeriod: s.attendancePeriod,
         visitStartDate: s.visitStartDate ?? null,
         visitEndDate: s.visitEndDate ?? null
       })),
       notes: notes.value || null
     })
     // rest is unchanged
   }
   ```

10. **Update confirm button disabled logic**:
    ```html
    :disabled="selectedMembers.value.length === 0"
    ```
    (or use `canProceedFromStep1` if you want the same check here)

---

### Step 6: Update `RegistrationPricingBreakdown.vue`

- **File**: `frontend/src/components/registrations/RegistrationPricingBreakdown.vue`
- **Action**: Add `attendancePeriod` and `attendanceDays` columns to the members table; conditionally show them only when any member has a non-Complete period

**Implementation Steps**:

1. **Update script imports**:
   ```typescript
   import type { PricingBreakdown, AgeCategory, AttendancePeriod } from '@/types/registration'
   import { ATTENDANCE_PERIOD_LABELS } from '@/utils/registration'
   ```

2. **Add computed `showPeriodColumn`**:
   ```typescript
   const showPeriodColumn = computed(() =>
     props.pricing.members.some(
       (m) => m.attendancePeriod && m.attendancePeriod !== 'Complete'
     )
   )
   ```

3. **Update members table template** — add two optional columns:
   ```html
   <thead class="bg-gray-50">
     <tr>
       <th class="px-4 py-2 text-left font-medium text-gray-600">Nombre</th>
       <th class="px-4 py-2 text-left font-medium text-gray-600">Categoría</th>
       <th v-if="showPeriodColumn" class="px-4 py-2 text-left font-medium text-gray-600">
         Periodo
       </th>
       <th class="px-4 py-2 text-right font-medium text-gray-600">Importe</th>
     </tr>
   </thead>
   <tbody>
     <tr v-for="member in pricing.members" :key="member.familyMemberId" ...>
       <td class="px-4 py-2 text-gray-900">{{ member.fullName }}</td>
       <td class="px-4 py-2 text-gray-600">
         {{ AGE_CATEGORY_LABELS[member.ageCategory] }}
         <span class="text-xs text-gray-400">({{ member.ageAtCamp }} años)</span>
       </td>
       <td v-if="showPeriodColumn" class="px-4 py-2 text-gray-500 text-sm"
           :data-testid="`member-period-${member.familyMemberId}`">
         {{ ATTENDANCE_PERIOD_LABELS[member.attendancePeriod] }}
         <span class="text-xs text-gray-400">({{ member.attendanceDays }}d)</span>
         <!-- WeekendVisit visit dates -->
         <span
           v-if="member.attendancePeriod === 'WeekendVisit' && member.visitStartDate"
           class="block text-xs text-gray-400"
         >
           {{ member.visitStartDate }} — {{ member.visitEndDate }}
         </span>
       </td>
       <td class="px-4 py-2 text-right text-gray-900">
         {{ formatCurrency(member.individualAmount) }}
       </td>
     </tr>
     <!-- colspan updated: 3 normally, 4 when showPeriodColumn -->
     <tr class="border-t border-gray-200 bg-gray-50">
       <td :colspan="showPeriodColumn ? 3 : 2" class="px-4 py-2 font-medium text-gray-700">
         Subtotal participantes
       </td>
       <td class="px-4 py-2 text-right font-medium text-gray-900">
         {{ formatCurrency(pricing.baseTotalAmount) }}
       </td>
     </tr>
   </tbody>
   ```

---

### Step 7: Update Tests — `RegistrationMemberSelector.test.ts`

- **File**: `frontend/src/components/registrations/__tests__/RegistrationMemberSelector.test.ts`
- **Action**: Update mount helper to use `WizardMemberSelection[]` modelValue + edition prop; add period selector tests

**Implementation Steps**:

1. **Update imports and `mountComponent` helper**:
   ```typescript
   import type { WizardMemberSelection } from '@/types/registration'
   import type { CampEdition } from '@/types/camp-edition'

   const mockEditionComplete: Partial<CampEdition> = {
     startDate: '2025-07-01',
     endDate: '2025-07-14',
     pricePerAdultWeek: null,    // no partial attendance
     weekendStartDate: null,     // no weekend visits
   }

   const mockEditionWithPeriods: Partial<CampEdition> = {
     startDate: '2025-07-01',
     endDate: '2025-07-14',
     pricePerAdultWeek: 110,
     halfDate: null,
     weekendStartDate: '2025-07-05',
     weekendEndDate: '2025-07-07',
   }

   const mountComponent = (
     modelValue: WizardMemberSelection[] = [],
     edition: Partial<CampEdition> = mockEditionComplete
   ) =>
     mount(RegistrationMemberSelector, {
       props: { members: mockMembers, modelValue, edition },
       global: { plugins: [PrimeVue] }
     })
   ```

2. **Update existing tests** — change `string[]` to `WizardMemberSelection[]`:
   - `mountComponent([])` stays valid
   - Any check on `wrapper.emitted('update:modelValue')` now expects `WizardMemberSelection[]` not `string[]`

3. **Add new test cases**:

   ```typescript
   it('should not show period selector when allowsPartialAttendance is false', async () => {
     const wrapper = mountComponent(
       [{ memberId: 'member-1', attendancePeriod: 'Complete', visitStartDate: null, visitEndDate: null }],
       mockEditionComplete
     )
     expect(wrapper.find('[data-testid="period-select-member-1"]').exists()).toBe(false)
   })

   it('should show period selector when member is selected and edition allows periods', () => {
     const wrapper = mountComponent(
       [{ memberId: 'member-1', attendancePeriod: 'Complete', visitStartDate: null, visitEndDate: null }],
       mockEditionWithPeriods
     )
     expect(wrapper.find('[data-testid="period-select-member-1"]').exists()).toBe(true)
   })

   it('should emit WizardMemberSelection with Complete period when member first checked', async () => {
     const wrapper = mountComponent([], mockEditionWithPeriods)
     const input = wrapper.find('input[type="checkbox"]')
     await input.trigger('change')
     const emitted = wrapper.emitted('update:modelValue') as WizardMemberSelection[][]
     expect(emitted).toBeTruthy()
     const emittedSelections = emitted[emitted.length - 1][0]
     expect(emittedSelections[0].attendancePeriod).toBe('Complete')
   })

   it('should show WeekendVisit date pickers when WeekendVisit period is selected', () => {
     const wrapper = mountComponent(
       [{ memberId: 'member-1', attendancePeriod: 'WeekendVisit', visitStartDate: null, visitEndDate: null }],
       mockEditionWithPeriods
     )
     expect(wrapper.find('[data-testid="visit-start-member-1"]').exists()).toBe(true)
     expect(wrapper.find('[data-testid="visit-end-member-1"]').exists()).toBe(true)
   })
   ```

---

### Step 8: Update Tests — `RegistrationPricingBreakdown.test.ts`

- **File**: `frontend/src/components/registrations/__tests__/RegistrationPricingBreakdown.test.ts`
- **Action**: Update `MemberPricingDetail` mock data to include new required fields; add period column test

**Implementation Steps**:

1. **Update mock `MemberPricingDetail`** to include new fields (add to all existing mock members):
   ```typescript
   {
     familyMemberId: 'member-1',
     fullName: 'Juan García',
     ageAtCamp: 35,
     ageCategory: 'Adult' as AgeCategory,
     attendancePeriod: 'Complete' as AttendancePeriod,
     attendanceDays: 14,
     visitStartDate: null,
     visitEndDate: null,
     individualAmount: 500
   }
   ```

2. **Add test for period column visibility**:
   ```typescript
   it('should not show period column when all members have Complete period', () => {
     // mount with all Complete members
     expect(wrapper.find('[data-testid="member-period-member-1"]').exists()).toBe(false)
   })

   it('should show period column when any member has non-Complete period', () => {
     const pricingWithPeriods = {
       ...mockPricing,
       members: [{
         ...mockMember,
         attendancePeriod: 'FirstWeek' as AttendancePeriod,
         attendanceDays: 7
       }]
     }
     const wrapper = mount(RegistrationPricingBreakdown, {
       props: { pricing: pricingWithPeriods },
       global: { plugins: [PrimeVue] }
     })
     expect(wrapper.find('[data-testid="member-period-member-1"]').exists()).toBe(true)
     expect(wrapper.text()).toContain('Primera semana')
   })
   ```

---

### Step 9: Create Utility Tests — `frontend/src/utils/__tests__/registration.test.ts`

- **File**: `frontend/src/utils/__tests__/registration.test.ts` (new file)
- **Action**: Unit tests for `computePeriodDays` and `getAllowedPeriods`

**Implementation Steps**:

```typescript
import { describe, it, expect } from 'vitest'
import { computePeriodDays, getAllowedPeriods, ATTENDANCE_PERIOD_LABELS } from '@/utils/registration'

describe('computePeriodDays', () => {
  it('returns correct total days', () => {
    const result = computePeriodDays('2025-07-01', '2025-07-15', null)
    expect(result.totalDays).toBe(14)
  })

  it('splits evenly when no halfDate', () => {
    const result = computePeriodDays('2025-07-01', '2025-07-15', null)
    expect(result.firstWeekDays).toBe(7)
    expect(result.secondWeekDays).toBe(7)
  })

  it('uses halfDate when provided', () => {
    const result = computePeriodDays('2025-07-01', '2025-07-14', '2025-07-05')
    expect(result.firstWeekDays).toBe(4)  // Jul 1 → Jul 5
    expect(result.secondWeekDays).toBe(9) // Jul 5 → Jul 14
  })
})

describe('getAllowedPeriods', () => {
  it('returns only Complete when no week price or weekend dates', () => {
    expect(getAllowedPeriods({ pricePerAdultWeek: null, weekendStartDate: null }))
      .toEqual(['Complete'])
  })

  it('includes week periods when pricePerAdultWeek is set', () => {
    const periods = getAllowedPeriods({ pricePerAdultWeek: 110, weekendStartDate: null })
    expect(periods).toContain('FirstWeek')
    expect(periods).toContain('SecondWeek')
  })

  it('includes WeekendVisit when weekendStartDate is set', () => {
    const periods = getAllowedPeriods({ pricePerAdultWeek: null, weekendStartDate: '2025-07-05' })
    expect(periods).toContain('WeekendVisit')
  })
})

describe('ATTENDANCE_PERIOD_LABELS', () => {
  it('has Spanish label for all four periods', () => {
    expect(ATTENDANCE_PERIOD_LABELS.Complete).toBe('Campamento completo')
    expect(ATTENDANCE_PERIOD_LABELS.FirstWeek).toBe('Primera semana')
    expect(ATTENDANCE_PERIOD_LABELS.SecondWeek).toBe('Segunda semana')
    expect(ATTENDANCE_PERIOD_LABELS.WeekendVisit).toBe('Visita de fin de semana')
  })
})
```

---

### Step 10: Update Technical Documentation

- **Action**: Update `ai-specs/specs/api-spec.yml` for registration endpoint shape changes

**Implementation Steps**:

1. Update `POST /api/registrations` request body: replace `memberIds: array[string]` with `members: array[MemberAttendanceRequest]`
2. Update `PUT /api/registrations/{id}/members` similarly
3. Add `MemberAttendanceRequest` schema with `memberId`, `attendancePeriod` (enum), `visitStartDate?`, `visitEndDate?`
4. Update `MemberPricingDetail` schema to include `attendancePeriod`, `attendanceDays`, `visitStartDate?`, `visitEndDate?`
5. Update `AvailableCampEditionResponse` schema with all new week/weekend fields

---

## Implementation Order

1. **Step 0**: Create feature branch
2. **Step 1**: Update `registration.ts` types ← unblocks everything else
3. **Step 2**: Update `camp-edition.ts` types ← unblocks wizard
4. **Step 3**: Create `utils/registration.ts` ← unblocks selector and wizard
5. **Step 9**: Write utility tests (TDD — write before implementing utils if possible)
6. **Step 4**: Refactor `RegistrationMemberSelector.vue`
7. **Step 7**: Update `RegistrationMemberSelector.test.ts`
8. **Step 5**: Update `RegisterForCampPage.vue`
9. **Step 6**: Update `RegistrationPricingBreakdown.vue`
10. **Step 8**: Update `RegistrationPricingBreakdown.test.ts`
11. **Step 10**: Update API documentation

---

## Testing Checklist

- [ ] `computePeriodDays` — even split, halfDate split, edge cases
- [ ] `getAllowedPeriods` — Complete-only, with week periods, with weekend, all three
- [ ] `RegistrationMemberSelector` — renders members, emits WizardMemberSelection, shows period selector only when multiple periods allowed, shows WeekendVisit date pickers, defaults to Complete
- [ ] `RegistrationPricingBreakdown` — hides period column when all Complete, shows column with Spanish label when non-Complete period present
- [ ] `RegisterForCampPage` (manual/E2E) — wizard proceeds only when WeekendVisit dates are set, review shows period per member, confirm sends `members[]` not `memberIds[]`
- [ ] Medical privacy: existing test must still pass — member selector never exposes note content

---

## Error Handling Patterns

- **Weekend date validation** — the backend returns `422 INVALID_VISIT_DATES` if visit dates are outside camp bounds or exceed 3 days. The frontend should prevent this via DatePicker constraints (`minDate`/`maxDate`), but the `createRegistration` error toast in `RegisterForCampPage.vue` already handles backend validation errors generically via `error.value`.
- **Partial attendance not allowed** — backend returns `422 PARTIAL_ATTENDANCE_NOT_ALLOWED` if week price not set. This is prevented in the frontend by only offering week periods when `pricePerAdultWeek != null`. The existing error toast handles any slipthrough.
- **Loading state** — `loading` from `useRegistrations` already shown on the confirm button spinner; no changes needed.

---

## UI/UX Considerations

- **Period selector placement**: Inside the member card, below the name/info row. Use `@click.stop` to prevent the `<label>` from toggling the checkbox when interacting with the Select/DatePicker.
- **`Select` (dropdown) vs `SelectButton`**: Use `Select` dropdown for periods — the label text is too long for `SelectButton` on mobile. `SelectButton` works if all labels fit horizontally (only viable for 2 options).
- **DatePicker**: PrimeVue v4 renamed `Calendar` → `DatePicker`. Use `date-format="dd/mm/yy"` for consistency with other date displays in the app.
- **Show period info only when edition supports it**: When only `Complete` is available, the selector is hidden entirely — no visual noise for the common case.
- **Review step pricing table**: Only add columns when `allowsPartialAttendance` or `allowsWeekendVisit` — keeps the common case clean.
- **Responsive**: The member selector card expands vertically when a period row is added — ensure the card layout uses `flex-col` (already does) and the period Select is `w-full`.

---

## Dependencies

No new npm packages required. All components used are already in PrimeVue v4:
- `Select` (v4 — was `Dropdown` in v3) — already used elsewhere in the app
- `DatePicker` (v4 — was `Calendar` in v3) — check if used elsewhere; if not, verify it's in the PrimeVue v4 bundle
- `Checkbox` — already used in `RegistrationMemberSelector`

---

## Notes

- **Backend branch**: The backend is on `feature/feat-registration-attendance-period-backend` (PR #67). Coordinate merging before releasing the frontend. The API is **not** backwards-compatible for the `members` field.
- **`WeekendVisit` labels**: Use `'Visita de fin de semana'` in Spanish. Do NOT use `'WeekendVisit'` as display text.
- **`visitStartDate` / `visitEndDate` format**: Always ISO `YYYY-MM-DD` strings (not full DateTime). The `DatePicker` returns a `Date` object; convert with `.toISOString().split('T')[0]`.
- **TypeScript strict**: No `any`. The `getSelection()` helper returns `WizardMemberSelection | undefined` — use `?.` and `!` assertion only when you've guarded for existence.
- **Existing `selectedMembers` computed rename**: The page currently has `const selectedMembers = computed(...)` (the FamilyMemberResponse objects). This MUST be renamed to `selectedMemberDetails` to avoid a naming collision with the new `const selectedMembers = ref<WizardMemberSelection[]>([])`.

---

## Next Steps After Implementation

1. Ensure backend PR #67 is merged before releasing frontend
2. Run `dotnet ef database update` on the deployment environment to apply the migration
3. Consider a future ticket for the admin camp edition form to expose the week/weekend pricing fields in the UI (currently the backend accepts them but the admin form doesn't have inputs for them)
4. `RegistrationDetailPage.vue` — currently read-only display using `RegistrationPricingBreakdown`; no additional changes required as the breakdown component will automatically show the period column when periods differ

---

## Implementation Verification

- [ ] **TypeScript strict** — no `any`, all props/emits fully typed, `WizardMemberSelection` used consistently
- [ ] **No `<style>` blocks** — only Tailwind utility classes
- [ ] **Composition API** — `<script setup lang="ts">` in all components
- [ ] **API calls via composable** — `createRegistration` called only through `useRegistrations`
- [ ] **Period selector hidden when only Complete is allowed** — verified with `getAllowedPeriods`
- [ ] **WeekendVisit date pickers respect camp weekend window** — `minDate`/`maxDate` set from `edition.weekendStartDate`/`weekendEndDate`
- [ ] **Medical privacy test still passes** — no content leaked in selector
- [ ] **Vitest tests green** — `utils/registration.test.ts`, `RegistrationMemberSelector.test.ts`, `RegistrationPricingBreakdown.test.ts`
- [ ] **Documentation updated** — `api-spec.yml` reflects new request/response shapes
