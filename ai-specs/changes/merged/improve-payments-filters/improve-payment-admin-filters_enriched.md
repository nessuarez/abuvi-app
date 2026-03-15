# US: Improve Payment Admin Page - Full-Width Layout & Installment Period Filter

## Summary

Enhance the payment management admin page (`PaymentsAllList.vue`) to maximize screen usage for desktop environments, eliminate horizontal scrolling, and add a new filter by payment period (installment number) that replaces or alternates with the existing date range filters.

## Context & Motivation

The admin panel is designed for desktop use, but the `Container` component constrains content to `max-w-screen-xl` (1280px). Payment tables with many columns produce horizontal scroll. Administrators need to filter payments by installment period (1st, 2nd, 3rd+ payment) rather than by arbitrary date ranges, since payment timing is structured around installment numbers.

---

## Scope of Changes

### 1. Full-Width Admin Layout

**Goal:** Admin pages should use the full available screen width.

**Files to modify:**
- [AdminPage.vue](frontend/src/views/AdminPage.vue) — Change `<Container>` to use `maxWidth="full"` prop instead of the default `xl`.

**Details:**
- Change `<Container>` to `<Container maxWidth="full">` at line 12
- This leverages the existing `Container` component's `full` option (`max-w-full`)
- No changes needed to `Container.vue` itself

---

### 2. Installment Period Filter (New)

**Goal:** Allow admins to filter payments by installment number (Plazo 1, Plazo 2, Plazo 3+).

#### 2.1 Backend — Add `InstallmentNumber` filter parameter

**Files to modify:**
- [PaymentsModels.cs](src/Abuvi.API/Features/Payments/PaymentsModels.cs) — Add `int? InstallmentNumber` parameter to `PaymentFilterRequest`
- [PaymentsRepository.cs](src/Abuvi.API/Features/Payments/PaymentsRepository.cs) — Add filter clause in `GetFilteredAsync`:
  - If `InstallmentNumber == 3`, filter `p.InstallmentNumber >= 3` (to capture all "3rd or later" payments)
  - Otherwise, exact match `p.InstallmentNumber == filter.InstallmentNumber`

**`PaymentFilterRequest` updated signature:**
```csharp
public record PaymentFilterRequest(
    PaymentStatus? Status = null,
    Guid? CampEditionId = null,
    int? InstallmentNumber = null,  // NEW
    DateTime? FromDate = null,
    DateTime? ToDate = null,
    int Page = 1,
    int PageSize = 20
);
```

**Repository filter logic (add after CampEditionId filter):**
```csharp
if (filter.InstallmentNumber.HasValue)
{
    if (filter.InstallmentNumber.Value >= 3)
        query = query.Where(p => p.InstallmentNumber >= 3);
    else
        query = query.Where(p => p.InstallmentNumber == filter.InstallmentNumber.Value);
}
```

**Endpoint** — No changes needed to [PaymentsEndpoints.cs](src/Abuvi.API/Features/Payments/PaymentsEndpoints.cs) beyond the automatic binding of the new query parameter `InstallmentNumber`.

#### 2.2 Frontend — Types

**File to modify:** [payment.ts](frontend/src/types/payment.ts)

Add `installmentNumber` to `PaymentFilterParams`:
```typescript
export interface PaymentFilterParams {
  status?: PaymentStatus
  campEditionId?: string
  installmentNumber?: number  // NEW
  fromDate?: string
  toDate?: string
  page?: number
  pageSize?: number
}
```

#### 2.3 Frontend — Composable

**File to modify:** [usePayments.ts](frontend/src/composables/usePayments.ts)

In `getAllPayments`, add the new parameter to the `URLSearchParams` construction:
```typescript
if (filter.installmentNumber) params.append('InstallmentNumber', String(filter.installmentNumber))
```

#### 2.4 Frontend — Filter UI in PaymentsAllList.vue

**File to modify:** [PaymentsAllList.vue](frontend/src/components/admin/PaymentsAllList.vue)

**New state:**
```typescript
const selectedInstallment = ref<number | null>(null)
const filterMode = ref<'installment' | 'dates'>('installment')
```

**Installment options:**
```typescript
const installmentOptions = [
  { label: 'Todos', value: null },
  { label: 'Plazo 1', value: 1 },
  { label: 'Plazo 2', value: 2 },
  { label: 'Plazo 3+', value: 3 }
]
```

**Filter mode toggle:** Add a `SelectButton` (PrimeVue) or simple button group allowing the user to choose between:
- "Por período" (installment filter visible, date filters hidden)
- "Por fechas" (date filters visible, installment filter hidden)

**Updated filter bar template (replace lines 165-204):**
```html
<div class="mb-4 flex flex-wrap items-end gap-3">
  <!-- Status filter (always visible) -->
  <div>
    <label class="mb-1 block text-xs font-medium text-gray-600">Estado</label>
    <Select v-model="selectedStatus" :options="statusOptions" ... />
  </div>

  <!-- Edition filter (always visible) -->
  <div>
    <label class="mb-1 block text-xs font-medium text-gray-600">Edición</label>
    <Select v-model="selectedEditionId" ... />
  </div>

  <!-- Filter mode toggle -->
  <div>
    <label class="mb-1 block text-xs font-medium text-gray-600">Filtrar por</label>
    <SelectButton v-model="filterMode" :options="filterModeOptions" option-label="label" option-value="value" />
  </div>

  <!-- Installment filter (visible when filterMode === 'installment') -->
  <div v-if="filterMode === 'installment'">
    <label class="mb-1 block text-xs font-medium text-gray-600">Período de pago</label>
    <Select v-model="selectedInstallment" :options="installmentOptions" option-label="label" option-value="value" placeholder="Todos" class="w-40" />
  </div>

  <!-- Date range filters (visible when filterMode === 'dates') -->
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

  <Button icon="pi pi-filter-slash" ... @click="resetFilters" />
</div>
```

**Updated `fetchPayments`:**
```typescript
const fetchPayments = async () => {
  const filter: PaymentFilterParams = { page: currentPage.value, pageSize }
  if (selectedStatus.value) filter.status = selectedStatus.value
  if (selectedEditionId.value) filter.campEditionId = selectedEditionId.value
  if (filterMode.value === 'installment' && selectedInstallment.value) {
    filter.installmentNumber = selectedInstallment.value
  }
  if (filterMode.value === 'dates') {
    if (dateFrom.value) filter.fromDate = toIsoDate(dateFrom.value)
    if (dateTo.value) filter.toDate = toIsoDate(dateTo.value)
  }
  // ... rest unchanged
}
```

**Updated `resetFilters`:** Also reset `selectedInstallment` and `filterMode`.

**Updated `watch`:** Add `selectedInstallment` and `filterMode` to the watched array. When `filterMode` changes, clear the values of the non-active filter group.

---

### 3. Table Layout — Eliminate Horizontal Scroll

**File to modify:** [PaymentsAllList.vue](frontend/src/components/admin/PaymentsAllList.vue)

**Changes:**
- Add `scrollable` and `scroll-height="flex"` props to `DataTable` if needed, but the primary fix is the wider container (step 1)
- Review column widths — with full-width layout, there should be enough room for all current columns without horizontal scroll
- If still needed, consider making columns like "Concepto" use `text-ellipsis` / `truncate` with a tooltip on hover, rather than expanding the table

---

## Acceptance Criteria

1. The admin panel uses the full available viewport width (no `max-w-screen-xl` constraint)
2. The payment table fits without horizontal scroll on standard desktop screens (1440px+)
3. A new "Período de pago" filter allows filtering by Plazo 1, Plazo 2, or Plazo 3+
4. A toggle allows switching between "filter by period" and "filter by dates" modes
5. When switching filter modes, the inactive filter values are cleared
6. The backend accepts and correctly applies the `InstallmentNumber` query parameter
7. `InstallmentNumber=3` returns payments with installment >= 3
8. The clear filters button resets all filters including the new ones

## Non-Functional Requirements

- **Performance:** The new filter uses an indexed column (`InstallmentNumber`) — no performance concern
- **Security:** No new authorization changes; existing admin-only middleware covers the endpoint
- **Backwards compatibility:** The new query parameter is optional with default `null` — existing API consumers are unaffected

## Testing

### Backend Unit Tests
- Test `GetFilteredAsync` with `InstallmentNumber = 1` returns only installment 1 payments
- Test `GetFilteredAsync` with `InstallmentNumber = 3` returns installment 3+ payments
- Test `GetFilteredAsync` with `InstallmentNumber = null` returns all payments (no filter)

### Frontend Tests
- Test that filter mode toggle shows/hides the correct filter group
- Test that changing filter mode clears inactive filter values
- Test that `fetchPayments` sends `InstallmentNumber` param when in installment mode
