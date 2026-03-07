# Attendance Period — Enriched Technical Specification v1.2

## Overview

This spec extends the existing camp registration system (already implemented in `feat-camps-registration`) to support per-member attendance periods. A camp edition can be split into two periods ("primera semana" / "segunda semana"), and each family member can independently attend the first period, the second, the complete camp, or a short **weekend visit** (máximo 3 días).

This is needed to:

- Know exactly how many people are on-site each day (food, logistics, accommodation)
- Calculate individual pricing based on actual attendance
- Enforce capacity per period, not just per edition
- Support short visits (fin de semana) with their own pricing and capacity

**Depends on**: `feat-camps-registration` (backend implemented and merged)

---

## Key Assumptions and Design Decisions

| Decision | Choice | Rationale |
|----------|--------|-----------|
| Period structure | Two named periods + Complete + **WeekendVisit** | Matches "primera semana / segunda semana / completo / fin de semana" |
| Split date | Configurable `HalfDate` on `CampEdition`, defaults to midpoint | Camps don't always split exactly at the middle |
| Pricing for one period | New fields `PricePerAdult/Child/BabyWeek` on `CampEdition` | Explicit pricing is cleaner than a percentage multiplier |
| Pricing for weekend | New fields `PricePerAdult/Child/BabyWeekend` on `CampEdition` | Same pattern as week pricing |
| Capacity check | Per-period concurrent **members** (Complete counts toward both) | Reflects real on-site capacity; counts members, not registrations |
| Weekend capacity | **Separate** via `MaxWeekendCapacity` — does NOT count toward week capacity | Weekend visitors are a distinct logistical group (different meal plan, no week-long accommodation) |
| Weekend duration | Configurable `WeekendStartDate`/`WeekendEndDate` on `CampEdition`, max 3 days | Must fall within camp dates; enforced in validator |
| PerDay extras | Use full camp duration (`CampDurationDays`) regardless of member period | Simplest approach; per-member extra pricing is a future enhancement |
| Per-family vs per-member period | **Per-member** — each family member has their own `AttendancePeriod` | User requirement |
| Request DTO change | Replace `List<Guid> MemberIds` with `List<MemberAttendanceRequest> Members` | Breaking change, documented below |
| `HalfDate` / `WeekendStartDate` / `WeekendEndDate` type | `DateOnly?` | Date-only concept; avoids timezone issues |
| `CampEdition.StartDate`/`EndDate` | Remain `DateTime` | No change to existing entity types; `GetPeriodDays` converts internally via `DateOnly.FromDateTime` |

---

## Codebase Anchors

Before starting implementation, understand these existing files:

| File | What to know |
|------|--------------|
| [RegistrationsModels.cs](src/Abuvi.API/Features/Registrations/RegistrationsModels.cs) | All domain entities + DTOs for registrations in one file |
| [RegistrationPricingService.cs](src/Abuvi.API/Features/Registrations/RegistrationPricingService.cs) | `GetPriceForCategory(AgeCategory, CampEdition)` — signature changes; needs new static `GetPeriodDays` |
| [RegistrationsService.cs](src/Abuvi.API/Features/Registrations/RegistrationsService.cs) | `CreateAsync` iterates `request.MemberIds` — must change to `request.Members` |
| [RegistrationsRepository.cs](src/Abuvi.API/Features/Registrations/RegistrationsRepository.cs) | `IRegistrationsRepository` interface AND implementation live in the **same file** |
| [CampsModels.cs](src/Abuvi.API/Features/Camps/CampsModels.cs) | `CampEdition` entity, `ProposeCampEditionRequest`, `UpdateCampEditionRequest`, `CampEditionResponse` |
| [CampsValidators.cs](src/Abuvi.API/Features/Camps/CampsValidators.cs) | `ProposeCampEditionRequestValidator`, `UpdateCampEditionRequestValidator` |
| [CampEditionConfiguration.cs](src/Abuvi.API/Data/Configurations/CampEditionConfiguration.cs) | EF Core config for `camp_editions` table |
| [RegistrationMemberConfiguration.cs](src/Abuvi.API/Data/Configurations/RegistrationMemberConfiguration.cs) | EF Core config for `registration_members` table |
| [RegistrationBuilder.cs](src/Abuvi.Tests/Helpers/Builders/RegistrationBuilder.cs) | Test builder — needs new builder methods for week pricing fields |
| [RegistrationPricingServiceTests.cs](src/Abuvi.Tests/Unit/Features/Registrations/RegistrationPricingServiceTests.cs) | Existing pricing tests — add `GetPeriodDays` and updated `GetPriceForCategory` tests |
| [RegistrationsServiceTests.cs](src/Abuvi.Tests/Unit/Features/Registrations/RegistrationsServiceTests.cs) | Update all tests using `MemberIds` to use `Members` |
| [CreateRegistrationValidatorTests.cs](src/Abuvi.Tests/Unit/Features/Registrations/CreateRegistrationValidatorTests.cs) | Update validator tests to use `Members` |

---

## Data Model Changes

### 1. New Enum: `AttendancePeriod`

Add to `RegistrationsModels.cs` alongside existing enums:

```csharp
public enum AttendancePeriod
{
    FirstWeek,
    SecondWeek,
    Complete,
    WeekendVisit   // ← Short visit, max 3 days, configurable window
}
```

### 2. Modified Entity: `RegistrationMember`

Add fields to the existing entity in `RegistrationsModels.cs`:

```csharp
public class RegistrationMember
{
    public Guid Id { get; set; }
    public Guid RegistrationId { get; set; }
    public Guid FamilyMemberId { get; set; }
    public int AgeAtCamp { get; set; }
    public AgeCategory AgeCategory { get; set; }
    public decimal IndividualAmount { get; set; }
    public AttendancePeriod AttendancePeriod { get; set; }  // ← NEW, defaults to Complete

    // Only populated when AttendancePeriod = WeekendVisit
    public DateOnly? VisitStartDate { get; set; }           // ← NEW
    public DateOnly? VisitEndDate { get; set; }             // ← NEW

    public DateTime CreatedAt { get; set; }

    // Navigation
    public Registration Registration { get; set; } = null!;
    public FamilyMember FamilyMember { get; set; } = null!;
}
```

**`VisitStartDate` / `VisitEndDate` rules**:

- Required when `AttendancePeriod = WeekendVisit`; must be null for all other periods.
- Both must fall within `[CampEdition.StartDate, CampEdition.EndDate]`.
- `VisitEndDate - VisitStartDate ≤ 3 days`.
- The edition's `WeekendStartDate`/`WeekendEndDate` act as the **UI default** shown to the user, not as a hard constraint on which dates are valid.
- Capacity is counted as a flat pool of all `WeekendVisit` members (regardless of their specific dates) against `MaxWeekendCapacity`. Per-day capacity is a future enhancement.

### 3. Modified Entity: `CampEdition`

Add to the existing `CampEdition` class in `CampsModels.cs` (after the existing `PricePerBaby` field):

```csharp
// Period split point
public DateOnly? HalfDate { get; set; }           // null = computed midpoint

// Per-period pricing (one period = FirstWeek or SecondWeek)
public decimal? PricePerAdultWeek { get; set; }   // null = partial attendance not allowed
public decimal? PricePerChildWeek { get; set; }
public decimal? PricePerBabyWeek { get; set; }

// Weekend visit window (max 3 days)
public DateOnly? WeekendStartDate { get; set; }   // null = weekend visit not allowed
public DateOnly? WeekendEndDate { get; set; }     // null = WeekendStartDate + 2 days (Sat/Sun)

// Weekend visit pricing
public decimal? PricePerAdultWeekend { get; set; }  // null = weekend visit not allowed
public decimal? PricePerChildWeekend { get; set; }
public decimal? PricePerBabyWeekend { get; set; }

// Weekend visit capacity (optional separate cap; if null, uses MaxCapacity)
public int? MaxWeekendCapacity { get; set; }
```

**Logic rules**:

- If `PricePerAdultWeek` is null → `FirstWeek` and `SecondWeek` not allowed; only `Complete` (and optionally `WeekendVisit`).
- If `WeekendStartDate` is null → `WeekendVisit` not allowed.
- If `WeekendStartDate` is set but `PricePerAdultWeekend` is null → invalid configuration (validator enforces: all weekend prices or none).
- `WeekendEndDate` must be ≤ `WeekendStartDate + 3 days`.
- Both `WeekendStartDate` and `WeekendEndDate` must fall within `[StartDate, EndDate]` of the camp.
- Weekend capacity: if `MaxWeekendCapacity` is set it is checked independently; otherwise `MaxCapacity` applies to weekend too (via a separate counter).

### 4. Modified DTOs

#### `MemberAttendanceRequest` (NEW) — add to `RegistrationsModels.cs`

```csharp
public record MemberAttendanceRequest(
    Guid MemberId,
    AttendancePeriod AttendancePeriod,
    DateOnly? VisitStartDate = null,   // Required when AttendancePeriod = WeekendVisit
    DateOnly? VisitEndDate = null      // Required when AttendancePeriod = WeekendVisit
);
```

#### `CreateRegistrationRequest` (CHANGED — Breaking)

```csharp
// BEFORE (existing):
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
// BEFORE (existing):
public record UpdateRegistrationMembersRequest(List<Guid> MemberIds);

// AFTER:
public record UpdateRegistrationMembersRequest(List<MemberAttendanceRequest> Members);
```

#### `MemberPricingDetail` response (CHANGED — additive) — in `RegistrationsModels.cs`

```csharp
// BEFORE (existing):
public record MemberPricingDetail(
    Guid FamilyMemberId, string FullName, int AgeAtCamp,
    AgeCategory AgeCategory, decimal IndividualAmount);

// AFTER:
public record MemberPricingDetail(
    Guid FamilyMemberId, string FullName, int AgeAtCamp,
    AgeCategory AgeCategory,
    AttendancePeriod AttendancePeriod,   // ← NEW
    int AttendanceDays,                  // ← NEW (computed from period and edition dates)
    DateOnly? VisitStartDate,            // ← NEW (only set for WeekendVisit)
    DateOnly? VisitEndDate,              // ← NEW (only set for WeekendVisit)
    decimal IndividualAmount);
```

**`AttendanceDays` mapping**: `RegistrationMappingExtensions.ToResponse` is a static extension method with no DI. To compute `AttendanceDays` it must call `RegistrationPricingService.GetPeriodDays`, which **must be declared `public static`**. For `WeekendVisit` members the overload that accepts member-specific dates is used. The mapping call becomes:

```csharp
r.Members.Select(m => new MemberPricingDetail(
    m.FamilyMemberId,
    $"{m.FamilyMember.FirstName} {m.FamilyMember.LastName}",
    m.AgeAtCamp,
    m.AgeCategory,
    m.AttendancePeriod,
    RegistrationPricingService.GetPeriodDays(
        m.AttendancePeriod, r.CampEdition, m.VisitStartDate, m.VisitEndDate),
    m.VisitStartDate,
    m.VisitEndDate,
    m.IndividualAmount)).ToList()
```

#### `AvailableCampEditionResponse` (CHANGED — additive) — in `RegistrationsModels.cs`

```csharp
// AFTER (full record — new fields marked):
public record AvailableCampEditionResponse(
    Guid Id,
    string CampName,
    int Year,
    DateTime StartDate,
    DateTime EndDate,
    string? Location,
    decimal PricePerAdult,
    decimal PricePerChild,
    decimal PricePerBaby,
    int? MaxCapacity,
    int CurrentRegistrations,
    int? SpotsRemaining,
    string Status,
    AgeRangesInfo AgeRanges,
    bool AllowsPartialAttendance,       // ← NEW: true if PricePerAdultWeek is set
    decimal? PricePerAdultWeek,         // ← NEW
    decimal? PricePerChildWeek,         // ← NEW
    decimal? PricePerBabyWeek,          // ← NEW
    DateOnly? HalfDate,                 // ← NEW
    int FirstWeekDays,                  // ← NEW (computed via GetPeriodDays)
    int SecondWeekDays,                 // ← NEW (computed via GetPeriodDays)
    bool AllowsWeekendVisit,            // ← NEW: true if WeekendStartDate + weekend prices set
    decimal? PricePerAdultWeekend,      // ← NEW
    decimal? PricePerChildWeekend,      // ← NEW
    decimal? PricePerBabyWeekend,       // ← NEW
    DateOnly? WeekendStartDate,         // ← NEW
    DateOnly? WeekendEndDate,           // ← NEW
    int WeekendDays,                    // ← NEW (computed via GetPeriodDays)
    int? MaxWeekendCapacity,            // ← NEW
    int? WeekendSpotsRemaining          // ← NEW
);
```

**`SpotsRemaining` update**: With per-period capacity, `SpotsRemaining` should reflect the most constrained period. `WeekendSpotsRemaining` is tracked separately.

```csharp
int? spotsRemaining = null;
if (edition.MaxCapacity.HasValue)
{
    var firstWeekCount = await registrationsRepo
        .CountConcurrentAttendeesByPeriodAsync(edition.Id, AttendancePeriod.FirstWeek, ct);
    var secondWeekCount = await registrationsRepo
        .CountConcurrentAttendeesByPeriodAsync(edition.Id, AttendancePeriod.SecondWeek, ct);
    spotsRemaining = Math.Max(0,
        edition.MaxCapacity.Value - Math.Max(firstWeekCount, secondWeekCount));
}

int? weekendSpotsRemaining = null;
if (edition.WeekendStartDate.HasValue)
{
    var weekendCap = edition.MaxWeekendCapacity ?? edition.MaxCapacity;
    if (weekendCap.HasValue)
    {
        var weekendCount = await registrationsRepo
            .CountConcurrentAttendeesByPeriodAsync(edition.Id, AttendancePeriod.WeekendVisit, ct);
        weekendSpotsRemaining = Math.Max(0, weekendCap.Value - weekendCount);
    }
}
```

---

## EF Core Configuration Changes

### `RegistrationMemberConfiguration.cs` — Add columns

Add to the existing `Configure` method (after `IndividualAmount`):

```csharp
builder.Property(m => m.AttendancePeriod)
    .HasConversion<string>()
    .IsRequired()
    .HasMaxLength(15)
    .HasColumnName("attendance_period")
    .HasDefaultValue(AttendancePeriod.Complete);

// Only populated for WeekendVisit members
builder.Property(m => m.VisitStartDate)
    .HasColumnName("visit_start_date")
    .HasColumnType("date");

builder.Property(m => m.VisitEndDate)
    .HasColumnName("visit_end_date")
    .HasColumnType("date");
```

**Migration SQL addition** (add to the `registration_members` ALTER):

```sql
ALTER TABLE registration_members
    ADD COLUMN attendance_period VARCHAR(15) NOT NULL DEFAULT 'Complete',
    ADD COLUMN visit_start_date  DATE,
    ADD COLUMN visit_end_date    DATE;
```

The existing uniqueness index (RegistrationId + FamilyMemberId) requires no change.

### `CampEditionConfiguration.cs` — Add columns

Add to the existing `Configure` method (after the `PricePerBaby` check constraint):

```csharp
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

// Weekend visit
builder.Property(e => e.WeekendStartDate)
    .HasColumnName("weekend_start_date")
    .HasColumnType("date");

builder.Property(e => e.WeekendEndDate)
    .HasColumnName("weekend_end_date")
    .HasColumnType("date");

builder.Property(e => e.PricePerAdultWeekend)
    .HasPrecision(10, 2)
    .HasColumnName("price_per_adult_weekend");

builder.Property(e => e.PricePerChildWeekend)
    .HasPrecision(10, 2)
    .HasColumnName("price_per_child_weekend");

builder.Property(e => e.PricePerBabyWeekend)
    .HasPrecision(10, 2)
    .HasColumnName("price_per_baby_weekend");

builder.Property(e => e.MaxWeekendCapacity)
    .HasColumnName("max_weekend_capacity");
```

---

## Migration

```bash
dotnet ef migrations add AddAttendancePeriodToRegistrations --project src/Abuvi.API
dotnet ef database update --project src/Abuvi.API
```

**Migration SQL (approximate)**:

```sql
ALTER TABLE registration_members
    ADD COLUMN attendance_period VARCHAR(15) NOT NULL DEFAULT 'Complete',
    ADD COLUMN visit_start_date  DATE,
    ADD COLUMN visit_end_date    DATE;

ALTER TABLE camp_editions
    ADD COLUMN half_date               DATE,
    ADD COLUMN price_per_adult_week    NUMERIC(10,2),
    ADD COLUMN price_per_child_week    NUMERIC(10,2),
    ADD COLUMN price_per_baby_week     NUMERIC(10,2),
    ADD COLUMN weekend_start_date      DATE,
    ADD COLUMN weekend_end_date        DATE,
    ADD COLUMN price_per_adult_weekend NUMERIC(10,2),
    ADD COLUMN price_per_child_weekend NUMERIC(10,2),
    ADD COLUMN price_per_baby_weekend  NUMERIC(10,2),
    ADD COLUMN max_weekend_capacity    INT;
```

> **Note on `attendance_period` column length**: `VARCHAR(15)` covers `'WeekendVisit'` (12 chars). No change needed.

Existing rows: `attendance_period` defaults to `'Complete'`, `visit_start_date`/`visit_end_date` default to null — backward compatible.

---

## Business Logic Changes

### `RegistrationPricingService.cs`

#### New method: `GetPeriodDays` — MUST be `public static`

Must be static so that `RegistrationMappingExtensions.ToResponse` can call it without DI.

`CampEdition.StartDate` and `EndDate` are `DateTime`. Convert to `DateOnly` before computing day counts.

```csharp
/// <summary>
/// Overload for edition-level display (AvailableCampEditionResponse).
/// Uses the edition's WeekendStartDate/WeekendEndDate as the reference window.
/// </summary>
public static int GetPeriodDays(AttendancePeriod period, CampEdition edition)
    => GetPeriodDays(period, edition, visitStart: null, visitEnd: null);

/// <summary>
/// Computes attendance days for a given period.
/// CampEdition.StartDate/EndDate are DateTime; converts via DateOnly.FromDateTime.
/// FirstWeek    = start → halfDate (exclusive of halfDate)
/// SecondWeek   = halfDate → end (exclusive of halfDate)
/// Complete     = start → end (full camp duration)
/// WeekendVisit = visitStart → visitEnd if provided, else edition.WeekendStartDate → WeekendEndDate; max 3 days
/// </summary>
public static int GetPeriodDays(
    AttendancePeriod period, CampEdition edition,
    DateOnly? visitStart, DateOnly? visitEnd)
{
    var startDate = DateOnly.FromDateTime(edition.StartDate);
    var endDate   = DateOnly.FromDateTime(edition.EndDate);
    var totalDays = endDate.DayNumber - startDate.DayNumber;
    var halfDate  = edition.HalfDate ?? startDate.AddDays(totalDays / 2);

    return period switch
    {
        AttendancePeriod.FirstWeek    => halfDate.DayNumber - startDate.DayNumber,
        AttendancePeriod.SecondWeek   => endDate.DayNumber  - halfDate.DayNumber,
        AttendancePeriod.Complete     => totalDays,
        AttendancePeriod.WeekendVisit => ComputeWeekendDays(edition, visitStart, visitEnd),
        _ => throw new ArgumentOutOfRangeException(nameof(period))
    };
}

private static int ComputeWeekendDays(
    CampEdition edition, DateOnly? visitStart, DateOnly? visitEnd)
{
    // Use member-specific dates if provided; fall back to edition defaults
    var start = visitStart ?? edition.WeekendStartDate;
    if (start is null) return 0;
    var end = visitEnd ?? edition.WeekendEndDate ?? start.Value.AddDays(2);
    var days = end.DayNumber - start.Value.DayNumber;
    return Math.Min(3, Math.Max(0, days));  // enforce max 3
}
```

> The test cases are authoritative for exact day semantics. Ensure tests cover:
>
> - Odd-total-days edge case (e.g. 13-day camp: firstWeek=6, secondWeek=7)
> - `WeekendVisit` with null `WeekendEndDate` → defaults to 2 days
> - `WeekendVisit` with `WeekendEndDate` > start + 3 days → capped at 3

#### Changed method: `GetPriceForCategory`

Add `AttendancePeriod period` parameter. The existing callers in `RegistrationsService` must be updated:

```csharp
public decimal GetPriceForCategory(AgeCategory category, AttendancePeriod period, CampEdition edition)
{
    if (period is AttendancePeriod.FirstWeek or AttendancePeriod.SecondWeek)
    {
        if (edition.PricePerAdultWeek is null)
            throw new BusinessRuleException(
                "Esta edición no permite inscripción parcial por semanas");

        return category switch
        {
            AgeCategory.Adult => edition.PricePerAdultWeek.Value,
            AgeCategory.Child => edition.PricePerChildWeek!.Value,
            AgeCategory.Baby  => edition.PricePerBabyWeek!.Value,
            _ => throw new ArgumentOutOfRangeException(nameof(category))
        };
    }

    if (period == AttendancePeriod.WeekendVisit)
    {
        if (edition.PricePerAdultWeekend is null)
            throw new BusinessRuleException(
                "Esta edición no permite visitas de fin de semana");

        return category switch
        {
            AgeCategory.Adult => edition.PricePerAdultWeekend.Value,
            AgeCategory.Child => edition.PricePerChildWeekend!.Value,
            AgeCategory.Baby  => edition.PricePerBabyWeekend!.Value,
            _ => throw new ArgumentOutOfRangeException(nameof(category))
        };
    }

    // Complete: existing logic unchanged
    return category switch
    {
        AgeCategory.Baby  => edition.PricePerBaby,
        AgeCategory.Child => edition.PricePerChild,
        AgeCategory.Adult => edition.PricePerAdult,
        _ => throw new ArgumentOutOfRangeException(nameof(category))
    };
}
```

> **Note**: `BusinessRuleException` in this project takes a single `string message` constructor argument. Do not add an error code parameter without a separate task.

#### `CalculateExtraAmount` — NO CHANGE

`PerDay` extras continue to use the full `campDurationDays`. Intentional simplification.

---

### `IRegistrationsRepository` + `RegistrationsRepository` — `RegistrationsRepository.cs`

Both the interface and implementation are in the same file. Add the new method to both:

**Interface addition:**

```csharp
/// <summary>
/// Counts members (not registrations) on-site for a given period.
/// A Complete member counts toward both FirstWeek and SecondWeek.
/// </summary>
Task<int> CountConcurrentAttendeesByPeriodAsync(
    Guid campEditionId,
    AttendancePeriod period,
    CancellationToken ct
);
```

**Implementation:**

```csharp
public async Task<int> CountConcurrentAttendeesByPeriodAsync(
    Guid campEditionId,
    AttendancePeriod period,
    CancellationToken ct)
    => await db.RegistrationMembers
        .Where(rm =>
            rm.Registration.CampEditionId == campEditionId &&
            rm.Registration.Status != RegistrationStatus.Cancelled &&
            (rm.AttendancePeriod == AttendancePeriod.Complete ||
             rm.AttendancePeriod == period))
        .CountAsync(ct);
```

Keep `CountActiveByEditionAsync` — it is still used by non-capacity logic and `GetAvailableEditionsAsync` (until updated).

---

### `RegistrationsService.cs`

#### `CreateAsync` — changes

1. Replace `foreach (var memberId in request.MemberIds)` with `foreach (var m in request.Members)`
2. For each `MemberAttendanceRequest m`:
   - Validate `m.MemberId` member exists and belongs to the family unit
   - Calculate age, determine `AgeCategory`
   - Get price: `pricingService.GetPriceForCategory(category, m.AttendancePeriod, edition)` ← new signature
   - Set the following on `RegistrationMember`:

     ```csharp
     AttendancePeriod = m.AttendancePeriod,
     VisitStartDate   = m.VisitStartDate,   // ← NEW (null for non-WeekendVisit)
     VisitEndDate     = m.VisitEndDate,     // ← NEW (null for non-WeekendVisit)
     ```

   - Validate visit dates are within camp bounds (for `WeekendVisit` members):

     ```csharp
     if (m.AttendancePeriod == AttendancePeriod.WeekendVisit)
     {
         var campStart = DateOnly.FromDateTime(edition.StartDate);
         var campEnd   = DateOnly.FromDateTime(edition.EndDate);
         if (m.VisitStartDate < campStart || m.VisitEndDate > campEnd)
             throw new BusinessRuleException(
                 "Las fechas de la visita deben estar dentro del periodo del campamento");
     }
     ```

3. Replace the existing single capacity check with the per-period check below

**Updated capacity check** (replaces step 6 in `CreateAsync`):

```csharp
// Week-period capacity check
if (edition.MaxCapacity.HasValue)
{
    foreach (var m in registrationMembers)
    {
        // Skip WeekendVisit — handled separately below
        if (m.AttendancePeriod == AttendancePeriod.WeekendVisit) continue;

        var periodsToCheck = m.AttendancePeriod == AttendancePeriod.Complete
            ? new[] { AttendancePeriod.FirstWeek, AttendancePeriod.SecondWeek }
            : new[] { m.AttendancePeriod };

        foreach (var p in periodsToCheck)
        {
            var count = await registrationsRepo
                .CountConcurrentAttendeesByPeriodAsync(request.CampEditionId, p, ct);
            if (count + 1 > edition.MaxCapacity.Value)
                throw new BusinessRuleException(
                    "El campamento ha alcanzado su capacidad máxima para ese periodo");
        }
    }
}

// Weekend capacity check (separate pool)
var weekendMembersCount = registrationMembers.Count(m =>
    m.AttendancePeriod == AttendancePeriod.WeekendVisit);
if (weekendMembersCount > 0)
{
    var weekendCap = edition.MaxWeekendCapacity ?? edition.MaxCapacity;
    if (weekendCap.HasValue)
    {
        var weekendCount = await registrationsRepo
            .CountConcurrentAttendeesByPeriodAsync(
                request.CampEditionId, AttendancePeriod.WeekendVisit, ct);
        if (weekendCount + weekendMembersCount > weekendCap.Value)
            throw new BusinessRuleException(
                "El campamento ha alcanzado su capacidad máxima para visitas de fin de semana");
    }
}
```

> **Concurrency note**: This check is not atomic under high concurrent load. Add a `// TODO: wrap in REPEATABLE READ transaction for production correctness` comment. Implementing the transaction requires injecting `AbuviDbContext` into the service, which is a separate task.

#### `UpdateMembersAsync` — same changes as `CreateAsync`

Replace `request.MemberIds` with `request.Members`, update pricing call, set `AttendancePeriod`, `VisitStartDate`, `VisitEndDate` on new members. Run both the per-period capacity check and the weekend capacity check against the new members before saving.

#### `GetAvailableEditionsAsync` — changes

Replace the existing `CountActiveByEditionAsync` spotsRemaining logic and populate new fields:

```csharp
// Replace existing spotsRemaining computation:
int? spotsRemaining = null;
if (edition.MaxCapacity.HasValue)
{
    var firstWeekCount = await registrationsRepo
        .CountConcurrentAttendeesByPeriodAsync(edition.Id, AttendancePeriod.FirstWeek, ct);
    var secondWeekCount = await registrationsRepo
        .CountConcurrentAttendeesByPeriodAsync(edition.Id, AttendancePeriod.SecondWeek, ct);
    spotsRemaining = Math.Max(0,
        edition.MaxCapacity.Value - Math.Max(firstWeekCount, secondWeekCount));
}

result.Add(new AvailableCampEditionResponse(
    Id: edition.Id,
    // ... all existing fields ...
    SpotsRemaining: spotsRemaining,
    // ↓ new fields:
    AllowsPartialAttendance: edition.PricePerAdultWeek is not null,
    PricePerAdultWeek: edition.PricePerAdultWeek,
    PricePerChildWeek: edition.PricePerChildWeek,
    PricePerBabyWeek: edition.PricePerBabyWeek,
    HalfDate: edition.HalfDate,
    FirstWeekDays: RegistrationPricingService.GetPeriodDays(AttendancePeriod.FirstWeek, edition),
    SecondWeekDays: RegistrationPricingService.GetPeriodDays(AttendancePeriod.SecondWeek, edition)
));
```

#### New error code

| Situation | HTTP | Message |
|-----------|------|---------|
| Edition does not allow partial attendance but FirstWeek/SecondWeek requested | 422 | `"Esta edición no permite inscripción parcial por semanas"` |
| Capacity exceeded for a period | 422 | `"El campamento ha alcanzado su capacidad máxima para ese periodo"` |

---

### Validator Changes

#### `CreateRegistrationValidator.cs`

The constructor already accepts `ICampEditionsRepository` — keep it. Replace `MemberIds` rule. The validator must look up the `CampEdition` to validate visit dates fall within camp bounds.

```csharp
// REMOVE:
// RuleFor(x => x.MemberIds) ...

// ADD:
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

    // VisitStartDate required when WeekendVisit, must be null otherwise
    member.RuleFor(m => m.VisitStartDate)
        .NotNull().WithMessage("La fecha de inicio de la visita es obligatoria para visitas de fin de semana")
        .When(m => m.AttendancePeriod == AttendancePeriod.WeekendVisit);

    member.RuleFor(m => m.VisitStartDate)
        .Null().WithMessage("La fecha de inicio de la visita solo aplica a visitas de fin de semana")
        .When(m => m.AttendancePeriod != AttendancePeriod.WeekendVisit);

    // VisitEndDate required when WeekendVisit, must be null otherwise
    member.RuleFor(m => m.VisitEndDate)
        .NotNull().WithMessage("La fecha de fin de la visita es obligatoria para visitas de fin de semana")
        .When(m => m.AttendancePeriod == AttendancePeriod.WeekendVisit);

    member.RuleFor(m => m.VisitEndDate)
        .Null().WithMessage("La fecha de fin de la visita solo aplica a visitas de fin de semana")
        .When(m => m.AttendancePeriod != AttendancePeriod.WeekendVisit);

    // Duration ≤ 3 days (pure check, no repo needed)
    member.RuleFor(m => m)
        .Must(m => m.VisitEndDate!.Value.DayNumber - m.VisitStartDate!.Value.DayNumber <= 3)
        .WithMessage("La visita de fin de semana no puede superar los 3 días")
        .WithName("VisitEndDate")
        .When(m => m.AttendancePeriod == AttendancePeriod.WeekendVisit
                   && m.VisitStartDate.HasValue && m.VisitEndDate.HasValue);

    // VisitEndDate must be after VisitStartDate
    member.RuleFor(m => m)
        .Must(m => m.VisitEndDate!.Value > m.VisitStartDate!.Value)
        .WithMessage("La fecha de fin de la visita debe ser posterior a la de inicio")
        .WithName("VisitEndDate")
        .When(m => m.AttendancePeriod == AttendancePeriod.WeekendVisit
                   && m.VisitStartDate.HasValue && m.VisitEndDate.HasValue);
});

// Visit dates within camp bounds (requires edition lookup, so validated via async rule at the service level)
// NOTE: Validator cannot easily do an async member-level check in a RuleForEach with access
// to the parent CampEditionId.  This cross-entity constraint is enforced in RegistrationsService.CreateAsync
// before the RegistrationMember is persisted.
```

#### `UpdateRegistrationMembersValidator.cs`

The constructor is parameterless (no repository). Camp-bound date validation is deferred to the service layer. Same structural rules as above except the null-safety rule for camp dates is a comment:

```csharp
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

    member.RuleFor(m => m.VisitStartDate)
        .NotNull().WithMessage("La fecha de inicio de la visita es obligatoria para visitas de fin de semana")
        .When(m => m.AttendancePeriod == AttendancePeriod.WeekendVisit);

    member.RuleFor(m => m.VisitStartDate)
        .Null().WithMessage("La fecha de inicio de la visita solo aplica a visitas de fin de semana")
        .When(m => m.AttendancePeriod != AttendancePeriod.WeekendVisit);

    member.RuleFor(m => m.VisitEndDate)
        .NotNull().WithMessage("La fecha de fin de la visita es obligatoria para visitas de fin de semana")
        .When(m => m.AttendancePeriod == AttendancePeriod.WeekendVisit);

    member.RuleFor(m => m.VisitEndDate)
        .Null().WithMessage("La fecha de fin de la visita solo aplica a visitas de fin de semana")
        .When(m => m.AttendancePeriod != AttendancePeriod.WeekendVisit);

    member.RuleFor(m => m)
        .Must(m => m.VisitEndDate!.Value.DayNumber - m.VisitStartDate!.Value.DayNumber <= 3)
        .WithMessage("La visita de fin de semana no puede superar los 3 días")
        .WithName("VisitEndDate")
        .When(m => m.AttendancePeriod == AttendancePeriod.WeekendVisit
                   && m.VisitStartDate.HasValue && m.VisitEndDate.HasValue);

    member.RuleFor(m => m)
        .Must(m => m.VisitEndDate!.Value > m.VisitStartDate!.Value)
        .WithMessage("La fecha de fin de la visita debe ser posterior a la de inicio")
        .WithName("VisitEndDate")
        .When(m => m.AttendancePeriod == AttendancePeriod.WeekendVisit
                   && m.VisitStartDate.HasValue && m.VisitEndDate.HasValue);
});
```

---

### `RegistrationMappingExtensions` — `RegistrationsModels.cs`

Update `ToResponse` to populate the new `MemberPricingDetail` fields (use the two-param overload that accepts member-specific visit dates):

```csharp
r.Members.Select(m => new MemberPricingDetail(
    m.FamilyMemberId,
    $"{m.FamilyMember.FirstName} {m.FamilyMember.LastName}",
    m.AgeAtCamp,
    m.AgeCategory,
    m.AttendancePeriod,
    RegistrationPricingService.GetPeriodDays(
        m.AttendancePeriod, r.CampEdition, m.VisitStartDate, m.VisitEndDate),
    m.VisitStartDate,
    m.VisitEndDate,
    m.IndividualAmount)).ToList()
```

---

## `CampEdition` API Changes (Board-facing)

### Actual request record names (codebase differs from original spec)

| Purpose | Actual record in codebase |
|---------|--------------------------|
| Create/propose edition | `ProposeCampEditionRequest` (not `CreateCampEditionRequest`) |
| Update edition | `UpdateCampEditionRequest` |
| Validator for propose | `ProposeCampEditionRequestValidator` in `CampsValidators.cs` |
| Validator for update | `UpdateCampEditionRequestValidator` in `CampsValidators.cs` |

### `ProposeCampEditionRequest` (additive) — `CampsModels.cs`

Add optional parameters with defaults so existing callers are not broken:

```csharp
public record ProposeCampEditionRequest(
    Guid CampId,
    int Year,
    DateTime StartDate,
    DateTime EndDate,
    decimal? PricePerAdult,
    decimal? PricePerChild,
    decimal? PricePerBaby,
    bool UseCustomAgeRanges,
    int? CustomBabyMaxAge,
    int? CustomChildMinAge,
    int? CustomChildMaxAge,
    int? CustomAdultMinAge,
    int? MaxCapacity,
    string? Notes,
    AccommodationCapacity? AccommodationCapacity = null,
    // ↓ NEW optional fields (partial attendance):
    DateOnly? HalfDate = null,
    decimal? PricePerAdultWeek = null,
    decimal? PricePerChildWeek = null,
    decimal? PricePerBabyWeek = null,
    // ↓ NEW optional fields (weekend visit):
    DateOnly? WeekendStartDate = null,
    DateOnly? WeekendEndDate = null,
    decimal? PricePerAdultWeekend = null,
    decimal? PricePerChildWeekend = null,
    decimal? PricePerBabyWeekend = null,
    int? MaxWeekendCapacity = null
);
```

### `UpdateCampEditionRequest` (additive) — `CampsModels.cs`

```csharp
public record UpdateCampEditionRequest(
    DateTime StartDate,
    DateTime EndDate,
    decimal PricePerAdult,
    decimal PricePerChild,
    decimal PricePerBaby,
    bool UseCustomAgeRanges,
    int? CustomBabyMaxAge,
    int? CustomChildMinAge,
    int? CustomChildMaxAge,
    int? CustomAdultMinAge,
    int? MaxCapacity,
    string? Notes,
    // ↓ NEW optional fields (partial attendance):
    DateOnly? HalfDate = null,
    decimal? PricePerAdultWeek = null,
    decimal? PricePerChildWeek = null,
    decimal? PricePerBabyWeek = null,
    // ↓ NEW optional fields (weekend visit):
    DateOnly? WeekendStartDate = null,
    DateOnly? WeekendEndDate = null,
    decimal? PricePerAdultWeekend = null,
    decimal? PricePerChildWeekend = null,
    decimal? PricePerBabyWeekend = null,
    int? MaxWeekendCapacity = null
);
```

### `ProposeCampEditionRequestValidator` + `UpdateCampEditionRequestValidator` — `CampsValidators.cs`

Add the week-pricing group rule and weekend rules to both validators:

```csharp
// ADD to both validators — week pricing group:
RuleFor(x => x)
    .Must(x =>
        (x.PricePerAdultWeek == null && x.PricePerChildWeek == null && x.PricePerBabyWeek == null) ||
        (x.PricePerAdultWeek != null && x.PricePerChildWeek != null && x.PricePerBabyWeek != null))
    .WithMessage("Si se configura precio por semana, todos los precios (adulto, niño, bebé) son obligatorios")
    .WithName("PricePerAdultWeek");

// ADD to both validators — weekend pricing group (all-or-nothing):
RuleFor(x => x)
    .Must(x =>
        (x.PricePerAdultWeekend == null && x.PricePerChildWeekend == null && x.PricePerBabyWeekend == null) ||
        (x.PricePerAdultWeekend != null && x.PricePerChildWeekend != null && x.PricePerBabyWeekend != null))
    .WithMessage("Si se configura precio de fin de semana, todos los precios (adulto, niño, bebé) son obligatorios")
    .WithName("PricePerAdultWeekend");

// If weekend prices set, WeekendStartDate is required:
RuleFor(x => x.WeekendStartDate)
    .NotNull().WithMessage("La fecha de inicio del fin de semana es obligatoria si se configura precio de fin de semana")
    .When(x => x.PricePerAdultWeekend.HasValue);

// WeekendEndDate must be set if WeekendStartDate is set and must be within StartDate..EndDate:
When(x => x.WeekendStartDate.HasValue, () =>
{
    RuleFor(x => x)
        .Must(x => x.WeekendEndDate == null ||
                   x.WeekendEndDate.Value.DayNumber - x.WeekendStartDate!.Value.DayNumber <= 3)
        .WithMessage("El fin de semana no puede superar los 3 días")
        .WithName("WeekendEndDate");

    // Both WeekendStart and WeekendEnd must fall within camp dates
    RuleFor(x => x)
        .Must(x => DateOnly.FromDateTime(x.StartDate) <= x.WeekendStartDate!.Value)
        .WithMessage("La fecha de inicio del fin de semana debe estar dentro del periodo del campamento")
        .WithName("WeekendStartDate");

    RuleFor(x => x)
        .Must(x => x.WeekendEndDate == null ||
                   x.WeekendEndDate.Value <= DateOnly.FromDateTime(x.EndDate))
        .WithMessage("La fecha de fin del fin de semana debe estar dentro del periodo del campamento")
        .WithName("WeekendEndDate");
});

// MaxWeekendCapacity must be > 0 if provided:
RuleFor(x => x.MaxWeekendCapacity)
    .GreaterThan(0).WithMessage("La capacidad máxima del fin de semana debe ser mayor que 0")
    .When(x => x.MaxWeekendCapacity.HasValue);
```

### `CampEditionsService.cs` — update `ProposeAsync` and `UpdateAsync`

Map new fields onto the `CampEdition` entity before saving:

```csharp
edition.HalfDate              = request.HalfDate;
edition.PricePerAdultWeek     = request.PricePerAdultWeek;
edition.PricePerChildWeek     = request.PricePerChildWeek;
edition.PricePerBabyWeek      = request.PricePerBabyWeek;
edition.WeekendStartDate      = request.WeekendStartDate;
edition.WeekendEndDate        = request.WeekendEndDate;
edition.PricePerAdultWeekend  = request.PricePerAdultWeekend;
edition.PricePerChildWeekend  = request.PricePerChildWeekend;
edition.PricePerBabyWeekend   = request.PricePerBabyWeekend;
edition.MaxWeekendCapacity    = request.MaxWeekendCapacity;
```

### `CampEditionResponse` (additive) — `CampsModels.cs`

Add new fields to the Board-facing response so admins can see partial-attendance and weekend configuration:

```csharp
// ADD to CampEditionResponse record:
DateOnly? HalfDate,
decimal? PricePerAdultWeek,
decimal? PricePerChildWeek,
decimal? PricePerBabyWeek,
DateOnly? WeekendStartDate,
DateOnly? WeekendEndDate,
decimal? PricePerAdultWeekend,
decimal? PricePerChildWeekend,
decimal? PricePerBabyWeekend,
int? MaxWeekendCapacity
```

Update the response mapping in `CampEditionsService` to populate these fields.

---

## Files to Create / Modify

### Backend

```
src/Abuvi.API/Features/Registrations/RegistrationsModels.cs
    ← Add AttendancePeriod enum (FirstWeek, SecondWeek, Complete, WeekendVisit)
    ← Add AttendancePeriod + VisitStartDate + VisitEndDate to RegistrationMember
    ← Add MemberAttendanceRequest record (with optional VisitStartDate/VisitEndDate)
    ← Change CreateRegistrationRequest.MemberIds → Members
    ← Change UpdateRegistrationMembersRequest.MemberIds → Members
    ← Add AttendancePeriod + AttendanceDays + VisitStartDate + VisitEndDate to MemberPricingDetail
    ← Add AllowsPartialAttendance + week prices + HalfDate + week day counts to AvailableCampEditionResponse
    ← Add AllowsWeekendVisit + weekend prices + weekend dates + WeekendDays + capacity to AvailableCampEditionResponse
    ← Update RegistrationMappingExtensions.ToResponse: call GetPeriodDays(period, edition, visitStart, visitEnd)

src/Abuvi.API/Features/Registrations/RegistrationPricingService.cs
    ← Add GetPeriodDays as a PUBLIC STATIC method (two overloads)
    ← Update GetPriceForCategory signature: add AttendancePeriod parameter + WeekendVisit branch

src/Abuvi.API/Features/Registrations/RegistrationsService.cs
    ← Update CreateAsync: iterate request.Members, call updated GetPriceForCategory, set AttendancePeriod + VisitStartDate + VisitEndDate
    ← Update CreateAsync: validate visit dates within camp bounds for WeekendVisit members
    ← Update CreateAsync: replace capacity check with per-period check + separate weekend capacity check
    ← Update UpdateMembersAsync: same member iteration, visit date, and capacity changes
    ← Update GetAvailableEditionsAsync: use CountConcurrentAttendeesByPeriodAsync, populate all new fields including weekend

src/Abuvi.API/Features/Registrations/RegistrationsRepository.cs
    ← Add CountConcurrentAttendeesByPeriodAsync to IRegistrationsRepository interface
    ← Implement CountConcurrentAttendeesByPeriodAsync in RegistrationsRepository class

src/Abuvi.API/Features/Registrations/CreateRegistrationValidator.cs
    ← Replace MemberIds rules with Members rules (AttendancePeriod + VisitStartDate/VisitEndDate validation)

src/Abuvi.API/Features/Registrations/UpdateRegistrationMembersValidator.cs
    ← Replace MemberIds rules with Members rules (AttendancePeriod + VisitStartDate/VisitEndDate validation)

src/Abuvi.API/Data/Configurations/RegistrationMemberConfiguration.cs
    ← Add attendance_period column configuration
    ← Add visit_start_date column configuration (date, nullable)
    ← Add visit_end_date column configuration (date, nullable)

src/Abuvi.API/Features/Camps/CampsModels.cs
    ← Add HalfDate + PricePerAdultWeek/Child/Baby to CampEdition entity
    ← Add WeekendStartDate + WeekendEndDate + PricePerAdultWeekend/Child/Baby + MaxWeekendCapacity to CampEdition
    ← Add new optional week + weekend fields to ProposeCampEditionRequest
    ← Add new optional week + weekend fields to UpdateCampEditionRequest
    ← Add week + weekend fields to CampEditionResponse

src/Abuvi.API/Data/Configurations/CampEditionConfiguration.cs
    ← Add 4 week column configurations (HalfDate + 3 week prices)
    ← Add 6 weekend column configurations (WeekendStartDate, WeekendEndDate, 3 weekend prices, MaxWeekendCapacity)

src/Abuvi.API/Features/Camps/CampsValidators.cs
    ← Add week-pricing group rule to ProposeCampEditionRequestValidator and UpdateCampEditionRequestValidator
    ← Add weekend pricing group rule + WeekendStartDate required if prices set + date bounds + duration ≤ 3 days

src/Abuvi.API/Features/Camps/CampEditionsService.cs
    ← Map HalfDate + week pricing fields + weekend fields in ProposeAsync and UpdateAsync
    ← Map new week + weekend fields in CampEditionResponse mapping
```

### Tests (TDD — write tests FIRST, then implement)

```
src/Abuvi.Tests/Unit/Features/Registrations/RegistrationPricingServiceTests.cs
    ← NEW: GetPeriodDays_Complete_ReturnsFullCampDuration
    ← NEW: GetPeriodDays_FirstWeek_WithExplicitHalfDate_ReturnsCorrectDays
    ← NEW: GetPeriodDays_FirstWeek_WithNullHalfDate_UsesComputedMidpoint
    ← NEW: GetPeriodDays_SecondWeek_WithExplicitHalfDate_ReturnsCorrectDays
    ← NEW: GetPeriodDays_SecondWeek_WithNullHalfDate_UsesComputedMidpoint
    ← NEW: GetPeriodDays_WeekendVisit_WithMemberSpecificDates_ReturnsCorrectDays
    ← NEW: GetPeriodDays_WeekendVisit_WithNullMemberDates_FallsBackToEditionDefaults
    ← NEW: GetPeriodDays_WeekendVisit_WhenDurationExceedsThreeDays_CapsAtThree
    ← NEW: GetPeriodDays_WeekendVisit_WithNullWeekendEndDate_DefaultsTwoExtraDays
    ← NEW: GetPriceForCategory_FirstWeek_Adult_ReturnsWeekPrice
    ← NEW: GetPriceForCategory_FirstWeek_WhenNoWeekPriceSet_ThrowsBusinessRuleException
    ← NEW: GetPriceForCategory_WeekendVisit_Adult_ReturnsWeekendPrice
    ← NEW: GetPriceForCategory_WeekendVisit_WhenNoWeekendPriceSet_ThrowsBusinessRuleException
    ← UPDATE: existing GetPriceForCategory_* tests — pass AttendancePeriod.Complete as new arg

src/Abuvi.Tests/Unit/Features/Registrations/RegistrationsServiceTests.cs
    ← UPDATE ALL: replace MemberIds: [MemberId] with
      Members: [new MemberAttendanceRequest(MemberId, AttendancePeriod.Complete)]
    ← UPDATE ALL: replace CountActiveByEditionAsync mock setup with
      CountConcurrentAttendeesByPeriodAsync mock setup
    ← NEW: CreateAsync_WithMixedPeriods_CalculatesPricingPerPeriod
    ← NEW: CreateAsync_WhenPartialAttendanceNotAllowedByEdition_ThrowsBusinessRuleException
    ← NEW: CreateAsync_WhenFirstWeekAtCapacity_ThrowsForFirstWeekMember
    ← NEW: CreateAsync_WhenCompleteMemberWouldExceedEitherPeriod_ThrowsCampFull
    ← NEW: CreateAsync_WithWeekendVisitMember_UsesWeekendPricing
    ← NEW: CreateAsync_WhenWeekendVisitNotAllowedByEdition_ThrowsBusinessRuleException
    ← NEW: CreateAsync_WhenWeekendAtCapacity_ThrowsForWeekendVisitMember
    ← NEW: CreateAsync_WhenVisitDatesOutsideCampBounds_ThrowsBusinessRuleException

src/Abuvi.Tests/Unit/Features/Registrations/CreateRegistrationValidatorTests.cs
    ← UPDATE ALL: replace MemberIds with Members
    ← NEW: Validator_WhenAttendancePeriodIsInvalid_Fails
    ← NEW: Validator_WhenSameMemberAppearsInMembersTwice_Fails
    ← NEW: Validator_WhenMembersIsEmpty_Fails
    ← NEW: Validator_WeekendVisit_WhenVisitStartDateNull_Fails
    ← NEW: Validator_WeekendVisit_WhenVisitEndDateNull_Fails
    ← NEW: Validator_WeekendVisit_WhenDurationExceedsThreeDays_Fails
    ← NEW: Validator_WeekendVisit_WhenEndBeforeStart_Fails
    ← NEW: Validator_NonWeekendVisit_WhenVisitStartDateSet_Fails
    ← NEW: Validator_NonWeekendVisit_WhenVisitEndDateSet_Fails

src/Abuvi.Tests/Helpers/Builders/RegistrationBuilder.cs
    ← ADD: WithWeekPricing(decimal adultWeek, decimal childWeek, decimal babyWeek)
    ← ADD: WithCampEditionHalfDate(DateOnly? halfDate)
    ← ADD: WithWeekendPricing(decimal adultWeekend, decimal childWeekend, decimal babyWeekend)
    ← ADD: WithWeekendDates(DateOnly start, DateOnly end)
    ← ADD: WithMaxWeekendCapacity(int? capacity)
```

---

## TDD Implementation Order

**Phase 1 — `GetPeriodDays` (pure, no dependencies)**

1. Write `GetPeriodDays_Complete_ReturnsFullCampDuration` → RED
2. Add `public static int GetPeriodDays(AttendancePeriod, CampEdition, DateOnly?, DateOnly?)` stub → still RED
3. Implement all period branches including `WeekendVisit` and `ComputeWeekendDays` → GREEN
4. Add FirstWeek/SecondWeek tests with explicit + null `HalfDate` → RED → implement → GREEN
5. Add WeekendVisit tests (member dates / fallback to edition / cap at 3 / null end) → RED → GREEN

**Phase 2 — `GetPriceForCategory` updated signature**

1. Write `GetPriceForCategory_FirstWeek_Adult_ReturnsWeekPrice` → RED
2. Write `GetPriceForCategory_WeekendVisit_Adult_ReturnsWeekendPrice` → RED
3. Update method signature, add week-pricing and weekend-pricing branches → GREEN
4. Update all existing callers to pass `AttendancePeriod.Complete` → compile + GREEN

**Phase 3 — Models + EF Config + Migration**

1. Add `AttendancePeriod` enum (including `WeekendVisit`)
2. Add `VisitStartDate` / `VisitEndDate` to `RegistrationMember`; add all weekend fields to `CampEdition`
3. Add `MemberAttendanceRequest` with optional visit dates
4. Update `CreateRegistrationRequest`, `UpdateRegistrationMembersRequest`, `MemberPricingDetail`, `AvailableCampEditionResponse`
5. Add EF Core config properties for `RegistrationMemberConfiguration` and `CampEditionConfiguration`
6. Generate migration and review SQL

**Phase 4 — Repository**

1. Add `CountConcurrentAttendeesByPeriodAsync` to interface (handles `WeekendVisit` via `rm.AttendancePeriod == period` check)
2. Implement in `RegistrationsRepository`
3. Verify via unit or manual test

**Phase 5 — Service**

1. Fix compile errors in `RegistrationsServiceTests` from DTO change
2. Write new capacity-check tests (week periods + weekend pool) → RED
3. Write `CreateAsync_WhenVisitDatesOutsideCampBounds_ThrowsBusinessRuleException` → RED
4. Write `CreateAsync_WithWeekendVisitMember_UsesWeekendPricing` → RED
5. Update `CreateAsync`, `UpdateMembersAsync`, `GetAvailableEditionsAsync` → GREEN

**Phase 6 — Validators**

1. Write updated validator tests including visit date rules → RED
2. Update `CreateRegistrationValidator` and `UpdateRegistrationMembersValidator` → GREEN

**Phase 7 — Camps feature**

1. Add week + weekend fields to `ProposeCampEditionRequest`, `UpdateCampEditionRequest`, `CampEditionResponse`
2. Add week-pricing + weekend validation rules in `CampsValidators.cs`
3. Map all new fields in `CampEditionsService`

---

## Frontend Changes

### 1. `types/registration.ts` — Changed

```typescript
// ADD new type (includes WeekendVisit):
export type AttendancePeriod = 'FirstWeek' | 'SecondWeek' | 'Complete' | 'WeekendVisit'

// ADD to MemberPricingDetail:
attendancePeriod: AttendancePeriod
attendanceDays: number
visitStartDate: string | null   // ISO date (DateOnly serialized as 'YYYY-MM-DD'); only set for WeekendVisit
visitEndDate: string | null     // same

// ADD to AvailableCampEditionResponse:
allowsPartialAttendance: boolean
pricePerAdultWeek: number | null
pricePerChildWeek: number | null
pricePerBabyWeek: number | null
halfDate: string | null
firstWeekDays: number
secondWeekDays: number
allowsWeekendVisit: boolean
pricePerAdultWeekend: number | null
pricePerChildWeekend: number | null
pricePerBabyWeekend: number | null
weekendStartDate: string | null   // ISO date; UI default for the visit-date picker
weekendEndDate: string | null
weekendDays: number
maxWeekendCapacity: number | null
weekendSpotsRemaining: number | null

// REPLACE in CreateRegistrationRequest:
// OLD: memberIds: string[]
// NEW:
members: MemberAttendanceRequest[]

// ADD new type:
export interface MemberAttendanceRequest {
  memberId: string
  attendancePeriod: AttendancePeriod
  visitStartDate?: string | null   // Required when attendancePeriod === 'WeekendVisit'
  visitEndDate?: string | null     // Required when attendancePeriod === 'WeekendVisit'
}

// REPLACE in UpdateRegistrationMembersRequest:
// OLD: memberIds: string[]
// NEW:
members: MemberAttendanceRequest[]

// ADD wizard-local type:
export interface WizardMemberSelection {
  memberId: string
  attendancePeriod: AttendancePeriod
  visitStartDate?: string | null   // only used when attendancePeriod === 'WeekendVisit'
  visitEndDate?: string | null
}
```

### 2. Attendance Period Utility — `frontend/src/utils/registration.ts`

Check if the file already exists before creating it:

```typescript
import type { AttendancePeriod } from '@/types/registration'

export const ATTENDANCE_PERIOD_LABELS: Record<AttendancePeriod, string> = {
  Complete: 'Campamento completo',
  FirstWeek: 'Primera semana',
  SecondWeek: 'Segunda semana',
  WeekendVisit: 'Visita de fin de semana',
}

export const getAttendancePeriodLabel = (period: AttendancePeriod): string =>
  ATTENDANCE_PERIOD_LABELS[period] ?? period
```

Import in `RegistrationMemberSelector`, `RegistrationPricingBreakdown`, and `RegisterForCampPage`.

### 3. `RegistrationMemberSelector.vue` — Changed

**Props change**:

```typescript
// OLD: modelValue: string[]
// NEW: modelValue: WizardMemberSelection[]
```

**Template**: Each family member row changes from a checkbox to checkbox + period dropdown + optional date pickers:

```
┌──────────────────────────────────────────────────────────────────┐
│ ☑ Juan García · Padre · 12/04/1975                               │
│   Periodo:  [Campamento Completo ▼]                              │
│   ⚠ Notas médicas                                                 │
└──────────────────────────────────────────────────────────────────┘

┌──────────────────────────────────────────────────────────────────┐
│ ☑ Lucía García · Hija · 03/06/2014                               │
│   Periodo:  [Visita de fin de semana ▼]                          │
│   Fecha entrada: [04/07/2025 📅]   Fecha salida: [06/07/2025 📅] │
└──────────────────────────────────────────────────────────────────┘
```

Period dropdown options (only shown when member is checked):

- "Campamento completo" → `Complete` (always visible)
- "Primera semana (X días)" → `FirstWeek` (only if `allowsPartialAttendance`)
- "Segunda semana (X días)" → `SecondWeek` (only if `allowsPartialAttendance`)
- "Visita de fin de semana" → `WeekendVisit` (only if `allowsWeekendVisit`)

Default when member is first checked: `Complete`.

**When `WeekendVisit` is selected**: show two `DatePicker` fields for `visitStartDate` and `visitEndDate`. Pre-populate with `edition.weekendStartDate` and `edition.weekendEndDate` as defaults (user can override). Disable dates outside `[edition.startDate, edition.endDate]`. Enforce that `visitEndDate - visitStartDate ≤ 3 days` with a validation message.

PrimeVue components: `Select` (dropdown), `DatePicker` (for visit dates).

**Emit**: `update:modelValue` emits `WizardMemberSelection[]` (including `visitStartDate`/`visitEndDate` when `WeekendVisit`).

### 4. `RegisterForCampPage.vue` — Changed

```typescript
// OLD:
const selectedMemberIds = ref<string[]>([])

// NEW:
const selectedMembers = ref<WizardMemberSelection[]>([])
```

**Step 2 (Review) pricing guide** — show week columns only if `edition.allowsPartialAttendance`:

```
Precios orientativos:
                 Completo   1ª semana   2ª semana
Adulto/a          180€       110€        110€
Niño/Niña          90€        55€         55€
Bebé                0€         0€          0€
```

**On confirm**:

```typescript
await createRegistration({
  campEditionId: editionId.value,
  familyUnitId: familyUnit.value!.id,
  members: selectedMembers.value.map(m => ({
    memberId: m.memberId,
    attendancePeriod: m.attendancePeriod,
    visitStartDate: m.visitStartDate ?? null,   // ← NEW: only non-null for WeekendVisit
    visitEndDate: m.visitEndDate ?? null         // ← NEW
  })),
  notes: notes.value || null
})
```

### 5. `RegistrationPricingBreakdown.vue` — Changed

Add `AttendancePeriod` column and labels. For `WeekendVisit` members, show visit dates as a sub-row or tooltip:

```
Nombre          Edad  Categoría   Periodo                     Importe
Juan García      49   Adulto/a    Campamento completo          180,00€
Lucía García     12   Niño/Niña   Primera semana                55,00€
Carlos García    35   Adulto/a    Visita fin semana             40,00€
                                  04/07 – 06/07/2025
```

### 6. `RegistrationCard.vue` — No change needed

---

## Frontend Test Coverage

### `RegistrationMemberSelector.test.ts`

- `should show period selector when member is checked`
- `should default to Complete period when member is first checked`
- `should not show FirstWeek/SecondWeek options when allowsPartialAttendance is false`
- `should not show WeekendVisit option when allowsWeekendVisit is false`
- `should show date pickers when WeekendVisit is selected`
- `should pre-populate date pickers with edition weekendStartDate/weekendEndDate defaults`
- `should emit WizardMemberSelection with correct memberId and period`
- `should emit visitStartDate and visitEndDate when WeekendVisit is selected`

### `RegistrationPricingBreakdown.test.ts`

- `should show AttendancePeriod column when members have different periods`
- `should display correct period label for each member`
- `should display visit dates for WeekendVisit members`

---

## Outstanding Decisions

1. **`PerPerson + PerDay` extras split by member period?**
   Current: NO (full camp days). Separate future spec if needed.

2. **Admin dashboard per-period headcounts?**
   Out of scope for this ticket.

3. **Concurrency / transaction for capacity check?**
   Add `// TODO: wrap in REPEATABLE READ transaction for production correctness` and track separately. Requires injecting `AbuviDbContext` into `RegistrationsService`.

---

## Document Control

- **Feature**: `feat-registration-attendance-period`
- **Extends**: `feat-camps-registration` (enriched spec v1.1)
- **Version**: 1.2 (enriched — corrected DTO names, added type clarifications, added AttendanceDays mapping strategy, TDD phases, codebase anchors; added WeekendVisit with per-member VisitStartDate/VisitEndDate)
- **Date**: 2026-02-25
- **Status**: Ready for Backend Implementation (TDD mandatory — write tests first)
- **Priority**: Backend first, then frontend
