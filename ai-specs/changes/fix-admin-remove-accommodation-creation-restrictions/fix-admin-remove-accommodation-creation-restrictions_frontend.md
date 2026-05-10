# Frontend Implementation Plan: fix-admin-remove-accommodation-creation-restrictions

## Overview

Remove the `Closed` status restriction from the "Añadir extra" button in `CampEditionExtrasList.vue`. Room assignments happen after registrations close, so a `Closed` edition must allow extras to be added.

The scope is minimal: a single computed property in one component. The accommodation and zone panels (`CampEditionAccommodationsPanel`, `AccommodationZonePanel`) already show their "Añadir" / "Nueva zona" buttons unconditionally (they rely on the Board-only tab guard in the parent, not a status check), so no changes are needed there.

**Frontend architecture**: Vue 3 Composition API, `<script setup lang="ts">`, PrimeVue, Tailwind CSS.

---

## Architecture Context

**Component to modify:**

| File | Change |
|---|---|
| `frontend/src/components/camps/CampEditionExtrasList.vue` | Remove `Closed` from `canAdd` computed |

**Components verified as already correct (no change needed):**

| File | Reason |
|---|---|
| `frontend/src/components/camps/CampEditionAccommodationsPanel.vue` | "Añadir" button (line 120) is unconditional; no status prop/check exists |
| `frontend/src/components/camps/AccommodationZonePanel.vue` | "Nueva zona" (line 242) and "Añadir unidad" (line 339) buttons are unconditional; no status prop/check exists |
| `frontend/src/views/camps/CampEditionDetailPage.vue` — `canEdit` | Controls the general edition "Editar" button (dates, prices, notes). Keeping `Closed` blocked there is **correct and intentional**. No change. |

**State management**: No Pinia stores involved. The change is purely in a local `computed` within the component.

**Routing**: No routing changes.

---

## Implementation Steps

### Step 0: Create Feature Branch

- **Action**: Create and switch to a new feature branch
- **Branch name**: `feature/fix-admin-remove-accommodation-creation-restrictions-frontend`
- **Steps**:
  1. `git checkout dev && git pull origin dev`
  2. `git checkout -b feature/fix-admin-remove-accommodation-creation-restrictions-frontend`
  3. `git branch` — verify you are on the new branch
- **Note**: This must be the FIRST step before any code change.

---

### Step 1: Fix `canAdd` in `CampEditionExtrasList.vue`

- **File**: `frontend/src/components/camps/CampEditionExtrasList.vue`
- **Lines**: 36–38
- **Action**: Remove `props.editionStatus !== 'Closed'` from the `canAdd` computed so that Board/Admin users can add extras to a `Closed` edition.

**Before:**
```typescript
const canAdd = computed(
  () => canManage.value && props.editionStatus !== 'Completed' && props.editionStatus !== 'Closed'
)
```

**After:**
```typescript
const canAdd = computed(
  () => canManage.value && props.editionStatus !== 'Completed'
)
```

- **Implementation Notes**:
  - `canManage` (line 35) already guards this to Board/Admin only — no role logic changes needed.
  - The `'Completed'` guard stays — a finished edition must remain immutable.
  - This is the only change in this file.

---

### Step 2: Manual Verification

Start the dev server and verify the golden paths:

```bash
cd frontend && npm run dev
```

1. Log in as a Board or Admin user.
2. Navigate to a camp edition with status **Closed** → "Extras" tab.
   - **Expected**: The "Añadir extra" button is visible and opens the form dialog.
   - Previously: Button was hidden.
3. Navigate to a camp edition with status **Completed** → "Extras" tab.
   - **Expected**: The "Añadir extra" button is **not** visible. Guard unchanged.
4. Navigate to a camp edition with status **Closed** → "Alojamientos" tab.
   - **Expected**: "Añadir" (accommodation) and "Nueva zona" buttons are visible — they already were, confirm no regression.
5. Navigate as a non-Board user.
   - **Expected**: No "Añadir extra" button in any status.

---

### Step 3: Update Technical Documentation

- **Action**: Review changed files and update documentation if any patterns were altered.
- **Steps**:
  1. No new components, types, composables, or routes were added.
  2. No changes to `frontend-standards.mdc` are needed.
  3. No changes to `api-spec.yml` needed (frontend-only change).
  4. Confirm no documentation file references the `Closed` restriction on extras creation.
- **Notes**: Documentation update step is MANDATORY before closing this ticket. Since the change is a one-line business-rule removal, no documentation file needs updating — record this as the finding.

---

## Implementation Order

1. Step 0 — Create feature branch
2. Step 1 — Fix `canAdd` in `CampEditionExtrasList.vue`
3. Step 2 — Manual verification in the browser
4. Step 3 — Documentation review (confirm no update needed)

---

## Testing Checklist

- [ ] "Añadir extra" button visible on `Closed` edition for Board/Admin
- [ ] "Añadir extra" button hidden on `Completed` edition for Board/Admin
- [ ] "Añadir extra" button hidden for non-Board users regardless of status
- [ ] "Añadir alojamiento" button unaffected (still visible on `Closed` as before)
- [ ] "Nueva zona" button unaffected
- [ ] General edition "Editar" button still hidden for `Closed` editions (no regression)
- [ ] TypeScript compiles with no errors: `npm run type-check`
- [ ] ESLint passes: `npm run lint`

---

## Error Handling Patterns

No new error paths introduced. The existing flow when the API call succeeds or fails is unchanged:
- Success: `toast.add({ severity: 'success', ... })`
- Failure: `toast.add({ severity: 'error', detail: error.value, ... })`

---

## UI/UX Considerations

- No visual changes beyond unhiding the "Añadir extra" button for `Closed` editions.
- No new PrimeVue components or Tailwind classes needed.
- The button appearance, position, and dialog behavior are identical to what already works for `Open` / `Draft` editions.

---

## Dependencies

- No new npm packages.
- No new PrimeVue components.

---

## Notes

- `CampEditionStatus` type (in `frontend/src/types/camp-edition.ts`) already includes `'Closed'` and `'Completed'` — no type changes needed.
- The enriched spec's suggestion to add a `canManageAccommodations` prop to the accommodation panels was investigated and found **unnecessary**: both panels already show their add buttons without status restrictions. The only real frontend gap was `CampEditionExtrasList.canAdd`.
- The general edition edit (`canEdit`) must remain blocked for `Closed` editions — changing that is explicitly out of scope per the enriched spec.

---

## Next Steps After Implementation

1. Open a PR targeting `dev`.
2. Reference the enriched spec: `ai-specs/changes/fix-admin-remove-accommodation-creation-restrictions/fix-admin-remove-accommodation-creation-restrictions_enriched.md`
3. The backend fix is a separate PR on branch `feature/fix-admin-remove-accommodation-creation-restrictions-backend`.
4. Both PRs must merge before the feature is complete end-to-end.

---

## Implementation Verification

- [ ] **Code Quality**: `npm run type-check` passes; `npm run lint` passes; no `any` types introduced
- [ ] **Functionality**: "Añadir extra" button visible on `Closed` editions, hidden on `Completed`
- [ ] **No regressions**: Accommodation and zone add buttons unchanged; general edition "Editar" button still locked for `Closed`
- [ ] **Single file changed**: Only `frontend/src/components/camps/CampEditionExtrasList.vue`
- [ ] **Documentation**: Confirmed no documentation files reference this restriction
