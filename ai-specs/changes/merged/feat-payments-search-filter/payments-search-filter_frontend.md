# Frontend Implementation Plan: feat-payments-search-filter — Payments Admin Search Filter

## Overview

Add a debounced text search input to the admin payments list (`PaymentsAllList.vue`) so admins/board users can filter payments by family name or representative name. The change mirrors the existing pattern in `RegistrationsAdminPanel.vue` exactly — same debounce timing, same watcher pattern, same reset behavior. No new files, no Pinia store, no routing changes.

---

## Architecture Context

- **Components**: `frontend/src/components/admin/PaymentsAllList.vue` (modified)
- **Composables**: `frontend/src/composables/usePayments.ts` (modified)
- **Types**: `frontend/src/types/payment.ts` (modified)
- **Tests**: `frontend/src/components/admin/__tests__/PaymentsAllList.test.ts` (modified)
- **State management**: Local component state only (`ref<string>('')`)
- **No new routes, no Pinia store, no new files**

**Reference pattern**: `frontend/src/components/admin/RegistrationsAdminPanel.vue` — uses `useDebounceFn` from `@vueuse/core` with 300ms delay, `watch(searchQuery, debouncedSearch)`, and clears query in a reset function.

---

## Implementation Steps

### Step 0: Create Feature Branch

- **Action**: Create and switch to a new feature branch.
- **Branch name**: `feature/feat-payments-search-filter-frontend`
- **Steps**:
  1. `git checkout dev`
  2. `git pull origin dev`
  3. `git checkout -b feature/feat-payments-search-filter-frontend`
  4. `git branch` — verify you are on the new branch

---

### Step 1: Update TypeScript Interface

- **File**: `frontend/src/types/payment.ts`
- **Action**: Add `search?: string` to `PaymentFilterParams`
- **Implementation Steps**:
  1. Locate `PaymentFilterParams` interface (lines 72–80)
  2. Add `search?: string` after `toDate?`:
     ```ts
     export interface PaymentFilterParams {
       status?: PaymentStatus
       campEditionId?: string
       installmentNumber?: number
       fromDate?: string
       toDate?: string
       search?: string        // NEW
       page?: number
       pageSize?: number
     }
     ```
- **Notes**: Optional field with no default — skipped when not set, consistent with all other filter params.

---

### Step 2: Update Composable

- **File**: `frontend/src/composables/usePayments.ts`
- **Action**: Append the `Search` query param when `filter.search` is set
- **Implementation Steps**:
  1. Locate `getAllPayments` function, find the `URLSearchParams` building block
  2. After the existing `if (filter.toDate) params.append('ToDate', ...)` line, add:
     ```ts
     if (filter.search) params.append('Search', filter.search)
     ```
- **Notes**: Param name is `Search` (capital S) — must match the backend `[AsParameters]` binding on `PaymentFilterRequest.Search`.

---

### Step 3: Update PaymentsAllList.vue

- **File**: `frontend/src/components/admin/PaymentsAllList.vue`
- **Action**: Add search ref, debounced watcher, reset support, and search input UI

#### 3a — Script setup

1. **Add import** at the top of the script block (after existing Vue imports):
   ```ts
   import { useDebounceFn } from '@vueuse/core'
   ```
   (`InputText` is already imported at line 10 — no second import needed.)

2. **Add `searchQuery` ref** after the existing filter refs (`currentPage`, `pageSize`), before the `// Edit manual payment dialog` comment:
   ```ts
   const searchQuery = ref('')
   ```

3. **Include in `fetchPayments()`** — inside the filter object block, after the `dateTo` guard:
   ```ts
   if (searchQuery.value) filter.search = searchQuery.value
   ```

4. **Add debounced watcher** — after the existing `watch([selectedStatus, selectedEditionId, ...])` block:
   ```ts
   const debouncedSearch = useDebounceFn(() => {
     currentPage.value = 1
     fetchPayments()
   }, 300)
   watch(searchQuery, debouncedSearch)
   ```

5. **Reset in `resetFilters()`** — add `searchQuery.value = ''` before the `fetchPayments()` call at the end of the function.

#### 3b — Template

Add a new `<div>` block **before** the clear-filters `<Button>` (inside the existing `flex flex-wrap items-end gap-3` container):

```html
<div>
  <label class="mb-1 block text-xs font-medium text-gray-600">Familia / Representante</label>
  <span class="p-input-icon-left">
    <i class="pi pi-search" />
    <InputText
      v-model="searchQuery"
      placeholder="Buscar familia o representante..."
      class="w-64"
    />
  </span>
</div>
```

- **Dependencies**: `@vueuse/core` already installed; `useDebounceFn` already used elsewhere in the project.

---

### Step 4: Add Unit Tests

- **File**: `frontend/src/components/admin/__tests__/PaymentsAllList.test.ts`
- **Action**: Add 3 new `it()` cases inside the existing `describe('PaymentsAllList')` block

**Test 1 — search input renders:**
```ts
it('renders search input for family/representative filter', () => {
  const wrapper = mountComponent()
  const input = wrapper.findComponent({ name: 'InputText' })
  expect(input.exists()).toBe(true)
})
```

**Test 2 — search param sent when set:**
```ts
it('includes search in API call when searchQuery has a value', async () => {
  const wrapper = mountComponent()
  vi.clearAllMocks()
  mockGetAllPayments.mockResolvedValue({ items: [], totalCount: 0, page: 1, pageSize: 20 })

  // Set searchQuery directly and call fetchPayments (bypasses debounce in unit test)
  ;(wrapper.vm as any).searchQuery = 'garcia'
  await (wrapper.vm as any).fetchPayments()

  expect(mockGetAllPayments).toHaveBeenCalledWith(
    expect.objectContaining({ search: 'garcia' })
  )
})
```

**Test 3 — resetFilters clears search:**
```ts
it('clears searchQuery when resetFilters is called', async () => {
  const wrapper = mountComponent()
  ;(wrapper.vm as any).searchQuery = 'test'
  ;(wrapper.vm as any).resetFilters()
  expect((wrapper.vm as any).searchQuery).toBe('')
})
```

- **Notes**: Tests call `fetchPayments()` directly to bypass the 300ms debounce — no need for `vi.useFakeTimers()`. This matches the existing test style in this file.

---

### Step 5: Update Technical Documentation

- **Action**: No documentation file changes required for this ticket.
  - `api-endpoints.md` — the `Search` param is documented in the backend plan.
  - `frontend-standards.mdc` — no new patterns introduced; existing debounce + watcher pattern already in the codebase.

---

## Implementation Order

1. Step 0 — Create feature branch
2. Step 1 — Add `search` to `PaymentFilterParams`
3. Step 2 — Update `usePayments.ts` composable
4. Step 3a — Update `PaymentsAllList.vue` script
5. Step 3b — Update `PaymentsAllList.vue` template
6. Step 4 — Add unit tests
7. Step 5 — Verify no doc updates needed

---

## Testing Checklist

- [ ] All 4 existing tests in `PaymentsAllList.test.ts` still pass
- [ ] 3 new tests pass
- [ ] `npm run test` exits 0 from `frontend/`
- [ ] Dev server: search input appears in the filters row next to existing filters
- [ ] Typing a family name filters the table (network tab shows one request after 300ms)
- [ ] Typing a representative name also filters correctly
- [ ] "Limpiar filtros" clears the search input and refetches without `Search` param
- [ ] Search combines with status, edition, and installment/date filters
- [ ] Pagination resets to page 1 on each search change

---

## Error Handling Patterns

No new error states — search is an optional filter. If the API call fails, the existing `error` ref in `usePayments` is set and the `<Message severity="error">` in the template already shows it.

---

## UI/UX Considerations

- **Placement**: Before the clear-filters button — consistent with registrations panel layout
- **Label**: `"Familia / Representante"` — matches label style of other filters in the row
- **Width**: `w-64` (16rem) — same as registrations search input
- **Icon**: `pi-search` inside `p-input-icon-left` span — identical to registrations panel
- **Debounce**: 300ms — identical to registrations panel, prevents excessive API calls
- **Accessibility**: `InputText` renders as `<input>` — keyboard navigable natively

---

## Dependencies

- `@vueuse/core` — already installed; `useDebounceFn` is already used in the project
- `primevue/inputtext` — already imported in `PaymentsAllList.vue`
- No new npm packages required

---

## Notes

- `InputText` is **already imported** at line 10 of `PaymentsAllList.vue` — do NOT add a duplicate import
- Backend param name is `Search` (capital S); `params.append('Search', filter.search)` must match exactly
- `searchQuery` is `ref<string>('')` — the empty string guard (`if (searchQuery.value)`) correctly omits the param when blank
- All code must be in English

---

## Next Steps After Implementation

- Merge frontend branch → `dev` after backend branch (`feature/feat-payments-search-filter-backend`) is merged and deployed to the dev environment for end-to-end testing.

---

## Implementation Verification

- [ ] **Code Quality**: No TypeScript errors, no `any`, `<script setup lang="ts">` used
- [ ] **Functionality**: Search input renders, filters server-side, debounces correctly, resets properly
- [ ] **Testing**: Vitest — all new + existing tests pass; no regressions
- [ ] **Integration**: `?Search=term` appears correctly in network requests when typing
- [ ] **Documentation**: No additional doc updates required
