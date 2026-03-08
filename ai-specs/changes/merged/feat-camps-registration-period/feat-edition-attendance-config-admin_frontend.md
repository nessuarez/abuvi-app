# Admin — Camp Edition Attendance Config Fields

## Context

The backend already supports partial-attendance (week pricing) and weekend-visit fields on `CampEdition`. The registration wizard (`RegistrationMemberSelector.vue`) conditionally shows period selectors and date pickers based on these fields. However, **the admin dialogs for creating and editing editions never send these fields**, so they are always `null` in the database and the registration UI only shows "Campamento completo".

**Goal**: Add the 10 missing attendance-configuration fields to both admin dialogs so that board members can enable partial attendance and weekend visits when proposing or editing a camp edition.

**Depends on**: `feat-registration-attendance-period` (backend + frontend — already implemented and merged)

---

## Codebase Anchors

| File | What to know |
|------|--------------|
| `frontend/src/components/camps/CampEditionProposeDialog.vue` | Form for proposing new editions. Uses `ref<FormModel>` + manual `validate()`. Calls `proposeEdition(...)` from composable. |
| `frontend/src/components/camps/CampEditionUpdateDialog.vue` | Form for editing editions. Uses `reactive<FormModel>` + manual `validate()`. Calls `updateEdition(id, request)`. Has `isOpenEdition` guard that disables pricing/date fields. |
| `frontend/src/types/camp-edition.ts` | `ProposeCampEditionRequest` and `UpdateCampEditionRequest` already declare all 10 fields as optional. `CampEdition` interface also has them. |
| `frontend/src/composables/useCampEditions.ts` | `proposeEdition` and `updateEdition` — just forward the request object to the API. No filtering. |

---

## Fields to Add (both dialogs)

All fields are **optional**. They appear in two collapsible sections controlled by `ToggleSwitch`.

### Section 1 — Asistencia parcial (semanas)

| Field | Type | Form key | Visible when |
|-------|------|----------|--------------|
| Permitir asistencia parcial | `boolean` toggle | `allowPartialAttendance` | Always |
| Fecha de corte (mitad) | `DatePicker` → `DateOnly` | `halfDate` | `allowPartialAttendance = true` |
| Precio adulto/semana | `InputNumber` currency | `pricePerAdultWeek` | `allowPartialAttendance = true` |
| Precio niño/semana | `InputNumber` currency | `pricePerChildWeek` | `allowPartialAttendance = true` |
| Precio bebé/semana | `InputNumber` currency | `pricePerBabyWeek` | `allowPartialAttendance = true` |

### Section 2 — Visitas de fin de semana

| Field | Type | Form key | Visible when |
|-------|------|----------|--------------|
| Permitir visitas fin de semana | `boolean` toggle | `allowWeekendVisit` | Always |
| Fecha inicio fin de semana | `DatePicker` → `DateOnly` | `weekendStartDate` | `allowWeekendVisit = true` |
| Fecha fin fin de semana | `DatePicker` → `DateOnly` | `weekendEndDate` | `allowWeekendVisit = true` |
| Precio adulto/fin de semana | `InputNumber` currency | `pricePerAdultWeekend` | `allowWeekendVisit = true` |
| Precio niño/fin de semana | `InputNumber` currency | `pricePerChildWeekend` | `allowWeekendVisit = true` |
| Precio bebé/fin de semana | `InputNumber` currency | `pricePerBabyWeekend` | `allowWeekendVisit = true` |
| Capacidad máx. fin de semana | `InputNumber` (optional) | `maxWeekendCapacity` | `allowWeekendVisit = true` |

> `allowPartialAttendance` and `allowWeekendVisit` are **UI-only toggles** derived from the presence of pricing fields. They are not sent to the API. When the toggle is off, all fields in that section are sent as `null`.

---

## UI Layout

Both sections follow the same pattern as the existing "Usar rangos de edad personalizados" toggle: a `ToggleSwitch` + label row, followed by a conditional block of fields.

```
┌─────────────────────────────────────────────────────────────┐
│ ... existing fields (dates, prices, capacity, notes) ...    │
│                                                             │
│ ── Asistencia parcial por semanas ──────────────────────── │
│ [○ toggle] Permitir inscripción por semanas                │
│                                                             │
│   (when ON:)                                                │
│   Fecha de corte          [  📅 dd/mm/yy  ] (opcional)     │
│                                                             │
│   Precio adulto/sem   Precio niño/sem   Precio bebé/sem   │
│   [  110,00 €  ]      [  55,00 €  ]     [  0,00 €  ]      │
│                                                             │
│ ── Visitas de fin de semana ────────────────────────────── │
│ [○ toggle] Permitir visitas de fin de semana               │
│                                                             │
│   (when ON:)                                                │
│   Fecha inicio            Fecha fin                         │
│   [  📅 dd/mm/yy  ]      [  📅 dd/mm/yy  ]                │
│                                                             │
│   Precio adulto/fds  Precio niño/fds   Precio bebé/fds    │
│   [  40,00 €  ]      [  20,00 €  ]     [  0,00 €  ]       │
│                                                             │
│   Capacidad máx. fin de semana (opcional)                   │
│   [  ___  ]                                                 │
└─────────────────────────────────────────────────────────────┘
```

Place both sections **after** the capacity field and **before** the notes field (in Update dialog) or the proposal reason field (in Propose dialog).

---

## Changes per File

### 1. `CampEditionProposeDialog.vue`

#### Form model — add fields

```typescript
// Add to form ref initial value:
allowPartialAttendance: false,
halfDate: null as Date | null,
pricePerAdultWeek: null as number | null,
pricePerChildWeek: null as number | null,
pricePerBabyWeek: null as number | null,
allowWeekendVisit: false,
weekendStartDate: null as Date | null,
weekendEndDate: null as Date | null,
pricePerAdultWeekend: null as number | null,
pricePerChildWeekend: null as number | null,
pricePerBabyWeekend: null as number | null,
maxWeekendCapacity: null as number | null,
```

#### Reset on open — in the `watch(visible)` block

Set all new fields to their defaults (false / null).

#### Validation — add to `validate()`

```typescript
// Week pricing: all-or-nothing when toggle is on
if (form.value.allowPartialAttendance) {
  if (form.value.pricePerAdultWeek == null || form.value.pricePerAdultWeek < 0)
    errors.value.pricePerAdultWeek = 'El precio por adulto/semana es obligatorio'
  if (form.value.pricePerChildWeek == null || form.value.pricePerChildWeek < 0)
    errors.value.pricePerChildWeek = 'El precio por niño/semana es obligatorio'
  if (form.value.pricePerBabyWeek == null || form.value.pricePerBabyWeek < 0)
    errors.value.pricePerBabyWeek = 'El precio por bebé/semana es obligatorio'
}

// Weekend: dates + prices required when toggle is on
if (form.value.allowWeekendVisit) {
  if (!form.value.weekendStartDate)
    errors.value.weekendStartDate = 'La fecha de inicio del fin de semana es obligatoria'
  if (!form.value.weekendEndDate)
    errors.value.weekendEndDate = 'La fecha de fin del fin de semana es obligatoria'
  if (form.value.weekendStartDate && form.value.weekendEndDate
      && form.value.weekendEndDate <= form.value.weekendStartDate)
    errors.value.weekendEndDate = 'La fecha de fin debe ser posterior a la de inicio'
  if (form.value.pricePerAdultWeekend == null || form.value.pricePerAdultWeekend < 0)
    errors.value.pricePerAdultWeekend = 'El precio por adulto/fds es obligatorio'
  if (form.value.pricePerChildWeekend == null || form.value.pricePerChildWeekend < 0)
    errors.value.pricePerChildWeekend = 'El precio por niño/fds es obligatorio'
  if (form.value.pricePerBabyWeekend == null || form.value.pricePerBabyWeekend < 0)
    errors.value.pricePerBabyWeekend = 'El precio por bebé/fds es obligatorio'
  if (form.value.maxWeekendCapacity != null && form.value.maxWeekendCapacity <= 0)
    errors.value.maxWeekendCapacity = 'La capacidad debe ser mayor a 0'
}
```

#### Submit payload — add to `handleSubmit`

```typescript
const result = await proposeEdition({
  // ... existing fields ...
  // Partial attendance:
  halfDate: form.value.allowPartialAttendance && form.value.halfDate
    ? toISODate(form.value.halfDate) : null,
  pricePerAdultWeek: form.value.allowPartialAttendance
    ? form.value.pricePerAdultWeek : null,
  pricePerChildWeek: form.value.allowPartialAttendance
    ? form.value.pricePerChildWeek : null,
  pricePerBabyWeek: form.value.allowPartialAttendance
    ? form.value.pricePerBabyWeek : null,
  // Weekend visit:
  weekendStartDate: form.value.allowWeekendVisit && form.value.weekendStartDate
    ? toISODate(form.value.weekendStartDate) : null,
  weekendEndDate: form.value.allowWeekendVisit && form.value.weekendEndDate
    ? toISODate(form.value.weekendEndDate) : null,
  pricePerAdultWeekend: form.value.allowWeekendVisit
    ? form.value.pricePerAdultWeekend : null,
  pricePerChildWeekend: form.value.allowWeekendVisit
    ? form.value.pricePerChildWeekend : null,
  pricePerBabyWeekend: form.value.allowWeekendVisit
    ? form.value.pricePerBabyWeekend : null,
  maxWeekendCapacity: form.value.allowWeekendVisit
    ? (form.value.maxWeekendCapacity || null) : null,
})
```

#### Template — add two sections after capacity, before proposal reason

Use `ToggleSwitch` + `v-if` pattern identical to the existing custom age ranges section in `CampEditionUpdateDialog.vue`. Import `ToggleSwitch` (not currently imported in Propose dialog).

---

### 2. `CampEditionUpdateDialog.vue`

#### FormModel interface — add fields

```typescript
// Add to FormModel interface:
allowPartialAttendance: boolean
halfDate: Date | null
pricePerAdultWeek: number | null
pricePerChildWeek: number | null
pricePerBabyWeek: number | null
allowWeekendVisit: boolean
weekendStartDate: Date | null
weekendEndDate: Date | null
pricePerAdultWeekend: number | null
pricePerChildWeekend: number | null
pricePerBabyWeekend: number | null
maxWeekendCapacity: number | null
```

#### `initializeForm()` — populate from edition

```typescript
// Derive toggle state from existing data:
form.allowPartialAttendance = props.edition.pricePerAdultWeek != null
form.halfDate = props.edition.halfDate ? new Date(props.edition.halfDate) : null
form.pricePerAdultWeek = props.edition.pricePerAdultWeek ?? null
form.pricePerChildWeek = props.edition.pricePerChildWeek ?? null
form.pricePerBabyWeek = props.edition.pricePerBabyWeek ?? null

form.allowWeekendVisit = props.edition.weekendStartDate != null
form.weekendStartDate = props.edition.weekendStartDate ? new Date(props.edition.weekendStartDate) : null
form.weekendEndDate = props.edition.weekendEndDate ? new Date(props.edition.weekendEndDate) : null
form.pricePerAdultWeekend = props.edition.pricePerAdultWeekend ?? null
form.pricePerChildWeekend = props.edition.pricePerChildWeekend ?? null
form.pricePerBabyWeekend = props.edition.pricePerBabyWeekend ?? null
form.maxWeekendCapacity = props.edition.maxWeekendCapacity ?? null
```

#### Validation — same rules as Propose dialog

#### Submit payload — add fields to request object

```typescript
const request: UpdateCampEditionRequest = {
  // ... existing fields ...
  halfDate: form.allowPartialAttendance && form.halfDate
    ? formatDateToIso(form.halfDate) : null,
  pricePerAdultWeek: form.allowPartialAttendance ? form.pricePerAdultWeek : null,
  pricePerChildWeek: form.allowPartialAttendance ? form.pricePerChildWeek : null,
  pricePerBabyWeek: form.allowPartialAttendance ? form.pricePerBabyWeek : null,
  weekendStartDate: form.allowWeekendVisit && form.weekendStartDate
    ? formatDateToIso(form.weekendStartDate) : null,
  weekendEndDate: form.allowWeekendVisit && form.weekendEndDate
    ? formatDateToIso(form.weekendEndDate) : null,
  pricePerAdultWeekend: form.allowWeekendVisit ? form.pricePerAdultWeekend : null,
  pricePerChildWeekend: form.allowWeekendVisit ? form.pricePerChildWeekend : null,
  pricePerBabyWeekend: form.allowWeekendVisit ? form.pricePerBabyWeekend : null,
  maxWeekendCapacity: form.allowWeekendVisit ? (form.maxWeekendCapacity || null) : null,
}
```

#### `isOpenEdition` guard

When edition is Open, disable the attendance toggles and all their sub-fields (same pattern as existing pricing fields).

#### Template — add two sections after capacity, before notes

Same layout as Propose dialog.

---

## Validation Rules Summary

| Rule | Where enforced |
|------|----------------|
| Week prices: all three required when toggle ON | Frontend validate() |
| Week prices ≥ 0 | Frontend validate() |
| Weekend dates: both required when toggle ON | Frontend validate() |
| Weekend end > start | Frontend validate() |
| Weekend prices: all three required when toggle ON | Frontend validate() |
| Weekend prices ≥ 0 | Frontend validate() |
| Weekend duration ≤ 3 days | Backend validator (already implemented) |
| Weekend dates within camp date range | Backend validator (already implemented) |
| `maxWeekendCapacity` > 0 if provided | Frontend validate() + backend |

Frontend validation provides immediate UX feedback. Backend validators are the source of truth.

---

## Date serialization note

`halfDate`, `weekendStartDate`, and `weekendEndDate` are `DateOnly` on the backend. The existing `toISODate()` / `formatDateToIso()` helper (already in both dialogs) produces `YYYY-MM-DD` which is the correct format for `DateOnly` JSON deserialization in .NET.

---

## Files to Modify

```
frontend/src/components/camps/CampEditionProposeDialog.vue
  ← Import ToggleSwitch
  ← Add 12 form fields (2 toggles + 10 data fields)
  ← Add validation rules for both sections
  ← Add fields to proposeEdition() payload
  ← Add template sections (partial attendance + weekend visit)

frontend/src/components/camps/CampEditionUpdateDialog.vue
  ← Add 12 fields to FormModel interface and reactive form
  ← Populate from edition in initializeForm()
  ← Add validation rules for both sections
  ← Add fields to UpdateCampEditionRequest payload
  ← Add template sections (partial attendance + weekend visit)
  ← Disable new fields when isOpenEdition
```

No backend changes required. No new files needed. Types and API payloads are already defined.

---

## Test Coverage

### `CampEditionProposeDialog.test.ts` (new or extend)

- `should show partial attendance fields when toggle is enabled`
- `should hide partial attendance fields when toggle is disabled`
- `should validate week prices are required when toggle is ON`
- `should send null week prices when toggle is OFF`
- `should show weekend visit fields when toggle is enabled`
- `should validate weekend dates and prices when toggle is ON`
- `should send null weekend fields when toggle is OFF`

### `CampEditionUpdateDialog.test.ts` (new or extend)

- `should initialize toggles from existing edition data`
- `should set allowPartialAttendance=true when edition has pricePerAdultWeek`
- `should set allowWeekendVisit=true when edition has weekendStartDate`
- `should disable attendance toggles when edition is Open`
- `should include attendance fields in update request`
- `should send null for attendance fields when toggles are OFF`

---

## Document Control

- **Feature**: `feat-edition-attendance-config-admin`
- **Extends**: `feat-registration-attendance-period` (enriched spec v1.2)
- **Version**: 1.0
- **Date**: 2026-02-28
- **Status**: Ready for Frontend Implementation
- **Priority**: Required — without this, attendance period features are unusable
