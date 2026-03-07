# Bug Fix: Custom Age Ranges Not Saving / Not Working

## Problem Summary

Custom age ranges for camp editions are not functioning correctly. When a user enables custom age ranges and fills in the values, they are either not sent to the backend or not displayed back after saving. Three distinct bugs have been identified.

---

## Root Cause Analysis

### Bug 1: Frontend `CampEdition` type uses wrong field names (CRITICAL)

**File:** `frontend/src/types/camp-edition.ts` (lines 18-21)

The `CampEdition` interface declares age range fields WITHOUT the `custom` prefix:

```typescript
babyMaxAge?: number
childMinAge?: number
childMaxAge?: number
adultMinAge?: number
```

But the backend `CampEditionResponse` DTO (and all other response DTOs) sends them WITH the `custom` prefix:

```csharp
int? CustomBabyMaxAge,   // serialized as "customBabyMaxAge" in JSON
int? CustomChildMinAge,
int? CustomChildMaxAge,
int? CustomAdultMinAge,
```

**Impact:** When the API response arrives, the values land on `customBabyMaxAge` in the raw JSON, but since the TypeScript interface only declares `babyMaxAge`, the values are silently ignored. The `CampEditionUpdateDialog.vue` reads `props.edition.babyMaxAge` (line 96) which is always `undefined`, so the form initializes with null values even when the database has correct data.

### Bug 2: `CampEditionProposeDialog.vue` is missing custom age range fields entirely

**File:** `frontend/src/components/camps/CampEditionProposeDialog.vue`

The propose dialog has NO UI for custom age ranges:

- No `useCustomAgeRanges` toggle
- No input fields for `customBabyMaxAge`, `customChildMinAge`, `customChildMaxAge`, `customAdultMinAge`
- The `handleSubmit` function does not send any age range fields to the API

**Impact:** It is impossible to set custom age ranges when proposing a new camp edition.

### Bug 3: `CreateCampEditionRequest` frontend type uses wrong field names

**File:** `frontend/src/types/camp-edition.ts` (lines 53-72)

Same naming mismatch as Bug 1 — uses `babyMaxAge` instead of `customBabyMaxAge`. Even if the propose dialog were to send these values, the backend would ignore them because the JSON property names don't match.

### Bug 4: `CampEditionDetails.vue` reads wrong field names for age ranges

**File:** `frontend/src/components/camps/CampEditionDetails.vue` (lines 28-33)

The computed `ageRanges` reads `props.campEdition.babyMaxAge` (wrong name, should be `customBabyMaxAge`). These are always `undefined` due to the type mismatch from Bug 1, so it always falls back to hardcoded defaults `{ babyMaxAge: 3, childMinAge: 4, childMaxAge: 14, adultMinAge: 15 }`, ignoring edition-specific custom ranges. It also ignores the `useCustomAgeRanges` flag entirely.

### Bug 5: `CampPage.vue` hardcodes global age range defaults instead of fetching from API

**File:** `frontend/src/views/CampPage.vue` (lines 37-48)

When `useCustomAgeRanges` is `false`, the computed falls back to hardcoded values `{ babyMaxAge: 3, childMinAge: 4, childMaxAge: 14, adultMinAge: 15 }` instead of fetching the actual global defaults from `GET /api/settings/age-ranges`. If an admin changes the global defaults via the API, these hardcoded values will be stale.

### Bug 6: No admin UI exists for managing global default age ranges

The composable `useAssociationSettings.ts` exists with `fetchAgeRanges()` and `updateAgeRanges()`, and the backend endpoints `GET/PUT /api/settings/age-ranges` are fully implemented. However, **no view or component** in the frontend uses this composable. There is no settings page or section in the admin panel where a board member can view or update the global default age ranges.

---

## Required Changes

### 1. Fix `CampEdition` interface field names

**File:** `frontend/src/types/camp-edition.ts`

Rename fields in `CampEdition` interface (lines 18-21):

```typescript
// FROM:
babyMaxAge?: number
childMinAge?: number
childMaxAge?: number
adultMinAge?: number

// TO:
customBabyMaxAge?: number
customChildMinAge?: number
customChildMaxAge?: number
customAdultMinAge?: number
```

### 2. Fix `CreateCampEditionRequest` interface field names

**File:** `frontend/src/types/camp-edition.ts`

Rename fields in `CreateCampEditionRequest` interface (lines 64-67):

```typescript
// FROM:
babyMaxAge?: number
childMinAge?: number
childMaxAge?: number
adultMinAge?: number

// TO:
customBabyMaxAge?: number
customChildMinAge?: number
customChildMaxAge?: number
customAdultMinAge?: number
```

### 3. Fix `CampEditionUpdateDialog.vue` form initialization

**File:** `frontend/src/components/camps/CampEditionUpdateDialog.vue`

Update `initializeForm` (lines 96-99) to use the correct field names:

```typescript
// FROM:
form.customBabyMaxAge = props.edition.babyMaxAge ?? null
form.customChildMinAge = props.edition.childMinAge ?? null
form.customChildMaxAge = props.edition.childMaxAge ?? null
form.customAdultMinAge = props.edition.adultMinAge ?? null

// TO:
form.customBabyMaxAge = props.edition.customBabyMaxAge ?? null
form.customChildMinAge = props.edition.customChildMinAge ?? null
form.customChildMaxAge = props.edition.customChildMaxAge ?? null
form.customAdultMinAge = props.edition.customAdultMinAge ?? null
```

### 4. Add custom age range UI to `CampEditionProposeDialog.vue`

**File:** `frontend/src/components/camps/CampEditionProposeDialog.vue`

- Add `useCustomAgeRanges`, `customBabyMaxAge`, `customChildMinAge`, `customChildMaxAge`, `customAdultMinAge` fields to the form model
- Add a `ToggleSwitch` and four `InputNumber` fields (matching the pattern from `CampEditionUpdateDialog.vue` lines 488-540)
- Add validation for custom age ranges (matching the pattern from `CampEditionUpdateDialog.vue` lines 167-178)
- Include the custom age range fields in `handleSubmit` payload

### 5. Fix `CampEditionDetails.vue` age range computed

**File:** `frontend/src/components/camps/CampEditionDetails.vue`

Update the `ageRanges` computed (lines 28-33) to:

- Use the correct `customBabyMaxAge` field names
- Respect the `useCustomAgeRanges` flag (only use custom values when enabled)
- Fall back to global defaults when custom ranges are disabled

```typescript
// FROM:
const ageRanges = computed<AgeRangeSettings>(() => ({
  babyMaxAge: props.campEdition.babyMaxAge ?? 3,
  childMinAge: props.campEdition.childMinAge ?? 4,
  childMaxAge: props.campEdition.childMaxAge ?? 14,
  adultMinAge: props.campEdition.adultMinAge ?? 15
}))

// TO:
const ageRanges = computed<AgeRangeSettings>(() => {
  if (props.campEdition.useCustomAgeRanges) {
    return {
      babyMaxAge: props.campEdition.customBabyMaxAge ?? 3,
      childMinAge: props.campEdition.customChildMinAge ?? 4,
      childMaxAge: props.campEdition.customChildMaxAge ?? 14,
      adultMinAge: props.campEdition.customAdultMinAge ?? 15
    }
  }
  // Fallback: use global defaults (fetched from API or hardcoded)
  return globalAgeRanges.value
})
```

### 6. Replace hardcoded fallback defaults with API-fetched global ranges

**Files:** `frontend/src/views/CampPage.vue`, `frontend/src/components/camps/CampEditionDetails.vue`

Both files hardcode `{ babyMaxAge: 3, childMinAge: 4, childMaxAge: 14, adultMinAge: 15 }` as fallback. Instead, use the `useAssociationSettings` composable to fetch the actual global defaults from `GET /api/settings/age-ranges` and use those as fallback values.

### 7. Create admin UI for global default age ranges

Create a settings section (either a new page or a section within the existing admin/board panel) that allows board members to:

- View the current global default age ranges
- Update them via `PUT /api/settings/age-ranges`
- See who last updated them and when (`updatedBy`, `updatedAt` from the API response)

The composable `useAssociationSettings.ts` is already implemented and ready to use. The backend endpoints (`GET/PUT /api/settings/age-ranges`) are also fully functional with validation.

**Suggested location:** Add a "Configuracion" or "Ajustes" section in the admin area, or add an "Age Ranges" card/panel within the existing camp management views. The UI should include:

- Four `InputNumber` fields: Baby max age, Child min age, Child max age, Adult min age
- Save button calling `updateAgeRanges()`
- Display of last update info (who + when)

---

## Files to Modify

| File | Change |
|------|--------|
| `frontend/src/types/camp-edition.ts` | Fix field names in `CampEdition` and `CreateCampEditionRequest` |
| `frontend/src/components/camps/CampEditionUpdateDialog.vue` | Fix `initializeForm` field references |
| `frontend/src/components/camps/CampEditionProposeDialog.vue` | Add custom age range toggle + fields + validation |
| `frontend/src/components/camps/CampEditionDetails.vue` | Fix field names + respect `useCustomAgeRanges` flag |
| `frontend/src/views/CampPage.vue` | Replace hardcoded defaults with API-fetched global ranges |
| New component or section in admin panel | Create UI for managing global default age ranges |

## Verification Steps

1. **Update dialog round-trip**: Edit an existing edition, enable custom age ranges, set values (e.g., Baby max: 2, Child min: 3, Child max: 12, Adult min: 13), save. Reopen the dialog and verify the values are preserved.
2. **Propose dialog**: Create a new edition proposal with custom age ranges enabled. Verify the values are saved and visible in the edition detail view.
3. **Disable toggle**: Edit an edition with custom age ranges, disable the toggle, save. Verify `useCustomAgeRanges` is `false` and custom fields are null in the API response.
4. **Backend validation**: Attempt to save custom age ranges with invalid ordering (e.g., BabyMax > ChildMin). Verify the backend returns a validation error.
5. **Global defaults admin UI**: Navigate to the settings section, view current defaults, change them, save. Verify the new values are reflected when viewing editions that don't use custom ranges.
6. **CampEditionDetails display**: View an edition with custom ranges enabled — verify it shows the custom values, not the global defaults. View an edition without custom ranges — verify it shows the global defaults from the API, not hardcoded values.

## Non-Functional Requirements

- No backend changes needed — the backend DTOs, services, and endpoints are all correct
- Frontend-only fixes: field naming mismatch, missing UI, hardcoded fallback defaults
- The global age ranges admin UI should be restricted to Board+ roles (matching the backend authorization)
