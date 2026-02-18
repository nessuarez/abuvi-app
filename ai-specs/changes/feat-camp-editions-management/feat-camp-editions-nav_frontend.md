# Frontend Implementation Plan: feat-camp-editions-nav — Camp Editions Navigation Entry Points

**Related plan:** [camp-editions-management_frontend.md](./feat-camp-editions-management/camp-editions-management_frontend.md)
**Branch:** `feature/feat-camp-editions-nav-frontend`
**Date:** 2026-02-18

---

## 1. Overview

This plan wires up navigation entry points so Board+ users can reach the Camp Editions management page from natural access points (Admin panel, camp location detail, camp locations list) and pre-filter editions by a specific camp. It also adds a "New Proposal" shortcut from the camp location detail page.

**Depends on**: `feat-camp-editions-management` plan — the `/camps/editions` route and `CampEditionsPage.vue` must either already exist or be built as part of this ticket.

Architecture: small, focused changes to 4 existing files + 1 router addition.

---

## 2. Architecture Context

### Files to modify

| File | Change |
|------|--------|
| `frontend/src/router/index.ts` | Add `/camps/editions` (Board+) and `/camps/editions/:id` (Member+) routes |
| `frontend/src/components/admin/CampsAdminPanel.vue` | Replace "próximamente" placeholder with real edition management navigation |
| `frontend/src/views/camps/CampLocationDetailPage.vue` | Add "Editions" action section with filtered link and proposal shortcut |
| `frontend/src/views/camps/CampLocationsPage.vue` | Add "Ver ediciones" action in the camp table/card |

### Files to create

None — this ticket is navigation wiring only.

> **Prerequisite**: `CampEditionsPage.vue` at `frontend/src/views/camps/CampEditionsPage.vue` must exist (built in `feat-camp-editions-management`). If not yet built, build it first or create a placeholder page.

### State management approach

No new state. Each modified component uses `useRouter()` for programmatic navigation. The `CampEditionsPage` handles filtering via URL query params (`?campId=`).

### Routing

The new `/camps/editions` route supports an optional `campId` query param. The page reads it via `useRoute().query.campId` and pre-fills the camp filter on mount.

---

## 3. Implementation Steps

### Step 0: Create Feature Branch

**Action**: Create and switch to a new feature branch.
**Branch name**: `feature/feat-camp-editions-nav-frontend`

```bash
git checkout main && git pull origin main
git checkout -b feature/feat-camp-editions-nav-frontend
```

> **Note**: If `feat-camp-editions-management` is still in progress on a separate branch, base from that branch instead and rebase onto `main` when it merges.

---

### Step 1: Add Routes to Router

**File**: `frontend/src/router/index.ts`
**Action**: Add two new routes in the "Camp Management routes (Board only)" section.

```typescript
// Add inside the routes array, after /camps/locations/:id

// Camp Editions Management (Board only)
{
  path: '/camps/editions',
  name: 'camp-editions',
  component: () => import('@/views/camps/CampEditionsPage.vue'),
  meta: {
    title: 'ABUVI | Gestión de Ediciones',
    requiresAuth: true,
    requiresBoard: true
  }
},
// Camp Edition Detail (authenticated users)
{
  path: '/camps/editions/:id',
  name: 'camp-edition-detail',
  component: () => import('@/views/camps/CampEditionDetailPage.vue'),
  meta: {
    title: 'ABUVI | Detalle de Edición',
    requiresAuth: true
  }
},
```

**Implementation Notes**:

- Place these routes **before** the legacy user routes redirect block.
- Both routes lazy-load their components.
- `camp-edition-detail` requires only `requiresAuth: true` (Members can view open editions).
- The `/camps/editions` path does **not** conflict with `/camps/editions/:id` — Vue Router matches static segments before dynamic ones.
- `CampEditionDetailPage.vue` should be a placeholder if not yet built (see `feat-camp-editions-management` plan).

---

### Step 2: Update `CampsAdminPanel.vue`

**File**: `frontend/src/components/admin/CampsAdminPanel.vue`
**Action**: Replace the "próximamente" placeholder card with real navigation to editions management.

**Current state**: Shows a `Card` with a disabled placeholder saying "La gestión completa de ediciones estará disponible próximamente."

**New implementation**:

```vue
<script setup lang="ts">
import { useRouter } from 'vue-router'
import Card from 'primevue/card'
import Button from 'primevue/button'

const router = useRouter()

const goToCampLocations = () => router.push('/camps/locations')
const goToCampEditions = () => router.push('/camps/editions')
</script>

<template>
  <div data-testid="camps-admin-panel" class="space-y-4">
    <div class="flex items-center justify-between">
      <h2 class="text-xl font-semibold text-gray-800">Gestión de Campamentos</h2>
    </div>

    <div class="grid grid-cols-1 gap-4 sm:grid-cols-2">
      <!-- Editions card -->
      <Card class="cursor-pointer transition-shadow hover:shadow-md" @click="goToCampEditions">
        <template #content>
          <div class="flex flex-col items-center py-6 text-center">
            <i class="pi pi-calendar mb-4 text-4xl text-primary-400" />
            <p class="text-lg font-semibold text-gray-800">Ediciones de Campamento</p>
            <p class="mt-1 text-sm text-gray-500">
              Gestiona el ciclo de vida de las ediciones: propuestas, apertura y cierre.
            </p>
            <Button
              label="Gestionar ediciones"
              icon="pi pi-arrow-right"
              icon-pos="right"
              outlined
              class="mt-4"
              @click.stop="goToCampEditions"
            />
          </div>
        </template>
      </Card>

      <!-- Locations card -->
      <Card class="cursor-pointer transition-shadow hover:shadow-md" @click="goToCampLocations">
        <template #content>
          <div class="flex flex-col items-center py-6 text-center">
            <i class="pi pi-map-marker mb-4 text-4xl text-primary-400" />
            <p class="text-lg font-semibold text-gray-800">Ubicaciones de Campamento</p>
            <p class="mt-1 text-sm text-gray-500">
              Gestiona los lugares físicos donde se celebran los campamentos.
            </p>
            <Button
              label="Gestionar ubicaciones"
              icon="pi pi-arrow-right"
              icon-pos="right"
              outlined
              class="mt-4"
              @click.stop="goToCampLocations"
            />
          </div>
        </template>
      </Card>
    </div>
  </div>
</template>
```

**Implementation Notes**:

- Two equal cards replace the single placeholder — editions and locations.
- Clicking anywhere on the card or the button navigates to the target page.
- `.stop` modifier on the inner button prevents the card's `@click` from double-firing.

---

### Step 3: Update `CampLocationDetailPage.vue`

**File**: `frontend/src/views/camps/CampLocationDetailPage.vue`
**Action**: Add an "Ediciones" action section in the info panel that links to the editions management page pre-filtered for this camp.

**What to add** — a new card alongside the existing Description, Pricing, and Metadata cards:

```vue
<!-- Inside the info panel <div class="space-y-6"> after the Metadata card -->

<!-- Editions -->
<div class="rounded-lg border border-gray-200 bg-white p-6">
  <div class="mb-4 flex items-center justify-between">
    <h2 class="text-lg font-semibold text-gray-900">Ediciones</h2>
    <span
      v-if="camp.editionCount !== undefined"
      class="rounded-full bg-gray-100 px-2 py-0.5 text-sm text-gray-600"
    >
      {{ camp.editionCount }} {{ camp.editionCount === 1 ? 'edición' : 'ediciones' }}
    </span>
  </div>
  <div class="flex flex-col gap-2 sm:flex-row">
    <Button
      label="Ver ediciones"
      icon="pi pi-list"
      outlined
      class="flex-1"
      data-testid="view-editions-btn"
      @click="goToEditions"
    />
    <Button
      label="Nueva propuesta"
      icon="pi pi-plus"
      class="flex-1"
      data-testid="propose-edition-btn"
      @click="proposeNewEdition"
    />
  </div>
</div>
```

**Script additions**:

```typescript
// Add these two methods to the existing <script setup>

const goToEditions = () => {
  router.push({ name: 'camp-editions', query: { campId: route.params.id as string } })
}

const proposeNewEdition = () => {
  router.push({
    name: 'camp-editions',
    query: { campId: route.params.id as string, action: 'propose' }
  })
}
```

**Implementation Notes**:

- Remove the existing edition count display from the Metadata card's `v-if="camp.editionCount !== undefined"` block (it is now shown in the new Editions card header) to avoid duplication.
- `goToEditions` navigates to `/camps/editions?campId={id}` — the `CampEditionsPage` must read `route.query.campId` on mount and pre-fill its camp filter.
- `proposeNewEdition` passes `action=propose` as a query param — `CampEditionsPage` must detect this and auto-open the proposal dialog pre-filled with the campId. This behavior is implemented in `CampEditionsPage` (see Step 4).

---

### Step 4: Update `CampEditionsPage.vue` to Handle Query Params

**File**: `frontend/src/views/camps/CampEditionsPage.vue`
**Action**: Read `campId` and `action` query params from the route on mount and apply them to the filter state and dialog state.

**Script additions** (inside the existing `<script setup>`):

```typescript
import { useRoute } from 'vue-router'

const route = useRoute()

// On mount, apply query param pre-fills
onMounted(async () => {
  // Pre-fill campId filter from query param (e.g., coming from CampLocationDetailPage)
  if (route.query.campId) {
    filters.campId = route.query.campId as string
  }

  // Load camps list for filter dropdown
  await fetchCamps()

  // Load editions with pre-filled filter
  await fetchAllEditions(activeFilters.value)

  // Auto-open proposal dialog if action=propose
  if (route.query.action === 'propose' && route.query.campId) {
    proposalCampId.value = route.query.campId as string
    showProposalDialog.value = true
  }
})
```

**Implementation Notes**:

- `filters` is the reactive filter state: `{ year: null, status: null, campId: null }` — update `campId` from the query param.
- `proposalCampId` and `showProposalDialog` are refs that control the proposal dialog. The proposal dialog opens pre-filled with the campId when `action=propose` is in the URL.
- After the dialog opens, **clear the query params** from the URL using `router.replace({ name: 'camp-editions' })` to avoid the dialog re-opening on refresh.
- This step is only relevant if `CampEditionsPage.vue` is being built as part of this ticket. If it was already built without query param support, add it here.

---

### Step 5: Update `CampLocationsPage.vue` — Add "Ediciones" Action

**File**: `frontend/src/views/camps/CampLocationsPage.vue`
**Action**: Add a "Ver ediciones" button in the camp locations DataTable actions column.

First, read `CampLocationsPage.vue` to understand the current table structure, then add the button to the existing actions column.

The button should be added alongside existing edit/delete/detail actions:

```vue
<!-- Inside the actions Column template (DataTable) -->
<Button
  v-tooltip.top="'Ver ediciones'"
  icon="pi pi-calendar"
  text
  rounded
  aria-label="Ver ediciones de esta ubicación"
  data-testid="view-camp-editions-btn"
  @click="goToEditionsForCamp(data.id)"
/>
```

**Script addition**:

```typescript
const goToEditionsForCamp = (campId: string) => {
  router.push({ name: 'camp-editions', query: { campId } })
}
```

**Implementation Notes**:

- Add `v-tooltip` directive — PrimeVue `Tooltip` is available globally if `TooltipDirective` is registered in `main.ts`. If not, import and use `useTooltip` or simply rely on `aria-label`.
- Check the existing action buttons in `CampLocationsPage.vue` for the correct icon button pattern (text + rounded + icon).
- If the page has both a table view and a card view, add the editions link to both.
- In the card view, add a footer button "Ver ediciones" alongside existing edit/delete actions.

---

### Step 6: Write Unit Tests

**File**: `frontend/src/components/admin/__tests__/CampsAdminPanel.test.ts`
**Action**: Update tests to reflect the new navigation (remove "próximamente" assertion, add editions navigation test).

**Tests to write/update**:

```typescript
describe('CampsAdminPanel', () => {
  it('should render two navigation cards', () => {
    const wrapper = mount(CampsAdminPanel, { global: { plugins: [router] } })
    expect(wrapper.text()).toContain('Ediciones de Campamento')
    expect(wrapper.text()).toContain('Ubicaciones de Campamento')
  })

  it('should navigate to /camps/editions when editions button is clicked', async () => {
    const wrapper = mount(CampsAdminPanel, { global: { plugins: [router] } })
    await wrapper.find('[data-testid="view-editions-btn"]').trigger('click') // add testid
    expect(router.currentRoute.value.path).toBe('/camps/editions')
  })

  it('should navigate to /camps/locations when locations button is clicked', async () => {
    // ...
  })
})
```

---

### Step 7: Write Cypress E2E Tests

**File**: `frontend/cypress/e2e/camps/camp-editions-nav.cy.ts`
**Action**: E2E tests for the navigation entry points.

```typescript
describe('Camp Editions Navigation', () => {
  beforeEach(() => {
    cy.login('board@abuvi.org', 'password')
  })

  describe('Admin panel → Editions', () => {
    it('should navigate to editions page from admin panel', () => {
      cy.intercept('GET', '/api/camps/editions*', { body: { success: true, data: [], error: null } })
      cy.visit('/admin')
      cy.get('[data-testid="tab-camps"]').click()
      cy.contains('Gestionar ediciones').click()
      cy.url().should('include', '/camps/editions')
    })
  })

  describe('Camp location detail → Editions', () => {
    it('should navigate to editions filtered by campId', () => {
      cy.intercept('GET', '/api/camps/*', { fixture: 'camp-detail.json' })
      cy.intercept('GET', '/api/camps/editions*', { body: { success: true, data: [], error: null } })
      cy.visit('/camps/locations/some-camp-id')
      cy.get('[data-testid="view-editions-btn"]').click()
      cy.url().should('include', '/camps/editions')
      cy.url().should('include', 'campId=some-camp-id')
    })

    it('should open proposal dialog when clicking Nueva propuesta', () => {
      cy.intercept('GET', '/api/camps/*', { fixture: 'camp-detail.json' })
      cy.intercept('GET', '/api/camps/editions*', { body: { success: true, data: [], error: null } })
      cy.visit('/camps/locations/some-camp-id')
      cy.get('[data-testid="propose-edition-btn"]').click()
      cy.url().should('include', 'action=propose')
      cy.get('[data-testid="proposal-dialog"]').should('be.visible')
    })
  })

  describe('Camp locations list → Editions', () => {
    it('should navigate to editions from the locations table', () => {
      cy.intercept('GET', '/api/camps*', { fixture: 'camps-list.json' })
      cy.intercept('GET', '/api/camps/editions*', { body: { success: true, data: [], error: null } })
      cy.visit('/camps/locations')
      cy.get('[data-testid="view-camp-editions-btn"]').first().click()
      cy.url().should('include', '/camps/editions')
      cy.url().should('include', 'campId=')
    })
  })

  describe('Access control', () => {
    it('should redirect Member to /home when visiting /camps/editions', () => {
      cy.login('member@abuvi.org', 'password')
      cy.visit('/camps/editions')
      cy.url().should('include', '/home')
    })
  })
})
```

---

### Step 8: Update Technical Documentation

**Action**: Update `ai-specs/specs/frontend-standards.mdc` to document the query-param navigation pattern.

Add to the **Navigation Patterns** section:

```
### Deep-link Navigation with Query Params

When navigating to a management page with a pre-applied filter (e.g., from a detail page to a list page filtered by the parent entity), use Vue Router query params:

```typescript
// Navigate to editions filtered by campId
router.push({ name: 'camp-editions', query: { campId: camp.id } })

// Navigate and auto-trigger an action (e.g., open a dialog)
router.push({ name: 'camp-editions', query: { campId: camp.id, action: 'propose' } })
```

The receiving page reads query params in `onMounted` and:

1. Pre-fills filter state from `route.query.campId`
2. Opens the relevant dialog if `route.query.action` matches
3. Clears the query params after applying them via `router.replace({ name: '...' })` to avoid reactivation on refresh

```

---

## 4. Implementation Order

1. Step 0 — Create feature branch
2. Step 1 — Add routes to router (prerequisite for all navigation)
3. Step 2 — Update `CampsAdminPanel.vue` (simplest change, standalone)
4. Step 3 — Update `CampLocationDetailPage.vue` (add editions section)
5. Step 4 — Update `CampEditionsPage.vue` to handle query params
6. Step 5 — Update `CampLocationsPage.vue` (read file first, then add button)
7. Step 6 — Write unit tests for `CampsAdminPanel`
8. Step 7 — Write Cypress E2E tests
9. Step 8 — Update documentation

---

## 5. Testing Checklist

- [ ] `/camps/editions` route exists and redirects Members to `/home`
- [ ] `/camps/editions/:id` route exists and is accessible to authenticated users
- [ ] `CampsAdminPanel` shows two cards: "Ediciones de Campamento" and "Ubicaciones de Campamento"
- [ ] "Gestionar ediciones" button in AdminPanel navigates to `/camps/editions`
- [ ] "Gestionar ubicaciones" button in AdminPanel navigates to `/camps/locations`
- [ ] `CampLocationDetailPage` shows an "Ediciones" card with edition count
- [ ] "Ver ediciones" button on detail page navigates to `/camps/editions?campId={id}`
- [ ] "Nueva propuesta" button navigates to `/camps/editions?campId={id}&action=propose`
- [ ] `CampEditionsPage` pre-fills the camp filter from `?campId` on load
- [ ] `CampEditionsPage` opens proposal dialog when `?action=propose` is present
- [ ] Proposal dialog is pre-filled with the campId from the query param
- [ ] Query params are cleared from URL after being applied (no re-trigger on refresh)
- [ ] `CampLocationsPage` table/cards show "Ver ediciones" button
- [ ] "Ver ediciones" in locations list navigates to `/camps/editions?campId={id}`
- [ ] Edition count no longer duplicated in `CampLocationDetailPage` Metadata card

---

## 6. Error Handling Patterns

No new API calls introduced — this ticket is navigation-only. Error handling is in the target pages.

The only potential issue: if `CampEditionsPage.vue` does not yet exist when the user clicks navigation buttons, the router will attempt to lazy-load the component and fail. Provide a minimal placeholder page if the full implementation is not ready:

```vue
<!-- frontend/src/views/camps/CampEditionsPage.vue (placeholder) -->
<script setup lang="ts">
import Container from '@/components/ui/Container.vue'
</script>
<template>
  <Container>
    <div class="py-12 text-center text-gray-500">
      <p class="text-xl font-semibold">Gestión de Ediciones</p>
      <p class="mt-2 text-sm">Próximamente disponible.</p>
    </div>
  </Container>
</template>
```

---

## 7. UI/UX Considerations

### Admin panel layout

- Two equal-width cards in a responsive `sm:grid-cols-2` grid.
- Cards have hover shadow (`hover:shadow-md`) to signal interactivity.
- The "Gestionar ediciones" card is listed first (primary action).

### Camp location detail

- The Editions section sits below the Metadata card in the info column.
- Show the edition count in the card header (badge) — remove it from the Metadata card to avoid duplication.
- Two buttons side by side on `sm+` breakpoints, stacked on mobile (`flex-col gap-2 sm:flex-row`).
- "Nueva propuesta" uses the default (filled) button style — primary action.
- "Ver ediciones" uses `outlined` style — secondary action.

### Camp locations table

- The editions icon button (`pi pi-calendar`) is added to the existing actions group.
- Use `v-tooltip.top` for discoverability — the button has no label.
- Maintain consistent icon button size with existing actions.

### Responsive

- `CampsAdminPanel`: `grid-cols-1` on mobile → `sm:grid-cols-2`.
- `CampLocationDetailPage` edition buttons: `flex-col` → `sm:flex-row`.
- No DataTable responsiveness changes needed (action column already exists).

---

## 8. Dependencies

No new dependencies. All PrimeVue components used (`Card`, `Button`, `Tooltip`) are already installed.

---

## 9. Notes

### Critical ordering

- This ticket's router changes (Step 1) **must be done first** — every other change depends on named routes existing.
- `CampEditionsPage.vue` must exist (even as a placeholder) before committing the router entry, or the lazy import will throw a build error.

### `action=propose` URL contract

- The `action=propose` query param is a UI-level contract between `CampLocationDetailPage` and `CampEditionsPage`. It is not part of the backend API.
- The proposal dialog is part of `feat-camp-editions-management` scope. If that dialog doesn't exist yet, the `action=propose` handling should be a no-op until the dialog is implemented.

### Edition count on detail page

- `camp.editionCount` is already displayed in the Metadata card (`v-if="camp.editionCount !== undefined"`). After this ticket, move it to the new Editions card header and remove it from the Metadata card to avoid duplication.
- If `camp.editionCount` is `undefined` (backend doesn't return it for all responses), hide the badge gracefully.

### Language

- All user-facing labels in **Spanish**: "Ediciones de Campamento", "Ver ediciones", "Nueva propuesta", "Gestionar ediciones".
- All code/variables/event names in **English**.

---

## 10. Next Steps After Implementation

- Build `CampEditionsPage.vue` fully (per `feat-camp-editions-management` plan) if it was implemented as a placeholder here.
- Once the proposal dialog is built, validate the `action=propose` flow end-to-end.
- Consider adding a breadcrumb or back-navigation inside `CampEditionsPage` so users can easily return to the location detail page when they arrived via deep-link.

---

## 11. Implementation Verification

- [ ] **Code quality**: `<script setup lang="ts">`, no `any`, TypeScript strict
- [ ] **Functionality**: All navigation entry points route to `/camps/editions` with correct query params
- [ ] **Access control**: `/camps/editions` route has `requiresBoard: true`
- [ ] **Query params**: `campId` pre-fills filter; `action=propose` opens dialog; params cleared after use
- [ ] **Testing**: Unit test for `CampsAdminPanel` passes; Cypress E2E for all 3 entry points pass
- [ ] **No duplicates**: Edition count shown in one place only on detail page
- [ ] **Responsive**: Admin panel cards and detail page buttons stack on mobile
- [ ] **Documentation**: Navigation pattern documented in `frontend-standards.mdc`
