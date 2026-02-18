# Frontend Implementation Plan: feat-camps-navigation-menu — Camps Navigation Menu & Current Edition Display

## Overview

This plan covers all frontend changes needed to:

1. Consolidate navigation — replace separate admin links with a single "Administración" button visible to Board/Admin users
2. Build a tabbed Admin panel replacing the current placeholder
3. Display the current/most-recent camp edition dynamically on the `/camp` page
4. Add a "Mi Unidad Familiar" section to the Profile page

**Stack**: Vue 3 Composition API (`<script setup lang="ts">`), PrimeVue, Tailwind CSS, Vitest unit tests, Cypress E2E tests.

**TDD Note**: All new composable logic is written test-first (RED → GREEN → REFACTOR). Unit tests are written before implementing the feature, not after.

---

## Architecture Context

### Files to Modify

| File | Change |
|------|--------|
| `frontend/src/types/camp-edition.ts` | Add `registrationCount`, `availableSpots` optional fields |
| `frontend/src/types/family-unit.ts` | Add optional `representativeName`, `membersCount` for admin list view |
| `frontend/src/composables/useCampEditions.ts` | Add `fetchCurrentCampEdition()` calling `GET /api/camps/current` |
| `frontend/src/composables/useFamilyUnits.ts` | Add `fetchAllFamilyUnits()` calling `GET /api/family-units` with pagination |
| `frontend/src/components/layout/AppHeader.vue` | Remove `adminBoardLinks`, change admin button from `auth.isAdmin` to `auth.isBoard` |
| `frontend/src/router/index.ts` | Remove duplicate `family-unit` route, change `/admin` guard to `requiresBoard`, add `/family-unit/me` redirect, keep `/users` as redirect |
| `frontend/src/views/AdminPage.vue` | Replace placeholder with TabView (Campamentos / Unidades Familiares / Usuarios) |
| `frontend/src/views/CampPage.vue` | Replace static placeholder with dynamic camp edition display |
| `frontend/src/views/ProfilePage.vue` | Add "Mi Unidad Familiar" card section |

### Files to Create

| File | Purpose |
|------|---------|
| `frontend/src/components/camps/CampEditionDetails.vue` | Reusable component to display all camp edition info |
| `frontend/src/components/admin/CampsAdminPanel.vue` | Admin tab: list/manage camp editions |
| `frontend/src/components/admin/FamilyUnitsAdminPanel.vue` | Admin tab: list all family units (Board/Admin) |
| `frontend/src/components/admin/UsersAdminPanel.vue` | Admin tab: wraps existing users management |
| `frontend/cypress/e2e/camp-edition.cy.ts` | E2E tests for camp page dynamic display |
| `frontend/cypress/e2e/admin-panel.cy.ts` | E2E tests for admin tabbed panel |
| `frontend/cypress/e2e/profile-family.cy.ts` | E2E tests for profile family section |

### Key Existing Assets to Reuse

- `frontend/src/components/camps/CampStatusBadge.vue` — already exists, use on CampPage
- `frontend/src/components/camps/PricingBreakdown.vue` — already exists, use in CampEditionDetails
- `frontend/src/components/camps/CampLocationMap.vue` — use in CampEditionDetails for coordinates
- `frontend/src/components/users/UserRoleCell.vue`, `UserRoleDialog.vue`, `UserForm.vue` — reuse in UsersAdminPanel
- `frontend/src/views/FamilyUnitPage.vue` — already complete; `/family-unit/me` will redirect here
- `frontend/src/pages/UsersPage.vue` — extract logic into `UsersAdminPanel.vue`

### Routing Considerations

The router guard already handles `requiresBoard` (Admin or Board). The `/admin` route currently uses `requiresAdmin`, which is too restrictive — this needs to be changed to `requiresBoard`.

The `/family-unit` route has a **duplicate entry** (lines 67–82 of `router/index.ts`) — remove the duplicate.

### State Management

- No new Pinia stores needed
- Composable-local state is sufficient for all features
- `useAuthStore()` already provides `isBoard` and `isAdmin` computed values

---

## Implementation Steps

### Step 0: Create Feature Branch

- **Action**: Create and switch to the frontend-specific branch
- **Branch Naming**: `feature/feat-camps-navigation-menu-frontend`
- **Implementation Steps**:
  1. Ensure you are on the latest `main` branch: `git checkout main && git pull origin main`
  2. Create the branch: `git checkout -b feature/feat-camps-navigation-menu-frontend`
  3. Verify: `git branch`
- **Note**: This MUST be done before any code changes. The current branch `feature/feat-camps-navigation-menu` is the backend branch — do NOT commit frontend changes there.

---

### Step 1: Update TypeScript Types

#### 1a. Update `camp-edition.ts`

- **File**: `frontend/src/types/camp-edition.ts`
- **Action**: Add optional computed fields returned by the new `GET /api/camps/current` endpoint
- **Implementation Steps**:
  1. Add two optional fields to the existing `CampEdition` interface:

     ```typescript
     // Computed fields from backend (present in GET /api/camps/current response)
     registrationCount?: number
     availableSpots?: number
     ```

  2. The existing `camp?: Camp` relationship field is already present — keep it

#### 1b. Update `family-unit.ts`

- **File**: `frontend/src/types/family-unit.ts`
- **Action**: Add optional admin-view fields (returned only in the paginated admin list endpoint)
- **Implementation Steps**:
  1. Add optional fields to `FamilyUnitResponse`:

     ```typescript
     // Optional: populated in admin list endpoint (GET /api/family-units)
     representativeName?: string
     membersCount?: number
     ```

  2. No breaking changes — these are optional and existing usages still work

---

### Step 2: [RED] Write Failing Tests — `fetchCurrentCampEdition`

- **File**: `frontend/src/composables/__tests__/useCampEditions.test.ts` (CREATE)
- **Action**: Write failing tests FIRST before implementing the method (TDD)
- **Implementation Steps**: Create the test file with these test cases:

```typescript
import { describe, it, expect, vi, beforeEach } from 'vitest'
import { useCampEditions } from '../useCampEditions'
import { api } from '@/utils/api'

vi.mock('@/utils/api', () => ({
  api: { get: vi.fn(), post: vi.fn(), put: vi.fn(), delete: vi.fn() }
}))

describe('useCampEditions - fetchCurrentCampEdition', () => {
  beforeEach(() => vi.clearAllMocks())

  it('should set currentCampEdition when API returns a camp edition', async () => {
    const mockEdition = {
      id: 'edition-1', campId: 'camp-1', year: 2026,
      status: 'Open', startDate: '2026-07-01', endDate: '2026-07-15',
      location: 'Montaña Norte', pricePerAdult: 450, pricePerChild: 300,
      pricePerBaby: 0, useCustomAgeRanges: false, maxCapacity: 120,
      isArchived: false, createdAt: '2026-01-01T00:00:00Z', updatedAt: '2026-01-01T00:00:00Z',
      registrationCount: 45, availableSpots: 75,
      camp: { id: 'camp-1', name: 'Mountain Camp', latitude: 46.8, longitude: 8.2 }
    }
    vi.mocked(api.get).mockResolvedValueOnce({
      data: { success: true, data: mockEdition, error: null }
    })

    const { currentCampEdition, loading, error, fetchCurrentCampEdition } = useCampEditions()
    await fetchCurrentCampEdition()

    expect(currentCampEdition.value).toEqual(mockEdition)
    expect(loading.value).toBe(false)
    expect(error.value).toBeNull()
    expect(api.get).toHaveBeenCalledWith('/camps/current')
  })

  it('should set currentCampEdition to null and no error on 404', async () => {
    vi.mocked(api.get).mockRejectedValueOnce({ response: { status: 404 } })

    const { currentCampEdition, error, fetchCurrentCampEdition } = useCampEditions()
    await fetchCurrentCampEdition()

    expect(currentCampEdition.value).toBeNull()
    expect(error.value).toBeNull()
  })

  it('should set error on non-404 network failure', async () => {
    vi.mocked(api.get).mockRejectedValueOnce({
      response: { status: 500, data: { error: { message: 'Server error' } } }
    })

    const { error, fetchCurrentCampEdition } = useCampEditions()
    await fetchCurrentCampEdition()

    expect(error.value).toBe('Server error')
  })

  it('should set loading to true during fetch and false after', async () => {
    let resolvePromise!: (value: unknown) => void
    vi.mocked(api.get).mockReturnValueOnce(
      new Promise((r) => { resolvePromise = r })
    )

    const { loading, fetchCurrentCampEdition } = useCampEditions()
    const promise = fetchCurrentCampEdition()
    expect(loading.value).toBe(true)
    resolvePromise({ data: { success: true, data: null, error: null } })
    await promise
    expect(loading.value).toBe(false)
  })
})
```

- **Note**: These tests will FAIL until Step 3 implements `fetchCurrentCampEdition`. Run `npx vitest` to confirm RED state.

---

### Step 3: [GREEN] Add `fetchCurrentCampEdition` to `useCampEditions.ts`

- **File**: `frontend/src/composables/useCampEditions.ts`
- **Action**: Add the new method to the existing composable (do NOT rewrite the file)
- **Implementation Steps**:
  1. Add a new `currentCampEdition` ref at the top of the composable (alongside existing `activeEdition`):

     ```typescript
     const currentCampEdition = ref<CampEdition | null>(null)
     ```

  2. Add the `fetchCurrentCampEdition` function:

     ```typescript
     const fetchCurrentCampEdition = async (): Promise<void> => {
       loading.value = true
       error.value = null
       try {
         const response = await api.get<ApiResponse<CampEdition>>('/camps/current')
         if (response.data.success && response.data.data) {
           currentCampEdition.value = response.data.data
         } else {
           currentCampEdition.value = null
         }
       } catch (err: unknown) {
         const apiErr = err as { response?: { status?: number; data?: { error?: { message?: string } } } }
         if (apiErr?.response?.status === 404) {
           currentCampEdition.value = null
           error.value = null
         } else {
           error.value = apiErr?.response?.data?.error?.message || 'Error al cargar campamento actual'
           console.error('Failed to fetch current camp edition:', err)
         }
       } finally {
         loading.value = false
       }
     }
     ```

  3. Add `currentCampEdition` and `fetchCurrentCampEdition` to the return statement
- **Verify**: Run `npx vitest` — the new tests from Step 2 should now PASS (GREEN)

---

### Step 4: [RED] Write Failing Tests — `fetchAllFamilyUnits`

- **File**: `frontend/src/composables/__tests__/useFamilyUnits.spec.ts` (MODIFY — add new describe block at end)
- **Action**: Add tests for the new admin list method before implementing it
- **Implementation Steps**: Add a new `describe` block:

```typescript
describe('fetchAllFamilyUnits', () => {
  it('should fetch paginated family units successfully', async () => {
    const mockPagedResult = {
      items: [
        { id: 'unit-1', name: 'Garcia Family', representativeUserId: 'user-1',
          representativeName: 'Juan Garcia', membersCount: 4,
          createdAt: '2026-01-01T00:00:00Z', updatedAt: '2026-01-01T00:00:00Z' }
      ],
      totalCount: 1, page: 1, pageSize: 20, totalPages: 1,
      hasNextPage: false, hasPreviousPage: false
    }
    vi.mocked(api.get).mockResolvedValueOnce({
      data: { success: true, data: mockPagedResult, error: null }
    })

    const { allFamilyUnits, fetchAllFamilyUnits } = useFamilyUnits()
    await fetchAllFamilyUnits()

    expect(allFamilyUnits.value).toEqual(mockPagedResult.items)
    expect(api.get).toHaveBeenCalledWith(expect.stringContaining('/family-units'))
    expect(api.get).toHaveBeenCalledWith(expect.stringContaining('page=1'))
  })

  it('should pass search parameter when provided', async () => {
    vi.mocked(api.get).mockResolvedValueOnce({
      data: { success: true, data: { items: [], totalCount: 0, page: 1, pageSize: 20, totalPages: 0, hasNextPage: false, hasPreviousPage: false }, error: null }
    })

    const { fetchAllFamilyUnits } = useFamilyUnits()
    await fetchAllFamilyUnits({ search: 'Garcia' })

    expect(api.get).toHaveBeenCalledWith(expect.stringContaining('search=Garcia'))
  })

  it('should set error message on failure', async () => {
    vi.mocked(api.get).mockRejectedValueOnce({
      response: { data: { error: { message: 'No autorizado' } } }
    })

    const { error, fetchAllFamilyUnits } = useFamilyUnits()
    await fetchAllFamilyUnits()

    expect(error.value).toBe('No autorizado')
  })
})
```

- **Note**: These will FAIL — `allFamilyUnits` and `fetchAllFamilyUnits` don't exist yet.

---

### Step 5: [GREEN] Add `fetchAllFamilyUnits` to `useFamilyUnits.ts`

- **File**: `frontend/src/composables/useFamilyUnits.ts`
- **Action**: Add admin list capability to existing composable
- **Implementation Steps**:
  1. Add imports at the top — ensure `PagedResult` is imported:

     ```typescript
     import type { PagedResult } from '@/types/api'
     ```

  2. Add new state refs alongside existing ones:

     ```typescript
     const allFamilyUnits: Ref<FamilyUnitResponse[]> = ref([])
     const familyUnitsPagination = ref({
       totalCount: 0, page: 1, pageSize: 20, totalPages: 0
     })
     ```

  3. Add the function:

     ```typescript
     const fetchAllFamilyUnits = async (params: {
       page?: number
       pageSize?: number
       search?: string
       sortBy?: string
       sortOrder?: 'asc' | 'desc'
     } = {}): Promise<void> => {
       loading.value = true
       error.value = null
       try {
         const queryParams = new URLSearchParams({
           page: String(params.page ?? 1),
           pageSize: String(params.pageSize ?? 20)
         })
         if (params.search) queryParams.set('search', params.search)
         if (params.sortBy) queryParams.set('sortBy', params.sortBy)
         if (params.sortOrder) queryParams.set('sortOrder', params.sortOrder)

         const response = await api.get<ApiResponse<PagedResult<FamilyUnitResponse>>>(
           `/family-units?${queryParams.toString()}`
         )
         if (response.data.success && response.data.data) {
           allFamilyUnits.value = response.data.data.items
           familyUnitsPagination.value = {
             totalCount: response.data.data.totalCount,
             page: response.data.data.page,
             pageSize: response.data.data.pageSize,
             totalPages: response.data.data.totalPages
           }
         }
       } catch (err: unknown) {
         const apiErr = err as { response?: { data?: { error?: { message?: string } } } }
         error.value = apiErr?.response?.data?.error?.message || 'Error al obtener unidades familiares'
       } finally {
         loading.value = false
       }
     }
     ```

  4. Add `allFamilyUnits`, `familyUnitsPagination`, and `fetchAllFamilyUnits` to the return statement
- **Verify**: Run `npx vitest` — all tests should pass (GREEN)

---

### Step 6: Update `AppHeader.vue`

- **File**: `frontend/src/components/layout/AppHeader.vue`
- **Action**: Remove `adminBoardLinks`, update "Administración" button to use `auth.isBoard`
- **Implementation Steps**:
  1. Remove the `adminBoardLinks` array (lines 20–22):

     ```typescript
     // DELETE these lines:
     const adminBoardLinks = [
       { label: 'Usuarios', path: '/users', icon: 'pi pi-users' }
     ]
     ```

  2. In the **Desktop Navigation** section, remove the `v-for` block that renders `adminBoardLinks` (approximately lines 64–77):

     ```html
     <!-- DELETE this entire block -->
     <router-link
       v-for="link in adminBoardLinks"
       v-if="auth.isBoard"
       ...
     >
     ```

  3. Change the admin button condition from `v-if="auth.isAdmin"` to `v-if="auth.isBoard"` (desktop, line ~80):

     ```html
     <!-- BEFORE -->
     <router-link v-if="auth.isAdmin" to="/admin" ...>
     <!-- AFTER -->
     <router-link v-if="auth.isBoard" to="/admin" ...>
     ```

  4. In the **Mobile Navigation** section, remove the `adminBoardLinks` v-for block (approximately lines 131–147)
  5. Change the mobile admin button condition from `v-if="auth.isAdmin"` to `v-if="auth.isBoard"` (line ~150)
  6. The admin button styling (red background) should remain — it visually signals special access
- **Result**: Only one "Administración" button visible to Board AND Admin users, no separate "Usuarios" link

---

### Step 7: Update `router/index.ts`

- **File**: `frontend/src/router/index.ts`
- **Action**: Fix duplicate route, change admin guard, add family-unit/me redirect
- **Implementation Steps**:
  1. **Remove the duplicate `family-unit` route** (lines 75–82 are identical to lines 67–73 — delete one)
  2. **Change `/admin` route meta** from `requiresAdmin: true` to `requiresBoard: true`:

     ```typescript
     // BEFORE
     meta: { requiresAuth: true, requiresAdmin: true, title: 'ABUVI | Administración' }
     // AFTER
     meta: { requiresAuth: true, requiresBoard: true, title: 'ABUVI | Administración' }
     ```

  3. **Add `/family-unit/me` as a redirect** to the existing `/family-unit` page (add after the existing `/family-unit` route):

     ```typescript
     {
       path: '/family-unit/me',
       redirect: '/family-unit'
     }
     ```

  4. **Convert `/users` to a redirect** to `/admin` (instead of removing, to maintain backward compatibility):

     ```typescript
     // REPLACE the current /users route with:
     {
       path: '/users',
       redirect: '/admin'
     }
     // REPLACE the current /users/:id route with:
     {
       path: '/users/:id',
       redirect: '/admin'
     }
     ```

  5. **Verify the route guard** — the guard at lines 180–184 already handles `requiresAdmin`, and lines 186–190 handle `requiresBoard`. Both guards remain. The `/admin` route will now use `requiresBoard`.

---

### Step 8: Create `CampEditionDetails.vue`

- **File**: `frontend/src/components/camps/CampEditionDetails.vue` (NEW)
- **Action**: Reusable component to display all camp edition information in a structured layout
- **Props**: `campEdition: CampEdition`
- **Implementation Steps**:
  1. Create component with `<script setup lang="ts">`:

     ```typescript
     import type { CampEdition } from '@/types/camp-edition'
     import Card from 'primevue/card'
     import PricingBreakdown from '@/components/camps/PricingBreakdown.vue'
     import CampLocationMap from '@/components/camps/CampLocationMap.vue'

     interface Props { campEdition: CampEdition }
     const props = defineProps<Props>()

     const formatDate = (dateStr: string) =>
       new Date(dateStr).toLocaleDateString('es-ES', {
         year: 'numeric', month: 'long', day: 'numeric'
       })
     ```

  2. Template structure (no `<style>` blocks, Tailwind only):
     - **Dates Card**: Start date, end date, duration in days (computed)
     - **Location Card**: Location text + `CampLocationMap` (only if `campEdition.camp?.latitude` exists)
     - **Description Card**: Camp description (if present)
     - **Pricing Card**: Use existing `PricingBreakdown` component
     - **Capacity Card**: Max capacity, current registrations, available spots (if `registrationCount` and `availableSpots` are present)
     - **Contact Card**: Email, phone (if present)
  3. All labels in Spanish, component name and variables in English
  4. Responsive layout: `grid grid-cols-1 gap-4 md:grid-cols-2`

---

### Step 9: Update `CampPage.vue`

- **File**: `frontend/src/views/CampPage.vue`
- **Action**: Replace static placeholder with dynamic current camp edition display
- **Implementation Steps**:
  1. Rewrite the entire file:

     ```typescript
     import { ref, onMounted, computed } from 'vue'
     import { useCampEditions } from '@/composables/useCampEditions'
     import Container from '@/components/ui/Container.vue'
     import CampEditionDetails from '@/components/camps/CampEditionDetails.vue'
     import CampStatusBadge from '@/components/camps/CampStatusBadge.vue'
     import Card from 'primevue/card'
     import Message from 'primevue/message'
     import ProgressSpinner from 'primevue/progressspinner'

     const { currentCampEdition, loading, error, fetchCurrentCampEdition } = useCampEditions()

     onMounted(() => { fetchCurrentCampEdition() })

     const displayTitle = computed(() =>
       currentCampEdition.value ? `Campamento ${currentCampEdition.value.year}` : 'Campamento'
     )

     const isFromPreviousYear = computed(() => {
       if (!currentCampEdition.value) return false
       return currentCampEdition.value.year < new Date().getFullYear()
     })
     ```

  2. Template sections:
     - **Loading**: `<ProgressSpinner />` centered while `loading`
     - **Error**: `<Message severity="error">` with error message
     - **Empty**: `<Message severity="info">` — "No hay información de campamento disponible para este año. Contacta con la junta directiva para más información."
     - **Content** (when `currentCampEdition` exists):
       - Page title: `<h1>{{ displayTitle }}</h1>`
       - `<CampStatusBadge :status="currentCampEdition.status" />`
       - Previous year warning: `<Message v-if="isFromPreviousYear" severity="warn">Mostrando información del campamento de {{ currentCampEdition.year }}</Message>`
       - `<CampEditionDetails :camp-edition="currentCampEdition" />`
       - Registration CTA card (if `status === 'Open'`): Show available spots and a link to future `/registrations/new` route
  3. Add `data-testid="camp-page-content"`, `data-testid="camp-loading"`, `data-testid="camp-empty"` for Cypress

---

### Step 10: Update `AdminPage.vue`

- **File**: `frontend/src/views/AdminPage.vue`
- **Action**: Replace placeholder with tabbed interface using PrimeVue TabView
- **Implementation Steps**:
  1. Rewrite with TabView:

     ```typescript
     import { ref } from 'vue'
     import TabView from 'primevue/tabview'
     import TabPanel from 'primevue/tabpanel'
     import Container from '@/components/ui/Container.vue'
     import CampsAdminPanel from '@/components/admin/CampsAdminPanel.vue'
     import FamilyUnitsAdminPanel from '@/components/admin/FamilyUnitsAdminPanel.vue'
     import UsersAdminPanel from '@/components/admin/UsersAdminPanel.vue'

     const activeTab = ref(0)
     ```

  2. Three tabs: "Campamentos" (tab 0, default), "Unidades Familiares" (tab 1), "Usuarios" (tab 2)
  3. Each tab header has an icon + label using `<template #header>`
  4. Page heading: `<h1>Panel de Administración</h1>`
  5. Add `data-testid` on TabView and each TabPanel for Cypress

---

### Step 11: Create `CampsAdminPanel.vue`

- **File**: `frontend/src/components/admin/CampsAdminPanel.vue` (NEW)
- **Action**: Admin panel for camps and editions management (start with functional list + basic actions)
- **Implementation Steps**:
  1. Use `useCampEditions` composable to `getEditionById` and `changeStatus`
  2. Use `useCamps` composable for listing camp locations
  3. Display a DataTable of camp editions (call existing `fetchProposedEditions` or add a new `fetchAllEditions` — **check if backend has `GET /api/camps/editions` without year filter**)
  4. **If `GET /api/camps/editions` (all, no year filter) does not exist yet** in the backend, display a placeholder card: "Gestión de campamentos - en desarrollo" with a link to `/camps/locations` for camp location management
  5. Include a "Ver ubicaciones" button linking to `/camps/locations` (already implemented)
  6. Columns: Nombre, Año, Fechas, Estado (with `CampStatusBadge`), Capacidad
  7. Status change dropdown per row
  8. Add `data-testid="camps-admin-panel"` for Cypress

**Important**: If the backend does not yet have a general camp editions list endpoint (only proposed/active), use a simple placeholder. Do NOT call an endpoint that doesn't exist. Coordinate with the backend plan first.

---

### Step 12: Create `FamilyUnitsAdminPanel.vue`

- **File**: `frontend/src/components/admin/FamilyUnitsAdminPanel.vue` (NEW)
- **Action**: Admin panel to list and manage all family units
- **Implementation Steps**:
  1. Import composable: `const { allFamilyUnits, familyUnitsPagination, loading, error, fetchAllFamilyUnits } = useFamilyUnits()`
  2. Call `fetchAllFamilyUnits()` in `onMounted`
  3. Search input with debounce (300ms, use `@vueuse/core` `useDebounceFn` — already installed):

     ```typescript
     import { useDebounceFn } from '@vueuse/core'
     const searchQuery = ref('')
     const debouncedSearch = useDebounceFn((val: string) => {
       fetchAllFamilyUnits({ search: val, page: 1 })
     }, 300)
     watch(searchQuery, debouncedSearch)
     ```

  4. DataTable with PrimeVue, lazy pagination (`@page` event triggers `fetchAllFamilyUnits` with new page)
  5. Columns: Nombre Familia, Representante (`representativeName`), Miembros (`membersCount`), Fecha Creación
  6. A "Ver detalle" button per row (future: navigate to family unit detail)
  7. Error handling: `<Message severity="error">` + retry button
  8. Add `data-testid="family-units-admin-panel"` for Cypress

---

### Step 13: Create `UsersAdminPanel.vue`

- **File**: `frontend/src/components/admin/UsersAdminPanel.vue` (NEW)
- **Action**: Migrate users management from `UsersPage.vue` into a reusable admin panel component
- **Implementation Steps**:
  1. Copy the `<script setup>` logic from `frontend/src/pages/UsersPage.vue` — it is already well-structured
  2. Remove the router-push to `/users/:id` (no longer a standalone page); replace with a Dialog showing user details instead, OR keep the eye button but note it will redirect to `/admin`
  3. Keep all existing functionality: DataTable, create dialog, role management dialog
  4. Use the same `UserForm`, `UserRoleCell`, `UserRoleDialog` components
  5. Remove the `<div class="container mx-auto p-4">` outer wrapper (AdminPage provides the container)
  6. Update heading from "User Management" (English) to "Gestión de Usuarios"
  7. Add `data-testid="users-admin-panel"` for Cypress
  8. The old `UsersPage.vue` in `/pages/` can remain for now (accessed via redirect) but is effectively deprecated

---

### Step 14: Update `ProfilePage.vue`

- **File**: `frontend/src/views/ProfilePage.vue`
- **Action**: Add "Mi Unidad Familiar" card section below the user info card
- **Implementation Steps**:
  1. Add composable and router imports:

     ```typescript
     import { onMounted } from 'vue'
     import { useRouter } from 'vue-router'
     import { useFamilyUnits } from '@/composables/useFamilyUnits'
     import Card from 'primevue/card'
     import Button from 'primevue/button'

     const router = useRouter()
     const { familyUnit, loading: familyLoading, getCurrentUserFamilyUnit } = useFamilyUnits()

     onMounted(() => { getCurrentUserFamilyUnit() })
     const goToFamilyManagement = () => router.push('/family-unit/me')
     ```

  2. Add a card below the existing profile card:

     ```html
     <!-- Mi Unidad Familiar -->
     <Card class="mt-6">
       <template #title>
         <div class="flex items-center gap-2">
           <i class="pi pi-users" />
           Mi Unidad Familiar
         </div>
       </template>
       <template #content>
         <!-- Loading -->
         <div v-if="familyLoading" class="flex justify-center py-4">
           <i class="pi pi-spin pi-spinner text-2xl text-primary-500" />
         </div>
         <!-- No Family Unit -->
         <div v-else-if="!familyUnit" class="space-y-3 text-center py-4">
           <p class="text-gray-600 text-sm">
             Aún no has creado tu unidad familiar. Crea una para poder inscribirte en campamentos.
           </p>
           <Button
             label="Crear Unidad Familiar"
             icon="pi pi-plus"
             data-testid="create-family-unit-btn"
             @click="goToFamilyManagement"
           />
         </div>
         <!-- Has Family Unit -->
         <div v-else class="flex items-center justify-between">
           <div>
             <h3 class="text-lg font-semibold">{{ familyUnit.name }}</h3>
             <p class="text-sm text-gray-500">Unidad familiar activa</p>
           </div>
           <Button
             label="Gestionar"
             icon="pi pi-pencil"
             outlined
             data-testid="manage-family-unit-btn"
             @click="goToFamilyManagement"
           />
         </div>
       </template>
     </Card>
     ```

  3. Fix the "Role:" label to show role in Spanish (use `translateRole` helper):

     ```typescript
     const translateRole = (role: string) =>
       ({ Admin: 'Administrador', Board: 'Junta Directiva', Member: 'Socio' }[role] ?? role)
     ```

---

### Step 15: Write Cypress E2E Tests

#### 15a. `camp-edition.cy.ts`

- **File**: `frontend/cypress/e2e/camp-edition.cy.ts` (NEW)
- **Implementation Steps**:

  ```typescript
  describe('Camp Edition Page (/camp)', () => {
    beforeEach(() => { cy.login('member@abuvi.org', 'password123') })

    it('shows loading state while fetching', () => {
      cy.intercept('GET', '/api/camps/current', (req) => { req.reply({ delay: 2000 }) })
      cy.visit('/camp')
      cy.get('[data-testid="camp-loading"]').should('be.visible')
    })

    it('displays current year open camp edition with title and status', () => {
      cy.intercept('GET', '/api/camps/current', { fixture: 'camp-edition-open.json' }).as('getCurrent')
      cy.visit('/camp')
      cy.wait('@getCurrent')
      cy.get('h1').should('contain.text', 'Campamento 2026')
    })

    it('shows warning when displaying previous year camp', () => {
      cy.intercept('GET', '/api/camps/current', { fixture: 'camp-edition-2025.json' })
      cy.visit('/camp')
      cy.contains('Mostrando información del campamento de 2025').should('be.visible')
    })

    it('shows info message when no camp edition exists (404)', () => {
      cy.intercept('GET', '/api/camps/current', { statusCode: 404, body: { success: false, data: null, error: { message: 'Not found', code: 'NOT_FOUND' } } })
      cy.visit('/camp')
      cy.get('[data-testid="camp-empty"]').should('be.visible')
      cy.contains('No hay información de campamento disponible').should('be.visible')
    })

    it('shows registration CTA when status is Open', () => {
      cy.intercept('GET', '/api/camps/current', { fixture: 'camp-edition-open.json' })
      cy.visit('/camp')
      cy.contains('Inscripciones Abiertas').should('be.visible')
    })
  })
  ```

- **Fixtures to create**: `frontend/cypress/fixtures/camp-edition-open.json`, `frontend/cypress/fixtures/camp-edition-2025.json`

#### 15b. `admin-panel.cy.ts`

- **File**: `frontend/cypress/e2e/admin-panel.cy.ts` (NEW)
- **Implementation Steps**:

  ```typescript
  describe('Admin Panel (/admin)', () => {
    it('board user can access admin panel', () => {
      cy.login('board@abuvi.org', 'password123')
      cy.visit('/admin')
      cy.get('h1').should('contain.text', 'Panel de Administración')
    })

    it('regular member is redirected away from /admin', () => {
      cy.login('member@abuvi.org', 'password123')
      cy.visit('/admin')
      cy.url().should('include', '/home')
    })

    it('navigation shows Administración button for board users', () => {
      cy.login('board@abuvi.org', 'password123')
      cy.visit('/home')
      cy.contains('Administración').should('be.visible')
    })

    it('navigation does NOT show Administración for regular members', () => {
      cy.login('member@abuvi.org', 'password123')
      cy.visit('/home')
      cy.contains('Administración').should('not.exist')
    })

    it('admin panel shows three tabs', () => {
      cy.login('board@abuvi.org', 'password123')
      cy.visit('/admin')
      cy.contains('Campamentos').should('be.visible')
      cy.contains('Unidades Familiares').should('be.visible')
      cy.contains('Usuarios').should('be.visible')
    })

    it('clicking Usuarios tab shows user management', () => {
      cy.login('board@abuvi.org', 'password123')
      cy.visit('/admin')
      cy.contains('Usuarios').click()
      cy.get('[data-testid="users-admin-panel"]').should('be.visible')
    })
  })
  ```

#### 15c. `profile-family.cy.ts`

- **File**: `frontend/cypress/e2e/profile-family.cy.ts` (NEW)
- **Implementation Steps**:

  ```typescript
  describe('Profile Page - Family Unit Section', () => {
    it('shows create button when user has no family unit', () => {
      cy.intercept('GET', '/api/family-units/me', { statusCode: 404 })
      cy.login('member@abuvi.org', 'password123')
      cy.visit('/profile')
      cy.get('[data-testid="create-family-unit-btn"]').should('be.visible')
    })

    it('shows manage button when user has a family unit', () => {
      cy.intercept('GET', '/api/family-units/me', { fixture: 'family-unit.json' })
      cy.login('member@abuvi.org', 'password123')
      cy.visit('/profile')
      cy.get('[data-testid="manage-family-unit-btn"]').should('be.visible')
    })

    it('clicking Gestionar navigates to /family-unit', () => {
      cy.intercept('GET', '/api/family-units/me', { fixture: 'family-unit.json' })
      cy.login('member@abuvi.org', 'password123')
      cy.visit('/profile')
      cy.get('[data-testid="manage-family-unit-btn"]').click()
      cy.url().should('include', '/family-unit')
    })
  })
  ```

- **Fixture to create**: `frontend/cypress/fixtures/family-unit.json`

---

### Step 16: Update Documentation

- **Files to update**:
  1. `ai-specs/specs/api-spec.yml` — document new `GET /api/camps/current` endpoint (if it's not already there from the backend plan)
  2. `ai-specs/specs/frontend-standards.mdc` — add note under "Navigation Patterns" about the consolidated admin navigation pattern
- **Implementation Steps**:
  1. Check if `GET /api/camps/current` is already documented in `api-spec.yml`; if not, add the endpoint documentation
  2. No major standards changes needed — existing patterns are followed

---

## Implementation Order

Follow this exact sequence to minimize blockers and respect TDD:

1. **Step 0** — Create feature branch (`feature/feat-camps-navigation-menu-frontend`)
2. **Step 1** — Update TypeScript types (`camp-edition.ts`, `family-unit.ts`)
3. **Step 2** — Write failing tests for `fetchCurrentCampEdition` [RED]
4. **Step 3** — Implement `fetchCurrentCampEdition` in `useCampEditions.ts` [GREEN]
5. **Step 4** — Write failing tests for `fetchAllFamilyUnits` [RED]
6. **Step 5** — Implement `fetchAllFamilyUnits` in `useFamilyUnits.ts` [GREEN]
7. **Step 6** — Update `AppHeader.vue` (navigation)
8. **Step 7** — Update `router/index.ts` (routing)
9. **Step 8** — Create `CampEditionDetails.vue` component
10. **Step 9** — Update `CampPage.vue` (dynamic content)
11. **Step 10** — Update `AdminPage.vue` (tabbed interface)
12. **Step 11** — Create `CampsAdminPanel.vue`
13. **Step 12** — Create `FamilyUnitsAdminPanel.vue`
14. **Step 13** — Create `UsersAdminPanel.vue`
15. **Step 14** — Update `ProfilePage.vue` (family section)
16. **Step 15** — Write Cypress E2E tests and Cypress fixtures
17. **Step 16** — Update documentation

---

## Testing Checklist

### Unit Tests (Vitest)

- [ ] `useCampEditions.fetchCurrentCampEdition` — happy path (returns edition)
- [ ] `useCampEditions.fetchCurrentCampEdition` — 404 response (no error, null edition)
- [ ] `useCampEditions.fetchCurrentCampEdition` — non-404 error (error message set)
- [ ] `useCampEditions.fetchCurrentCampEdition` — loading state management
- [ ] `useFamilyUnits.fetchAllFamilyUnits` — happy path (returns paged result)
- [ ] `useFamilyUnits.fetchAllFamilyUnits` — search parameter passed correctly
- [ ] `useFamilyUnits.fetchAllFamilyUnits` — error handling
- [ ] Run: `npx vitest --coverage` — confirm >90% coverage on modified files

### Component Tests

- [ ] `CampEditionDetails` renders dates, location, pricing correctly
- [ ] `CampEditionDetails` hides map when no coordinates
- [ ] `CampEditionDetails` hides description when empty

### E2E Tests (Cypress)

- [ ] `/camp` page shows loading, then content
- [ ] `/camp` page shows warning for previous-year camp
- [ ] `/camp` page shows empty message on 404
- [ ] `/camp` page shows registration CTA for open edition
- [ ] Board user can access `/admin` with all three tabs
- [ ] Regular member is redirected from `/admin` to `/home`
- [ ] Navigation shows "Administración" only for Board/Admin
- [ ] `/users` redirect goes to `/admin`
- [ ] Profile shows family unit section with correct state
- [ ] Profile "Gestionar" button navigates to `/family-unit`

---

## Error Handling Patterns

All composable methods follow the established project pattern:

```typescript
const fetchCurrentCampEdition = async (): Promise<void> => {
  loading.value = true
  error.value = null        // Always clear previous errors
  try {
    // ... API call
  } catch (err: unknown) {
    // Type-safe error extraction — NO any
    const apiErr = err as { response?: { status?: number; data?: { error?: { message?: string } } } }
    if (apiErr?.response?.status === 404) {
      // 404 is NOT an error — it's a valid state (no camp edition exists)
      error.value = null
    } else {
      error.value = apiErr?.response?.data?.error?.message || 'Mensaje de error por defecto en español'
      console.error('Description in English for developer debugging:', err)
    }
  } finally {
    loading.value = false   // Always reset loading
  }
}
```

**User-facing messages**: Always in Spanish. Use `PrimeVue Message` component (not Toast) for inline errors in pages. Use Toast for transient feedback after actions.

---

## UI/UX Considerations

### PrimeVue Components Used

- `TabView` / `TabPanel` — Admin panel tabs (ensure `primevue/tabview` is registered in `main.ts`)
- `DataTable` / `Column` — Family units admin list, users admin list
- `Card` — Camp edition details sections, profile family section
- `Message` — Error and empty state display on CampPage
- `ProgressSpinner` — Loading state on CampPage
- `Button` — All CTAs
- `InputText` — Search field in FamilyUnitsAdminPanel

### Check PrimeVue Registration

Before using `TabView`, verify it is registered globally in `frontend/src/main.ts`. If not, add:

```typescript
import TabView from 'primevue/tabview'
import TabPanel from 'primevue/tabpanel'
app.component('TabView', TabView)
app.component('TabPanel', TabPanel)
```

### Tailwind CSS

- No `<style>` blocks anywhere
- Responsive: Mobile-first with `sm:`, `md:`, `lg:` breakpoints
- Admin panel tabs: full-width on mobile, horizontal tabs on desktop

### Accessibility

- All `<Button>` elements with icon-only must have `aria-label`
- Tab navigation must work with keyboard (PrimeVue TabView handles this natively)
- `<Message>` components have `:closable="false"` for persistent state messages
- Add `role="status"` to loading spinners

---

## Dependencies

All required packages are already installed:

- `primevue` — TabView, DataTable, Card, Message, Button, Dialog, ProgressSpinner, InputText
- `@vueuse/core` — `useDebounceFn` for search debouncing in FamilyUnitsAdminPanel
- `pinia` — auth store (already in use)
- `vue-router` — routing (already in use)
- `axios` — via `@/utils/api` (already in use)

**No new npm packages needed.**

---

## Notes

1. **TDD is mandatory**: Write tests in Steps 2 and 4 BEFORE implementing in Steps 3 and 5. Verify RED state before implementing.
2. **`CampsAdminPanel`**: The backend may not yet have `GET /api/camps/editions` (all editions, no year filter). Coordinate with the backend plan. If the endpoint isn't ready, display a placeholder with a link to `/camps/locations`.
3. **`FamilyUnitsAdminPanel`**: The backend `GET /api/family-units` (admin, paginated) must be confirmed implemented before full implementation. Check the backend plan.
4. **Duplicate route**: The existing router has two identical `/family-unit` route entries. This is a bug — fix it in Step 7.
5. **`auth.isAdmin` vs `auth.isBoard`**: `isBoard` returns `true` for both Admin AND Board roles (`user.value?.role === 'Admin' || user.value?.role === 'Board'`). This is correct — using `isBoard` includes admins.
6. **User-facing text**: All UI labels, buttons, messages, and headings must be in Spanish. Code (variables, functions, types) must be in English.
7. **No `any` types**: Use proper TypeScript types or `unknown` with explicit casting.
8. **`FamilyUnitPage.vue`** at `/family-unit` is already fully implemented — no changes needed there. `/family-unit/me` simply redirects to it.
9. **`CampEditionStatus`**: The existing type includes `'Proposed'` in addition to `'Draft' | 'Open' | 'Closed' | 'Completed'`. Handle `'Proposed'` in `CampStatusBadge` display if not already handled.

---

## Next Steps After Implementation

1. Coordinate with backend to verify `GET /api/camps/current` is deployed
2. Coordinate with backend to verify `GET /api/family-units` (paginated admin list) is deployed
3. Verify `GET /api/family-units` returns `representativeName` and `membersCount` — if not, hide those columns from `FamilyUnitsAdminPanel`
4. Once registration feature is built, update the "Inscribir mi familia" CTA link on `CampPage.vue`
5. After stabilization, remove the deprecated `/pages/UsersPage.vue` and `/pages/UserDetailPage.vue`

---

## Implementation Verification

Final checklist before marking this feature as complete:

- **Code Quality**
  - [ ] All components use `<script setup lang="ts">`
  - [ ] No `any` types — use `unknown` + explicit casting
  - [ ] No `<style>` blocks — Tailwind only
  - [ ] All API calls go through composables, never directly from components

- **Functionality**
  - [ ] Navigation shows "Administración" for Board/Admin, hidden for Members
  - [ ] `/admin` accessible to Board AND Admin, redirects Members to `/home`
  - [ ] Admin panel has three working tabs
  - [ ] `/camp` page loads current edition dynamically from API
  - [ ] `/camp` page handles loading, error, empty, and previous-year states
  - [ ] Profile page shows family unit section with correct state
  - [ ] `/family-unit/me` redirects to `/family-unit`
  - [ ] `/users` redirects to `/admin`
  - [ ] Duplicate route in router is fixed

- **Testing**
  - [ ] All Vitest unit tests pass: `npx vitest`
  - [ ] Coverage >90% on modified composables: `npx vitest --coverage`
  - [ ] All Cypress E2E tests pass: `npx cypress run`

- **Integration**
  - [ ] Composables connect to backend endpoints correctly (verify with running backend)
  - [ ] Auth guard prevents non-board users from accessing `/admin`

- **Documentation**
  - [ ] `api-spec.yml` updated with `GET /api/camps/current`
  - [ ] No outdated documentation references to removed routes or components
