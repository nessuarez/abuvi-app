# Frontend Implementation Plan: feat-membership-numbers-admin-filter

## Overview

Add membership status filter and family number column to the admin family units panel, update TypeScript types to include `familyNumber` and `memberNumber` fields from the backend, and add composable methods for editing these numbers. Built with Vue 3 Composition API, PrimeVue components, and Tailwind CSS.

## Architecture Context

### Components/composables involved

| File | Role |
|---|---|
| `frontend/src/types/family-unit.ts` | TypeScript interfaces for family unit responses |
| `frontend/src/types/membership.ts` | TypeScript interfaces for membership responses |
| `frontend/src/composables/useFamilyUnits.ts` | API composable for family unit operations |
| `frontend/src/composables/useMemberships.ts` | API composable for membership operations |
| `frontend/src/components/admin/FamilyUnitsAdminPanel.vue` | Admin panel with DataTable for family units |

### State management

- No Pinia store changes needed — all state is local within composables
- Filter state (`membershipStatus`) is local to `FamilyUnitsAdminPanel.vue`

### Routing

- No new routes needed

## Implementation Steps

### Step 0: Create Feature Branch

- **Action**: Create and switch to a new feature branch
- **Branch name**: `feature/feat-membership-numbers-admin-filter-frontend`
- **Implementation Steps**:
  1. `git checkout dev && git pull origin dev`
  2. `git checkout -b feature/feat-membership-numbers-admin-filter-frontend`
  3. `git branch` to verify

---

### Step 1: Update TypeScript Interfaces

#### Step 1a: Update `FamilyUnitResponse`

- **File**: `frontend/src/types/family-unit.ts`
- **Action**: Add `familyNumber` field to `FamilyUnitResponse` interface

**Add after `representativeUserId` (line 13):**

```typescript
familyNumber: number | null
```

**Result — `FamilyUnitResponse` interface (line 10):**

```typescript
export interface FamilyUnitResponse {
  id: string
  name: string
  representativeUserId: string
  familyNumber: number | null
  profilePhotoUrl: string | null
  createdAt: string
  updatedAt: string
  // Optional: populated in admin list endpoint (GET /api/family-units)
  representativeName?: string
  membersCount?: number
}
```

**Add new request interface (after `UpdateFamilyUnitRequest`, line 30):**

```typescript
export interface UpdateFamilyNumberRequest {
  familyNumber: number
}
```

#### Step 1b: Update `MembershipResponse`

- **File**: `frontend/src/types/membership.ts`
- **Action**: Add `memberNumber` field to `MembershipResponse` interface

**Add after `familyMemberId` (line 34):**

```typescript
memberNumber: number | null
```

**Add new request interface (after `PayFeeRequest`, line 50):**

```typescript
export interface UpdateMemberNumberRequest {
  memberNumber: number
}
```

---

### Step 2: Update Composables

#### Step 2a: Update `useFamilyUnits` composable

- **File**: `frontend/src/composables/useFamilyUnits.ts`

**2a.1 — Add `membershipStatus` parameter to `fetchAllFamilyUnits` (line 239):**

Update the params type to include `membershipStatus`:

```typescript
const fetchAllFamilyUnits = async (params: {
  page?: number
  pageSize?: number
  search?: string
  sortBy?: string
  sortOrder?: 'asc' | 'desc'
  membershipStatus?: 'all' | 'active' | 'none'
} = {}): Promise<void> => {
```

In the query params construction (after line 255), add:

```typescript
if (params.membershipStatus && params.membershipStatus !== 'all')
  queryParams.set('membershipStatus', params.membershipStatus)
```

**2a.2 — Add `updateFamilyNumber` method:**

Add new method before the `return` block (before line 410):

```typescript
/**
 * Update family number for a family unit (Admin/Board only)
 */
const updateFamilyNumber = async (
  familyUnitId: string,
  familyNumber: number
): Promise<FamilyUnitResponse | null> => {
  loading.value = true
  error.value = null
  try {
    const response = await api.put<ApiResponse<FamilyUnitResponse>>(
      `/family-units/${familyUnitId}/family-number`,
      { familyNumber }
    )
    return response.data.data
  } catch (err: unknown) {
    const apiErr = err as { response?: { data?: { error?: { message?: string } } } }
    error.value = apiErr?.response?.data?.error?.message || 'Error al actualizar el número de familia'
    return null
  } finally {
    loading.value = false
  }
}
```

**Add to the `return` object:**

```typescript
updateFamilyNumber,
```

**Add import for `UpdateFamilyNumberRequest`** (not strictly needed since we inline the object, but add the type import if the request interface is used).

#### Step 2b: Update `useMemberships` composable

- **File**: `frontend/src/composables/useMemberships.ts`

**Add `updateMemberNumber` method before the `return` block (before line 169):**

```typescript
/**
 * Update member number for a membership (Admin/Board only)
 */
const updateMemberNumber = async (
  membershipId: string,
  memberNumber: number
): Promise<MembershipResponse | null> => {
  loading.value = true
  error.value = null
  try {
    const response = await api.put<ApiResponse<MembershipResponse>>(
      `/memberships/${membershipId}/member-number`,
      { memberNumber }
    )
    membership.value = response.data.data
    return response.data.data
  } catch (err: any) {
    error.value = err.response?.data?.error?.message || 'Error al actualizar el número de socio/a'
    return null
  } finally {
    loading.value = false
  }
}
```

**Add to the `return` object:**

```typescript
updateMemberNumber,
```

---

### Step 3: Update `FamilyUnitsAdminPanel.vue`

- **File**: `frontend/src/components/admin/FamilyUnitsAdminPanel.vue`
- **Action**: Add membership status filter (SelectButton) and "Nº Familia" column

#### 3.1 — Add imports

Add `SelectButton` import (after line 11):

```typescript
import SelectButton from 'primevue/selectbutton'
```

#### 3.2 — Add filter state

Add after `searchQuery` ref (after line 18):

```typescript
const membershipFilter = ref<string>('all')
const membershipFilterOptions = [
  { label: 'Todas', value: 'all' },
  { label: 'Socias', value: 'active' },
  { label: 'No socias', value: 'none' }
]
```

#### 3.3 — Update `debouncedSearch` to include filter

Update the `debouncedSearch` function (line 20-22):

```typescript
const debouncedSearch = useDebounceFn((val: string) => {
  fetchAllFamilyUnits({ search: val, page: 1, membershipStatus: membershipFilter.value as 'all' | 'active' | 'none' })
}, 300)
```

#### 3.4 — Add watcher for filter changes

Add after the `watch(searchQuery, debouncedSearch)` (after line 24):

```typescript
watch(membershipFilter, () => {
  fetchAllFamilyUnits({
    search: searchQuery.value,
    page: 1,
    membershipStatus: membershipFilter.value as 'all' | 'active' | 'none'
  })
})
```

#### 3.5 — Update `onMounted` to pass filter

Update line 26:

```typescript
onMounted(() => { fetchAllFamilyUnits({ membershipStatus: membershipFilter.value as 'all' | 'active' | 'none' }) })
```

#### 3.6 — Update `onPage` to include filter

Update `onPage` handler (line 28-30):

```typescript
const onPage = (event: DataTablePageEvent) => {
  fetchAllFamilyUnits({
    page: event.page + 1,
    pageSize: event.rows,
    search: searchQuery.value,
    membershipStatus: membershipFilter.value as 'all' | 'active' | 'none'
  })
}
```

#### 3.7 — Add SelectButton to template

In the header area (after the search input `</span>`, line 50), add the SelectButton:

```html
<SelectButton
  v-model="membershipFilter"
  :options="membershipFilterOptions"
  option-label="label"
  option-value="value"
  :allow-empty="false"
  data-testid="membership-filter"
/>
```

#### 3.8 — Add "Nº Familia" column to DataTable

Add a new `<Column>` after the "Nombre Familia" column (after line 87):

```html
<Column field="familyNumber" header="Nº Familia">
  <template #body="{ data }">
    <span class="text-gray-600">{{ data.familyNumber ?? '—' }}</span>
  </template>
</Column>
```

---

### Step 4: Write Vitest Unit Tests

#### Step 4a: Test `useFamilyUnits` composable

- **File**: `frontend/src/composables/__tests__/useFamilyUnits.test.ts` (update existing or create)

**Test cases:**

1. **fetchAllFamilyUnits passes membershipStatus to query params**: Mock API call, verify `membershipStatus=active` is in the URL when passed
2. **fetchAllFamilyUnits omits membershipStatus when 'all'**: Verify the param is not sent when value is 'all'
3. **updateFamilyNumber calls PUT endpoint correctly**: Mock API, verify URL `/family-units/{id}/family-number` and body `{ familyNumber: 5 }`
4. **updateFamilyNumber returns null on error**: Verify error handling sets `error.value`

#### Step 4b: Test `useMemberships` composable

- **File**: `frontend/src/composables/__tests__/useMemberships.test.ts` (update existing or create)

**Test cases:**

5. **updateMemberNumber calls PUT endpoint correctly**: Mock API, verify URL and body
6. **updateMemberNumber returns null on error**: Verify error handling

#### Step 4c: Test `FamilyUnitsAdminPanel.vue` component

- **File**: `frontend/src/components/admin/__tests__/FamilyUnitsAdminPanel.test.ts` (update existing or create)

**Test cases:**

7. **renders SelectButton with filter options**: Verify 3 options rendered (Todas, Socias, No socias)
8. **default filter is 'Todas'**: Verify initial state
9. **changing filter triggers fetchAllFamilyUnits with membershipStatus**: Click "No socias", verify API called with `membershipStatus=none`
10. **renders Nº Familia column**: Verify column header and data display
11. **displays '—' when familyNumber is null**: Verify fallback display

---

### Step 5: Update Technical Documentation

- **Action**: Update documentation to reflect frontend changes

**Implementation Steps:**

1. **Review all frontend changes**: types, composables, component
2. **No routing changes** — no doc update needed for routes
3. **Verify** the `data-model.md` was updated in the backend ticket (includes `familyNumber` and `memberNumber` fields)
4. **Report**: List files modified and changes made

---

## Implementation Order

1. Step 0 — Create feature branch
2. Step 1 — Update TypeScript interfaces (`family-unit.ts`, `membership.ts`)
3. Step 2 — Update composables (`useFamilyUnits.ts`, `useMemberships.ts`)
4. Step 3 — Update `FamilyUnitsAdminPanel.vue` (filter + column)
5. Step 4 — Write Vitest unit tests
6. Step 5 — Update documentation

## Testing Checklist

- [ ] `npm run test` — all existing tests pass
- [ ] New composable tests: `fetchAllFamilyUnits` with `membershipStatus` param
- [ ] New composable tests: `updateFamilyNumber` and `updateMemberNumber`
- [ ] Component test: filter SelectButton renders and triggers fetch
- [ ] Component test: "Nº Familia" column renders correctly
- [ ] Manual: open `/admin/family-units`, verify filter buttons appear
- [ ] Manual: click "No socias" → only families without memberships shown
- [ ] Manual: click "Socias" → only families with active memberships shown
- [ ] Manual: "Nº Familia" column shows numbers or "—"
- [ ] `npm run type-check` — no TypeScript errors

## Error Handling Patterns

- `fetchAllFamilyUnits`: sets `error.value` on API failure, shows `Message` component
- `updateFamilyNumber`: returns `null` on error, sets `error.value` with server message
- `updateMemberNumber`: returns `null` on error, sets `error.value` with server message
- Filter state resets to page 1 when changed to avoid stale pagination

## UI/UX Considerations

- **SelectButton**: PrimeVue `SelectButton` with 3 options ("Todas", "Socias", "No socias"), `:allow-empty="false"` to prevent deselection
- **Layout**: Filter placed next to search input in the header row, using flexbox gap
- **Nº Familia column**: Placed after "Nombre Familia" for natural reading order
- **Empty state**: Shows "—" when `familyNumber` is null
- **Responsive**: SelectButton and search stack on small screens via `flex-wrap`

## Dependencies

- **PrimeVue components used**: `SelectButton` (new), `DataTable`, `Column`, `InputText`, `Button`, `Message`, `ProgressSpinner` (existing)
- No new npm packages required

## Notes

- **Backend dependency**: This frontend plan assumes the backend endpoints are already implemented (query param `membershipStatus` on `GET /api/family-units`, and the two PUT endpoints for number updates)
- **TypeScript strict**: All new code uses proper types, no `any` where avoidable
- **Spanish UI labels**: All user-facing text in Spanish (filter labels, column headers, error messages)
- **Number editing UI**: The `updateFamilyNumber` and `updateMemberNumber` composable methods are provided but no inline editing UI is added to the admin panel in this ticket — that can be added in a follow-up when the admin detail view for family units is implemented

## Next Steps After Implementation

1. When the admin family unit detail view is implemented, add inline editing for family number and member numbers using the composable methods created here
2. Show `memberNumber` in the membership dialog and profile page membership badges
3. Consider adding a "Nº Socio/a" column to a members admin listing if one is created

## Implementation Verification

- [ ] **Code Quality**: `<script setup lang="ts">`, no `any`, strict TypeScript
- [ ] **Functionality**: Filter changes correctly update the family list, Nº Familia column displays
- [ ] **Testing**: Vitest tests pass for composable changes and component changes
- [ ] **Integration**: Composables correctly call backend API with new params
- [ ] **Documentation**: Updated as needed
