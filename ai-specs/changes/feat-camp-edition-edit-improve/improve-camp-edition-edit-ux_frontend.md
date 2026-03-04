# Frontend Implementation Plan: improve-camp-edition-edit-ux — Improve CampEdition Edit Page UX

## Overview

Replace the cramped `CampEditionUpdateDialog` modal with a dedicated full-page edit view at `/camps/editions/:id/edit`. The new page reorganizes the form with the description as the primary field, adds sensible date defaults (Aug 15–30), and provides a visual week-range indicator for the half-date cutoff. No backend changes required — the existing `GET /api/camps/editions/{id}` and `PUT /api/camps/editions/{id}` endpoints are sufficient.

Architecture: Vue 3 Composition API, PrimeVue components, Tailwind CSS utilities, composable-based API communication via `useCampEditions`.

## Architecture Context

### Components / Files Involved

| File | Role |
|------|------|
| `frontend/src/views/camps/CampEditionEditPage.vue` | **NEW** — Full-page edit form |
| `frontend/src/views/camps/CampEditionsPage.vue` | List page — remove dialog, navigate to edit page |
| `frontend/src/views/camps/CampEditionDetailPage.vue` | Detail page — add "Editar" button |
| `frontend/src/components/camps/CampEditionUpdateDialog.vue` | **DELETE** — replaced by the new page |
| `frontend/src/router/index.ts` | Add `/camps/editions/:id/edit` route |
| `frontend/src/composables/useCampEditions.ts` | Reused as-is (`getEditionById`, `updateEdition`) |
| `frontend/src/types/camp-edition.ts` | Reused as-is (`CampEdition`, `UpdateCampEditionRequest`) |

### Routing

New route `/camps/editions/:id/edit` with `requiresAuth: true` + `requiresBoard: true`. Must be placed **before** the catch-all `/camps/editions/:id` in the router config to avoid route conflicts.

### State Management

Local component state only (`reactive` form + `ref` for edition data). No Pinia store needed — data is fetched on mount and submitted on save.

---

## Implementation Steps

### Step 0: Create Feature Branch

- **Action**: Create and switch to a new feature branch
- **Branch Naming**: `feature/improve-camp-edition-edit-ux-frontend`
- **Implementation Steps**:
  1. Ensure on latest `dev` branch: `git checkout dev && git pull origin dev`
  2. Create new branch: `git checkout -b feature/improve-camp-edition-edit-ux-frontend`
  3. Verify: `git branch`

---

### Step 1: Add Route for Edit Page

- **File**: `frontend/src/router/index.ts`
- **Action**: Register the new `/camps/editions/:id/edit` route
- **Implementation Steps**:
  1. Add the route entry **after** the existing `camp-edition-detail` route (line ~121):

     ```typescript
     // Camp Edition Edit (Board only)
     {
       path: '/camps/editions/:id/edit',
       name: 'camp-edition-edit',
       component: () => import('@/views/camps/CampEditionEditPage.vue'),
       meta: {
         title: 'ABUVI | Editar Edición',
         requiresAuth: true,
         requiresBoard: true
       }
     },
     ```

  2. **Important**: This route MUST be placed **before** `/camps/editions/:id` in the file because Vue Router matches routes in order. Move the `:id/edit` route above the `:id` route to prevent `:id` from catching `edit` as a param value.

     Final order should be:
     ```
     /camps/editions          → CampEditionsPage (list)
     /camps/editions/:id/edit → CampEditionEditPage (edit) ← NEW
     /camps/editions/:id      → CampEditionDetailPage (detail)
     ```

- **Dependencies**: None
- **Implementation Notes**: The route guard `requiresBoard` already exists in the router's `beforeEach` — no additional guard code needed.

---

### Step 2: Create `CampEditionEditPage.vue`

- **File**: `frontend/src/views/camps/CampEditionEditPage.vue` (**NEW**)
- **Action**: Create the full-page edit form with all sections from the enriched spec
- **Implementation Steps**:

  1. **Script setup**: Import dependencies:
     ```typescript
     import { reactive, ref, computed, onMounted } from 'vue'
     import { useRoute, useRouter } from 'vue-router'
     import { useToast } from 'primevue/usetoast'
     import Container from '@/components/ui/Container.vue'
     import CampEditionStatusBadge from '@/components/camps/CampEditionStatusBadge.vue'
     import Button from 'primevue/button'
     import DatePicker from 'primevue/datepicker'
     import InputNumber from 'primevue/inputnumber'
     import Textarea from 'primevue/textarea'
     import ToggleSwitch from 'primevue/toggleswitch'
     import Message from 'primevue/message'
     import Toast from 'primevue/toast'
     import ProgressSpinner from 'primevue/progressspinner'
     import { useCampEditions } from '@/composables/useCampEditions'
     import type { CampEdition, UpdateCampEditionRequest } from '@/types/camp-edition'
     ```

  2. **State setup**:
     ```typescript
     const route = useRoute()
     const router = useRouter()
     const toast = useToast()
     const { getEditionById, updateEdition, loading, error } = useCampEditions()

     const edition = ref<CampEdition | null>(null)
     const pageLoading = ref(true)
     const pageError = ref<string | null>(null)
     const saving = ref(false)
     ```

  3. **Form model**: Reuse the exact same `FormModel` interface from `CampEditionUpdateDialog.vue` (lines 27–53). Define as `reactive<FormModel>({...})` with same defaults.

  4. **`initializeForm()` function**: Migrate from dialog (lines 87–115) with one key change — **date defaults**:
     ```typescript
     const initializeForm = () => {
       if (!edition.value) return
       const year = edition.value.year

       // Date defaults: 15/08 - 30/08 of the edition's year
       form.startDate = edition.value.startDate
         ? new Date(edition.value.startDate)
         : new Date(year, 7, 15) // Aug 15 (month is 0-indexed)
       form.endDate = edition.value.endDate
         ? new Date(edition.value.endDate)
         : new Date(year, 7, 30) // Aug 30

       // ... rest identical to dialog lines 90-114
     }
     ```

  5. **`validate()` function**: Copy directly from dialog (lines 126–178). No changes needed.

  6. **`formatDateToIso()` function**: Copy from dialog (lines 121–124).

  7. **`weekRanges` computed**: New addition for the week indicator:
     ```typescript
     const weekRanges = computed(() => {
       if (!form.startDate || !form.endDate || !form.halfDate) return null
       const fmt = (d: Date) =>
         new Intl.DateTimeFormat('es-ES', { day: '2-digit', month: '2-digit' }).format(d)
       const nextDay = new Date(form.halfDate)
       nextDay.setDate(nextDay.getDate() + 1)
       return {
         week1: `${fmt(form.startDate)} → ${fmt(form.halfDate)} (incluido)`,
         week2: `${fmt(nextDay)} → ${fmt(form.endDate)}`
       }
     })
     ```

  8. **`isOpenEdition` computed**: Same as dialog line 85:
     ```typescript
     const isOpenEdition = computed(() => edition.value?.status === 'Open')
     ```

  9. **`handleSave()` function**: Based on dialog (lines 181–220) but with navigation + toast:
     ```typescript
     const handleSave = async () => {
       if (!edition.value || !validate()) return
       saving.value = true

       const request: UpdateCampEditionRequest = {
         // ... identical construction to dialog lines 184-213
       }

       const result = await updateEdition(edition.value.id, request)
       saving.value = false

       if (result) {
         toast.add({
           severity: 'success',
           summary: 'Éxito',
           detail: 'Edición actualizada correctamente',
           life: 3000
         })
         router.push({ name: 'camp-edition-detail', params: { id: edition.value.id } })
       }
     }
     ```

  10. **`handleCancel()` function**:
      ```typescript
      const handleCancel = () => {
        router.back()
      }
      ```

  11. **`onMounted` hook**: Fetch edition and initialize form:
      ```typescript
      onMounted(async () => {
        pageLoading.value = true
        const id = route.params.id as string
        edition.value = await getEditionById(id)
        if (!edition.value) {
          pageError.value = 'Edición no encontrada'
        } else {
          initializeForm()
        }
        pageLoading.value = false
      })
      ```

  12. **Template structure** — follow the layout from the enriched spec. Use `Container` wrapper. Sections as cards (`rounded-lg border border-gray-200 bg-white p-6`):

      ```html
      <template>
        <Container>
          <Toast />
          <div class="py-8">
            <!-- Back button -->
            <Button label="Volver" icon="pi pi-arrow-left" text class="mb-4" @click="handleCancel" />

            <!-- Loading state -->
            <div v-if="pageLoading" class="flex justify-center py-12">
              <ProgressSpinner />
            </div>

            <!-- Error state -->
            <Message v-else-if="pageError" severity="error" :closable="false">
              {{ pageError }}
            </Message>

            <!-- Form -->
            <div v-else-if="edition">
              <!-- Header: title + status badge -->
              <div class="mb-6 flex flex-wrap items-center justify-between gap-4">
                <h1 class="text-3xl font-bold text-gray-900">
                  Editar Edición {{ edition.year }}
                </h1>
                <CampEditionStatusBadge :status="edition.status" />
              </div>

              <!-- Open edition warning -->
              <Message v-if="isOpenEdition" severity="info" :closable="false" class="mb-6">
                Esta edición está abierta para inscripciones. Solo se pueden modificar las notas, la descripción y la capacidad máxima.
              </Message>

              <!-- API error -->
              <Message v-if="error" severity="error" :closable="false" class="mb-6">
                {{ error }}
              </Message>

              <div class="space-y-6">
                <!-- SECTION 1: Description (prominent) -->
                <div class="rounded-lg border border-gray-200 bg-white p-6">
                  <h2 class="mb-1 text-lg font-semibold text-gray-900">Descripción de la edición</h2>
                  <p class="mb-3 text-sm text-gray-500">
                    Texto principal de la edición del año. Describe las actividades, novedades y datos relevantes.
                  </p>
                  <Textarea
                    v-model="form.description"
                    rows="8"
                    class="w-full"
                    placeholder="Describe las actividades, novedades y datos relevantes de esta edición..."
                    data-testid="edition-description"
                  />
                </div>

                <!-- SECTION 2: Dates + Prices (2-column grid) -->
                <div class="grid grid-cols-1 gap-6 lg:grid-cols-2">
                  <!-- Dates card -->
                  <div class="rounded-lg border border-gray-200 bg-white p-6">
                    <h2 class="mb-4 text-lg font-semibold text-gray-900">Fechas</h2>
                    <div class="space-y-4">
                      <div class="flex flex-col gap-1">
                        <label class="text-sm font-medium text-gray-700">Fecha de inicio</label>
                        <DatePicker
                          v-model="form.startDate"
                          date-format="dd/mm/yy"
                          :disabled="isOpenEdition"
                          class="w-full"
                        />
                        <span v-if="errors.startDate" class="text-xs text-red-600">{{ errors.startDate }}</span>
                      </div>
                      <div class="flex flex-col gap-1">
                        <label class="text-sm font-medium text-gray-700">Fecha de fin</label>
                        <DatePicker
                          v-model="form.endDate"
                          date-format="dd/mm/yy"
                          :disabled="isOpenEdition"
                          class="w-full"
                        />
                        <span v-if="errors.endDate" class="text-xs text-red-600">{{ errors.endDate }}</span>
                      </div>
                    </div>
                  </div>

                  <!-- Prices card -->
                  <div class="rounded-lg border border-gray-200 bg-white p-6">
                    <h2 class="mb-4 text-lg font-semibold text-gray-900">Precios</h2>
                    <div class="space-y-4">
                      <!-- 3 InputNumbers for adult/child/baby prices -->
                      <!-- Same as dialog lines 267-302, but with vertical layout -->
                    </div>
                  </div>
                </div>

                <!-- SECTION 3: Capacity -->
                <div class="rounded-lg border border-gray-200 bg-white p-6">
                  <h2 class="mb-4 text-lg font-semibold text-gray-900">Capacidad</h2>
                  <!-- InputNumber for maxCapacity, same as dialog lines 306-316 -->
                </div>

                <!-- SECTION 4: Partial Attendance (weekly) -->
                <div class="rounded-lg border border-gray-200 bg-white p-6">
                  <div class="flex items-center gap-3">
                    <ToggleSwitch v-model="form.allowPartialAttendance" :disabled="isOpenEdition" />
                    <h2 class="text-lg font-semibold text-gray-900">Inscripción por semanas</h2>
                  </div>
                  <div v-if="form.allowPartialAttendance" class="mt-4 space-y-4">
                    <!-- Half date picker -->
                    <div class="flex flex-col gap-1">
                      <label class="text-sm font-medium text-gray-700">Fecha de corte</label>
                      <DatePicker
                        v-model="form.halfDate"
                        date-format="dd/mm/yy"
                        show-icon
                        :disabled="isOpenEdition"
                        class="w-full"
                      />
                    </div>

                    <!-- Week ranges indicator (NEW) -->
                    <div v-if="weekRanges" class="rounded-lg bg-blue-50 p-3 text-sm text-blue-800">
                      <p><strong>1ª semana:</strong> {{ weekRanges.week1 }}</p>
                      <p><strong>2ª semana:</strong> {{ weekRanges.week2 }}</p>
                    </div>

                    <!-- Weekly prices (3-col grid) -->
                    <div class="grid grid-cols-1 gap-4 sm:grid-cols-3">
                      <!-- Same as dialog lines 337-376 -->
                    </div>
                  </div>
                </div>

                <!-- SECTION 5: Weekend Visits -->
                <div class="rounded-lg border border-gray-200 bg-white p-6">
                  <!-- Same structure as dialog lines 381-466 but in card layout -->
                </div>

                <!-- SECTION 6: Custom Age Ranges -->
                <div class="rounded-lg border border-gray-200 bg-white p-6">
                  <!-- Same structure as dialog lines 487-539 but in card layout -->
                </div>

                <!-- SECTION 7: Internal Notes -->
                <div class="rounded-lg border border-gray-200 bg-white p-6">
                  <h2 class="mb-2 text-lg font-semibold text-gray-900">Notas internas</h2>
                  <Textarea v-model="form.notes" :max-length="2000" rows="3" class="w-full" />
                  <span v-if="errors.notes" class="text-xs text-red-600">{{ errors.notes }}</span>
                </div>

                <!-- Footer: action buttons -->
                <div class="flex justify-end gap-3">
                  <Button
                    label="Cancelar"
                    text
                    :disabled="saving"
                    @click="handleCancel"
                  />
                  <Button
                    label="Guardar"
                    icon="pi pi-check"
                    :loading="saving"
                    :disabled="saving"
                    data-testid="save-edition-btn"
                    @click="handleSave"
                  />
                </div>
              </div>
            </div>
          </div>
        </Container>
      </template>
      ```

- **Dependencies**: All PrimeVue components already used in the project. No new npm packages.
- **Implementation Notes**:
  - The template above is a skeleton — fill in each section by migrating the corresponding fields from `CampEditionUpdateDialog.vue`, wrapping each group in a card (`rounded-lg border border-gray-200 bg-white p-6`)
  - Every field from the dialog MUST be present in the new page — no fields should be lost
  - All `data-testid` attributes from the dialog should be preserved (especially `save-edition-btn`)
  - Add `data-testid="edition-description"` to the description textarea (new)
  - The `errors` ref is the same as dialog line 83: `const errors = ref<Record<string, string>>({})`

---

### Step 3: Update `CampEditionsPage.vue` — Remove Dialog, Navigate to Edit Page

- **File**: `frontend/src/views/camps/CampEditionsPage.vue`
- **Action**: Replace dialog-based editing with navigation to the new edit page
- **Implementation Steps**:

  1. **Remove imports** (line 17):
     - Delete: `import CampEditionUpdateDialog from '@/components/camps/CampEditionUpdateDialog.vue'`

  2. **Remove state** (line 49):
     - Delete: `const showEditDialog = ref(false)`

  3. **Simplify `handleEdit`** (lines 99–102):
     Replace with:
     ```typescript
     const handleEdit = (edition: CampEdition) => {
       router.push({ name: 'camp-edition-edit', params: { id: edition.id } })
     }
     ```

  4. **Remove `handleEditionSaved`** (lines 135–143):
     - Delete the entire function — toast is now handled in the edit page itself.

  5. **Remove dialog from template** (lines 246–247):
     - Delete the `<CampEditionUpdateDialog>` component and its containing comment.

  6. **Remove unused imports**: `CampEditionUpdateDialog` is the only removal. Check that `selectedEdition` is still needed for the status dialog — yes, it is (line 242), so keep `selectedEdition` ref.

- **Implementation Notes**: After this step, the "edit" pencil button in the actions column (line 233) still works but now navigates to `/camps/editions/:id/edit` instead of opening a dialog.

---

### Step 4: Update `CampEditionDetailPage.vue` — Add "Editar" Button

- **File**: `frontend/src/views/camps/CampEditionDetailPage.vue`
- **Action**: Add an "Editar" button in the header for Board users
- **Implementation Steps**:

  1. The `router` import and `useRouter()` already exist (lines 3, 16). The `isBoard` computed already exists (line 21).

  2. Add the button in the header section, after the `<h1>` tag (around line 59):
     ```html
     <div class="mb-6 flex flex-wrap items-center justify-between gap-4">
       <div>
         <h1 class="text-3xl font-bold text-gray-900">
           Edición {{ edition.year }}
           <span v-if="edition.name"> — {{ edition.name }}</span>
         </h1>
         <p class="mt-1 text-gray-500">{{ edition.location }}</p>
       </div>
       <Button
         v-if="isBoard && edition.status !== 'Closed' && edition.status !== 'Completed'"
         label="Editar"
         icon="pi pi-pencil"
         data-testid="edit-edition-from-detail-btn"
         @click="router.push({ name: 'camp-edition-edit', params: { id: edition.id } })"
       />
     </div>
     ```

  3. Replace the current header block (lines 55-61) with the above layout that wraps title + button in a flex container.

- **Implementation Notes**: The button is conditionally rendered — hidden for Closed/Completed editions (matching the same disable logic from the list page).

---

### Step 5: Delete `CampEditionUpdateDialog.vue`

- **File**: `frontend/src/components/camps/CampEditionUpdateDialog.vue`
- **Action**: Delete the file entirely
- **Implementation Steps**:
  1. Run `git rm frontend/src/components/camps/CampEditionUpdateDialog.vue`
  2. Verify no other files import it:
     - `grep -r "CampEditionUpdateDialog" frontend/src/` should return 0 results after Step 3
  3. Check for any test files referencing this component:
     - Look for `frontend/src/components/camps/__tests__/CampEditionUpdateDialog.test.ts` — if it exists, delete it too (the tests will be replaced by tests for the new page)

- **Implementation Notes**: This is safe to do after Step 3 removes all references.

---

### Step 6: Write Vitest Unit Tests for `CampEditionEditPage.vue`

- **File**: `frontend/src/views/camps/__tests__/CampEditionEditPage.test.ts` (**NEW**)
- **Action**: Create unit tests covering key behaviors
- **Implementation Steps**:

  1. **Test setup pattern** (follow `CampEditionProposeDialog.test.ts` pattern):
     ```typescript
     import { describe, it, expect, vi, beforeEach } from 'vitest'
     import { mount, flushPromises } from '@vue/test-utils'
     import CampEditionEditPage from '../CampEditionEditPage.vue'

     // Mock dependencies
     vi.mock('vue-router', () => ({
       useRoute: vi.fn(() => ({ params: { id: 'test-id' } })),
       useRouter: vi.fn(() => ({ push: vi.fn(), back: vi.fn() }))
     }))
     vi.mock('primevue/usetoast', () => ({
       useToast: vi.fn(() => ({ add: vi.fn() }))
     }))

     const mockGetEditionById = vi.fn()
     const mockUpdateEdition = vi.fn()
     vi.mock('@/composables/useCampEditions', () => ({
       useCampEditions: vi.fn(() => ({
         getEditionById: mockGetEditionById,
         updateEdition: mockUpdateEdition,
         loading: ref(false),
         error: ref(null)
       }))
     }))
     ```

  2. **Test cases** to write:
     - `'loads edition data on mount and initializes form'`
     - `'shows error message when edition not found'`
     - `'defaults dates to Aug 15-30 when edition dates are empty'`
     - `'preserves existing dates when edition has dates'`
     - `'shows description textarea prominently at the top'`
     - `'shows week range indicator when halfDate is set and partial attendance is enabled'`
     - `'disables fields when edition status is Open'`
     - `'navigates to detail page on successful save'`
     - `'shows toast on successful save'`
     - `'navigates back on cancel'`
     - `'validates required fields before save'`

  3. **Key assertion for date defaults**:
     ```typescript
     it('defaults dates to Aug 15-30 when edition dates are empty', async () => {
       mockGetEditionById.mockResolvedValue({
         id: 'test-id',
         year: 2026,
         startDate: '',  // or null
         endDate: '',
         // ... other fields
       })
       const wrapper = mount(CampEditionEditPage)
       await flushPromises()
       const vm = wrapper.vm as any
       expect(vm.form.startDate.getMonth()).toBe(7)  // August
       expect(vm.form.startDate.getDate()).toBe(15)
       expect(vm.form.endDate.getMonth()).toBe(7)
       expect(vm.form.endDate.getDate()).toBe(30)
     })
     ```

  4. **Key assertion for week ranges**:
     ```typescript
     it('shows week range indicator when halfDate is set', async () => {
       // ... mount with edition data including halfDate
       const vm = wrapper.vm as any
       vm.form.allowPartialAttendance = true
       vm.form.halfDate = new Date(2026, 7, 22)
       await flushPromises()
       expect(vm.weekRanges).not.toBeNull()
       expect(vm.weekRanges.week1).toContain('15/08')
       expect(vm.weekRanges.week1).toContain('22/08')
       expect(vm.weekRanges.week2).toContain('23/08')
       expect(vm.weekRanges.week2).toContain('30/08')
     })
     ```

---

### Step 7: Update Cypress E2E Tests

- **File**: `frontend/cypress/e2e/camps/camp-editions.cy.ts`
- **Action**: Update E2E tests to reflect the new page-based edit flow
- **Implementation Steps**:

  1. **Update the existing edit test** (the one that clicks `[data-testid="edit-edition-btn"]` and expects a dialog):
     - Instead of expecting `[data-testid="edition-dialog"]` to be visible, expect navigation to `/camps/editions/{id}/edit`
     - Intercept the `GET /api/camps/editions/{id}` request for the edit page

  2. **Add new test cases**:
     ```typescript
     describe('Camp Edition Edit Page', () => {
       beforeEach(() => {
         cy.login('board@abuvi.org', 'password123')
         cy.intercept('GET', '/api/camps/editions/test-id', { fixture: 'edition-detail.json' }).as('getEdition')
       })

       it('should navigate to edit page from list', () => {
         cy.intercept('GET', '/api/camps/editions*', { fixture: 'editions-list.json' }).as('getEditions')
         cy.intercept('GET', '/api/camps', { body: { success: true, data: [], error: null } }).as('getCamps')
         cy.visit('/camps/editions')
         cy.wait('@getEditions')
         cy.get('[data-testid="edit-edition-btn"]').first().click()
         cy.url().should('include', '/camps/editions/')
         cy.url().should('include', '/edit')
       })

       it('should show description textarea prominently', () => {
         cy.visit('/camps/editions/test-id/edit')
         cy.wait('@getEdition')
         cy.get('[data-testid="edition-description"]').should('be.visible')
       })

       it('should save and redirect to detail with toast', () => {
         cy.intercept('PUT', '/api/camps/editions/test-id', { fixture: 'edition-updated.json' }).as('putEdition')
         cy.visit('/camps/editions/test-id/edit')
         cy.wait('@getEdition')
         cy.get('[data-testid="save-edition-btn"]').click()
         cy.wait('@putEdition')
         cy.url().should('include', '/camps/editions/test-id')
         cy.url().should('not.include', '/edit')
         cy.contains('Edición actualizada correctamente').should('be.visible')
       })
     })
     ```

  3. **Cypress fixture**: May need to create `frontend/cypress/fixtures/edition-detail.json` if it doesn't exist. It should match the `CampEdition` interface with all fields populated.

---

### Step 8: Update Technical Documentation

- **Action**: Review and update technical documentation according to changes made
- **Implementation Steps**:
  1. **Review Changes**: New route, new page component, removed dialog component, updated navigation
  2. **Update `ai-specs/specs/frontend-standards.mdc`**:
     - If there's a section listing routes or page components, add the new `/camps/editions/:id/edit` route
     - If there's a navigation patterns section, note the preference for full pages over modals for complex forms
  3. **Update routing documentation** (if separate): Add the new route to any route listing
  4. **Verify Documentation**: Confirm all changes are reflected accurately
  5. **Report Updates**: Document which files were updated

---

## Implementation Order

1. **Step 0**: Create feature branch `feature/improve-camp-edition-edit-ux-frontend`
2. **Step 1**: Add route in `router/index.ts`
3. **Step 2**: Create `CampEditionEditPage.vue` (the main work)
4. **Step 3**: Update `CampEditionsPage.vue` (remove dialog, use navigation)
5. **Step 4**: Update `CampEditionDetailPage.vue` (add "Editar" button)
6. **Step 5**: Delete `CampEditionUpdateDialog.vue`
7. **Step 6**: Write Vitest unit tests
8. **Step 7**: Update Cypress E2E tests
9. **Step 8**: Update technical documentation

---

## Testing Checklist

### Unit Tests (Vitest)

- [ ] Page loads edition data and initializes form correctly
- [ ] Date defaults: empty dates → Aug 15-30 of edition year
- [ ] Date preservation: existing dates → unchanged
- [ ] Week range indicator: computed correctly from startDate, halfDate, endDate
- [ ] Week range indicator: null when any date is missing
- [ ] Open edition: fields are disabled except notes, description, maxCapacity
- [ ] Save: calls `updateEdition` with correct request payload
- [ ] Save success: navigates to detail page + shows toast
- [ ] Save failure: shows error message, stays on page
- [ ] Cancel: navigates back
- [ ] Validation: required fields produce errors

### E2E Tests (Cypress)

- [ ] Edit button on list page navigates to `/camps/editions/:id/edit`
- [ ] Edit page loads and displays form with edition data
- [ ] Description textarea is visible and editable
- [ ] Save submits PUT request and redirects to detail
- [ ] Cancel returns to previous page
- [ ] Edit button on detail page navigates to edit page

### Manual Verification

- [ ] Description textarea is visually prominent (top of page, large)
- [ ] Date pickers default to 15/08 and 30/08
- [ ] Week range indicator updates dynamically when changing halfDate
- [ ] Week range shows "(incluido)" on the halfDate in week 1
- [ ] All sections (dates, prices, capacity, partial, weekend, age ranges, notes) are present
- [ ] Responsive layout: 1 column on mobile, 2 columns for dates+prices on desktop
- [ ] Toast appears after successful save

---

## Error Handling Patterns

- **Page load error**: If `getEditionById` returns null, show `<Message severity="error">` with "Edición no encontrada"
- **Save error**: If `updateEdition` fails, the composable sets `error.value` which is displayed in a `<Message severity="error">` at the top of the form. User stays on the page and can retry.
- **Loading states**: `pageLoading` ref for initial load (shows `ProgressSpinner`), `saving` ref for save operation (disables buttons, shows loading indicator on save button)
- **Validation errors**: `errors` reactive object populated by `validate()`, displayed as `<span class="text-xs text-red-600">` below each field

---

## UI/UX Considerations

- **PrimeVue components**: `DatePicker`, `InputNumber`, `Textarea`, `ToggleSwitch`, `Button`, `Message`, `Toast`, `ProgressSpinner`, `CampEditionStatusBadge` (custom)
- **Tailwind layout**:
  - Page: `Container` wrapper → `py-8`
  - Sections: `rounded-lg border border-gray-200 bg-white p-6` cards with `space-y-6` gap
  - Responsive grid: `grid grid-cols-1 lg:grid-cols-2 gap-6` for dates + prices
  - Weekly prices: `grid grid-cols-1 sm:grid-cols-3 gap-4`
- **Responsive design**: Single column on mobile, 2-column for dates+prices on `lg:` breakpoint
- **Accessibility**:
  - All inputs have associated `<label>` elements
  - Buttons have descriptive labels (`Guardar`, `Cancelar`, `Volver`)
  - Error messages are adjacent to their fields
  - `data-testid` attributes for E2E testing
- **Loading states**: Spinner during page load, loading indicator on save button, disabled buttons during save

---

## Dependencies

- **npm packages**: None new — all PrimeVue components already installed
- **PrimeVue components used**: DatePicker, InputNumber, Textarea, ToggleSwitch, Button, Message, Toast, ProgressSpinner
- **Internal components**: Container, CampEditionStatusBadge
- **Composables**: `useCampEditions` (existing, no changes)
- **Types**: `CampEdition`, `UpdateCampEditionRequest` (existing, no changes)

---

## Notes

- **Language**: All user-facing text in Spanish (matching existing app convention). Code comments and documentation in English.
- **Business rules**:
  - Open editions: only `notes`, `description`, and `maxCapacity` are editable (enforced by `isOpenEdition` computed disabling fields)
  - Closed/Completed editions: no edit button shown (enforced in both list and detail pages)
  - Date defaults (15/08 - 30/08) only apply when dates are empty — existing dates are always preserved
  - The `halfDate` cutoff day is **included** in the first week (business rule, displayed in the week indicator)
- **TypeScript**: All code strictly typed. No `any` except in test files for `wrapper.vm` access.
- **No style blocks**: All styling via Tailwind utility classes, no `<style>` sections.

---

## Next Steps After Implementation

1. Run `npm run lint` to verify no linting errors
2. Run `npm run type-check` to verify TypeScript
3. Run `npm run test:unit` to verify all Vitest tests pass
4. Run `npx cypress run --spec "cypress/e2e/camps/camp-editions.cy.ts"` to verify E2E tests
5. Manual QA in browser: test the full flow (list → edit → save → detail)
6. Create PR against `dev` branch

---

## Implementation Verification

- [ ] **Code Quality**: All `.vue` files use `<script setup lang="ts">`, no `any` types (except tests), no `<style>` blocks
- [ ] **Functionality**: Edit page loads correctly, form initializes with edition data, save works, navigation works
- [ ] **Testing**: Vitest unit tests + Cypress E2E tests pass
- [ ] **Integration**: `useCampEditions` composable connects correctly to backend API
- [ ] **Documentation**: Updated routing docs, frontend standards (if applicable)
- [ ] **No regressions**: Status dialog still works from list page, detail page still renders correctly
