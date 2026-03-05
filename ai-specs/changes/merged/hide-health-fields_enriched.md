# Hide Health Fields from Frontend

## User Story

**As a** product owner,
**I want** all health-related fields (medical notes, allergies) hidden from the web frontend,
**so that** sensitive health data is not displayed or editable through the web application.

## Context

The application currently displays and allows editing of health fields (`medicalNotes`, `allergies`, `hasMedicalNotes`, `hasAllergies`) in multiple locations. These fields must be completely hidden from the UI — forms should not show them, tables should not display them, and warning icons should not reference them. The backend API contracts and types remain unchanged; this is a frontend-only visibility change.

## Scope

**In scope:** Remove all health field UI rendering from the frontend.
**Out of scope:** Backend API changes, database changes, type/interface changes (keep types intact for API compatibility).

## Fields to Hide

| Field | Type | Description |
|-------|------|-------------|
| `medicalNotes` | `string \| null` | Free-text medical notes (encrypted at rest) |
| `allergies` | `string \| null` | Free-text allergies info (encrypted at rest) |
| `hasMedicalNotes` | `boolean` | Indicator flag for medical notes presence |
| `hasAllergies` | `boolean` | Indicator flag for allergies presence |

## Files to Modify

### 1. Family Member List — Health Column

**File:** `frontend/src/components/family-units/FamilyMemberList.vue`
**Lines:** ~121-141
**Action:** Remove the entire `<Column header="Salud">` block (the health status column showing "Notas médicas" / "Alergias" tags).

### 2. Family Member Form — Medical Notes & Allergies Fields

**File:** `frontend/src/components/family-units/FamilyMemberForm.vue`
**Lines:** ~328-362
**Action:**

- Remove the "Notas Médicas" textarea section (~lines 328-344)
- Remove the "Alergias" textarea section (~lines 346-362)
- Remove associated refs and computed properties for `medicalNotes`, `allergies`, `hasMedicalNotesInfo`, `hasAllergiesInfo` (~lines 44-45, 59-60)
- Remove `medicalNotes` and `allergies` from the form submission payload (~lines 198-199)

### 3. Guest Form — Medical Notes & Allergies Fields

**File:** `frontend/src/components/guests/GuestForm.vue`
**Lines:** ~284-332
**Action:**

- Remove the "Notas Médicas" textarea section (~lines 284-307)
- Remove the "Alergias" textarea section (~lines 309-332)
- Remove associated refs and computed properties for `medicalNotes`, `allergies`, `hasMedicalNotesInfo`, `hasAllergiesInfo` (~lines 31-32, 45-46)
- Remove `medicalNotes` and `allergies` from the form submission payload (~lines 174-175)

### 4. Registration Member Selector — Health Warning Icons

**File:** `frontend/src/components/registrations/RegistrationMemberSelector.vue`
**Lines:** ~176-183
**Action:** Remove the health warning icons that display "Tiene notas médicas" and "Tiene alergias" indicators.

### 5. Tests to Update

| Test File | Action |
|-----------|--------|
| `frontend/src/components/registrations/__tests__/RegistrationMemberSelector.test.ts` | Remove tests for health warning icons (lines ~149-163). Keep mock data fields for type compatibility. |
| `frontend/src/components/family-units/__tests__/FamilyMemberList.spec.ts` | Remove tests for health column if any. Keep mock data fields. |
| `frontend/src/components/memberships/__tests__/BulkMembershipDialog.spec.ts` | No UI changes needed — mock data fields stay for type compatibility. |
| `frontend/src/composables/__tests__/useFamilyUnits.spec.ts` | No changes needed — composable layer untouched. |
| `frontend/src/views/__tests__/FamilyUnitPage.spec.ts` | No changes needed unless it tests health column rendering. |

## Types — No Changes

Keep all TypeScript interfaces (`GuestResponse`, `CreateGuestRequest`, `FamilyMemberResponse`, `CreateFamilyMemberRequest`, `UpdateFamilyMemberRequest`) intact. The API still sends/receives these fields; the frontend simply won't render or send them.

## Implementation Steps

1. **FamilyMemberList.vue** — Remove the `<Column header="Salud">` block
2. **FamilyMemberForm.vue** — Remove health form fields, refs, computed properties, and form payload entries
3. **GuestForm.vue** — Remove health form fields, refs, computed properties, and form payload entries
4. **RegistrationMemberSelector.vue** — Remove health warning icons
5. **Update tests** — Remove assertions about health UI elements; keep mock data fields for type compatibility
6. **Run tests** — `npx vitest --run` to verify nothing breaks
7. **Manual verification** — Check Family Unit page, Guest form, and Camp Registration flow in browser

## Acceptance Criteria

- [ ] No "Salud" column visible in the Family Members table
- [ ] No "Notas Médicas" or "Alergias" form fields in Family Member form
- [ ] No "Notas Médicas" or "Alergias" form fields in Guest form
- [ ] No health warning icons in Registration Member Selector
- [ ] All existing tests pass (updated as needed)
- [ ] TypeScript types remain unchanged (API compatibility preserved)
- [ ] No health-related labels, icons, or data visible anywhere in the frontend

## Non-Functional Requirements

- **Security:** Health data is no longer exposed in the UI, reducing the attack surface for sensitive data
- **Performance:** No impact — removing UI elements only
- **Backwards Compatibility:** Backend API contracts unchanged; frontend simply stops rendering and sending these fields
