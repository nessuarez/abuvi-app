# Frontend Implementation Plan: improve-payment-admin-filters — Full-Width Layout & Installment Period Filter

## Overview

Enhance the payment admin page to maximize desktop screen usage and add an installment period filter that alternates with the existing date range filters. Uses Vue 3 Composition API, PrimeVue components (`SelectButton`, `Select`, `DataTable`), and Tailwind CSS utility classes.

## Architecture Context

- **Components involved:**
  - `frontend/src/views/AdminPage.vue` — Layout container width
  - `frontend/src/components/admin/PaymentsAllList.vue` — Filter bar and table
- **Composable involved:**
  - `frontend/src/composables/usePayments.ts` — API call with filter params
- **Types involved:**
  - `frontend/src/types/payment.ts` — `PaymentFilterParams` interface
- **Routing:** No changes
- **State management:** Local component state only (no Pinia store needed)

## Implementation Steps

### Step 0: Create Feature Branch

- **Action**: Create and switch to a new feature branch
- **Branch Naming**: `feature/improve-payment-admin-filters-frontend`
- **Implementation Steps**:
  1. Ensure on `dev` branch: `git checkout dev`
  2. Pull latest: `git pull origin dev`
  3. Create branch: `git checkout -b feature/improve-payment-admin-filters-frontend`
  4. Verify: `git branch`

### Step 1: Widen Admin Layout

- **File**: `frontend/src/views/AdminPage.vue`
- **Action**: Change the `Container` component to use full width for desktop admin usage
- **Implementation Steps**:
  1. Change `<Container>` (line 12) to `<Container maxWidth="full">`
- **Implementation Notes**:
  - The `Container` component already supports a `maxWidth="full"` prop that applies `max-w-full`
  - This is a one-line change, no import changes needed
  - This affects all admin sub-pages, which is the desired behavior per the ticket

### Step 2: Update `PaymentFilterParams` Type

- **File**: `frontend/src/types/payment.ts`
- **Action**: Add `installmentNumber` optional property to `PaymentFilterParams`
- **Implementation Steps**:
  1. Add `installmentNumber?: number` after `campEditionId` in the `PaymentFilterParams` interface
- **Updated interface**:
  ```typescript
  export interface PaymentFilterParams {
    status?: PaymentStatus
    campEditionId?: string
    installmentNumber?: number
    fromDate?: string
    toDate?: string
    page?: number
    pageSize?: number
  }
  ```

### Step 3: Update `usePayments` Composable

- **File**: `frontend/src/composables/usePayments.ts`
- **Action**: Pass the new `installmentNumber` parameter to the API
- **Implementation Steps**:
  1. In the `getAllPayments` function, add after the `CampEditionId` param append (line 133):
     ```typescript
     if (filter.installmentNumber) params.append('InstallmentNumber', String(filter.installmentNumber))
     ```
- **Implementation Notes**:
  - The backend binds `InstallmentNumber` from query string via `[AsParameters]`
  - When `installmentNumber` is `undefined` or `null`, no param is sent — backwards compatible

### Step 4: Update PaymentsAllList Filter UI

- **File**: `frontend/src/components/admin/PaymentsAllList.vue`
- **Action**: Add installment period filter with a mode toggle that alternates between period and date range filters
- **Dependencies**: Add `import SelectButton from 'primevue/selectbutton'`

#### 4a: Script changes

- **New state variables** (add after existing filter refs, ~line 37):
  ```typescript
  const selectedInstallment = ref<number | null>(null)
  const filterMode = ref<'installment' | 'dates'>('installment')
  ```

- **New options** (add after `statusOptions`):
  ```typescript
  const installmentOptions = [
    { label: 'Todos', value: null },
    { label: 'Plazo 1', value: 1 },
    { label: 'Plazo 2', value: 2 },
    { label: 'Plazo 3+', value: 3 }
  ]

  const filterModeOptions = [
    { label: 'Por período', value: 'installment' },
    { label: 'Por fechas', value: 'dates' }
  ]
  ```

- **Update `fetchPayments`** (lines 77-92): Conditionally apply either installment or date filter based on `filterMode`:
  ```typescript
  const fetchPayments = async () => {
    const filter: PaymentFilterParams = {
      page: currentPage.value,
      pageSize
    }
    if (selectedStatus.value) filter.status = selectedStatus.value
    if (selectedEditionId.value) filter.campEditionId = selectedEditionId.value
    if (filterMode.value === 'installment' && selectedInstallment.value) {
      filter.installmentNumber = selectedInstallment.value
    }
    if (filterMode.value === 'dates') {
      if (dateFrom.value) filter.fromDate = toIsoDate(dateFrom.value)
      if (dateTo.value) filter.toDate = toIsoDate(dateTo.value)
    }

    const result = await getAllPayments(filter)
    if (result) {
      payments.value = result.items
      totalRecords.value = result.totalCount
    }
  }
  ```

- **Update `resetFilters`** (lines 99-106): Also reset `selectedInstallment` and `filterMode`:
  ```typescript
  const resetFilters = () => {
    selectedStatus.value = null
    selectedEditionId.value = null
    selectedInstallment.value = null
    filterMode.value = 'installment'
    dateFrom.value = null
    dateTo.value = null
    currentPage.value = 1
    fetchPayments()
  }
  ```

- **Update `watch`** (line 151): Add `selectedInstallment` and `filterMode` to the watched array. Add a separate watcher for `filterMode` that clears the inactive filter group:
  ```typescript
  watch([selectedStatus, selectedEditionId, selectedInstallment, dateFrom, dateTo], () => {
    currentPage.value = 1
    fetchPayments()
  })

  watch(filterMode, (newMode) => {
    if (newMode === 'installment') {
      dateFrom.value = null
      dateTo.value = null
    } else {
      selectedInstallment.value = null
    }
  })
  ```
  - **Note**: The existing watcher already resets `currentPage` and calls `fetchPayments`. Adding `selectedInstallment` triggers it. The `filterMode` watcher clears inactive values, which in turn triggers the main watcher → `fetchPayments` is called automatically.

#### 4b: Template changes

- **Replace the filter bar** (lines 165-204) with the updated version including the mode toggle:

  ```html
  <!-- Filters -->
  <div class="mb-4 flex flex-wrap items-end gap-3">
    <div>
      <label class="mb-1 block text-xs font-medium text-gray-600">Estado</label>
      <Select
        v-model="selectedStatus"
        :options="statusOptions"
        option-label="label"
        option-value="value"
        placeholder="Todos"
        class="w-40"
      />
    </div>
    <div>
      <label class="mb-1 block text-xs font-medium text-gray-600">Edicion</label>
      <Select
        v-model="selectedEditionId"
        :options="[{ id: null, label: 'Todas' }, ...allEditions.map((e) => ({ id: e.id, label: `${e.name ?? 'Campamento'} ${e.year}` }))]"
        option-label="label"
        option-value="id"
        placeholder="Todas"
        class="w-48"
      />
    </div>
    <div>
      <label class="mb-1 block text-xs font-medium text-gray-600">Filtrar por</label>
      <SelectButton
        v-model="filterMode"
        :options="filterModeOptions"
        option-label="label"
        option-value="value"
      />
    </div>
    <div v-if="filterMode === 'installment'">
      <label class="mb-1 block text-xs font-medium text-gray-600">Período de pago</label>
      <Select
        v-model="selectedInstallment"
        :options="installmentOptions"
        option-label="label"
        option-value="value"
        placeholder="Todos"
        class="w-40"
      />
    </div>
    <template v-if="filterMode === 'dates'">
      <div>
        <label class="mb-1 block text-xs font-medium text-gray-600">Desde</label>
        <DateInput v-model="dateFrom" placeholder="Desde" :show-calendar="false" />
      </div>
      <div>
        <label class="mb-1 block text-xs font-medium text-gray-600">Hasta</label>
        <DateInput v-model="dateTo" placeholder="Hasta" :show-calendar="false" />
      </div>
    </template>
    <Button
      icon="pi pi-filter-slash"
      severity="secondary"
      text
      rounded
      aria-label="Limpiar filtros"
      @click="resetFilters"
    />
  </div>
  ```

### Step 5: Write Vitest Unit Tests

- **File**: `frontend/src/components/admin/__tests__/PaymentsAllList.test.ts` (new file)
- **Action**: Write unit tests for the filter behavior
- **Dependencies**: `vitest`, `@vue/test-utils`, `primevue`
- **Test Cases**:

  1. **`renders installment filter by default (filterMode = installment)`**
     - Mount component with mocked `usePayments` and `useCampEditions`
     - Assert: Installment `Select` is visible, date `DateInput` fields are not rendered

  2. **`hides installment filter and shows date filters when filterMode = dates`**
     - Set `filterMode` to `'dates'`
     - Assert: Date inputs visible, installment select not rendered

  3. **`clears date values when switching to installment mode`**
     - Set `filterMode` to `'dates'`, set `dateFrom` and `dateTo`
     - Switch `filterMode` to `'installment'`
     - Assert: `dateFrom` and `dateTo` are `null`

  4. **`clears installment value when switching to dates mode`**
     - Set `selectedInstallment` to `2`
     - Switch `filterMode` to `'dates'`
     - Assert: `selectedInstallment` is `null`

  5. **`resetFilters resets all filters including installment and filterMode`**
     - Set various filters to non-default values
     - Call `resetFilters`
     - Assert: All values reset, `filterMode` back to `'installment'`

  6. **`fetchPayments sends installmentNumber when in installment mode`**
     - Set `filterMode` to `'installment'` and `selectedInstallment` to `1`
     - Trigger fetch
     - Assert: `getAllPayments` called with `{ installmentNumber: 1, ... }`

  7. **`fetchPayments sends date params when in dates mode`**
     - Set `filterMode` to `'dates'`, set date values
     - Trigger fetch
     - Assert: `getAllPayments` called with `fromDate` and `toDate`, no `installmentNumber`

### Step 6: Update Technical Documentation

- **Action**: Review and update documentation
- **Implementation Steps**:
  1. No routing changes — no router documentation needed
  2. No new dependencies — `SelectButton` from `primevue/selectbutton` is already used in the project (`FamilyUnitsAdminPanel.vue`)
  3. If `api-spec.yml` documents frontend query params or filter options, update accordingly
- **Notes**: Mandatory step before marking implementation complete

## Implementation Order

1. Step 0: Create feature branch
2. Step 1: Widen admin layout (`AdminPage.vue`)
3. Step 2: Update `PaymentFilterParams` type
4. Step 3: Update `usePayments` composable
5. Step 4: Update `PaymentsAllList.vue` (script + template)
6. Step 5: Write Vitest unit tests
7. Step 6: Update technical documentation

## Testing Checklist

- [ ] Admin page renders full-width (no `max-w-screen-xl` visible constraint)
- [ ] Payment table has no horizontal scroll on 1440px+ screens
- [ ] Filter bar shows installment period filter by default
- [ ] `SelectButton` toggles between "Por período" and "Por fechas"
- [ ] Selecting "Plazo 1" filters to only installment 1 payments
- [ ] Selecting "Plazo 3+" filters to installment 3 and higher
- [ ] Switching to "Por fechas" hides installment filter, shows date inputs
- [ ] Switching back to "Por período" clears date values
- [ ] Clear filters button resets everything including mode and installment
- [ ] Pagination works correctly with installment filter active
- [ ] All existing filters (status, edition) still work alongside the new filter

## Error Handling Patterns

- No new error states introduced — the filter simply adds a query parameter
- Existing `loading` and `error` refs from `usePayments` handle API errors
- PrimeVue `Message` component already displays errors at the bottom of the table

## UI/UX Considerations

- **PrimeVue `SelectButton`**: Used for the filter mode toggle — consistent with `FamilyUnitsAdminPanel.vue` which uses the same pattern for membership filtering
- **Filter layout**: `flex-wrap` ensures the filter bar wraps gracefully on narrower admin viewports
- **Default mode**: `'installment'` is the default since the user specified this is the more common use case
- **Accessibility**: `SelectButton` has built-in keyboard navigation; the installment `Select` uses standard ARIA patterns from PrimeVue
- **No mobile considerations**: The admin page is explicitly desktop-only per the ticket requirements

## Dependencies

- **No new npm packages**
- **PrimeVue components used**:
  - `SelectButton` from `primevue/selectbutton` (already in project)
  - `Select` from `primevue/select` (already imported)
  - `DataTable`, `Column` (unchanged)
- **Existing composables**: `usePayments`, `useCampEditions` (both already imported)

## Notes

- The admin layout width change (`maxWidth="full"`) affects all admin sub-pages, not just payments. This is intentional per the ticket: "la página de administración se considere que estamos en entornos de escritorio y ampliar al máximo el uso de la pantalla."
- All code and variable names in English per `base-standards.mdc`
- UI labels remain in Spanish (matching existing convention for user-facing text in this app: "Estado", "Edicion", "Filtrar por", etc.)
- The `filterMode` watcher clearing inactive values prevents stale filter params from being sent to the API

## Next Steps After Implementation

- Verify backend `InstallmentNumber` filter is deployed (see `improve-payment-admin-filters_backend.md`)
- Manual QA on staging with real payment data across all three installment periods
- Confirm no visual regressions on other admin pages from the full-width layout change

## Implementation Verification

- [ ] **Code Quality**: TypeScript strict, no `any`, `<script setup lang="ts">` used
- [ ] **Functionality**: Filter mode toggle works, installment filter sends correct API param
- [ ] **Testing**: Vitest unit tests pass with expected coverage
- [ ] **Integration**: Composable correctly sends `InstallmentNumber` query parameter
- [ ] **Documentation**: Updated as needed
