# Frontend Implementation Plan: fix-registration-detail-button

## Overview

The "Ver detalles" button on the "Mis Inscripciones" page does not work correctly. The root cause is a **frontend/backend type mismatch**: the list endpoint (`GET /api/registrations`) returns `RegistrationListResponse` (a lightweight DTO with flat `totalAmount`), but the frontend deserializes and types it as `RegistrationResponse` (the full detail DTO with nested `pricing` object). This causes missing data on cards and potential runtime issues.

Additionally, the frontend `RegistrationFamilyUnitSummary` type includes `representativeUserId` which the backend currently does not send (backend fix is tracked separately in `fix-registration-detail-button_backend.md`).

This plan creates a proper `RegistrationListItem` type matching the backend list DTO, updates the composable and components to use it, and ensures the card displays data correctly.

## Architecture Context

- **Components involved**:
  - `frontend/src/components/registrations/RegistrationCard.vue` — Card with "Ver detalles" button
  - `frontend/src/views/registrations/RegistrationsPage.vue` — List page using the cards
  - `frontend/src/views/registrations/RegistrationDetailPage.vue` — Detail page (minor type update)
- **Composable**: `frontend/src/composables/useRegistrations.ts` — API calls
- **Types**: `frontend/src/types/registration.ts` — TypeScript interfaces
- **Tests**: `frontend/src/components/registrations/__tests__/RegistrationCard.test.ts`
- **Routing**: No changes needed — routes are correctly defined
- **State management**: Local composable state (no Pinia store involved)

## Implementation Steps

### Step 0: Create Feature Branch

- **Action**: Create and switch to a new feature branch
- **Branch Naming**: `fix/fix-registration-detail-button-frontend`
- **Implementation Steps**:
  1. Ensure you're on the latest `dev` branch
  2. Pull latest changes: `git pull origin dev`
  3. Create new branch: `git checkout -b fix/fix-registration-detail-button-frontend`
  4. Verify branch creation: `git branch`

### Step 1: Create `RegistrationListItem` type

- **File**: `frontend/src/types/registration.ts`
- **Action**: Add a new interface matching the backend `RegistrationListResponse` DTO
- **Implementation Steps**:
  1. Add the new interface after the existing `RegistrationResponse` interface (after line 105):
     ```typescript
     // List endpoint response (lightweight, used by GET /api/registrations)
     export interface RegistrationListItem {
       id: string
       familyUnit: RegistrationFamilyUnitSummary
       campEdition: RegistrationCampEditionSummary
       status: RegistrationStatus
       totalAmount: number
       amountPaid: number
       amountRemaining: number
       createdAt: string
     }
     ```
  2. Update `RegistrationCampEditionSummary` to include `duration` (backend sends it but frontend type is missing it):
     ```typescript
     export interface RegistrationCampEditionSummary {
       id: string
       campName: string
       year: number
       startDate: string
       endDate: string
       location: string | null
       duration: number  // <-- ADD: (endDate - startDate).Days from backend
     }
     ```
- **Implementation Notes**:
  - `RegistrationFamilyUnitSummary` already has `representativeUserId` in the frontend type. The backend fix (separate ticket) will add this field to the API response. No frontend type change needed for this field.
  - The `RegistrationListItem` intentionally does NOT have `pricing`, `payments`, `notes`, etc. — these are only available on the detail endpoint.

### Step 2: Update `useRegistrations` composable

- **File**: `frontend/src/composables/useRegistrations.ts`
- **Action**: Change the `registrations` ref and `fetchMyRegistrations` to use `RegistrationListItem[]`
- **Implementation Steps**:
  1. Add `RegistrationListItem` to the import from `@/types/registration` (line 6):
     ```typescript
     import type {
       RegistrationResponse,
       RegistrationListItem,  // <-- ADD
       CreateRegistrationRequest,
       ...
     } from '@/types/registration'
     ```
  2. Change the `registrations` ref type (line 14):
     ```typescript
     // Before:
     const registrations = ref<RegistrationResponse[]>([])
     // After:
     const registrations = ref<RegistrationListItem[]>([])
     ```
  3. Update `fetchMyRegistrations` return type annotation (line 19):
     ```typescript
     // Before:
     const response = await api.get<ApiResponse<RegistrationResponse[]>>('/registrations')
     // After:
     const response = await api.get<ApiResponse<RegistrationListItem[]>>('/registrations')
     ```
  4. The `registration` ref (singular, line 15) stays as `RegistrationResponse | null` — it's used for the detail endpoint which returns the full DTO.
- **Implementation Notes**:
  - The `createRegistration` method pushes to `registrations.value` (line 62). After `createRegistration`, the API returns `RegistrationResponse` (full DTO). Since `RegistrationListItem` is a subset, we need to map it:
    ```typescript
    // In createRegistration, after success:
    const listItem: RegistrationListItem = {
      id: response.data.data.id,
      familyUnit: response.data.data.familyUnit,
      campEdition: response.data.data.campEdition,
      status: response.data.data.status,
      totalAmount: response.data.data.pricing.totalAmount,
      amountPaid: response.data.data.amountPaid,
      amountRemaining: response.data.data.amountRemaining,
      createdAt: response.data.data.createdAt
    }
    registrations.value.push(listItem)
    ```
  - Similarly, `updateMembers` and `setExtras` update the `registrations` array (lines 89-91, 118-120). These also need the same mapping when updating a list item from a full `RegistrationResponse`.
  - Consider extracting a helper: `function toListItem(r: RegistrationResponse): RegistrationListItem`

### Step 3: Update `RegistrationCard` component

- **File**: `frontend/src/components/registrations/RegistrationCard.vue`
- **Action**: Change props type to `RegistrationListItem` and update total price display
- **Implementation Steps**:
  1. Update the import (line 2):
     ```typescript
     // Before:
     import type { RegistrationResponse } from '@/types/registration'
     // After:
     import type { RegistrationListItem } from '@/types/registration'
     ```
  2. Update the props definition (lines 6-8):
     ```typescript
     // Before:
     defineProps<{ registration: RegistrationResponse }>()
     // After:
     defineProps<{ registration: RegistrationListItem }>()
     ```
  3. Update the total price display in the template (lines 46-48):
     ```html
     <!-- Before: -->
     <template v-if="registration.pricing">
       Total: {{ formatCurrency(registration.pricing.totalAmount) }}
     </template>
     <!-- After: -->
     Total: {{ formatCurrency(registration.totalAmount) }}
     ```
     Remove the `v-if` guard since `totalAmount` is always present in the list item.

### Step 4: Update `RegistrationsPage` view

- **File**: `frontend/src/views/registrations/RegistrationsPage.vue`
- **Action**: Change type references from `RegistrationResponse` to `RegistrationListItem`
- **Implementation Steps**:
  1. Update the import (line 10):
     ```typescript
     // Before:
     import type { RegistrationResponse } from '@/types/registration'
     // After:
     import type { RegistrationListItem } from '@/types/registration'
     ```
  2. Update the `sortedRegistrations` computed type (line 15):
     ```typescript
     // Before:
     const sortedRegistrations = computed<RegistrationResponse[]>(() => {
     // After:
     const sortedRegistrations = computed<RegistrationListItem[]>(() => {
     ```
- **Implementation Notes**: No template changes needed — the `RegistrationCard` component handles the display.

### Step 5: Update `RegistrationCard` tests

- **File**: `frontend/src/components/registrations/__tests__/RegistrationCard.test.ts`
- **Action**: Update mock data and type references to use `RegistrationListItem`
- **Implementation Steps**:
  1. Update the import (line 5):
     ```typescript
     // Before:
     import type { RegistrationResponse } from '@/types/registration'
     // After:
     import type { RegistrationListItem } from '@/types/registration'
     ```
  2. Replace `mockRegistration` with the list item shape (lines 7-34):
     ```typescript
     const mockRegistration: RegistrationListItem = {
       id: 'reg-1',
       familyUnit: { id: 'fu-1', name: 'Familia Garcia', representativeUserId: 'user-1' },
       campEdition: {
         id: 'edition-1',
         campName: 'Campamento ABUVI',
         year: 2026,
         startDate: '2026-07-01',
         endDate: '2026-07-15',
         location: 'Montana Norte',
         duration: 14
       },
       status: 'Pending',
       totalAmount: 450,
       amountPaid: 0,
       amountRemaining: 450,
       createdAt: '2026-02-01T00:00:00Z'
     }
     ```
  3. Update `mountComponent` type (line 36):
     ```typescript
     const mountComponent = (props: { registration: RegistrationListItem }) =>
     ```
  4. Add a new test for total amount display:
     ```typescript
     it('should display the total amount', () => {
       const wrapper = mountComponent({ registration: mockRegistration })
       expect(wrapper.text()).toContain('450')
     })
     ```
  5. Add a test for location display:
     ```typescript
     it('should display the camp location when available', () => {
       const wrapper = mountComponent({ registration: mockRegistration })
       expect(wrapper.text()).toContain('Montana Norte')
     })

     it('should not display location when null', () => {
       const reg = { ...mockRegistration, campEdition: { ...mockRegistration.campEdition, location: null } }
       const wrapper = mountComponent({ registration: reg })
       expect(wrapper.find('.pi-map-marker').exists()).toBe(false)
     })
     ```
  6. Keep the existing `view` event test — it should pass as-is since `id` is present in both types

### Step 6: Update `useRegistrations` composable tests

- **File**: `frontend/src/composables/__tests__/useRegistrations.test.ts`
- **Action**: Update mock API responses to match `RegistrationListItem` shape for list tests
- **Implementation Steps**:
  1. Check the existing test file for `fetchMyRegistrations` tests
  2. Update the mock response data to use the `RegistrationListItem` shape (flat `totalAmount` instead of nested `pricing`)
  3. Add assertion that `registrations.value` items have `totalAmount` as a number (not a `pricing` object)
  4. Ensure `getRegistrationById` tests still use the full `RegistrationResponse` shape

### Step 7: Update Technical Documentation

- **Action**: Review and update technical documentation
- **Implementation Steps**:
  1. **Review Changes**: Type split (`RegistrationResponse` vs `RegistrationListItem`), component prop changes
  2. **Identify Documentation Files**:
     - `RegistrationCampEditionSummary` type changed → No doc file needed (types are self-documenting)
     - No new routes, no new dependencies
  3. **Update Documentation**: If `ai-specs/specs/api-spec.yml` documents the registration list endpoint response, update it to reflect `RegistrationListItem` fields
  4. **Verify**: Run `npx vitest --run` to confirm all tests pass

## Implementation Order

1. **Step 0**: Create feature branch `fix/fix-registration-detail-button-frontend`
2. **Step 1**: Create `RegistrationListItem` type and update `RegistrationCampEditionSummary`
3. **Step 2**: Update `useRegistrations` composable to use `RegistrationListItem[]`
4. **Step 3**: Update `RegistrationCard` props and template
5. **Step 4**: Update `RegistrationsPage` type references
6. **Step 5**: Update `RegistrationCard` tests
7. **Step 6**: Update `useRegistrations` composable tests
8. **Step 7**: Update technical documentation

## Testing Checklist

### Vitest Unit Tests
- [ ] `RegistrationCard` renders camp name, year, dates from `RegistrationListItem`
- [ ] `RegistrationCard` displays `totalAmount` directly (no `pricing` nesting)
- [ ] `RegistrationCard` displays location when present, hides when null
- [ ] `RegistrationCard` emits `view` event with correct `id` on button click
- [ ] `RegistrationCard` displays correct status badge
- [ ] `useRegistrations.fetchMyRegistrations` populates `registrations` with `RegistrationListItem[]`
- [ ] `useRegistrations.getRegistrationById` populates `registration` with full `RegistrationResponse`
- [ ] `useRegistrations.createRegistration` maps response to `RegistrationListItem` before pushing to list

### Manual Verification
- [ ] Navigate to `/registrations` — cards show camp name, dates, location, total price, status
- [ ] Click "Ver detalles" — navigates to `/registrations/{id}`
- [ ] Detail page loads with full pricing breakdown, payments, accommodation preferences
- [ ] "Cancelar inscripcion" button visible for the representative (requires backend fix deployed)
- [ ] No console errors in DevTools

## Error Handling Patterns

- **List page**: `useRegistrations` already handles errors with `error` ref and displays via `<Message severity="error">` — no changes needed
- **Detail page**: `getRegistrationById` already handles 403/404 errors and displays them — no changes needed
- **Loading states**: Both pages already show `<ProgressSpinner>` while loading — no changes needed

## UI/UX Considerations

- **Total price display**: Removing the `v-if="registration.pricing"` guard means the total will always show on cards. This is correct — the list DTO always includes `totalAmount`.
- **Location display**: Already guarded by `v-if="registration.campEdition.location"` in `RegistrationCard` — no change needed.
- **No visual changes**: The card layout, button styling, and page structure remain identical. Only the data source for the total price changes.

## Dependencies

- **No new npm packages**
- **PrimeVue components used** (unchanged): `Button`, `ProgressSpinner`, `Message`
- **Backend dependency**: The `representativeUserId` field in `RegistrationFamilyUnitSummary` requires the backend fix to be deployed. Until then, `isRepresentative` on the detail page will remain `false`. The frontend type already expects this field — no conditional handling needed.

## Notes

- **Backward compatibility**: Once the backend is updated to include `representativeUserId` and `location` in the shared DTOs, the frontend will automatically pick them up since the types already expect these fields.
- **Type safety**: All changes maintain strict TypeScript typing. No `any` types introduced.
- **Language**: All code, test names, and comments must be in English.
- **Helper function**: Consider adding a `toListItem(r: RegistrationResponse): RegistrationListItem` helper in the composable to avoid duplicating the mapping logic in `createRegistration`, `updateMembers`, and `setExtras`.

## Next Steps After Implementation

1. Coordinate with backend to deploy the `representativeUserId` and `location` DTO changes
2. After backend deploy, verify end-to-end: list cards show location, detail page `isRepresentative` works
3. Consider adding a Cypress E2E test for the full registration list > detail flow

## Implementation Verification

- [ ] **Code Quality**: TypeScript strict mode passes, no `any`, all components use `<script setup lang="ts">`
- [ ] **Functionality**: Cards display correct data from list DTO, "Ver detalles" navigates to detail page
- [ ] **Testing**: All Vitest tests pass (`npx vitest --run`)
- [ ] **Integration**: `fetchMyRegistrations` correctly deserializes `RegistrationListItem[]` from API
- [ ] **Documentation**: Updated where applicable
