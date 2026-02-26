# Enriched User Story: Gestión de membresías familiares en bloque y modelo anual

## Summary

As a **Board/Admin** user, I want to activate memberships for all members of a family unit in a single action, and I want the membership creation to be based on a **year** (not a specific date), since memberships are annual and fees are charged per calendar year.

---

## Problem Context

Two related problems with the current membership system:

### Problem 1 — No bulk activation

Activating memberships for a family requires opening `MembershipDialog` **one member at a time**. For a family of 4, that is 4 separate clicks, 4 separate dialogs, and 4 separate API calls — all with the same start year. This is unnecessarily slow for the most common onboarding scenario: a new family joins and all members need to be activated at once.

### Problem 2 — Date picker is semantically wrong for an annual membership

`MembershipDialog` shows a `<Calendar>` date picker (`dd/mm/yyyy`) for the membership start date. But memberships in this system are **annual**: the fee is generated per calendar year (`MembershipFee.Year: int`), and the date within a year is meaningless for fee calculation. Asking the user for a day and month is confusing and creates inconsistent data (one member starts on `2024-03-15`, another on `2024-07-01`, but both pay the same 2024 annual fee).

The correct model: a membership starts in a **year** (e.g., 2024), represented internally as `{year}-01-01` in the database. The user should select a year, not a date.

---

## Scope

### In scope

1. **Backend: Change `CreateMembershipRequest`** — replace `startDate: DateTime` with `year: int`. Backend normalizes to `{year}-01-01` for storage. The `MembershipResponse` continues to return `startDate` (date string) unchanged.

2. **Backend: New bulk endpoint** — `POST /api/family-units/{familyUnitId}/membership/bulk` — activates membership for all family members who do not yet have one, in a single request.

3. **Frontend: Update `CreateMembershipRequest` type** — change `{ startDate: string }` to `{ year: number }`.

4. **Frontend: Update `MembershipDialog.vue`** — replace the `<Calendar>` date picker with an `<InputNumber>` year picker. Default = current year. Cannot be a future year.

5. **Frontend: New component `BulkMembershipDialog.vue`** — a dialog that shows all members of a family unit, lets the user pick a year, and activates memberships for all members without one in a single action.

6. **Frontend: Add "Activar membresía familiar" button** on `FamilyUnitPage.vue` and `ProfilePage.vue`.

### Out of scope

- Changing the `MembershipResponse` shape (still returns `startDate: string`).
- Changing the deactivate or pay-fee flows.
- Bulk deactivation.
- Bulk fee payment.
- Any changes to `MembershipFee` generation logic (the existing `AnnualFeeGenerationService` is unaffected).

---

## Business Rules

### Rule 1 — Membership year cannot be in the future

When creating a membership (individual or bulk), the selected year must be ≤ current calendar year. A board user cannot pre-register a membership for a future year.

### Rule 2 — Bulk activation skips members who already have a membership

The bulk endpoint (and the bulk dialog) skips members who already have an active membership. It does not fail — it reports how many were activated and how many were skipped.

### Rule 3 — Bulk activation is all-or-nothing per member, not all-or-nothing for the family

If one member fails (e.g., race condition — another user just created a membership for that member), the endpoint continues to the remaining members and returns a partial result. The frontend shows which members were activated and which failed.

### Rule 4 — `startDate` stored as January 1st of the selected year

Internally, `startDate = new DateTime(year, 1, 1, 0, 0, 0, DateTimeKind.Utc)`. This is a normalization done by the backend — the frontend only sends `year: int`.

### Rule 5 — Board/Admin only

Both the individual "Gestionar membresía" flow and the new bulk flow are gated on `auth.isBoard`.

---

## Technical Changes Required

---

### 1. Backend — Update `CreateMembershipRequest`

**File:** `src/Abuvi.API/Features/Memberships/MembershipsModels.cs`

**Change:**

```csharp
// BEFORE
public record CreateMembershipRequest(DateTime StartDate);

// AFTER
public record CreateMembershipRequest(int Year);
```

**File:** `src/Abuvi.API/Features/Memberships/MembershipsService.cs` — `CreateAsync` method:

```csharp
// BEFORE
var membership = new Membership
{
    StartDate = request.StartDate,
    ...
};

// AFTER
var membership = new Membership
{
    StartDate = new DateTime(request.Year, 1, 1, 0, 0, 0, DateTimeKind.Utc),
    ...
};
```

**File:** `src/Abuvi.API/Features/Memberships/CreateMembershipValidator.cs`

```csharp
// BEFORE
RuleFor(x => x.StartDate)
    .NotEmpty().WithMessage("La fecha de inicio es obligatoria")
    .LessThanOrEqualTo(DateTime.UtcNow).WithMessage("La fecha de inicio no puede ser futura");

// AFTER
RuleFor(x => x.Year)
    .GreaterThan(2000).WithMessage("El año de inicio no es válido")
    .LessThanOrEqualTo(DateTime.UtcNow.Year).WithMessage("El año de inicio no puede ser futuro");
```

**Impact:** Breaking change to the `POST /api/family-units/{id}/members/{id}/membership` request body. Since this endpoint was never called from any UI before the `feat-my-memberships-dialog` wiring (implemented today), this is the right moment to make this change.

---

### 2. Backend — New Bulk Endpoint

**New endpoint:** `POST /api/family-units/{familyUnitId}/membership/bulk`

**Request body:**

```json
{
  "year": 2026
}
```

**Response body (200 OK):**

```json
{
  "success": true,
  "data": {
    "activated": 3,
    "skipped": 1,
    "results": [
      { "memberId": "...", "memberName": "Ana García", "status": "Activated" },
      { "memberId": "...", "memberName": "Pedro García", "status": "Activated" },
      { "memberId": "...", "memberName": "Luis García", "status": "Activated" },
      { "memberId": "...", "memberName": "María García", "status": "Skipped", "reason": "Ya tiene membresía activa" }
    ]
  }
}
```

**File:** `src/Abuvi.API/Features/Memberships/MembershipsModels.cs` — new DTOs:

```csharp
public record BulkActivateMembershipRequest(int Year);

public enum BulkMembershipResultStatus { Activated, Skipped, Failed }

public record BulkMembershipMemberResult(
    Guid MemberId,
    string MemberName,
    BulkMembershipResultStatus Status,
    string? Reason = null
);

public record BulkActivateMembershipResponse(
    int Activated,
    int Skipped,
    IReadOnlyList<BulkMembershipMemberResult> Results
);
```

**File:** `src/Abuvi.API/Features/Memberships/MembershipsService.cs` — new method `BulkActivateAsync`:

```csharp
public async Task<BulkActivateMembershipResponse> BulkActivateAsync(
    Guid familyUnitId,
    BulkActivateMembershipRequest request,
    CancellationToken ct)
{
    // Validate family unit exists (use IFamilyUnitsRepository)
    var familyUnit = await familyUnitsRepository.GetByIdAsync(familyUnitId, ct);
    if (familyUnit is null)
        throw new NotFoundException(nameof(FamilyUnit), familyUnitId);

    // Get all members of the family
    var members = await familyUnitsRepository.GetMembersByFamilyUnitIdAsync(familyUnitId, ct);

    var results = new List<BulkMembershipMemberResult>();
    int activated = 0, skipped = 0;

    var startDate = new DateTime(request.Year, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    foreach (var member in members)
    {
        var memberName = $"{member.FirstName} {member.LastName}";

        // Check if already has active membership
        var existing = await repository.GetByFamilyMemberIdAsync(member.Id, ct);
        if (existing is not null)
        {
            results.Add(new(member.Id, memberName, BulkMembershipResultStatus.Skipped, "Ya tiene membresía activa"));
            skipped++;
            continue;
        }

        try
        {
            var membership = new Membership
            {
                Id = Guid.NewGuid(),
                FamilyMemberId = member.Id,
                StartDate = startDate,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            await repository.AddAsync(membership, ct);
            results.Add(new(member.Id, memberName, BulkMembershipResultStatus.Activated));
            activated++;
        }
        catch (Exception ex)
        {
            results.Add(new(member.Id, memberName, BulkMembershipResultStatus.Failed, ex.Message));
        }
    }

    return new BulkActivateMembershipResponse(activated, skipped, results);
}
```

**File:** `src/Abuvi.API/Features/Memberships/MembershipsEndpoints.cs` — new endpoint registration:

```csharp
// In MapMembershipsEndpoints, add:
app.MapPost("/api/family-units/{familyUnitId:guid}/membership/bulk", BulkActivateMemberships)
    .WithName("BulkActivateMemberships")
    .WithTags("Memberships")
    .RequireAuthorization()
    .AddEndpointFilter<ValidationFilter<BulkActivateMembershipRequest>>()
    .Produces<ApiResponse<BulkActivateMembershipResponse>>()
    .Produces(StatusCodes.Status400BadRequest)
    .Produces(StatusCodes.Status404NotFound);
```

**File:** `src/Abuvi.API/Features/Memberships/BulkActivateMembershipValidator.cs` (new file):

```csharp
using FluentValidation;

namespace Abuvi.API.Features.Memberships;

public class BulkActivateMembershipValidator : AbstractValidator<BulkActivateMembershipRequest>
{
    public BulkActivateMembershipValidator()
    {
        RuleFor(x => x.Year)
            .GreaterThan(2000).WithMessage("El año de inicio no es válido")
            .LessThanOrEqualTo(DateTime.UtcNow.Year).WithMessage("El año de inicio no puede ser futuro");
    }
}
```

**Authorization:** The bulk endpoint requires the caller to be Board/Admin or the representative of the family unit. Use the same authorization pattern as other family unit endpoints.

---

### 3. Frontend — Update `CreateMembershipRequest` type

**File:** `frontend/src/types/membership.ts`

```typescript
// BEFORE
export interface CreateMembershipRequest {
  startDate: string // ISO 8601 date string — must not be in the future
}

// AFTER
export interface CreateMembershipRequest {
  year: number // Calendar year — must not be in the future (≤ current year)
}
```

Add new types for the bulk response:

```typescript
export type BulkMembershipResultStatus = 'Activated' | 'Skipped' | 'Failed'

export interface BulkMembershipMemberResult {
  memberId: string
  memberName: string
  status: BulkMembershipResultStatus
  reason?: string | null
}

export interface BulkActivateMembershipResponse {
  activated: number
  skipped: number
  results: BulkMembershipMemberResult[]
}

export interface BulkActivateMembershipRequest {
  year: number
}
```

---

### 4. Frontend — Update `useMemberships.ts`

**File:** `frontend/src/composables/useMemberships.ts`

**Change `createMembership`** — the request body now sends `{ year }` instead of `{ startDate }`:

```typescript
// The signature stays the same (CreateMembershipRequest is still the type),
// but it now contains { year: number } not { startDate: string }
const createMembership = async (
  familyUnitId: string,
  memberId: string,
  request: CreateMembershipRequest,
): Promise<MembershipResponse | null> => { ... }
```

**Add new `bulkActivateMemberships` method:**

```typescript
const bulkActivateMemberships = async (
  familyUnitId: string,
  request: BulkActivateMembershipRequest,
): Promise<BulkActivateMembershipResponse | null> => {
  loading.value = true
  error.value = null
  try {
    const response = await api.post<ApiResponse<BulkActivateMembershipResponse>>(
      `/family-units/${familyUnitId}/membership/bulk`,
      request,
    )
    return response.data.data
  } catch (err: any) {
    error.value = err.response?.data?.error?.message || 'Error al activar las membresías'
    return null
  } finally {
    loading.value = false
  }
}

// Export from the composable return
return {
  ...,
  bulkActivateMemberships,
}
```

---

### 5. Frontend — Update `MembershipDialog.vue`

**File:** `frontend/src/components/memberships/MembershipDialog.vue`

**Replace Calendar date picker with year InputNumber:**

Remove:

```typescript
import Calendar from 'primevue/calendar'
const createStartDate = ref<Date>(new Date())
```

Add:

```typescript
import InputNumber from 'primevue/inputnumber'
const currentYear = new Date().getFullYear()
const createStartYear = ref<number>(currentYear)
```

**Template change** — replace `<Calendar>` with `<InputNumber>`:

```html
<!-- BEFORE -->
<div class="flex flex-col gap-2">
  <label for="membership-start-date" class="font-medium text-sm">
    Fecha de inicio <span class="text-red-500">*</span>
  </label>
  <Calendar
    id="membership-start-date"
    v-model="createStartDate"
    dateFormat="dd/mm/yy"
    :maxDate="new Date()"
    showIcon
    class="w-full"
  />
  <small class="text-gray-500">Debe ser la fecha actual o una fecha pasada.</small>
</div>

<!-- AFTER -->
<div class="flex flex-col gap-2">
  <label for="membership-start-year" class="font-medium text-sm">
    Año de inicio <span class="text-red-500">*</span>
  </label>
  <InputNumber
    id="membership-start-year"
    v-model="createStartYear"
    :min="2000"
    :max="currentYear"
    :use-grouping="false"
    class="w-full"
  />
  <small class="text-gray-500">Año en que el miembro se hizo socio. No puede ser futuro.</small>
</div>
```

**Update `handleCreate`:**

```typescript
// BEFORE
const handleCreate = async () => {
  const dateStr = createStartDate.value.toISOString().split('T')[0]
  const result = await createMembership(props.familyUnitId, props.memberId, { startDate: dateStr })
  ...
}

// AFTER
const handleCreate = async () => {
  const result = await createMembership(props.familyUnitId, props.memberId, { year: createStartYear.value })
  ...
}
```

---

### 6. Frontend — New `BulkMembershipDialog.vue`

**File:** `frontend/src/components/memberships/BulkMembershipDialog.vue` (new)

**Props:**

```typescript
const props = defineProps<{
  visible: boolean
  familyUnitId: string
  members: FamilyMemberResponse[]
  memberData: MemberMembershipData[] // membership status per member — passed from parent (no N+1)
}>()

const emit = defineEmits<{
  'update:visible': [value: boolean]
  done: [] // emitted when the activation is complete — parent should reload
}>()
```

**State:**

```typescript
const currentYear = new Date().getFullYear()
const selectedYear = ref<number>(currentYear)
const result = ref<BulkActivateMembershipResponse | null>(null)
const { loading, error, bulkActivateMemberships } = useMemberships()
```

**Computed — members without membership:**

```typescript
const membersWithoutMembership = computed(() =>
  props.memberData.filter((d) => !d.membershipId)
)
const membersAlreadyActive = computed(() =>
  props.memberData.filter((d) => d.membershipId && d.isActiveMembership)
)
```

**Handler:**

```typescript
const handleBulkActivate = async () => {
  result.value = null
  const res = await bulkActivateMemberships(props.familyUnitId, { year: selectedYear.value })
  if (res) {
    result.value = res
    if (res.activated > 0) {
      toast.add({ severity: 'success', summary: 'Éxito', detail: `${res.activated} membresía(s) activada(s)`, life: 3000 })
    }
  } else {
    toast.add({ severity: 'error', summary: 'Error', detail: error.value, life: 5000 })
  }
}

const handleClose = () => {
  if (result.value?.activated ?? 0 > 0) {
    emit('done')
  }
  emit('update:visible', false)
  result.value = null
  selectedYear.value = currentYear
}
```

**Template structure:**

```html
<Dialog
  :visible="visible"
  header="Activar membresía familiar"
  :modal="true"
  :closable="true"
  :dismissableMask="!loading"
  class="w-full max-w-xl"
  @update:visible="handleClose"
>
  <!-- Summary of current state -->
  <div class="mb-4 space-y-1 text-sm text-gray-600">
    <p>
      <strong>{{ membersWithoutMembership.length }}</strong>
      miembro(s) sin membresía activa.
    </p>
    <p v-if="membersAlreadyActive.length > 0">
      <strong>{{ membersAlreadyActive.length }}</strong>
      miembro(s) ya con membresía activa (se omitirán).
    </p>
  </div>

  <!-- Year picker -->
  <div v-if="!result" class="flex flex-col gap-2 mb-6">
    <label for="bulk-start-year" class="font-medium text-sm">
      Año de inicio <span class="text-red-500">*</span>
    </label>
    <InputNumber
      id="bulk-start-year"
      v-model="selectedYear"
      :min="2000"
      :max="currentYear"
      :use-grouping="false"
      class="w-full"
    />
    <small class="text-gray-500">
      Año en que estos miembros se hacen socios. Se aplicará a todos los que no tengan membresía.
    </small>
  </div>

  <!-- Empty state -->
  <Message v-if="membersWithoutMembership.length === 0 && !result" severity="info" class="mb-4">
    Todos los miembros de esta familia ya tienen una membresía activa.
  </Message>

  <!-- Result summary (after activation) -->
  <div v-if="result" class="space-y-3 mb-4">
    <Message
      :severity="result.activated > 0 ? 'success' : 'info'"
    >
      {{ result.activated }} membresía(s) activada(s), {{ result.skipped }} omitida(s).
    </Message>
    <div class="space-y-2">
      <div
        v-for="r in result.results"
        :key="r.memberId"
        class="flex items-center justify-between text-sm rounded-lg border border-gray-100 px-3 py-2"
      >
        <span>{{ r.memberName }}</span>
        <Tag
          :value="r.status === 'Activated' ? 'Activada' : r.status === 'Skipped' ? 'Omitida' : 'Error'"
          :severity="r.status === 'Activated' ? 'success' : r.status === 'Skipped' ? 'secondary' : 'danger'"
        />
      </div>
    </div>
  </div>

  <!-- Actions -->
  <div class="flex justify-end gap-2">
    <Button
      label="Cancelar"
      severity="secondary"
      :disabled="loading"
      @click="handleClose"
    />
    <Button
      v-if="!result && membersWithoutMembership.length > 0"
      :label="`Activar ${membersWithoutMembership.length} membresía(s)`"
      icon="pi pi-check"
      :loading="loading"
      @click="handleBulkActivate"
    />
    <Button
      v-if="result"
      label="Cerrar"
      @click="handleClose"
    />
  </div>
</Dialog>
```

---

### 7. Frontend — Add "Activar membresía familiar" button to `FamilyUnitPage.vue`

**File:** `frontend/src/views/FamilyUnitPage.vue`

**New import:**

```typescript
import BulkMembershipDialog from '@/components/memberships/BulkMembershipDialog.vue'
```

**New state:**

```typescript
const showBulkMembershipDialog = ref(false)
```

**Template — add button next to "Añadir Miembro" in the members Card title:**

```html
<template #title>
  <div class="flex justify-between items-center">
    <span>Miembros Familiares</span>
    <div class="flex gap-2">
      <Button
        v-if="auth.isBoard"
        icon="pi pi-users"
        label="Activar membresía familiar"
        severity="secondary"
        outlined
        size="small"
        @click="showBulkMembershipDialog = true"
      />
      <Button
        icon="pi pi-plus"
        label="Añadir Miembro"
        @click="openCreateMemberDialog"
      />
    </div>
  </div>
</template>
```

**Template — add `BulkMembershipDialog` at the bottom (alongside `MembershipDialog`):**

```html
<BulkMembershipDialog
  v-if="showBulkMembershipDialog"
  v-model:visible="showBulkMembershipDialog"
  :family-unit-id="familyUnit?.id ?? ''"
  :members="familyMembers"
  :member-data="[]"
  @done="getFamilyMembers(familyUnit!.id)"
/>
```

**Note on `memberData` in `FamilyUnitPage`:** `FamilyUnitPage` does not load membership status data (no N+1). The `BulkMembershipDialog` will use `memberData: []` in this context — it can still derive `membersWithoutMembership.length = members.length` (all assumed to need activation). After the bulk call, the response tells the user how many were actually activated vs skipped. If membership status needs to be shown before the dialog opens, this can be added in a future iteration.

---

### 8. Frontend — Add "Activar membresía familiar" button to `ProfilePage.vue`

**File:** `frontend/src/views/ProfilePage.vue`

**New import:**

```typescript
import BulkMembershipDialog from '@/components/memberships/BulkMembershipDialog.vue'
```

**New state:**

```typescript
const showBulkMembershipDialog = ref(false)
```

**Template — add button in the family unit Card title (next to "Gestionar"):**

```html
<Button
  v-if="auth.isBoard && familyUnit"
  label="Activar membresía familiar"
  icon="pi pi-users"
  severity="secondary"
  outlined
  size="small"
  data-testid="bulk-membership-btn"
  @click="showBulkMembershipDialog = true"
/>
```

**Template — add `BulkMembershipDialog` at the bottom (alongside `MembershipDialog`):**

```html
<BulkMembershipDialog
  v-if="showBulkMembershipDialog"
  v-model:visible="showBulkMembershipDialog"
  :family-unit-id="familyUnit?.id ?? ''"
  :members="familyMembers"
  :member-data="memberData"
  @done="loadMemberMembershipData"
/>
```

In `ProfilePage`, `memberData` is already loaded (it holds the membership status for each member), so the `BulkMembershipDialog` can accurately show how many members have no membership before the user clicks "Activar".

---

## Files to Modify / Create

| File | Type | Change |
|---|---|---|
| `src/Abuvi.API/Features/Memberships/MembershipsModels.cs` | Modify | Change `CreateMembershipRequest`, add bulk DTOs |
| `src/Abuvi.API/Features/Memberships/MembershipsService.cs` | Modify | Update `CreateAsync`, add `BulkActivateAsync` |
| `src/Abuvi.API/Features/Memberships/CreateMembershipValidator.cs` | Modify | Validate `Year` instead of `StartDate` |
| `src/Abuvi.API/Features/Memberships/BulkActivateMembershipValidator.cs` | Create | New validator for bulk request |
| `src/Abuvi.API/Features/Memberships/MembershipsEndpoints.cs` | Modify | Register new bulk endpoint |
| `frontend/src/types/membership.ts` | Modify | Change `CreateMembershipRequest`, add bulk types |
| `frontend/src/composables/useMemberships.ts` | Modify | Update `createMembership`, add `bulkActivateMemberships` |
| `frontend/src/components/memberships/MembershipDialog.vue` | Modify | Replace Calendar with InputNumber year picker |
| `frontend/src/components/memberships/BulkMembershipDialog.vue` | Create | New bulk activation dialog |
| `frontend/src/views/FamilyUnitPage.vue` | Modify | Import + wire `BulkMembershipDialog`, add button |
| `frontend/src/views/ProfilePage.vue` | Modify | Import + wire `BulkMembershipDialog`, add button |

---

## TDD Test Cases

### Backend — Unit: `CreateMembershipValidatorTests`

- `ValidYear_PassesValidation`
- `FutureYear_FailsValidation`
- `Year2000_PassesValidation`
- `Year1999_FailsValidation`

### Backend — Unit: `BulkActivateMembershipValidatorTests`

- `ValidYear_PassesValidation`
- `FutureYear_FailsValidation`

### Backend — Unit: `MembershipsServiceTests` (additions)

- `CreateAsync_WhenValidYear_SetsStartDateToJanFirst`
- `BulkActivateAsync_WhenAllMembersHaveNoMembership_ActivatesAll`
- `BulkActivateAsync_WhenSomeMembersAlreadyHaveMembership_SkipsThose`
- `BulkActivateAsync_WhenFamilyUnitNotFound_ThrowsNotFoundException`
- `BulkActivateAsync_WhenFamilyHasNoMembers_ReturnsZeroActivated`

### Backend — Integration: `MembershipsEndpointsTests` (additions)

- `POST_bulk_ValidRequest_Returns200WithActivatedCount`
- `POST_bulk_AllMembersAlreadyHaveMembership_Returns200WithZeroActivated`
- `POST_bulk_FutureYear_Returns400`
- `POST_bulk_FamilyNotFound_Returns404`
- `POST_CreateMembership_WithYear_Returns201AndStartDateIsJanFirst`
- `POST_CreateMembership_FutureYear_Returns400`

### Frontend — Unit: `MembershipDialog.spec.ts` (additions/changes)

- `renders year InputNumber instead of Calendar`
- `defaults to current year`
- `does not allow future year (max = currentYear)`
- `calls createMembership with { year: selectedYear } when Activar is clicked`

### Frontend — Unit: `BulkMembershipDialog.spec.ts` (new)

- `shows correct count of members without membership`
- `shows "todos ya tienen membresía" message when all members are active`
- `calls bulkActivateMemberships with correct familyUnitId and year`
- `shows result summary after activation`
- `emits done when dialog is closed after successful activation`
- `does not emit done when dialog is closed with 0 activations`

---

## Acceptance Criteria

- [ ] `POST /api/.../membership` now accepts `{ year: int }` and rejects `{ startDate: string }`
- [ ] `POST /api/.../membership` with `year = currentYear` stores `startDate = {year}-01-01`
- [ ] `POST /api/.../membership` with a future year returns 400
- [ ] `POST /api/family-units/{id}/membership/bulk` with `{ year }` activates memberships for all members without one
- [ ] Bulk endpoint skips members who already have an active membership and includes them in the response with `status: "Skipped"`
- [ ] Bulk endpoint returns `{ activated, skipped, results[] }` even when some members fail
- [ ] `MembershipDialog` shows a year input (not a date picker) with `min=2000`, `max=currentYear`
- [ ] `MembershipDialog` defaults to current year
- [ ] `BulkMembershipDialog` shows how many members will be activated and how many skipped before clicking
- [ ] `BulkMembershipDialog` shows the per-member result after activation
- [ ] "Activar membresía familiar" button is visible in `FamilyUnitPage` and `ProfilePage` (board users only)
- [ ] After bulk activation from `ProfilePage`, member badges refresh
- [ ] Non-board users do not see the "Activar membresía familiar" button

---

## Implementation Notes

1. **`IFamilyUnitsRepository.GetMembersByFamilyUnitIdAsync`**: The bulk service method needs to fetch all members of a family unit. Check whether `IFamilyUnitsRepository` already exposes this method; if not, add it. Do not fetch members directly in the endpoint handler.

2. **`BulkMembershipDialog` in `FamilyUnitPage`**: Since `FamilyUnitPage` does not load membership status (`memberData = []`), the dialog cannot pre-compute how many members have no membership. The dialog gracefully handles this: when `memberData` is empty, the count shown will be `members.length` (assuming all need activation). After the API call, the response reveals the true breakdown. This is acceptable for now.

3. **Breaking change window**: The `CreateMembershipRequest` change (date → year) is safe because `MembershipDialog` was wired to pages for the first time in `feat-my-memberships-dialog` (implemented today). There is no existing frontend code or user flow that calls this endpoint with `{ startDate }`.

4. **`InputNumber` for year**: PrimeVue's `InputNumber` component supports `:min`, `:max`, and `:use-grouping="false"` (no thousands separator). Import it from `primevue/inputnumber`. It is already in the project's PrimeVue installation.

5. **`BulkMembershipDialog` receives `memberData` as prop** (not fetched internally). In `ProfilePage`, this is already loaded. In `FamilyUnitPage`, it is not — pass `[]` and the dialog handles it gracefully. This follows Rule 3 (no N+1 in the component).

6. **Error state in bulk**: The `BulkActivateAsync` service uses a try/catch per member and records failures in the result, rather than throwing. This ensures partial success is reported correctly.

7. **`@done` emit from `BulkMembershipDialog`**: Emitted only when `result.activated > 0`. The parent page uses this to trigger a data reload. If 0 were activated (all skipped or no members), there is nothing to reload.

---

## Security Considerations

- The bulk endpoint enforces the same authorization as the individual endpoint: the caller must be authenticated and must be Board/Admin or the family representative.
- The `year` field is validated server-side; a client cannot bypass year validation.

---

## Document Control

- **Feature ID**: `feat-bulk-family-membership`
- **Date**: 2026-02-24
- **Status**: Ready for Implementation
- **Approach**: Extend existing membership system with bulk operation + annual model simplification
- **Depends on**: `feat-my-memberships-dialog` (must be merged first — wires `MembershipDialog` into pages)
- **Breaking change**: `CreateMembershipRequest` body changes from `{ startDate }` to `{ year }` — safe because the endpoint was never called from UI before today
- **No new routes**
- **No new npm packages**
- **No DB migration required** (schema unchanged; `startDate` column stays as a date, just always normalized to Jan 1st)
