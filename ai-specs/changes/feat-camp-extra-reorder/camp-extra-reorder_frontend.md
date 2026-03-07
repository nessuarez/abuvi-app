# Frontend Implementation Plan: camp-extra-reorder — CampEditionExtra Manual Reordering

## Overview

Add frontend support for manually reordering `CampEditionExtra` items using a drag-and-drop interface (PrimeVue `OrderList`). This follows the **existing reorder pattern** in `CampPhotoGallery.vue` (toggle reorder mode → drag items → save order) and the `sortOrder` field pattern from `CampEditionAccommodation`.

**Architecture**: Vue 3 Composition API, composable-based API communication, PrimeVue + Tailwind CSS.

## Architecture Context

- **Components to modify**:
  - `frontend/src/components/camps/CampEditionExtrasList.vue` — Add reorder toggle + `OrderList` mode
  - `frontend/src/components/camps/CampEditionExtrasFormDialog.vue` — Add `sortOrder` field to form
- **Types to modify**:
  - `frontend/src/types/camp-edition.ts` — Add `sortOrder` to interface + request types, add `ReorderCampExtrasRequest`
- **Composable to modify**:
  - `frontend/src/composables/useCampExtras.ts` — Add `reorderExtras()` method
- **Reference implementations**:
  - `CampPhotoGallery.vue` — Reorder toggle + `OrderList` UI pattern
  - `useCampPhotos.ts` → `reorderPhotos()` — API call pattern
  - `CampEditionAccommodationDialog.vue` — `sortOrder` field in form
  - `CampEditionAccommodationsPanel.vue` — `sortOrder` sorting pattern
- **No new routes or Pinia stores needed**

## Implementation Steps

### Step 0: Create Feature Branch

- **Action**: Create and switch to a new feature branch
- **Branch Naming**: `feature/camp-extra-reorder-frontend`
- **Implementation Steps**:
  1. Ensure on latest `dev` branch: `git checkout dev && git pull origin dev`
  2. Create new branch: `git checkout -b feature/camp-extra-reorder-frontend`
  3. Verify: `git branch`
- **Notes**: Assumes the backend branch `feature/camp-extra-reorder-backend` is already merged or available. If not, branch from `dev` and coordinate merge order.

---

### Step 1: Update TypeScript Types

- **File**: `frontend/src/types/camp-edition.ts`
- **Action**: Add `sortOrder` to existing types and add reorder request type

**Implementation Steps**:

1. Add `sortOrder` to `CampEditionExtra` interface (after `maxQuantity`, before `currentQuantitySold`, line ~103):
   ```typescript
   export interface CampEditionExtra {
     id: string
     campEditionId: string
     name: string
     description?: string
     price: number
     pricingType: 'PerPerson' | 'PerFamily'
     pricingPeriod: 'OneTime' | 'PerDay'
     isRequired: boolean
     requiresUserInput: boolean
     userInputLabel?: string
     maxQuantity?: number
     sortOrder: number              // <-- ADD
     currentQuantitySold: number | null
     isActive: boolean
     createdAt: string
     updatedAt: string
   }
   ```

2. Add `sortOrder` to `CreateCampExtraRequest` (line ~119, optional with default):
   ```typescript
   export interface CreateCampExtraRequest {
     name: string
     description?: string
     price: number
     pricingType: 'PerPerson' | 'PerFamily'
     pricingPeriod: 'OneTime' | 'PerDay'
     isRequired: boolean
     requiresUserInput?: boolean
     userInputLabel?: string
     maxQuantity?: number
     sortOrder?: number             // <-- ADD
   }
   ```

3. Add `sortOrder` to `UpdateCampExtraRequest` (line ~130, optional with default):
   ```typescript
   export interface UpdateCampExtraRequest {
     name: string
     description?: string
     price: number
     isRequired: boolean
     isActive: boolean
     requiresUserInput?: boolean
     userInputLabel?: string
     maxQuantity?: number
     sortOrder?: number             // <-- ADD
   }
   ```

4. Add `ReorderCampExtrasRequest` interface (after `UpdateCampExtraRequest`):
   ```typescript
   export interface ReorderCampExtrasRequest {
     orderedIds: string[]
   }
   ```

---

### Step 2: Update Composable

- **File**: `frontend/src/composables/useCampExtras.ts`
- **Action**: Add `reorderExtras()` method following `useCampPhotos.reorderPhotos()` pattern

**Implementation Steps**:

1. Add import for `ReorderCampExtrasRequest` type:
   ```typescript
   import type {
     CampEditionExtra,
     CreateCampExtraRequest,
     UpdateCampExtraRequest,
     ReorderCampExtrasRequest       // <-- ADD
   } from '@/types/camp-edition'
   ```

2. Add `reorderExtras` method (after `deactivateExtra`, before the `return` statement):
   ```typescript
   const reorderExtras = async (request: ReorderCampExtrasRequest): Promise<boolean> => {
     loading.value = true
     error.value = null
     try {
       await api.put(`/camps/editions/${editionId}/extras/reorder`, request)
       return true
     } catch (err: unknown) {
       error.value = (err as { response?: { data?: { error?: { message?: string } } } })
         ?.response?.data?.error?.message || 'Error al reordenar extras'
       console.error('Failed to reorder extras:', err)
       return false
     } finally {
       loading.value = false
     }
   }
   ```

3. Add `reorderExtras` to the return object:
   ```typescript
   return {
     extras,
     loading,
     error,
     fetchExtras,
     getExtraById,
     createExtra,
     updateExtra,
     deleteExtra,
     activateExtra,
     deactivateExtra,
     reorderExtras              // <-- ADD
   }
   ```

- **Reference**: `useCampPhotos.ts` `reorderPhotos()` method (lines ~132-148) for the exact pattern.

---

### Step 3: Update List Component — Add Reorder Mode

- **File**: `frontend/src/components/camps/CampEditionExtrasList.vue`
- **Action**: Add reorder toggle button and PrimeVue `OrderList` mode, following `CampPhotoGallery.vue` pattern

**Implementation Steps**:

1. Add imports for `OrderList`:
   ```typescript
   import OrderList from 'primevue/orderlist'
   ```

2. Destructure `reorderExtras` from the composable:
   ```typescript
   const {
     extras,
     loading,
     error,
     fetchExtras,
     deleteExtra,
     activateExtra,
     deactivateExtra,
     reorderExtras              // <-- ADD
   } = useCampExtras(props.editionId)
   ```

3. Add `reorderMode` ref:
   ```typescript
   const reorderMode = ref(false)
   ```

4. Add `toggleReorderMode` function (following `CampPhotoGallery.vue` pattern):
   ```typescript
   const toggleReorderMode = async () => {
     if (reorderMode.value) {
       // Exiting reorder mode → save the new order
       const request: ReorderCampExtrasRequest = {
         orderedIds: extras.value.map((e) => e.id)
       }
       const success = await reorderExtras(request)
       if (success) {
         // Update local sortOrder values
         extras.value = extras.value.map((e, index) => ({ ...e, sortOrder: index }))
         toast.add({
           severity: 'success',
           summary: 'Orden guardado',
           detail: 'El orden de los extras se ha actualizado',
           life: 3000
         })
       } else if (error.value) {
         toast.add({ severity: 'error', summary: 'Error', detail: error.value, life: 5000 })
         // Revert to original order on failure
         await fetchExtras()
       }
     }
     reorderMode.value = !reorderMode.value
   }
   ```

5. Add import for `ReorderCampExtrasRequest`:
   ```typescript
   import type { CampEditionExtra, CampEditionStatus, ReorderCampExtrasRequest } from '@/types/camp-edition'
   ```

6. Update the template — add reorder toggle button after the header, before the DataTable:
   ```html
   <!-- Reorder toggle (only for managers with extras) -->
   <div v-if="canManage && extras.length > 1" class="mb-3">
     <Button
       :label="reorderMode ? 'Guardar orden' : 'Reordenar'"
       :icon="reorderMode ? 'pi pi-check' : 'pi pi-arrows-v'"
       text
       size="small"
       :loading="loading && reorderMode"
       data-testid="reorder-extras-button"
       @click="toggleReorderMode"
     />
     <span v-if="reorderMode" class="ml-2 text-xs text-gray-500">
       Arrastra los extras para cambiar el orden
     </span>
   </div>
   ```

7. Add `OrderList` for reorder mode (between the reorder toggle and the DataTable):
   ```html
   <!-- Reorder mode: OrderList -->
   <OrderList
     v-if="reorderMode && extras.length > 0"
     v-model="extras"
     data-key="id"
     :option-label="() => ''"
     class="mb-4 w-full"
     data-testid="extras-order-list"
   >
     <template #item="{ item }">
       <div class="flex items-center justify-between gap-3 p-2">
         <div class="flex items-center gap-3 min-w-0">
           <i class="pi pi-bars text-gray-400" />
           <div class="min-w-0">
             <span class="font-medium text-gray-900">{{ item.name }}</span>
             <p v-if="item.description" class="truncate text-xs text-gray-500">
               {{ item.description }}
             </p>
           </div>
         </div>
         <div class="shrink-0 text-right">
           <span class="text-sm font-semibold">{{ formatCurrency(item.price) }}</span>
           <Tag
             v-if="item.isRequired"
             value="Obligatorio"
             severity="danger"
             class="ml-2"
           />
         </div>
       </div>
     </template>
   </OrderList>
   ```

8. Wrap the existing `DataTable` with a `v-if="!reorderMode"` condition:
   ```html
   <DataTable
     v-if="!reorderMode"
     :value="extras"
     scrollable
     data-testid="extras-table"
   >
     <!-- existing columns unchanged -->
   </DataTable>
   ```

- **Reference**: `CampPhotoGallery.vue` lines 162-234 for the exact toggle + OrderList pattern.
- **Notes**: The `OrderList` component from PrimeVue handles drag-and-drop natively. The `v-model` binding mutates the array order in place, so when saving, we just read the current order of `extras.value`.

---

### Step 4: Update Form Dialog — Add SortOrder Field

- **File**: `frontend/src/components/camps/CampEditionExtrasFormDialog.vue`
- **Action**: Add optional `sortOrder` input field, following `CampEditionAccommodationDialog.vue` pattern

**Implementation Steps**:

1. Add `sortOrder` ref (after `isActive` ref, line ~50):
   ```typescript
   const sortOrder = ref(0)
   ```

2. Update the `watch` to populate `sortOrder` on open (line ~66-88):
   - In the `if (props.extra)` block, add:
     ```typescript
     sortOrder.value = props.extra.sortOrder
     ```
   - In the `else` block (new extra), add:
     ```typescript
     sortOrder.value = 0
     ```

3. Add `sortOrder` to the `handleSave` function:
   - In the `updateExtra` call (line ~109-118), add `sortOrder: sortOrder.value`:
     ```typescript
     const result = await updateExtra(props.extra.id, {
       name: name.value.trim(),
       description: description.value.trim() || undefined,
       price: price.value ?? 0,
       isRequired: isRequired.value,
       isActive: isActive.value,
       requiresUserInput: requiresUserInput.value,
       userInputLabel: requiresUserInput.value ? userInputLabel.value.trim() || undefined : undefined,
       maxQuantity: maxQuantity.value ?? undefined,
       sortOrder: sortOrder.value              // <-- ADD
     })
     ```
   - In the `createExtra` call (line ~125-135), add `sortOrder: sortOrder.value`:
     ```typescript
     const result = await createExtra({
       name: name.value.trim(),
       description: description.value.trim() || undefined,
       price: price.value ?? 0,
       pricingType: pricingType.value,
       pricingPeriod: pricingPeriod.value,
       isRequired: isRequired.value,
       requiresUserInput: requiresUserInput.value,
       userInputLabel: requiresUserInput.value ? userInputLabel.value.trim() || undefined : undefined,
       maxQuantity: maxQuantity.value ?? undefined,
       sortOrder: sortOrder.value              // <-- ADD
     })
     ```

4. Add `InputNumber` import (already imported at line 6).

5. Add sort order field to the template (before the "Is Active" toggle, after the "Max Quantity" field, around line ~277):
   ```html
   <!-- Sort Order -->
   <div>
     <label class="mb-1 block text-sm font-medium text-gray-700">Orden</label>
     <InputNumber
       v-model="sortOrder"
       :min="0"
       class="w-full"
       data-testid="extra-sort-order-input"
     />
     <small class="text-gray-400">Orden de visualización (menor = primero)</small>
   </div>
   ```

- **Reference**: `CampEditionAccommodationDialog.vue` sort order field pattern.

---

### Step 5: Update CampExtrasSection (Read-Only Display)

- **File**: `frontend/src/components/camps/CampExtrasSection.vue`
- **Action**: Ensure extras are displayed sorted by `sortOrder`

**Implementation Steps**:

1. The component receives `extras` as a prop and iterates with `v-for`. Since the backend now returns extras sorted by `sortOrder`, no changes are strictly needed.

2. However, as a safety measure, add local sorting (following `CampEditionAccommodationsPanel.vue` pattern):
   ```typescript
   import { computed } from 'vue'

   const sortedExtras = computed(() =>
     [...props.extras].sort((a, b) => a.sortOrder - b.sortOrder)
   )
   ```

3. Update the `v-for` to use `sortedExtras`:
   ```html
   <li v-for="extra in sortedExtras" :key="extra.id" ...>
   ```

---

### Step 6: Write Vitest Unit Tests

- **Action**: Add tests for the new reorder functionality

**Implementation Steps**:

#### Composable Tests (`frontend/src/composables/__tests__/useCampExtras.spec.ts` or similar):

1. **reorderExtras_success_returnsTrue**
   - Mock `api.put` to resolve
   - Call `reorderExtras({ orderedIds: ['id1', 'id2'] })`
   - Assert returns `true`, loading transitions correctly

2. **reorderExtras_failure_returnsFalseAndSetsError**
   - Mock `api.put` to reject with error
   - Assert returns `false`, `error.value` is set

3. **reorderExtras_callsCorrectEndpoint**
   - Verify `api.put` called with `/camps/editions/${editionId}/extras/reorder`

#### Component Tests:

4. **CampEditionExtrasList_reorderButton_visibleForManagers**
   - Render with `canManage = true` and 2+ extras
   - Assert reorder button is visible

5. **CampEditionExtrasList_reorderButton_hiddenForMembers**
   - Render with `canManage = false`
   - Assert reorder button is not visible

6. **CampEditionExtrasList_reorderButton_hiddenWithSingleExtra**
   - Render with 1 extra
   - Assert reorder button is not visible (nothing to reorder)

7. **CampEditionExtrasList_reorderMode_showsOrderList**
   - Click reorder button
   - Assert `OrderList` is rendered, `DataTable` is hidden

8. **CampEditionExtrasFormDialog_sortOrder_populatedOnEdit**
   - Open dialog with existing extra (sortOrder: 5)
   - Assert `InputNumber` value is 5

---

### Step 7: Update Technical Documentation

- **Action**: Update documentation to reflect frontend changes

**Implementation Steps**:

1. No frontend-specific documentation files need updating beyond what the backend plan already covers
2. Verify the components render correctly with the new `sortOrder` field from the API
3. Confirm PrimeVue `OrderList` works as expected with the extras data structure

---

## Implementation Order

1. Step 0: Create Feature Branch
2. Step 1: Update TypeScript Types (`camp-edition.ts`)
3. Step 2: Update Composable (`useCampExtras.ts`)
4. Step 3: Update List Component (`CampEditionExtrasList.vue`)
5. Step 4: Update Form Dialog (`CampEditionExtrasFormDialog.vue`)
6. Step 5: Update Read-Only Display (`CampExtrasSection.vue`)
7. Step 6: Write Vitest Unit Tests
8. Step 7: Update Technical Documentation

## Testing Checklist

- [ ] TypeScript compiles with no errors (`npm run type-check`)
- [ ] Reorder button visible only for Board/Admin users with 2+ extras
- [ ] Clicking "Reordenar" shows `OrderList` and hides `DataTable`
- [ ] Drag-and-drop in `OrderList` reorders items visually
- [ ] Clicking "Guardar orden" calls API and shows success toast
- [ ] API failure during reorder shows error toast and reverts order
- [ ] Form dialog shows `sortOrder` field with correct value on edit
- [ ] Creating an extra with custom `sortOrder` works
- [ ] `CampExtrasSection` displays extras in `sortOrder` order
- [ ] All existing tests still pass
- [ ] New Vitest tests pass

## Error Handling Patterns

| Scenario | Handling |
|----------|----------|
| Reorder API success | Toast: "Orden guardado" (success), update local `sortOrder` values |
| Reorder API failure | Toast: error message, `fetchExtras()` to revert to server order |
| Network error | Generic "Error al reordenar extras" message |

Following the existing error pattern:
```typescript
error.value = (err as { response?: { data?: { error?: { message?: string } } } })
  ?.response?.data?.error?.message || 'Error al reordenar extras'
```

## UI/UX Considerations

- **Reorder toggle**: Text button with `pi pi-arrows-v` icon → `pi pi-check` when active (matches `CampPhotoGallery` pattern)
- **OrderList items**: Show name, description (truncated), price, and "Obligatorio" tag for quick identification during reorder
- **Drag handle**: `pi pi-bars` icon on each item for clear drag affordance
- **Minimum 2 extras**: Reorder button only shown when there are 2+ extras (nothing to reorder with 1)
- **Inline help**: "Arrastra los extras para cambiar el orden" hint text when in reorder mode
- **Accessibility**: PrimeVue `OrderList` supports keyboard navigation (arrow keys) natively

## Dependencies

- **npm packages**: No new packages required
- **PrimeVue components used**: `OrderList` (already available in project dependencies)

## Notes

- **Backend dependency**: This frontend implementation requires the backend `sort_order` column and `/extras/reorder` endpoint to be deployed first
- **All UI text in Spanish** (matching existing component pattern — UI labels are in Spanish, code is in English)
- **TypeScript strict**: All new code must be fully typed, no `any`
- **Pattern consistency**: Follows the exact same reorder UX as `CampPhotoGallery.vue` for user familiarity

## Next Steps After Implementation

1. Integration testing with backend reorder endpoint
2. Verify drag-and-drop works on mobile/tablet devices
3. Consider adding E2E Cypress test for the full reorder flow
