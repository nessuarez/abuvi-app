# Frontend Implementation Plan: feat-registration-detail-tab1-members — Show Participants & Extras in Tab 1

## Overview

This task enriches the first tab ("Datos de la Inscripción") of `RegistrationDetailPage.vue` with two improvements:

1. **Add read-only lists** of enrolled participants and selected extras so users no longer need to visit Tab 2 to see who is registered.
2. **Reorder sections** so participants & extras appear first, followed by additional info, accommodation preferences, and admin-only sections.

No new API calls, composables, Pinia stores, routes, or components are needed. All data is already loaded via `registration.pricing` at mount time. The change is entirely within the `<template>` block of a single view file, plus a small utility extraction.

Architecture: Vue 3 Composition API, `<script setup lang="ts">`, PrimeVue, Tailwind CSS.

---

## Architecture Context

**Files to modify:**
- `frontend/src/views/registrations/RegistrationDetailPage.vue` — template restructure + new script imports
- `frontend/src/utils/registration.ts` — extract `AGE_CATEGORY_LABELS`
- `frontend/src/components/registrations/RegistrationPricingBreakdown.vue` — switch to shared `AGE_CATEGORY_LABELS`

**Files read (no changes):**
- `frontend/src/types/registration.ts` — types for `PricingBreakdown`, `PricingMember`, `PricingExtra`

**State management:** Local component state only — no Pinia changes. All participant/extras data comes from `registration.value.pricing` (already loaded in `onMounted`).

**Routing:** No changes.

---

## Implementation Steps

### Step 0: Create Feature Branch

- **Action**: Create and switch to a new feature branch.
- **Branch name**: `feature/feat-registration-detail-tab1-members-frontend`
- **Steps**:
  1. Ensure you are on `dev`: `git checkout dev && git pull origin dev`
  2. Create branch: `git checkout -b feature/feat-registration-detail-tab1-members-frontend`
  3. Verify: `git branch`

---

### Step 1: Extract `AGE_CATEGORY_LABELS` to `@/utils/registration`

- **File**: `frontend/src/utils/registration.ts`
- **Action**: Add and export `AGE_CATEGORY_LABELS` map so it can be shared between `RegistrationPricingBreakdown.vue` and `RegistrationDetailPage.vue`.
- **Implementation Steps**:
  1. Import `AgeCategory` from `@/types/registration` (it should already be defined there).
  2. Add the exported constant at the top of the file alongside the other label maps:
     ```typescript
     export const AGE_CATEGORY_LABELS: Record<AgeCategory, string> = {
       Baby: 'Bebé',
       Child: 'Niño/Niña',
       Adult: 'Adulto/Adulta'
     }
     ```
- **Notes**: The `AgeCategory` type must be available in `@/types/registration`. If it is not exported from there yet, add the export.

---

### Step 2: Update `RegistrationPricingBreakdown.vue` to use the shared constant

- **File**: `frontend/src/components/registrations/RegistrationPricingBreakdown.vue`
- **Action**: Remove the locally-defined `AGE_CATEGORY_LABELS` and import the shared one from `@/utils/registration`.
- **Implementation Steps**:
  1. Remove the existing `const AGE_CATEGORY_LABELS = { ... }` declaration (lines 10-14 in the current file).
  2. Add the import at the top of `<script setup>`:
     ```typescript
     import { AGE_CATEGORY_LABELS, ATTENDANCE_PERIOD_LABELS } from '@/utils/registration'
     ```
  3. `ATTENDANCE_PERIOD_LABELS` is already exported from `@/utils/registration`, so it can be imported there too if it was previously imported separately.
- **Verification**: The component should render identically to before — no visual change.

---

### Step 3: Add read-only participant & extras lists and reorder Tab 1 sections

- **File**: `frontend/src/views/registrations/RegistrationDetailPage.vue`
- **Action**: Modify the `<script setup>` imports and restructure the `<TabPanel value="datos">` template.

#### 3a — Script changes

In `<script setup lang="ts">`, add the import of the newly shared constants:
```typescript
import { AGE_CATEGORY_LABELS, ATTENDANCE_PERIOD_LABELS } from '@/utils/registration'
```
`ATTENDANCE_PERIOD_LABELS` is already exported from `@/utils/registration`, so this just adds `AGE_CATEGORY_LABELS`.

#### 3b — Template restructure

Reorder the sections inside `<TabPanel value="datos">` as follows (new order):

1. **Notes** block (unchanged, stays at top — it's a contextual note, not user-editable)
2. **Participantes y extras** block (moved before "Información adicional")
3. **Información adicional** block (moved after participants)
4. **Preferencias de alojamiento** block (unchanged relative order)
5. **Admin: Accommodation needs & friend links** (unchanged)

Within the **Participantes y extras** block, add read-only lists that display when the edit forms are not open:

**Read-only participant list** — shown when `!isEditingMembers`:
```vue
<ul v-if="!isEditingMembers" class="mb-3 space-y-1.5 text-sm text-gray-800">
  <li
    v-for="m in registration.pricing.members"
    :key="m.familyMemberId"
    class="flex items-baseline gap-2"
    :data-testid="`member-row-${m.familyMemberId}`"
  >
    <span class="font-medium text-gray-900">{{ m.fullName }}</span>
    <span class="text-xs text-gray-500">
      {{ AGE_CATEGORY_LABELS[m.ageCategory] }}
      <template v-if="m.attendancePeriod && m.attendancePeriod !== 'Complete'">
        · {{ ATTENDANCE_PERIOD_LABELS[m.attendancePeriod] }}
      </template>
      <template v-if="m.guardianName">
        · Tutor/a: {{ m.guardianName }}
      </template>
    </span>
  </li>
</ul>
```

**Read-only extras list** — shown when `!isEditingExtras`, placed after the participant list:
```vue
<template v-if="!isEditingExtras">
  <div v-if="registration.pricing.extras.filter(e => e.quantity > 0).length > 0" class="mt-3">
    <h3 class="mb-1.5 text-xs font-semibold uppercase tracking-wide text-gray-500">Extras</h3>
    <ul class="space-y-0.5 text-sm text-gray-800">
      <li
        v-for="e in registration.pricing.extras.filter(x => x.quantity > 0)"
        :key="e.campEditionExtraId"
        :data-testid="`extra-row-${e.campEditionExtraId}`"
      >
        {{ e.name }}
        <span class="text-gray-500">× {{ e.quantity }}</span>
        <span v-if="e.userInput" class="ml-1 text-xs text-gray-400 italic">— {{ e.userInput }}</span>
      </li>
    </ul>
  </div>
  <p v-else class="mt-2 text-sm text-gray-400 italic">Sin extras seleccionados.</p>
</template>
```

**Important layout detail**: The read-only lists must appear **above** the edit buttons row (the `<div class="mb-3 flex items-center justify-between">` with the "Editar participantes" / "Editar extras" buttons). Place them between the section title and the buttons row.

Full updated structure for the "Participantes y extras" section header + read-only content:
```vue
<div class="mb-6">
  <div class="mb-3 flex items-center justify-between">
    <h2 class="text-base font-semibold text-gray-900">Participantes y extras</h2>
    <div v-if="canEdit || canUserEditExtras || canAdminEdit" class="flex gap-2">
      <!-- existing edit buttons — unchanged -->
    </div>
  </div>

  <!-- NEW: read-only participant list -->
  <ul v-if="!isEditingMembers" ...> ... </ul>

  <!-- NEW: read-only extras list -->
  <template v-if="!isEditingExtras"> ... </template>

  <!-- existing edit member form (v-if="isEditingMembers") — unchanged -->
  <!-- existing edit extras form (v-if="isEditingExtras") — unchanged -->
</div>
```

---

### Step 4: Verify TypeScript types

- **File**: `frontend/src/types/registration.ts`
- **Action**: Confirm `AgeCategory` is exported. If not, add `export` to its declaration.
- Check that `PricingMember` has `ageCategory: AgeCategory` and `attendancePeriod: AttendancePeriod` fields (they are used in the new template expressions).

---

### Step 5: Write Vitest unit tests

- **File**: `frontend/src/utils/__tests__/registration.test.ts` (create if not exists, add to existing if it does)
- **Action**: Add tests for the newly exported `AGE_CATEGORY_LABELS`.
- **Test cases**:
  - `AGE_CATEGORY_LABELS['Baby']` returns `'Bebé'`
  - `AGE_CATEGORY_LABELS['Child']` returns `'Niño/Niña'`
  - `AGE_CATEGORY_LABELS['Adult']` returns `'Adulto/Adulta'`
- **Notes**: These are trivial but verify that the export was not accidentally broken when moving the constant.

---

### Step 6: Update Technical Documentation

- **Action**: Review and update relevant docs after implementation.
- **Steps**:
  1. No new API endpoints or routing changes — `api-endpoints.md` unchanged.
  2. No new PrimeVue component patterns introduced — `frontend-standards.mdc` unchanged.
  3. If `AGE_CATEGORY_LABELS` is referenced in any existing doc, update the reference path.
  4. Update the enriched spec file at `ai-specs/changes/feat-registration-detail-tab1-members/enriched.md` with an "Implemented" note if required by team process.
- **Notes**: This step is MANDATORY before considering implementation complete.

---

## Implementation Order

1. Step 0 — Create feature branch
2. Step 1 — Extract `AGE_CATEGORY_LABELS` to `@/utils/registration`
3. Step 2 — Update `RegistrationPricingBreakdown.vue` to import from shared util
4. Step 3 — Add read-only lists + reorder sections in `RegistrationDetailPage.vue`
5. Step 4 — Verify TypeScript types
6. Step 5 — Write Vitest unit tests
7. Step 6 — Update technical documentation

---

## Testing Checklist

- [ ] `RegistrationPricingBreakdown.vue` renders identically to before (visual regression check in browser)
- [ ] Tab 1 shows enrolled participants list with name, age category, and period (when not `'Complete'`)
- [ ] Tab 1 shows guardian name when `guardianName` is present
- [ ] Tab 1 shows extras list when there are extras with `quantity > 0`
- [ ] Tab 1 shows "Sin extras seleccionados." placeholder when no extras
- [ ] Read-only participant list hides when `isEditingMembers === true`
- [ ] Read-only extras list hides when `isEditingExtras === true`
- [ ] Section order is: Notas → Participantes y extras → Información adicional → Preferencias → Admin sections
- [ ] Tab 2 (Desglose del precio) is visually unchanged
- [ ] `AGE_CATEGORY_LABELS` Vitest tests pass
- [ ] No TypeScript errors (`npx vue-tsc --noEmit`)
- [ ] No ESLint errors (`npm run lint`)

---

## Error Handling Patterns

No new async operations are introduced. All data comes from `registration.value.pricing`, which is loaded at mount time via the existing `getRegistrationById` call. If `registration.value` is null, the entire `<template v-else-if="registration">` block is hidden — the new lists are implicitly protected by this guard.

---

## UI/UX Considerations

- **Participant list**: Each participant on its own row, name in medium weight, metadata (age category, period, guardian) in smaller gray text — consistent with the existing `dl` read-views in the page.
- **Extras list**: Compact, one line per extra. `name × quantity` pattern is scannable. `userInput` in italic gray.
- **Empty extras**: Italic gray placeholder text ("Sin extras seleccionados.") matches the style of other empty-state messages in the page.
- **Edit form visibility**: Read-only lists are hidden when the edit form is active (`v-if="!isEditingMembers"` / `v-if="!isEditingExtras"`) to avoid double display.
- **Responsive**: `<ul>` and `<li>` elements flow naturally on all screen sizes — no grid layout needed.
- **Accessibility**: `<ul>` / `<li>` semantic markup; `data-testid` attributes on each row for reliable selection.

---

## Dependencies

No new npm packages. All imports come from existing project dependencies:
- `@/utils/registration` — shared label maps
- `@/types/registration` — `AgeCategory`, `AttendancePeriod`

---

## Notes

- **No backend changes** — this is a pure frontend template task.
- **No new API calls** — `registration.pricing.members` and `registration.pricing.extras` are already in the loaded `RegistrationResponse`.
- **English code, Spanish UI** — all new variable names in English; all new display text in Spanish (per `frontend-standards.mdc`).
- **TypeScript strict** — no `any`, all template expressions are type-safe via `registration.pricing.members` and `registration.pricing.extras` which are already typed.
- The `registration.pricing.extras` array contains ALL extras (including quantity 0). Filter with `.filter(e => e.quantity > 0)` for display — same logic used in `RegistrationPricingBreakdown.vue`'s `paidExtras` computed.
- The `isEditingMembers` and `isEditingExtras` flags are independent — hiding the member list when `isEditingExtras` is true (or vice versa) is NOT correct. Each list is hidden only when its own edit form is open.

---

## Next Steps After Implementation

- Run the development server and visually verify Tab 1 on a real registration with multiple members and extras.
- Verify on a registration with no extras that the placeholder renders.
- Verify on a registration where editing members is active — confirm the read-only list disappears.
- Submit PR targeting `dev` branch.
