# Frontend Implementation Plan: 7324393 — Fix Admin Enrollment Back Navigation

## Overview

Fixes a navigation bug where the back button on `RegistrationDetailPage.vue` always redirects to the user-facing "My Enrollments" page (`/registrations`) regardless of the caller context. When an admin navigates to a registration detail from the admin panel, pressing back should return to the admin enrollment list (`/admin/registrations`).

**Stack:** Vue 3 Composition API (`<script setup lang="ts">`), Vue Router query params, PrimeVue Button, Tailwind CSS. No new composables, stores, or routes required.

---

## Architecture Context

### Components involved
| File | Role |
|------|------|
| `frontend/src/components/admin/RegistrationsAdminPanel.vue` | Source of the admin navigation — must pass `returnTo` query param |
| `frontend/src/views/registrations/RegistrationDetailPage.vue` | Shared detail page — must read `returnTo` and route accordingly |

### Routing considerations
- Route `registration-detail` at `/registrations/:id` is shared between admin and user flows.
- No new routes are needed. Context is communicated via `?returnTo=admin-registrations` query param.
- Vue Router's `useRoute` is already imported in `RegistrationDetailPage.vue` (line 3).

### State management
- No Pinia store changes needed. Navigation context lives in the URL query param, which persists across page refreshes.

---

## Implementation Steps

### Step 0: Create Feature Branch

- **Action**: Create and switch to a new feature branch
- **Branch name**: `feature/7324393-frontend`
- **Base branch**: `dev`
- **Steps**:
  1. `git checkout dev`
  2. `git pull origin dev`
  3. `git checkout -b feature/7324393-frontend`
  4. `git branch` — verify active branch

---

### Step 1: Update `RegistrationsAdminPanel.vue` — Pass `returnTo` query param

- **File**: `frontend/src/components/admin/RegistrationsAdminPanel.vue`
- **Action**: Add `query: { returnTo: 'admin-registrations' }` to the `router.push` call in `onRowClick`
- **Where**: Line 105–107

**Before:**
```ts
const onRowClick = (event: DataTableRowClickEvent) => {
  router.push({ name: 'registration-detail', params: { id: event.data.id } })
}
```

**After:**
```ts
const onRowClick = (event: DataTableRowClickEvent) => {
  router.push({
    name: 'registration-detail',
    params: { id: event.data.id },
    query: { returnTo: 'admin-registrations' },
  })
}
```

- **Dependencies**: None — `useRouter` already imported
- **Notes**: The string `'admin-registrations'` maps to the route name `admin-registrations` in the router

---

### Step 2: Update `RegistrationDetailPage.vue` — Context-aware back navigation

- **File**: `frontend/src/views/registrations/RegistrationDetailPage.vue`
- **Action**: Add two `computed` refs that derive the back destination and aria-label from `route.query.returnTo`; update the back button to use them
- **Where**: Script setup (after existing `const route = useRoute()` line, ~line 37) and template (~line 387–393)

**Add to `<script setup>` (after `const route = useRoute()`):**
```ts
const backRoute = computed(() =>
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

**Update the back button in `<template>` (lines 387–393):**
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
  @click="router.push(backRoute)"
  :aria-label="backLabel"
/>
```

- **Dependencies**: `computed` is already imported from `vue` (line 1). `useRoute` already imported (line 3).
- **Notes**:
  - `route.query.returnTo` is typed `string | string[] | undefined`. Strict equality `=== 'admin-registrations'` handles all three cases correctly without needing a cast.
  - Variable named `backRoute` (not `returnTo`) to avoid shadowing the query param name.

---

### Step 3: Write Unit Tests — `RegistrationDetailPage.vue`

- **File**: `frontend/src/views/__tests__/RegistrationDetailPage.spec.ts` *(new file)*
- **Action**: Test that the back button navigates to the correct route based on `returnTo` query param
- **Pattern**: Follow existing view spec pattern from `frontend/src/views/__tests__/CampPage.spec.ts` and `ProfilePage.spec.ts`

**Test cases:**
```ts
describe('RegistrationDetailPage back navigation', () => {
  it('navigates to registrations when no returnTo param', async () => {
    // mount with route.query = {}
    // click back button
    // assert router.push called with { name: 'registrations' }
  })

  it('navigates to admin-registrations when returnTo=admin-registrations', async () => {
    // mount with route.query = { returnTo: 'admin-registrations' }
    // click back button
    // assert router.push called with { name: 'admin-registrations' }
  })

  it('uses correct aria-label when returnTo=admin-registrations', () => {
    // assert aria-label = 'Volver a inscripciones'
  })

  it('uses default aria-label when no returnTo', () => {
    // assert aria-label = 'Volver a mis inscripciones'
  })
})
```

- **Mocking**: Mock `useRegistrations`, `usePayments`, `useFamilyUnits`, `useCampEditions`, `useAuthStore` to avoid API calls. Mock `vue-router` with `vi.mock`.
- **Notes**: The page has many composable dependencies; stub them all returning safe defaults so the component mounts without errors.

---

### Step 4: Update Technical Documentation

- **Action**: No `api-spec.yml` changes (no API changes). No `frontend-standards.mdc` additions needed since query-param-based `returnTo` is a standard Vue Router pattern already documented. No routing changes.
- **What to note**: This fix establishes the `returnTo` query-param navigation pattern. If this pattern is reused elsewhere in future, add it to `frontend-standards.mdc` under "Navigation Patterns".

---

## Implementation Order

1. **Step 0** — Create branch `feature/7324393-frontend` from `dev`
2. **Step 1** — Update `RegistrationsAdminPanel.vue` (`onRowClick`)
3. **Step 2** — Update `RegistrationDetailPage.vue` (computed + template)
4. **Step 3** — Write unit tests
5. **Step 4** — Documentation review

---

## Testing Checklist

- [ ] Admin flow: `/admin/registrations` → click row → URL contains `?returnTo=admin-registrations` → back button → lands on `/admin/registrations`
- [ ] User flow: `/registrations` → click enrollment → back button → lands on `/registrations`
- [ ] Refresh at `/registrations/:id?returnTo=admin-registrations` → back button still works correctly (query param preserved)
- [ ] Vitest: both `returnTo=admin-registrations` and no-param cases covered
- [ ] Vitest: `aria-label` renders correctly in both cases
- [ ] TypeScript: `tsc --noEmit` passes with no errors

---

## Error Handling Patterns

No new error states introduced. The computed `backRoute` has a safe default (`{ name: 'registrations' }`) for any unrecognised or missing `returnTo` value — no error handling needed.

---

## UI/UX Considerations

- The back button appearance is unchanged (same PrimeVue `Button` with `pi-arrow-left`, `severity="secondary"`, `text` prop).
- `aria-label` is contextualised: admin users will see "Volver a inscripciones"; regular users see "Volver a mis inscripciones".
- No layout or responsive-design changes required.

---

## Dependencies

No new npm packages or PrimeVue components required. All dependencies already present:
- `vue-router` — `useRoute`, `useRouter`
- `vue` — `computed`
- `primevue/button` — existing

---

## Notes

- All user-facing strings in Spanish (per `base-standards.mdc`).
- No `any` types introduced.
- No `<style>` blocks.
- No Options API.
- Branch targets `dev` for PR (per project git workflow).

---

## Next Steps After Implementation

1. Open PR `feature/7324393-frontend` → `dev`
2. Reference Userback ticket #7324393 in PR description
3. Move Userback ticket to "In Progress" / "Resolved" after merge

---

## Implementation Verification

- [ ] **TypeScript**: `<script setup lang="ts">`, no `any`, `tsc --noEmit` clean
- [ ] **Functionality**: Back button routes correctly in both admin and user contexts
- [ ] **Testing**: Vitest unit tests cover all 4 cases; no Cypress E2E needed (covered by unit tests for this scope)
- [ ] **Integration**: No composable or store changes; routing uses existing named routes
- [ ] **Documentation**: No spec files require updates for this change
