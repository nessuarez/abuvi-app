# Frontend Implementation Plan: feat-encaje-bolillos-ux-fixes

## Overview

Five frontend changes for the Encaje de Bolillos accommodation-assignment board. Two are pure frontend bug/UX fixes (ProgressBar, zone thumbnail fallback). Three require consuming new backend DTO fields added by the companion backend ticket (`accommodationTypeLookup`, `allFeatures`, `babyCount`). Fix 1 (zone gallery 405) requires **no frontend change** — the backend now exposes the `GET /{zoneId}` endpoint the panel already calls.

All work is in `<script setup lang="ts">` components and `.ts` type files. No new route, composable, or Pinia store is needed.

---

## Architecture Context

| File | Change |
|------|--------|
| `frontend/src/types/accommodation-assignment.ts` | Add `AccommodationTypeLookupItem`, `AccommodationFeatureSummary`; add `babyCount` to `AssignmentFamilyResponse`; add `accommodationTypeLookup` + `allFeatures` to `ProposalAssignmentStateResponse` |
| `frontend/src/components/camps/AccommodationAssignmentPanel.vue` | Consume new lookup fields to fix preference icons; add feature filter chips; expand selected-family detail banner |
| `frontend/src/components/camps/AccommodationSlotCard.vue` | Hide ProgressBar when empty; remove zone thumbnail fallback |

No new components, composables, routes, or Pinia stores needed.

---

## Implementation Steps

### Step 0 — Create feature branch

- **Base branch**: `dev`
- **Branch name**: `feature/feat-encaje-bolillos-ux-fixes-frontend`

```bash
git checkout dev
git pull origin dev
git checkout -b feature/feat-encaje-bolillos-ux-fixes-frontend
```

> Must be the FIRST step before any code changes.

---

### Step 1 — Update TypeScript types

**File:** `frontend/src/types/accommodation-assignment.ts`

#### 1a — Add new interfaces after `AssignmentEntry`

```typescript
export interface AccommodationTypeLookupItem {
  id: string
  type: AccommodationTypeValue
}

export interface AccommodationFeatureSummary {
  id: string
  name: string
  icon: string
}
```

#### 1b — Add `babyCount` to `AssignmentFamilyResponse`

Insert `babyCount: number` **after** `childCount` and **before** `hasPet`:

```typescript
export interface AssignmentFamilyResponse {
  registrationId: string
  familyUnitId: string
  familyName: string
  representativeName: string
  memberCount: number
  adultCount: number
  childCount: number
  babyCount: number          // ADD
  hasPet: boolean
  specialNeeds: string | null
  campatesPreference: string | null
  accommodationPreferences: AccommodationPreferenceItem[]
  hasSpecialNeeds: boolean
  requiredFeatures: string[]
  friendlyFamilyUnitIds: string[]
}
```

#### 1c — Extend `ProposalAssignmentStateResponse`

```typescript
export interface ProposalAssignmentStateResponse {
  proposalId: string
  families: AssignmentFamilyResponse[]
  accommodations: AssignmentAccommodationResponse[]
  assignments: AssignmentEntry[]
  accommodationTypeLookup: AccommodationTypeLookupItem[]   // ADD
  allFeatures: AccommodationFeatureSummary[]               // ADD
}
```

**Implementation notes:**
- These fields are always present once the backend ticket is merged. Existing tests/fixtures that construct `ProposalAssignmentStateResponse` need `accommodationTypeLookup: []` and `allFeatures: []` added.
- `babyCount` is a required field — TypeScript will surface any usages that construct `AssignmentFamilyResponse` manually.

---

### Step 2 — Fix `AccommodationSlotCard.vue`

**File:** `frontend/src/components/camps/AccommodationSlotCard.vue`

Two changes, both in `<script setup>`:

#### 2a — Remove zone thumbnail fallback

```typescript
// Before
const displayThumbnail = computed(
  () => props.accommodation.primaryThumbnailUrl ?? props.accommodation.zonePrimaryThumbnailUrl ?? null
)
const thumbnailIsZoneFallback = computed(
  () => !props.accommodation.primaryThumbnailUrl && !!props.accommodation.zonePrimaryThumbnailUrl
)

// After
const displayThumbnail = computed(() => props.accommodation.primaryThumbnailUrl ?? null)
```

Delete the `thumbnailIsZoneFallback` computed entirely.

#### 2b — Remove "zona" fallback elements from template

In the `<template>`, the thumbnail `<div>` currently has:
- `:class="thumbnailIsZoneFallback ? 'h-7 w-7 opacity-60' : 'h-8 w-8'"`
- A `<span v-if="thumbnailIsZoneFallback">` overlay with "zona" label

Simplify the thumbnail container:
```html
<div
  v-if="displayThumbnail"
  class="absolute right-2 top-2 h-8 w-8 overflow-hidden rounded-md shadow-sm"
>
  <img
    :src="displayThumbnail"
    alt=""
    class="h-full w-full object-cover"
    @error="($event.target as HTMLImageElement).style.display = 'none'"
  />
</div>
```

Remove the `<span v-if="thumbnailIsZoneFallback">zona</span>` entirely.

#### 2c — Hide ProgressBar when no families assigned

```html
<!-- Before -->
<ProgressBar
  v-if="accommodation.capacity"
  ...

<!-- After -->
<ProgressBar
  v-if="accommodation.capacity && occupiedUnits > 0"
  ...
```

No other changes to `AccommodationSlotCard.vue`.

---

### Step 3 — Update `AccommodationAssignmentPanel.vue`

**File:** `frontend/src/components/camps/AccommodationAssignmentPanel.vue`

Three targeted changes:

#### 3a — Fix `accommodationTypeMap` to cover non-assignable accommodations (Fix 2)

Import the new types at the top of `<script setup>`:

```typescript
import type {
  ProposalAssignmentStateResponse,
  AssignmentFamilyResponse,
  AssignmentAccommodationResponse,
  AccommodationTypeValue,
  AccommodationFeatureSummary   // ADD
} from '@/types/accommodation-assignment'
```

Update the `accommodationTypeMap` computed to also seed from `accommodationTypeLookup`:

```typescript
// Before
const accommodationTypeMap = computed((): Map<string, AccommodationTypeValue> => {
  const map = new Map<string, AccommodationTypeValue>()
  props.state.accommodations.forEach((a) => map.set(a.id, a.type))
  return map
})

// After
const accommodationTypeMap = computed((): Map<string, AccommodationTypeValue> => {
  const map = new Map<string, AccommodationTypeValue>()
  props.state.accommodations.forEach((a) => map.set(a.id, a.type))
  props.state.accommodationTypeLookup.forEach((item) => map.set(item.id, item.type))
  return map
})
```

This ensures preference pills in `FamilyAssignmentCard` resolve correctly even when the preference targets a non-assignable accommodation not in the grid.

#### 3b — Add feature filter chips (Feature 4)

**3b-i** Add filter state (alongside the other filter refs):

```typescript
const activeFeatureFilter = ref<string | null>(null)  // feature ID or null
```

**3b-ii** Add computed for features present in the current proposal:

```typescript
const availableFeatures = computed((): AccommodationFeatureSummary[] => {
  const presentIds = new Set(props.state.accommodations.flatMap((a) => a.availableFeatures))
  return props.state.allFeatures.filter((f) => presentIds.has(f.id))
})
```

**3b-iii** Apply feature filter in `groupedAccommodations` — add a new `continue` guard after the existing zone/type/availability guards:

```typescript
if (activeFeatureFilter.value && !acc.availableFeatures.includes(activeFeatureFilter.value)) continue
```

**3b-iv** Reset feature filter when the proposal changes — add to the existing `watch(() => props.state.proposalId, ...)` or create one if it doesn't exist:

```typescript
watch(() => props.state.proposalId, () => {
  activeFeatureFilter.value = null
  activeTypeFilter.value = null
})
```

**3b-v** Feature chips template — add a second row of chips inside the existing filter bar `<div class="mb-4 space-y-2">`, below the type chips row. Show only when there are features to display:

```html
<!-- Feature filter chips (below type chips) -->
<div v-if="availableFeatures.length" class="flex flex-wrap gap-1">
  <button
    class="inline-flex items-center gap-1 rounded-full border px-2 py-0.5 text-xs transition-colors"
    :class="activeFeatureFilter === null
      ? 'border-indigo-500 bg-indigo-500 text-white'
      : 'border-gray-300 bg-white text-gray-600 hover:border-gray-400'"
    @click="activeFeatureFilter = null"
  >
    <i class="pi pi-tag text-[10px]" />
    Todas las características
  </button>
  <button
    v-for="feat in availableFeatures"
    :key="feat.id"
    class="inline-flex items-center gap-1 rounded-full border px-2 py-0.5 text-xs transition-colors"
    :class="activeFeatureFilter === feat.id
      ? 'border-indigo-500 bg-indigo-500 text-white'
      : 'border-gray-300 bg-white text-gray-600 hover:border-gray-400'"
    @click="activeFeatureFilter = activeFeatureFilter === feat.id ? null : feat.id"
  >
    <i :class="[feat.icon, 'text-[10px]']" />
    {{ feat.name }}
  </button>
</div>
```

> Feature chip icons come from the `icon` field on `AccommodationFeatureSummary` — they are PrimeIcon class strings stored in the database by the admin.

#### 3c — Expand selected-family detail banner (Enhancement 6)

Replace the existing blue info `<div v-if="selectedFamily">` with a richer multi-line panel:

```html
<div
  v-if="selectedFamily"
  class="mb-4 rounded-lg border border-blue-200 bg-blue-50 px-3 py-2 text-sm"
>
  <!-- Header row -->
  <div class="flex items-center justify-between">
    <span class="font-semibold text-blue-800">{{ selectedFamily.familyName }}</span>
    <span class="text-xs text-blue-500">Haz clic en un alojamiento para asignar</span>
  </div>

  <!-- Member composition -->
  <div class="mt-1 flex flex-wrap items-center gap-x-3 gap-y-0.5 text-xs text-blue-700">
    <span v-if="selectedFamily.adultCount > 0">
      <i class="pi pi-user mr-0.5" />
      {{ selectedFamily.adultCount }}
      {{ selectedFamily.adultCount === 1 ? 'adulto' : 'adultos' }}
    </span>
    <span v-if="selectedFamily.childCount > 0">
      <i class="pi pi-star mr-0.5" />
      {{ selectedFamily.childCount }}
      {{ selectedFamily.childCount === 1 ? 'niño' : 'niños' }}
    </span>
    <span v-if="selectedFamily.babyCount > 0">
      <i class="pi pi-heart mr-0.5" />
      {{ selectedFamily.babyCount }}
      {{ selectedFamily.babyCount === 1 ? 'bebé' : 'bebés' }}
    </span>
    <span v-if="selectedFamily.hasPet" class="text-amber-600 font-medium">
      <i class="pi pi-heart-fill mr-0.5" />Mascota
    </span>
  </div>

  <!-- Special needs (amber box) -->
  <div
    v-if="selectedFamily.specialNeeds"
    class="mt-1.5 rounded border border-amber-300 bg-amber-50 px-2 py-1 text-xs text-amber-800"
  >
    <i class="pi pi-exclamation-triangle mr-1 text-amber-500" />{{ selectedFamily.specialNeeds }}
  </div>

  <!-- Campates preference -->
  <p v-if="selectedFamily.campatesPreference" class="mt-1 text-xs italic text-blue-500">
    "{{ selectedFamily.campatesPreference }}"
  </p>
</div>
```

**Implementation notes:**
- Use PrimeIcons only: `pi-user` (adults), `pi-star` (children), `pi-heart` (babies), `pi-heart-fill` (pet). Do not add external icon libraries.
- Each count section is shown conditionally (`v-if="... > 0"`) so zero-count categories are invisible.
- `specialNeeds` is shown with an amber background to draw attention — not clipped, not truncated.
- `campatesPreference` is the free-text "with whom do you want to share camp" field — shown in italic below.

---

### Step 4 — TypeScript build check

Run the TypeScript compiler to verify no type errors:

```bash
cd frontend
npx vue-tsc --noEmit
```

Confirm zero errors before proceeding. Common issues to watch:
- Usages of `ProposalAssignmentStateResponse` in test fixtures or mock factories that need `accommodationTypeLookup: []` and `allFeatures: []` added.
- Usages of `AssignmentFamilyResponse` literals that need `babyCount: 0` (or real value) added.

---

### Step 5 — Manual browser verification

Start the dev server and verify each fix in the browser:

```bash
cd frontend && npm run dev
```

**Verification checklist:**

1. **Fix 1 (zone gallery)**: Open a proposal → click "ver fotos" on a zone header → gallery modal opens, photos load (HTTP 200 instead of 405). *Requires the backend ticket to be deployed first.*

2. **Fix 2 (preference icons)**: Open a proposal → family list on the left → preference pills on each family card show type icon + type label (not `?`).

3. **Fix 3 (ProgressBar)**: Accommodation cards with zero families assigned show NO progress bar. Assign a family and verify the bar appears.

4. **Feature 4 (feature chips)**: If any accommodations have features assigned → feature chips row appears below type chips → clicking a feature chip filters the grid to only accommodations with that feature → clicking again deactivates.

5. **Fix 5 (zone thumbnail)**: Accommodation cards with no own photo show no thumbnail. Cards with their own photo show it. The zone photo in the group header still shows.

6. **Enhancement 6 (family detail)**: Click a family → blue banner shows adult/child/baby counts; mascota indicator appears if hasPet; special needs text shown in amber box.

---

### Step 6 — Update technical documentation

1. **`ai-specs/specs/frontend-standards.mdc`**: No structural change needed — this ticket follows existing patterns.
2. **`ai-specs/specs/api-spec.yml`**: No change needed (handled by backend ticket).
3. Note in this plan that `accommodationTypeLookup` and `allFeatures` are new DTO fields; any future mock/test fixtures must include them.

---

## Implementation Order

1. Step 0 — Create branch
2. Step 1 — Update TypeScript types
3. Step 4 — TypeScript build check (catch broken fixtures early)
4. Step 2 — Fix `AccommodationSlotCard.vue` (ProgressBar + thumbnail fallback)
5. Step 3a — Fix `accommodationTypeMap` in panel
6. Step 3b — Add feature filter chips
7. Step 3c — Expand selected-family banner
8. Step 4 — Final TypeScript build check
9. Step 5 — Manual browser verification
10. Step 6 — Documentation

---

## Testing Checklist

- [ ] `npx vue-tsc --noEmit` reports zero errors
- [ ] ProgressBar hidden on empty accommodation slots; visible once a family is assigned
- [ ] Zone thumbnail does NOT appear inside slot cards that lack their own photo
- [ ] Slot cards with own photo still show their thumbnail
- [ ] Preference pills in `FamilyAssignmentCard` show type icon + label for all preferences (including those pointing to non-assignable accommodations)
- [ ] Feature chips row appears only when `availableFeatures.length > 0`
- [ ] Clicking a feature chip filters the accommodation grid; clicking again resets
- [ ] Feature and type filters reset when switching proposals
- [ ] Selected-family banner shows `X adultos / Y niños / Z bebés` (hidden when 0)
- [ ] Mascota tag in amber shown only when `hasPet = true`
- [ ] `specialNeeds` text shown in amber alert box (full text, not truncated)
- [ ] `campatesPreference` shown in italic when present

---

## Error Handling Patterns

- `AccommodationAssignmentPanel` already wraps `openZoneGallery` in try/catch; the gallery shows an empty-state message on API error — no change needed.
- New feature filter state (`activeFeatureFilter`) is local `ref` — no error path.
- `babyCount` / lookup fields coming from the API are non-nullable after the backend is updated; the frontend TypeScript types enforce this. If called against an older backend build, the panel will degrade gracefully (0 feature chips, no baby count display since `babyCount` would be `undefined` at runtime — add a `?? 0` fallback if needed during the transition period).

---

## UI/UX Considerations

- **Feature chip colours**: Use `indigo` for feature chips (vs `primary`/blue for type chips) to visually differentiate the two filter rows.
- **ProgressBar absence**: An empty slot now relies entirely on the `0 / N pers.` counter text. The counter is already present and readable — no additional indicator needed.
- **Family detail banner height**: The banner grows with content (special needs text can be long). The right panel is `overflow-y-auto` so the accommodation grid scrolls independently — no layout issue.
- **Zone thumbnail in group header**: Still present and unchanged. Cards no longer duplicate the zone photo.
- **Accessibility**: `v-tooltip.top` on the member count badge in `FamilyAssignmentCard` already provides screen-reader context. The new detail banner uses semantic elements — `<i>` icons have `aria-hidden` behaviour (they are decorative); text labels provide full context.

---

## Dependencies

No new npm packages required. All changes use:
- PrimeIcons (already installed): `pi-user`, `pi-star`, `pi-heart`, `pi-heart-fill`, `pi-tag`, `pi-exclamation-triangle`
- Tailwind CSS utilities (all existing classes)
- PrimeVue components already imported in the panel

---

## Notes

- **Backend dependency**: Fix 2 (preference icons), Feature 4 (feature chips), and Enhancement 6 (richer family detail) require the `feat-encaje-bolillos-ux-fixes-backend` branch to be merged first (adds `accommodationTypeLookup`, `allFeatures`, `babyCount` to the state response). Fixes 3 and 5 are independent.
- **English only**: All code, comments, and variable names must be in English. Spanish is used only for user-facing string literals in templates.
- **No `<style>` blocks**: All styling via Tailwind CSS utility classes only.
- **`<script setup lang="ts">`**: All components use this syntax. No Options API.
- **PrimeIcons verification**: `pi-user`, `pi-star`, `pi-heart`, `pi-heart-fill`, `pi-tag`, `pi-exclamation-triangle` are all confirmed present in PrimeIcons 7.x (used elsewhere in the codebase).

---

## Next Steps After Implementation

- Merge `feat-encaje-bolillos-ux-fixes-backend` PR first (or develop in parallel and merge backend before frontend).
- Open PR from `feature/feat-encaje-bolillos-ux-fixes-frontend` → `dev`.
- No database migration, no new environment variables, no deployment configuration changes.
