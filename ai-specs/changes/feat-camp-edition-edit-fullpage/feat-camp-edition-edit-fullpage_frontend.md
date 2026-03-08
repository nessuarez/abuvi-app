# Frontend Implementation Plan: feat-camp-edition-edit-fullpage — Replace CampEdition Edit Modal with Full Edit Page

## Overview

Replace the cramped `CampEditionUpdateDialog` modal with a dedicated full-page edit view at `/camps/editions/:id/edit`. This fixes critical bugs (date initialization, notes reset, validation blocking Open edition edits) and provides a better UX for the 25+ fields organized across 7 sections.

Architecture: Vue 3 Composition API, `<script setup lang="ts">`, PrimeVue components, Tailwind CSS, composable-based API communication.

## Architecture Context

### Components/Composables Involved

| File | Role |
|------|------|
| `frontend/src/views/camps/CampEditionEditPage.vue` | **NEW** — Full-page edit form |
| `frontend/src/views/camps/CampEditionsPage.vue` | MODIFY — Remove modal, navigate to edit page |
| `frontend/src/views/camps/CampEditionDetailPage.vue` | MODIFY — Add "Edit" button |
| `frontend/src/components/camps/CampEditionUpdateDialog.vue` | **DELETE** — Replaced by the new page |
| `frontend/src/router/index.ts` | MODIFY — Add new route |
| `frontend/src/composables/useCampEditions.ts` | READ-ONLY — Uses `getEditionById()` and `updateEdition()` |
| `frontend/src/utils/date.ts` | READ-ONLY — Uses `parseDateLocal()` and `formatDateLocal()` |
| `frontend/src/components/shared/DateInput.vue` | READ-ONLY — Date input component |
| `frontend/src/types/camp-edition.ts` | READ-ONLY — `CampEdition`, `UpdateCampEditionRequest` types |

### Routing

- New route: `/camps/editions/:id/edit` → `CampEditionEditPage.vue`
- Meta: `{ title: "ABUVI | Editar Edición", requiresAuth: true, requiresBoard: true }`

### State Management

- **Local state only** (no Pinia store changes needed)
- `reactive()` for the form model
- `ref()` for the loaded edition, errors, loading state
- `useCampEditions()` composable for API calls

## Implementation Steps

### Step 0: Create Feature Branch

- **Action**: Create and switch to a new feature branch
- **Branch Naming**: `feature/feat-camp-edition-edit-fullpage-frontend`
- **Implementation Steps**:
  1. Ensure on latest `main`: `git checkout main && git pull origin main`
  2. Create branch: `git checkout -b feature/feat-camp-edition-edit-fullpage-frontend`
  3. Verify: `git branch`

---

### Step 1: Add Route Configuration

- **File**: `frontend/src/router/index.ts`
- **Action**: Add the new edit page route
- **Implementation Steps**:
  1. Add the new route entry **before** the existing `camp-edition-detail` route (since `:id/edit` is more specific than `:id`):
     ```typescript
     {
       path: "/camps/editions/:id/edit",
       name: "camp-edition-edit",
       component: () => import("@/views/camps/CampEditionEditPage.vue"),
       meta: { title: "ABUVI | Editar Edición", requiresAuth: true, requiresBoard: true }
     }
     ```
  2. Ensure it's placed before the `camp-edition-detail` route so Vue Router matches it first
- **Notes**: The `requiresBoard: true` meta ensures only Board and Admin users can access this page, matching the existing pattern used by `camp-editions` list page.

---

### Step 2: Create the Edit Page

- **File**: `frontend/src/views/camps/CampEditionEditPage.vue`
- **Action**: Create new full-page edit form view
- **Dependencies**:
  - `vue`: `ref`, `reactive`, `computed`, `onMounted`
  - `vue-router`: `useRoute`, `useRouter`
  - `primevue/usetoast`: `useToast`
  - `@/composables/useCampEditions`: `getEditionById`, `updateEdition`, `loading`, `error`
  - `@/utils/date`: `parseDateLocal`, `formatDateLocal`
  - `@/components/ui/Container.vue`
  - `@/components/shared/DateInput.vue`
  - PrimeVue: `Button`, `InputNumber`, `Textarea`, `ToggleSwitch`, `Message`, `ProgressSpinner`, `Toast`
  - `@/types/camp-edition`: `CampEdition`, `UpdateCampEditionRequest`

#### 2.1 Script Setup Structure

```typescript
<script setup lang="ts">
import { ref, reactive, computed, onMounted } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { useToast } from 'primevue/usetoast'
// ... component imports
import { useCampEditions } from '@/composables/useCampEditions'
import { parseDateLocal, formatDateLocal } from '@/utils/date'
import type { CampEdition, UpdateCampEditionRequest } from '@/types/camp-edition'

const route = useRoute()
const router = useRouter()
const toast = useToast()
const { getEditionById, updateEdition, loading, error } = useCampEditions()

const edition = ref<CampEdition | null>(null)
const saving = ref(false)
const errors = ref<Record<string, string>>({})

// FormModel interface — same 25 fields as the current dialog
interface FormModel { /* ... same as CampEditionUpdateDialog ... */ }

const form = reactive<FormModel>({ /* ... defaults ... */ })

const isOpenEdition = computed(() => edition.value?.status === 'Open')
```

#### 2.2 Data Loading (`onMounted`)

```typescript
onMounted(async () => {
  const result = await getEditionById(route.params.id as string)
  if (result) {
    edition.value = result
    initializeForm(result)
  }
})
```

#### 2.3 Form Initialization (fix date bugs)

Critical: use `parseDateLocal()` for ALL date fields. Fix notes initialization.

```typescript
const initializeForm = (ed: CampEdition) => {
  // FIX: Use parseDateLocal for startDate/endDate instead of new Date()
  form.startDate = ed.startDate ? parseDateLocal(ed.startDate) : null
  form.endDate = ed.endDate ? parseDateLocal(ed.endDate) : null
  form.pricePerAdult = ed.pricePerAdult
  form.pricePerChild = ed.pricePerChild
  form.pricePerBaby = ed.pricePerBaby
  form.useCustomAgeRanges = ed.useCustomAgeRanges
  form.customBabyMaxAge = ed.customBabyMaxAge ?? null
  form.customChildMinAge = ed.customChildMinAge ?? null
  form.customChildMaxAge = ed.customChildMaxAge ?? null
  form.customAdultMinAge = ed.customAdultMinAge ?? null
  form.maxCapacity = ed.maxCapacity > 0 ? ed.maxCapacity : null
  // FIX: Load existing notes instead of empty string
  form.notes = ed.notes ?? ''
  form.allowPartialAttendance = ed.pricePerAdultWeek != null
  form.halfDate = ed.halfDate ? parseDateLocal(ed.halfDate) : null
  form.pricePerAdultWeek = ed.pricePerAdultWeek ?? null
  form.pricePerChildWeek = ed.pricePerChildWeek ?? null
  form.pricePerBabyWeek = ed.pricePerBabyWeek ?? null
  form.allowWeekendVisit = ed.weekendStartDate != null
  form.weekendStartDate = ed.weekendStartDate ? parseDateLocal(ed.weekendStartDate) : null
  form.weekendEndDate = ed.weekendEndDate ? parseDateLocal(ed.weekendEndDate) : null
  form.pricePerAdultWeekend = ed.pricePerAdultWeekend ?? null
  form.pricePerChildWeekend = ed.pricePerChildWeekend ?? null
  form.pricePerBabyWeekend = ed.pricePerBabyWeekend ?? null
  form.maxWeekendCapacity = ed.maxWeekendCapacity ?? null
  form.description = ed.description ?? ''
  // FIX: Use parseDateLocal for payment deadlines too
  form.firstPaymentDeadline = ed.firstPaymentDeadline ? parseDateLocal(ed.firstPaymentDeadline) : null
  form.secondPaymentDeadline = ed.secondPaymentDeadline ? parseDateLocal(ed.secondPaymentDeadline) : null
  errors.value = {}
}
```

#### 2.4 Validation (fix Open edition blocking)

```typescript
const validate = (): boolean => {
  errors.value = {}

  // Only validate dates/prices when they CAN be edited (not Open)
  if (!isOpenEdition.value) {
    if (!form.startDate) errors.value.startDate = 'La fecha de inicio es obligatoria'
    if (!form.endDate) errors.value.endDate = 'La fecha de fin es obligatoria'
    if (form.endDate && form.startDate && form.endDate <= form.startDate) {
      errors.value.endDate = 'La fecha de fin debe ser posterior a la fecha de inicio'
    }
    if (form.pricePerAdult < 0) errors.value.pricePerAdult = 'El precio debe ser >= 0'
    if (form.pricePerChild < 0) errors.value.pricePerChild = 'El precio debe ser >= 0'
    if (form.pricePerBaby < 0) errors.value.pricePerBaby = 'El precio debe ser >= 0'

    // Partial attendance validation
    if (form.allowPartialAttendance) {
      if (form.pricePerAdultWeek == null || form.pricePerAdultWeek < 0)
        errors.value.pricePerAdultWeek = 'Obligatorio'
      if (form.pricePerChildWeek == null || form.pricePerChildWeek < 0)
        errors.value.pricePerChildWeek = 'Obligatorio'
      if (form.pricePerBabyWeek == null || form.pricePerBabyWeek < 0)
        errors.value.pricePerBabyWeek = 'Obligatorio'
    }

    // Weekend validation
    if (form.allowWeekendVisit) {
      if (!form.weekendStartDate) errors.value.weekendStartDate = 'Obligatorio'
      if (!form.weekendEndDate) errors.value.weekendEndDate = 'Obligatorio'
      if (form.weekendStartDate && form.weekendEndDate && form.weekendEndDate <= form.weekendStartDate)
        errors.value.weekendEndDate = 'Debe ser posterior a la de inicio'
      if (form.pricePerAdultWeekend == null || form.pricePerAdultWeekend < 0)
        errors.value.pricePerAdultWeekend = 'Obligatorio'
      if (form.pricePerChildWeekend == null || form.pricePerChildWeekend < 0)
        errors.value.pricePerChildWeekend = 'Obligatorio'
      if (form.pricePerBabyWeekend == null || form.pricePerBabyWeekend < 0)
        errors.value.pricePerBabyWeekend = 'Obligatorio'
      if (form.maxWeekendCapacity != null && form.maxWeekendCapacity <= 0)
        errors.value.maxWeekendCapacity = 'Debe ser mayor a 0'
    }

    // Custom age ranges validation
    if (form.useCustomAgeRanges) {
      if (!form.customBabyMaxAge) errors.value.customBabyMaxAge = 'Obligatorio'
      if (!form.customChildMinAge) errors.value.customChildMinAge = 'Obligatorio'
      if (!form.customChildMaxAge) errors.value.customChildMaxAge = 'Obligatorio'
      if (!form.customAdultMinAge) errors.value.customAdultMinAge = 'Obligatorio'
      if (form.customBabyMaxAge && form.customChildMinAge && form.customBabyMaxAge >= form.customChildMinAge)
        errors.value.customBabyMaxAge = 'Debe ser menor a la edad mínima de niño'
      if (form.customChildMaxAge && form.customAdultMinAge && form.customChildMaxAge >= form.customAdultMinAge)
        errors.value.customChildMaxAge = 'Debe ser menor a la edad mínima de adulto'
    }
  }

  // Always validate (editable for all statuses)
  if (form.maxCapacity !== null && form.maxCapacity !== undefined && form.maxCapacity <= 0) {
    errors.value.maxCapacity = 'La capacidad máxima debe ser mayor a 0'
  }
  if (form.notes && form.notes.length > 2000) {
    errors.value.notes = 'Las notas no deben superar los 2000 caracteres'
  }

  return Object.keys(errors.value).length === 0
}
```

#### 2.5 Save Handler (send original values for Open restricted fields)

```typescript
const formatDateToIso = (date: Date | null): string => {
  if (!date) return ''
  return formatDateLocal(date)
}

const handleSave = async () => {
  if (!validate() || !edition.value) return

  saving.value = true
  const ed = edition.value

  const request: UpdateCampEditionRequest = {
    // For Open editions, send original values for restricted fields
    startDate: isOpenEdition.value ? ed.startDate : formatDateToIso(form.startDate),
    endDate: isOpenEdition.value ? ed.endDate : formatDateToIso(form.endDate),
    pricePerAdult: isOpenEdition.value ? ed.pricePerAdult : form.pricePerAdult,
    pricePerChild: isOpenEdition.value ? ed.pricePerChild : form.pricePerChild,
    pricePerBaby: isOpenEdition.value ? ed.pricePerBaby : form.pricePerBaby,
    useCustomAgeRanges: isOpenEdition.value ? ed.useCustomAgeRanges : form.useCustomAgeRanges,
    ...((!isOpenEdition.value && form.useCustomAgeRanges) && {
      customBabyMaxAge: form.customBabyMaxAge ?? undefined,
      customChildMinAge: form.customChildMinAge ?? undefined,
      customChildMaxAge: form.customChildMaxAge ?? undefined,
      customAdultMinAge: form.customAdultMinAge ?? undefined
    }),
    ...(isOpenEdition.value && ed.useCustomAgeRanges && {
      customBabyMaxAge: ed.customBabyMaxAge ?? undefined,
      customChildMinAge: ed.customChildMinAge ?? undefined,
      customChildMaxAge: ed.customChildMaxAge ?? undefined,
      customAdultMinAge: ed.customAdultMinAge ?? undefined
    }),
    // Always use form values for allowed fields
    maxCapacity: form.maxCapacity ?? undefined,
    notes: form.notes || undefined,
    description: form.description || undefined,
    // Partial attendance — send originals when Open
    halfDate: isOpenEdition.value ? (ed.halfDate ?? null)
      : (form.allowPartialAttendance && form.halfDate ? formatDateToIso(form.halfDate) : null),
    pricePerAdultWeek: isOpenEdition.value ? (ed.pricePerAdultWeek ?? null)
      : (form.allowPartialAttendance ? form.pricePerAdultWeek : null),
    pricePerChildWeek: isOpenEdition.value ? (ed.pricePerChildWeek ?? null)
      : (form.allowPartialAttendance ? form.pricePerChildWeek : null),
    pricePerBabyWeek: isOpenEdition.value ? (ed.pricePerBabyWeek ?? null)
      : (form.allowPartialAttendance ? form.pricePerBabyWeek : null),
    // Weekend — send originals when Open
    weekendStartDate: isOpenEdition.value ? (ed.weekendStartDate ?? null)
      : (form.allowWeekendVisit && form.weekendStartDate ? formatDateToIso(form.weekendStartDate) : null),
    weekendEndDate: isOpenEdition.value ? (ed.weekendEndDate ?? null)
      : (form.allowWeekendVisit && form.weekendEndDate ? formatDateToIso(form.weekendEndDate) : null),
    pricePerAdultWeekend: isOpenEdition.value ? (ed.pricePerAdultWeekend ?? null)
      : (form.allowWeekendVisit ? form.pricePerAdultWeekend : null),
    pricePerChildWeekend: isOpenEdition.value ? (ed.pricePerChildWeekend ?? null)
      : (form.allowWeekendVisit ? form.pricePerChildWeekend : null),
    pricePerBabyWeekend: isOpenEdition.value ? (ed.pricePerBabyWeekend ?? null)
      : (form.allowWeekendVisit ? form.pricePerBabyWeekend : null),
    maxWeekendCapacity: isOpenEdition.value ? (ed.maxWeekendCapacity ?? null)
      : (form.allowWeekendVisit ? (form.maxWeekendCapacity || null) : null),
    // Payment deadlines — always editable
    firstPaymentDeadline: form.firstPaymentDeadline ? formatDateToIso(form.firstPaymentDeadline) : null,
    secondPaymentDeadline: form.secondPaymentDeadline ? formatDateToIso(form.secondPaymentDeadline) : null
  }

  const result = await updateEdition(ed.id, request)
  saving.value = false

  if (result) {
    toast.add({
      severity: 'success',
      summary: 'Éxito',
      detail: 'Edición actualizada correctamente',
      life: 3000
    })
    router.push({ name: 'camp-edition-detail', params: { id: ed.id } })
  }
}
```

#### 2.6 Template Structure

Use the card-based layout pattern from `CampEditionDetailPage.vue` and grid patterns from existing forms:

```html
<template>
  <Container>
    <Toast />
    <div class="py-8">
      <!-- Header with Back + Title -->
      <div class="mb-6 flex items-center justify-between">
        <div>
          <Button label="Volver" icon="pi pi-arrow-left" text @click="router.back()" />
          <h1 class="mt-2 text-3xl font-bold text-gray-900">
            Editar Edición {{ edition?.year }}
          </h1>
        </div>
      </div>

      <!-- Loading spinner -->
      <div v-if="loading && !edition" class="flex justify-center py-12">
        <ProgressSpinner />
      </div>

      <!-- Edition not found -->
      <div v-else-if="!edition" class="rounded-lg border border-gray-200 bg-gray-50 p-8 text-center">
        <p class="text-gray-500">Edición no encontrada.</p>
      </div>

      <!-- Form -->
      <div v-else class="space-y-6">
        <!-- Info message for Open editions -->
        <Message v-if="isOpenEdition" severity="info" :closable="false">
          Esta edición está abierta para inscripciones. Solo se pueden modificar la capacidad,
          las notas, la descripción y las fechas de pago.
        </Message>

        <!-- API Error -->
        <Message v-if="error" severity="error" :closable="false">{{ error }}</Message>

        <!-- Section 1: General Information (dates + capacity) -->
        <div class="rounded-lg border border-gray-200 bg-white p-6">
          <h2 class="mb-4 text-lg font-semibold text-gray-900">Información General</h2>
          <div class="grid grid-cols-1 gap-4 sm:grid-cols-2">
            <!-- startDate field -->
            <!-- endDate field -->
          </div>
          <div class="mt-4">
            <!-- maxCapacity field -->
          </div>
        </div>

        <!-- Section 2: Pricing -->
        <div class="rounded-lg border border-gray-200 bg-white p-6">
          <h2 class="mb-4 text-lg font-semibold text-gray-900">Precios</h2>
          <div class="grid grid-cols-1 gap-4 sm:grid-cols-3">
            <!-- pricePerAdult, pricePerChild, pricePerBaby -->
          </div>
        </div>

        <!-- Section 3: Partial Attendance (collapsible) -->
        <div class="rounded-lg border border-gray-200 bg-white p-6">
          <div class="flex items-center gap-3">
            <ToggleSwitch v-model="form.allowPartialAttendance" :disabled="isOpenEdition" />
            <h2 class="text-lg font-semibold text-gray-900">Inscripción por semanas</h2>
          </div>
          <div v-if="form.allowPartialAttendance" class="mt-4 space-y-4">
            <!-- halfDate, week prices -->
          </div>
        </div>

        <!-- Section 4: Weekend Visits (collapsible) -->
        <div class="rounded-lg border border-gray-200 bg-white p-6">
          <!-- Same pattern as partial attendance -->
        </div>

        <!-- Section 5: Custom Age Ranges (collapsible) -->
        <div class="rounded-lg border border-gray-200 bg-white p-6">
          <!-- Toggle + 4 age fields -->
        </div>

        <!-- Section 6: Payment Deadlines -->
        <div class="rounded-lg border border-gray-200 bg-white p-6">
          <h2 class="mb-4 text-lg font-semibold text-gray-900">Fechas límite de pago</h2>
          <div class="grid grid-cols-1 gap-4 sm:grid-cols-2">
            <!-- firstPaymentDeadline, secondPaymentDeadline -->
          </div>
        </div>

        <!-- Section 7: Additional Info -->
        <div class="rounded-lg border border-gray-200 bg-white p-6">
          <h2 class="mb-4 text-lg font-semibold text-gray-900">Información Adicional</h2>
          <!-- notes Textarea (max 2000) -->
          <!-- description Textarea -->
        </div>

        <!-- Action Buttons (sticky bottom or card) -->
        <div class="flex justify-end gap-3 rounded-lg border border-gray-200 bg-white p-4">
          <Button label="Cancelar" text :disabled="saving" @click="router.back()" />
          <Button
            label="Guardar cambios"
            :loading="saving"
            :disabled="saving"
            data-testid="save-edition-btn"
            @click="handleSave"
          />
        </div>
      </div>
    </div>
  </Container>
</template>
```

- **Field pattern**: Use the same `<div class="flex flex-col gap-1">` + label + input + error span pattern from the existing dialog
- **Disabled fields**: Pass `:disabled="isOpenEdition"` to all restricted fields (dates, prices, toggles, age ranges)
- **Data testids**: Keep `save-edition-btn` for test compatibility; add `data-testid="edition-edit-page"` to root

---

### Step 3: Update CampEditionsPage — Remove Modal, Navigate to Edit Page

- **File**: `frontend/src/views/camps/CampEditionsPage.vue`
- **Action**: Replace modal-based editing with router navigation
- **Implementation Steps**:
  1. Remove the `CampEditionUpdateDialog` import (line 17)
  2. Remove `showEditDialog` ref (line 49)
  3. Update `handleEdit` function to navigate instead of opening dialog:
     ```typescript
     const handleEdit = (edition: CampEdition) => {
       router.push({ name: 'camp-edition-edit', params: { id: edition.id } })
     }
     ```
  4. Remove the `<CampEditionUpdateDialog>` component from template (lines 246-247)
  5. Remove `handleEditionSaved` callback function (lines 135-143) — no longer needed since the edit page handles its own toast
  6. Keep `selectedEdition` ref only if still needed by `CampEditionStatusDialog`

---

### Step 4: Update CampEditionDetailPage — Add Edit Button

- **File**: `frontend/src/views/camps/CampEditionDetailPage.vue`
- **Action**: Add "Edit" button in the header, visible only for Board/Admin users
- **Implementation Steps**:
  1. Import `useRouter` (already imported)
  2. The `isBoard` computed is already defined (line 21)
  3. Add an "Edit" button next to the title, conditionally rendered:
     ```html
     <div class="mb-6 flex items-center justify-between">
       <div>
         <h1 class="text-3xl font-bold text-gray-900">
           Edición {{ edition.year }}
           <span v-if="edition.name"> — {{ edition.name }}</span>
         </h1>
         <p class="mt-1 text-gray-500">{{ edition.location }}</p>
       </div>
       <Button
         v-if="isBoard && edition.status !== 'Closed' && edition.status !== 'Completed'"
         label="Editar"
         icon="pi pi-pencil"
         @click="router.push({ name: 'camp-edition-edit', params: { id: edition.id } })"
       />
     </div>
     ```

---

### Step 5: Delete CampEditionUpdateDialog

- **File**: `frontend/src/components/camps/CampEditionUpdateDialog.vue`
- **Action**: Delete the file entirely — it's fully replaced by the new page
- **Pre-check**: Verify no other files import this component (only `CampEditionsPage.vue` did, and it was removed in Step 3)

---

### Step 6: Update Cypress E2E Tests

- **File**: `frontend/cypress/e2e/camps/camp-editions.cy.ts`
- **Action**: Update tests to reflect navigation-based editing instead of modal
- **Implementation Steps**:
  1. Update the "should open edit dialog" test → "should navigate to edit page":
     ```typescript
     it('should navigate to edit page when clicking edit', () => {
       cy.get('[data-testid="edit-edition-btn"]').first().click()
       cy.url().should('include', '/camps/editions/')
       cy.url().should('include', '/edit')
     })
     ```
  2. Update the "should save changes" test:
     ```typescript
     it('should save changes and redirect after editing', () => {
       cy.intercept('GET', '/api/camps/editions/*', { fixture: 'edition.json' }).as('getEdition')
       cy.intercept('PUT', '/api/camps/editions/*', { fixture: 'edition-updated.json' }).as('putEdition')
       cy.get('[data-testid="edit-edition-btn"]').first().click()
       cy.wait('@getEdition')
       cy.get('[data-testid="save-edition-btn"]').click()
       cy.wait('@putEdition')
       cy.contains('Edición actualizada correctamente').should('be.visible')
     })
     ```
  3. Remove any references to `edition-dialog` testid (modal no longer exists)
  4. If there's a test fixture for the edition GET response, ensure it includes all fields (dates, notes, etc.)

---

### Step 7: Update Technical Documentation

- **Action**: Review and update technical documentation according to changes made
- **Implementation Steps**:
  1. **Review Changes**: Analyze all code changes (new route, removed component, updated pages)
  2. **Identify Documentation Files**:
     - Routing changes → Update route listing if documented anywhere in `ai-specs/specs/`
     - Component removal → No docs reference the dialog specifically
     - UI patterns → Note the new full-page edit pattern in `frontend-standards.mdc` if applicable
  3. **Update Documentation**: Update any affected files in English
  4. **Verify**: Confirm all changes are accurately reflected

## Implementation Order

1. **Step 0**: Create feature branch
2. **Step 1**: Add route configuration (quick, unblocks Step 2)
3. **Step 2**: Create `CampEditionEditPage.vue` (main work — the full-page form with all bug fixes)
4. **Step 3**: Update `CampEditionsPage.vue` (remove modal, navigate to new page)
5. **Step 4**: Update `CampEditionDetailPage.vue` (add Edit button)
6. **Step 5**: Delete `CampEditionUpdateDialog.vue`
7. **Step 6**: Update Cypress E2E tests
8. **Step 7**: Update technical documentation

## Testing Checklist

### Unit Tests (Vitest)
- [ ] (Optional) Test `validate()` logic — validation skips restricted fields for Open editions
- [ ] (Optional) Test `initializeForm()` — dates use `parseDateLocal`, notes loaded from edition

### E2E Tests (Cypress)
- [ ] Edit button on editions list navigates to `/camps/editions/:id/edit`
- [ ] Edit page loads edition data correctly (dates, prices, notes populated)
- [ ] Save button submits form and shows success toast
- [ ] Cancel button navigates back without saving
- [ ] Open edition: restricted fields are disabled, allowed fields are editable
- [ ] Edit button on detail page navigates to edit page (Board/Admin only)
- [ ] Edit button is not visible for Closed/Completed editions

### Manual Verification
- [ ] Date fields show correct values (no timezone shift — test in both UTC+ and UTC- zones)
- [ ] Notes field retains existing content when editing
- [ ] Open edition can be saved (notes, description, capacity, payment deadlines)
- [ ] Form sections are visually clear and well-spaced on all screen sizes

## Error Handling Patterns

- **Loading state**: Show `ProgressSpinner` while `getEditionById` is in progress
- **Not found**: Show "Edición no encontrada" message if `getEditionById` returns null
- **API errors**: Display `error` from `useCampEditions()` in a `Message severity="error"` component
- **Validation errors**: Inline `<span class="text-xs text-red-600">` below each field
- **Save state**: Use separate `saving` ref (not the composable's `loading`) to avoid interfering with initial load state

## UI/UX Considerations

- **Card layout**: Each form section in its own `rounded-lg border border-gray-200 bg-white p-6` card, matching the detail page pattern
- **Responsive grids**: `grid-cols-1 sm:grid-cols-2` for dates/deadlines, `sm:grid-cols-3` for pricing rows
- **Disabled state**: PrimeVue components natively support `:disabled` with grayed-out styling
- **Info message**: PrimeVue `Message severity="info"` at the top for Open edition restrictions
- **Section toggles**: `ToggleSwitch` inline with section headers for partial attendance, weekend, age ranges
- **Action buttons**: Right-aligned in a bottom card with clear Cancel/Save hierarchy

## Dependencies

- No new npm packages required
- **PrimeVue components used**: Button, InputNumber, Textarea, ToggleSwitch, Message, ProgressSpinner, Toast
- **Project composables**: `useCampEditions` (existing)
- **Project utilities**: `parseDateLocal`, `formatDateLocal` (existing)
- **Project components**: Container, DateInput (existing)

## Notes

- All code and comments must be in **English** (UI labels remain in Spanish as is the existing pattern)
- No `any` types — use proper TypeScript interfaces
- The `CampEdition` type in `types/camp-edition.ts` has a `notes` field (optional string) but the current dialog hardcodes it to `''` — this is a confirmed bug to fix
- The backend `UpdateCampEditionRequest` requires `startDate` and `endDate` even for Open editions (it compares them against current values). The frontend must send the original values for restricted fields when Open.
- Payment deadline dates should use `formatDateLocal()` (not `toISOString()`) for consistency — the backend stores them as date-only strings

## Next Steps After Implementation

1. Manual QA testing with an Open edition to verify all bugs are fixed
2. Verify no regressions on Draft/Proposed edition editing
3. Consider adding a "notes" field to the edition detail page (currently not displayed)
4. Merge PR and clean up feature branch
