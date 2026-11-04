# Frontend Implementation Plan: fix-admin-users-server-search — Server-side search and pagination in the admin Users panel

## 1. Overview

The admin Users panel (`/admin/users`) loads `GET /api/users` once with no parameters and then filters the rows in the browser. The backend only returns the first 100 users, so users beyond that limit cannot be found.

This plan moves the panel to a **lazy PrimeVue DataTable**. Every search, page change and sort change triggers a request with `page`, `pageSize`, `search`, `sortBy` and `sortOrder`. The panel consumes the new `PagedUsersResponse` contract defined in [fix-admin-users-server-search_backend.md](./fix-admin-users-server-search_backend.md).

Principles:
- Vue 3 `<script setup lang="ts">` and the Composition API.
- A composable owns API communication and state.
- The component is presentational and only holds local UI state.
- UI is built with PrimeVue and Tailwind.
- The reference pattern is `FamilyUnitsAdminPanel.vue` together with `useFamilyUnits.ts`.

Source spec: [fix-admin-users-server-search_enriched.md](./fix-admin-users-server-search_enriched.md)

**Dependency**: this change requires the backend change (the response shape is breaking). Both must merge into `dev` together.

## 2. Architecture Context

| File | Change |
|---|---|
| `frontend/src/types/user.ts` | Add `UserListParams` and `UserSortField` |
| `frontend/src/composables/useUsers.ts` | `fetchUsers(params)` sends paged query params and stores `pagination`. Mutations refresh the current page. |
| `frontend/src/components/admin/UsersAdminPanel.vue` | Remove the client-side `filteredUsers`. Use a lazy DataTable, debounced search, server sort, total counter and empty state. |
| `frontend/src/composables/__tests__/useUsers.test.ts` | Update the `fetchUsers` and `createUser`/`deleteUser` tests |
| `frontend/src/components/admin/__tests__/UsersAdminPanel.test.ts` | Update the mock; add search, paging and sort tests |
| `frontend/src/pages/UsersPage.vue` | **Delete**. It is dead code: `/users` redirects to `/admin/users` (`router/index.ts:265-272`), and no route imports it. |
| `frontend/src/pages/UserDetailPage.vue` | **Delete**. It is dead code: `/users/:id` also redirects to `/admin/users`, and no route imports it. |
| `frontend/cypress/e2e/admin-users-search.cy.ts` (new) | E2E test for server search |

Notes:
- **Routing**: no changes. The legacy redirects stay.
- **State**: local composable state only, with no Pinia store. `useUsers()` creates fresh refs on every call, as it does today.
- **Reused type**: `PagedResult<T>` in `frontend/src/types/api.ts:13-21`. The backend does not return `hasNextPage` or `hasPreviousPage`, so type the response as `Omit<PagedResult<User>, 'hasNextPage' | 'hasPreviousPage'>`. Alternatively, mirror the response type that `useFamilyUnits` uses (check which one it uses and stay consistent).
- **Existing quirk, fixed here**: `UserRoleDialog.vue:26` calls its own `useUsers()` instance. Its `updateUserRole` therefore updates a different `users` array, not the panel's. The panel must refresh the current page on `role-updated`.

## 3. Implementation Steps

### Step 0: Create Feature Branch

- **Action**: Create and switch to `feature/fix-admin-users-server-search-frontend`.
- **Implementation Steps**:
  1. `git checkout dev && git pull origin dev`. If the backend branch is not merged yet, branch from `feature/fix-admin-users-server-search-backend` instead, so the API contract is available locally.
  2. `git checkout -b feature/fix-admin-users-server-search-frontend`. If it already exists, check it out.
  3. Confirm with `git branch`.
- **Notes**:
  - The `-frontend` suffix is mandatory (`frontend-standards.mdc` → Git Workflow).
  - Do not work on the generic ticket branch.

### Step 1: Types

- **File**: `frontend/src/types/user.ts`
- **Action**: Add:

```ts
export type UserSortField = 'email' | 'firstName' | 'lastName' | 'createdAt' | 'role'

export interface UserListParams {
  page?: number          // 1-based, default 1
  pageSize?: number      // default 20, backend clamps to 100
  search?: string
  role?: UserRole | null
  isActive?: boolean | null
  sortBy?: UserSortField
  sortOrder?: 'asc' | 'desc'
}
```

### Step 2: Composable `useUsers.ts`

- **File**: `frontend/src/composables/useUsers.ts`
- **New state**:

```ts
const pagination = ref({ page: 1, pageSize: 20, totalCount: 0, totalPages: 0 })
const lastParams = ref<UserListParams>({ page: 1, pageSize: 20 })
let requestSeq = 0
```

- **`fetchUsers(params: UserListParams = {})` implementation steps**:
  1. Merge the arguments into the current state: `const merged = { ...lastParams.value, ...params }`, and store it in `lastParams.value`.
  2. Build the query. Omit empty values: drop `search` when it is empty after trimming, and drop `role` and `isActive` when they are `null` or `undefined`.
  3. Use a stale-response guard:

     ```ts
     const seq = ++requestSeq
     const response = await api.get<ApiResponse<PagedUsersResponse>>('/users', { params: query })
     if (seq !== requestSeq) return // a newer request superseded this one
     ```

  4. Set `users.value = data.items` and `pagination.value = { page, pageSize, totalCount, totalPages }`.
  5. Keep the `loading` and `error` handling and the existing error message. Only set `loading = false` when `seq === requestSeq`.
- **`refresh()`**: re-runs `fetchUsers(lastParams.value)`. Expose it.
- **Mutations**:
  - `createUser`: on success, call `await refresh()` instead of `users.value.push(...)`, and return the created user.
  - `deleteUser`: on success, if `users.value.length === 1 && pagination.value.page > 1`, call `fetchUsers({ page: pagination.value.page - 1 })`; otherwise call `refresh()`.
  - `updateUser`, `toggleUserActive` and `updateUserRole`: keep the in-place replacement. It is correct for the current page and avoids an extra request.
- **Return**: add `pagination` and `refresh` to the returned object. Keep every existing member (`fetchUserById`, `selectedUser`, `updateUserRole`, `clearError`, …), because `UserRoleDialog.vue` uses `updateUserRole`, `loading`, `error` and `clearError`.
- **Implementation notes**:
  - Pass the query through `params` in the axios config, not by string concatenation, so values are encoded correctly.
  - No `any`. Define `PagedUsersResponse` locally or reuse it from `types`, as described in Architecture Context.

### Step 3: Pinia store

- Not needed.

### Step 4: Component `UsersAdminPanel.vue`

- **File**: `frontend/src/components/admin/UsersAdminPanel.vue`
- **Script**:
  1. Remove the `filteredUsers` computed (lines 30-38) and the `computed` import if it becomes unused.
  2. Destructure `pagination` and `refresh` from `useUsers()`.
  3. Add local state:
     - `searchQuery` (it already exists);
     - `sortField = ref<UserSortField>('createdAt')`;
     - `sortOrder = ref<'asc' | 'desc'>('desc')`;
     - `rows = ref(20)`.
  4. Add `loadUsers(page = 1)`, which calls `fetchUsers({ page, pageSize: rows.value, search: searchQuery.value, sortBy: sortField.value, sortOrder: sortOrder.value })`.
  5. Add `const debouncedSearch = useDebounceFn(() => loadUsers(1), 300)` (from `@vueuse/core`) and `watch(searchQuery, debouncedSearch)`.
  6. On mount, call `onMounted(() => loadUsers(1))`.
  7. Add `onPage(e: DataTablePageEvent)`: set `rows.value = e.rows`, then call `loadUsers(e.page + 1)`.
  8. Add `onSort(e: DataTableSortEvent)`. Map `e.sortField` to a `UserSortField`:
     - the "Nombre" column → `firstName`;
     - `email` → `email`;
     - `role` → `role`;
     - `createdAt` → `createdAt`.

     Set `sortOrder` to `e.sortOrder === 1 ? 'asc' : 'desc'`. When `sortOrder` is null or 0 (the sort is cleared), reset to `createdAt` and `desc`. Then call `loadUsers(1)`.
  9. Change `handleRoleUpdated` to call `refresh()` after the toast.
  10. Change the retry button to `@click="refresh"`.
- **Template changes**:
  1. DataTable:

     ```vue
     <DataTable
       v-else
       :value="users"
       lazy
       paginator
       :rows="rows"
       :first="(pagination.page - 1) * rows"
       :total-records="pagination.totalCount"
       :rows-per-page-options="[10, 20, 50, 100]"
       :loading="loading"
       :sort-field="sortFieldColumn"
       :sort-order="sortOrder === 'asc' ? 1 : -1"
       @page="onPage"
       @sort="onSort"
       striped-rows scrollable scroll-height="flex" class="rounded-lg"
       data-testid="users-table"
     >
     ```

     `sortFieldColumn` is a computed that maps back to the column `field`. It is needed because `firstName` is the field of the "Nombre" column.
  2. Remove `sortable` from the "Estado" (`isActive`) column, because the backend cannot sort by it. Keep `sortable` on Nombre, Email, Rol and Creado.
  3. Loading: keep the full-page spinner only for the first load (`loading && users.length === 0 && !searchQuery`). Later loads use the DataTable `:loading` overlay, so the table and the search box do not unmount while the user types.
  4. Next to the title, add a total counter:

     ```vue
     <span class="text-sm text-gray-500" data-testid="users-total">{{ pagination.totalCount }} usuarios</span>
     ```

  5. Empty-state text:
     - with a search term: `No se encontraron usuarios para «{{ searchQuery.trim() }}».`
     - without one: `No hay usuarios.`
  6. Keep `data-testid="users-search-input"`, `users-table`, `users-admin-panel` and all row testids.
- **Dependencies**:
  - `useDebounceFn` from `@vueuse/core`, which is already a dependency (used in `FamilyUnitsAdminPanel.vue`).
  - The types `DataTablePageEvent` and `DataTableSortEvent` from `primevue/datatable`.
- **Implementation notes**:
  - Changing the search always resets to page 1.
  - Changing `rows` (page size) also goes through `onPage`; PrimeVue emits `page` with the new `rows`.
  - UI copy stays in Spanish (existing convention). Code, identifiers and comments are in English.

### Step 5: Routing

- No route changes.
- Delete `frontend/src/pages/UsersPage.vue` and `frontend/src/pages/UserDetailPage.vue`. Delete any test files for them too (none were found under `src/pages/__tests__`). Before deleting, run `grep -rn "UsersPage\|UserDetailPage" frontend/src frontend/cypress` and confirm the only references are the files themselves.

### Step 6: Vitest unit tests (TDD: write them first and watch them fail)

**6a. `useUsers.test.ts`**

- `fetchUsers` tests:
  - `sends page, pageSize, search, sortBy and sortOrder as query params`: assert `api.get` was called with `('/users', { params: { page: 2, pageSize: 20, search: 'ana', sortBy: 'email', sortOrder: 'asc' } })`.
  - `omits empty search and null filters from params`.
  - `maps PagedUsersResponse into users and pagination`.
  - `ignores a stale response when a newer request was issued`: resolve the first promise after the second, and assert that `users` holds the second payload.
  - `sets error when fetch fails` (keep this test and adapt its mock).
- `createUser` and `deleteUser` tests:
  - `createUser refreshes current page after success`: assert a second `api.get('/users', …)` call with the last params.
  - `deleteUser on last row of a page > 1 fetches previous page`.
  - `deleteUser otherwise refreshes current page`.
- Adapt the existing `updateUserRole` "updates user in list" tests: they still apply to in-place replacement.

**6b. `UsersAdminPanel.test.ts`**

- Update the `vi.mock('@/composables/useUsers')` mock (line ~40) to expose `pagination: ref({ page: 1, pageSize: 20, totalCount: N, totalPages: 1 })` and `refresh: vi.fn()`.
- Use `vi.useFakeTimers()` for the debounce tests.
- New tests:
  - `fetches first page on mount`: `mockFetchUsers` is called with `{ page: 1, pageSize: 20, search: '', sortBy: 'createdAt', sortOrder: 'desc' }`.
  - `debounces search input and resets to page 1`: type "ana", advance timers by 299 ms (no call), then by 1 more ms (exactly one call with `search: 'ana', page: 1`).
  - `does not filter rows client-side`: with the mock returning 3 users and the search set to "zzz", 3 rows are still rendered until the composable changes `users`.
  - `requests the next page on paginator page event`: emit `page` from the DataTable with `{ page: 1, rows: 20 }`, and expect `page: 2`.
  - `maps column sort to server sortBy/sortOrder`: emit `sort` with `{ sortField: 'email', sortOrder: 1 }`, and expect `sortBy: 'email', sortOrder: 'asc', page: 1`.
  - `shows total count`: `users-total` contains "N usuarios".
  - `shows search-specific empty message`.
  - `refreshes list after role update`: `UserRoleDialog` emits `role-updated`, and `refresh` is called.
- Keep all the existing role-visibility and edit-dialog tests passing.

### Step 7: Cypress E2E test

- **File**: `frontend/cypress/e2e/admin-users-search.cy.ts`
- **Scenarios**:
  1. Log in as `board@abuvi.org` (same as `admin-panel.cy.ts`) and visit `/admin/users`.
  2. Set up `cy.intercept('GET', '/api/users*').as('getUsers')`.
  3. Type an email fragment from a seeded user into `[data-testid="users-search-input"]`. Wait for `@getUsers` and assert `request.query.search` equals the fragment and `request.query.page` is `'1'`. Assert the table contains that user.
  4. Clear the search. Assert a request without `search`, and that `users-total` shows the full count.
- **Notes**: the main regression (a user beyond the first 100) needs seeding of more than 100 users. If the E2E seed cannot provide that, rely on the backend repository test plus the manual check, and keep this E2E test focused on the request wiring.

### Step 8: Update technical documentation

- **Implementation Steps**:
  1. `ai-specs/specs/api-endpoints.md`: the backend step adds `GET /api/users`. Verify the documented query params match what the frontend sends.
  2. `ai-specs/specs/frontend-standards.mdc`: if there is no section yet on server-side search and pagination for admin tables, add a short one. It should describe the pattern: a lazy `DataTable`, `useDebounceFn` at 300 ms, a search change resetting to page 1, a stale-response guard in the composable, and no client-side filtering of paged data. Reference `FamilyUnitsAdminPanel.vue` and `UsersAdminPanel.vue`. This prevents the bug from recurring.
  3. Note the removal of `UsersPage.vue` and `UserDetailPage.vue` in the PR description.
- **References**: `ai-specs/specs/documentation-standards.mdc`. Write everything in English.
- **Notes**: this step is mandatory.

## 4. Implementation Order

0. Create branch `feature/fix-admin-users-server-search-frontend`.
1. Step 1: types.
2. Step 6a tests for `useUsers`. Expect RED.
3. Step 2: composable. Expect GREEN.
4. Step 6b tests for `UsersAdminPanel`. Expect RED.
5. Step 4: component. Expect GREEN.
6. Step 5: delete the dead pages.
7. Refactor, then run `npm run lint`, `npm run type-check` (or `vue-tsc`) and `npm run test:unit`.
8. Step 7: Cypress E2E test.
9. Step 8: documentation.

## 5. Testing Checklist

- [ ] The `useUsers` tests pass, including params, mapping, the stale guard and mutation refresh.
- [ ] The `UsersAdminPanel` tests pass, including mount, debounce, paging, sort, total, empty state and role refresh. The existing permission tests still pass.
- [ ] `vue-tsc` shows no type errors, and there is no `any`.
- [ ] Lint is clean.
- [ ] The Cypress `admin-users-search.cy.ts` test passes, and `admin-panel.cy.ts` still passes.
- [ ] Manual check against a backend with more than 100 users:
  - Search finds a previously invisible user.
  - The paginator total matches the backend.
  - Sorting by email, name, role and created date works.
  - Changing the page size works.
  - Creating, deleting, toggling active and changing a role all update the table.
  - Fast typing never shows stale results.

## 6. Error Handling Patterns

- The composable keeps the `loading`, `error` and data refs. A failed fetch sets `error = 'Failed to load users. Please try again.'`, which is the existing message, and logs with `console.error`.
- The panel shows a `Message severity="error"` with a "Reintentar" button that calls `refresh()`, so the retry keeps the current page, search and sort.
- A failed fetch does not clear `users`; the previous page stays visible under the error.
- Mutation errors keep the current Toast and Message behavior.

## 7. UI/UX Considerations

- PrimeVue components:
  - `DataTable` with `lazy`, `paginator`, `:loading` and `@page`/`@sort`;
  - `IconField`, `InputIcon` and `InputText` for search;
  - `Message`, `Tag` and `ProgressSpinner`.
- The search input stays mounted during refetches, so focus is never lost while typing.
- The counter "N usuarios" gives feedback that the list is complete or filtered.
- The layout stays responsive: the header uses `flex flex-wrap gap-3`. Consider `w-full sm:w-72` on the search input so it fits on mobile.
- Accessibility: keep `aria-label="Buscar usuarios"`.

## 8. Dependencies

- No new npm packages. `@vueuse/core` and `primevue` are already installed.

## 9. Notes

- Do not reintroduce client-side filtering over paged data anywhere in this panel.
- Filters by role and active status are supported by the backend (`role`, `isActive`). Adding them to the UI is optional and out of scope for this ticket. If added, they reset to page 1, as in `FamilyUnitsAdminPanel.vue`.
- Language: code, identifiers, comments and docs are in English. UI copy stays in Spanish.
- TypeScript strict mode applies, with typed DataTable events.
- `GET /camps` (camp locations list and dropdown) has the same latent issue. It is tracked as a follow-up in the enriched spec and is out of scope here.

## 10. Next Steps After Implementation

- Open a single PR to `dev` that includes the backend branch, or merge them in sequence with no deploy in between, because the response change is breaking.
- Manually verify on an environment with more than 100 users.
- Create the follow-up ticket `fix-camps-list-limit`.

## 11. Implementation Verification

- [ ] Code quality: `<script setup lang="ts">`, no `any`, typed events, no unused imports.
- [ ] Functionality: every search, page and sort action hits the API with the correct params, and users beyond the first 100 can be found.
- [ ] Testing: Vitest coverage for the composable and the component, and the Cypress test for the search flow.
- [ ] Integration: works against the new backend `PagedUsersResponse`.
- [ ] Documentation: the `frontend-standards.mdc` pattern note and the `api-endpoints.md` alignment are done.
