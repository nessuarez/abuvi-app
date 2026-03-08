# Replace CampEdition Edit Modal with Inline Edit on Detail Page

## Problem Description

The current CampEdition editing experience uses a modal dialog (`CampEditionUpdateDialog.vue`) that has become inadequate for the number of fields it manages (~25+ fields across 7 sections). The modal creates several UX and functional issues:

1. **Modal is too cramped**: The `max-w-2xl` dialog doesn't provide enough space for all form sections (dates, pricing, partial attendance, weekend visits, custom age ranges, payment deadlines, notes, description).
2. **Date fields don't load correctly**: The `initializeForm()` function uses `new Date(props.edition.startDate)` for start/end dates, which can produce incorrect results due to timezone parsing of ISO date strings (e.g., `"2026-07-15"` parsed as UTC midnight can shift to the previous day in negative UTC offsets). Other date fields correctly use `parseDateLocal()`.
3. **Notes field loses existing data**: `form.notes = ''` (line 104) always initializes notes as empty instead of loading `props.edition.notes`.
4. **Validation blocks Open edition edits**: The `validate()` function always requires `startDate` and `endDate`, even when the edition is Open and those fields are disabled. If dates fail to load (due to bug #2), the user cannot save even the allowed fields (notes, description, maxCapacity, payment deadlines).
5. **Edition data comes from list endpoint**: The edit dialog receives the `CampEdition` object from the `allEditions` list (fetched via `fetchAllEditions`), not from a dedicated `getEditionById` call. If the list response omits or truncates fields, the form won't populate correctly.
6. **Detail page is incomplete**: The current detail page (`CampEditionDetailPage.vue`) only shows dates, status, basic pricing, description, accommodations, and extras. It does NOT show: partial attendance (week pricing), weekend visits, custom age ranges, payment deadlines, or notes.

## Solution

Adopt an **inline edit pattern** on the existing detail page `/camps/editions/:id` (like `ProfilePage.vue`), organized with a **vertical sidebar navigation** to manage the many sections:

- The detail page shows ALL edition fields in read-only mode (currently many are missing)
- A "Edit" button (Board/Admin only) toggles the page into edit mode, replacing read-only values with form inputs
- "Save" / "Cancel" buttons appear in edit mode
- Data is already properly loaded via `getEditionById()` on mount
- No new route needed — keeps the URL structure simple
- **Vertical section sidebar**: A left sidebar with section buttons (using PrimeVue `Tabs` with `orientation="vertical"`) lets users navigate between field groups without scrolling through a long page. On mobile, the sidebar collapses to horizontal tabs at the top.

This approach:

- Avoids creating a separate edit page/route
- Ensures all data is visible even in read mode (solving the "missing fields" issue)
- Uses the proven inline-edit pattern already established in the codebase
- Organizes 7+ sections via sidebar navigation for easy access
- Fixes all the date, notes, and validation bugs

## Scope

### Frontend Changes

#### 1. Expand `CampEditionDetailPage.vue` with all missing fields (read mode)

The detail page currently shows only: status, year, dates, capacity, pricing (adult/child/baby), description, accommodations, and extras.

**Missing fields to add in read mode:**

- Partial attendance section: `halfDate`, `pricePerAdultWeek`, `pricePerChildWeek`, `pricePerBabyWeek`
- Weekend visits section: `weekendStartDate`, `weekendEndDate`, `pricePerAdultWeekend`, `pricePerChildWeekend`, `pricePerBabyWeekend`, `maxWeekendCapacity`
- Custom age ranges: `useCustomAgeRanges`, `customBabyMaxAge`, `customChildMinAge`, `customChildMaxAge`, `customAdultMinAge`
- Payment deadlines: `firstPaymentDeadline`, `secondPaymentDeadline`
- Notes: `notes`

All fields should be shown conditionally (only when they have values or when their toggle is enabled).

#### 2. Add inline edit mode (like ProfilePage)

- Add `isEditing` ref to toggle between read and edit modes
- Add `FormModel` reactive state (same fields as the current dialog)
- "Edit" button visible for Board/Admin when status is not Closed/Completed
- In edit mode, each section's read-only content is replaced with form inputs
- "Cancel" resets and exits edit mode; "Save" validates, calls API, exits edit mode

#### 3. Fix date initialization bugs

In `initializeForm()`:

- Use `parseDateLocal()` consistently for ALL date fields (startDate, endDate, halfDate, weekendStartDate, weekendEndDate, firstPaymentDeadline, secondPaymentDeadline)
- Initialize `form.notes` from `edition.notes ?? ''` (not empty string)

#### 4. Fix validation for Open editions

The `validate()` function must skip validation of disabled/locked fields when the edition is Open:

- Only validate dates, prices, partial attendance, weekend, and age ranges when edition is NOT Open
- Always validate: maxCapacity, notes (these are always editable)

#### 5. Fix `handleSave` for Open editions

When the edition is Open, the request should send the **original** values for restricted fields (dates, prices) to pass backend validation, and only the user-modified values for allowed fields.

#### 6. Update `CampEditionsPage.vue` navigation

Change the "Edit" button in the editions list to navigate to the detail page instead of opening the modal. Remove modal imports and state.

#### 7. Remove `CampEditionUpdateDialog.vue`

Delete the file — fully replaced by inline editing on the detail page.

### Backend Changes

**No backend changes required.**

## Files to Modify

| File | Action | Description |
|------|--------|-------------|
| `frontend/src/views/camps/CampEditionDetailPage.vue` | **MAJOR MODIFY** | Add all missing read fields + inline edit mode |
| `frontend/src/views/camps/CampEditionsPage.vue` | MODIFY | Remove modal, navigate to detail page for editing |
| `frontend/src/components/camps/CampEditionUpdateDialog.vue` | **DELETE** | No longer needed |

## Acceptance Criteria

- [ ] Detail page shows ALL CampEdition fields in read mode (including partial attendance, weekend, age ranges, deadlines, notes)
- [ ] "Edit" button toggles inline edit mode (Board/Admin only, not for Closed/Completed)
- [ ] All date fields display correct values (no timezone shift issues) — use `parseDateLocal()`
- [ ] Notes field loads existing content on edit
- [ ] When edition is Open: restricted fields are disabled, but notes, description, maxCapacity, and payment deadlines can be edited and saved
- [ ] When edition is Draft/Proposed: all fields can be edited
- [ ] Validation errors displayed inline next to respective fields
- [ ] Successful save shows toast and exits edit mode (stays on detail page)
- [ ] "Cancel" exits edit mode without saving
- [ ] The old modal dialog component is removed
- [ ] Edit works both from the detail page button and from the editions list "edit" icon

## Non-Functional Requirements

- **Performance**: Data loads via existing `getEditionById()` — no additional API calls
- **Security**: Edit button only visible for Board/Admin roles (existing `isBoard` computed)
- **UX**: Vertical sidebar navigation (PrimeVue Tabs vertical) to switch between sections; disabled fields grayed out with explanatory info message for Open editions; on mobile, sidebar collapses to horizontal tabs
- **Testing**: Update Cypress E2E tests for the new inline-edit flow
