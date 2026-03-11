# Frontend Implementation Plan: feat-member-data-completeness-warnings

## Overview

Add visual warnings (orange exclamation icons with tooltips) on adult family members who are missing critical data (DNI, email, or valid birth date) in two key areas: the **Family Member List** (family unit management) and the **Registration Member Selector**. Additionally, show a warning banner with a link to family management when selecting incomplete members during registration. No blocking — purely informational.

**Stack**: Vue 3 Composition API, PrimeVue (`Message`, `v-tooltip`), Tailwind CSS, TypeScript.

## Architecture Context

### Components Involved
- `frontend/src/components/family-units/FamilyMemberList.vue` — DataTable listing family members (add warning icons)
- `frontend/src/components/registrations/RegistrationMemberSelector.vue` — Member selection cards for registration (add warning icons + banner)

### New Files
- `frontend/src/utils/member-validation.ts` — Pure utility functions for data completeness checks

### Existing Test Files
- `frontend/src/components/family-units/__tests__/FamilyMemberList.spec.ts`
- `frontend/src/components/registrations/__tests__/RegistrationMemberSelector.test.ts`

### Routing
- Family unit page route: `/family-unit` (name: `family-unit`) — used for the "update data" link in the registration banner

### State Management
- No Pinia store changes needed. All warnings are computed locally from existing `FamilyMemberResponse` data.

## Implementation Steps

### Step 0: Create Feature Branch

- **Action**: Create and switch to a new feature branch
- **Branch Naming**: `feature/feat-member-data-completeness-warnings-frontend`
- **Implementation Steps**:
  1. Ensure on latest `dev` branch: `git checkout dev && git pull origin dev`
  2. Create new branch: `git checkout -b feature/feat-member-data-completeness-warnings-frontend`
  3. Verify: `git branch`

### Step 1: Create Utility Functions

- **File**: `frontend/src/utils/member-validation.ts` (NEW)
- **Action**: Create pure utility functions for member data completeness checks
- **Implementation Steps**:
  1. Define `MemberDataWarning` interface:
     ```typescript
     export interface MemberDataWarning {
       missingDni: boolean
       missingEmail: boolean
       invalidBirthDate: boolean
     }
     ```
  2. Implement `getMemberDataWarnings(member: FamilyMemberResponse, isAdult: boolean): MemberDataWarning | null`:
     - `missingDni`: only for adults — `documentNumber` is null, undefined, or empty string after trim
     - `missingEmail`: only for adults — `email` is null, undefined, or empty string after trim
     - `invalidBirthDate`: for all ages — birth year < 1900 or > current year (catches placeholder dates like `0001-01-01` or future dates)
     - Return `null` if no warnings (all checks pass) — this makes it easy to use as a boolean in `v-if`
  3. Implement `getWarningMessage(warnings: MemberDataWarning): string`:
     - Build array of missing field labels: `'DNI'`, `'Email'`, `'Fecha de nacimiento'`
     - Return `'Falta: ${missing.join(', ')}'`
  4. Use `parseDateLocal` from `@/utils/date` for birth date parsing (consistent with rest of app)
- **Dependencies**: `@/types/family-unit` (FamilyMemberResponse), `@/utils/date` (parseDateLocal)
- **Implementation Notes**: Keep functions pure — no Vue reactivity, no side effects. Import `FamilyMemberResponse` as type-only.

### Step 2: Create Unit Tests for Utility Functions

- **File**: `frontend/src/utils/__tests__/member-validation.test.ts` (NEW)
- **Action**: Write Vitest unit tests for the utility functions
- **Implementation Steps**:
  1. Test `getMemberDataWarnings`:
     - Adult with all data → returns `null`
     - Adult missing DNI only → returns `{ missingDni: true, missingEmail: false, invalidBirthDate: false }`
     - Adult missing email only → returns warning with `missingEmail: true`
     - Adult missing both DNI and email → returns both flags true
     - Adult with birth year `0001` → returns `invalidBirthDate: true`
     - Adult with birth year in future → returns `invalidBirthDate: true`
     - Minor missing DNI and email → returns `null` (not flagged for these fields)
     - Minor with invalid birth date → returns warning with `invalidBirthDate: true`
     - Adult with empty string DNI (after trim) → returns `missingDni: true`
  2. Test `getWarningMessage`:
     - Single missing field → `'Falta: DNI'`
     - Multiple missing fields → `'Falta: DNI, Email'`
     - All three missing → `'Falta: DNI, Email, Fecha de nacimiento'`
- **Dependencies**: vitest

### Step 3: Add Warning Icons to FamilyMemberList

- **File**: `frontend/src/components/family-units/FamilyMemberList.vue`
- **Action**: Show warning icons next to adult member names with incomplete data
- **Implementation Steps**:
  1. Add imports:
     ```typescript
     import { getMemberDataWarnings, getWarningMessage } from '@/utils/member-validation'
     ```
  2. In the `membersWithAge` computed, extend each member object with a `warnings` property:
     ```typescript
     const warnings = getMemberDataWarnings(member, age >= 18)
     return { ...member, age, warnings }
     ```
  3. In the Name column template (after the name `<div class="font-medium">`), add the warning icon inline:
     ```vue
     <i
       v-if="data.warnings"
       class="pi pi-exclamation-triangle text-orange-500 ml-1 text-xs"
       v-tooltip.top="getWarningMessage(data.warnings)"
       data-testid="member-warning-icon"
     />
     ```
  4. Add `getWarningMessage` to the template scope (already imported in script setup, so it's automatically available).
  5. Below the `</DataTable>`, add a summary `Message` when any member has warnings:
     ```vue
     <Message
       v-if="membersWithWarnings"
       severity="warn"
       :closable="false"
       class="mt-3"
     >
       Algunos miembros adultos tienen datos incompletos (DNI, email o fecha de nacimiento).
       Estos datos son necesarios para la inscripción oficial en el campamento
       por motivos legales y de seguro. Asegúrate de que cada nombre, apellido,
       DNI y email sea correcto y único.
     </Message>
     ```
     Where `membersWithWarnings` is a computed:
     ```typescript
     const membersWithWarnings = computed(() =>
       membersWithAge.value.some((m) => m.warnings !== null)
     )
     ```
  6. Import `Message` from `primevue/message`.
- **Dependencies**: `primevue/message`
- **Implementation Notes**:
  - The warning icon sits inline next to the name, not in a separate column
  - Tooltip uses PrimeVue's `v-tooltip` directive (already registered globally via `Tooltip` in test config)
  - The `Message` banner only shows when viewing your own family (not in `readOnly` mode) — wrap with `v-if="!readOnly && membersWithWarnings"`

### Step 4: Update FamilyMemberList Tests

- **File**: `frontend/src/components/family-units/__tests__/FamilyMemberList.spec.ts`
- **Action**: Add tests for the warning icon rendering
- **Implementation Steps**:
  1. Add test: adult member without DNI → warning icon is rendered (`data-testid="member-warning-icon"`)
  2. Add test: adult member with all data complete → warning icon is NOT rendered
  3. Add test: minor member without DNI/email → warning icon is NOT rendered
  4. Add test: warning Message banner appears when any member has warnings
  5. Add test: warning Message banner does NOT appear when all members have complete data
  6. Use existing `mockMember` fixture (which has `documentNumber: null`, `email: null`) — this member should show warnings
  7. Create a new `completeMember` fixture with all fields filled for the "no warning" test
- **Dependencies**: existing test infrastructure (vitest, vue-test-utils, PrimeVue config)

### Step 5: Add Warning Icons and Banner to RegistrationMemberSelector

- **File**: `frontend/src/components/registrations/RegistrationMemberSelector.vue`
- **Action**: Show warning icons on member cards + a warning banner with link to family management
- **Implementation Steps**:
  1. Add imports:
     ```typescript
     import { getMemberDataWarnings, getWarningMessage } from '@/utils/member-validation'
     import Message from 'primevue/message'
     import { RouterLink } from 'vue-router'
     ```
  2. Add a helper function that combines `isMinor` with `getMemberDataWarnings`:
     ```typescript
     const getMemberWarnings = (member: FamilyMemberResponse) =>
       getMemberDataWarnings(member, !isMinor(member))
     ```
  3. In each member card, after the name `<span class="font-medium text-gray-900">`, add:
     ```vue
     <i
       v-if="getMemberWarnings(member)"
       class="pi pi-exclamation-triangle text-orange-500"
       v-tooltip.top="getWarningMessage(getMemberWarnings(member)!)"
       data-testid="member-warning-icon"
       @click.stop
     />
     ```
     Note the `@click.stop` to prevent toggling the checkbox when clicking the warning icon.
  4. Add a computed for whether any selected adult member has incomplete data:
     ```typescript
     const hasIncompleteSelectedMembers = computed(() =>
       props.modelValue.some((sel) => {
         const member = props.members.find((m) => m.id === sel.memberId)
         return member && getMemberWarnings(member) !== null
       })
     )
     ```
  5. Above the member grid (`<div class="grid ...">`) add the warning banner:
     ```vue
     <Message v-if="hasIncompleteSelectedMembers" severity="warn" :closable="false" class="mb-3">
       Algunos miembros tienen datos incompletos (DNI, email o fecha de nacimiento)
       necesarios para la inscripción oficial por motivos legales y de seguro.
       Por favor, asegúrate de que los datos sean correctos y no se repitan.
       <RouterLink to="/family-unit" class="font-semibold underline ml-1">
         Actualizar datos de la familia
       </RouterLink>
     </Message>
     ```
  6. The `RouterLink` points to `/family-unit` (the authenticated user's family unit page).
- **Dependencies**: `primevue/message`, `vue-router` (RouterLink)
- **Implementation Notes**:
  - The `@click.stop` on the warning icon prevents the card click handler from toggling selection
  - The banner only appears when at least one **selected** member has warnings (not just any member in the list)
  - The link opens in the same tab — the user can use browser back to return to registration

### Step 6: Update RegistrationMemberSelector Tests

- **File**: `frontend/src/components/registrations/__tests__/RegistrationMemberSelector.test.ts`
- **Action**: Add tests for warning icons and banner in the registration member selector
- **Implementation Steps**:
  1. Note: existing `mockMembers[0]` (Juan García) has `documentNumber: '12345678A'` but `email: null` — this adult should show a warning
  2. Add test: adult member without email shows warning icon
  3. Add test: minor member (Ana García, born 2015) without DNI/email does NOT show warning icon
  4. Add test: warning banner appears when a selected adult member has incomplete data
  5. Add test: warning banner does NOT appear when no members are selected
  6. Add test: warning banner contains link to `/family-unit`
  7. Create a mock member with all fields complete to test the "no warning" case
  8. Add `RouterLink` stub or use `vue-router` mock in the mount config:
     ```typescript
     global: {
       plugins: [PrimeVue],
       stubs: { RouterLink: true }
     }
     ```
- **Dependencies**: existing test infrastructure

### Step 7: Update Technical Documentation

- **Action**: Review and update technical documentation according to changes made
- **Implementation Steps**:
  1. **Review Changes**: Analyze all code changes made during implementation
  2. **Identify Documentation Files**: Determine which documentation files need updates:
     - No API changes → no `api-spec.yml` update needed
     - New utility file and UI pattern → potentially note in `frontend-standards.mdc` if the member validation pattern is reusable
     - No routing changes
     - No new dependencies
  3. **Update Documentation**:
     - If the enriched spec at `ai-specs/changes/feat-member-data-completeness-warnings_enriched.md` needs any corrections based on actual implementation, update it
  4. **Verify Documentation**: Confirm all changes are accurately reflected
  5. **Report Updates**: Document which files were updated and what changes were made
- **References**: Follow process in `ai-specs/specs/documentation-standards.mdc`
- **Notes**: All documentation in English

## Implementation Order

1. **Step 0**: Create feature branch `feature/feat-member-data-completeness-warnings-frontend`
2. **Step 1**: Create `frontend/src/utils/member-validation.ts`
3. **Step 2**: Create `frontend/src/utils/__tests__/member-validation.test.ts` — run tests, verify green
4. **Step 3**: Modify `FamilyMemberList.vue` — add warning icons + banner
5. **Step 4**: Update `FamilyMemberList.spec.ts` — run tests, verify green
6. **Step 5**: Modify `RegistrationMemberSelector.vue` — add warning icons + banner with link
7. **Step 6**: Update `RegistrationMemberSelector.test.ts` — run tests, verify green
8. **Step 7**: Update technical documentation

## Testing Checklist

### Unit Tests (Vitest)
- [ ] `member-validation.ts`: `getMemberDataWarnings` returns `null` for complete adult
- [ ] `member-validation.ts`: `getMemberDataWarnings` returns warnings for adult missing DNI
- [ ] `member-validation.ts`: `getMemberDataWarnings` returns warnings for adult missing email
- [ ] `member-validation.ts`: `getMemberDataWarnings` returns `null` for minor missing DNI/email
- [ ] `member-validation.ts`: `getMemberDataWarnings` returns warnings for invalid birth date (any age)
- [ ] `member-validation.ts`: `getWarningMessage` formats single missing field
- [ ] `member-validation.ts`: `getWarningMessage` formats multiple missing fields
- [ ] `FamilyMemberList`: warning icon rendered for adult with missing data
- [ ] `FamilyMemberList`: warning icon NOT rendered for complete adult
- [ ] `FamilyMemberList`: warning icon NOT rendered for minor missing DNI/email
- [ ] `FamilyMemberList`: warning Message banner shown when warnings exist
- [ ] `FamilyMemberList`: warning Message banner hidden when no warnings
- [ ] `RegistrationMemberSelector`: warning icon rendered on incomplete adult card
- [ ] `RegistrationMemberSelector`: warning icon NOT rendered on minor card
- [ ] `RegistrationMemberSelector`: warning banner shown when selected adult has incomplete data
- [ ] `RegistrationMemberSelector`: warning banner hidden when no selected members have warnings
- [ ] `RegistrationMemberSelector`: warning banner contains link to `/family-unit`

### Manual Verification
- [ ] Warning icons visible on mobile (responsive)
- [ ] Tooltip appears on hover/focus
- [ ] Clicking warning icon in registration selector does NOT toggle member selection
- [ ] Registration proceeds normally (no blocking) even with warnings
- [ ] Link in registration banner navigates to family unit page
- [ ] Browser back from family unit page returns to registration

## Error Handling Patterns

- No API calls involved — all checks are client-side computed values
- No error states to manage
- No loading states needed

## UI/UX Considerations

- **Warning icon**: `pi pi-exclamation-triangle` in `text-orange-500`, inline next to member name
- **Tooltip**: PrimeVue `v-tooltip.top` with the specific missing fields listed
- **Banner**: PrimeVue `<Message severity="warn" :closable="false">` — consistent with existing warning patterns in `FamilyUnitForm.vue`, `CampEditionStatusDialog.vue`
- **Link**: `RouterLink` with `font-semibold underline` — navigates to `/family-unit`
- **Responsive**: Icons use `text-xs` sizing, banner is full-width — both work on mobile
- **Accessibility**: `v-tooltip` provides keyboard-accessible tooltips via PrimeVue; `RouterLink` is natively accessible
- **Non-blocking**: Users can proceed with registration regardless of warnings

## Dependencies

### npm Packages
- No new packages needed

### PrimeVue Components Used
- `Message` (severity="warn") — already used elsewhere in the app
- `v-tooltip` directive — already registered globally

## Notes

- **Adults only** flagged for missing DNI/email. Minors are expected not to have these.
- **All text in Spanish** — consistent with existing UI language.
- **Birth date validation** is minimal: just checks year range (< 1900 or > current year). This catches placeholder dates like `0001-01-01` without being overly restrictive.
- **No backend changes** — all data needed (`documentNumber`, `email`, `dateOfBirth`) is already in `FamilyMemberResponse`.
- The `readOnly` prop in `FamilyMemberList` should be considered: the warning banner should not show when viewing another user's family (they can't edit it).
- Route path is `/family-unit` (NOT `/mi-familia` as mentioned in the enriched spec — corrected based on actual router config).

## Next Steps After Implementation

1. Create PR targeting `dev` branch
2. Visual review of warning icons and banner on staging
3. Consider future enhancements: admin panel could show a column/filter for members with incomplete data

## Implementation Verification

- [ ] Code Quality: TypeScript strict, no `any`, `<script setup lang="ts">`
- [ ] Functionality: Warning icons render correctly on incomplete adult members
- [ ] Functionality: Warning banner appears in registration with link to family management
- [ ] Functionality: No blocking — registration proceeds normally
- [ ] Testing: Vitest coverage for utility functions and both components
- [ ] Integration: Utility functions correctly imported and used in both components
- [ ] Documentation: Updates completed
