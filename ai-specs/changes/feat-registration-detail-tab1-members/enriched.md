# Enrich Tab 1: Show Participants & Extras + Reorder Sections

## Problem

In `RegistrationDetailPage.vue`, the first tab ("Datos de la Inscripción") shows a section titled **"Participantes y extras"** but it only contains the **edit buttons** — no actual list of participants or extras is displayed. The participant/extras data is only shown in the "Desglose del precio" tab (Tab 2), mixed with pricing info.

This means a user visiting the first tab cannot see who is registered or which extras are included without switching to the pricing tab.

---

## Goal

- Show a read-only list of participants and extras **in Tab 1**, independent from the pricing breakdown.
- Keep the pricing breakdown (Tab 2) unchanged — it already shows pricing-focused data correctly.
- **Reorder** sections in Tab 1 so that participants & extras come **first**, and notes/preferences/accommodation info come **below**.

---

## Proposed New Tab 1 Section Order

1. **Participantes y extras** (moved to top)
   - Read-only list of participants (name, age category, attendance period)
   - Read-only list of extras with quantities (if any)
   - Edit buttons (existing behavior unchanged)
2. **Información adicional** (notes, campatesPreference, hasPet) — moved below
3. **Preferencias de alojamiento** — moved below
4. **Accommodation needs & friend links** (admin only) — remains at bottom

---

## Data Available

All data comes from `registration.pricing` (already loaded on mount):
- `registration.pricing.members[]` — `fullName`, `ageCategory`, `attendancePeriod`, `visitStartDate`, `visitEndDate`, `guardianName`, `guardianDocumentNumber`
- `registration.pricing.extras[]` — `name`, `quantity`, `userInput`

---

## Implementation

### Files to modify

**`frontend/src/views/registrations/RegistrationDetailPage.vue`**

Only the `<template>` section needs changing — no new composables, no API calls, no new component needed.

#### Changes

1. **Move** the "Participantes y extras" `<div class="mb-6">` block to the **top** of `<TabPanel value="datos">` (before "Información adicional").

2. **Add a read-only participant list** inside the "Participantes y extras" block, shown when `!isEditingMembers`. Display:
   - Each member: `fullName` + `ageCategory` label (use the existing `AGE_CATEGORY_LABELS` map from `RegistrationPricingBreakdown` — duplicate the map locally in the page or extract it to `@/utils/registration`). Show attendance period if not `'Complete'`, and guardian name if present.
   - Use a simple `<ul>` list or `<dl>` definition list styled consistently with the rest of the page (similar to the "Información adicional" read view).

3. **Add a read-only extras list** inside the same block, shown when `!isEditingExtras`. Display:
   - Each extra with `quantity > 0`: `name` × `quantity`, plus `userInput` if present.
   - If no extras: show "Sin extras" placeholder (italic, gray).

4. **Move** "Información adicional" block below the participants block.

5. **Keep** "Preferencias de alojamiento" and admin accommodation/friend-links sections in the same relative order (they just shift down due to the reorder).

#### Read-only participants render example

```vue
<!-- Read-only participant list (shown when !isEditingMembers) -->
<ul v-if="!isEditingMembers" class="space-y-1 text-sm text-gray-800">
  <li v-for="m in registration.pricing.members" :key="m.familyMemberId" class="flex items-baseline gap-2">
    <span class="font-medium">{{ m.fullName }}</span>
    <span class="text-xs text-gray-500">
      {{ AGE_CATEGORY_LABELS[m.ageCategory] }}
      <template v-if="m.attendancePeriod && m.attendancePeriod !== 'Complete'">
        · {{ ATTENDANCE_PERIOD_LABELS[m.attendancePeriod] }}
      </template>
      <template v-if="m.guardianName">· Tutor/a: {{ m.guardianName }}</template>
    </span>
  </li>
</ul>
```

#### Read-only extras render example

```vue
<!-- Read-only extras list (shown when !isEditingExtras) -->
<template v-if="registration.pricing.extras.length > 0">
  <ul class="mt-2 space-y-0.5 text-sm text-gray-800">
    <li v-for="e in registration.pricing.extras.filter(x => x.quantity > 0)" :key="e.campEditionExtraId">
      {{ e.name }} × {{ e.quantity }}
      <span v-if="e.userInput" class="text-xs text-gray-500 italic">— {{ e.userInput }}</span>
    </li>
  </ul>
</template>
<p v-else class="mt-2 text-sm text-gray-400 italic">Sin extras</p>
```

---

## Constants to reuse / extract

`AGE_CATEGORY_LABELS` and `ATTENDANCE_PERIOD_LABELS` are currently defined inside `RegistrationPricingBreakdown.vue` and `@/utils/registration` respectively.

- `ATTENDANCE_PERIOD_LABELS` is already exported from `@/utils/registration` — import it in the page.
- `AGE_CATEGORY_LABELS` is only defined locally in `RegistrationPricingBreakdown.vue`. **Extract it to `@/utils/registration`** and import it in both `RegistrationPricingBreakdown.vue` and `RegistrationDetailPage.vue`.

---

## Acceptance Criteria

- [ ] Tab 1 shows a read-only list of enrolled participants (name, age category, period) **above** the edit buttons.
- [ ] Tab 1 shows a read-only list of extras (name × quantity, userInput) or "Sin extras" placeholder.
- [ ] The read-only lists are hidden when the edit form is open (i.e., when `isEditingMembers` / `isEditingExtras` is true).
- [ ] Section order in Tab 1: Participantes y extras → Información adicional → Preferencias de alojamiento → Admin sections.
- [ ] Tab 2 ("Desglose del precio") is unchanged.
- [ ] No new API calls; all data is sourced from `registration.pricing` (already loaded).
- [ ] `AGE_CATEGORY_LABELS` is extracted to `@/utils/registration` and shared.

---

## Out of Scope

- No changes to Tab 2, Tab 3, or Tab 4.
- No changes to edit flows (member/extras edit forms remain as-is).
- No backend changes.
