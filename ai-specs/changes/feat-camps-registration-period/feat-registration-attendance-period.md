# Attendance Period — Enriched Technical Specification

## Overview

This spec extends the existing camp registration system (already implemented in `feat-camps-registration`) to support per-member attendance periods. A camp edition can be split into two periods ("primera semana" / "segunda semana"), and each family member can independently attend the first period, the second, or the complete camp.

This is needed to:

- Know exactly how many people are on-site each day (food, logistics, accommodation)
- Calculate individual pricing based on actual attendance
- Enforce capacity per period, not just per edition

**Depends on**: `feat-camps-registration` (backend already implemented and merged)

---

## Key Assumptions and Design Decisions

| Decision | Choice | Rationale |
|----------|--------|-----------|
| Period structure | Always two named periods + Complete | Matches the "first week / second week" terminology |
| Split date | Configurable `HalfDate` on `CampEdition`, defaults to midpoint | Camps don't always split exactly at the middle |
| Pricing for one period | New fields `PricePerAdult/Child/BabyWeek` on `CampEdition` | Explicit pricing is cleaner than a percentage multiplier |
| Capacity check | Per-period concurrent attendees (Complete counts toward both) | Reflects real on-site capacity |
| PerDay extras | Use full camp duration (`CampDurationDays`) regardless of member period | Simplest approach; per-member extra pricing is a future enhancement |
| Per-family vs per-member period | **Per-member** — each family member has their own `AttendancePeriod` | User requirement: "specify by family member" |
| Request DTO change | Replace `List<Guid> MemberIds` with `List<MemberAttendanceRequest> Members` | Breaking change, documented below |

---

## Data Model Changes

### 1. New Enum: `AttendancePeriod`

Add to `RegistrationsModels.cs`:

```csharp
public enum AttendancePeriod
{
    FirstWeek,
    SecondWeek,
    Complete
}
```

### 2. Modified Entity: `RegistrationMember`

Add one field to the existing entity:

```csharp
public class RegistrationMember
{
    public Guid Id { get; set; }
    public Guid RegistrationId { get; set; }
    public Guid FamilyMemberId { get; set; }
    public int AgeAtCamp { get; set; }
    public AgeCategory AgeCategory { get; set; }
    public decimal IndividualAmount { get; set; }
    public AttendancePeriod AttendancePeriod { get; set; }  // ← NEW
    public DateTime CreatedAt { get; set; }

    // Navigation
    public Registration Registration { get; set; } = null!;
    public FamilyMember FamilyMember { get; set; } = null!;
}
```

### 3. Modified Entity: `CampEdition`

Add pricing for one period and an optional split date. These fields belong in `CampsModels.cs` (wherever `CampEdition` is defined):

```csharp
// ADD to existing CampEdition entity:

// Period split
public DateOnly? HalfDate { get; set; }           // Split point; null = computed midpoint

// Per-period pricing (one period = FirstWeek or SecondWeek)
public decimal? PricePerAdultWeek { get; set; }   // null = no partial attendance allowed
public decimal? PricePerChildWeek { get; set; }
public decimal? PricePerBabyWeek { get; set; }
```

**Logic rule**: If `PricePerAdultWeek` is null, then `FirstWeek` and `SecondWeek` attendance periods are not allowed for that edition. Only `Complete` is valid.

### 4. Modified DTOs

#### `MemberAttendanceRequest` (NEW)

```csharp
public record MemberAttendanceRequest(
    Guid MemberId,
    AttendancePeriod AttendancePeriod
);
```

#### `CreateRegistrationRequest` (CHANGED — Breaking)

```csharp
// BEFORE:
public record CreateRegistrationRequest(
    Guid CampEditionId,
    Guid FamilyUnitId,
    List<Guid> MemberIds,
    string? Notes
);

// AFTER:
public record CreateRegistrationRequest(
    Guid CampEditionId,
    Guid FamilyUnitId,
    List<MemberAttendanceRequest> Members,   // ← CHANGED
    string? Notes
);
```

#### `UpdateRegistrationMembersRequest` (CHANGED — Breaking)

```csharp
// BEFORE:
public record UpdateRegistrationMembersRequest(List<Guid> MemberIds);

// AFTER:
public record UpdateRegistrationMembersRequest(List<MemberAttendanceRequest> Members);
```

#### `MemberPricingDetail` response (CHANGED — additive)

```csharp
// ADD to existing MemberPricingDetail:
public record MemberPricingDetail(
    Guid FamilyMemberId,
    string FullName,
    int AgeAtCamp,
    AgeCategory AgeCategory,
    AttendancePeriod AttendancePeriod,   // ← NEW
    int AttendanceDays,                  // ← NEW (computed from period dates)
    decimal IndividualAmount
);
```

#### `AvailableCampEditionResponse` (CHANGED — additive)

```csharp
// ADD fields to existing response:
public record AvailableCampEditionResponse(
    // ... existing fields ...
    bool AllowsPartialAttendance,        // ← NEW: true if PricePerAdultWeek is set
    decimal? PricePerAdultWeek,          // ← NEW
    decimal? PricePerChildWeek,          // ← NEW
    decimal? PricePerBabyWeek,           // ← NEW
    DateOnly? HalfDate,                  // ← NEW
    int FirstWeekDays,                   // ← NEW: computed
    int SecondWeekDays                   // ← NEW: computed
);
```

---

## EF Core Configuration Changes

### `RegistrationMemberConfiguration.cs` — Add column

```csharp
// ADD to existing Configure method:
builder.Property(m => m.AttendancePeriod)
    .HasConversion<string>()
    .IsRequired()
    .HasMaxLength(15)
    .HasColumnName("attendance_period")
    .HasDefaultValue(AttendancePeriod.Complete);   // default for existing rows
```

Update the uniqueness index — no change needed (already unique on RegistrationId + FamilyMemberId).

### `CampEditionConfiguration.cs` — Add columns

```csharp
// ADD to existing Configure method:
builder.Property(e => e.HalfDate)
    .HasColumnName("half_date")
    .HasColumnType("date");

builder.Property(e => e.PricePerAdultWeek)
    .HasPrecision(10, 2)
    .HasColumnName("price_per_adult_week");

builder.Property(e => e.PricePerChildWeek)
    .HasPrecision(10, 2)
    .HasColumnName("price_per_child_week");

builder.Property(e => e.PricePerBabyWeek)
    .HasPrecision(10, 2)
    .HasColumnName("price_per_baby_week");
```

---

## Migration

Single migration adding all new columns:

```bash
dotnet ef migrations add AddAttendancePeriodToRegistrations --project src/Abuvi.API
dotnet ef database update --project src/Abuvi.API
```

**Migration SQL (approximate)**:

```sql
ALTER TABLE registration_members
    ADD COLUMN attendance_period VARCHAR(15) NOT NULL DEFAULT 'Complete';

ALTER TABLE camp_editions
    ADD COLUMN half_date DATE,
    ADD COLUMN price_per_adult_week NUMERIC(10,2),
    ADD COLUMN price_per_child_week NUMERIC(10,2),
    ADD COLUMN price_per_baby_week  NUMERIC(10,2);
```

Existing rows: `attendance_period` defaults to `'Complete'` — backward compatible.

---

## Business Logic Changes

### `RegistrationPricingService.cs`

#### New helper: `GetPeriodDays`

```csharp
// GetPeriodDays(AttendancePeriod period, CampEdition edition) → int
//   Compute the half-point:
//     halfDate = edition.HalfDate ?? midpoint of (StartDate, EndDate)
//   FirstWeek:  days from StartDate to halfDate (inclusive)
//   SecondWeek: days from halfDate+1 to EndDate (inclusive)
//   Complete:   full campDurationDays (StartDate to EndDate)
```

#### Changed: `GetPriceForCategory`

```csharp
// GetPriceForCategory(AgeCategory category, AttendancePeriod period, CampEdition edition) → decimal
//   Complete:    existing logic (pricePerAdult / pricePerChild / pricePerBaby)
//   FirstWeek or SecondWeek:
//     Adult → edition.PricePerAdultWeek
//     Child → edition.PricePerChildWeek
//     Baby  → edition.PricePerBabyWeek
//     Throws BusinessRuleException("PARTIAL_ATTENDANCE_NOT_ALLOWED",
//       "Esta edición no permite inscripción parcial por semanas")
//       if PricePerAdultWeek is null
```

#### `CalculateExtraAmount` — NO CHANGE

`PerDay` extras continue to use the full `campDurationDays` of the edition. This is intentional simplification documented as a known limitation (partial attendance does not reduce the cost of PerDay extras).

### `RegistrationsService.cs`

#### `CreateAsync` — changes

1. Iterate `request.Members` (instead of `request.MemberIds`)
2. For each `MemberAttendanceRequest`:
   - Validate `AttendancePeriod` is allowed (edition has week pricing if not `Complete`)
   - Calculate age at camp, determine `AgeCategory`
   - Get price: `RegistrationPricingService.GetPriceForCategory(category, period, edition)`
   - Set `RegistrationMember.AttendancePeriod = request.AttendancePeriod`
3. Capacity check changes — see below

#### `UpdateMembersAsync` — same changes as CreateAsync

#### New error code

| Situation | HTTP | Code |
|-----------|------|------|
| Edition does not allow partial attendance but FirstWeek/SecondWeek requested | 422 | `PARTIAL_ATTENDANCE_NOT_ALLOWED` |

### Capacity Check (Updated)

Replace the simple "count non-cancelled registrations" with a per-period concurrent count:

```csharp
// IRegistrationsRepository — NEW method:
Task<int> CountConcurrentAttendeesByPeriodAsync(
    Guid campEditionId,
    AttendancePeriod period,
    CancellationToken ct
);

// Implementation (SQL equivalent):
// SELECT COUNT(rm.id)
// FROM registration_members rm
// JOIN registrations r ON r.id = rm.registration_id
// WHERE r.camp_edition_id = @editionId
//   AND r.status != 'Cancelled'
//   AND (rm.attendance_period = 'Complete' OR rm.attendance_period = @period)
```

**Capacity check logic in `CreateAsync` (within RepeatableRead transaction)**:

```csharp
// For each incoming member with their AttendancePeriod:
//   if period == Complete:
//     check CountConcurrentAttendees(FirstWeek) + 1 <= MaxCapacity
//     check CountConcurrentAttendees(SecondWeek) + 1 <= MaxCapacity
//   if period == FirstWeek or SecondWeek:
//     check CountConcurrentAttendees(period) + 1 <= MaxCapacity
//
// If ANY check fails → throw BusinessRuleException("CAMP_FULL",
//   "El campamento ha alcanzado su capacidad máxima para ese periodo")
```

---

## Validator Changes

### `CreateRegistrationValidator.cs`

```csharp
// Replace MemberIds rule with:
RuleFor(x => x.Members)
    .NotEmpty().WithMessage("Debe seleccionar al menos un miembro de la familia")
    .Must(members => members.Select(m => m.MemberId).Distinct().Count() == members.Count)
    .WithMessage("No se puede incluir el mismo miembro dos veces");

RuleForEach(x => x.Members).ChildRules(member =>
{
    member.RuleFor(m => m.MemberId)
        .NotEmpty().WithMessage("El identificador del miembro es obligatorio");
    member.RuleFor(m => m.AttendancePeriod)
        .IsInEnum().WithMessage("El periodo de asistencia no es válido");
});
```

### `UpdateRegistrationMembersValidator.cs`

Same change: replace `MemberIds` with `Members` with identical rules.

---

## IRegistrationsRepository Changes

```csharp
public interface IRegistrationsRepository
{
    // ... existing methods unchanged ...

    // CHANGED signature — used internally by service, no breaking change on interface if
    // you keep the old CountActiveByEditionAsync for non-capacity purposes:
    Task<int> CountActiveByEditionAsync(Guid campEditionId, CancellationToken ct);  // keep

    // NEW:
    Task<int> CountConcurrentAttendeesByPeriodAsync(
        Guid campEditionId,
        AttendancePeriod period,
        CancellationToken ct
    );
}
```

---

## `CampEdition` API Changes

The Board-facing endpoints for creating/updating editions need new optional fields:

### `CreateCampEditionRequest` / `UpdateCampEditionRequest` (additive)

```csharp
// ADD optional fields:
DateOnly? HalfDate
decimal? PricePerAdultWeek
decimal? PricePerChildWeek
decimal? PricePerBabyWeek
```

No validation required beyond: if any week price is provided, all three must be provided.

```csharp
// Validator rule:
RuleFor(x => x)
    .Must(x =>
        (x.PricePerAdultWeek == null && x.PricePerChildWeek == null && x.PricePerBabyWeek == null) ||
        (x.PricePerAdultWeek != null && x.PricePerChildWeek != null && x.PricePerBabyWeek != null)
    )
    .WithMessage("Si se configura precio por semana, todos los precios (adulto, niño, bebé) son obligatorios");
```

---

## Files to Create / Modify

### Backend

```
src/Abuvi.API/Features/Registrations/RegistrationsModels.cs
    ← Add AttendancePeriod enum
    ← Add AttendancePeriod to RegistrationMember
    ← Add MemberAttendanceRequest record
    ← Change CreateRegistrationRequest.MemberIds → Members
    ← Change UpdateRegistrationMembersRequest.MemberIds → Members
    ← Add AttendancePeriod + AttendanceDays to MemberPricingDetail response
    ← Add AllowsPartialAttendance + week prices + half date to AvailableCampEditionResponse

src/Abuvi.API/Features/Registrations/RegistrationPricingService.cs
    ← Add GetPeriodDays method
    ← Update GetPriceForCategory signature to accept AttendancePeriod

src/Abuvi.API/Features/Registrations/RegistrationsService.cs
    ← Update CreateAsync / UpdateMembersAsync to use MemberAttendanceRequest
    ← Update capacity check to use CountConcurrentAttendeesByPeriodAsync

src/Abuvi.API/Features/Registrations/IRegistrationsRepository.cs
    ← Add CountConcurrentAttendeesByPeriodAsync

src/Abuvi.API/Features/Registrations/RegistrationsRepository.cs
    ← Implement CountConcurrentAttendeesByPeriodAsync

src/Abuvi.API/Features/Registrations/CreateRegistrationValidator.cs
    ← Replace MemberIds with Members rules

src/Abuvi.API/Features/Registrations/UpdateRegistrationMembersValidator.cs
    ← Replace MemberIds with Members rules

src/Abuvi.API/Data/Configurations/RegistrationMemberConfiguration.cs
    ← Add attendance_period column

src/Abuvi.API/Features/Camps/CampsModels.cs  (or wherever CampEdition entity lives)
    ← Add HalfDate, PricePerAdultWeek, PricePerChildWeek, PricePerBabyWeek

src/Abuvi.API/Data/Configurations/CampEditionConfiguration.cs
    ← Add 4 new columns

src/Abuvi.API/Features/Camps/CampEditionsService.cs  (or requests)
    ← Accept new week pricing fields in create/update
```

### Tests to Update / Add

```
src/Abuvi.Tests/Unit/Features/Registrations/RegistrationPricingServiceTests.cs
    ← Add: GetPeriodDays_* tests
    ← Add: GetPriceForCategory_FirstWeek_ReturnsWeekPrice
    ← Add: GetPriceForCategory_WhenNoWeekPriceConfigured_ThrowsBusinessRuleException

src/Abuvi.Tests/Unit/Features/Registrations/RegistrationsServiceTests.cs
    ← Update all tests that pass MemberIds to pass MemberAttendanceRequest list
    ← Add: CreateAsync_WhenPartialAttendanceNotAllowed_ThrowsBusinessRuleException
    ← Add: CreateAsync_CapacityCheck_FirstWeekCountsOnlyFirstWeekAndComplete
    ← Add: CreateAsync_CapacityCheck_CompleteCountsTowardsBothPeriods

src/Abuvi.Tests/Unit/Features/Registrations/CreateRegistrationValidatorTests.cs
    ← Update to use Members instead of MemberIds
    ← Add: Validator_WhenAttendancePeriodInvalid_Fails
```

---

## Frontend Changes

The frontend plan `feat-camps-registration_frontend.md` must be updated to account for the following diffs:

### 1. `types/registration.ts` — Changed

```typescript
// ADD new type:
export type AttendancePeriod = 'FirstWeek' | 'SecondWeek' | 'Complete'

// ADD to MemberPricingDetail:
attendancePeriod: AttendancePeriod  // new field from API
attendanceDays: number              // new field from API

// ADD to AvailableCampEditionResponse:
allowsPartialAttendance: boolean
pricePerAdultWeek: number | null
pricePerChildWeek: number | null
pricePerBabyWeek: number | null
halfDate: string | null
firstWeekDays: number
secondWeekDays: number

// REPLACE in CreateRegistrationRequest:
// OLD: memberIds: string[]
// NEW:
members: MemberAttendanceRequest[]

// ADD new type:
export interface MemberAttendanceRequest {
  memberId: string
  attendancePeriod: AttendancePeriod
}

// REPLACE in UpdateRegistrationMembersRequest:
// OLD: memberIds: string[]
// NEW:
members: MemberAttendanceRequest[]

// ADD wizard-local type (replaces WizardMemberSelection if it existed):
export interface WizardMemberSelection {
  memberId: string
  attendancePeriod: AttendancePeriod
}

// UPDATE WizardExtrasSelection — no change needed
```

### 2. `RegistrationMemberSelector.vue` — Changed

**Props change**:

```typescript
// OLD: modelValue: string[]  (selected IDs)
// NEW: modelValue: WizardMemberSelection[]  (selected ID + period)
```

**Template change** (Step 0 of wizard):

Each family member row changes from a simple checkbox to:

- Checkbox (include/exclude member)
- When checked: show `Select` dropdown with period options

```
┌─────────────────────────────────────────────────────┐
│ ☑ Juan García · Padre · 12/04/1975                  │
│   Periodo:  [Campamento Completo ▼]                  │
│   ⚠ Notas médicas                                    │
└─────────────────────────────────────────────────────┘
```

Period options (only shown when member is checked):

- "Campamento completo" → `Complete`
- "Primera semana (X días)" → `FirstWeek` (only if `allowsPartialAttendance`)
- "Segunda semana (X días)" → `SecondWeek` (only if `allowsPartialAttendance`)

PrimeVue component: `Select` (dropdown) for period, or `SelectButton` for more visual clarity.

**Emit**: `update:modelValue` emits `WizardMemberSelection[]` instead of `string[]`.

### 3. `RegisterForCampPage.vue` — Changed

```typescript
// OLD wizard state:
const selectedMemberIds = ref<string[]>([])

// NEW wizard state:
const selectedMembers = ref<WizardMemberSelection[]>([])
```

**Step 2 (Review) pricing guide** — update to show per-period prices:

```
Precios orientativos:
                 Completo   1ª semana   2ª semana
Adulto/a          180€       110€        110€
Niño/Niña          90€        55€         55€
Bebé                0€         0€          0€
```

(Only show week columns if `edition.allowsPartialAttendance`)

**On confirm** — pass new shape:

```typescript
const created = await createRegistration({
  campEditionId: editionId.value,
  familyUnitId: familyUnit.value!.id,
  members: selectedMembers.value.map(m => ({
    memberId: m.memberId,
    attendancePeriod: m.attendancePeriod
  })),
  notes: notes.value || null
})
```

### 4. `RegistrationPricingBreakdown.vue` — Changed

Add `AttendancePeriod` column to the members table:

```
Nombre          Edad  Categoría   Periodo           Importe
Juan García      49   Adulto/a    Campamento compl.  180,00€
Lucía García     12   Niño/Niña   Primera semana      55,00€
```

**Labels for AttendancePeriod** (add to component):

```typescript
const ATTENDANCE_PERIOD_LABELS: Record<AttendancePeriod, string> = {
  Complete: 'Campamento completo',
  FirstWeek: 'Primera semana',
  SecondWeek: 'Segunda semana'
}
```

### 5. `RegistrationCard.vue` — No change needed

The card shows status, camp name, total — not member details.

---

## AttendancePeriod Label Utility

Add to `frontend/src/utils/registration.ts` (new file, shared utility):

```typescript
import type { AttendancePeriod } from '@/types/registration'

export const ATTENDANCE_PERIOD_LABELS: Record<AttendancePeriod, string> = {
  Complete: 'Campamento completo',
  FirstWeek: 'Primera semana',
  SecondWeek: 'Segunda semana',
}

export const getAttendancePeriodLabel = (period: AttendancePeriod): string =>
  ATTENDANCE_PERIOD_LABELS[period] ?? period
```

Import this in `RegistrationMemberSelector`, `RegistrationPricingBreakdown`, and `RegisterForCampPage`.

---

## Test Coverage Additions

### Backend Unit Tests

New tests for `RegistrationPricingServiceTests.cs`:

- `GetPeriodDays_Complete_ReturnsFullCampDuration`
- `GetPeriodDays_FirstWeek_WithExplicitHalfDate_ReturnsCorrectDays`
- `GetPeriodDays_SecondWeek_WithNullHalfDate_UsesComputedMidpoint`
- `GetPriceForCategory_FirstWeek_ReturnsWeekPrice`
- `GetPriceForCategory_FirstWeek_WhenNoWeekPriceSet_ThrowsBusinessRuleException`

New tests for `RegistrationsServiceTests.cs`:

- `CreateAsync_WithMixedPeriods_CalculatesPricingPerPeriod`
- `CreateAsync_WhenFirstWeekFullButSecondWeekHasSpace_ThrowsForFirstWeekMember`
- `CreateAsync_WhenCompleteMemberWouldExceedEitherPeriod_ThrowsCampFull`
- `CreateAsync_WhenPartialAttendanceNotAllowedByEdition_ThrowsBusinessRuleException`

### Frontend Unit Tests

New tests for `RegistrationMemberSelector.test.ts`:

- `should show period selector when member is checked`
- `should default to Complete period when member is first checked`
- `should not show FirstWeek/SecondWeek options when allowsPartialAttendance is false`
- `should emit WizardMemberSelection with correct memberId and period`

New tests for `RegistrationPricingBreakdown.test.ts`:

- `should show AttendancePeriod column when members have different periods`
- `should display correct period label for each member`

---

## Outstanding Decisions Requiring Product Confirmation

1. **What is the default period when a member is first checked in the wizard?**
   Recommendation: `Complete`. User can change it if partial attendance is allowed.

2. **Can a member switch period after registration is Confirmed?**
   Current spec: `UpdateMembersAsync` already requires `Pending` status. No change needed — update is blocked on Confirmed.

3. **Should `PerPerson + PerDay` extras be split by member period?**
   Current spec says NO (use full camp days). If needed, this is a separate future spec.

4. **What should the camp admin see in their dashboard?**
   The admin panel registration view (separate future ticket) should show per-period headcounts. Not in scope here.

---

## Document Control

- **Feature**: `feat-registration-attendance-period`
- **Extends**: `feat-camps-registration` (enriched spec v1.1)
- **Also updates**: `feat-camps-registration_frontend.md`
- **Version**: 1.0
- **Date**: 2026-02-22
- **Status**: Ready for Backend Implementation (backend must be updated before frontend)
- **Priority**: Implement backend changes first, then proceed with frontend from the updated frontend plan
