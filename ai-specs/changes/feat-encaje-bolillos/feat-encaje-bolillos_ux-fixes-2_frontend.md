# Frontend Implementation Plan: feat-encaje-bolillos-ux-fixes-2

## 1. Overview

Four targeted UX fixes on the accommodation assignment screen (`AccommodationAssignmentView`). All changes are pure frontend — no new API calls, no new composables, no routing changes. The scope is limited to three existing components in `frontend/src/components/camps/`.

Tech: Vue 3 Composition API (`<script setup lang="ts">`), PrimeVue `Select`, Tailwind CSS utility classes.

---

## 2. Architecture Context

### Components involved

| Component | Role |
| --- | --- |
| `AccommodationAssignmentPanel.vue` | Right panel: accommodation grid + filter bar + family detail card |
| `AccommodationSlotCard.vue` | Individual accommodation tile in the grid |
| `FamilyAssignmentCard.vue` | Family card in left panel list |

### State & data flow

- `ProposalAssignmentStateResponse.allFeatures: AccommodationFeatureSummary[]` — already available on `props.state` inside `AccommodationAssignmentPanel`. Contains `{ id, name, icon }` for every feature.
- `AssignmentFamilyResponse.requiredFeatures: string[]` — UUIDs of required features per family.
- `AssignmentAccommodationResponse.availableFeatures: string[]` — UUIDs of features present in each accommodation.
- `accommodation.capacity: number | null` — the field used for the new capacity filter.

### No changes needed in

- `frontend/src/types/accommodation-assignment.ts` — no new types.
- Backend — purely visual changes.
- Routing — no new routes.
- Pinia stores — all state is local `ref`/`computed`.

---

## 3. Implementation Steps

### Step 0: Create feature branch

- **Action**: Create and switch to `feature/feat-encaje-bolillos-ux-fixes-2-frontend`.
- **Base branch**: `dev` (project policy — PRs target `dev`, not `main`).
- **Commands**:
  ```bash
  git checkout dev
  git pull origin dev
  git checkout -b feature/feat-encaje-bolillos-ux-fixes-2-frontend
  git branch
  ```

---

### Step 1: Fix "Preferencia no cubierta" — show feature names instead of UUIDs

**Files**: `AccommodationAssignmentPanel.vue`, `AccommodationSlotCard.vue`

#### 1a — `AccommodationAssignmentPanel.vue` (script)

Add a `featureMap` computed after the existing `availableFeatures` computed (~line 79):

```ts
const featureMap = computed((): Map<string, string> => {
  const map = new Map<string, string>()
  ;(props.state.allFeatures ?? []).forEach((f) => map.set(f.id, f.name))
  return map
})
```

Pass it to each `<AccommodationSlotCard />` in the template (~line 418):

```html
<AccommodationSlotCard
  ...
  :feature-map="featureMap"
  ...
/>
```

#### 1b — `AccommodationSlotCard.vue` (script)

1. Add prop `featureMap: Map<string, string>` to `defineProps`:
   ```ts
   const props = defineProps<{
     accommodation: AssignmentAccommodationResponse
     assignedFamilies: AssignmentFamilyResponse[]
     selectedFamily: AssignmentFamilyResponse | null
     hasFriendlyFamilyInZone: boolean
     featureMap: Map<string, string>
   }>()
   ```

2. Add `missingFeatureNames` computed after `missingFeatures`:
   ```ts
   const missingFeatureNames = computed(() =>
     missingFeatures.value.map((id) => props.featureMap.get(id) ?? id)
   )
   ```

#### 1c — `AccommodationSlotCard.vue` (template)

Replace the badge text (~line 172):

```html
<!-- Before: -->
Preferencia no cubierta: {{ missingFeatures.join(', ') }}

<!-- After: -->
Preferencia no cubierta: {{ missingFeatureNames.join(', ') }}
```

Also update the `title` attribute on the same span:

```html
<!-- Before: -->
:title="`Faltan: ${missingFeatures.join(', ')}`"

<!-- After: -->
:title="`Faltan: ${missingFeatureNames.join(', ')}`"
```

---

### Step 2: Shorten the occupancy counter

**File**: `AccommodationSlotCard.vue` (template, ~lines 121–127)

Two sub-changes:

#### 2a — Main label

```html
<!-- Before: -->
{{ accommodation.countByFamily ? 'fam.' : 'pers.' }}

<!-- After: -->
{{ accommodation.countByFamily ? 'f.' : 'p.' }}
```

#### 2b — "No cabe" fallback message

```html
<!-- Before: -->
Necesitan {{ accommodation.countByFamily ? '1 plaza' : `${selectedFamily.memberCount} pers.` }},
quedan {{ Math.max(0, (accommodation.capacity ?? 0) - occupiedUnits) }}

<!-- After: -->
Necesitan {{ accommodation.countByFamily ? '1' : selectedFamily.memberCount }} p.,
quedan {{ Math.max(0, (accommodation.capacity ?? 0) - occupiedUnits) }}
```

---

### Step 3: Capacity range filter

**File**: `AccommodationAssignmentPanel.vue`

#### 3a — Reactive state (script, after existing `filterOnlyAvailable`)

```ts
const filterCapacityMin = ref<number | null>(null)
const filterCapacityMax = ref<number | null>(null)
```

#### 3b — Options constant (script, top of `<script setup>`, after imports)

```ts
const CAPACITY_OPTIONS = [
  { label: '1', value: 1 },
  { label: '2', value: 2 },
  { label: '3', value: 3 },
  { label: '4', value: 4 },
  { label: '5', value: 5 },
  { label: '6', value: 6 },
  { label: '7', value: 7 },
  { label: '8', value: 8 },
  { label: '9', value: 9 },
  { label: '10+', value: 10 },
]
```

#### 3c — Filter logic in `groupedAccommodations` computed (after the `filterOnlyAvailable` block, ~line 197)

```ts
if (filterCapacityMin.value !== null) {
  const cap = acc.capacity ?? Infinity
  if (cap < filterCapacityMin.value) continue
}
if (filterCapacityMax.value !== null && filterCapacityMax.value < 10) {
  const cap = acc.capacity ?? Infinity
  if (cap > filterCapacityMax.value) continue
}
```

> Rule: accommodations with `capacity === null` (unlimited) always pass the filter.
> Rule: selecting `10` in the Max dropdown means `≥ 10`, so no upper-bound check is applied (`filterCapacityMax.value < 10` guards this).

#### 3d — Template — add capacity selects to the filter bar

Locate the "Zone + availability filters" row (~line 361). Add the capacity range controls after the existing `<Select>` for zone and before (or after) the availability toggle:

```html
<!-- Capacity range filter -->
<div class="flex items-center gap-1 text-xs text-gray-600">
  <span>Cap.</span>
  <Select
    v-model="filterCapacityMin"
    :options="CAPACITY_OPTIONS"
    option-label="label"
    option-value="value"
    placeholder="Mín"
    show-clear
    class="w-20"
    size="small"
  />
  <span>–</span>
  <Select
    v-model="filterCapacityMax"
    :options="CAPACITY_OPTIONS"
    option-label="label"
    option-value="value"
    placeholder="Máx"
    show-clear
    class="w-20"
    size="small"
  />
</div>
```

`Select` is already imported in `AccommodationAssignmentPanel.vue` — no new imports needed.

---

### Step 4: Replace pet icon with "Con mascotas" label

#### 4a — `AccommodationAssignmentPanel.vue` (~line 288, family detail panel)

```html
<!-- Before: -->
<span v-if="selectedFamily.hasPet" class="font-medium text-amber-600">
  <i class="pi pi-heart-fill mr-0.5" />Mascota
</span>

<!-- After: -->
<span v-if="selectedFamily.hasPet" class="font-medium text-amber-600">
  Con mascotas
</span>
```

#### 4b — `FamilyAssignmentCard.vue` (~line 51, compact icon in list card)

```html
<!-- Before: -->
<i
  v-if="family.hasPet"
  class="pi pi-heart text-xs text-amber-500"
  v-tooltip.top="'Viaja con mascota'"
  aria-label="Viaja con mascota"
/>

<!-- After: -->
<span
  v-if="family.hasPet"
  class="rounded-full border border-amber-300 bg-amber-50 px-1.5 py-0.5 text-[10px] text-amber-700"
>
  Con mascotas
</span>
```

The chip style mirrors the existing preference chips in that same component for visual consistency.

---

### Step 5: Update technical documentation

- **Action**: Review if any of these changes affects documented patterns.
- **Assessment**: No new patterns or components are introduced. No doc updates required in `ai-specs/specs/`.
- If the reviewer disagrees, the only candidate is `ai-specs/specs/frontend-standards.mdc` under "UI and UX Standards" — but these are domain-specific tweaks, not cross-cutting patterns.

---

## 4. Implementation Order

1. Step 0 — Create branch `feature/feat-encaje-bolillos-ux-fixes-2-frontend`
2. Step 1 — Fix "Preferencia no cubierta" UUIDs → names
3. Step 2 — Shorten occupancy counter
4. Step 3 — Capacity range filter
5. Step 4 — Replace pet icons with "Con mascotas"
6. Step 5 — Documentation review

---

## 5. Testing Checklist

### Manual (in browser)

- [ ] Select a family with `requiredFeatures` set; badge shows readable names, not UUIDs.
- [ ] Select a family with no `requiredFeatures`; no badge appears.
- [ ] Occupancy counter reads "2 / 6 p." and "1 / 4 f." depending on `countByFamily`.
- [ ] When a family does not fit, fallback text is abbreviated correctly.
- [ ] Setting capacity min to 4 hides accommodations with `capacity < 4`.
- [ ] Setting capacity max to 3 hides accommodations with `capacity > 3`.
- [ ] Setting capacity max to "10+" shows all accommodations with `capacity >= 10`.
- [ ] Accommodations with `capacity === null` are never hidden by the capacity filter.
- [ ] Clearing both capacity selects restores full list.
- [ ] Family cards with `hasPet = true` show "Con mascotas" chip.
- [ ] Family detail panel shows "Con mascotas" without any icon.
- [ ] Families without `hasPet` show no pet indicator.

### Unit tests (Vitest)

No composables or stores changed — unit test scope is limited to component props/computed behavior. Tests are optional for this change set given it is template-only work, but if written:

- `AccommodationSlotCard.vue` — test that `missingFeatureNames` correctly maps IDs via `featureMap`.
- `AccommodationAssignmentPanel.vue` — test that `groupedAccommodations` filters correctly for min/max capacity edge cases (especially `null` capacity passthrough and `10+` max).

---

## 6. Error Handling

No async operations are introduced. No new error states required. The `featureMap.get(id) ?? id` fallback in `missingFeatureNames` ensures the badge never breaks even if `allFeatures` is empty or incomplete.

---

## 7. UI/UX Considerations

- **Capacity selects width**: `w-20` (5rem) fits a 2-digit number plus the "10+" label without overflow.
- **Filter bar wrapping**: The filter bar already uses `flex flex-wrap items-center gap-2` — the new capacity block slots in naturally.
- **Chip sizing in `FamilyAssignmentCard`**: `text-[10px]` matches the existing preference order chips, keeping visual density consistent.
- **No responsive breakpoint changes needed**: The accommodation panel is already scoped to the right column of the 2-column grid.

---

## 8. Dependencies

No new npm packages. No new PrimeVue components (all components are already imported in the affected files).

---

## 9. Notes

- The `featureMap` prop on `AccommodationSlotCard` does not have a default value. All call sites go through `AccommodationAssignmentPanel`, which always provides it. If the component is ever used standalone in tests, pass `new Map()`.
- `CAPACITY_OPTIONS` is defined as a plain `const` array (not `computed` or `ref`) since it is static — no reactivity needed.
- The "10+" value uses `value: 10` (a number) so that the filter logic can compare with `accommodation.capacity` (`number | null`) without type coercion.
- UI text stays in Spanish per `frontend-standards.mdc` language rules: "Cap.", "Mín", "Máx", "Con mascotas".

---

## 10. Next Steps After Implementation

- Open PR targeting `dev` branch.
- Tag for review as part of the ongoing Encaje de Bolillos UX fixes iteration.
