# Frontend Implementation Plan: feat-registration-has-pet — Añadir campo "viene con mascota"

## Overview

Add a `hasPet` boolean field to the registration flow in the frontend. This involves updating TypeScript types, adding a checkbox to the registration wizard (Step 2 - Extras), displaying the value in the registration detail page, and including it in the confirmation summary step.

**Architecture**: Vue 3 Composition API, PrimeVue components (Checkbox), Tailwind CSS utilities. No new composables, stores, or routes needed.

## Architecture Context

- **Components affected**:
  - `RegisterForCampPage.vue` — Registration wizard (add checkbox + include in payload)
  - `RegistrationDetailPage.vue` — Detail view (display pet status)
- **Types affected**:
  - `frontend/src/types/registration.ts` — Add `hasPet` to request and response interfaces
- **Composables**: No changes needed — `useRegistrations` already handles `createRegistration` generically
- **Routing**: No changes needed
- **State management**: Local `ref<boolean>` in wizard, no Pinia store changes

## Implementation Steps

### Step 0: Create Feature Branch

- **Action**: Create and switch to a new feature branch
- **Branch Naming**: `feature/feat-registration-has-pet-frontend`
- **Implementation Steps**:
  1. Ensure you're on the latest `dev` branch
  2. Pull latest changes: `git pull origin dev`
  3. Create new branch: `git checkout -b feature/feat-registration-has-pet-frontend`
  4. Verify branch creation: `git branch`
- **Notes**: PRs target `dev`, not `main`.

### Step 1: Update TypeScript Types

- **File**: `frontend/src/types/registration.ts`
- **Action**: Add `hasPet` field to `CreateRegistrationRequest` and `RegistrationResponse`
- **Implementation Steps**:
  1. In `CreateRegistrationRequest` (line 195-202), add `hasPet`:
     ```typescript
     export interface CreateRegistrationRequest {
       campEditionId: string
       familyUnitId: string
       members: MemberAttendanceRequest[]
       notes?: string | null
       specialNeeds: string | null
       campatesPreference: string | null
       hasPet: boolean
     }
     ```
  2. In `RegistrationResponse` (line 104-118), add `hasPet`:
     ```typescript
     export interface RegistrationResponse {
       id: string
       familyUnit: RegistrationFamilyUnitSummary
       campEdition: RegistrationCampEditionSummary
       status: RegistrationStatus
       notes: string | null
       pricing: PricingBreakdown
       payments: PaymentSummary[]
       amountPaid: number
       amountRemaining: number
       createdAt: string
       updatedAt: string
       specialNeeds: string | null
       campatesPreference: string | null
       hasPet: boolean
     }
     ```
- **Notes**: `hasPet` is a non-nullable `boolean` (always present, defaults to `false` on backend).

### Step 2: Update Registration Wizard — Add Checkbox

- **File**: `frontend/src/views/registrations/RegisterForCampPage.vue`
- **Action**: Add a `hasPet` ref and a PrimeVue Checkbox in Step 2 (Extras)
- **Implementation Steps**:
  1. **Add ref** — After line 56 (`campatesPreference` ref), add:
     ```typescript
     const hasPet = ref<boolean>(false)
     ```
  2. **Add checkbox in template** — In Step 2, after the "Preferencia de acampantes" `<div>` (after line 323, before the closing `</div>` of the step content), add:
     ```vue
     <!-- Pet -->
     <div class="mb-5 flex items-center gap-2">
       <Checkbox v-model="hasPet" :binary="true" input-id="has-pet" data-testid="has-pet" />
       <label for="has-pet" class="text-sm font-medium text-gray-700">
         ¿Viene con mascota?
       </label>
     </div>
     ```
  3. **Include in payload** — In `handleConfirm()` (line 115-129), add `hasPet` to the `createRegistration` call:
     ```typescript
     const created = await createRegistration({
       campEditionId: editionId.value,
       familyUnitId: familyUnit.value.id,
       members: selectedMembers.value.map((s) => ({
         memberId: s.memberId,
         attendancePeriod: s.attendancePeriod,
         visitStartDate: s.visitStartDate ?? null,
         visitEndDate: s.visitEndDate ?? null,
         guardianName: s.guardianName || null,
         guardianDocumentNumber: s.guardianDocumentNumber || null
       })),
       notes: notes.value || null,
       specialNeeds: specialNeeds.value || null,
       campatesPreference: campatesPreference.value || null,
       hasPet: hasPet.value
     })
     ```
- **Dependencies**: `Checkbox` from PrimeVue is already imported (line 11).
- **Notes**: The checkbox is placed after "Preferencia de acampantes" and before the step navigation buttons, which keeps it visually grouped with the other "additional info" fields.

### Step 3: Update Confirmation Summary

- **File**: `frontend/src/views/registrations/RegisterForCampPage.vue`
- **Action**: Show "Viene con mascota" in the confirmation step's additional info block
- **Implementation Steps**:
  1. Update the `v-if` condition on the additional info block (line 444) to include `hasPet`:
     ```vue
     <div v-if="specialNeeds || campatesPreference || hasPet" class="mb-4 rounded-lg border border-gray-200 p-4">
     ```
  2. Add a new entry inside the `<dl>` for pet, after the campatesPreference `<div>` (after line 454):
     ```vue
     <div v-if="hasPet" class="flex gap-2">
       <dt class="text-gray-500">Mascota:</dt>
       <dd class="text-gray-800">Sí, asiste con mascota</dd>
     </div>
     ```

### Step 4: Update Registration Detail Page — Display

- **File**: `frontend/src/views/registrations/RegistrationDetailPage.vue`
- **Action**: Show pet status in the "Información adicional" section
- **Implementation Steps**:
  1. Update the `v-if` condition on the preference fields block (line 403) to include `hasPet`:
     ```vue
     <div
       v-if="registration.specialNeeds || registration.campatesPreference || registration.hasPet"
       class="mb-6 rounded-lg border border-gray-200 bg-gray-50 p-4"
     >
     ```
  2. Add a new entry inside the `<dl>` after the campatesPreference block (after line 415):
     ```vue
     <div v-if="registration.hasPet" class="flex flex-col gap-0.5">
       <dt class="font-medium text-gray-600">Mascota</dt>
       <dd class="text-gray-800">Sí, asiste con mascota</dd>
     </div>
     ```

### Step 5: Update Technical Documentation

- **Action**: Review and update technical documentation according to changes made
- **Implementation Steps**:
  1. **Review Changes**: All changes are limited to types and two Vue pages
  2. **Identify Documentation Files**:
     - No new routes, components, or dependencies — minimal doc impact
     - If `ai-specs/specs/data-model.md` was already updated by backend, verify consistency
  3. **Verify**: Ensure frontend types match the backend DTOs
- **Notes**: This step is MANDATORY before considering the implementation complete.

## Implementation Order

1. **Step 0**: Create feature branch `feature/feat-registration-has-pet-frontend`
2. **Step 1**: Update TypeScript types (`registration.ts`)
3. **Step 2**: Add checkbox to registration wizard (`RegisterForCampPage.vue`)
4. **Step 3**: Update confirmation summary in wizard
5. **Step 4**: Update detail page display (`RegistrationDetailPage.vue`)
6. **Step 5**: Update technical documentation

## Testing Checklist

- [ ] **TypeScript compiles** — `npm run type-check` passes with no errors
- [ ] **Checkbox renders** — In Step 2, the "¿Viene con mascota?" checkbox appears after "Preferencia de acampantes"
- [ ] **Default unchecked** — Checkbox starts unchecked (`false`)
- [ ] **Payload includes hasPet** — When creating a registration with checkbox checked, the API call includes `hasPet: true`
- [ ] **Payload default** — When creating without checking, the API call includes `hasPet: false`
- [ ] **Confirmation step** — When checked, "Mascota: Sí, asiste con mascota" appears in the review step
- [ ] **Confirmation step hidden** — When unchecked and no special needs/campates preference, the additional info block is hidden
- [ ] **Detail page shows pet** — When `hasPet` is `true` in the response, "Mascota: Sí, asiste con mascota" appears
- [ ] **Detail page hides pet** — When `hasPet` is `false`, the pet line doesn't show (but the block may still show if other fields are present)
- [ ] **Lint passes** — `npm run lint` passes

## Error Handling Patterns

No new error handling needed. The checkbox is a simple boolean that:
- Cannot fail validation
- Has a safe default (`false`)
- Is included in the existing `createRegistration` flow which already handles errors via toast

## UI/UX Considerations

- **PrimeVue components**: Reuses existing `Checkbox` component (already imported in `RegisterForCampPage.vue`)
- **Layout**: Checkbox with inline label using `flex items-center gap-2`, consistent with the `acceptTerms` checkbox pattern used in the confirmation step (line 480-486)
- **Placement**: After "Preferencia de acampantes" in Step 2, keeping all "additional info" fields together
- **Accessibility**: `input-id="has-pet"` + matching `for="has-pet"` on label ensures screen reader compatibility
- **`data-testid`**: `has-pet` for future E2E test targeting
- **Responsive**: No responsive concerns — checkbox + label flows naturally at any width

## Dependencies

- No new npm packages
- PrimeVue `Checkbox` — already imported and used in the wizard
- No third-party packages

## Notes

- **Backend dependency**: This frontend plan assumes the backend `feat-registration-has-pet` changes are already deployed (or will be deployed alongside). The `hasPet` field must exist in the API response and be accepted in the create request.
- **No admin edit UI**: The admin edit flow (`AdminEditRegistrationRequest`) accepts `hasPet` on the backend, but there is no dedicated admin edit form in the frontend currently. The field will be editable via admin-edit API calls if needed in the future.
- **Language**: UI text is in Spanish (per project convention). TypeScript/code is in English.
- **Strict typing**: `hasPet: boolean` (non-nullable) — never `any` or `unknown`.

## Next Steps After Implementation

- Coordinate with backend PR to merge both simultaneously (or backend first)
- Verify end-to-end flow with real API after both PRs are merged

## Implementation Verification

- [ ] **Code Quality**: TypeScript strict mode, no `any`, `<script setup lang="ts">` used
- [ ] **Functionality**: Checkbox renders, value persists through wizard, API call includes field, detail page displays it
- [ ] **Testing**: TypeScript type-check passes, lint passes
- [ ] **Integration**: Frontend types match backend DTOs
- [ ] **Documentation**: Updates completed
