# Tech Debt: Migrate PrimeVue v4 Deprecated Components

## Context

The project uses **PrimeVue `^4.5.4`**. PrimeVue v4 deprecated and renamed several components, and the browser console currently emits deprecation warnings for the affected files. Some components have already been migrated (e.g., `CampEditionUpdateDialog.vue` already uses `DatePicker` and `ToggleSwitch`), but the migration is incomplete.

**Goal**: Eliminate all PrimeVue v4 deprecation warnings by migrating the remaining 3 component types in 6 files.

---

## Deprecated Components Audit

### ✅ Already Migrated (Reference)

These components are already using the correct v4 names in the codebase:

| Old (deprecated) | New (v4) | Already Used In |
|---|---|---|
| `Calendar` | `DatePicker` | `CampEditionUpdateDialog.vue`, `CampEditionProposeDialog.vue` |
| `Dropdown` | `Select` | `UserForm.vue`, `UserRoleDialog.vue`, `CampLocationsPage.vue`, `AnniversaryUploadForm.vue`, `CampEditionsPage.vue` |
| `InputSwitch` | `ToggleSwitch` | `CampEditionUpdateDialog.vue` |

### ❌ Pending Migration (This Task)

| Deprecated Component | Replacement | Files Affected |
|---|---|---|
| `Calendar` | `DatePicker` | `FamilyMemberForm.vue`, `GuestForm.vue`, `PayFeeDialog.vue`, `MembershipDialog.vue` |
| `Dropdown` | `Select` | `FamilyMemberForm.vue` |
| `InputSwitch` | `ToggleSwitch` | `UserForm.vue` |

---

## Files to Modify

### 1. `frontend/src/components/family-units/FamilyMemberForm.vue`

**Current:**
```ts
import Calendar from 'primevue/calendar'
import Dropdown from 'primevue/dropdown'
```

**Target:**
```ts
import DatePicker from 'primevue/datepicker'
import Select from 'primevue/select'
```

- In the template: replace `<Calendar` / `</Calendar>` → `<DatePicker` / `</DatePicker>`
- In the template: replace `<Dropdown` / `</Dropdown>` → `<Select` / `</Select>`

---

### 2. `frontend/src/components/guests/GuestForm.vue`

**Current:**
```ts
import Calendar from 'primevue/calendar'
```

**Target:**
```ts
import DatePicker from 'primevue/datepicker'
```

- In the template: replace `<Calendar` / `</Calendar>` → `<DatePicker` / `</DatePicker>`

---

### 3. `frontend/src/components/memberships/PayFeeDialog.vue`

**Current:**
```ts
import Calendar from 'primevue/calendar'
```

**Target:**
```ts
import DatePicker from 'primevue/datepicker'
```

- In the template: replace `<Calendar` / `</Calendar>` → `<DatePicker` / `</DatePicker>`

---

### 4. `frontend/src/components/memberships/MembershipDialog.vue`

**Current:**
```ts
import Calendar from 'primevue/calendar'
```

**Target:**
```ts
import DatePicker from 'primevue/datepicker'
```

- In the template: replace `<Calendar` / `</Calendar>` → `<DatePicker` / `</DatePicker>`

---

### 5. `frontend/src/components/users/UserForm.vue`

**Current:**
```ts
import InputSwitch from 'primevue/inputswitch'
```

**Target:**
```ts
import ToggleSwitch from 'primevue/toggleswitch'
```

- In the template: replace `<InputSwitch` / `</InputSwitch>` → `<ToggleSwitch` / `</ToggleSwitch>`

---

## API Compatibility Notes

All three migrations are API-compatible (same props/events):

### `Calendar` → `DatePicker`
- `v-model` — works identically
- `date-format` / `dateFormat` — prop retained
- `disabled` — retained
- `min-date` / `max-date` — retained
- `placeholder` — retained
- `class` — retained

### `Dropdown` → `Select`
- Same API as documented in `merged/tech-debt/primevue-dropdown-migration.md`
- All props (`v-model`, `:options`, `option-label`, `option-value`, `placeholder`, `class`, `disabled`, `filter`) are compatible

### `InputSwitch` → `ToggleSwitch`
- `v-model` — works identically
- `disabled` — retained
- `class` — retained

---

## Verification Checklist

After implementing, verify:

1. **No browser console warnings**: Open each affected page and confirm no `Deprecated since v4` messages
   - [ ] Family Unit page (open edit member dialog → `FamilyMemberForm`)
   - [ ] Family Unit page (open add guest dialog → `GuestForm`)
   - [ ] Profile page → memberships section → `PayFeeDialog` and `MembershipDialog`
   - [ ] Admin → Users panel → edit user → `UserForm`

2. **Functional tests**:
   - [ ] Date picker opens and selects dates correctly in `FamilyMemberForm`
   - [ ] Date picker opens and selects dates correctly in `GuestForm`
   - [ ] Date picker opens and selects dates correctly in `PayFeeDialog`
   - [ ] Date picker opens and selects dates correctly in `MembershipDialog`
   - [ ] Relationship dropdown renders options and v-model binding works in `FamilyMemberForm`
   - [ ] Active toggle renders correctly and binds v-model in `UserForm`

3. **No regressions**: Existing form submission flows work end-to-end

---

## Non-Functional Notes

- **Risk**: Very low — API-compatible renames, no logic changes required
- **Scope**: Frontend only, no backend changes
- **Tests**: No new tests required (these are cosmetic UI component renames with no behavior change). Existing tests should continue to pass.
- **Priority**: Medium (console noise; no functional impact but degrades DX and signals code readiness for future PrimeVue upgrades)

---

## Out of Scope

The following PrimeVue components in use are **not deprecated** in v4 and require no action:

`Button`, `Dialog`, `DataTable`, `Column`, `InputText`, `Textarea`, `Message`, `Tag`, `Card`, `ProgressSpinner`, `ConfirmDialog`, `Checkbox`, `InputNumber`, `Toast`, `Menu`, `Avatar`, `Password`, `AutoComplete`, `Panel`, `Divider`, `Select`, `DatePicker`, `ToggleSwitch`, `Tabs`/`TabList`/`Tab`/`TabPanels`/`TabPanel`, `Stepper`/`StepList`/`Step`/`StepPanels`/`StepPanel`, `Accordion`/`AccordionPanel`/`AccordionHeader`/`AccordionContent`, `Galleria`, `Image`, `Timeline`, `OrderList`, `FileUpload`, `Skeleton`
