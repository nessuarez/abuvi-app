# feat: Integrated user search in admin user management view

## Context

The admin user management view (`UsersAdminPanel.vue`) currently displays all users in a sortable, paginated DataTable but has no search or filter capability. Administrators and board members must scroll through the full list to find a specific user. Given the association has a manageable number of users (expected < 500), a **client-side filter** is the right approach: the full list is already fetched on mount (`GET /api/users?take=100`), so filtering the in-memory array is both simpler and sufficient.

---

## User Story

**As** an administrator or board member,
**I want** to search users by name or email in the admin user management panel,
**so that** I can quickly locate a specific user without scrolling through the full list.

---

## Approach: Client-side filtering via PrimeVue DataTable `globalFilter`

No backend changes are required. PrimeVue's `DataTable` component natively supports client-side global filtering via the `:global-filter-value` and `globalFilterFields` props. The implementation adds an `InputText` search box above the table and wires it to the DataTable's filter mechanism.

> **Why not server-side search?** The association's user base is small and the full list is already loaded. Adding a backend search param would introduce unnecessary round-trips and complexity. If the user count grows significantly in the future, the backend can be extended then.

---

## Functional Requirements

1. A search input field appears above the DataTable in the `UsersAdminPanel.vue` header row, to the left of the "Crear Usuario" button.
2. The search filters **as-you-type** (no submit button needed).
3. Filtering applies to:
   - **Email** (`email` field, exact substring match, case-insensitive)
   - **Full name** — combines `firstName` + `lastName` into a searchable field (see implementation note below)
4. When no results match, the DataTable shows PrimeVue's built-in empty state message (customizable).
5. The filter is cleared when the input is emptied.
6. Pagination resets to page 1 whenever the search term changes.
7. The search input is not shown during initial loading or when the error state is active.

---

## UI Specification

### Header layout (before and after)

**Before:**
```
[Gestión de Usuarios]                          [Crear Usuario]
```

**After:**
```
[Gestión de Usuarios]   [🔍 Buscar por nombre o email...]   [Crear Usuario]
```

### Search input details

- PrimeVue `InputText` component with `placeholder="Buscar por nombre o email…"`
- Icon prefix: `pi pi-search` (using PrimeVue `IconField` + `InputIcon` pattern)
- Width: `w-72` (288px) on desktop; full width on mobile
- `data-testid="users-search-input"` for E2E testing

---

## Implementation Details

### Files to modify

| File | Change |
|------|--------|
| `frontend/src/components/admin/UsersAdminPanel.vue` | Add search input, wire to DataTable filter |

### No files to modify in backend

The existing `GET /api/users?skip=0&take=100` endpoint is unchanged.

---

### Code changes in `UsersAdminPanel.vue`

#### Script additions

```ts
import { onMounted, ref, computed } from 'vue'
import InputText from 'primevue/inputtext'
import IconField from 'primevue/iconfield'
import InputIcon from 'primevue/inputicon'

// Add after existing refs:
const searchQuery = ref('')

// Computed list filtered client-side — used as DataTable :value
const filteredUsers = computed(() => {
  const q = searchQuery.value.trim().toLowerCase()
  if (!q) return users.value
  return users.value.filter(u =>
    u.email.toLowerCase().includes(q) ||
    `${u.firstName} ${u.lastName}`.toLowerCase().includes(q)
  )
})
```

> **Note:** Instead of using PrimeVue's `globalFilterFields` (which requires exact field name matching and doesn't support composite `firstName + lastName`), we use a computed `filteredUsers` array. This gives full control over what is searched and avoids the need for a custom filter function in the DataTable config.

#### Template changes

Replace the header `div`:

```html
<div class="flex flex-wrap items-center justify-between gap-3">
  <h2 class="text-xl font-semibold text-gray-800">Gestión de Usuarios</h2>
  <div class="flex items-center gap-3">
    <IconField>
      <InputIcon class="pi pi-search" />
      <InputText
        v-model="searchQuery"
        placeholder="Buscar por nombre o email…"
        class="w-72"
        data-testid="users-search-input"
      />
    </IconField>
    <Button label="Crear Usuario" icon="pi pi-plus" @click="openCreateDialog" />
  </div>
</div>
```

Bind the computed list to the DataTable:

```html
<DataTable
  :value="filteredUsers"
  ...
>
```

Add empty message customization:

```html
<template #empty>
  <span class="text-gray-500">
    No se encontraron usuarios que coincidan con la búsqueda.
  </span>
</template>
```

---

## Acceptance Criteria

- [ ] A search input is visible in the `UsersAdminPanel` header, between the title and the "Crear Usuario" button
- [ ] Typing in the search input filters the DataTable rows in real time
- [ ] Filtering works for partial matches on **email** (e.g., "gmail" matches "user@gmail.com")
- [ ] Filtering works for partial matches on **first name**, **last name**, or **full name** (e.g., "juan" matches "Juan García"; "garcía" also matches)
- [ ] Filtering is **case-insensitive**
- [ ] When the search input is cleared, all users are shown again
- [ ] When no users match, the DataTable shows a "No se encontraron usuarios…" empty message
- [ ] The search input is hidden while initial loading is in progress
- [ ] The existing sort, pagination, and role-change functionality is unaffected

---

## Non-functional Requirements

- **Performance**: The computed filter runs synchronously over the in-memory list. For up to 500 users this is instantaneous; no debouncing is required.
- **Accessibility**: The search `InputText` must have an accessible label. Use `aria-label="Buscar usuarios"` on the input element.
- **Responsiveness**: On screens `< md`, the search input expands to full width (`w-full`) and stacks above the "Crear Usuario" button using `flex-wrap`.
- **No regressions**: No changes to the backend API, composable, or other user components.

---

## Testing

### Unit/component test (Vitest + Vue Test Utils) — optional

- Mount `UsersAdminPanel` with mocked `useUsers` returning a fixed list of 5 users
- Assert that typing "juan" in `[data-testid="users-search-input"]` reduces the displayed rows to only those matching

### E2E (Cypress) — recommended

```ts
// cypress/e2e/admin/users-search.cy.ts
it('filters users by name in admin panel', () => {
  cy.loginAsAdmin()
  cy.visit('/admin/users')          // or the current admin tab path
  cy.get('[data-testid="users-table"]').should('be.visible')
  cy.get('[data-testid="users-search-input"]').type('juan')
  cy.get('[data-testid="users-table"] tbody tr').each(($row) => {
    cy.wrap($row).invoke('text').then((text) => {
      expect(text.toLowerCase()).to.include('juan')
    })
  })
  cy.get('[data-testid="users-search-input"]').clear()
  // all rows visible again — count should equal original
})
```

---

## Out of scope

- Server-side search/pagination (not needed at current scale)
- Filtering by role or status (separate feature if needed)
- Debouncing (not needed for < 500 users)
- Highlighting matched text in search results
