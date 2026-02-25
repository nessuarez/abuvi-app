# Frontend Implementation Plan: tech-debt-primevue-deprecated-components — Migrate PrimeVue v4 Deprecated Components

## 1. Overview

This task eliminates all PrimeVue v4 deprecation warnings by renaming 3 deprecated component types across 5 files. All renames are API-compatible (same props/events/v-model bindings). No new types, composables, stores, or routing changes are required. Risk is very low.

Architecture principles in scope: Vue 3 Composition API with `<script setup lang="ts">`, PrimeVue component imports.

---

## 2. Architecture Context

- **Components involved**: `FamilyMemberForm.vue`, `GuestForm.vue`, `PayFeeDialog.vue`, `MembershipDialog.vue`, `UserForm.vue`
- **Files referenced**:
  - `frontend/src/components/family-units/FamilyMemberForm.vue`
  - `frontend/src/components/guests/GuestForm.vue`
  - `frontend/src/components/memberships/PayFeeDialog.vue`
  - `frontend/src/components/memberships/MembershipDialog.vue`
  - `frontend/src/components/users/UserForm.vue`
- **Routing**: No changes
- **State management**: No changes
- **No new npm packages**: All replacement components (`primevue/datepicker`, `primevue/select`, `primevue/toggleswitch`) are already part of PrimeVue `^4.5.4`

---

## 3. Implementation Steps

### Step 0: Create Feature Branch

- **Action**: Create and switch to a new feature branch
- **Branch Name**: `feature/tech-debt-primevue-deprecated-components-frontend`
  > Important: Do NOT continue on `feature/adding-new-specs` (the general spec branch). Create a dedicated frontend branch.
- **Implementation Steps**:
  1. `git checkout main`
  2. `git pull origin main`
  3. `git checkout -b feature/tech-debt-primevue-deprecated-components-frontend`
  4. `git branch` — confirm branch is active

---

### Step 1: Migrate `FamilyMemberForm.vue` — Calendar + Dropdown

- **File**: `frontend/src/components/family-units/FamilyMemberForm.vue`
- **Action**: Replace two deprecated imports and their template usages

**Script block changes (lines 5–6):**

| Current | Replace With |
|---|---|
| `import Calendar from 'primevue/calendar'` | `import DatePicker from 'primevue/datepicker'` |
| `import Dropdown from 'primevue/dropdown'` | `import Select from 'primevue/select'` |

**Template changes:**

| Location | Current | Replace With |
|---|---|---|
| Line 249 | `<Calendar` | `<DatePicker` |
| Line 260 | `</Calendar>` | `</DatePicker>` |
| Line 268 | `<Dropdown` | `<Select` |
| Line 280 | `</Dropdown>` | `</Select>` |

- **Implementation Notes**:
  - All props (`dateFormat`, `maxDate`, `invalid`, `disabled`, `showIcon`, `@blur`, `class`, `optionLabel`, `optionValue`, `placeholder`, `@change`) are identical in the v4 replacements — no prop changes needed.
  - The `Select` component is already imported in other files (`UserForm.vue`) with the same API. Verify no naming collision in this file (there is none — `Select` was `Dropdown` here before).

---

### Step 2: Migrate `GuestForm.vue` — Calendar

- **File**: `frontend/src/components/guests/GuestForm.vue`
- **Action**: Replace one deprecated import and its template usage

**Script block change (line 5):**

| Current | Replace With |
|---|---|
| `import Calendar from 'primevue/calendar'` | `import DatePicker from 'primevue/datepicker'` |

**Template changes:**

| Location | Current | Replace With |
|---|---|---|
| Line 225 | `<Calendar` | `<DatePicker` |
| Line 236 | `</Calendar>` | `</DatePicker>` |

- **Implementation Notes**: All props (`dateFormat`, `maxDate`, `invalid`, `disabled`, `showIcon`, `@blur`, `class`) are retained as-is.

---

### Step 3: Migrate `PayFeeDialog.vue` — Calendar

- **File**: `frontend/src/components/memberships/PayFeeDialog.vue`
- **Action**: Replace one deprecated import and its template usage

**Script block change (line 5):**

| Current | Replace With |
|---|---|
| `import Calendar from 'primevue/calendar'` | `import DatePicker from 'primevue/datepicker'` |

**Template changes:**

| Location | Current | Replace With |
|---|---|---|
| Line 107 | `<Calendar` | `<DatePicker` |
| Line 117 | `</Calendar>` | `</DatePicker>` |

- **Implementation Notes**: All props retained. Note that `paidDate` is initialized as `new Date()` (not null), so no binding changes needed.

---

### Step 4: Migrate `MembershipDialog.vue` — Calendar

- **File**: `frontend/src/components/memberships/MembershipDialog.vue`
- **Action**: Replace one deprecated import and its template usage

**Script block change (line 5):**

| Current | Replace With |
|---|---|
| `import Calendar from 'primevue/calendar'` | `import DatePicker from 'primevue/datepicker'` |

**Template changes:**

| Location | Current | Replace With |
|---|---|---|
| Line 142 | `<Calendar` | `<DatePicker` |
| Line 149 | `</Calendar>` | `</DatePicker>` |

- **Implementation Notes**: All props retained as-is.

---

### Step 5: Migrate `UserForm.vue` — InputSwitch

- **File**: `frontend/src/components/users/UserForm.vue`
- **Action**: Replace one deprecated import and its template usage

**Script block change (line 5):**

| Current | Replace With |
|---|---|
| `import InputSwitch from 'primevue/inputswitch'` | `import ToggleSwitch from 'primevue/toggleswitch'` |

**Template change:**

| Location | Current | Replace With |
|---|---|---|
| Line 219 | `<InputSwitch id="isActive" v-model="formData.isActive" />` | `<ToggleSwitch id="isActive" v-model="formData.isActive" />` |

- **Implementation Notes**: Self-closing tag, no closing tag needed. `v-model` binding is identical.

---

### Step 6: Update Technical Documentation

- **Action**: Review and update technical documentation to reflect the migration
- **Implementation Steps**:
  1. **Review Changes**: The migration renames 3 component types across 5 files in the frontend
  2. **Identify Documentation Files**:
     - Check `ai-specs/specs/frontend-standards.mdc` for any references to `Calendar`, `Dropdown`, or `InputSwitch` in component lists or examples — replace with `DatePicker`, `Select`, `ToggleSwitch` respectively
     - The spec's "Out of Scope" section already lists `Select`, `DatePicker`, `ToggleSwitch` as non-deprecated — documentation already reflects end state
  3. **Update Documentation**: If `frontend-standards.mdc` lists PrimeVue component examples using deprecated names, update them to v4 names
  4. **Verify**: Confirm the enriched spec file (`tech-debt-primevue-deprecated-components_enriched.md`) can be moved/archived as completed if a workflow exists for that
- **References**: `ai-specs/specs/documentation-standards.mdc`
- **Notes**: This step is MANDATORY before considering implementation complete.

---

## 4. Implementation Order

1. **Step 0** — Create feature branch `feature/tech-debt-primevue-deprecated-components-frontend`
2. **Step 1** — Migrate `FamilyMemberForm.vue` (Calendar + Dropdown → DatePicker + Select)
3. **Step 2** — Migrate `GuestForm.vue` (Calendar → DatePicker)
4. **Step 3** — Migrate `PayFeeDialog.vue` (Calendar → DatePicker)
5. **Step 4** — Migrate `MembershipDialog.vue` (Calendar → DatePicker)
6. **Step 5** — Migrate `UserForm.vue` (InputSwitch → ToggleSwitch)
7. **Step 6** — Update technical documentation

---

## 5. Testing Checklist

### No new tests required
These are API-compatible renames with no behavior change. Existing tests must continue to pass.

### Vitest (if unit tests exist for these components)
- [ ] Run `npm run test:unit` from `frontend/` — all existing tests pass

### Manual verification (from the spec)
- [ ] Open Family Unit page → edit member dialog → `FamilyMemberForm` renders with date picker and relationship dropdown — no console warnings
- [ ] Open Family Unit page → add guest dialog → `GuestForm` renders with date picker — no console warnings
- [ ] Open Profile page → memberships section → `MembershipDialog` opens with date picker — no console warnings
- [ ] Open Profile page → memberships → click "Pagar" → `PayFeeDialog` opens with date picker — no console warnings
- [ ] Open Admin → Users panel → edit user → `UserForm` renders with toggle switch — no console warnings
- [ ] Browser console: zero `Deprecated since v4` messages on all affected pages

### Functional regression checks
- [ ] Date picker opens, allows date selection, and v-model binds correctly in all 4 affected components
- [ ] Relationship dropdown in `FamilyMemberForm` renders options and binds v-model correctly
- [ ] Active toggle in `UserForm` toggles correctly and binds v-model correctly
- [ ] Form submission flows complete end-to-end without errors

---

## 6. Error Handling Patterns

No new error handling required. This task contains no logic changes, only component renames. Existing error handling in each component remains unchanged.

---

## 7. UI/UX Considerations

- **DatePicker** (`primevue/datepicker`): Replaces `Calendar` with identical props. The rendered UI is the same.
- **Select** (`primevue/select`): Replaces `Dropdown` with identical props. The rendered UI is the same.
- **ToggleSwitch** (`primevue/toggleswitch`): Replaces `InputSwitch` with identical props. The rendered UI is the same.
- No layout, spacing, or accessibility changes required.

---

## 8. Dependencies

- **No new npm packages**: All components are already included in `primevue ^4.5.4`
- The import paths change:
  - `primevue/calendar` → `primevue/datepicker`
  - `primevue/dropdown` → `primevue/select` *(already used in `UserForm.vue` with the same path)*
  - `primevue/inputswitch` → `primevue/toggleswitch`

---

## 9. Notes

- **No logic changes**: This is purely a rename migration. Do not alter any props, events, computed properties, or validation logic.
- **API compatibility confirmed**: All props used in the 5 files are retained in v4 replacement components (verified in spec).
- **`Select` already in use**: `primevue/select` is already imported in `UserForm.vue` (line 4) and other files — the import path is correct.
- **English only**: All code and documentation must remain in English (business UI labels in Spanish are already present and should not be changed).
- **TypeScript**: No type changes needed — these are PrimeVue UI components, not data model types.
- **Self-closing tags**: `InputSwitch` in `UserForm.vue` is self-closing (`/>`). Rename to `ToggleSwitch` preserving the self-closing syntax.

---

## 10. Next Steps After Implementation

- Verify with a browser session that no `Deprecated since v4` console messages appear on any affected page
- Commit with a descriptive message (e.g., `chore(frontend): migrate PrimeVue deprecated components to v4 names`)
- Open PR against `main` following the project's PR workflow

---

## 11. Implementation Verification

- [ ] **Code Quality**: All 5 files still use `<script setup lang="ts">` — no Options API introduced
- [ ] **Functionality**: All date pickers, dropdowns, and toggle switches render and bind correctly
- [ ] **Testing**: Existing Vitest tests pass; manual functional checks pass (see checklist above)
- [ ] **No regressions**: Form submission flows work end-to-end
- [ ] **Zero console warnings**: No `Deprecated since v4` messages in browser console
- [ ] **Documentation**: `frontend-standards.mdc` reviewed and updated if needed
