# User Story: Registrations Admin Panel for Board & Admin Roles

## Summary

As a **Board** or **Admin** user, I need a registration management panel **scoped to a specific camp edition** where I can view all registrations for that edition in a filterable, paginated table, see summary totals, and access/edit individual registration details. Edited registrations revert to "Draft" status until the family representative re-confirms them.

The panel always operates in the context of a selected camp edition — there is no global "all registrations" view.

---

## Acceptance Criteria

### AC1 - Camp Edition Selection & Registration List View

- A new **"Inscripciones"** tab is added to the existing Admin panel (`AdminPage.vue`) — visible to both Board and Admin roles.
- At the top, a **camp edition selector** (`Select`) lists all camp editions (name + year), defaulting to the current/latest open edition.
- Selecting a camp edition loads its registrations into a `DataTable` with server-side pagination (page size: 20).
- The camp edition is **required** — no data is shown until one is selected.
- **Columns:**

  | Column | Source | Notes |
  |---|---|---|
  | Family Unit | `registration.familyUnit.name` | Family display name |
  | Representative | `user.firstName + user.lastName` | Name of the `registeredByUser` |
  | Email | `user.email` | Email of the representative |
  | Status | `registration.status` | Displayed as a colored PrimeVue `Tag` (Pending=warn, Confirmed=success, Cancelled=danger, Draft=info) |
  | Members | Count of `registration.members` | Number of attending members |
  | Total Amount | `registration.totalAmount` | Formatted as currency (EUR) |
  | Amount Paid | `registration.amountPaid` | Sum of completed payments, formatted as currency |
  | Amount Remaining | `registration.amountRemaining` | `totalAmount - amountPaid`, formatted as currency |
  | Created | `registration.createdAt` | Formatted date (es-ES locale) |

- **Totals row** at the bottom showing:
  - Total registrations count
  - Total members count (sum)
  - Total amount (sum)
  - Total paid (sum)
  - Total remaining (sum)

### AC2 - Filters

- **Family search**: `InputText` with debounce (300ms) that filters by family unit name or representative name (server-side).
- **Status filter**: `Dropdown` / `Select` with options: All, Pending, Confirmed, Cancelled, Draft.
- Filters are applied server-side. Changing any filter resets pagination to page 1.

### AC3 - Registration Detail (Board/Admin Edit)

- Clicking a row navigates to the existing registration detail page (`/registrations/:id`).
- Board and Admin users can see and **edit** the registration detail (members, extras, accommodation preferences, notes).
- When a Board/Admin user saves changes to a registration, the status is automatically set to **`Draft`** (new status).
- The family representative will see the registration marked as "Draft" and must re-confirm it.
- A visual banner/message on the detail page indicates: _"This registration was modified by an administrator and requires re-confirmation by the family representative."_

### AC4 - Draft Status

- A new `Draft` value is added to the `RegistrationStatus` enum.
- Draft registrations behave like Pending: they are not counted toward confirmed capacity, and the representative can edit and re-confirm them.
- Only Board/Admin users can transition a registration to Draft (by editing it).
- The representative can transition Draft -> Confirmed (by re-confirming).

---

## Technical Implementation

### Backend

#### 1. New Endpoint: `GET /api/camp-editions/{campEditionId}/registrations`

**Authorization:** `RequireRole("Admin", "Board")`

The camp edition is a **required path parameter** — the panel always operates in the context of a single edition.

**Path Parameters:**

| Parameter | Type | Description |
|---|---|---|
| `campEditionId` | Guid | **Required.** The camp edition to list registrations for |

**Query Parameters:**

| Parameter | Type | Default | Description |
|---|---|---|---|
| `page` | int | 1 | Page number |
| `pageSize` | int | 20 | Items per page (max 100) |
| `search` | string? | null | Search by family unit name or representative name (ILIKE) |
| `status` | string? | null | Filter by RegistrationStatus (Pending, Confirmed, Cancelled, Draft) |

**Response DTO:** `AdminRegistrationListResponse`

```csharp
public record AdminRegistrationListResponse(
    List<AdminRegistrationListItem> Items,
    int TotalCount,
    AdminRegistrationTotals Totals
);

public record AdminRegistrationListItem(
    Guid Id,
    FamilyUnitSummary FamilyUnit,
    RepresentativeSummary Representative,
    string Status,
    int MemberCount,
    decimal TotalAmount,
    decimal AmountPaid,
    decimal AmountRemaining,
    DateTime CreatedAt
);

public record RepresentativeSummary(
    Guid Id,
    string FirstName,
    string LastName,
    string Email
);

public record AdminRegistrationTotals(
    int TotalRegistrations,
    int TotalMembers,
    decimal TotalAmount,
    decimal TotalPaid,
    decimal TotalRemaining
);
```

**Files to modify/create:**

- `src/Abuvi.API/Features/Registrations/RegistrationsEndpoints.cs` — Add new endpoint mapping
- `src/Abuvi.API/Features/Registrations/RegistrationsModels.cs` — Add DTOs above
- `src/Abuvi.API/Features/Registrations/RegistrationsService.cs` — Add `GetAdminListAsync` method
- `src/Abuvi.API/Features/Registrations/RegistrationsRepository.cs` — Add query with joins, filtering, pagination, and totals
- `src/Abuvi.API/Features/Registrations/IRegistrationsRepository.cs` — Add interface method

#### 2. Update Endpoint: `PUT /api/registrations/{id}/admin-edit`

**Authorization:** `RequireRole("Admin", "Board")`

Allows Board/Admin to update members, extras, notes, specialNeeds, and campatesPreference. Upon save, sets `status = Draft` and stores `adminModifiedAt` timestamp.

**Request DTO:** Reuse existing update DTOs (members, extras, preferences) combined into a single request or call existing endpoints sequentially.

**Files to modify:**

- `src/Abuvi.API/Features/Registrations/RegistrationsEndpoints.cs` — Add admin edit endpoint
- `src/Abuvi.API/Features/Registrations/RegistrationsService.cs` — Add `AdminUpdateAsync` method that sets status to Draft
- `src/Abuvi.API/Features/Registrations/RegistrationsRepository.cs` — Add update method

#### 3. Update Registration Status Enum

**File:** `src/Abuvi.API/Features/Registrations/RegistrationsModels.cs`

```csharp
public enum RegistrationStatus
{
    Pending,
    Confirmed,
    Cancelled,
    Draft  // NEW: Set when Board/Admin edits a confirmed registration
}
```

#### 4. EF Migration

- Add `Draft` to RegistrationStatus enum in PostgreSQL
- No new columns needed (status already exists)

**File:** New migration in `src/Abuvi.API/Migrations/`

#### 5. Update Existing Registration Detail Endpoint

**File:** `src/Abuvi.API/Features/Registrations/RegistrationsEndpoints.cs`

The existing `GET /api/registrations/{id}` already allows Board/Admin access. Ensure the response includes a flag `isAdminModified: boolean` so the frontend can show the re-confirmation banner.

---

### Frontend

#### 1. New Component: `RegistrationsAdminPanel.vue`

**Location:** `frontend/src/components/admin/RegistrationsAdminPanel.vue`

**Pattern:** Follow `FamilyUnitsAdminPanel.vue` and `UsersAdminPanel.vue` patterns.

**Structure:**

```
<template>
  <!-- Camp edition selector (required context) -->
  <div class="flex gap-3 mb-4 flex-wrap items-end">
    <Select v-model="selectedEditionId" :options="campEditionOptions" placeholder="Select camp edition..."
      optionLabel="label" optionValue="value" class="w-80" />
  </div>

  <!-- Filters row (only visible when edition is selected) -->
  <div v-if="selectedEditionId" class="flex gap-3 mb-4 flex-wrap">
    <InputText v-model="search" placeholder="Search family or representative..." />
    <Select v-model="statusFilter" :options="statusOptions" placeholder="Status" />
  </div>

  <!-- DataTable with lazy pagination -->
  <DataTable :value="registrations" lazy paginator :rows="20" :totalRecords="totalCount"
    @page="onPage" @row-click="onRowClick">
    <Column field="familyUnit.name" header="Family Unit" />
    <Column header="Representative">
      <template #body="{ data }">{{ data.representative.firstName }} {{ data.representative.lastName }}</template>
    </Column>
    <Column field="representative.email" header="Email" />
    <Column field="status" header="Status">
      <template #body="{ data }"><Tag :value="data.status" :severity="statusSeverity(data.status)" /></template>
    </Column>
    <Column field="memberCount" header="Members" />
    <Column header="Total"><template #body="{ data }">{{ formatCurrency(data.totalAmount) }}</template></Column>
    <Column header="Paid"><template #body="{ data }">{{ formatCurrency(data.amountPaid) }}</template></Column>
    <Column header="Remaining"><template #body="{ data }">{{ formatCurrency(data.amountRemaining) }}</template></Column>
    <Column field="createdAt" header="Created">
      <template #body="{ data }">{{ formatDate(data.createdAt) }}</template>
    </Column>

    <!-- Footer totals row -->
    <ColumnGroup type="footer">...</ColumnGroup>
  </DataTable>
</template>
```

**Files to create:**

- `frontend/src/components/admin/RegistrationsAdminPanel.vue`

#### 2. Update Admin Page

**File:** `frontend/src/views/AdminPage.vue`

- Add new tab "Inscripciones" (index it before or after existing tabs, logically near "Unidades Familiares")
- Import and render `RegistrationsAdminPanel` component

#### 3. New Composable: `useAdminRegistrations.ts`

**Location:** `frontend/src/composables/useAdminRegistrations.ts`

```typescript
export function useAdminRegistrations() {
  const registrations = ref<AdminRegistrationListItem[]>([])
  const totals = ref<AdminRegistrationTotals | null>(null)
  const totalCount = ref(0)
  const loading = ref(false)
  const error = ref<string | null>(null)

  const fetchAdminRegistrations = async (
    campEditionId: string,
    params: {
      page?: number
      pageSize?: number
      search?: string
      status?: string
    }
  ) => { /* GET /api/camp-editions/{campEditionId}/registrations */ }

  return { registrations, totals, totalCount, loading, error, fetchAdminRegistrations }
}
```

**Files to create:**

- `frontend/src/composables/useAdminRegistrations.ts`

#### 4. New TypeScript Types

**File:** `frontend/src/types/registration.ts` (or existing types file)

```typescript
export interface AdminRegistrationListItem {
  id: string
  familyUnit: { id: string; name: string }
  representative: { id: string; firstName: string; lastName: string; email: string }
  status: 'Pending' | 'Confirmed' | 'Cancelled' | 'Draft'
  memberCount: number
  totalAmount: number
  amountPaid: number
  amountRemaining: number
  createdAt: string
}

export interface AdminRegistrationTotals {
  totalRegistrations: number
  totalMembers: number
  totalAmount: number
  totalPaid: number
  totalRemaining: number
}

export interface AdminRegistrationListResponse {
  items: AdminRegistrationListItem[]
  totalCount: number
  totals: AdminRegistrationTotals
}
```

#### 5. Update Registration Detail Page

**File:** `frontend/src/views/registrations/RegistrationDetailPage.vue`

- If user is Board/Admin, show edit controls (existing form fields become editable).
- Show a warning `Message` banner when registration `status === 'Draft'`: _"This registration was modified by an administrator. The family representative must re-confirm it."_
- Board/Admin save button calls `PUT /api/registrations/{id}/admin-edit`.

---

### Database Changes

- **Migration:** Add `Draft` to `RegistrationStatus` PostgreSQL enum type.
- No new tables or columns required.

---

## Non-Functional Requirements

### Security

- All new endpoints require JWT authentication with Admin or Board role.
- The admin list endpoint must NOT expose sensitive fields (medicalNotes, allergies) — only boolean flags.
- Input validation on all query parameters (page > 0, pageSize 1-100, valid enum for status).

### Performance

- Server-side pagination and filtering (no client-side filtering of large datasets).
- Database query should use indexed columns for filtering (`status`, `campEditionId`, `familyUnitId`).
- Consider adding a composite index on `registrations(camp_edition_id, status)` if not present.

### Accessibility

- DataTable must be keyboard-navigable.
- Status tags must have sufficient color contrast.
- Filter inputs must have proper `aria-label` attributes.

---

## Definition of Done

1. [ ] `GET /api/camp-editions/{campEditionId}/registrations` endpoint implemented with pagination, filters, and totals
2. [ ] `Draft` status added to RegistrationStatus enum with EF migration
3. [ ] `PUT /api/registrations/{id}/admin-edit` endpoint sets status to Draft on save
4. [ ] Admin panel "Inscripciones" tab renders `RegistrationsAdminPanel` component
5. [ ] DataTable shows all required columns with proper formatting
6. [ ] Totals row displays aggregated values
7. [ ] Camp edition selector loads editions and defaults to latest open edition
8. [ ] Filters (search, status) work with server-side filtering
9. [ ] Clicking a row navigates to registration detail
10. [ ] Board/Admin can edit registration detail; save sets status to Draft
11. [ ] Draft banner displayed on modified registrations
12. [ ] Representative can re-confirm a Draft registration
13. [ ] Unit tests for new service methods and repository queries
14. [ ] Component tests for `RegistrationsAdminPanel`
15. [ ] API endpoints documentation updated in `ai-specs/specs/api-endpoints.md`
16. [ ] Data model documentation updated with Draft status in `ai-specs/specs/data-model.md`
