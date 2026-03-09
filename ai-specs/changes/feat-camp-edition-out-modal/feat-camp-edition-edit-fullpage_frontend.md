# Frontend Implementation Plan: feat-camp-edition-edit-fullpage — Inline Edit on Detail Page

## Overview

Replace the `CampEditionUpdateDialog` modal with inline editing on the existing `CampEditionDetailPage.vue`, following the `ProfilePage.vue` pattern. The detail page is expanded to show all edition fields (many are currently missing) and gains an `isEditing` toggle for Board/Admin users.

The page uses a **vertical sidebar with section tabs** (PrimeVue `Tabs` with `orientation="vertical"`) so users can navigate between the 7+ field sections without scrolling. On mobile (`< lg` breakpoint), the sidebar collapses to horizontal tabs at the top.

Architecture: Vue 3 Composition API, `<script setup lang="ts">`, PrimeVue + Tailwind CSS, composable-based API communication.

## Architecture Context

### Components/Composables Involved

| File | Role |
|------|------|
| `frontend/src/views/camps/CampEditionDetailPage.vue` | **MAJOR MODIFY** — Add missing read fields + inline edit mode |
| `frontend/src/views/camps/CampEditionsPage.vue` | MODIFY — Remove modal, navigate to detail page |
| `frontend/src/components/camps/CampEditionUpdateDialog.vue` | **DELETE** — Replaced by inline edit |
| `frontend/src/composables/useCampEditions.ts` | READ-ONLY — `getEditionById()`, `updateEdition()` |
| `frontend/src/utils/date.ts` | READ-ONLY — `parseDateLocal()`, `formatDateLocal()` |
| `frontend/src/components/shared/DateInput.vue` | READ-ONLY — Used for date inputs in edit mode |
| `frontend/src/types/camp-edition.ts` | READ-ONLY — `CampEdition`, `UpdateCampEditionRequest` |

### Routing

- **No new routes** — edit happens inline on existing `/camps/editions/:id`
- `CampEditionsPage` edit button navigates to `/camps/editions/:id` (with optional `?edit=true` query param to auto-enter edit mode)

### State Management

- **Local state only** — no Pinia store changes
- `isEditing` ref toggles read/edit mode
- `reactive()` for form model, `ref()` for errors, saving state

## Implementation Steps

### Step 0: Create Feature Branch

- **Action**: Create and switch to feature branch
- **Branch Naming**: `feature/feat-camp-edition-edit-fullpage-frontend`
- **Implementation Steps**:
  1. `git checkout main && git pull origin main`
  2. `git checkout -b feature/feat-camp-edition-edit-fullpage-frontend`

---

### Step 1: Restructure Detail Page with Vertical Sidebar Navigation

- **File**: `frontend/src/views/camps/CampEditionDetailPage.vue`
- **Action**: Replace the current flat card layout with a vertical tab sidebar + content panel layout. Add all missing fields.
- **Dependencies** (new imports):
  - `Tabs`, `TabList`, `Tab`, `TabPanels`, `TabPanel` from PrimeVue
  - Already used in `PaymentsAdminPanel.vue` and `AuthContainer.vue`

#### 1.1 Section Definitions

Define the tab sections as a constant:

```typescript
const sections = [
  { value: '0', label: 'General', icon: 'pi pi-info-circle' },
  { value: '1', label: 'Precios', icon: 'pi pi-euro' },
  { value: '2', label: 'Semanas', icon: 'pi pi-calendar-minus' },
  { value: '3', label: 'Fin de semana', icon: 'pi pi-sun' },
  { value: '4', label: 'Edades', icon: 'pi pi-users' },
  { value: '5', label: 'Pagos', icon: 'pi pi-credit-card' },
  { value: '6', label: 'Notas', icon: 'pi pi-file-edit' },
  { value: '7', label: 'Alojamientos', icon: 'pi pi-home' },
  { value: '8', label: 'Extras', icon: 'pi pi-plus-circle' },
] as const

const activeTab = ref('0')
```

#### 1.2 Layout Structure — Sidebar + Content

Replace the current flat layout with a two-column layout:

```html
<!-- After header and loading/error states -->
<div v-else class="flex flex-col gap-6 lg:flex-row">
  <!-- Vertical sidebar (desktop) / Horizontal tabs (mobile) -->
  <Tabs v-model:value="activeTab" orientation="vertical" class="hidden lg:flex">
    <TabList class="w-48 shrink-0">
      <Tab v-for="s in sections" :key="s.value" :value="s.value">
        <div class="flex items-center gap-2">
          <i :class="s.icon" />
          <span>{{ s.label }}</span>
        </div>
      </Tab>
    </TabList>
    <TabPanels class="flex-1">
      <TabPanel v-for="s in sections" :key="s.value" :value="s.value">
        <!-- Section content rendered here -->
      </TabPanel>
    </TabPanels>
  </Tabs>

  <!-- Mobile: horizontal scrollable tabs -->
  <Tabs v-model:value="activeTab" class="lg:hidden">
    <TabList>
      <Tab v-for="s in sections" :key="s.value" :value="s.value">
        <div class="flex items-center gap-1">
          <i :class="s.icon" class="text-xs" />
          <span class="text-xs">{{ s.label }}</span>
        </div>
      </Tab>
    </TabList>
    <TabPanels>
      <TabPanel v-for="s in sections" :key="s.value" :value="s.value">
        <!-- Section content rendered here -->
      </TabPanel>
    </TabPanels>
  </Tabs>
</div>
```

**Note**: To avoid duplicating content, extract each section's content into a template helper or use a single `Tabs` component with responsive classes. The recommended approach is a **single `Tabs`** with responsive orientation handling:

```html
<Tabs v-model:value="activeTab">
  <div class="flex flex-col gap-6 lg:flex-row">
    <TabList class="flex overflow-x-auto lg:w-48 lg:shrink-0 lg:flex-col lg:overflow-visible">
      <Tab v-for="s in sections" :key="s.value" :value="s.value" class="whitespace-nowrap">
        <div class="flex items-center gap-2">
          <i :class="s.icon" />
          <span>{{ s.label }}</span>
        </div>
      </Tab>
    </TabList>
    <TabPanels class="flex-1 min-w-0">
      <!-- Tab 0: General Information -->
      <TabPanel value="0">
        <div class="rounded-lg border border-gray-200 bg-white p-6">
          <!-- content -->
        </div>
      </TabPanel>
      <!-- Tab 1: Pricing -->
      <TabPanel value="1">...</TabPanel>
      <!-- Tab 2: Partial Attendance -->
      <TabPanel value="2">...</TabPanel>
      <!-- etc. -->
    </TabPanels>
  </div>
</Tabs>
```

#### 1.3 Section Content — Read Mode

Each `TabPanel` contains a card with the section's read-only fields. Use the existing key-value row pattern (`flex justify-between` with `text-gray-600` label and value):

- **Tab 0 — General Information** (existing fields): Status badge, year, start date, end date, capacity
- **Tab 1 — Pricing** (existing fields): Price per adult, child, baby
- **Tab 2 — Partial Attendance** (NEW): Show week prices if configured, otherwise "No configurado"
- **Tab 3 — Weekend Visits** (NEW): Show weekend dates/prices if configured, otherwise "No configurado"
- **Tab 4 — Custom Age Ranges** (NEW): Show age ranges if `useCustomAgeRanges`, otherwise "Rangos por defecto"
- **Tab 5 — Payment Deadlines** (NEW): Show deadlines if set, otherwise "Calculadas automáticamente"
- **Tab 6 — Notes & Description** (notes is NEW): Notes with `whitespace-pre-line`, description
- **Tab 7 — Accommodations** (existing): Render `CampEditionAccommodationsPanel`; only for Board/Admin
- **Tab 8 — Extras** (existing): Render `CampEditionExtrasList`

**Key difference from old layout**: Empty/unconfigured sections now show a "not configured" message instead of being hidden, since the sidebar always lists all sections.

---

### Step 2: Add Inline Edit Mode

- **File**: `frontend/src/views/camps/CampEditionDetailPage.vue`
- **Action**: Add `isEditing` toggle with full form support, following the `ProfilePage.vue` pattern
- **Dependencies** (new imports needed):
  - `reactive` from `vue`
  - `useToast` from `primevue/usetoast`
  - `Toast` from `primevue/toast`
  - `DateInput` from `@/components/shared/DateInput.vue`
  - `InputNumber` from `primevue/inputnumber`
  - `Textarea` from `primevue/textarea`
  - `ToggleSwitch` from `primevue/toggleswitch`
  - `parseDateLocal`, `formatDateLocal` from `@/utils/date`
  - `UpdateCampEditionRequest` type from `@/types/camp-edition`
  - Add `updateEdition` to the `useCampEditions()` destructuring

#### 2.1 Script Setup — State & Form Model

Add the following to the script:

```typescript
const toast = useToast()

// Edit mode state
const isEditing = ref(false)
const saving = ref(false)
const errors = ref<Record<string, string>>({})

const isOpenEdition = computed(() => edition.value?.status === 'Open')
const canEdit = computed(() =>
  isBoard.value &&
  edition.value != null &&
  edition.value.status !== 'Closed' &&
  edition.value.status !== 'Completed'
)

interface FormModel {
  startDate: Date | null
  endDate: Date | null
  pricePerAdult: number
  pricePerChild: number
  pricePerBaby: number
  useCustomAgeRanges: boolean
  customBabyMaxAge: number | null
  customChildMinAge: number | null
  customChildMaxAge: number | null
  customAdultMinAge: number | null
  maxCapacity: number | null
  notes: string
  allowPartialAttendance: boolean
  halfDate: Date | null
  pricePerAdultWeek: number | null
  pricePerChildWeek: number | null
  pricePerBabyWeek: number | null
  allowWeekendVisit: boolean
  weekendStartDate: Date | null
  weekendEndDate: Date | null
  pricePerAdultWeekend: number | null
  pricePerChildWeekend: number | null
  pricePerBabyWeekend: number | null
  maxWeekendCapacity: number | null
  description: string
  firstPaymentDeadline: Date | null
  secondPaymentDeadline: Date | null
}

const form = reactive<FormModel>({
  startDate: null, endDate: null,
  pricePerAdult: 0, pricePerChild: 0, pricePerBaby: 0,
  useCustomAgeRanges: false,
  customBabyMaxAge: null, customChildMinAge: null, customChildMaxAge: null, customAdultMinAge: null,
  maxCapacity: null, notes: '', description: '',
  allowPartialAttendance: false,
  halfDate: null, pricePerAdultWeek: null, pricePerChildWeek: null, pricePerBabyWeek: null,
  allowWeekendVisit: false,
  weekendStartDate: null, weekendEndDate: null,
  pricePerAdultWeekend: null, pricePerChildWeekend: null, pricePerBabyWeekend: null,
  maxWeekendCapacity: null,
  firstPaymentDeadline: null, secondPaymentDeadline: null
})
```

#### 2.2 `startEditing()` — Initialize Form from Edition

```typescript
const startEditing = () => {
  if (!edition.value) return
  const ed = edition.value

  // FIX: Use parseDateLocal for ALL date fields
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
  // FIX: Load existing notes
  form.notes = ed.notes ?? ''
  form.description = ed.description ?? ''
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
  form.firstPaymentDeadline = ed.firstPaymentDeadline ? parseDateLocal(ed.firstPaymentDeadline) : null
  form.secondPaymentDeadline = ed.secondPaymentDeadline ? parseDateLocal(ed.secondPaymentDeadline) : null
  errors.value = {}
  isEditing.value = true
}

const cancelEditing = () => {
  isEditing.value = false
  errors.value = {}
}
```

#### 2.3 `validate()` — Status-Aware Validation

```typescript
const validate = (): boolean => {
  errors.value = {}

  // Only validate dates/prices when NOT Open (editable)
  if (!isOpenEdition.value) {
    if (!form.startDate) errors.value.startDate = 'La fecha de inicio es obligatoria'
    if (!form.endDate) errors.value.endDate = 'La fecha de fin es obligatoria'
    if (form.endDate && form.startDate && form.endDate <= form.startDate)
      errors.value.endDate = 'La fecha de fin debe ser posterior a la fecha de inicio'
    if (form.pricePerAdult < 0) errors.value.pricePerAdult = 'El precio debe ser >= 0'
    if (form.pricePerChild < 0) errors.value.pricePerChild = 'El precio debe ser >= 0'
    if (form.pricePerBaby < 0) errors.value.pricePerBaby = 'El precio debe ser >= 0'

    if (form.allowPartialAttendance) {
      if (form.pricePerAdultWeek == null || form.pricePerAdultWeek < 0)
        errors.value.pricePerAdultWeek = 'El precio por adulto/semana es obligatorio'
      if (form.pricePerChildWeek == null || form.pricePerChildWeek < 0)
        errors.value.pricePerChildWeek = 'El precio infantil/semana es obligatorio'
      if (form.pricePerBabyWeek == null || form.pricePerBabyWeek < 0)
        errors.value.pricePerBabyWeek = 'El precio por bebé/semana es obligatorio'
    }

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

    if (form.useCustomAgeRanges) {
      if (!form.customBabyMaxAge) errors.value.customBabyMaxAge = 'Obligatorio'
      if (!form.customChildMinAge) errors.value.customChildMinAge = 'Obligatorio'
      if (!form.customChildMaxAge) errors.value.customChildMaxAge = 'Obligatorio'
      if (!form.customAdultMinAge) errors.value.customAdultMinAge = 'Obligatorio'
      if (form.customBabyMaxAge && form.customChildMinAge && form.customBabyMaxAge >= form.customChildMinAge)
        errors.value.customBabyMaxAge = 'Debe ser menor a la edad mínima infantil'
      if (form.customChildMaxAge && form.customAdultMinAge && form.customChildMaxAge >= form.customAdultMinAge)
        errors.value.customChildMaxAge = 'Debe ser menor a la edad mínima de adulto'
    }
  }

  // Always validate (editable for all statuses)
  if (form.maxCapacity !== null && form.maxCapacity !== undefined && form.maxCapacity <= 0)
    errors.value.maxCapacity = 'La capacidad máxima debe ser mayor a 0'
  if (form.notes && form.notes.length > 2000)
    errors.value.notes = 'Las notas no deben superar los 2000 caracteres'

  return Object.keys(errors.value).length === 0
}
```

#### 2.4 `handleSave()` — Send Original Values for Open Restricted Fields

```typescript
const formatDateToIso = (date: Date | null): string => {
  if (!date) return ''
  return formatDateLocal(date)
}

const handleSave = async () => {
  if (!validate() || !edition.value) return

  saving.value = true
  const ed = edition.value
  const open = isOpenEdition.value

  const request: UpdateCampEditionRequest = {
    startDate: open ? ed.startDate : formatDateToIso(form.startDate),
    endDate: open ? ed.endDate : formatDateToIso(form.endDate),
    pricePerAdult: open ? ed.pricePerAdult : form.pricePerAdult,
    pricePerChild: open ? ed.pricePerChild : form.pricePerChild,
    pricePerBaby: open ? ed.pricePerBaby : form.pricePerBaby,
    useCustomAgeRanges: open ? ed.useCustomAgeRanges : form.useCustomAgeRanges,
    ...((open ? ed.useCustomAgeRanges : form.useCustomAgeRanges) && {
      customBabyMaxAge: open ? (ed.customBabyMaxAge ?? undefined) : (form.customBabyMaxAge ?? undefined),
      customChildMinAge: open ? (ed.customChildMinAge ?? undefined) : (form.customChildMinAge ?? undefined),
      customChildMaxAge: open ? (ed.customChildMaxAge ?? undefined) : (form.customChildMaxAge ?? undefined),
      customAdultMinAge: open ? (ed.customAdultMinAge ?? undefined) : (form.customAdultMinAge ?? undefined),
    }),
    maxCapacity: form.maxCapacity ?? undefined,
    notes: form.notes || undefined,
    description: form.description || undefined,
    halfDate: open ? (ed.halfDate ?? null)
      : (form.allowPartialAttendance && form.halfDate ? formatDateToIso(form.halfDate) : null),
    pricePerAdultWeek: open ? (ed.pricePerAdultWeek ?? null)
      : (form.allowPartialAttendance ? form.pricePerAdultWeek : null),
    pricePerChildWeek: open ? (ed.pricePerChildWeek ?? null)
      : (form.allowPartialAttendance ? form.pricePerChildWeek : null),
    pricePerBabyWeek: open ? (ed.pricePerBabyWeek ?? null)
      : (form.allowPartialAttendance ? form.pricePerBabyWeek : null),
    weekendStartDate: open ? (ed.weekendStartDate ?? null)
      : (form.allowWeekendVisit && form.weekendStartDate ? formatDateToIso(form.weekendStartDate) : null),
    weekendEndDate: open ? (ed.weekendEndDate ?? null)
      : (form.allowWeekendVisit && form.weekendEndDate ? formatDateToIso(form.weekendEndDate) : null),
    pricePerAdultWeekend: open ? (ed.pricePerAdultWeekend ?? null)
      : (form.allowWeekendVisit ? form.pricePerAdultWeekend : null),
    pricePerChildWeekend: open ? (ed.pricePerChildWeekend ?? null)
      : (form.allowWeekendVisit ? form.pricePerChildWeekend : null),
    pricePerBabyWeekend: open ? (ed.pricePerBabyWeekend ?? null)
      : (form.allowWeekendVisit ? form.pricePerBabyWeekend : null),
    maxWeekendCapacity: open ? (ed.maxWeekendCapacity ?? null)
      : (form.allowWeekendVisit ? (form.maxWeekendCapacity || null) : null),
    firstPaymentDeadline: form.firstPaymentDeadline ? formatDateToIso(form.firstPaymentDeadline) : null,
    secondPaymentDeadline: form.secondPaymentDeadline ? formatDateToIso(form.secondPaymentDeadline) : null
  }

  const result = await updateEdition(ed.id, request)
  saving.value = false

  if (result) {
    edition.value = result
    isEditing.value = false
    toast.add({ severity: 'success', summary: 'Éxito', detail: 'Edición actualizada correctamente', life: 3000 })
  } else {
    toast.add({ severity: 'error', summary: 'Error', detail: error.value || 'Error al actualizar', life: 5000 })
  }
}
```

#### 2.5 Template — Inline Edit within Tabbed Layout

The edit mode integrates into the tabbed layout from Step 1. Each `TabPanel` uses `v-if="!isEditing"` / `v-else` to toggle between read and edit content within the same panel.

**Header area** — Add Edit button next to title:

```html
<div class="mb-6 flex items-center justify-between">
  <div>
    <h1 class="text-3xl font-bold text-gray-900">
      Edición {{ edition.year }}
      <span v-if="edition.name"> — {{ edition.name }}</span>
    </h1>
    <p class="mt-1 text-gray-500">{{ edition.location }}</p>
  </div>
  <div class="flex gap-2">
    <Button
      v-if="canEdit && !isEditing"
      label="Editar"
      icon="pi pi-pencil"
      outlined
      data-testid="edit-edition-btn"
      @click="startEditing"
    />
    <template v-if="isEditing">
      <Button label="Cancelar" text :disabled="saving" @click="cancelEditing" />
      <Button label="Guardar" :loading="saving" :disabled="saving" data-testid="save-edition-btn" @click="handleSave" />
    </template>
  </div>
</div>
```

**Info message for Open editions** (above the tabs, only in edit mode):

```html
<Message v-if="isEditing && isOpenEdition" severity="info" :closable="false" class="mb-4">
  Esta edición está abierta para inscripciones. Solo se pueden modificar la capacidad,
  las notas, la descripción y las fechas de pago.
</Message>
```

**API error** (above the tabs):

```html
<Message v-if="isEditing && error" severity="error" :closable="false" class="mb-4">
  {{ error }}
</Message>
```

**Within each TabPanel** — Read/Edit toggle. Example for Tab 0 (General):

```html
<TabPanel value="0">
  <div class="rounded-lg border border-gray-200 bg-white p-6">
    <h2 class="mb-4 text-lg font-semibold text-gray-900">Información General</h2>

    <!-- Read mode -->
    <div v-if="!isEditing" class="space-y-2 text-sm">
      <div class="flex justify-between">
        <span class="text-gray-600">Estado:</span>
        <CampEditionStatusBadge :status="edition.status" size="sm" />
      </div>
      <!-- year, dates, capacity rows -->
    </div>

    <!-- Edit mode -->
    <div v-else class="space-y-4">
      <div class="grid grid-cols-1 gap-4 sm:grid-cols-2">
        <div class="flex flex-col gap-1">
          <label class="text-sm font-medium text-gray-700">Fecha de inicio</label>
          <DateInput v-model="form.startDate" :disabled="isOpenEdition" />
          <span v-if="errors.startDate" class="text-xs text-red-600">{{ errors.startDate }}</span>
        </div>
        <div class="flex flex-col gap-1">
          <label class="text-sm font-medium text-gray-700">Fecha de fin</label>
          <DateInput v-model="form.endDate" :disabled="isOpenEdition" />
          <span v-if="errors.endDate" class="text-xs text-red-600">{{ errors.endDate }}</span>
        </div>
      </div>
      <div class="flex flex-col gap-1">
        <label class="text-sm font-medium text-gray-700">Capacidad máxima</label>
        <InputNumber v-model="form.maxCapacity" :min="1" :use-grouping="false" placeholder="Sin límite" class="w-full" />
        <span v-if="errors.maxCapacity" class="text-xs text-red-600">{{ errors.maxCapacity }}</span>
      </div>
    </div>
  </div>
</TabPanel>
```

Apply the same read/edit toggle pattern inside each TabPanel:

- **Tab 1 (Pricing)**: Read shows formatted currency; edit shows InputNumber fields with `:disabled="isOpenEdition"`
- **Tab 2 (Partial Attendance)**: Read shows "No configurado" or values; edit shows ToggleSwitch + fields with `:disabled="isOpenEdition"`
- **Tab 3 (Weekend Visits)**: Same toggle + conditional fields pattern
- **Tab 4 (Age Ranges)**: Same toggle + conditional fields pattern
- **Tab 5 (Payment Deadlines)**: Read shows dates or "Automáticas"; edit always shows DateInput fields (always editable, even for Open)
- **Tab 6 (Notes & Description)**: Read shows content or "Sin notas/descripción"; edit shows Textarea fields (always editable)
- **Tab 7 (Accommodations)**: `CampEditionAccommodationsPanel` — no edit mode (has its own CRUD). Only visible for Board/Admin.
- **Tab 8 (Extras)**: `CampEditionExtrasList` — no edit mode (has its own CRUD).

**Important**: In edit mode, Tabs 2-4 (optional sections) show a ToggleSwitch at the top of the panel to enable/disable the feature, and the sub-fields appear below when enabled. In read mode they show "No configurado" if the feature is not active.

#### 2.6 Auto-enter edit mode from query param (optional but recommended)

Check for `?edit=true` on mount so the editions list can link directly to edit mode:
```typescript
onMounted(async () => {
  edition.value = await getEditionById(route.params.id as string)
  if (edition.value && route.query.edit === 'true' && canEdit.value) {
    startEditing()
    router.replace({ query: {} }) // Clean up URL
  }
})
```

---

### Step 3: Update CampEditionsPage — Remove Modal, Navigate to Detail

- **File**: `frontend/src/views/camps/CampEditionsPage.vue`
- **Action**: Replace modal-based editing with navigation to detail page
- **Implementation Steps**:
  1. Remove `CampEditionUpdateDialog` import (line 17)
  2. Remove `showEditDialog` ref (line 49)
  3. Update `handleEdit` to navigate:
     ```typescript
     const handleEdit = (edition: CampEdition) => {
       router.push({ name: 'camp-edition-detail', params: { id: edition.id }, query: { edit: 'true' } })
     }
     ```
  4. Remove `<CampEditionUpdateDialog>` from template (lines 246-247)
  5. Remove `handleEditionSaved` callback (lines 135-143)
  6. Keep `selectedEdition` ref only if needed for `CampEditionStatusDialog`

---

### Step 4: Delete CampEditionUpdateDialog

- **File**: `frontend/src/components/camps/CampEditionUpdateDialog.vue`
- **Action**: Delete the file
- **Pre-check**: Verify no imports remain (only `CampEditionsPage.vue` used it, updated in Step 3)

---

### Step 5: Update Cypress E2E Tests

- **File**: `frontend/cypress/e2e/camps/camp-editions.cy.ts`
- **Action**: Update tests for inline-edit flow
- **Implementation Steps**:
  1. Update "edit" test to expect navigation instead of modal:
     ```typescript
     it('should navigate to detail page in edit mode when clicking edit', () => {
       cy.get('[data-testid="edit-edition-btn"]').first().click()
       cy.url().should('include', '/camps/editions/')
     })
     ```
  2. Update save test:
     ```typescript
     it('should save edition changes inline', () => {
       cy.intercept('GET', '/api/camps/editions/*', { fixture: 'edition.json' }).as('getEdition')
       cy.intercept('PUT', '/api/camps/editions/*', { fixture: 'edition-updated.json' }).as('putEdition')
       // Navigate to detail page
       cy.visit('/camps/editions/test-id?edit=true')
       cy.wait('@getEdition')
       cy.get('[data-testid="save-edition-btn"]').click()
       cy.wait('@putEdition')
       cy.contains('Edición actualizada correctamente').should('be.visible')
     })
     ```
  3. Remove references to `edition-dialog` testid
  4. Ensure test fixtures include all fields (partial attendance, weekend, notes, etc.)

---

### Step 6: Update Technical Documentation

- **Action**: Review and update documentation for changes made
- **Implementation Steps**:
  1. Review all code changes (expanded detail page, removed dialog component)
  2. No API changes — no API spec updates needed
  3. If routing documentation exists in `ai-specs/specs/`, note that no new routes were added
  4. Update any references to the modal editing pattern in frontend docs if applicable

## Implementation Order

1. **Step 0**: Create feature branch
2. **Step 1**: Expand detail page with all missing read-mode fields
3. **Step 2**: Add inline edit mode (form state, validation, save, template)
4. **Step 3**: Update `CampEditionsPage.vue` (remove modal, navigate to detail)
5. **Step 4**: Delete `CampEditionUpdateDialog.vue`
6. **Step 5**: Update Cypress E2E tests
7. **Step 6**: Update technical documentation

## Testing Checklist

### E2E Tests (Cypress)
- [ ] Detail page shows all edition fields in read mode
- [ ] Edit button visible for Board/Admin, not for Member
- [ ] Edit button not shown for Closed/Completed editions
- [ ] Clicking "Edit" enters inline edit mode with fields populated
- [ ] All date fields show correct values (no timezone shift)
- [ ] Notes field loads existing content
- [ ] Open edition: restricted fields disabled, allowed fields editable
- [ ] Save submits form, shows toast, returns to read mode
- [ ] Cancel exits edit mode without saving
- [ ] Edition list "edit" icon navigates to detail page in edit mode

### Manual Verification
- [ ] Test with Open edition: can save notes/description/capacity/deadlines
- [ ] Test with Draft edition: can save all fields
- [ ] Date fields correct in both UTC+ and UTC- timezones
- [ ] Partial attendance and weekend sections can be toggled on/off
- [ ] Responsive layout works on mobile

## Error Handling Patterns

- **Loading**: `ProgressSpinner` while `getEditionById` loads (existing)
- **Not found**: "Edición no encontrada" message (existing)
- **API errors**: Toast with `error.value` message on save failure
- **Validation errors**: Inline `<span class="text-xs text-red-600">` below each field
- **Save state**: Separate `saving` ref to avoid conflicting with initial `loading`

## UI/UX Considerations

- **Vertical sidebar tabs**: PrimeVue `Tabs` organizes 9 sections in a left sidebar (`lg:` and up). Users click a section to view/edit it without scrolling.
- **Responsive**: On mobile (`< lg`), tabs display horizontally at the top with horizontal scroll overflow. Content stacks below.
- **Inline edit pattern**: Follows `ProfilePage.vue` — toggle between read and edit within the same TabPanel.
- **Save/Cancel in header**: Action buttons are in the header (always visible), not at the bottom of a long page, since the sidebar means only one section is visible at a time.
- **Card layout**: Each TabPanel content wrapped in `rounded-lg border border-gray-200 bg-white p-6`.
- **Responsive grids**: `grid-cols-1 sm:grid-cols-2` for dates/deadlines, `sm:grid-cols-3` for pricing.
- **Disabled fields**: `:disabled="isOpenEdition"` with PrimeVue's native grayed-out styling.
- **Info message**: PrimeVue `Message severity="info"` for Open edition restrictions, shown above the tabs.
- **Empty sections**: In read mode, unconfigured sections show "No configurado" / "Automáticas" messages. In edit mode, they show ToggleSwitches to enable features.

## Dependencies

- No new npm packages
- **PrimeVue**: Tabs, TabList, Tab, TabPanels, TabPanel (already used in `PaymentsAdminPanel.vue`, `AuthContainer.vue`), Button, InputNumber, Textarea, ToggleSwitch, Message, Toast
- **Project components**: Container, DateInput, CampEditionStatusBadge, CampEditionAccommodationsPanel, CampEditionExtrasList (all existing)
- **Composables**: `useCampEditions` (existing)
- **Utilities**: `parseDateLocal`, `formatDateLocal` (existing)

## Notes

- All code and comments in **English**; UI labels remain in Spanish (existing pattern)
- No `any` types — use proper TypeScript interfaces
- The `CampEdition` type has a `notes?: string` field but the old dialog hardcoded it to `''` — confirmed bug, fixed in `startEditing()`
- Backend requires `startDate` and `endDate` in the update request even for Open editions — must send original values
- Payment deadline dates should use `formatDateLocal()` for consistency
- The detail page already loads data via `getEditionById()` — this naturally solves the "data from list endpoint" bug

## Next Steps After Implementation

1. Manual QA with Open edition to verify all bugs are fixed
2. Verify no regressions for Draft/Proposed edition editing
3. Merge PR and clean up feature branch
