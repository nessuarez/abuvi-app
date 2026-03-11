# Feature: Member Data Completeness Warnings

## Summary

Show visual warnings on family members (adults) who are missing critical data (DNI, email, and/or correct birth date) in two key areas: the **Family Unit management page** and the **Registration member selector**. Additionally, provide a quick link from the registration flow to the family unit management page so users can update missing data without leaving the flow. **No blocking** — just soft encouragement with clear messaging about why this data matters (insurance, legal requirements for camp registration).

## User Story

**As a** family unit representative,
**I want to** see clear visual indicators when adult family members are missing DNI, email, or a valid birth date,
**So that** I can complete their data before or during registration, ensuring all legal and insurance requirements are met for the camp.

## Acceptance Criteria

1. **Family Member List (`FamilyMemberList.vue`)**: Each adult member row shows a warning icon (⚠️ / `pi-exclamation-triangle`) next to their name if they are missing any of:
   - `documentNumber` (DNI) — null or empty
   - `email` — null or empty
   - `dateOfBirth` — considered "incorrect" if the year is before 1900 or after today (placeholder/dummy dates)
2. **Hovering/clicking the warning icon** shows a tooltip listing exactly which fields are missing (e.g., "Falta: DNI, Email").
3. **Registration Member Selector (`RegistrationMemberSelector.vue`)**: Each adult member card shows the same warning icon and tooltip when data is incomplete.
4. **Registration Member Selector**: A `Message` banner (severity `warn`) appears above the member grid when **any** selected adult member has incomplete data, with text explaining the importance of complete data and a **router-link** to `/mi-familia` (or the family unit page route).
5. **No blocking**: Users can still proceed with registration even if members have incomplete data. The warnings are purely informational.
6. **Minors** (age < 18) are **not** flagged for missing DNI or email (these are expected to be optional for children). Only birth date correctness applies to minors.
7. The warning icon uses PrimeVue's `pi-exclamation-triangle` icon with `text-orange-500` color, consistent with existing warning patterns.

## Technical Implementation

### Frontend Changes Only (no backend changes needed)

#### 1. Create utility function: `frontend/src/utils/member-validation.ts`

```typescript
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

  const birthYear = new Date(member.dateOfBirth).getFullYear()
  const invalidBirthDate = birthYear < 1900 || birthYear > new Date().getFullYear()

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
```

#### 2. Modify `FamilyMemberList.vue`

**File**: `frontend/src/components/family-units/FamilyMemberList.vue`

**Changes**:

- Import `getMemberDataWarnings`, `getWarningMessage` from `@/utils/member-validation`
- In the `membersWithAge` computed, add a `warnings` property to each member using `getMemberDataWarnings(member, age >= 18)`
- In the Name column template (`<Column field="firstName">`), after the name `<div>`, add:

  ```vue
  <i
    v-if="data.warnings"
    class="pi pi-exclamation-triangle text-orange-500 ml-2"
    v-tooltip.top="getWarningMessage(data.warnings)"
  />
  ```

- Add a new `Message` component (severity `warn`) below the DataTable when any member has warnings, encouraging users to complete data. Text example:
  > "Algunos miembros adultos tienen datos incompletos (DNI, email o fecha de nacimiento). Estos datos son necesarios para la inscripción oficial en el campamento por motivos legales y de seguro. Por favor, asegúrate de que cada nombre, apellido, DNI y email sea correcto y no esté repetido."

#### 3. Modify `RegistrationMemberSelector.vue`

**File**: `frontend/src/components/registrations/RegistrationMemberSelector.vue`

**Changes**:

- Import `getMemberDataWarnings`, `getWarningMessage` from `@/utils/member-validation`
- Import `Message` from `primevue/message`
- Import `RouterLink` from `vue-router` (or use `router.push`)
- Add `isMinor` already exists in the component — reuse it
- In each member card, after the name `<span>`, add the warning icon:

  ```vue
  <i
    v-if="getMemberWarnings(member)"
    class="pi pi-exclamation-triangle text-orange-500"
    v-tooltip.top="getWarningMessage(getMemberWarnings(member)!)"
  />
  ```

  Where `getMemberWarnings` is a helper method calling `getMemberDataWarnings(member, !isMinor(member))`
- **Above the member grid** (`<div class="grid ...">`) add a conditional `Message` banner:

  ```vue
  <Message v-if="hasIncompleteSelectedMembers" severity="warn" :closable="false">
    Algunos miembros tienen datos incompletos (DNI, email o fecha de nacimiento) que son necesarios
    para la inscripción oficial por motivos legales y de seguro.
    <RouterLink to="/mi-familia" class="font-semibold underline ml-1">
      Actualizar datos de la familia
    </RouterLink>
  </Message>
  ```

  Where `hasIncompleteSelectedMembers` is a computed that checks if any selected adult member has warnings.

#### 4. Verify the family unit page route

**File**: `frontend/src/router/index.ts`

Confirm the route path for the family unit page (likely `/mi-familia` or `/family-unit`). Use the correct path in the `RouterLink`.

### Files to Modify

| File | Change |
|------|--------|
| `frontend/src/utils/member-validation.ts` | **NEW** — Utility functions for data completeness checks |
| `frontend/src/components/family-units/FamilyMemberList.vue` | Add warning icons + banner message |
| `frontend/src/components/registrations/RegistrationMemberSelector.vue` | Add warning icons + banner with link to family management |

### Files to NOT Modify

- No backend changes needed — all data is already available in `FamilyMemberResponse`
- No database changes
- No API changes
- No type changes

## UI/UX Details

- **Warning icon**: `pi pi-exclamation-triangle` in `text-orange-500`, placed inline next to the member name
- **Tooltip**: On hover, shows exactly which fields are missing (e.g., "Falta: DNI, Email")
- **Banner message**: PrimeVue `<Message severity="warn" :closable="false">` with encouraging (not alarming) text
- **Link**: Standard `RouterLink` with underline styling to navigate to family management
- **No blocking**: Registration flow continues regardless of warnings
- **Language**: All text in Spanish (consistent with existing UI)

## Testing Checklist

- [ ] Adult member without DNI shows warning icon in FamilyMemberList
- [ ] Adult member without email shows warning icon in FamilyMemberList
- [ ] Adult member with birth date year < 1900 shows warning icon
- [ ] Adult member with all data complete shows NO warning
- [ ] Minor member without DNI/email does NOT show warning
- [ ] Warning tooltip shows correct missing fields list
- [ ] RegistrationMemberSelector shows warning icons on incomplete adult members
- [ ] Banner message appears when selecting adult members with incomplete data
- [ ] Banner message contains working link to family management page
- [ ] Banner message disappears when no selected members have warnings
- [ ] Registration can proceed (no blocking) even with warnings present
- [ ] Warning message text is in Spanish

## Non-Functional Requirements

- No performance impact — warnings are computed client-side from existing data
- Accessibility: tooltips must be keyboard-accessible (PrimeVue `v-tooltip` handles this)
- Responsive: warning icons must be visible on mobile layouts
