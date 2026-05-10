# Frontend Implementation Plan: feat-accommodation-quantity — Accommodation Quantity per Zone

## Overview

This feature adds a `quantity` field to camp edition accommodations so admins can specify how many physical units of a given type exist in a zone (e.g. "10 double rooms"). The assignment board then expands that single accommodation record into N independently assignable slots, each named `"Name #N"` when `quantity > 1`.

The backend PR #245 is already merged. All backend changes are live: the API returns `quantity` and `unitIndex` on accommodation/assignment responses and accepts `quantity` and `unitIndex` in request bodies.

Architecture: Vue 3 Composition API, PrimeVue + Tailwind, composable-based API calls.

---

## Architecture Context

### Files to modify

| File | Change |
|---|---|
| `frontend/src/types/accommodation-assignment.ts` | Add `quantity`, `unitIndex` to `AssignmentAccommodationResponse`; add `unitIndex` to `AssignmentEntry` |
| `frontend/src/types/camp-edition.ts` | Add `quantity` to `CampEditionAccommodation`, `CreateCampEditionAccommodationRequest`, `UpdateCampEditionAccommodationRequest` |
| `frontend/src/composables/useAccommodationAssignment.ts` | Change `assignmentsMap` type; update `assignFamily` to pass `unitIndex` |
| `frontend/src/components/camps/CampEditionAccommodationDialog.vue` | Add "Número de unidades" `InputNumber` field |
| `frontend/src/components/camps/CampEditionAccommodationsPanel.vue` | Show `×N` badge when `quantity > 1` |
| `frontend/src/components/camps/AccommodationAssignmentPanel.vue` | Update `assignedFamiliesFor`, `friendlyFamilyInZoneMap`, `handleAssign`, and filter logic to use `(accommodationId, unitIndex)` slot identity |
| `frontend/src/components/camps/AccommodationSlotCard.vue` | Emit `unitIndex` alongside `accommodationId` on assign |
| `frontend/src/views/camps/AccommodationAssignmentView.vue` | Update `handleAssign` signature to include `unitIndex` |

### No new files, no routing changes, no new Pinia stores.

---

## Implementation Steps

### Step 0: Create Feature Branch

- **Action**: Create and switch to the feature branch.
- **Branch name**: `feature/feat-accommodation-quantity-frontend`
- **Base**: latest `dev`
- **Commands**:
  ```bash
  git checkout dev && git pull origin dev
  git checkout -b feature/feat-accommodation-quantity-frontend
  ```

---

### Step 1: Update TypeScript Types

#### File: `frontend/src/types/accommodation-assignment.ts`

**`AssignmentAccommodationResponse`** — add two fields at the end:

```typescript
export interface AssignmentAccommodationResponse {
  id: string
  name: string
  type: AccommodationTypeValue
  capacity: number | null
  countByFamily: boolean
  zoneId: string | null
  zoneName: string | null
  sortOrder: number
  availableFeatures: string[]
  quantity: number          // NEW — total physical units of this accommodation type
  unitIndex: number | null  // NEW — null when quantity === 1; 0-indexed when quantity > 1
}
```

**`AssignmentEntry`** — add `unitIndex`:

```typescript
export interface AssignmentEntry {
  registrationId: string
  accommodationId: string
  unitIndex: number | null  // NEW — null for single-unit accommodations
}
```

#### File: `frontend/src/types/camp-edition.ts`

**`CampEditionAccommodation`** — add `quantity` after `countByFamily`:

```typescript
export interface CampEditionAccommodation {
  // ...existing fields...
  countByFamily: boolean
  quantity: number          // NEW — number of physical units; default 1
  isActive: boolean
  // ...rest unchanged...
}
```

**`CreateCampEditionAccommodationRequest`** — add optional `quantity`:

```typescript
export interface CreateCampEditionAccommodationRequest {
  name: string
  accommodationType: AccommodationType
  description?: string
  capacity?: number
  countByFamily?: boolean
  quantity?: number         // NEW — optional, defaults to 1 on the backend
  zoneId?: string | null
  sortOrder?: number
}
```

**`UpdateCampEditionAccommodationRequest`** — add required `quantity`:

```typescript
export interface UpdateCampEditionAccommodationRequest {
  name: string
  accommodationType: AccommodationType
  description?: string
  capacity?: number
  countByFamily: boolean
  quantity: number          // NEW — required on update; must be >= 1
  isActive: boolean
  zoneId?: string | null
  sortOrder: number
}
```

---

### Step 2: Update `useAccommodationAssignment` composable

**File**: `frontend/src/composables/useAccommodationAssignment.ts`

#### 2a. Change `assignmentsMap` type

The current `assignmentsMap` is `Map<registrationId, accommodationId>`. With multi-unit slots, we need to know both `accommodationId` and `unitIndex` for each assigned family to correctly determine which physical slot they're in.

**Change the computed** from:
```typescript
const assignmentsMap = computed((): Map<string, string> => {
  const map = new Map<string, string>()
  assignmentState.value?.assignments.forEach((a) => map.set(a.registrationId, a.accommodationId))
  return map
})
```

**To:**
```typescript
const assignmentsMap = computed((): Map<string, { accommodationId: string; unitIndex: number | null }> => {
  const map = new Map<string, { accommodationId: string; unitIndex: number | null }>()
  assignmentState.value?.assignments.forEach((a) =>
    map.set(a.registrationId, { accommodationId: a.accommodationId, unitIndex: a.unitIndex })
  )
  return map
})
```

Also update `sortedFamilies` — it uses `assignmentsMap.has(...)` which still works fine.

#### 2b. Update `assignFamily` to accept and pass `unitIndex`

**Change** the function signature from:
```typescript
async function assignFamily(registrationId: string, accommodationId: string): Promise<void>
```

**To:**
```typescript
async function assignFamily(registrationId: string, accommodationId: string, unitIndex: number | null): Promise<void>
```

**Change** the API call body from:
```typescript
{ accommodationId }
```
**To:**
```typescript
{ accommodationId, unitIndex }
```

Full updated function:
```typescript
async function assignFamily(registrationId: string, accommodationId: string, unitIndex: number | null): Promise<void> {
  if (!selectedProposalId.value) return
  saving.value = true
  error.value = null
  try {
    await api.post(
      `/camps/editions/${campEditionId.value}/assignment-proposals/${selectedProposalId.value}/assignments/${registrationId}`,
      { accommodationId, unitIndex }
    )
    await loadAssignmentState()
    selectedRegistrationId.value = null
  } catch (err: unknown) {
    error.value = extractError(err)
  } finally {
    saving.value = false
  }
}
```

#### 2c. Update the return type export

The composable's return object exports `assignmentsMap` — update its inferred type by ensuring it's exported correctly (TypeScript infers from the `computed` return type automatically, but any explicit typing referencing `Map<string, string>` must be updated).

---

### Step 3: Update `AccommodationSlotCard.vue`

**File**: `frontend/src/components/camps/AccommodationSlotCard.vue`

#### 3a. Update emit signature

**Change** from:
```typescript
defineEmits<{
  (e: 'assign', accommodationId: string): void
  (e: 'unassign', registrationId: string): void
}>()
```

**To:**
```typescript
defineEmits<{
  (e: 'assign', accommodationId: string, unitIndex: number | null): void
  (e: 'unassign', registrationId: string): void
}>()
```

#### 3b. Update template click handler

**Change** from:
```html
@click="selectedFamily && $emit('assign', accommodation.id)"
```

**To:**
```html
@click="selectedFamily && $emit('assign', accommodation.id, accommodation.unitIndex)"
```

No other changes needed in this file — `accommodation.name` already comes from the backend as `"Name #N"` for multi-unit slots.

---

### Step 4: Update `AccommodationAssignmentPanel.vue`

**File**: `frontend/src/components/camps/AccommodationAssignmentPanel.vue`

This is the most impactful change. Four things must update:

#### 4a. Update prop type for `assignmentsMap`

**Change** the prop type from:
```typescript
assignmentsMap: Map<string, string>
```

**To:**
```typescript
assignmentsMap: Map<string, { accommodationId: string; unitIndex: number | null }>
```

Also update the import from `accommodation-assignment` to use the updated `AssignmentAccommodationResponse`.

#### 4b. Update emit signature to include `unitIndex`

**Change** from:
```typescript
(e: 'assign', registrationId: string, accommodationId: string): void
```

**To:**
```typescript
(e: 'assign', registrationId: string, accommodationId: string, unitIndex: number | null): void
```

#### 4c. Update helper functions

**`assignedAccommodationName`** — currently reads the map value as a string:
```typescript
// OLD
const accId = props.assignmentsMap.get(registrationId)
return accommodationNameMap.value.get(accId) ?? null
```

**Change to:**
```typescript
const assignment = props.assignmentsMap.get(registrationId)
if (!assignment) return null
return accommodationNameMap.value.get(assignment.accommodationId) ?? null
```

**`assignedFamiliesFor`** — currently filters by `accommodationId` only:
```typescript
// OLD
function assignedFamiliesFor(accId: string): AssignmentFamilyResponse[]
  return props.state.families.filter((f) => props.assignmentsMap.get(f.registrationId) === accId)
```

**Change to:**
```typescript
function assignedFamiliesFor(acc: AssignmentAccommodationResponse): AssignmentFamilyResponse[] {
  return props.state.families.filter((f) => {
    const a = props.assignmentsMap.get(f.registrationId)
    return a?.accommodationId === acc.id && a?.unitIndex === acc.unitIndex
  })
}
```

**Why**: With multi-unit slots, two entries can share the same `accommodationId` but differ by `unitIndex`. Matching only on `id` would show all families assigned to ANY unit of that accommodation in every slot card — a visual bug.

#### 4d. Update `friendlyFamilyInZoneMap`

This computed builds a zone-to-slotIds map to detect if a friendly family is in the same zone. With multi-unit, we must use a slot key `${acc.id}|${acc.unitIndex ?? ''}` to distinguish slots.

**Add** a helper at the top of the script:
```typescript
function slotKey(acc: AssignmentAccommodationResponse): string {
  return `${acc.id}|${acc.unitIndex ?? ''}`
}
```

**Change** `friendlyFamilyInZoneMap` from using `acc.id` as the zone-to-acc map key to using `slotKey(acc)`:

```typescript
const friendlyFamilyInZoneMap = computed((): Map<string, boolean> => {
  const map = new Map<string, boolean>()
  if (!selectedFamily.value || (selectedFamily.value.friendlyFamilyUnitIds ?? []).length === 0) {
    props.state.accommodations.forEach((acc) => map.set(slotKey(acc), false))
    return map
  }

  const zoneToSlotKeys = new Map<string | null, string[]>()
  for (const acc of props.state.accommodations) {
    const zoneKey = acc.zoneId ?? null
    if (!zoneToSlotKeys.has(zoneKey)) zoneToSlotKeys.set(zoneKey, [])
    zoneToSlotKeys.get(zoneKey)!.push(slotKey(acc))
  }

  for (const acc of props.state.accommodations) {
    const zoneKey = acc.zoneId ?? null
    const currentKey = slotKey(acc)
    const sameZoneSlotKeys = (zoneToSlotKeys.get(zoneKey) ?? []).filter((k) => k !== currentKey)

    const hasFriendlyInZone = sameZoneSlotKeys.some((k) => {
      const slotAcc = props.state.accommodations.find((a) => slotKey(a) === k)
      if (!slotAcc) return false
      const familiesHere = assignedFamiliesFor(slotAcc)
      return familiesHere.some((f) =>
        selectedFamily.value!.friendlyFamilyUnitIds.includes(f.familyUnitId)
      )
    })

    map.set(currentKey, hasFriendlyInZone)
  }
  return map
})
```

#### 4e. Update `groupedAccommodations` filterOnlyAvailable

**Change** from:
```typescript
const families = assignedFamiliesFor(acc.id)
```

**To:**
```typescript
const families = assignedFamiliesFor(acc)
```

#### 4f. Update `AccommodationSlotCard` bindings in template

Update the `v-for` key, `assignedFamiliesFor` call, and `friendlyFamilyInZoneMap` lookup:

```html
<AccommodationSlotCard
  v-for="acc in accommodations"
  :key="`${acc.id}-${acc.unitIndex ?? 'null'}`"   <!-- changed key to be unique per slot -->
  :accommodation="acc"
  :assigned-families="assignedFamiliesFor(acc)"   <!-- was assignedFamiliesFor(acc.id) -->
  :selected-family="selectedFamily"
  :has-friendly-family-in-zone="friendlyFamilyInZoneMap.get(slotKey(acc)) ?? false"  <!-- was .get(acc.id) -->
  @assign="(accId, unitIndex) => handleAssign(accId, unitIndex)"
  @unassign="$emit('unassign', $event)"
/>
```

#### 4g. Update `handleAssign`

**Change** from:
```typescript
function handleAssign(accId: string) {
  if (props.selectedRegistrationId) {
    emit('assign', props.selectedRegistrationId, accId)
  }
}
```

**To:**
```typescript
function handleAssign(accId: string, unitIndex: number | null) {
  if (props.selectedRegistrationId) {
    emit('assign', props.selectedRegistrationId, accId, unitIndex)
  }
}
```

---

### Step 5: Update `AccommodationAssignmentView.vue`

**File**: `frontend/src/views/camps/AccommodationAssignmentView.vue`

#### 5a. Update `handleAssign` signature

**Change** from:
```typescript
async function handleAssign(registrationId: string, accommodationId: string) {
  await assignFamily(registrationId, accommodationId)
```

**To:**
```typescript
async function handleAssign(registrationId: string, accommodationId: string, unitIndex: number | null) {
  await assignFamily(registrationId, accommodationId, unitIndex)
```

No other changes needed in this file.

---

### Step 6: Update `CampEditionAccommodationDialog.vue`

**File**: `frontend/src/components/camps/CampEditionAccommodationDialog.vue`

#### 6a. Add `quantity` reactive ref

After the existing `isActive` ref, add:
```typescript
const quantity = ref<number>(1)
```

#### 6b. Initialize in the `watch` for `props.visible`

In the **edit branch** (`if (props.accommodation)`), add:
```typescript
quantity.value = props.accommodation.quantity ?? 1
```

In the **create branch** (`else`), add:
```typescript
quantity.value = 1
```

#### 6c. Add validation rule

In `validate()`:
```typescript
if (quantity.value < 1) errors.quantity = 'Mínimo 1 unidad'
```

#### 6d. Include `quantity` in API calls

In `updateAccommodation` call, add `quantity: quantity.value`:
```typescript
const result = await updateAccommodation(props.accommodation.id, {
  name: name.value.trim(),
  accommodationType: accommodationType.value,
  description: description.value.trim() || undefined,
  capacity: capacity.value ?? undefined,
  countByFamily: countByFamily.value,
  quantity: quantity.value,      // NEW
  isActive: isActive.value,
  zoneId: props.accommodation.zoneId ?? undefined,
  sortOrder: sortOrder.value
})
```

In `createAccommodation` call, add `quantity: quantity.value`:
```typescript
const result = await createAccommodation({
  name: name.value.trim(),
  accommodationType: accommodationType.value,
  description: description.value.trim() || undefined,
  capacity: capacity.value ?? undefined,
  countByFamily: countByFamily.value,
  quantity: quantity.value,      // NEW
  zoneId: props.prefilledZoneId ?? undefined,
  sortOrder: sortOrder.value
})
```

#### 6e. Add the form field in template

Place the new field **after the Capacity field and before the Occupancy model section**:

```html
<!-- Quantity (number of physical units) -->
<div>
  <label class="mb-1 block text-sm font-medium text-gray-700">Número de unidades</label>
  <InputNumber
    v-model="quantity"
    :min="1"
    class="w-full"
    :invalid="!!validationErrors.quantity"
  />
  <small v-if="validationErrors.quantity" class="text-red-500">
    {{ validationErrors.quantity }}
  </small>
  <small v-else class="text-gray-400">
    Cuántas unidades físicas de este tipo hay disponibles en la zona.
  </small>
  <!-- Informational note when multiple units + countByFamily=false -->
  <Message
    v-if="quantity > 1 && !countByFamily"
    severity="info"
    :closable="false"
    class="mt-1 text-xs"
  >
    Múltiples unidades por personas: cada unidad aparecerá como una plaza independiente en el tablero.
  </Message>
</div>
```

---

### Step 7: Update `CampEditionAccommodationsPanel.vue`

**File**: `frontend/src/components/camps/CampEditionAccommodationsPanel.vue`

#### 7a. Add quantity badge in the accommodation list item

In the `<div class="flex items-center gap-2">` block that shows the name and type tags, add a `Tag` after the name:

```html
<span class="text-sm font-medium text-gray-900">{{ acc.name }}</span>
<Tag
  v-if="acc.quantity > 1"
  :value="`×${acc.quantity}`"
  severity="secondary"
  class="text-xs"
  title="Número de unidades"
/>
```

Place it immediately after the `<span>` with the name, before the type Tag.

#### 7b. Show total capacity accounting for quantity

In the metadata row (the `<div class="mt-1 flex flex-wrap gap-4 text-xs text-gray-500">` block), update the capacity display:

**Change** from:
```html
<span v-if="acc.capacity">Capacidad: {{ acc.capacity }}</span>
```

**To:**
```html
<span v-if="acc.capacity">
  Capacidad: {{ acc.capacity }}
  <template v-if="acc.quantity > 1"> × {{ acc.quantity }} = {{ acc.capacity * acc.quantity }}</template>
</span>
```

This shows e.g. "Capacidad: 2 × 10 = 20" for 10 double rooms.

---

### Step 8: Write Vitest Unit Tests

#### File: `frontend/src/composables/__tests__/useAccommodationAssignment.test.ts`

Test the updated `assignmentsMap` computed and `assignFamily` function:

```typescript
import { describe, it, expect, vi, beforeEach } from 'vitest'
import { ref } from 'vue'
import { useAccommodationAssignment } from '@/composables/useAccommodationAssignment'
import { api } from '@/utils/api'

vi.mock('@/utils/api')

describe('useAccommodationAssignment', () => {
  const campEditionId = ref('edition-1')

  beforeEach(() => vi.clearAllMocks())

  it('should build assignmentsMap with unitIndex from assignment entries', async () => {
    vi.mocked(api.get).mockResolvedValueOnce({
      data: { success: true, data: [{ id: 'p1', name: 'P1', isActive: true, assignmentCount: 1, unassignedCount: 0, campEditionId: 'e1', createdByUserId: 'u1', notes: null, createdAt: '', updatedAt: '', lastModifiedByUserName: null }] }
    })
    vi.mocked(api.get).mockResolvedValueOnce({
      data: {
        success: true,
        data: {
          proposalId: 'p1',
          families: [],
          accommodations: [],
          assignments: [
            { registrationId: 'r1', accommodationId: 'a1', unitIndex: 2 },
            { registrationId: 'r2', accommodationId: 'a1', unitIndex: null }
          ]
        }
      }
    })

    const { loadProposals, loadAssignmentState, assignmentsMap } = useAccommodationAssignment(campEditionId)
    await loadProposals()
    await loadAssignmentState()

    expect(assignmentsMap.value.get('r1')).toEqual({ accommodationId: 'a1', unitIndex: 2 })
    expect(assignmentsMap.value.get('r2')).toEqual({ accommodationId: 'a1', unitIndex: null })
  })

  it('should pass unitIndex in assignFamily request body', async () => {
    vi.mocked(api.post).mockResolvedValue({ data: { success: true } })
    vi.mocked(api.get).mockResolvedValue({ data: { success: true, data: { proposalId: 'p1', families: [], accommodations: [], assignments: [] } } })

    const { assignFamily, selectedProposalId } = useAccommodationAssignment(campEditionId)
    selectedProposalId.value = 'p1'

    await assignFamily('r1', 'a1', 3)

    expect(api.post).toHaveBeenCalledWith(
      expect.stringContaining('/assignments/r1'),
      { accommodationId: 'a1', unitIndex: 3 }
    )
  })

  it('should pass null unitIndex for single-unit accommodation', async () => {
    vi.mocked(api.post).mockResolvedValue({ data: { success: true } })
    vi.mocked(api.get).mockResolvedValue({ data: { success: true, data: { proposalId: 'p1', families: [], accommodations: [], assignments: [] } } })

    const { assignFamily, selectedProposalId } = useAccommodationAssignment(campEditionId)
    selectedProposalId.value = 'p1'

    await assignFamily('r1', 'a1', null)

    expect(api.post).toHaveBeenCalledWith(
      expect.stringContaining('/assignments/r1'),
      { accommodationId: 'a1', unitIndex: null }
    )
  })
})
```

#### File: `frontend/src/components/camps/__tests__/CampEditionAccommodationDialog.test.ts`

```typescript
import { describe, it, expect, vi } from 'vitest'
import { mount } from '@vue/test-utils'
import CampEditionAccommodationDialog from '@/components/camps/CampEditionAccommodationDialog.vue'

// Mock composable
vi.mock('@/composables/useCampAccommodations', () => ({
  useCampAccommodations: () => ({
    createAccommodation: vi.fn().mockResolvedValue({ id: 'new', name: 'Test', quantity: 3 }),
    updateAccommodation: vi.fn().mockResolvedValue({ id: 'existing', name: 'Test', quantity: 3 }),
    loading: { value: false },
    error: { value: null }
  })
}))

describe('CampEditionAccommodationDialog', () => {
  it('should render quantity field with default value 1', async () => {
    const wrapper = mount(CampEditionAccommodationDialog, {
      props: { visible: true, editionId: 'e1' }
    })
    const quantityLabel = wrapper.text()
    expect(quantityLabel).toContain('Número de unidades')
  })

  it('should initialize quantity from existing accommodation when editing', async () => {
    const acc = {
      id: 'a1', name: 'Suite', accommodationType: 'Lodge' as const,
      quantity: 5, capacity: 2, countByFamily: false,
      isActive: true, sortOrder: 0, currentPreferenceCount: 0,
      firstChoiceCount: 0, features: [], createdAt: '', updatedAt: '',
      campEditionId: 'e1'
    }
    const wrapper = mount(CampEditionAccommodationDialog, {
      props: { visible: true, editionId: 'e1', accommodation: acc }
    })
    // Quantity input should show 5
    const quantityInput = wrapper.findAll('input').find((el) => el.element.value === '5')
    expect(quantityInput).toBeDefined()
  })
})
```

---

### Step 9: Update Technical Documentation

**File**: `ai-specs/specs/api-spec.yml` (if exists and documents accommodation endpoints)

Add `quantity` to the accommodation response schema and request bodies:
- `GET /api/camps/editions/{editionId}/accommodations` — response item gains `quantity: integer`
- `POST /api/camps/editions/{editionId}/accommodations` — request body gains `quantity?: integer (default 1)`
- `PUT /api/camps/editions/{editionId}/accommodations/{id}` — request body gains `quantity: integer`
- `GET .../assignment-state` response — `accommodations[*]` gains `quantity`, `unitIndex`; `assignments[*]` gains `unitIndex`
- `POST .../assignments/{registrationId}` — request body gains `unitIndex?: integer | null`

---

## Implementation Order

1. Step 0: Create feature branch `feature/feat-accommodation-quantity-frontend` from `dev`
2. Step 1: Update TypeScript types (`accommodation-assignment.ts`, `camp-edition.ts`)
3. Step 2: Update `useAccommodationAssignment.ts` composable (type + `assignFamily`)
4. Step 3: Update `AccommodationSlotCard.vue` (emit unitIndex)
5. Step 4: Update `AccommodationAssignmentPanel.vue` (full slot-key logic)
6. Step 5: Update `AccommodationAssignmentView.vue` (handleAssign signature)
7. Step 6: Update `CampEditionAccommodationDialog.vue` (quantity field)
8. Step 7: Update `CampEditionAccommodationsPanel.vue` (×N badge)
9. Step 8: Write Vitest unit tests
10. Step 9: Update API documentation

> **Important**: Steps 2–5 are tightly coupled. The `assignmentsMap` type change propagates from the composable → panel → slot card → view. Complete them in order (2 → 3 → 4 → 5) and verify TypeScript compiles (`npx vue-tsc --noEmit`) before moving on.

---

## Testing Checklist

- [ ] TypeScript compiles with no errors: `npx vue-tsc --noEmit` in `frontend/`
- [ ] Accommodation dialog shows "Número de unidades" field defaulting to 1
- [ ] Creating an accommodation with quantity=1 sends `quantity: 1` in the request
- [ ] Creating an accommodation with quantity=5 sends `quantity: 5`
- [ ] Editing an existing accommodation shows the current quantity (including old records where quantity=1)
- [ ] Accommodation list shows `×5` badge for an accommodation with quantity=5
- [ ] Accommodation list shows no badge for quantity=1
- [ ] Capacity display shows "2 × 5 = 10" for 5 double rooms
- [ ] Assignment board shows 5 named slots for a quantity=5 accommodation: "Name #1" … "Name #5"
- [ ] Each slot is independently assignable
- [ ] Assigning family to slot sends `{ accommodationId, unitIndex }` with the correct unitIndex
- [ ] Single-unit accommodations (quantity=1) still work exactly as before (no visual change)
- [ ] `unitIndex: null` is sent for single-unit accommodation assignments
- [ ] Friendly family badge correctly uses slot-key identity (not just accId)
- [ ] Filter "Solo disponibles" correctly filters per-slot capacity (not total across all units)
- [ ] Vitest unit tests pass: `npx vitest run`

---

## Error Handling Patterns

- `quantity` validation error shown inline below the field in the dialog (same pattern as `capacity`)
- Informational `Message` (severity="info") shown when `quantity > 1 && !countByFamily` — not a hard block, just informational
- API error from assignment (e.g. slot already full, double-booking) surfaced via existing `error.value` → toast in `AccommodationAssignmentView.vue`

---

## UI/UX Considerations

- **PrimeVue `InputNumber`** is already imported in the dialog — reuse it for quantity with `:min="1"`
- **Quantity badge**: use PrimeVue `Tag` with `severity="secondary"` and value `×N` — matches existing tag pattern in the panel
- The slot names (`"Name #1"` etc.) come from the **backend** — no frontend formatting needed
- The `v-for` key in `AccommodationSlotCard` must use the slot key `${acc.id}-${acc.unitIndex ?? 'null'}` to avoid Vue recycling issues when a single accommodation is expanded into N slots with the same `acc.id`
- No additional responsive design changes needed

---

## Dependencies

No new npm packages required. All PrimeVue components used (`InputNumber`, `Tag`, `Message`) are already installed.

---

## Notes

- **Backward compatibility**: existing accommodation records from the API will have `quantity: 1` (the backend default). The `v-if="acc.quantity > 1"` badge guard handles this correctly — no badge for existing data.
- **`assignmentsMap` type change** is the most impactful change. It breaks `AccommodationAssignmentPanel.vue` which uses `assignmentsMap.get(registrationId)` and compares it as a string. All these comparisons must be updated to use `.accommodationId`.
- **Slot key uniqueness**: the `v-for :key` on `AccommodationSlotCard` must include `unitIndex` — slots for the same accommodation share `acc.id`, so using just `acc.id` as key would cause Vue to reuse the same DOM node for all slots of the same accommodation.
- **UI text is in Spanish** as required by project standards; code, types, and function names stay in English.

---

## Next Steps After Implementation

- Run the dev server and manually test the full flow: create accommodation with quantity=3, see the 3 slots on the board, assign families to each slot independently, verify the DB unique constraint works (second attempt to assign to an already-occupied slot returns 409).
- Open PR targeting `dev` (not `main`) per project git workflow.
