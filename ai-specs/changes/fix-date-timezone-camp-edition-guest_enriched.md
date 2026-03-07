# Fix: Systematic Date Off-by-One-Day Bug (Timezone Conversion)

## Summary

Multiple components across the application have a timezone bug where dates are shifted one day backwards in UTC+ timezones (e.g. Spain UTC+1/+2). This was previously fixed only for `FamilyMemberForm.vue`, but the same pattern exists in **13+ additional files**. The bug affects:

1. **Data corruption** — wrong dates sent to the API via `.toISOString().split('T')[0]`
2. **Wrong form display** — wrong dates shown in DatePickers via `new Date(dateOnlyString)`
3. **Wrong calculations** — incorrect age calculations and duration computations
4. **Wrong display** — incorrect dates shown in read-only views

---

## Root Cause Analysis

Two anti-patterns cause this bug:

### Anti-pattern 1: Serialization — `date.toISOString().split('T')[0]`

`.toISOString()` converts the local Date to UTC before extracting the date string. In UTC+ timezones (e.g. Spain UTC+1), local midnight `2025-08-15T00:00:00+01:00` becomes `2025-08-14T23:00:00Z`, so the extracted date is `2025-08-14` — **one day earlier**.

### Anti-pattern 2: Deserialization — `new Date('YYYY-MM-DD')`

Per ECMAScript spec, `new Date('2025-08-15')` (date-only ISO string without time) is parsed as **UTC midnight**. In UTC+1 this becomes `2025-08-14T23:00:00` local time — the DatePicker/calculation sees **August 14** instead of August 15.

**Important distinction**: Full ISO timestamps like `"2025-08-15T00:00:00Z"` (with `T` and timezone) are parsed correctly by `new Date()`. The bug only affects **date-only strings** (`"YYYY-MM-DD"`) which correspond to backend `DateOnly` fields.

### Backend field types for reference

| Backend Type | JSON Format | `new Date()` safe? | Fields |
|---|---|---|---|
| `DateTime` | `"2025-08-15T00:00:00Z"` | Yes (has timezone) | startDate, endDate, createdAt, updatedAt |
| `DateOnly` | `"2025-08-15"` | **NO (parsed as UTC)** | dateOfBirth, halfDate, weekendStartDate, weekendEndDate, paidDate |

---

## Existing Fix (Already Implemented)

Shared utility functions exist in [`frontend/src/utils/date.ts`](../../frontend/src/utils/date.ts):

```typescript
export function formatDateLocal(date: Date): string   // replaces .toISOString().split('T')[0]
export function parseDateLocal(dateStr: string): Date  // replaces new Date('YYYY-MM-DD')
```

These use local timezone components (`getFullYear`, `getMonth`, `getDate`) instead of UTC conversion. Unit tests exist in `frontend/src/utils/date.test.ts`.

**Note**: `parseDateLocal` only works with date-only strings (`YYYY-MM-DD`). Do NOT use it for full timestamps.

---

## Acceptance Criteria

- [ ] All date-only fields are sent correctly to the backend (verified in DevTools Network tab)
- [ ] All DatePickers display the correct stored dates when editing
- [ ] Age calculations are correct regardless of timezone
- [ ] Duration calculations are correct regardless of timezone
- [ ] Display-only date formatting shows the correct date regardless of timezone
- [ ] The fix works for all UTC offsets (UTC-12 through UTC+14)
- [ ] No regressions in fields that were already working (full timestamps)

---

## Files to Modify — Organized by Severity

### CRITICAL: Serialization bugs (wrong data sent to API)

These are the highest priority — they cause **data corruption** in the database.

| # | File | Line | Current Code | Fix |
|---|------|------|---|---|
| 1 | `frontend/src/components/camps/CampEditionProposeDialog.vue` | 150 | `date.toISOString().split('T')[0]` | `formatDateLocal(date)` |
| 2 | `frontend/src/components/camps/CampEditionUpdateDialog.vue` | 123 | `date.toISOString().split('T')[0]` | `formatDateLocal(date)` |
| 3 | `frontend/src/components/guests/GuestForm.vue` | 162 | `dateOfBirth.value!.toISOString().split('T')[0]` | `formatDateLocal(dateOfBirth.value!)` |
| 4 | `frontend/src/components/memberships/PayFeeDialog.vue` | 79 | `paidDate.value.toISOString().split('T')[0]` | `formatDateLocal(paidDate.value)` |
| 5 | `frontend/src/components/admin/PaymentsAllList.vue` | 48 | `d.toISOString().split('T')[0]` | `formatDateLocal(d)` |

### HIGH: Form deserialization bugs (wrong dates in editable DatePickers)

These cause users to see/submit wrong dates when editing existing records.

| # | File | Lines | Field(s) | Fix |
|---|------|-------|----------|-----|
| 6 | `frontend/src/components/camps/CampEditionUpdateDialog.vue` | 102, 107-108 | halfDate, weekendStartDate, weekendEndDate (`DateOnly`) | `parseDateLocal()` |
| 7 | `frontend/src/components/guests/GuestForm.vue` | 24-26 | dateOfBirth (`DateOnly`) | `parseDateLocal()` |
| 8 | `frontend/src/components/registrations/RegistrationMemberSelector.vue` | 36, 39 | weekendStartDate, weekendEndDate (`DateOnly`, used as min/max for DatePickers) | `parseDateLocal()` |
| 9 | `frontend/src/components/registrations/RegistrationMemberSelector.vue` | 194, 203, 206 | visitStartDate, visitEndDate (in DatePicker model-value and min-date) | `parseDateLocal()` |

**Note on CampEditionUpdateDialog lines 88-89**: `startDate` and `endDate` are `DateTime` fields (full timestamps), so `new Date()` works correctly there. However, for consistency and safety, consider using `parseDateLocal()` if the backend ever changes serialization. The same applies to `CampEditionProposeDialog.vue` lines 60, 63-64 (sorting/prefilling with `startDate`/`endDate`).

### MEDIUM: Calculation bugs (wrong age/duration results)

These cause incorrect business logic results.

| # | File | Lines | Issue | Fix |
|---|------|-------|-------|-----|
| 10 | `frontend/src/components/registrations/RegistrationMemberSelector.vue` | 127 | `new Date(member.dateOfBirth)` in `isMinor()` — age may be off by 1 year at boundary | `parseDateLocal()` |
| 11 | `frontend/src/components/family-units/FamilyMemberList.vue` | 30 | `new Date(dateOfBirth)` in `calculateAge()` — same off-by-one-year risk | `parseDateLocal()` |
| 12 | `frontend/src/views/ProfilePage.vue` | 55 | `new Date(dateOfBirth)` in `calculateAge()` — same issue | `parseDateLocal()` |
| 13 | `frontend/src/utils/registration.ts` | 22-23, 27 | `new Date(startDate)`, `new Date(endDate)`, `new Date(halfDate)` in `computePeriodDays()` — `halfDate` is `DateOnly`, duration may be off | `parseDateLocal()` for halfDate; startDate/endDate are `DateTime` (safe but review) |
| 14 | `frontend/src/components/camps/CampEditionStatusDialog.vue` | 48 | `new Date(props.edition.startDate) < today` — startDate is `DateTime` (likely safe), but verify | Review only |
| 15 | `frontend/src/components/camps/CampEditionDetails.vue` | 23-24 | `new Date(startDate/endDate)` for duration — `DateTime` fields (likely safe) | Review only |
| 16 | `frontend/src/views/CampPage.vue` | 83 | `new Date(e.endDate) - new Date(e.startDate)` for duration — `DateTime` fields (likely safe) | Review only |

### LOW: Display-only formatting bugs (wrong date shown in read-only views)

These use `new Date(dateStr)` inside `formatDate()` helper functions. Only date-only strings (`DateOnly` backend fields) are affected. Full timestamps (`DateTime` fields like `createdAt`) display correctly.

The `formatDate` pattern appears in these files — each needs review to determine if it ever receives a `DateOnly` value:

| # | File | Line | Used for |
|---|------|------|----------|
| 17 | `frontend/src/components/registrations/RegistrationMemberSelector.vue` | 121-124 | `formatDate(member.dateOfBirth)` — `DateOnly`, **AFFECTED** |
| 18 | `frontend/src/components/family-units/FamilyMemberList.vue` | 42-49 | `formatDate(dateString)` — used for dateOfBirth, **AFFECTED** |
| 19 | `frontend/src/components/admin/PaymentsAllList.vue` | 43-46 | `formatDate(dateStr)` — review if used with `DateOnly` fields |
| 20 | `frontend/src/components/admin/PaymentsReviewQueue.vue` | 41 | `formatDate(dateStr)` — review if used with `DateOnly` fields |
| 21 | `frontend/src/components/payments/PaymentInstallmentCard.vue` | 27 | `formatDate(dateStr)` — review if used with `DateOnly` fields (e.g. dueDate, paidDate) |
| 22 | `frontend/src/components/payments/ProofUploader.vue` | 45 | `formatDate(dateStr)` — review if used with `DateOnly` fields |
| 23 | `frontend/src/views/CampPage.vue` | 77 | `formatDate(dateStr)` — used with startDate/endDate (`DateTime`, likely safe) |
| 24 | `frontend/src/views/camps/CampEditionDetailPage.vue` | 25 | `formatDate(dateStr)` — review which fields it formats |
| 25 | `frontend/src/views/camps/CampEditionsPage.vue` | 92 | `formatDate(dateStr)` — review which fields it formats |
| 26 | `frontend/src/views/registrations/RegistrationDetailPage.vue` | 70, 83 | `formatDate(dateStr)` — review which fields it formats |
| 27 | `frontend/src/views/registrations/RegisterForCampPage.vue` | 95 | `formatDate(dateStr)` — review which fields it formats |
| 28 | `frontend/src/components/camps/CampEditionDetails.vue` | 17-20 | `formatDate(dateStr)` — used with startDate/endDate (`DateTime`, likely safe) |
| 29 | `frontend/src/components/camps/ActiveEditionCard.vue` | 15 | `formatDate(dateStr)` — review which fields it formats |
| 30 | `frontend/src/components/registrations/RegistrationCard.vue` | 16 | `formatDate(dateStr)` — review which fields it formats |
| 31 | `frontend/src/components/memberships/MembershipDialog.vue` | 51 | `formatDate(dateString)` — review if used with `DateOnly` fields |
| 32 | `frontend/src/views/ProfilePage.vue` | 50-51 | `formatDate(iso)` — likely used for dateOfBirth, **AFFECTED** |

**Note**: Components that only format `DateTime` fields (createdAt, updatedAt, lastGoogleSyncAt, changedAt) are NOT affected and should NOT be changed. These include:
- `CampLocationDetailPage.vue` (lines 330, 334 — createdAt, updatedAt)
- `FamilyUnitPage.vue` (line 290 — createdAt)
- `CampContactInfo.vue` (line 129 — lastGoogleSyncAt)
- `CampAuditLogSection.vue` (line 35 — changedAt)
- `CampObservationsSection.vue` (line 98 — createdAt)
- `UserDetailPage.vue` (line 70 — likely full timestamps)
- `UsersPage.vue` (line 89 — likely full timestamps)
- `UsersAdminPanel.vue` (line 62 — likely full timestamps)
- `RegistrationsAdminPanel.vue` (line 69 — review)
- `FamilyUnitsAdminPanel.vue` (line 31 — review)
- `MediaItemsReviewPanel.vue` (line 51 — review)

---

## Recommended Implementation Strategy

### Step 1: Create a `parseDateSafe` helper (optional but recommended)

To avoid accidentally using `parseDateLocal` on full timestamps, consider adding a defensive wrapper:

```typescript
// In frontend/src/utils/date.ts

/**
 * Safe parser that handles both date-only ("YYYY-MM-DD") and full ISO timestamps.
 * Use this when you're unsure which format the string will be in.
 */
export function parseDateSafe(dateStr: string): Date {
  if (dateStr.includes('T')) {
    // Full ISO timestamp — new Date handles these correctly
    return new Date(dateStr)
  }
  // Date-only string — must parse as local to avoid UTC shift
  return parseDateLocal(dateStr)
}
```

This would allow a single function to replace ALL `new Date(dateStr)` calls safely, regardless of whether the input is a date-only or full timestamp.

### Step 2: Fix CRITICAL serialization bugs (5 files)

Replace all `.toISOString().split('T')[0]` with `formatDateLocal()`.

### Step 3: Fix HIGH form deserialization bugs (4 files)

Replace `new Date(dateOnlyString)` with `parseDateLocal()` in DatePicker bindings.

### Step 4: Fix MEDIUM calculation bugs (3-6 files)

Replace `new Date(dateOfBirth)` and `new Date(halfDate)` with `parseDateLocal()` in age/duration calculations.

### Step 5: Fix LOW display formatting bugs (review and fix selectively)

For each `formatDate()` function, check if it ever receives a `DateOnly` field value. If so, use `parseDateLocal()` or `parseDateSafe()` instead of `new Date()`.

---

## Detailed Fix Instructions

### Fix 1: CampEditionProposeDialog.vue

```typescript
// Add import:
import { formatDateLocal } from '@/utils/date'

// Line 150 — replace:
const toISODate = (date: Date): string => date.toISOString().split('T')[0]
// with:
const toISODate = (date: Date): string => formatDateLocal(date)
```

Lines 60, 63-64 use `new Date()` on `startDate`/`endDate` which are `DateTime` fields (full timestamps). These are safe but can optionally use `parseDateSafe()` for consistency.

### Fix 2: CampEditionUpdateDialog.vue

```typescript
// Add import:
import { formatDateLocal, parseDateLocal } from '@/utils/date'

// Lines 121-124 — replace serialization:
const formatDateToIso = (date: Date | null): string => {
  if (!date) return ''
  return formatDateLocal(date)
}

// Lines 102, 107-108 — replace deserialization of DateOnly fields:
form.halfDate = props.edition.halfDate ? parseDateLocal(props.edition.halfDate) : null
form.weekendStartDate = props.edition.weekendStartDate ? parseDateLocal(props.edition.weekendStartDate) : null
form.weekendEndDate = props.edition.weekendEndDate ? parseDateLocal(props.edition.weekendEndDate) : null
```

Lines 88-89 (`startDate`, `endDate`) are `DateTime` fields — safe with `new Date()`, but can optionally use `parseDateSafe()`.

### Fix 3: GuestForm.vue

```typescript
// Add import:
import { formatDateLocal, parseDateLocal } from '@/utils/date'

// Lines 24-26 — replace deserialization:
const dateOfBirth = ref<Date | null>(
  props.guest?.dateOfBirth ? parseDateLocal(props.guest.dateOfBirth) : null,
)

// Line 162 — replace serialization:
dateOfBirth: formatDateLocal(dateOfBirth.value!),
```

### Fix 4: PayFeeDialog.vue

```typescript
// Add import:
import { formatDateLocal } from '@/utils/date'

// Line 79 — replace serialization:
paidDate: formatDateLocal(paidDate.value),
```

### Fix 5: PaymentsAllList.vue

```typescript
// Add import:
import { formatDateLocal } from '@/utils/date'

// Line 48 — replace serialization:
const toIsoDate = (d: Date): string => formatDateLocal(d)
```

### Fix 6: RegistrationMemberSelector.vue

```typescript
// Add import:
import { parseDateLocal } from '@/utils/date'

// Lines 35-40 — replace deserialization of DateOnly fields:
const weekendMinDate = computed(() =>
  props.edition.weekendStartDate ? parseDateLocal(props.edition.weekendStartDate) : undefined
)
const weekendMaxDate = computed(() =>
  props.edition.weekendEndDate ? parseDateLocal(props.edition.weekendEndDate) : undefined
)

// Line 127 — replace in isMinor:
const dob = parseDateLocal(member.dateOfBirth)

// Lines 194, 203, 206 — replace in template DatePicker bindings:
// Use parseDateLocal() for visitStartDate/visitEndDate strings
```

### Fix 7: FamilyMemberList.vue

```typescript
// Add import:
import { parseDateLocal } from '@/utils/date'

// Line 30 — replace in calculateAge:
const birthDate = parseDateLocal(dateOfBirth)

// Line 43 — replace in formatDate:
const date = parseDateLocal(dateString)
```

### Fix 8: ProfilePage.vue

```typescript
// Add import:
import { parseDateLocal } from '@/utils/date'

// Line 55 — replace in calculateAge:
const birth = parseDateLocal(dateOfBirth)
```

### Fix 9: registration.ts (utility)

```typescript
// Add import:
import { parseDateLocal } from '@/utils/date'

// Lines 22-23, 27 — in computePeriodDays:
// halfDate is DateOnly — must use parseDateLocal:
const half = parseDateLocal(halfDate)
// startDate/endDate are DateTime — review if they come as full timestamps or date-only
```

---

## Non-Functional Requirements

- **No new dependencies** — uses the existing `@/utils/date` utility
- **Timezone-safe** — works in all UTC offsets (UTC-12 through UTC+14)
- **No backend changes** — the YYYY-MM-DD / ISO timestamp formats are correct
- **Backwards compatible** — no API contract changes

## Scope

- **Affects**: 13+ frontend files (see tables above)
- **Already fixed**: FamilyMemberForm.vue
- **Does NOT require**: backend changes, migrations, API changes

## Completion Checklist

- [ ] Fix all 5 CRITICAL serialization bugs (`.toISOString().split('T')[0]`)
- [ ] Fix all 4 HIGH deserialization bugs (DatePicker bindings with `DateOnly` fields)
- [ ] Fix all MEDIUM calculation bugs (`calculateAge`, `computePeriodDays`, `isMinor`)
- [ ] Review and fix LOW display formatting bugs (only for `DateOnly` field values)
- [ ] Optionally add `parseDateSafe()` utility for mixed date string handling
- [ ] Verify dates in Network tab when creating/editing CampEdition, Guest, PayFee
- [ ] Verify correct display when opening existing records for editing
- [ ] Verify age calculations are correct
- [ ] Run existing `date.test.ts` tests to confirm utilities work
