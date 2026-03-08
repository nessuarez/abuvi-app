# Fix: "Ver detalles" Button Not Working on "Mis Inscripciones" Page

## User Story

**As a** registered user who has completed a camp registration,
**I want** to click the "Ver detalles" button on my registration card in the "Mis Inscripciones" page,
**So that** I can view the full details of my registration and eventually modify it.

## Problem Description

Users who have completed a registration navigate to the "Mis Inscripciones" page (`/registrations`) and click the "Ver detalles" button on a registration card. The button does not work as expected — the user is not navigated to the registration detail page, or the detail page fails to display correctly.

## Root Cause Analysis

### Primary Issue: Frontend/Backend Type Mismatch

The list endpoint `GET /api/registrations` returns `RegistrationListResponse` (a lightweight DTO), but the frontend composable `useRegistrations.ts` deserializes the response as `RegistrationResponse[]` (the full detail DTO). While the button click and navigation should still function (since both DTOs include `id`), this type mismatch causes several downstream issues:

| Field | `RegistrationListResponse` (backend) | `RegistrationResponse` (frontend type) | Impact |
|---|---|---|---|
| `pricing` | Not present (`totalAmount` is a flat field) | Expected as `PricingBreakdown` object | Card shows no total price (guarded by `v-if`) |
| `location` | Not in `RegistrationCampEditionSummary` | Expected as `string \| null` | Location never displayed on cards |
| `representativeUserId` | Not in `RegistrationFamilyUnitSummary` | Expected in frontend type | `isRepresentative` is always `false` on detail page |
| `notes`, `payments`, `specialNeeds`, `campatesPreference` | Not present in list DTO | Expected in frontend type | Unused on list view, but creates confusing type contract |

### Secondary Issue: Missing `representativeUserId` in Backend DTO

The backend `RegistrationFamilyUnitSummary` record is defined as:

```csharp
public record RegistrationFamilyUnitSummary(Guid Id, string Name);
```

But the frontend type expects:

```typescript
export interface RegistrationFamilyUnitSummary {
  id: string
  name: string
  representativeUserId: string  // <-- NOT sent by backend
}
```

This means on `RegistrationDetailPage.vue`, `isRepresentative` (line 53-55) is always `false`, which hides the "Cancelar inscripcion" button for all users, including legitimate representatives.

### Investigation Needed

The exact failure mode of the "Ver detalles" button needs to be reproduced to determine which of these scenarios applies:

1. **Button click does nothing** — Possible JavaScript error in the console preventing navigation
2. **Navigation occurs but detail page shows error** — API call to `GET /api/registrations/{id}` fails (403, 404, or server error)
3. **Navigation occurs but page is blank/broken** — Detail page renders but data is missing or malformed
4. **Button is visually unresponsive** — CSS/layout issue covering the button

## Affected Files

### Frontend

| File | Purpose |
|---|---|
| `frontend/src/views/registrations/RegistrationsPage.vue` | Main list page with `navigateToDetail()` |
| `frontend/src/components/registrations/RegistrationCard.vue` | Card component with "Ver detalles" button |
| `frontend/src/views/registrations/RegistrationDetailPage.vue` | Detail page loaded after click |
| `frontend/src/composables/useRegistrations.ts` | API calls (`fetchMyRegistrations`, `getRegistrationById`) |
| `frontend/src/types/registration.ts` | TypeScript types (needs separate list type) |

### Backend

| File | Purpose |
|---|---|
| `src/Abuvi.API/Features/Registrations/RegistrationsModels.cs` | `RegistrationListResponse`, `RegistrationFamilyUnitSummary` DTOs |
| `src/Abuvi.API/Features/Registrations/RegistrationsEndpoints.cs` | `GetMyRegistrations`, `GetRegistrationById` endpoints |

### Routes

| Route | File |
|---|---|
| `/registrations` | `frontend/src/router/index.ts:136-142` |
| `/registrations/:id` | `frontend/src/router/index.ts:153-161` |

### API Endpoints

| Method | URL | Handler |
|---|---|---|
| `GET` | `/api/registrations` | `GetMyRegistrations` — returns `List<RegistrationListResponse>` |
| `GET` | `/api/registrations/{id:guid}` | `GetRegistrationById` — returns `RegistrationResponse` |

## Tasks

### Step 1: Reproduce and identify the exact failure

- [ ] Open browser DevTools (Console + Network tabs)
- [ ] Navigate to `/registrations` as an authenticated user with at least one registration
- [ ] Click "Ver detalles" on a registration card
- [ ] Document: Does navigation occur? Is there a JS error? Does the API call succeed?

### Step 2: Fix frontend type for list endpoint

- [ ] Create a new `RegistrationListItem` type in `frontend/src/types/registration.ts` matching `RegistrationListResponse` from the backend:

  ```typescript
  export interface RegistrationListItem {
    id: string
    familyUnit: { id: string; name: string }
    campEdition: RegistrationCampEditionSummary
    status: RegistrationStatus
    totalAmount: number
    amountPaid: number
    amountRemaining: number
    createdAt: string
  }
  ```

- [ ] Update `useRegistrations.ts` to use `RegistrationListItem[]` for `registrations` ref and `fetchMyRegistrations`
- [ ] Update `RegistrationsPage.vue` to use `RegistrationListItem` instead of `RegistrationResponse`
- [ ] Update `RegistrationCard.vue` props to accept `RegistrationListItem`
- [ ] Update `RegistrationCard.vue` template to use `registration.totalAmount` instead of `registration.pricing.totalAmount`

### Step 3: Add `representativeUserId` to backend DTO

- [ ] Update `RegistrationFamilyUnitSummary` in `RegistrationsModels.cs`:

  ```csharp
  public record RegistrationFamilyUnitSummary(Guid Id, string Name, Guid RepresentativeUserId);
  ```

- [ ] Update `ToResponse()` mapping in `RegistrationMappingExtensions` to include `r.FamilyUnit.RepresentativeUserId`
- [ ] Verify the list endpoint mapping also passes `RepresentativeUserId`

### Step 4: Add `location` to `RegistrationCampEditionSummary` (if applicable)

- [ ] Check if `CampEdition` entity has a `Location` property
- [ ] If yes, add `string? Location` to `RegistrationCampEditionSummary` record and update the mapping
- [ ] Update frontend type to match

### Step 5: Fix the specific "Ver detalles" failure (based on Step 1 findings)

- [ ] Apply the targeted fix based on reproduction results
- [ ] If API returns 403: verify authorization logic in `GetRegistrationById`
- [ ] If API returns 404: verify the registration ID is correctly passed as a GUID

### Step 6: Tests

- [ ] Add/update unit test for `RegistrationCard` to verify the `view` event emits the correct ID
- [ ] Add/update unit test for `RegistrationsPage` to verify `navigateToDetail` navigates correctly
- [ ] Verify backend `GetRegistrationById` endpoint returns 200 for an authorized user

### Step 7: Manual verification

- [ ] Create a registration through the wizard
- [ ] Navigate to "Mis Inscripciones"
- [ ] Click "Ver detalles" — verify navigation to `/registrations/{id}`
- [ ] Verify detail page shows all data correctly (pricing, payments, accommodation preferences)
- [ ] Verify "Cancelar inscripcion" button appears for the representative

## Non-Functional Requirements

- **No regressions**: The admin registration panel at `/admin` also navigates to `registration-detail` — ensure it continues to work
- **Type safety**: All new types must be fully typed (no `any`)
- **Error handling**: If `getRegistrationById` fails, the detail page must show a user-friendly error message (already implemented via `error` ref)
