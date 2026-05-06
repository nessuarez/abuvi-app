# Frontend Implementation Plan: feat-assignment-board-v2 — Assignment Board Enhancements v2

## Overview

This ticket extends the existing accommodation assignment board with richer compatibility signals on slot cards, filtering panels for families and accommodations, a last-modifier trace on proposals, and no new routes or composables. All changes are confined to three existing components and the shared type file.

Architecture principles: Vue 3 `<script setup lang="ts">`, composable-based data flow, PrimeVue + Tailwind CSS, no `<style>` blocks, no `any`.

---

## Architecture Context

**Components modified:**

| File | Change type |
|------|-------------|
| `frontend/src/types/accommodation-assignment.ts` | Extend 3 interfaces |
| `frontend/src/components/camps/AccommodationSlotCard.vue` | New prop, computeds, badges, capacity message |
| `frontend/src/components/camps/AccommodationAssignmentPanel.vue` | New computed map, filter state, filter UI, prop pass-through |
| `frontend/src/components/camps/ProposalSelectorBar.vue` | New field display |
| `frontend/src/components/camps/__tests__/AccommodationSlotCard.test.ts` | New test cases + updated helpers |

**No new composables, stores, routes, or npm packages.** `ToggleSwitch` and `Select` are already used in sibling components (`AccommodationFeatureDialog.vue`, `ProposalSelectorBar.vue`); they are available in PrimeVue without additional installation.

**State management:** All filter state is local `ref` inside `AccommodationAssignmentPanel.vue`. No Pinia store changes.

---

## Implementation Steps

### Step 0: Create Feature Branch

- **Action:** Create and switch to the frontend feature branch.
- **Branch name:** `feature/feat-assignment-board-v2-frontend`
- **Implementation Steps:**
  1. `git checkout dev && git pull origin dev`
  2. `git checkout -b feature/feat-assignment-board-v2-frontend`
  3. `git branch` — confirm active branch.
- **Notes:** Work in `c:\repos\abuvi-app` (main repo), not the backend worktree. The backend changes (new DTO fields) must be merged to `dev` before the frontend can connect to real data — but the frontend can be built and tested against the extended type interfaces immediately.

---

### Step 1: Extend TypeScript Interfaces

**File:** `frontend/src/types/accommodation-assignment.ts`

#### 1a. `AssignmentFamilyResponse` — add 3 fields

```typescript
export interface AssignmentFamilyResponse {
  registrationId: string
  familyUnitId: string
  familyName: string
  representativeName: string
  memberCount: number
  adultCount: number
  childCount: number
  hasPet: boolean
  specialNeeds: string | null
  campatesPreference: string | null
  accommodationPreferences: AccommodationPreferenceItem[]
  // NEW:
  hasSpecialNeeds: boolean
  requiredFeatures: string[]           // AccommodationFeature IDs required by this family
  friendlyFamilyUnitIds: string[]      // FamilyUnit IDs of friend-linked families
}
```

#### 1b. `AssignmentAccommodationResponse` — add 1 field

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
  // NEW:
  availableFeatures: string[]          // AccommodationFeature IDs available in this accommodation
}
```

#### 1c. `AccommodationAssignmentProposalSummaryResponse` — add 1 field

```typescript
export interface AccommodationAssignmentProposalSummaryResponse {
  id: string
  campEditionId: string
  name: string
  notes: string | null
  isActive: boolean
  assignmentCount: number
  unassignedCount: number
  createdByUserId: string
  createdAt: string
  updatedAt: string
  // NEW:
  lastModifiedByUserName: string | null
}
```

---

### Step 2: Update `AccommodationSlotCard.vue`

**File:** `frontend/src/components/camps/AccommodationSlotCard.vue`

This component currently has 3 props. All changes are additive.

#### 2a. Add prop `hasFriendlyFamilyInZone`

Update `defineProps` to include the new boolean:

```typescript
const props = defineProps<{
  accommodation: AssignmentAccommodationResponse
  assignedFamilies: AssignmentFamilyResponse[]
  selectedFamily: AssignmentFamilyResponse | null
  hasFriendlyFamilyInZone: boolean   // NEW — computed by parent AccommodationAssignmentPanel
}>()
```

#### 2b. Add new computed properties (insert before the existing `signalClass`)

```typescript
const allFeaturesMatch = computed(() => {
  if (!props.selectedFamily) return false
  const required = props.selectedFamily.requiredFeatures
  if (required.length === 0) return false
  return required.every((feat) => props.accommodation.availableFeatures.includes(feat))
})

const missingFeatures = computed(() => {
  if (!props.selectedFamily) return []
  return props.selectedFamily.requiredFeatures.filter(
    (feat) => !props.accommodation.availableFeatures.includes(feat)
  )
})

const hasFriendlyFamilyHere = computed(() => {
  if (!props.selectedFamily || props.selectedFamily.friendlyFamilyUnitIds.length === 0) return false
  return props.assignedFamilies.some((f) =>
    props.selectedFamily!.friendlyFamilyUnitIds.includes(f.familyUnitId)
  )
})

const canFitSelectedFamily = computed(() => {
  if (!props.selectedFamily || props.accommodation.capacity === null) return true
  const needed = props.accommodation.countByFamily ? 1 : props.selectedFamily.memberCount
  return (props.accommodation.capacity - occupiedUnits.value) >= needed
})
```

#### 2c. Replace `signalClass` computed entirely

The existing `signalClass` only handles preferences and over-capacity. Replace it with the full priority chain:

```typescript
const signalClass = computed(() => {
  if (!props.selectedFamily) return 'border-gray-200'

  // Priority 1 — Red: family does not fit
  const needed = props.accommodation.countByFamily ? 1 : props.selectedFamily.memberCount
  const remaining = props.accommodation.capacity === null
    ? Infinity
    : props.accommodation.capacity - occupiedUnits.value
  if (remaining < needed) return 'border-red-500 ring-1 ring-red-400'

  // Priority 2 — Green: 1st preference OR all features match OR friendly family already here
  const prefs = props.selectedFamily.accommodationPreferences
  const pref = prefs.find((p) => p.accommodationId === props.accommodation.id)
  if (pref?.preferenceOrder === 1 || allFeaturesMatch.value || hasFriendlyFamilyHere.value) {
    return 'border-green-400 ring-1 ring-green-300'
  }

  // Priority 3 — Blue: friendly family in same zone (different accommodation)
  if (props.hasFriendlyFamilyInZone) return 'border-blue-400 ring-1 ring-blue-300'

  // Priority 4 — Amber: 2nd/3rd preference OR missing required features
  if (pref?.preferenceOrder === 2 || pref?.preferenceOrder === 3 || missingFeatures.value.length > 0) {
    return 'border-amber-400 ring-1 ring-amber-300'
  }

  return 'border-blue-200'
})
```

#### 2d. Update capacity display in template

Replace the existing capacity counter `<span>` with the improved version that shows "Necesitan X, quedan Y" when the family cannot fit:

```html
<span
  class="text-xs"
  :class="isOverCapacity ? 'font-bold text-red-600' : canFitSelectedFamily ? 'text-gray-500' : 'font-medium text-red-500'"
>
  <template v-if="!selectedFamily || canFitSelectedFamily || isOverCapacity">
    {{ occupiedUnits }} / {{ accommodation.capacity ?? '∞' }}
    {{ accommodation.countByFamily ? 'fam.' : 'pers.' }}
  </template>
  <template v-else>
    Necesitan {{ accommodation.countByFamily ? '1 plaza' : `${selectedFamily.memberCount} pers.` }},
    quedan {{ Math.max(0, (accommodation.capacity ?? 0) - occupiedUnits) }}
  </template>
</span>
```

#### 2e. Add compatibility badges in template

Insert after the capacity counter (still inside the card header area), before the family chip list:

```html
<!-- Compatibility badges — visible only when a family is selected -->
<div v-if="selectedFamily" class="mt-1 flex flex-wrap gap-1">
  <span
    v-if="allFeaturesMatch"
    class="rounded bg-green-100 px-1.5 py-0.5 text-xs text-green-700"
    title="El alojamiento tiene todas las características requeridas"
  >
    Cumple todas las preferencias
  </span>

  <span
    v-if="hasFriendlyFamilyHere"
    class="rounded bg-green-100 px-1.5 py-0.5 text-xs text-green-700"
  >
    Familia amiga ya aquí
  </span>

  <span
    v-if="hasFriendlyFamilyInZone && !hasFriendlyFamilyHere"
    class="rounded bg-blue-100 px-1.5 py-0.5 text-xs text-blue-700"
  >
    Familia amiga en misma zona
  </span>

  <span
    v-if="missingFeatures.length > 0"
    class="rounded bg-amber-100 px-1.5 py-0.5 text-xs text-amber-700"
    :title="`Faltan: ${missingFeatures.join(', ')}`"
  >
    Preferencia no cubierta: {{ missingFeatures.join(', ') }}
  </span>
</div>
```

---

### Step 3: Update `AccommodationAssignmentPanel.vue`

**File:** `frontend/src/components/camps/AccommodationAssignmentPanel.vue`

#### 3a. Add imports

```typescript
import Select from 'primevue/select'
import ToggleSwitch from 'primevue/toggleswitch'
```

#### 3b. Add filter state refs (family panel)

```typescript
const filterSpecialNeeds = ref(false)
```

#### 3c. Add filter state refs (accommodation panel)

```typescript
const filterType = ref<string | null>(null)
const filterZone = ref<string | null>(null)
const filterOnlyAvailable = ref(false)
```

#### 3d. Add `friendlyFamilyInZoneMap` computed

Insert after `selectedFamily`:

```typescript
const friendlyFamilyInZoneMap = computed((): Map<string, boolean> => {
  const map = new Map<string, boolean>()
  if (!selectedFamily.value || selectedFamily.value.friendlyFamilyUnitIds.length === 0) {
    props.state.accommodations.forEach((acc) => map.set(acc.id, false))
    return map
  }

  const zoneToAccIds = new Map<string | null, string[]>()
  for (const acc of props.state.accommodations) {
    const zoneKey = acc.zoneId ?? null
    if (!zoneToAccIds.has(zoneKey)) zoneToAccIds.set(zoneKey, [])
    zoneToAccIds.get(zoneKey)!.push(acc.id)
  }

  for (const acc of props.state.accommodations) {
    const zoneKey = acc.zoneId ?? null
    const sameZoneAccIds = (zoneToAccIds.get(zoneKey) ?? []).filter((id) => id !== acc.id)

    const hasFriendlyInZone = sameZoneAccIds.some((sameZoneAccId) => {
      const familiesHere = assignedFamiliesFor(sameZoneAccId)
      return familiesHere.some((f) =>
        selectedFamily.value!.friendlyFamilyUnitIds.includes(f.familyUnitId)
      )
    })

    map.set(acc.id, hasFriendlyInZone)
  }
  return map
})
```

#### 3e. Update `filteredFamilies` to include special-needs filter

```typescript
const filteredFamilies = computed(() => {
  const q = searchQuery.value.toLowerCase()
  return sortedFamilies.value.filter((f) => {
    const matchesSearch =
      !q ||
      f.familyName.toLowerCase().includes(q) ||
      f.representativeName.toLowerCase().includes(q)
    const matchesSpecialNeeds = !filterSpecialNeeds.value || f.hasSpecialNeeds
    return matchesSearch && matchesSpecialNeeds
  })
})
```

#### 3f. Add accommodation filter option computeds

```typescript
const availableTypeOptions = computed(() => {
  const types = [...new Set(props.state.accommodations.map((a) => a.type))]
  return types.map((t) => ({ label: ACCOMMODATION_TYPE_LABELS[t as AccommodationTypeValue], value: t }))
})

const availableZoneOptions = computed(() => {
  const zones = [
    ...new Set(
      props.state.accommodations.map((a) => a.zoneName).filter((z): z is string => z !== null)
    ),
  ]
  return zones.map((z) => ({ label: z, value: z }))
})
```

#### 3g. Update `groupedAccommodations` to apply accommodation filters

```typescript
const groupedAccommodations = computed((): Map<string, Map<string, AssignmentAccommodationResponse[]>> => {
  const byType = new Map<string, Map<string, AssignmentAccommodationResponse[]>>()
  const sorted = [...props.state.accommodations].sort((a, b) => a.sortOrder - b.sortOrder)

  for (const acc of sorted) {
    if (filterType.value && acc.type !== filterType.value) continue
    if (filterZone.value && acc.zoneName !== filterZone.value) continue
    if (filterOnlyAvailable.value) {
      const families = assignedFamiliesFor(acc.id)
      const used = acc.countByFamily
        ? families.length
        : families.reduce((sum, f) => sum + f.memberCount, 0)
      if (acc.capacity !== null && used >= acc.capacity) continue
    }

    if (!byType.has(acc.type)) byType.set(acc.type, new Map())
    const byZone = byType.get(acc.type)!
    const zoneKey = acc.zoneName ?? 'Sin zona'
    if (!byZone.has(zoneKey)) byZone.set(zoneKey, [])
    byZone.get(zoneKey)!.push(acc)
  }
  return byType
})
```

#### 3h. Template — add family panel filter toggle

Insert after the `IconField` search box (left panel):

```html
<div class="mt-2 flex items-center gap-2">
  <ToggleSwitch v-model="filterSpecialNeeds" input-id="filter-special-needs" size="small" />
  <label for="filter-special-needs" class="cursor-pointer text-xs text-gray-600">
    Solo con necesidades especiales
  </label>
</div>
```

#### 3i. Template — add accommodation filter bar

Insert above the accommodation grid (right panel), before the `v-for` loop over `groupedAccommodations`:

```html
<div class="mb-4 flex flex-wrap items-center gap-2">
  <Select
    v-model="filterType"
    :options="availableTypeOptions"
    option-label="label"
    option-value="value"
    placeholder="Todos los tipos"
    show-clear
    class="w-44"
    size="small"
  />
  <Select
    v-model="filterZone"
    :options="availableZoneOptions"
    option-label="label"
    option-value="value"
    placeholder="Todas las zonas"
    show-clear
    class="w-44"
    size="small"
  />
  <div class="flex items-center gap-1.5">
    <ToggleSwitch v-model="filterOnlyAvailable" input-id="filter-available" size="small" />
    <label for="filter-available" class="cursor-pointer text-xs text-gray-600">
      Solo disponibles
    </label>
  </div>
</div>
```

#### 3j. Template — pass `hasFriendlyFamilyInZone` to `AccommodationSlotCard`

Find the `<AccommodationSlotCard>` usage and add the new prop:

```html
<AccommodationSlotCard
  v-for="acc in accommodations"
  :key="acc.id"
  :accommodation="acc"
  :assigned-families="assignedFamiliesFor(acc.id)"
  :selected-family="selectedFamily"
  :has-friendly-family-in-zone="friendlyFamilyInZoneMap.get(acc.id) ?? false"
  @assign="handleAssign"
  @unassign="$emit('unassign', $event)"
/>
```

---

### Step 4: Update `ProposalSelectorBar.vue`

**File:** `frontend/src/components/camps/ProposalSelectorBar.vue`

Locate the stats span (shows "X sin asignar · Y asignadas") and extend it to show the last-modifier line below:

```html
<span v-if="selectedProposal" class="ml-auto flex flex-col items-end text-right">
  <span class="text-sm text-gray-500">
    {{ selectedProposal.unassignedCount }} sin asignar · {{ selectedProposal.assignmentCount }} asignadas
  </span>
  <!-- NEW -->
  <span
    v-if="selectedProposal.lastModifiedByUserName"
    class="text-xs text-gray-400"
  >
    Última modificación por {{ selectedProposal.lastModifiedByUserName }}
  </span>
</span>
```

No new computed, no new props — `lastModifiedByUserName` is already part of `AccommodationAssignmentProposalSummaryResponse` after Step 1.

---

### Step 5: Update `AccommodationSlotCard.test.ts`

**File:** `frontend/src/components/camps/__tests__/AccommodationSlotCard.test.ts`

#### 5a. Update `makeFamily` helper

The factory must pass the 3 new fields:

```typescript
function makeFamily(overrides: Partial<AssignmentFamilyResponse> = {}): AssignmentFamilyResponse {
  return {
    registrationId: 'reg-1',
    familyUnitId: 'fu-1',
    familyName: 'Test Family',
    representativeName: 'Test Rep',
    memberCount: 2,
    adultCount: 2,
    childCount: 0,
    hasPet: false,
    specialNeeds: null,
    campatesPreference: null,
    accommodationPreferences: [],
    hasSpecialNeeds: false,
    requiredFeatures: [],
    friendlyFamilyUnitIds: [],
    ...overrides,
  }
}
```

#### 5b. Update `makeAccommodation` helper

```typescript
function makeAccommodation(overrides: Partial<AssignmentAccommodationResponse> = {}): AssignmentAccommodationResponse {
  return {
    id: 'acc-1',
    name: 'Test Accommodation',
    type: 'Lodge',
    capacity: 4,
    countByFamily: false,
    zoneId: null,
    zoneName: null,
    sortOrder: 0,
    availableFeatures: [],
    ...overrides,
  }
}
```

#### 5c. New test cases to add

```typescript
it('showsGreenBadge_whenAllFeaturesMatch', () => {
  // family requires feat-1; accommodation has feat-1 → badge "Cumple todas las preferencias"
})

it('showsGreenBadge_whenFriendlyFamilyIsAlreadyAssignedHere', () => {
  // family has friendlyFamilyUnitIds: ['fu-friend']
  // assigned families includes one with familyUnitId 'fu-friend'
  // badge "Familia amiga ya aquí" visible
})

it('showsBlueBadge_whenFriendlyFamilyInZone_hasFriendlyFamilyInZone_prop_true', () => {
  // prop hasFriendlyFamilyInZone=true, hasFriendlyFamilyHere=false
  // badge "Familia amiga en misma zona" visible
})

it('showsAmberBadge_whenSomeRequiredFeaturesAreMissing', () => {
  // family requires ['feat-1', 'feat-2']; accommodation has ['feat-1']
  // badge "Preferencia no cubierta: feat-2" visible
})

it('showsMissingFeaturesList_inAmberBadge', () => {
  // verify :title attribute contains missing feature ID
})

it('showsImprovedCapacityMessage_whenFamilyDoesNotFit', () => {
  // capacity 2, occupied 2, family of 3 → text "Necesitan 3 pers., quedan 0"
})

it('signalClass_isGreen_whenAllFeaturesMatchEvenWithNoPreference', () => {
  // family has no accommodationPreferences but requiredFeatures all match
  // signalClass includes 'border-green-400'
})

it('signalClass_priority_redBeatsGreen', () => {
  // family fits 1st preference but capacity is full → signalClass is red, not green
})

it('signalClass_isBlue_whenHasFriendlyFamilyInZone', () => {
  // hasFriendlyFamilyInZone=true, no preference, no features → border-blue-400
})
```

For each test, mount `AccommodationSlotCard` with the `hasFriendlyFamilyInZone` prop (default `false`). Use the same PrimeVue mock pattern already in the file.

---

### Step 6: Update Technical Documentation

**Action:** After implementation, update:

1. **`ai-specs/specs/api-spec.yml`** — The new fields (`hasSpecialNeeds`, `requiredFeatures`, `friendlyFamilyUnitIds`, `availableFeatures`, `lastModifiedByUserName`) must be reflected in the relevant response schemas. These changes mirror what the backend plan already documented; verify consistency.
2. No routing, store, or composable documentation changes needed.

---

## Implementation Order

1. Step 0 — Create branch `feature/feat-assignment-board-v2-frontend`
2. Step 1 — Extend TypeScript interfaces
3. Step 2 — `AccommodationSlotCard.vue` (prop, computeds, signalClass, badges, capacity)
4. Step 3 — `AccommodationAssignmentPanel.vue` (computed map, filters, template)
5. Step 4 — `ProposalSelectorBar.vue` (last-modifier line)
6. Step 5 — Update tests
7. Step 6 — Documentation

---

## Testing Checklist

- [ ] `AccommodationSlotCard` — green badge "Cumple todas las preferencias" when all features match
- [ ] `AccommodationSlotCard` — green badge "Familia amiga ya aquí" when friendly family is in the same accommodation
- [ ] `AccommodationSlotCard` — blue badge "Familia amiga en misma zona" when `hasFriendlyFamilyInZone=true` and friendly family is not here
- [ ] `AccommodationSlotCard` — amber badge "Preferencia no cubierta: {list}" when features are missing
- [ ] `AccommodationSlotCard` — improved "Necesitan X, quedan Y" message when family does not fit
- [ ] `AccommodationSlotCard` — signal priority: red > green > blue > amber respected
- [ ] `AccommodationSlotCard` — no badges visible when `selectedFamily` is null
- [ ] `AccommodationAssignmentPanel` — "Solo con necesidades especiales" toggle filters family list
- [ ] `AccommodationAssignmentPanel` — type/zone selects filter the accommodation grid
- [ ] `AccommodationAssignmentPanel` — "Solo disponibles" toggle hides full accommodations
- [ ] `AccommodationAssignmentPanel` — filters are combinable simultaneously
- [ ] `AccommodationAssignmentPanel` — clearing a Select restores all accommodations
- [ ] `ProposalSelectorBar` — "Última modificación por {name}" line appears when `lastModifiedByUserName` is set
- [ ] `ProposalSelectorBar` — line is absent when `lastModifiedByUserName` is null
- [ ] All existing `AccommodationSlotCard` tests still pass after helper updates

---

## Error Handling Patterns

No new API calls are introduced. All new logic is computed from data already in `ProposalAssignmentStateResponse`. There are no new error states to handle.

If backend has not yet merged the new DTO fields, the frontend will receive `undefined` for `requiredFeatures`, `availableFeatures`, and `friendlyFamilyUnitIds`. Guard against this defensively in the computeds:

```typescript
const required = props.selectedFamily?.requiredFeatures ?? []
```

This matches the existing pattern in the codebase for optional fields.

---

## UI/UX Considerations

- **Signal priority (card border color):** Red → Green → Blue → Amber → Blue-light. Implemented as a single `signalClass` computed; only one signal is active at a time.
- **Badges:** Visible only when a family is selected (`v-if="selectedFamily"`). Multiple badges can appear simultaneously (e.g., "Cumple todas las preferencias" + "Familia amiga ya aquí").
- **Filter bar placement:** Accommodation filters appear above the grid, inside the right panel, above the type/zone group headings.
- **Filter toggles:** `ToggleSwitch size="small"` (consistent with existing usage in the Camps feature).
- **Filter selects:** `Select size="small" show-clear class="w-44"` (consistent with `ProposalSelectorBar.vue` usage).
- **Last-modifier text:** `text-xs text-gray-400` below the existing stats line — unobtrusive.
- **No mobile breakpoint changes needed** — the board is already desktop-only (fixed left panel, wide grid).

---

## Dependencies

No new npm packages. PrimeVue components used:

| Component | Already imported in Camps folder? |
|-----------|----------------------------------|
| `primevue/select` | Yes (ProposalSelectorBar, others) |
| `primevue/toggleswitch` | Yes (AccommodationFeatureDialog, others) |

No additions to `main.ts` or PrimeVue plugin config required.

---

## Notes

- **`ACCOMMODATION_TYPE_LABELS`** is already defined in `accommodation-assignment.ts` and used in `AccommodationAssignmentPanel`. Import it directly for `availableTypeOptions`. No duplication.
- **`assignedFamiliesFor(accId)`** is an existing helper in `AccommodationAssignmentPanel` — the `friendlyFamilyInZoneMap` computed uses it. No refactoring needed.
- **Test helper shape change** — updating `makeFamily` and `makeAccommodation` factories in the test file will cause TypeScript compile errors on existing tests until all calls pass the new required fields. Use object spread overrides (as shown in Step 5) to keep existing tests terse.
- **`hasFriendlyFamilyInZone` is a prop, not a computed inside `AccommodationSlotCard`** — the parent panel computes the full map once and passes per-card booleans. This avoids re-running the zone scan inside every card.
- **All variable and function names in English** per `base-standards.mdc`. Spanish only in UI strings.
- **Branch:** `feature/feat-assignment-board-v2-frontend` from `dev`. PR target: `dev`.

---

## Next Steps After Implementation

- After both backend and frontend PRs are merged to `dev`, run a full Cypress smoke test on the assignment board page.
- The `feat-assignment-board-v2` spec checklist (acceptance criteria) can serve as the manual test script.
