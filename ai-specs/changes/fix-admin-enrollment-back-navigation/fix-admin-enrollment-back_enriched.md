# Enriched User Story — Userback #7324393

## Context

**Original report (Cristina Dalia Garcia Ortega):**
From the administration tab, when an admin opens a specific enrollment detail and clicks the back arrow button, instead of returning to the admin enrollment list, the app redirects to "My Enrollments" (the user-facing personal enrollments page).

**Root cause:**
`RegistrationDetailPage.vue` is a **shared component** used by both the user-facing and admin flows. Its back button hardcodes navigation to `{ name: 'registrations' }` (the user's personal `/registrations` page), with no awareness of the navigation context (admin vs. user).

---

## User Story

**As an admin**, when I open an enrollment detail from the admin panel and press the back button, I should be taken back to the **admin enrollments list**, not to "My Enrollments".

### Acceptance Criteria

- Clicking the back button in `RegistrationDetailPage` when reached from `/admin/registrations` navigates to `/admin/registrations`.
- Clicking the back button when reached from `/registrations` (user-facing) continues to navigate to `/registrations`.
- Behavior is consistent even after a page refresh (query param preserved in URL).
- No duplicate routes or components are created.

---

## Implementation

### Solution: `returnTo` query parameter

When the admin panel navigates to a registration detail, it passes `?returnTo=admin-registrations` as a query parameter. `RegistrationDetailPage` reads this param on back press and routes accordingly.

### Files to modify

#### 1. `frontend/src/components/admin/RegistrationsAdminPanel.vue`

**Where:** `onRowClick` handler (~line 106).

```ts
// Before
const onRowClick = (event: DataTableRowClickEvent) => {
  router.push({ name: 'registration-detail', params: { id: event.data.id } })
}

// After
const onRowClick = (event: DataTableRowClickEvent) => {
  router.push({
    name: 'registration-detail',
    params: { id: event.data.id },
    query: { returnTo: 'admin-registrations' },
  })
}
```

#### 2. `frontend/src/views/registrations/RegistrationDetailPage.vue`

**Where:** Script setup and back button template (~line 391).

Add computed refs in `<script setup>`:

```ts
const returnTo = computed(() =>
  route.query.returnTo === 'admin-registrations'
    ? { name: 'admin-registrations' }
    : { name: 'registrations' }
)

const backLabel = computed(() =>
  route.query.returnTo === 'admin-registrations'
    ? 'Volver a inscripciones'
    : 'Volver a mis inscripciones'
)
```

Update the back button in the template:

```html
<!-- Before -->
<Button
  icon="pi pi-arrow-left"
  severity="secondary"
  text
  @click="router.push({ name: 'registrations' })"
  aria-label="Volver a mis inscripciones"
/>

<!-- After -->
<Button
  icon="pi pi-arrow-left"
  severity="secondary"
  text
  @click="router.push(returnTo)"
  :aria-label="backLabel"
/>
```

**No backend changes required.**

---

## Testing

### Manual verification
1. Admin → `/admin/registrations` → click a row → URL becomes `/registrations/:id?returnTo=admin-registrations` → click back → lands on `/admin/registrations`. ✓
2. User → `/registrations` → click an enrollment → click back → lands on `/registrations`. ✓
3. Admin: directly open `/registrations/:id?returnTo=admin-registrations` (refresh) → click back → lands on `/admin/registrations`. ✓

### Unit tests (`frontend/src/views/registrations/RegistrationDetailPage.spec.ts`)
- Mount with `route.query.returnTo = 'admin-registrations'` → assert `router.push` called with `{ name: 'admin-registrations' }`.
- Mount without query param → assert `router.push` called with `{ name: 'registrations' }`.

---

## Non-functional requirements

- TypeScript: `route.query.returnTo` is typed as `string | string[]`; compare with strict equality (`=== 'admin-registrations'`) which handles this safely.
- All user-facing text (aria-labels) in Spanish.
- Change is minimal: two files, no new routes or components.
