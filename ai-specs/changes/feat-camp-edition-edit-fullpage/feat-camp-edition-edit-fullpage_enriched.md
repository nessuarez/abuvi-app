# Replace CampEdition Edit Modal with Full Edit Page

## Problem Description

The current CampEdition editing experience uses a modal dialog (`CampEditionUpdateDialog.vue`) that has become inadequate for the number of fields it manages (~25+ fields across 7 sections). The modal creates several UX and functional issues:

1. **Modal is too cramped**: The `max-w-2xl` dialog doesn't provide enough space for all form sections (dates, pricing, partial attendance, weekend visits, custom age ranges, payment deadlines, notes, description).
2. **Date fields don't load correctly**: The `initializeForm()` function uses `new Date(props.edition.startDate)` for start/end dates, which can produce incorrect results due to timezone parsing of ISO date strings (e.g., `"2026-07-15"` parsed as UTC midnight can shift to the previous day in negative UTC offsets). Other date fields correctly use `parseDateLocal()`.
3. **Notes field loses existing data**: `form.notes = ''` (line 104) always initializes notes as empty instead of loading `props.edition.notes`.
4. **Validation blocks Open edition edits**: The `validate()` function always requires `startDate` and `endDate`, even when the edition is Open and those fields are disabled. If dates fail to load (due to bug #2), the user cannot save even the allowed fields (notes, description, maxCapacity, payment deadlines).
5. **Edition data comes from list endpoint**: The edit dialog receives the `CampEdition` object from the `allEditions` list (fetched via `fetchAllEditions`), not from a dedicated `getEditionById` call. If the list response omits or truncates fields, the form won't populate correctly.

## Solution

Replace the modal with a dedicated full-page edit view at `/camps/editions/:id/edit`. This provides:

- Ample space for all form sections with clear visual separation
- Proper data loading via `getEditionById()` API call on page mount
- Better UX with collapsible/organized sections
- Consistent navigation pattern with the rest of the app

## Scope

### Frontend Changes

#### 1. Create new page: `frontend/src/views/camps/CampEditionEditPage.vue`

A full-page form view that replaces the modal. Key requirements:

- **Route**: `/camps/editions/:id/edit` (name: `camp-edition-edit`)
- **Auth**: `requiresAuth: true, requiresBoard: true`
- **Data loading**: On `onMounted`, call `getEditionById(route.params.id)` to fetch the full edition data before populating the form
- **Layout**: Use `Container` component, with form sections organized in cards (like the detail page)
- **Form sections** (each in its own card/panel):
  1. **General Information**: Start date, end date, max capacity
  2. **Pricing**: Adult, child, baby prices
  3. **Partial Attendance** (collapsible via toggle): Half date, week prices
  4. **Weekend Visits** (collapsible via toggle): Weekend dates, weekend prices, weekend capacity
  5. **Custom Age Ranges** (collapsible via toggle): Baby max, child min/max, adult min ages
  6. **Payment Deadlines**: First and second payment deadline dates
  7. **Additional Info**: Notes (textarea, max 2000), description (textarea)
- **Status-aware disabling**: When `edition.status === 'Open'`, disable dates, all pricing fields, partial attendance, weekend visit, and custom age ranges. Show info `Message` explaining restrictions. Allow editing: maxCapacity, notes, description, payment deadlines.
- **Buttons**: "Cancel" (navigates back) and "Save" (submits form)
- **Success**: Show toast notification and navigate to edition detail page
- **Error**: Show inline `Message` with error text

#### 2. Fix date initialization bugs

In the new page's `initializeForm()`:

- Use `parseDateLocal()` consistently for ALL date fields (startDate, endDate, halfDate, weekendStartDate, weekendEndDate, firstPaymentDeadline, secondPaymentDeadline)
- Initialize `form.notes` from `edition.notes ?? ''` (not empty string)

#### 3. Fix validation for Open editions

The `validate()` function must skip validation of disabled/locked fields when the edition is Open:

```typescript
const validate = (): boolean => {
  errors.value = {}
  if (!isOpenEdition.value) {
    // Only validate dates/prices when they can be edited
    if (!form.startDate) errors.value.startDate = 'La fecha de inicio es obligatoria'
    if (!form.endDate) errors.value.endDate = 'La fecha de fin es obligatoria'
    if (form.endDate && form.startDate && form.endDate <= form.startDate) {
      errors.value.endDate = 'La fecha de fin debe ser posterior a la fecha de inicio'
    }
    // ... price and partial/weekend validations only when not Open
  }
  // Always validate: maxCapacity, notes (these are always editable)
  if (form.maxCapacity !== null && form.maxCapacity <= 0) {
    errors.value.maxCapacity = 'La capacidad máxima debe ser mayor a 0'
  }
  if (form.notes && form.notes.length > 2000) {
    errors.value.notes = 'Las notas no deben superar los 2000 caracteres'
  }
  return Object.keys(errors.value).length === 0
}
```

#### 4. Update `handleSave` for Open editions

When the edition is Open, the request should send the **original** values for restricted fields (dates, prices) to pass backend validation, and only the user-modified values for allowed fields:

```typescript
const request: UpdateCampEditionRequest = {
  // For Open editions, use original values for restricted fields
  startDate: isOpenEdition.value ? edition.value!.startDate : formatDateToIso(form.startDate),
  endDate: isOpenEdition.value ? edition.value!.endDate : formatDateToIso(form.endDate),
  pricePerAdult: isOpenEdition.value ? edition.value!.pricePerAdult : form.pricePerAdult,
  // ... etc for all restricted fields
  // Always use form values for allowed fields
  maxCapacity: form.maxCapacity ?? undefined,
  notes: form.notes || undefined,
  description: form.description || undefined,
  firstPaymentDeadline: ...,
  secondPaymentDeadline: ...
}
```

#### 5. Add route in `frontend/src/router/index.ts`

```typescript
{
  path: "/camps/editions/:id/edit",
  name: "camp-edition-edit",
  component: () => import("@/views/camps/CampEditionEditPage.vue"),
  meta: { title: "ABUVI | Editar Edición", requiresAuth: true, requiresBoard: true }
}
```

#### 6. Update navigation references

- **`CampEditionsPage.vue`**: Change "Edit" button to navigate via `router.push({ name: 'camp-edition-edit', params: { id: data.id } })` instead of opening the modal dialog
- **`CampEditionDetailPage.vue`**: Add an "Edit" button in the header area (visible only for Board/Admin) that navigates to the edit page
- Remove `CampEditionUpdateDialog` import and usage from `CampEditionsPage.vue`

#### 7. Remove or deprecate `CampEditionUpdateDialog.vue`

Once the full page is in place, the modal component (`frontend/src/components/camps/CampEditionUpdateDialog.vue`) can be deleted as it will no longer be used.

### Backend Changes

**No backend changes required.** The existing `PUT /api/camps/editions/{id}` endpoint and its validation/service logic are correct. The backend already:

- Validates required fields (StartDate, EndDate)
- Enforces Open edition restrictions (dates and prices must match current values)
- Rejects modifications to Closed/Completed editions

The fix is entirely on the frontend side: properly loading data and sending correct values.

## Files to Modify

| File | Action | Description |
|------|--------|-------------|
| `frontend/src/views/camps/CampEditionEditPage.vue` | **CREATE** | New full-page edit form |
| `frontend/src/router/index.ts` | MODIFY | Add `/camps/editions/:id/edit` route |
| `frontend/src/views/camps/CampEditionsPage.vue` | MODIFY | Replace modal open with router navigation |
| `frontend/src/views/camps/CampEditionDetailPage.vue` | MODIFY | Add "Edit" button for Board/Admin |
| `frontend/src/components/camps/CampEditionUpdateDialog.vue` | **DELETE** | No longer needed |

## Acceptance Criteria

- [ ] New edit page at `/camps/editions/:id/edit` loads and displays all CampEdition fields correctly
- [ ] All date fields display the correct values (no timezone shift issues)
- [ ] Notes field loads existing notes content on edit
- [ ] When edition is Open: dates and pricing fields are disabled, but notes, description, maxCapacity, and payment deadlines can be edited and saved successfully
- [ ] When edition is Draft/Proposed: all fields can be edited
- [ ] Closed/Completed editions: edit button is disabled (existing behavior preserved)
- [ ] Form validation errors are displayed inline next to their respective fields
- [ ] Successful save shows toast and redirects to edition detail page
- [ ] API errors are displayed in an inline Message component
- [ ] "Cancel" button navigates back without saving
- [ ] Edit button is visible from both the editions list and the edition detail page
- [ ] The old modal dialog component is removed
- [ ] Page is accessible only to Board and Admin users

## Non-Functional Requirements

- **Performance**: Data should load via a single `GET /api/camps/editions/{id}` call on mount
- **Security**: Route must enforce `requiresBoard: true` in meta (existing pattern)
- **UX**: Form sections should be visually separated in cards for readability; disabled fields should be clearly grayed out with an explanatory info message
- **Testing**: Update any existing Cypress E2E tests that interact with the edition edit modal to use the new page flow instead
