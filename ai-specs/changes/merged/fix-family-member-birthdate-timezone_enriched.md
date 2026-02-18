# Fix: Family Member Birthdate Off-by-One-Day Due to Timezone Conversion

## Summary

When a user selects a birthdate for a family member (e.g. 20/09/2020), the frontend sends the previous day (2020-09-19) to the backend. This is caused by a UTC timezone conversion bug in `FamilyMemberForm.vue`, and affects both **submitting** dates and **loading** existing dates into the form.

---

## Root Cause Analysis

The bug has **two manifestations** in [`frontend/src/components/family-units/FamilyMemberForm.vue`](../../frontend/src/components/family-units/FamilyMemberForm.vue):

### 1. Serialization bug (on form submit) — Line 192

```typescript
// ❌ BUG: .toISOString() converts to UTC before extracting the date part
dateOfBirth: dateOfBirth.value!.toISOString().split('T')[0], // YYYY-MM-DD
```

**Why it fails**: When a user in UTC+1 (e.g. Spain/Madrid timezone) selects September 20, 2020, the browser creates a `Date` representing `2020-09-20T00:00:00+01:00`. Calling `.toISOString()` converts this to UTC: `2020-09-19T23:00:00Z`. Splitting on `T` extracts `2020-09-19` — **one day earlier than selected**.

### 2. Deserialization bug (on load for edit mode) — Line 38

```typescript
// ❌ BUG: new Date('YYYY-MM-DD') parses date-only strings as UTC midnight
const dateOfBirth = ref<Date | null>(props.member?.dateOfBirth ? new Date(props.member.dateOfBirth) : null)
```

**Why it fails**: Per ECMAScript spec, `new Date('2020-09-20')` (a date-only ISO string) is parsed as `2020-09-20T00:00:00Z` (UTC midnight). In a UTC+1 browser, this becomes `2020-09-19T23:00:00` local time, which the PrimeVue Calendar renders as **September 19**.

---

## Acceptance Criteria

- [ ] Selecting September 20, 2020 in the form sends `2020-09-20` to the backend (verified in DevTools Network tab)
- [ ] Opening a family member for editing shows the correct stored birthdate in the calendar
- [ ] The fix works regardless of the user's browser timezone (UTC-12 through UTC+14)
- [ ] Unit tests cover the date serialization and deserialization logic

---

## Technical Approach (TDD)

### Step 1 — RED: Write failing tests first

Create `frontend/src/utils/__tests__/date.spec.ts`:

```typescript
import { describe, it, expect } from 'vitest'
import { formatDateLocal, parseDateLocal } from '@/utils/date'

describe('formatDateLocal', () => {
  it('should format date using local timezone components, not UTC', () => {
    // Simulate a date that is midnight local time in UTC+1
    // 2020-09-20T00:00:00+01:00 == 2020-09-19T23:00:00Z
    const date = new Date(2020, 8, 20) // month is 0-indexed; creates local date
    expect(formatDateLocal(date)).toBe('2020-09-20')
  })

  it('should zero-pad month and day', () => {
    const date = new Date(2020, 0, 5) // January 5
    expect(formatDateLocal(date)).toBe('2020-01-05')
  })
})

describe('parseDateLocal', () => {
  it('should parse YYYY-MM-DD as local midnight, not UTC midnight', () => {
    const date = parseDateLocal('2020-09-20')
    expect(date.getFullYear()).toBe(2020)
    expect(date.getMonth()).toBe(8) // 0-indexed
    expect(date.getDate()).toBe(20)
  })

  it('should handle single-digit months and days', () => {
    const date = parseDateLocal('2020-01-05')
    expect(date.getFullYear()).toBe(2020)
    expect(date.getMonth()).toBe(0)
    expect(date.getDate()).toBe(5)
  })
})
```

### Step 2 — GREEN: Implement utility functions

Create `frontend/src/utils/date.ts`:

```typescript
/**
 * Format a Date to YYYY-MM-DD using LOCAL timezone components.
 * Do NOT use .toISOString() which converts to UTC first.
 */
export function formatDateLocal(date: Date): string {
  const year = date.getFullYear()
  const month = String(date.getMonth() + 1).padStart(2, '0')
  const day = String(date.getDate()).padStart(2, '0')
  return `${year}-${month}-${day}`
}

/**
 * Parse a YYYY-MM-DD string as a LOCAL midnight Date.
 * Do NOT use new Date(str) for date-only strings — it parses as UTC midnight.
 */
export function parseDateLocal(dateStr: string): Date {
  const [year, month, day] = dateStr.split('-').map(Number)
  return new Date(year, month - 1, day) // constructor uses local timezone
}
```

### Step 3 — Fix `FamilyMemberForm.vue`

Apply two targeted fixes:

**Fix deserialization (line 38):**
```typescript
// Before (BUG):
const dateOfBirth = ref<Date | null>(props.member?.dateOfBirth ? new Date(props.member.dateOfBirth) : null)

// After (FIXED):
import { formatDateLocal, parseDateLocal } from '@/utils/date'

const dateOfBirth = ref<Date | null>(props.member?.dateOfBirth ? parseDateLocal(props.member.dateOfBirth) : null)
```

**Fix serialization (line 192):**
```typescript
// Before (BUG):
dateOfBirth: dateOfBirth.value!.toISOString().split('T')[0],

// After (FIXED):
dateOfBirth: formatDateLocal(dateOfBirth.value!),
```

---

## Files to Modify

| File | Change |
|------|--------|
| `frontend/src/utils/date.ts` | **CREATE** — `formatDateLocal` and `parseDateLocal` utilities |
| `frontend/src/utils/__tests__/date.spec.ts` | **CREATE** — Unit tests (write FIRST, TDD) |
| `frontend/src/components/family-units/FamilyMemberForm.vue` | **MODIFY** — Use `parseDateLocal` on line 38 and `formatDateLocal` on line 192 |

> **Note**: Do not modify `useFamilyUnits.ts` or any backend code. The `dateOfBirth` field type (`string` / YYYY-MM-DD) is correct in `types/family-unit.ts`. The problem is purely in how the form component converts between `Date` objects and ISO strings.

---

## Non-Functional Requirements

- **No new dependencies** — fix uses only native JavaScript `Date` API
- **Timezone-safe** — must work in all UTC offsets (e.g. UTC-5 Americas, UTC+1 Spain, UTC+9 Japan)
- **Test coverage**: Both utility functions must be fully covered

---

## Scope

- **Does NOT affect**: camp dates, registration dates, or any other date fields in the app
- **Does NOT require**: backend changes, migration, or API changes
- Scope is limited to `FamilyMemberForm.vue` and a new shared date utility
