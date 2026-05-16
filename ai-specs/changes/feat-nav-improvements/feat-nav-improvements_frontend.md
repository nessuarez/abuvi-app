# Frontend Implementation Plan: feat-nav-improvements Navigation Improvements

## 1. Overview

This ticket reorganizes the app navigation in three areas:

1. **AppHeader** — adds an "Edición Actual" shortcut link for Board/Admin users pointing to the current camp edition detail page.
2. **AdminSidebar** — adds "Campamento Actual" and "Asignación de Habitaciones" to the Gestión group, and moves "Unidades Familiares" to Personas.
3. **AdminPage** — makes the hamburger button always visible on desktop (the drawer it controls remains hidden on desktop via CSS, so no functional change on desktop).

Stack: Vue 3 `<script setup lang="ts">`, Pinia, `useCampEditions` composable, PrimeVue, Tailwind CSS.

**No backend changes required.**

---

## 2. Architecture Context

### Components involved

| File | Role |
|------|------|
| `frontend/src/components/layout/AppHeader.vue` | Top navigation bar — add "Edición Actual" link |
| `frontend/src/components/admin/AdminSidebar.vue` | Admin sidebar — reorder items + add dynamic routes |
| `frontend/src/views/AdminPage.vue` | Admin shell — remove responsive hide class from hamburger |
| `frontend/src/stores/camp-editions.ts` | **New** — Pinia store caching `currentCampEdition` globally |

### State management approach

Both `AppHeader` and `AdminSidebar` need `currentCampEdition`. Calling `fetchCurrentCampEdition()` independently in both components would fire two identical `GET /camps/current` requests on each admin-panel page load.

**Solution:** introduce a thin Pinia store (`useCampEditionsStore`) that wraps the `currentCampEdition` ref and exposes a single `fetchCurrentCampEdition()` action. Both components call the same store action; Pinia ensures the state is shared and only one request is in flight at a time.

### Existing composable

`useCampEditions()` (`frontend/src/composables/useCampEditions.ts`) already has `fetchCurrentCampEdition()` which calls `GET /camps/current` and populates `currentCampEdition` (type `CurrentCampEditionResponse | null`). The store will delegate to this composable.

### Routing

No new routes are needed. The links use existing routes:
- `camp-edition-detail` → `/camps/editions/:id`
- `accommodation-assignment` → `/camps/editions/:campEditionId/assignment`

---

## 3. Implementation Steps

### Step 0: Create Feature Branch

- **Action**: Create and switch to a dedicated feature branch.
- **Branch name**: `feature/feat-nav-improvements-frontend`
- **Implementation Steps**:
  1. Ensure you are on the latest `dev` branch: `git checkout dev && git pull origin dev`
  2. Create branch: `git checkout -b feature/feat-nav-improvements-frontend`
  3. Verify: `git branch`
- **Notes**: PRs target `dev` (not `main`) per project workflow.

---

### Step 1: Create Pinia Store for Current Camp Edition

- **File**: `frontend/src/stores/camp-editions.ts` *(new file)*
- **Action**: Create a Pinia setup store that holds `currentCampEdition` and exposes `fetchCurrentCampEdition()`. Both `AppHeader` and `AdminSidebar` will consume this store instead of calling the composable directly.
- **Implementation Steps**:
  1. Create `frontend/src/stores/camp-editions.ts` with the following structure:

```typescript
import { ref } from 'vue'
import { defineStore } from 'pinia'
import { useCampEditions } from '@/composables/useCampEditions'
import type { CurrentCampEditionResponse } from '@/types/camp-edition'

export const useCampEditionsStore = defineStore('campEditions', () => {
  const { currentCampEdition, loading, error, fetchCurrentCampEdition: fetchFromComposable } = useCampEditions()

  const fetched = ref(false)

  const fetchCurrentCampEdition = async (): Promise<void> => {
    if (fetched.value) return
    fetched.value = true
    await fetchFromComposable()
  }

  return {
    currentCampEdition,
    loading,
    error,
    fetchCurrentCampEdition,
  }
})
```

  2. The `fetched` guard prevents duplicate API calls when both `AppHeader` and `AdminSidebar` call `fetchCurrentCampEdition()` on the same page load.
  3. Note: `useCampEditions()` returns a **new instance** of the composable each time it is called. The store wraps a single instance, making the refs shared through Pinia.

- **Dependencies**: `pinia`, `@/composables/useCampEditions`, `@/types/camp-edition`
- **Implementation Notes**: No new API or types — just wires the existing composable into Pinia.

---

### Step 2: Update AppHeader — Add "Edición Actual" Link

- **File**: `frontend/src/components/layout/AppHeader.vue`
- **Action**: Import the new store, fetch the current edition on mount, and add the "Edición Actual" link to both the desktop nav and the mobile nav.
- **Implementation Steps**:

  1. **Import the store and call fetch on mount**:
  ```typescript
  import { onMounted, computed } from 'vue'
  import { useCampEditionsStore } from '@/stores/camp-editions'

  const campEditionsStore = useCampEditionsStore()
  const currentEditionId = computed(() => campEditionsStore.currentCampEdition?.id ?? null)

  onMounted(() => {
    if (auth.isBoard) {
      campEditionsStore.fetchCurrentCampEdition()
    }
  })
  ```

  2. **Add a computed for the active state** (desktop nav):
  ```typescript
  const isEditionActive = (): boolean => {
    return router.currentRoute.value.path.startsWith('/camps/editions')
  }
  ```

  3. **Desktop nav block** — add between the regular links loop and the "Administración" button, guarded by `auth.isBoard && currentEditionId`:
  ```html
  <!-- Current edition shortcut (Board/Admin only) -->
  <router-link
    v-if="auth.isBoard && currentEditionId"
    :to="{ name: 'camp-edition-detail', params: { id: currentEditionId } }"
    data-testid="nav-current-edition"
    class="rounded-md px-4 py-2 text-sm font-medium transition-colors"
    :class="
      isEditionActive()
        ? 'bg-red-50 text-red-700'
        : 'border border-red-600 text-red-600 hover:bg-red-50'
    "
    :aria-current="isEditionActive() ? 'page' : undefined"
  >
    <i class="pi pi-calendar mr-1" />
    Edición Actual
  </router-link>
  ```

  4. **Mobile nav block** — add the same link inside the mobile `<nav v-if="mobileMenuOpen">` block, between the regular links and the "Administración" entry:
  ```html
  <router-link
    v-if="auth.isBoard && currentEditionId"
    :to="{ name: 'camp-edition-detail', params: { id: currentEditionId } }"
    data-testid="nav-current-edition-mobile"
    class="flex items-center gap-3 rounded-md px-4 py-3 text-sm font-medium transition-colors"
    :class="
      isEditionActive()
        ? 'bg-red-50 text-red-700'
        : 'border border-red-600 text-red-600 hover:bg-red-50'
    "
    :aria-current="isEditionActive() ? 'page' : undefined"
    @click="mobileMenuOpen = false"
  >
    <i class="pi pi-calendar" />
    Edición Actual
  </router-link>
  ```

- **Dependencies**: `useCampEditionsStore` (Step 1), `onMounted`, `computed` from Vue.
- **Implementation Notes**:
  - Only call `fetchCurrentCampEdition()` when `auth.isBoard` to avoid unnecessary API calls for regular members.
  - `navigationLinks` array is static — no changes needed there.
  - The `isEditionActive()` helper uses `startsWith('/camps/editions')` so it also highlights when on the assignment sub-page.

---

### Step 3: Update AdminSidebar — Restructure Menu Groups

- **File**: `frontend/src/components/admin/AdminSidebar.vue`
- **Action**: Import the store, resolve the dynamic routes at compute time, add two new Gestión items, move Unidades Familiares to Personas, and update `isActive()` to support prefix matching.
- **Implementation Steps**:

  1. **Add imports and store setup** at the top of `<script setup>`:
  ```typescript
  import { computed, onMounted } from 'vue'
  import { useRoute } from 'vue-router'
  import { useAuthStore } from '@/stores/auth'
  import { useCampEditionsStore } from '@/stores/camp-editions'

  const route = useRoute()
  const auth = useAuthStore()
  const campEditionsStore = useCampEditionsStore()

  onMounted(() => {
    campEditionsStore.fetchCurrentCampEdition()
  })

  const currentEditionId = computed(() => campEditionsStore.currentCampEdition?.id ?? null)
  ```

  2. **Extend `AdminMenuItem` interface** to add an optional `activePrefix` for prefix-based active detection:
  ```typescript
  interface AdminMenuItem {
    label: string
    icon: string
    to: string
    testId: string
    visible: boolean
    activePrefix?: string
  }
  ```

  3. **Update `menuGroups` computed** — full replacement:
  ```typescript
  const menuGroups = computed<AdminMenuGroup[]>(() => [
    {
      label: 'Gestión',
      items: [
        {
          label: 'Campamento Actual',
          icon: 'pi pi-calendar',
          to: currentEditionId.value ? `/camps/editions/${currentEditionId.value}` : '',
          testId: 'sidebar-current-edition',
          visible: !!currentEditionId.value,
          activePrefix: currentEditionId.value ? `/camps/editions/${currentEditionId.value}` : undefined,
        },
        {
          label: 'Asignación de Habitaciones',
          icon: 'pi pi-th-large',
          to: currentEditionId.value ? `/camps/editions/${currentEditionId.value}/assignment` : '',
          testId: 'sidebar-room-assignment',
          visible: auth.isBoard && !!currentEditionId.value,
        },
        { label: 'Campamentos', icon: 'pi pi-map', to: '/admin/camps', testId: 'sidebar-camps', visible: true },
        { label: 'Inscripciones', icon: 'pi pi-list-check', to: '/admin/registrations', testId: 'sidebar-registrations', visible: true },
      ],
    },
    {
      label: 'Personas',
      items: [
        { label: 'Usuarios', icon: 'pi pi-user-edit', to: '/admin/users', testId: 'sidebar-users', visible: true },
        { label: 'Unidades Familiares', icon: 'pi pi-users', to: '/admin/family-units', testId: 'sidebar-family-units', visible: true },
      ],
    },
    {
      label: 'Contenido',
      items: [
        { label: 'Revisión de medios', icon: 'pi pi-images', to: '/admin/media-review', testId: 'sidebar-media-review', visible: auth.isBoard },
      ],
    },
    {
      label: 'Finanzas',
      items: [
        { label: 'Pagos', icon: 'pi pi-credit-card', to: '/admin/payments', testId: 'sidebar-payments', visible: auth.isBoard },
      ],
    },
    {
      label: 'Sistema',
      items: [
        { label: 'Almacenamiento', icon: 'pi pi-database', to: '/admin/storage', testId: 'sidebar-storage', visible: auth.isAdmin },
        { label: 'Configuración', icon: 'pi pi-cog', to: '/admin/settings', testId: 'sidebar-settings', visible: auth.isBoard },
      ],
    },
  ])
  ```

  4. **Update `isActive()` to support prefix matching**:
  ```typescript
  const isActive = (item: AdminMenuItem): boolean => {
    if (item.activePrefix) return route.path.startsWith(item.activePrefix)
    return route.path === item.to
  }
  ```

  5. **Update template** — change `:class` binding to use `isActive(item)` (passing the whole item):
  ```html
  :class="
    isActive(item)
      ? 'border-l-4 border-red-600 bg-red-50 text-red-700'
      : 'border-l-4 border-transparent text-gray-700 hover:bg-gray-100 hover:text-gray-900'
  "
  ```
  Also update `aria-current`:
  ```html
  :aria-current="isActive(item) ? 'page' : undefined"
  ```

  6. **Guard dynamic `to` in router-link** — when `to` is an empty string (no current edition yet) the `router-link` must not render. This is already handled by `visible: false` filtering in `visibleGroups`, but as a safety net keep the `v-if` in `visibleGroups` (already present — no extra change needed).

- **Dependencies**: `useCampEditionsStore` (Step 1), `onMounted`, `computed`.
- **Implementation Notes**:
  - "Campamento Actual" uses `activePrefix` so it highlights on both the edition detail page and the assignment sub-page.
  - "Asignación de Habitaciones" uses exact match (no `activePrefix`) since it has its own full path.
  - Items with `visible: false` are already filtered out by `visibleGroups` — no changes to that computed.

---

### Step 4: Update AdminPage — Hamburger Always Visible

- **File**: `frontend/src/views/AdminPage.vue`
- **Action**: Remove the `md:hidden` class from the hamburger `<Button>` so it is visible on all screen sizes. The `<Drawer>` retains `class="md:hidden"`, so on desktop the drawer never renders even when `drawerVisible` becomes `true`.
- **Implementation Steps**:

  1. Find the `<Button icon="pi pi-bars">` element — currently has `class="md:hidden"`.
  2. Remove `md:hidden` from it. Keep everything else unchanged.

  **Before:**
  ```html
  <Button icon="pi pi-bars" text rounded class="md:hidden" data-testid="admin-menu-toggle"
    @click="drawerVisible = true" />
  ```

  **After:**
  ```html
  <Button icon="pi pi-bars" text rounded data-testid="admin-menu-toggle"
    @click="drawerVisible = true" />
  ```

  3. The `<Drawer class="md:hidden">` and the desktop `<AdminSidebar class="hidden md:block">` are **unchanged**.

- **Notes**: No JavaScript logic changes. On desktop, clicking the button sets `drawerVisible = true` but the `<Drawer class="md:hidden">` never mounts, so nothing visible happens. On mobile, behavior is unchanged.

---

### Step 5: Unit Tests (Vitest)

- **Files**:
  - `frontend/src/stores/__tests__/camp-editions.test.ts` *(new)*
  - `frontend/src/components/layout/__tests__/AppHeader.test.ts` *(update)*
  - `frontend/src/components/admin/__tests__/AdminSidebar.test.ts` *(update if it exists, create if not)*

#### Store test (`camp-editions.test.ts`)

```typescript
import { describe, it, expect, vi, beforeEach } from 'vitest'
import { setActivePinia, createPinia } from 'pinia'
import { useCampEditionsStore } from '@/stores/camp-editions'

vi.mock('@/composables/useCampEditions', () => ({
  useCampEditions: () => ({
    currentCampEdition: { value: null },
    loading: { value: false },
    error: { value: null },
    fetchCurrentCampEdition: vi.fn().mockResolvedValue(undefined),
  }),
}))

describe('useCampEditionsStore', () => {
  beforeEach(() => { setActivePinia(createPinia()) })

  it('should not call fetch twice on repeated calls', async () => {
    const store = useCampEditionsStore()
    await store.fetchCurrentCampEdition()
    await store.fetchCurrentCampEdition()
    // only the composable mock's fn is called once
    // verify via the composable mock spy
  })
})
```

#### AppHeader test additions

- Test that "Edición Actual" link renders when `auth.isBoard && currentEditionId` is set.
- Test that "Edición Actual" link is absent when `currentEditionId` is null.
- Test that clicking the link closes the mobile menu.

#### AdminSidebar test additions

- Test that "Campamento Actual" and "Asignación de Habitaciones" render when `currentEditionId` is set.
- Test that both items are absent when `currentEditionId` is null.
- Test that "Unidades Familiares" appears under Personas, not Gestión.
- Test prefix-based active state for "Campamento Actual".

---

### Step 6: Update Technical Documentation

- **Action**: Review and update affected documentation files.
- **Implementation Steps**:
  1. **`ai-specs/specs/frontend-standards.mdc`** — If the "Navigation Patterns" section references the admin sidebar structure, update it to reflect the new group organization.
  2. **`ai-specs/specs/api-endpoints.md`** — No changes (no new endpoints).
  3. Confirm no other spec files reference the old sidebar structure.
- **Notes**: This step is mandatory before closing the ticket.

---

## 4. Implementation Order

1. Step 0: Create feature branch
2. Step 1: Create `frontend/src/stores/camp-editions.ts`
3. Step 2: Update `AppHeader.vue`
4. Step 3: Update `AdminSidebar.vue`
5. Step 4: Update `AdminPage.vue`
6. Step 5: Write/update unit tests
7. Step 6: Update documentation

---

## 5. Testing Checklist

- [ ] "Edición Actual" appears in desktop nav for Board/Admin users when a current edition exists
- [ ] "Edición Actual" appears in mobile nav for Board/Admin users when a current edition exists
- [ ] "Edición Actual" is absent when no current edition exists
- [ ] "Edición Actual" is absent for non-Board users (regular Members)
- [ ] "Campamento Actual" is first in Gestión and navigates to `/camps/editions/:id`
- [ ] "Asignación de Habitaciones" is second in Gestión and navigates to `/camps/editions/:id/assignment`
- [ ] Both dynamic items are absent when no current edition exists
- [ ] "Unidades Familiares" is no longer in Gestión
- [ ] "Unidades Familiares" appears under Personas after "Usuarios"
- [ ] Hamburger button is visible on desktop in the admin panel
- [ ] Clicking hamburger on desktop does not open any drawer
- [ ] On mobile, clicking hamburger still opens the admin sidebar drawer
- [ ] `GET /camps/current` is called only once per page load (store deduplication)
- [ ] All new links have `data-testid` attributes
- [ ] Vitest: store deduplication test passes
- [ ] Vitest: AppHeader conditional rendering tests pass
- [ ] Vitest: AdminSidebar restructure tests pass

---

## 6. Error Handling Patterns

- If `fetchCurrentCampEdition()` fails (e.g., network error), `currentCampEdition` stays `null`. Both the "Edición Actual" link and the two dynamic sidebar items simply do not render — no error message needed in navigation UI.
- The store's `error` ref is available for debugging but is not surfaced in the UI for these navigation items.

---

## 7. UI/UX Considerations

- **"Edición Actual" link styling**: outlined red variant (`border border-red-600 text-red-600 hover:bg-red-50`) to differentiate it from the filled "Administración" button. Active state: `bg-red-50 text-red-700`.
- **Active state breadth**: "Campamento Actual" in the sidebar uses prefix matching so it stays highlighted on both the edition detail page and the assignment sub-page — the user always knows where they are in the camp context.
- **Loading state**: The nav links simply don't render while `currentCampEdition` is null (which includes the loading window). No skeleton/spinner is needed in the header.
- **Responsive**: Desktop nav (`lg:flex`), mobile nav (`lg:hidden`) — existing breakpoints are preserved.
- **Accessibility**: All new `router-link` elements include `:aria-current="isActive ? 'page' : undefined"`.

---

## 8. Dependencies

No new npm packages required. All functionality uses:
- `pinia` (already installed)
- `vue-router` (already installed)
- `@/composables/useCampEditions` (existing)
- `@/types/camp-edition` (existing `CurrentCampEditionResponse`)
- PrimeVue `Button`, `Drawer` (already used)

---

## 9. Notes

- All code must be in English (variables, functions, types, comments).
- User-facing text (labels) in Spanish: "Edición Actual", "Campamento Actual", "Asignación de Habitaciones".
- No `any` types — `currentEditionId` is `string | null`, always checked before use.
- The `fetched` guard in the store only prevents duplicate calls within the same Pinia instance lifetime (i.e., same browser session without a page reload). This is the correct scope — a full reload re-fetches as expected.
- Keep `<script setup lang="ts">` in all modified components — no Options API.
- No `<style>` blocks — Tailwind only.

---

## 10. Next Steps After Implementation

- Open PR targeting `dev` branch.
- QA verification: test on mobile viewport (375px) and desktop (1280px+) in both admin and non-admin accounts.
- Confirm with the team that the "Edición Actual" link placement (between regular links and "Administración") is acceptable from a UX perspective.

---

## 11. Implementation Verification

- [ ] TypeScript strict — no `any`, no type errors (`npx vue-tsc --noEmit`)
- [ ] All components use `<script setup lang="ts">`
- [ ] No `<style>` blocks introduced
- [ ] Composable/store communication: `AppHeader` and `AdminSidebar` both use `useCampEditionsStore`, not the composable directly
- [ ] Single `GET /camps/current` request per page load confirmed in browser Network tab
- [ ] Vitest tests pass: `npx vitest run`
- [ ] ESLint passes: `npm run lint`
- [ ] Documentation updated
