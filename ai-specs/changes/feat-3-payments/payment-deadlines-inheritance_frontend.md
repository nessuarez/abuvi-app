# Frontend Implementation Plan: Payment Deadline Inheritance

## Overview

Update the frontend to support the payment deadline inheritance pattern where:
1. **PaymentSettings** (global) stores 3 day-offset defaults: `firstInstallmentDaysBefore`, `secondInstallmentDaysBefore`, `extrasInstallmentDaysFromCampStart`
2. **CampEdition** materializes 3 concrete deadline dates: `firstPaymentDeadline`, `secondPaymentDeadline`, `extrasPaymentDeadline`
3. The admin edits defaults in payment settings; deadlines are auto-computed at edition creation and can be overridden per edition.

**Tech stack**: Vue 3 Composition API, PrimeVue, Tailwind CSS, TypeScript strict.

---

## Architecture Context

**Components/files to modify:**
| File | Change |
|---|---|
| `frontend/src/types/payment.ts` | Add `firstInstallmentDaysBefore`, `extrasInstallmentDaysFromCampStart` to `PaymentSettings` |
| `frontend/src/types/camp-edition.ts` | Add `extrasPaymentDeadline` to `CampEdition`, `UpdateCampEditionRequest`, `ActiveCampEditionResponse`, `CurrentCampEditionResponse` |
| `frontend/src/types/registration.ts` | Add `extrasPaymentDeadline` to `AvailableCampEditionResponse` |
| `frontend/src/components/admin/PaymentSettingsForm.vue` | Add 2 new `InputNumber` fields for `firstInstallmentDaysBefore` and `extrasInstallmentDaysFromCampStart` |
| `frontend/src/views/camps/CampEditionDetailPage.vue` | Add `extrasPaymentDeadline` to FormModel, form init, request construction, and template (Tab 5) |

**No new files, routes, or Pinia stores needed.**

---

## Implementation Steps

### Step 0: Create Feature Branch

- **Action**: Create and switch to `feature/payment-deadlines-inheritance-frontend` from `dev`.
- **Commands**:
  ```bash
  git checkout dev && git pull origin dev
  git checkout -b feature/payment-deadlines-inheritance-frontend
  ```

---

### Step 1: Update TypeScript Types — `PaymentSettings`

- **File**: `frontend/src/types/payment.ts`
- **Action**: Add 2 new fields to `PaymentSettings` interface.

**Current:**
```typescript
export interface PaymentSettings {
  iban: string
  bankName: string
  accountHolder: string
  secondInstallmentDaysBefore: number
  transferConceptPrefix: string
}
```

**Updated:**
```typescript
export interface PaymentSettings {
  iban: string
  bankName: string
  accountHolder: string
  firstInstallmentDaysBefore: number
  secondInstallmentDaysBefore: number
  extrasInstallmentDaysFromCampStart: number
  transferConceptPrefix: string
}
```

- **Implementation Notes**: No changes to `PaymentResponse`, `PaymentFilterParams`, or other types in this file.

---

### Step 2: Update TypeScript Types — Camp Edition

- **File**: `frontend/src/types/camp-edition.ts`
- **Action**: Add `extrasPaymentDeadline` to all relevant interfaces.

**2a. `CampEdition` interface** (line ~52):
```typescript
  // Payment deadlines:
  firstPaymentDeadline?: string | null
  secondPaymentDeadline?: string | null
  extrasPaymentDeadline?: string | null    // NEW
```

**2b. `UpdateCampEditionRequest`** (line ~207):
```typescript
  // Payment deadlines:
  firstPaymentDeadline?: string | null
  secondPaymentDeadline?: string | null
  extrasPaymentDeadline?: string | null    // NEW
```

**2c. `ActiveCampEditionResponse`** (line ~253):
```typescript
  // Payment deadlines:
  firstPaymentDeadline?: string | null
  secondPaymentDeadline?: string | null
  extrasPaymentDeadline?: string | null    // NEW
```

**2d. `CurrentCampEditionResponse`** (line ~302):
```typescript
  // Payment deadlines:
  firstPaymentDeadline?: string | null
  secondPaymentDeadline?: string | null
  extrasPaymentDeadline?: string | null    // NEW
```

---

### Step 3: Update TypeScript Types — Registration

- **File**: `frontend/src/types/registration.ts`
- **Action**: Add `extrasPaymentDeadline` to `AvailableCampEditionResponse`.

**At line ~190:**
```typescript
  // Payment deadlines:
  firstPaymentDeadline: string | null
  secondPaymentDeadline: string | null
  extrasPaymentDeadline: string | null     // NEW
```

---

### Step 4: Update PaymentSettingsForm Component

- **File**: `frontend/src/components/admin/PaymentSettingsForm.vue`
- **Action**: Add `InputNumber` fields for the 2 new settings; wire them into load/save.

**4a. Script — Add refs:**
```typescript
const firstInstallmentDaysBefore = ref(30)
const extrasInstallmentDaysFromCampStart = ref(0)
```

**4b. Script — Update `onMounted` load:**
```typescript
firstInstallmentDaysBefore.value = settings.firstInstallmentDaysBefore
extrasInstallmentDaysFromCampStart.value = settings.extrasInstallmentDaysFromCampStart
```

**4c. Script — Update `handleSave` request:**
```typescript
const result = await updatePaymentSettings({
  iban: iban.value.replace(/\s/g, ''),
  bankName: bankName.value.trim(),
  accountHolder: accountHolder.value.trim(),
  firstInstallmentDaysBefore: firstInstallmentDaysBefore.value,
  secondInstallmentDaysBefore: secondInstallmentDaysBefore.value,
  extrasInstallmentDaysFromCampStart: extrasInstallmentDaysFromCampStart.value,
  transferConceptPrefix: transferConceptPrefix.value.trim().toUpperCase()
})
```

**4d. Template — Add fields after the existing "Días de antelación para el 2º plazo":**

Before the existing `secondInstallmentDaysBefore` field, add:
```html
<div>
  <label class="mb-1 block text-sm font-medium text-gray-700">
    Días de antelación para el 1er plazo
  </label>
  <InputNumber v-model="firstInstallmentDaysBefore" :min="0" :max="365" class="w-full" />
  <p class="mt-1 text-xs text-gray-500">
    Fecha límite del 1er pago = fecha inicio del campamento menos estos días.
  </p>
</div>
```

After the existing `secondInstallmentDaysBefore` field, add:
```html
<div>
  <label class="mb-1 block text-sm font-medium text-gray-700">
    Días desde el inicio para el plazo de extras
  </label>
  <InputNumber v-model="extrasInstallmentDaysFromCampStart" :min="-90" :max="90" class="w-full" />
  <p class="mt-1 text-xs text-gray-500">
    Positivo = después del inicio del campamento. Negativo = antes. 0 = mismo día del inicio.
  </p>
</div>
```

- **Implementation Notes**:
  - Reorder fields logically: 1er plazo, 2º plazo, extras.
  - Update the existing label from "Días de antelación para el 2º plazo" — keep as is since it is still correct.
  - The help text for the existing `secondInstallmentDaysBefore` already says "El 2º plazo vencerá estos días antes del inicio del campamento" — keep as is.

---

### Step 5: Update CampEditionDetailPage — `extrasPaymentDeadline`

- **File**: `frontend/src/views/camps/CampEditionDetailPage.vue`
- **Action**: Add `extrasPaymentDeadline` to the form model, initialization, save request, and template.

**5a. FormModel interface** (line ~85):
```typescript
  firstPaymentDeadline: Date | null
  secondPaymentDeadline: Date | null
  extrasPaymentDeadline: Date | null    // NEW
```

**5b. Form reactive init** (line ~100):
```typescript
  firstPaymentDeadline: null, secondPaymentDeadline: null, extrasPaymentDeadline: null
```

**5c. `startEditing` function** (after line ~145):
```typescript
  form.extrasPaymentDeadline = ed.extrasPaymentDeadline ? parseDateLocal(ed.extrasPaymentDeadline) : null
```

**5d. `handleSave` request construction** (after line ~260):
```typescript
    firstPaymentDeadline: form.firstPaymentDeadline ? formatDateToIso(form.firstPaymentDeadline) : null,
    secondPaymentDeadline: form.secondPaymentDeadline ? formatDateToIso(form.secondPaymentDeadline) : null,
    extrasPaymentDeadline: form.extrasPaymentDeadline ? formatDateToIso(form.extrasPaymentDeadline) : null
```

**5e. Template — Tab 5 "Fechas de pago":**

Add to the read-only view (after secondPaymentDeadline display):
```html
<div class="flex justify-between">
  <span class="text-gray-600">Fecha límite pago extras:</span>
  <span>{{ edition.extrasPaymentDeadline ? formatDate(edition.extrasPaymentDeadline) : 'Automática' }}</span>
</div>
```

Add to the edit form (make it a 3-column grid on desktop):
```html
<div class="grid grid-cols-1 gap-4 sm:grid-cols-3">
  <!-- existing firstPaymentDeadline field -->
  <!-- existing secondPaymentDeadline field -->
  <div class="flex flex-col gap-1">
    <label class="text-sm font-medium text-gray-700">Fecha límite pago extras</label>
    <DateInput v-model="form.extrasPaymentDeadline" />
    <p class="text-xs text-gray-500">Si se deja vacío, se calcula desde los ajustes globales.</p>
  </div>
</div>
```

- **Implementation Notes**:
  - Change the grid from `sm:grid-cols-2` to `sm:grid-cols-3` to accommodate the third field.
  - Update help text for first two fields: replace the hardcoded "117 días" / "75 días" with "se calcula desde los ajustes globales" since defaults now come from settings, not hardcoded constants.

---

### Step 6: Update Technical Documentation

- **Action**: Review and update documentation after implementation.
- **Implementation Steps**:
  1. Update `ai-specs/specs/api-spec.yml` — update `PaymentSettings` schema with new fields; update `CampEdition` response/request schemas with `extrasPaymentDeadline`.
  2. Verify that the `payment-deadlines-inheritance.md` spec acceptance criteria match the implementation.

---

## Implementation Order

1. **Step 0**: Create feature branch
2. **Step 1**: Update `payment.ts` types
3. **Step 2**: Update `camp-edition.ts` types
4. **Step 3**: Update `registration.ts` types
5. **Step 4**: Update `PaymentSettingsForm.vue`
6. **Step 5**: Update `CampEditionDetailPage.vue`
7. **Step 6**: Update documentation

---

## Testing Checklist

- [ ] `PaymentSettingsForm` loads and displays all 3 day-offset fields correctly
- [ ] `PaymentSettingsForm` saves all 3 fields and shows success toast
- [ ] `PaymentSettingsForm` validates min/max ranges (0-365 for P1, 1-90 for P2, -90 to 90 for extras)
- [ ] `CampEditionDetailPage` Tab 5 displays all 3 deadlines in read mode
- [ ] `CampEditionDetailPage` Tab 5 allows editing all 3 deadlines
- [ ] Saving a CampEdition with `null` deadlines sends `null` (triggers server re-derive from settings)
- [ ] Saving a CampEdition with explicit dates sends those dates
- [ ] `extrasPaymentDeadline` shows "Automática" when `null`
- [ ] TypeScript compiles with no errors (`npm run type-check`)
- [ ] No regressions in existing functionality

---

## Error Handling Patterns

- Existing patterns are sufficient — `usePayments` and `useCampEditions` composables already handle API errors via `error` ref and toast notifications.
- No new error states needed.

---

## UI/UX Considerations

- **PaymentSettingsForm**: Three `InputNumber` fields in a vertical stack (same pattern as existing). Group them visually under a subtle section label "Plazos de pago por defecto" for clarity.
- **CampEditionDetailPage Tab 5**: 3-column responsive grid (`sm:grid-cols-3`) for the 3 deadline DateInputs in edit mode. Falls back to single column on mobile.
- **Help text update**: Replace hardcoded day references ("117 días", "75 días") with "se calcula desde los ajustes globales" to reflect the new inheritance pattern.

---

## Dependencies

- No new npm packages required.
- PrimeVue components already in use: `InputNumber`, `DateInput`, `Button`, `Message`, `InputText`.
- No Pinia store changes needed.

---

## Notes

- **Backward compatibility**: If the backend hasn't been updated yet (old `PaymentSettings` without new fields), the form will receive `undefined` for `firstInstallmentDaysBefore` and `extrasInstallmentDaysFromCampStart` — the refs default to `30` and `0` respectively, which matches the backend defaults. No crash.
- **Coordinate with backend**: This plan assumes the backend from `payment-deadlines-inheritance_backend.md` is deployed first or simultaneously. The frontend can be deployed first safely (new fields are optional).
- **No breaking changes**: All new fields are additive (optional in types, nullable in requests).
- **Language**: UI labels are in Spanish (matching existing patterns). Code/types are in English.

---

## Next Steps After Implementation

1. Verify end-to-end flow: create a new CampEdition → verify 3 deadlines are auto-populated from settings → edit one → verify it persists.
2. Registration wizard: verify `AvailableCampEditionResponse` exposes `extrasPaymentDeadline` for informational display (if needed in the future).
