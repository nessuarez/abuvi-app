# Backend Implementation Plan: feat-registration-attendance-period — Attendance Periods for Camp Registrations

## Overview

This feature extends the existing camp registration system to support per-member attendance periods. Each family member can independently attend the first period (`FirstWeek`), the second (`SecondWeek`), the complete camp (`Complete`), or a short weekend visit (`WeekendVisit`, max 3 days). Pricing, capacity checks, and response DTOs are all updated accordingly.

**Architecture**: Vertical Slice Architecture — changes spread across `Features/Registrations/` and `Features/Camps/` slices, plus EF Core configuration and shared test infrastructure.

**Spec file**: `ai-specs/changes/feat-camps-registration-period/feat-registration-attendance-period_enriched.md`

**Depends on**: `feat-camps-registration` (backend — already merged on `main`)

---

## Architecture Context

### Feature Slices Affected

- `src/Abuvi.API/Features/Registrations/` — primary slice
- `src/Abuvi.API/Features/Camps/` — additive changes (new fields on `CampEdition`)
- `src/Abuvi.API/Data/Configurations/` — EF Core config for both slices

### Files to Modify

| File | Change Type |
|------|-------------|
| `RegistrationsModels.cs` | Add enum, modify entities + DTOs, update mapping |
| `RegistrationPricingService.cs` | Add `GetPeriodDays`, update `GetPriceForCategory` signature |
| `RegistrationsService.cs` | Update `CreateAsync`, `UpdateMembersAsync`, `GetAvailableEditionsAsync` |
| `RegistrationsRepository.cs` | Add `CountConcurrentAttendeesByPeriodAsync` to interface + impl |
| `CreateRegistrationValidator.cs` | Replace `MemberIds` rules with `Members` rules |
| `UpdateRegistrationMembersValidator.cs` | Replace `MemberIds` rules with `Members` rules |
| `Data/Configurations/RegistrationMemberConfiguration.cs` | Add 3 new column configs |
| `Features/Camps/CampsModels.cs` | Add fields to `CampEdition`, `ProposeCampEditionRequest`, `UpdateCampEditionRequest`, `CampEditionResponse` |
| `Data/Configurations/CampEditionConfiguration.cs` | Add 10 new column configs |
| `Features/Camps/CampsValidators.cs` | Add week + weekend pricing group rules to both validators |
| `Features/Camps/CampEditionsService.cs` | Map new fields in `ProposeAsync`, `UpdateAsync`, `CampEditionResponse` mapping |

### Test Files to Modify/Create

| File | Change Type |
|------|-------------|
| `Tests/Unit/Features/Registrations/RegistrationPricingServiceTests.cs` | Add `GetPeriodDays` tests + update `GetPriceForCategory` tests |
| `Tests/Unit/Features/Registrations/RegistrationsServiceTests.cs` | Migrate `MemberIds` → `Members`, add new capacity + weekend tests |
| `Tests/Unit/Features/Registrations/CreateRegistrationValidatorTests.cs` | Migrate `MemberIds` → `Members`, add `AttendancePeriod` tests |
| `Tests/Helpers/Builders/RegistrationBuilder.cs` | Add week/weekend pricing builder methods |

---

## Implementation Steps

### Step 0: Create Feature Branch

- **Action**: Create and switch to a new feature branch
- **Branch name**: `feature/feat-registration-attendance-period-backend`
- **Important**: The current branch `feature/feat-open-edition-amendments-backend` is for a different feature. Do NOT work on that branch for this ticket.
- **Implementation Steps**:
  1. `git checkout main`
  2. `git pull origin main`
  3. `git checkout -b feature/feat-registration-attendance-period-backend`
  4. `git branch` — verify you are on the new branch

---

### Step 1: Extend `RegistrationBuilder.cs` (Test Infrastructure — needed for TDD)

- **File**: `src/Abuvi.Tests/Helpers/Builders/RegistrationBuilder.cs`
- **Action**: Add builder methods for new pricing fields on `CampEdition`
- **Implementation Steps**:

  Add these fluent builder methods to `RegistrationBuilder` after `WithMaxCapacity`:

  ```csharp
  public RegistrationBuilder WithWeekPricing(decimal adultWeek, decimal childWeek, decimal babyWeek)
  {
      _edition.PricePerAdultWeek = adultWeek;
      _edition.PricePerChildWeek = childWeek;
      _edition.PricePerBabyWeek = babyWeek;
      return this;
  }

  public RegistrationBuilder WithCampEditionHalfDate(DateOnly? halfDate)
  {
      _edition.HalfDate = halfDate;
      return this;
  }

  public RegistrationBuilder WithWeekendPricing(decimal adultWeekend, decimal childWeekend, decimal babyWeekend)
  {
      _edition.PricePerAdultWeekend = adultWeekend;
      _edition.PricePerChildWeekend = childWeekend;
      _edition.PricePerBabyWeekend = babyWeekend;
      return this;
  }

  public RegistrationBuilder WithWeekendDates(DateOnly start, DateOnly end)
  {
      _edition.WeekendStartDate = start;
      _edition.WeekendEndDate = end;
      return this;
  }

  public RegistrationBuilder WithMaxWeekendCapacity(int? capacity)
  {
      _edition.MaxWeekendCapacity = capacity;
      return this;
  }
  ```

  **Note**: `_edition` is the `CampEdition` object inside the builder. Check the existing builder to confirm the field name. These builder methods reference new properties on `CampEdition` (Step 6 adds them). The builder will not compile until Step 6 is done — but write it now (RED phase is OK for partial compilation).

---

### Step 2: Phase 1 TDD — `GetPeriodDays` Tests (RED First)

- **File**: `src/Abuvi.Tests/Unit/Features/Registrations/RegistrationPricingServiceTests.cs`
- **Action**: Add tests for the new `GetPeriodDays` static method **before** implementing the method
- **Implementation Steps**:

  Add a new test class `GetPeriodDaysTests` nested within or alongside the existing test class. All tests reference `RegistrationPricingService.GetPeriodDays(...)` which does not exist yet (these will be RED):

  ```csharp
  public class GetPeriodDaysTests
  {
      private static CampEdition CreateEdition(
          DateTime start, DateTime end, DateOnly? halfDate = null,
          DateOnly? weekendStart = null, DateOnly? weekendEnd = null)
          => new CampEdition
          {
              StartDate = start,
              EndDate = end,
              HalfDate = halfDate,
              WeekendStartDate = weekendStart,
              WeekendEndDate = weekendEnd,
              // required fields — must satisfy entity defaults
              PricePerAdult = 180m, PricePerChild = 90m, PricePerBaby = 0m,
              Status = CampEditionStatus.Open,
          };

      [Fact]
      public void GetPeriodDays_Complete_ReturnsFullCampDuration()
      {
          var edition = CreateEdition(
              new DateTime(2025, 7, 1), new DateTime(2025, 7, 15));

          var result = RegistrationPricingService.GetPeriodDays(
              AttendancePeriod.Complete, edition);

          result.Should().Be(14);
      }

      [Fact]
      public void GetPeriodDays_FirstWeek_WithExplicitHalfDate_ReturnsCorrectDays()
      {
          var edition = CreateEdition(
              new DateTime(2025, 7, 1), new DateTime(2025, 7, 15),
              halfDate: new DateOnly(2025, 7, 8));

          var result = RegistrationPricingService.GetPeriodDays(
              AttendancePeriod.FirstWeek, edition);

          result.Should().Be(7); // July 1 → July 8 = 7 days
      }

      [Fact]
      public void GetPeriodDays_SecondWeek_WithExplicitHalfDate_ReturnsCorrectDays()
      {
          var edition = CreateEdition(
              new DateTime(2025, 7, 1), new DateTime(2025, 7, 15),
              halfDate: new DateOnly(2025, 7, 8));

          var result = RegistrationPricingService.GetPeriodDays(
              AttendancePeriod.SecondWeek, edition);

          result.Should().Be(7); // July 8 → July 15 = 7 days
      }

      [Fact]
      public void GetPeriodDays_FirstWeek_WithNullHalfDate_UsesComputedMidpoint()
      {
          // 14-day camp: midpoint at day 7
          var edition = CreateEdition(
              new DateTime(2025, 7, 1), new DateTime(2025, 7, 15));
          // halfDate computed = startDate.AddDays(14/2) = July 8

          var result = RegistrationPricingService.GetPeriodDays(
              AttendancePeriod.FirstWeek, edition);

          result.Should().Be(7);
      }

      [Fact]
      public void GetPeriodDays_SecondWeek_WithNullHalfDate_UsesComputedMidpoint()
      {
          // 14-day camp: midpoint at day 7
          var edition = CreateEdition(
              new DateTime(2025, 7, 1), new DateTime(2025, 7, 15));

          var result = RegistrationPricingService.GetPeriodDays(
              AttendancePeriod.SecondWeek, edition);

          result.Should().Be(7);
      }

      [Fact]
      public void GetPeriodDays_FirstWeek_WithOddTotalDays_RoundsDown()
      {
          // 13-day camp: halfDate = startDate.AddDays(13/2 = 6) → firstWeek=6, secondWeek=7
          var edition = CreateEdition(
              new DateTime(2025, 7, 1), new DateTime(2025, 7, 14));

          var first = RegistrationPricingService.GetPeriodDays(
              AttendancePeriod.FirstWeek, edition);
          var second = RegistrationPricingService.GetPeriodDays(
              AttendancePeriod.SecondWeek, edition);

          first.Should().Be(6);
          second.Should().Be(7);
      }

      [Fact]
      public void GetPeriodDays_WeekendVisit_WithMemberSpecificDates_ReturnsCorrectDays()
      {
          var edition = CreateEdition(
              new DateTime(2025, 7, 1), new DateTime(2025, 7, 15));

          var result = RegistrationPricingService.GetPeriodDays(
              AttendancePeriod.WeekendVisit, edition,
              visitStart: new DateOnly(2025, 7, 4),
              visitEnd: new DateOnly(2025, 7, 6));

          result.Should().Be(2); // July 4 → July 6 = 2 days diff
      }

      [Fact]
      public void GetPeriodDays_WeekendVisit_WithNullMemberDates_FallsBackToEditionDefaults()
      {
          var edition = CreateEdition(
              new DateTime(2025, 7, 1), new DateTime(2025, 7, 15),
              weekendStart: new DateOnly(2025, 7, 5),
              weekendEnd: new DateOnly(2025, 7, 7));

          var result = RegistrationPricingService.GetPeriodDays(
              AttendancePeriod.WeekendVisit, edition,
              visitStart: null, visitEnd: null);

          result.Should().Be(2);
      }

      [Fact]
      public void GetPeriodDays_WeekendVisit_WhenDurationExceedsThreeDays_CapsAtThree()
      {
          var edition = CreateEdition(
              new DateTime(2025, 7, 1), new DateTime(2025, 7, 15));

          var result = RegistrationPricingService.GetPeriodDays(
              AttendancePeriod.WeekendVisit, edition,
              visitStart: new DateOnly(2025, 7, 4),
              visitEnd: new DateOnly(2025, 7, 10)); // 6-day diff → capped at 3

          result.Should().Be(3);
      }

      [Fact]
      public void GetPeriodDays_WeekendVisit_WithNullWeekendEndDate_DefaultsTwoExtraDays()
      {
          // visitEnd = null and edition.WeekendEndDate = null → end = start + 2
          var edition = CreateEdition(
              new DateTime(2025, 7, 1), new DateTime(2025, 7, 15),
              weekendStart: new DateOnly(2025, 7, 5),
              weekendEnd: null); // no end date → defaults to start + 2

          var result = RegistrationPricingService.GetPeriodDays(
              AttendancePeriod.WeekendVisit, edition,
              visitStart: null, visitEnd: null);

          result.Should().Be(2);
      }
  }
  ```

  **Note**: The single-overload call `GetPeriodDays(period, edition)` (no `visitStart`/`visitEnd` params) is used for edition-level display. Add two separate test helper calls — both static overloads (edition-only and edition+dates) should be tested.

---

### Step 3: Phase 2 TDD — `GetPriceForCategory` Updated Signature Tests (RED First)

- **File**: `src/Abuvi.Tests/Unit/Features/Registrations/RegistrationPricingServiceTests.cs`
- **Action**: Add tests for week/weekend pricing branches in `GetPriceForCategory` and update existing tests to pass `AttendancePeriod.Complete`
- **Implementation Steps**:

  1. **Add new tests** (will be RED until Step 5):

  ```csharp
  [Fact]
  public void GetPriceForCategory_FirstWeek_Adult_ReturnsWeekPrice()
  {
      var edition = new CampEditionBuilder()
          .WithWeekPricing(adultWeek: 110m, childWeek: 55m, babyWeek: 0m)
          .Build();

      var result = _pricingService.GetPriceForCategory(
          AgeCategory.Adult, AttendancePeriod.FirstWeek, edition);

      result.Should().Be(110m);
  }

  [Fact]
  public void GetPriceForCategory_SecondWeek_Child_ReturnsWeekPrice()
  {
      var edition = new CampEditionBuilder()
          .WithWeekPricing(adultWeek: 110m, childWeek: 55m, babyWeek: 0m)
          .Build();

      var result = _pricingService.GetPriceForCategory(
          AgeCategory.Child, AttendancePeriod.SecondWeek, edition);

      result.Should().Be(55m);
  }

  [Fact]
  public void GetPriceForCategory_FirstWeek_WhenNoWeekPriceSet_ThrowsBusinessRuleException()
  {
      var edition = new CampEditionBuilder().Build(); // PricePerAdultWeek = null

      var act = () => _pricingService.GetPriceForCategory(
          AgeCategory.Adult, AttendancePeriod.FirstWeek, edition);

      act.Should().Throw<BusinessRuleException>()
         .WithMessage("Esta edición no permite inscripción parcial por semanas");
  }

  [Fact]
  public void GetPriceForCategory_WeekendVisit_Adult_ReturnsWeekendPrice()
  {
      var edition = new CampEditionBuilder()
          .WithWeekendPricing(adultWeekend: 40m, childWeekend: 20m, babyWeekend: 0m)
          .Build();

      var result = _pricingService.GetPriceForCategory(
          AgeCategory.Adult, AttendancePeriod.WeekendVisit, edition);

      result.Should().Be(40m);
  }

  [Fact]
  public void GetPriceForCategory_WeekendVisit_WhenNoWeekendPriceSet_ThrowsBusinessRuleException()
  {
      var edition = new CampEditionBuilder().Build(); // PricePerAdultWeekend = null

      var act = () => _pricingService.GetPriceForCategory(
          AgeCategory.Adult, AttendancePeriod.WeekendVisit, edition);

      act.Should().Throw<BusinessRuleException>()
         .WithMessage("Esta edición no permite visitas de fin de semana");
  }
  ```

  2. **Update all existing `GetPriceForCategory_*` tests** — add `AttendancePeriod.Complete` as the second argument:
     - Find all calls like `_pricingService.GetPriceForCategory(AgeCategory.Adult, edition)` and change to `_pricingService.GetPriceForCategory(AgeCategory.Adult, AttendancePeriod.Complete, edition)`
     - Look for `CampEditionBuilder` usage in the existing tests — it may be a local helper or from `RegistrationBuilder`. Confirm the existing test pattern and follow it exactly.

  **Note**: Existing tests use `CampEditionBuilder` (check the existing test file carefully) or build the edition inline. Whichever pattern is used, follow it consistently.

---

### Step 4: Phase 1+2 Implementation — `RegistrationPricingService.cs`

- **File**: `src/Abuvi.API/Features/Registrations/RegistrationPricingService.cs`
- **Action**: Add `GetPeriodDays` (public static, two overloads) and update `GetPriceForCategory` signature
- **Implementation Steps**:

  1. **Add `AttendancePeriod` enum** — this comes from `RegistrationsModels.cs` (Step 6), so `RegistrationPricingService.cs` depends on it. The enum must be defined first in Step 6 before the service compiles. For now, write the service code and accept compilation failure until Step 6 lands.

  2. **Add `GetPeriodDays` static methods** at the end of the class (before the closing brace):

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

  3. **Update `GetPriceForCategory` signature** — add `AttendancePeriod period` as the second parameter:

  ```csharp
  // BEFORE:
  public decimal GetPriceForCategory(AgeCategory category, CampEdition edition)

  // AFTER:
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

  **Important**: `BusinessRuleException` takes a single `string message` constructor argument. Do not add additional parameters.

  4. After updating the signature, fix all internal callers in the same class (if any call `GetPriceForCategory` internally). Then fix callers in `RegistrationsService.cs` (Step 9).

---

### Step 5: Phase 3 — Models: Add `AttendancePeriod` Enum, Extend Entities + DTOs

- **File**: `src/Abuvi.API/Features/Registrations/RegistrationsModels.cs`
- **Action**: Add enum, extend `RegistrationMember`, add `MemberAttendanceRequest`, change `CreateRegistrationRequest` and `UpdateRegistrationMembersRequest`, update `MemberPricingDetail` and `AvailableCampEditionResponse`
- **Implementation Steps**:

  **5a. Add `AttendancePeriod` enum** (alongside existing enums like `RegistrationStatus`, `AgeCategory`):

  ```csharp
  public enum AttendancePeriod
  {
      FirstWeek,
      SecondWeek,
      Complete,
      WeekendVisit   // Short visit, max 3 days, configurable window
  }
  ```

  **5b. Extend `RegistrationMember` entity** — add three new properties after `IndividualAmount`:

  ```csharp
  public AttendancePeriod AttendancePeriod { get; set; }  // defaults to Complete
  // Only populated when AttendancePeriod = WeekendVisit
  public DateOnly? VisitStartDate { get; set; }
  public DateOnly? VisitEndDate { get; set; }
  ```

  **5c. Add `MemberAttendanceRequest` record** (new, near the other request records):

  ```csharp
  public record MemberAttendanceRequest(
      Guid MemberId,
      AttendancePeriod AttendancePeriod,
      DateOnly? VisitStartDate = null,   // Required when AttendancePeriod = WeekendVisit
      DateOnly? VisitEndDate = null      // Required when AttendancePeriod = WeekendVisit
  );
  ```

  **5d. Change `CreateRegistrationRequest`** — breaking change, replace `List<Guid> MemberIds` with `List<MemberAttendanceRequest> Members`:

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
      List<MemberAttendanceRequest> Members,
      string? Notes
  );
  ```

  **5e. Change `UpdateRegistrationMembersRequest`** — breaking change:

  ```csharp
  // BEFORE:
  public record UpdateRegistrationMembersRequest(List<Guid> MemberIds);

  // AFTER:
  public record UpdateRegistrationMembersRequest(List<MemberAttendanceRequest> Members);
  ```

  **5f. Update `MemberPricingDetail` record** — additive, add four new fields:

  ```csharp
  // AFTER (full record):
  public record MemberPricingDetail(
      Guid FamilyMemberId,
      string FullName,
      int AgeAtCamp,
      AgeCategory AgeCategory,
      AttendancePeriod AttendancePeriod,   // NEW
      int AttendanceDays,                  // NEW (computed from period and edition dates)
      DateOnly? VisitStartDate,            // NEW (only set for WeekendVisit)
      DateOnly? VisitEndDate,              // NEW (only set for WeekendVisit)
      decimal IndividualAmount
  );
  ```

  **5g. Update `AvailableCampEditionResponse` record** — additive, add all new fields:

  ```csharp
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
      // NEW:
      bool AllowsPartialAttendance,       // true if PricePerAdultWeek is set
      decimal? PricePerAdultWeek,
      decimal? PricePerChildWeek,
      decimal? PricePerBabyWeek,
      DateOnly? HalfDate,
      int FirstWeekDays,                  // computed via GetPeriodDays
      int SecondWeekDays,                 // computed via GetPeriodDays
      bool AllowsWeekendVisit,            // true if WeekendStartDate + weekend prices set
      decimal? PricePerAdultWeekend,
      decimal? PricePerChildWeekend,
      decimal? PricePerBabyWeekend,
      DateOnly? WeekendStartDate,
      DateOnly? WeekendEndDate,
      int WeekendDays,                    // computed via GetPeriodDays
      int? MaxWeekendCapacity,
      int? WeekendSpotsRemaining
  );
  ```

  **5h. Update `RegistrationMappingExtensions.ToResponse`** — update the `MemberPricingDetail` projection:

  ```csharp
  r.Members.Select(m => new MemberPricingDetail(
      m.FamilyMemberId,
      $"{m.FamilyMember.FirstName} {m.FamilyMember.LastName}",
      m.AgeAtCamp,
      m.AgeCategory,
      m.AttendancePeriod,                                           // NEW
      RegistrationPricingService.GetPeriodDays(                     // NEW
          m.AttendancePeriod, r.CampEdition, m.VisitStartDate, m.VisitEndDate),
      m.VisitStartDate,                                             // NEW
      m.VisitEndDate,                                               // NEW
      m.IndividualAmount)).ToList()
  ```

  Note: `RegistrationMappingExtensions.ToResponse` is a static extension method — it calls `RegistrationPricingService.GetPeriodDays` which is `public static`, so no DI is needed.

---

### Step 6: Phase 3 — Extend `CampEdition` Entity

- **File**: `src/Abuvi.API/Features/Camps/CampsModels.cs`
- **Action**: Add new fields to `CampEdition` entity and update request/response DTOs
- **Implementation Steps**:

  **6a. Extend `CampEdition` entity** — add after `PricePerBaby` field:

  ```csharp
  // Period split point
  public DateOnly? HalfDate { get; set; }           // null = computed midpoint

  // Per-period pricing (one period = FirstWeek or SecondWeek)
  public decimal? PricePerAdultWeek { get; set; }   // null = partial attendance not allowed
  public decimal? PricePerChildWeek { get; set; }
  public decimal? PricePerBabyWeek { get; set; }

  // Weekend visit window (max 3 days)
  public DateOnly? WeekendStartDate { get; set; }   // null = weekend visit not allowed
  public DateOnly? WeekendEndDate { get; set; }

  // Weekend visit pricing
  public decimal? PricePerAdultWeekend { get; set; }  // null = weekend visit not allowed
  public decimal? PricePerChildWeekend { get; set; }
  public decimal? PricePerBabyWeekend { get; set; }

  // Weekend visit capacity (optional separate cap; if null, uses MaxCapacity)
  public int? MaxWeekendCapacity { get; set; }
  ```

  **6b. Update `ProposeCampEditionRequest`** — add optional parameters with defaults (append after `AccommodationCapacity`):

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
      // NEW optional fields (partial attendance):
      DateOnly? HalfDate = null,
      decimal? PricePerAdultWeek = null,
      decimal? PricePerChildWeek = null,
      decimal? PricePerBabyWeek = null,
      // NEW optional fields (weekend visit):
      DateOnly? WeekendStartDate = null,
      DateOnly? WeekendEndDate = null,
      decimal? PricePerAdultWeekend = null,
      decimal? PricePerChildWeekend = null,
      decimal? PricePerBabyWeekend = null,
      int? MaxWeekendCapacity = null
  );
  ```

  **6c. Update `UpdateCampEditionRequest`** — same new optional fields appended:

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
      // NEW optional fields:
      DateOnly? HalfDate = null,
      decimal? PricePerAdultWeek = null,
      decimal? PricePerChildWeek = null,
      decimal? PricePerBabyWeek = null,
      DateOnly? WeekendStartDate = null,
      DateOnly? WeekendEndDate = null,
      decimal? PricePerAdultWeekend = null,
      decimal? PricePerChildWeekend = null,
      decimal? PricePerBabyWeekend = null,
      int? MaxWeekendCapacity = null
  );
  ```

  **6d. Update `CampEditionResponse`** — add new fields (board-facing response):

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

  Note: Append these at the end of the positional record. Check the existing `CampEditionResponse` record signature in `CampsModels.cs` and append AFTER all existing fields. This is additive (backward-compatible from the DB side; callers in `CampEditionsService.cs` will need updating in Step 11).

---

### Step 7: Phase 3 — EF Core Configuration

- **File 1**: `src/Abuvi.API/Data/Configurations/RegistrationMemberConfiguration.cs`
- **Action**: Add three new column configs after `IndividualAmount`
- **Implementation Steps**:

  ```csharp
  builder.Property(m => m.AttendancePeriod)
      .HasConversion<string>()
      .IsRequired()
      .HasMaxLength(15)
      .HasColumnName("attendance_period")
      .HasDefaultValue(AttendancePeriod.Complete);

  builder.Property(m => m.VisitStartDate)
      .HasColumnName("visit_start_date")
      .HasColumnType("date");

  builder.Property(m => m.VisitEndDate)
      .HasColumnName("visit_end_date")
      .HasColumnType("date");
  ```

- **File 2**: `src/Abuvi.API/Data/Configurations/CampEditionConfiguration.cs`
- **Action**: Add 10 new column configs after the existing `PricePerBaby` check constraint

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

### Step 8: Phase 3 — EF Core Migration

- **Action**: Generate the migration after all entity changes are in place
- **Commands**:

  ```bash
  dotnet ef migrations add AddAttendancePeriodToRegistrations --project src/Abuvi.API
  dotnet ef database update --project src/Abuvi.API
  ```

- **Expected SQL** (verify in generated migration file):

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

  `VARCHAR(15)` covers `'WeekendVisit'` (12 chars). Existing rows: `attendance_period` defaults to `'Complete'`, date columns default null — fully backward compatible.

---

### Step 9: Phase 4 — Repository

- **File**: `src/Abuvi.API/Features/Registrations/RegistrationsRepository.cs`
- **Action**: Add `CountConcurrentAttendeesByPeriodAsync` to both the interface AND the implementation (they are in the same file)
- **Implementation Steps**:

  **Interface addition** (add to `IRegistrationsRepository`):

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

  **Implementation** (add to `RegistrationsRepository` class):

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

  **Important**: Keep `CountActiveByEditionAsync` — it is still referenced by other code paths. Do NOT remove it.

---

### Step 10: Phase 5 TDD — `RegistrationsServiceTests.cs` (Write Tests FIRST)

- **File**: `src/Abuvi.Tests/Unit/Features/Registrations/RegistrationsServiceTests.cs`
- **Action**: Migrate existing tests from `MemberIds` to `Members`, replace `CountActiveByEditionAsync` mock setup with `CountConcurrentAttendeesByPeriodAsync`, add new capacity + weekend + visit-date tests
- **Implementation Steps**:

  **10a. Fix broken existing tests** — update all `CreateRegistrationRequest` instantiations:

  ```csharp
  // BEFORE (every existing test that builds a CreateRegistrationRequest):
  new CreateRegistrationRequest(CampEditionId: ..., FamilyUnitId: ...,
      MemberIds: [memberId], Notes: null)

  // AFTER:
  new CreateRegistrationRequest(CampEditionId: ..., FamilyUnitId: ...,
      Members: [new MemberAttendanceRequest(memberId, AttendancePeriod.Complete)],
      Notes: null)
  ```

  **10b. Fix broken `UpdateRegistrationMembersRequest` usages**:

  ```csharp
  // BEFORE:
  new UpdateRegistrationMembersRequest(MemberIds: [memberId])

  // AFTER:
  new UpdateRegistrationMembersRequest(Members: [new MemberAttendanceRequest(memberId, AttendancePeriod.Complete)])
  ```

  **10c. Fix mock setup for capacity checks** — replace `CountActiveByEditionAsync` mock with `CountConcurrentAttendeesByPeriodAsync`:

  ```csharp
  // BEFORE (old mock — one call):
  _registrationsRepo.CountActiveByEditionAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
      .Returns(0);

  // AFTER — mock returns 0 for any period:
  _registrationsRepo.CountConcurrentAttendeesByPeriodAsync(
      Arg.Any<Guid>(), Arg.Any<AttendancePeriod>(), Arg.Any<CancellationToken>())
      .Returns(0);
  ```

  **10d. Add new tests (all RED until Step 11)**:

  ```csharp
  [Fact]
  public async Task CreateAsync_WithFirstWeekMember_UsesWeekPricing()
  {
      // Arrange
      var memberId = Guid.NewGuid();
      var edition = new RegistrationBuilder()
          .WithWeekPricing(adultWeek: 110m, childWeek: 55m, babyWeek: 0m)
          .BuildEdition(); // check existing builder API, adapt accordingly
      // ... setup family, member (Adult), mock pricing service or configure real service
      var request = new CreateRegistrationRequest(
          CampEditionId: edition.Id,
          FamilyUnitId: _familyUnit.Id,
          Members: [new MemberAttendanceRequest(memberId, AttendancePeriod.FirstWeek)],
          Notes: null);

      // Act
      var result = await _service.CreateAsync(request, _userId, ct);

      // Assert
      result.Members.Single().IndividualAmount.Should().Be(110m);
      result.Members.Single().AttendancePeriod.Should().Be(AttendancePeriod.FirstWeek);
  }

  [Fact]
  public async Task CreateAsync_WhenPartialAttendanceNotAllowedByEdition_ThrowsBusinessRuleException()
  {
      // Edition has no PricePerAdultWeek
      var request = new CreateRegistrationRequest(
          CampEditionId: _campEditionId,
          FamilyUnitId: _familyUnit.Id,
          Members: [new MemberAttendanceRequest(_memberId, AttendancePeriod.FirstWeek)],
          Notes: null);

      var act = async () => await _service.CreateAsync(request, _userId, ct);

      await act.Should().ThrowAsync<BusinessRuleException>()
          .WithMessage("Esta edición no permite inscripción parcial por semanas");
  }

  [Fact]
  public async Task CreateAsync_WhenFirstWeekAtCapacity_ThrowsBusinessRuleException()
  {
      _registrationsRepo.CountConcurrentAttendeesByPeriodAsync(
              _campEditionId, AttendancePeriod.FirstWeek, Arg.Any<CancellationToken>())
          .Returns(50); // at MaxCapacity
      var edition = /* edition with MaxCapacity = 50 */;

      var request = new CreateRegistrationRequest(
          CampEditionId: _campEditionId,
          FamilyUnitId: _familyUnit.Id,
          Members: [new MemberAttendanceRequest(_memberId, AttendancePeriod.FirstWeek)],
          Notes: null);

      var act = async () => await _service.CreateAsync(request, _userId, ct);

      await act.Should().ThrowAsync<BusinessRuleException>()
          .WithMessage("El campamento ha alcanzado su capacidad máxima para ese periodo");
  }

  [Fact]
  public async Task CreateAsync_WhenCompleteMemberWouldExceedEitherPeriod_ThrowsBusinessRuleException()
  {
      // SecondWeek already at MaxCapacity
      _registrationsRepo.CountConcurrentAttendeesByPeriodAsync(
              _campEditionId, AttendancePeriod.FirstWeek, Arg.Any<CancellationToken>())
          .Returns(0);
      _registrationsRepo.CountConcurrentAttendeesByPeriodAsync(
              _campEditionId, AttendancePeriod.SecondWeek, Arg.Any<CancellationToken>())
          .Returns(50);

      var request = new CreateRegistrationRequest(
          CampEditionId: _campEditionId,
          FamilyUnitId: _familyUnit.Id,
          Members: [new MemberAttendanceRequest(_memberId, AttendancePeriod.Complete)],
          Notes: null);

      var act = async () => await _service.CreateAsync(request, _userId, ct);

      await act.Should().ThrowAsync<BusinessRuleException>()
          .WithMessage("El campamento ha alcanzado su capacidad máxima para ese periodo");
  }

  [Fact]
  public async Task CreateAsync_WithWeekendVisitMember_UsesWeekendPricing()
  {
      var edition = /* edition with WeekendStartDate set and PricePerAdultWeekend = 40m */;
      var request = new CreateRegistrationRequest(
          CampEditionId: edition.Id,
          FamilyUnitId: _familyUnit.Id,
          Members: [new MemberAttendanceRequest(
              _memberId, AttendancePeriod.WeekendVisit,
              VisitStartDate: new DateOnly(2025, 7, 4),
              VisitEndDate: new DateOnly(2025, 7, 6))],
          Notes: null);

      var result = await _service.CreateAsync(request, _userId, ct);

      result.Members.Single().IndividualAmount.Should().Be(40m);
  }

  [Fact]
  public async Task CreateAsync_WhenWeekendVisitNotAllowedByEdition_ThrowsBusinessRuleException()
  {
      // Edition has no WeekendStartDate / PricePerAdultWeekend
      var request = new CreateRegistrationRequest(
          CampEditionId: _campEditionId,
          FamilyUnitId: _familyUnit.Id,
          Members: [new MemberAttendanceRequest(
              _memberId, AttendancePeriod.WeekendVisit,
              VisitStartDate: new DateOnly(2025, 7, 4),
              VisitEndDate: new DateOnly(2025, 7, 6))],
          Notes: null);

      var act = async () => await _service.CreateAsync(request, _userId, ct);

      await act.Should().ThrowAsync<BusinessRuleException>()
          .WithMessage("Esta edición no permite visitas de fin de semana");
  }

  [Fact]
  public async Task CreateAsync_WhenWeekendAtCapacity_ThrowsBusinessRuleException()
  {
      _registrationsRepo.CountConcurrentAttendeesByPeriodAsync(
              _campEditionId, AttendancePeriod.WeekendVisit, Arg.Any<CancellationToken>())
          .Returns(20);
      // edition MaxWeekendCapacity = 20

      var request = new CreateRegistrationRequest(
          CampEditionId: _campEditionId,
          FamilyUnitId: _familyUnit.Id,
          Members: [new MemberAttendanceRequest(
              _memberId, AttendancePeriod.WeekendVisit,
              VisitStartDate: new DateOnly(2025, 7, 4),
              VisitEndDate: new DateOnly(2025, 7, 6))],
          Notes: null);

      var act = async () => await _service.CreateAsync(request, _userId, ct);

      await act.Should().ThrowAsync<BusinessRuleException>()
          .WithMessage("El campamento ha alcanzado su capacidad máxima para visitas de fin de semana");
  }

  [Fact]
  public async Task CreateAsync_WhenVisitDatesOutsideCampBounds_ThrowsBusinessRuleException()
  {
      // Camp: July 1–15. Visit: June 30 → July 2 (start before camp start)
      var request = new CreateRegistrationRequest(
          CampEditionId: _campEditionId,
          FamilyUnitId: _familyUnit.Id,
          Members: [new MemberAttendanceRequest(
              _memberId, AttendancePeriod.WeekendVisit,
              VisitStartDate: new DateOnly(2025, 6, 30),
              VisitEndDate: new DateOnly(2025, 7, 2))],
          Notes: null);

      var act = async () => await _service.CreateAsync(request, _userId, ct);

      await act.Should().ThrowAsync<BusinessRuleException>()
          .WithMessage("Las fechas de la visita deben estar dentro del periodo del campamento");
  }
  ```

  **Implementation Note**: Follow the exact AAA structure and field naming conventions used in existing tests. Look at how `_campEditionId`, `_familyUnit`, `_memberId` are defined in the test class constructor and follow that pattern.

---

### Step 11: Phase 5 — Update `RegistrationsService.cs`

- **File**: `src/Abuvi.API/Features/Registrations/RegistrationsService.cs`
- **Action**: Update `CreateAsync`, `UpdateMembersAsync`, `GetAvailableEditionsAsync`
- **Implementation Steps**:

  **11a. Update `CreateAsync`**:

  1. Replace `foreach (var memberId in request.MemberIds)` with `foreach (var m in request.Members)`
  2. Inside the loop:
     - Change member lookup to use `m.MemberId` instead of `memberId`
     - Change pricing call to pass `m.AttendancePeriod`: `pricingService.GetPriceForCategory(category, m.AttendancePeriod, edition)`
     - Set new fields on the `RegistrationMember` entity:
       ```csharp
       AttendancePeriod = m.AttendancePeriod,
       VisitStartDate   = m.VisitStartDate,
       VisitEndDate     = m.VisitEndDate,
       ```
     - Add visit-date validation for `WeekendVisit` members (after member lookup, before adding to list):
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

  3. Replace the existing single capacity check with the per-period check:

     ```csharp
     // TODO: wrap in REPEATABLE READ transaction for production correctness
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

  **11b. Update `UpdateMembersAsync`** — same changes as `CreateAsync`:
  - Replace `request.MemberIds` with `request.Members`
  - Add `AttendancePeriod`, `VisitStartDate`, `VisitEndDate` to new member entities
  - Add visit-date validation for `WeekendVisit` members
  - Replace capacity check with per-period check + weekend capacity check (same code as `CreateAsync`)

  **11c. Update `GetAvailableEditionsAsync`** — replace `CountActiveByEditionAsync` logic and populate new fields:

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

  Then populate the `AvailableCampEditionResponse` constructor with all new fields:

  ```csharp
  // (keep all existing fields, then add new ones:)
  AllowsPartialAttendance: edition.PricePerAdultWeek is not null,
  PricePerAdultWeek: edition.PricePerAdultWeek,
  PricePerChildWeek: edition.PricePerChildWeek,
  PricePerBabyWeek: edition.PricePerBabyWeek,
  HalfDate: edition.HalfDate,
  FirstWeekDays: RegistrationPricingService.GetPeriodDays(AttendancePeriod.FirstWeek, edition),
  SecondWeekDays: RegistrationPricingService.GetPeriodDays(AttendancePeriod.SecondWeek, edition),
  AllowsWeekendVisit: edition.WeekendStartDate.HasValue && edition.PricePerAdultWeekend.HasValue,
  PricePerAdultWeekend: edition.PricePerAdultWeekend,
  PricePerChildWeekend: edition.PricePerChildWeekend,
  PricePerBabyWeekend: edition.PricePerBabyWeekend,
  WeekendStartDate: edition.WeekendStartDate,
  WeekendEndDate: edition.WeekendEndDate,
  WeekendDays: RegistrationPricingService.GetPeriodDays(AttendancePeriod.WeekendVisit, edition),
  MaxWeekendCapacity: edition.MaxWeekendCapacity,
  WeekendSpotsRemaining: weekendSpotsRemaining
  ```

---

### Step 12: Phase 6 TDD — Validator Tests (Write FIRST)

- **File**: `src/Abuvi.Tests/Unit/Features/Registrations/CreateRegistrationValidatorTests.cs`
- **Action**: Migrate existing tests from `MemberIds` to `Members`, add new `AttendancePeriod` + visit date tests
- **Implementation Steps**:

  **12a. Fix existing tests** — replace all `MemberIds: [memberId]` with `Members: [new MemberAttendanceRequest(memberId, AttendancePeriod.Complete)]`

  **12b. Add new tests** (will be RED until Step 13):

  ```csharp
  [Fact]
  public async Task Validator_WhenMembersIsEmpty_Fails()
  {
      var request = ValidRequest() with { Members = [] };
      var result = await _validator.ValidateAsync(request);
      result.IsValid.Should().BeFalse();
      result.Errors.Should().Contain(e => e.ErrorMessage == "Debe seleccionar al menos un miembro de la familia");
  }

  [Fact]
  public async Task Validator_WhenSameMemberAppearsInMembersTwice_Fails()
  {
      var memberId = Guid.NewGuid();
      var request = ValidRequest() with
      {
          Members = [
              new MemberAttendanceRequest(memberId, AttendancePeriod.Complete),
              new MemberAttendanceRequest(memberId, AttendancePeriod.Complete)
          ]
      };
      var result = await _validator.ValidateAsync(request);
      result.IsValid.Should().BeFalse();
      result.Errors.Should().Contain(e => e.ErrorMessage == "No se puede incluir el mismo miembro dos veces");
  }

  [Fact]
  public async Task Validator_WeekendVisit_WhenVisitStartDateNull_Fails()
  {
      var request = ValidRequest() with
      {
          Members = [new MemberAttendanceRequest(Guid.NewGuid(),
              AttendancePeriod.WeekendVisit, VisitStartDate: null, VisitEndDate: new DateOnly(2025, 7, 6))]
      };
      var result = await _validator.ValidateAsync(request);
      result.IsValid.Should().BeFalse();
      result.Errors.Should().Contain(e =>
          e.ErrorMessage == "La fecha de inicio de la visita es obligatoria para visitas de fin de semana");
  }

  [Fact]
  public async Task Validator_WeekendVisit_WhenVisitEndDateNull_Fails()
  {
      var request = ValidRequest() with
      {
          Members = [new MemberAttendanceRequest(Guid.NewGuid(),
              AttendancePeriod.WeekendVisit, VisitStartDate: new DateOnly(2025, 7, 4), VisitEndDate: null)]
      };
      var result = await _validator.ValidateAsync(request);
      result.IsValid.Should().BeFalse();
      result.Errors.Should().Contain(e =>
          e.ErrorMessage == "La fecha de fin de la visita es obligatoria para visitas de fin de semana");
  }

  [Fact]
  public async Task Validator_WeekendVisit_WhenDurationExceedsThreeDays_Fails()
  {
      var request = ValidRequest() with
      {
          Members = [new MemberAttendanceRequest(Guid.NewGuid(),
              AttendancePeriod.WeekendVisit,
              VisitStartDate: new DateOnly(2025, 7, 1),
              VisitEndDate: new DateOnly(2025, 7, 5))] // 4 days > 3
      };
      var result = await _validator.ValidateAsync(request);
      result.IsValid.Should().BeFalse();
      result.Errors.Should().Contain(e =>
          e.ErrorMessage == "La visita de fin de semana no puede superar los 3 días");
  }

  [Fact]
  public async Task Validator_WeekendVisit_WhenEndBeforeStart_Fails()
  {
      var request = ValidRequest() with
      {
          Members = [new MemberAttendanceRequest(Guid.NewGuid(),
              AttendancePeriod.WeekendVisit,
              VisitStartDate: new DateOnly(2025, 7, 6),
              VisitEndDate: new DateOnly(2025, 7, 4))] // end before start
      };
      var result = await _validator.ValidateAsync(request);
      result.IsValid.Should().BeFalse();
      result.Errors.Should().Contain(e =>
          e.ErrorMessage == "La fecha de fin de la visita debe ser posterior a la de inicio");
  }

  [Fact]
  public async Task Validator_NonWeekendVisit_WhenVisitStartDateSet_Fails()
  {
      var request = ValidRequest() with
      {
          Members = [new MemberAttendanceRequest(Guid.NewGuid(),
              AttendancePeriod.Complete,
              VisitStartDate: new DateOnly(2025, 7, 4), VisitEndDate: null)]
      };
      var result = await _validator.ValidateAsync(request);
      result.IsValid.Should().BeFalse();
      result.Errors.Should().Contain(e =>
          e.ErrorMessage == "La fecha de inicio de la visita solo aplica a visitas de fin de semana");
  }

  [Fact]
  public async Task Validator_NonWeekendVisit_WhenVisitEndDateSet_Fails()
  {
      var request = ValidRequest() with
      {
          Members = [new MemberAttendanceRequest(Guid.NewGuid(),
              AttendancePeriod.Complete,
              VisitStartDate: null, VisitEndDate: new DateOnly(2025, 7, 6))]
      };
      var result = await _validator.ValidateAsync(request);
      result.IsValid.Should().BeFalse();
      result.Errors.Should().Contain(e =>
          e.ErrorMessage == "La fecha de fin de la visita solo aplica a visitas de fin de semana");
  }
  ```

---

### Step 13: Phase 6 — Update Validators

- **File 1**: `src/Abuvi.API/Features/Registrations/CreateRegistrationValidator.cs`
- **Action**: Replace `MemberIds` rules with `Members` rules
- **Implementation Steps**:

  Remove the existing `RuleFor(x => x.MemberIds)` block. Add new rules:

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

      // Duration ≤ 3 days
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
  // Visit dates within camp bounds: validated in RegistrationsService.CreateAsync (cross-entity constraint)
  ```

- **File 2**: `src/Abuvi.API/Features/Registrations/UpdateRegistrationMembersValidator.cs`
- **Action**: Same structural changes as above (no repository injection — no async camp lookup)

  Replace existing `MemberIds` rules with the same `Members` rules (identical to `CreateRegistrationValidator` rules above, minus the async `CampEditionId` check which was specific to create).

---

### Step 14: Phase 7 — Update `CampsValidators.cs`

- **File**: `src/Abuvi.API/Features/Camps/CampsValidators.cs`
- **Action**: Add week-pricing group rule and weekend rules to BOTH `ProposeCampEditionRequestValidator` AND `UpdateCampEditionRequestValidator`
- **Implementation Steps**:

  Add to both validators (in their respective `RuleFor` blocks):

  ```csharp
  // Week pricing group — all-or-nothing:
  RuleFor(x => x)
      .Must(x =>
          (x.PricePerAdultWeek == null && x.PricePerChildWeek == null && x.PricePerBabyWeek == null) ||
          (x.PricePerAdultWeek != null && x.PricePerChildWeek != null && x.PricePerBabyWeek != null))
      .WithMessage("Si se configura precio por semana, todos los precios (adulto, niño, bebé) son obligatorios")
      .WithName("PricePerAdultWeek");

  // Weekend pricing group — all-or-nothing:
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

  // WeekendEndDate and date bounds:
  When(x => x.WeekendStartDate.HasValue, () =>
  {
      RuleFor(x => x)
          .Must(x => x.WeekendEndDate == null ||
                     x.WeekendEndDate.Value.DayNumber - x.WeekendStartDate!.Value.DayNumber <= 3)
          .WithMessage("El fin de semana no puede superar los 3 días")
          .WithName("WeekendEndDate");

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

  RuleFor(x => x.MaxWeekendCapacity)
      .GreaterThan(0).WithMessage("La capacidad máxima del fin de semana debe ser mayor que 0")
      .When(x => x.MaxWeekendCapacity.HasValue);
  ```

---

### Step 15: Phase 7 — Update `CampEditionsService.cs`

- **File**: `src/Abuvi.API/Features/Camps/CampEditionsService.cs`
- **Action**: Map new fields in `ProposeAsync`, `UpdateAsync`, and `CampEditionResponse` mapping
- **Implementation Steps**:

  **15a. In `ProposeAsync`** — after setting existing edition fields, add:

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

  **15b. In `UpdateAsync`** — same 10 field assignments (identical to `ProposeAsync`)

  **15c. In `CampEditionResponse` mapping** — add the 10 new fields in the response constructor call:

  ```csharp
  HalfDate: edition.HalfDate,
  PricePerAdultWeek: edition.PricePerAdultWeek,
  PricePerChildWeek: edition.PricePerChildWeek,
  PricePerBabyWeek: edition.PricePerBabyWeek,
  WeekendStartDate: edition.WeekendStartDate,
  WeekendEndDate: edition.WeekendEndDate,
  PricePerAdultWeekend: edition.PricePerAdultWeekend,
  PricePerChildWeekend: edition.PricePerChildWeekend,
  PricePerBabyWeekend: edition.PricePerBabyWeekend,
  MaxWeekendCapacity: edition.MaxWeekendCapacity,
  ```

  Note: There may be multiple places in `CampEditionsService.cs` where a `CampEditionResponse` is constructed (e.g., in `ProposeAsync`, `UpdateAsync`, `GetByIdAsync`). Find all occurrences and update each one.

---

### Step 16: Documentation Update

- **Action**: Update technical documentation to reflect all schema and API changes
- **Implementation Steps**:

  1. **`ai-specs/specs/data-model.md`** (if it exists): Add `AttendancePeriod` enum, new `RegistrationMember` fields (`attendance_period`, `visit_start_date`, `visit_end_date`), new `CampEdition` fields (10 new columns)

  2. **`ai-specs/specs/api-spec.yml`** (if it exists): Update `AvailableCampEditionResponse` and `MemberPricingDetail` schemas to include new fields; update `CreateRegistrationRequest` and `UpdateRegistrationMembersRequest` to use `members` instead of `memberIds`

  3. **OpenAPI auto-generated docs**: Run the project to verify the Swagger UI reflects all new fields correctly

  4. **Notes**: All documentation must be in English

---

## Implementation Order

1. **Step 0**: Create feature branch `feature/feat-registration-attendance-period-backend`
2. **Step 1**: Extend `RegistrationBuilder.cs` (test infrastructure)
3. **Step 2**: Write `GetPeriodDays` tests (RED — Phase 1 TDD)
4. **Step 3**: Write updated `GetPriceForCategory` tests (RED — Phase 2 TDD)
5. **Step 4**: Implement `GetPeriodDays` + updated `GetPriceForCategory` in `RegistrationPricingService.cs` (GREEN)
6. **Step 5**: Add `AttendancePeriod` enum + extend models/DTOs in `RegistrationsModels.cs` (Phase 3)
7. **Step 6**: Extend `CampEdition` entity + request/response records in `CampsModels.cs` (Phase 3)
8. **Step 7**: Update EF Core configurations (Phase 3)
9. **Step 8**: Generate EF Core migration
10. **Step 9**: Add `CountConcurrentAttendeesByPeriodAsync` to repository (Phase 4)
11. **Step 10**: Write `RegistrationsService` tests (RED — Phase 5 TDD)
12. **Step 11**: Update `RegistrationsService.cs` (GREEN)
13. **Step 12**: Write updated validator tests (RED — Phase 6 TDD)
14. **Step 13**: Update `CreateRegistrationValidator.cs` + `UpdateRegistrationMembersValidator.cs` (GREEN)
15. **Step 14**: Update `CampsValidators.cs` (Phase 7)
16. **Step 15**: Update `CampEditionsService.cs` (Phase 7)
17. **Step 16**: Update technical documentation

---

## Testing Checklist

### Unit Tests — `RegistrationPricingServiceTests.cs`
- [ ] `GetPeriodDays_Complete_ReturnsFullCampDuration`
- [ ] `GetPeriodDays_FirstWeek_WithExplicitHalfDate_ReturnsCorrectDays`
- [ ] `GetPeriodDays_FirstWeek_WithNullHalfDate_UsesComputedMidpoint`
- [ ] `GetPeriodDays_SecondWeek_WithExplicitHalfDate_ReturnsCorrectDays`
- [ ] `GetPeriodDays_SecondWeek_WithNullHalfDate_UsesComputedMidpoint`
- [ ] `GetPeriodDays_FirstWeek_WithOddTotalDays_RoundsDown` (13-day camp: firstWeek=6, secondWeek=7)
- [ ] `GetPeriodDays_WeekendVisit_WithMemberSpecificDates_ReturnsCorrectDays`
- [ ] `GetPeriodDays_WeekendVisit_WithNullMemberDates_FallsBackToEditionDefaults`
- [ ] `GetPeriodDays_WeekendVisit_WhenDurationExceedsThreeDays_CapsAtThree`
- [ ] `GetPeriodDays_WeekendVisit_WithNullWeekendEndDate_DefaultsTwoExtraDays`
- [ ] `GetPriceForCategory_FirstWeek_Adult_ReturnsWeekPrice`
- [ ] `GetPriceForCategory_SecondWeek_Child_ReturnsWeekPrice`
- [ ] `GetPriceForCategory_FirstWeek_WhenNoWeekPriceSet_ThrowsBusinessRuleException`
- [ ] `GetPriceForCategory_WeekendVisit_Adult_ReturnsWeekendPrice`
- [ ] `GetPriceForCategory_WeekendVisit_WhenNoWeekendPriceSet_ThrowsBusinessRuleException`
- [ ] All existing `GetPriceForCategory_*` tests updated with `AttendancePeriod.Complete`

### Unit Tests — `RegistrationsServiceTests.cs`
- [ ] All existing tests migrated from `MemberIds` to `Members`
- [ ] `CountConcurrentAttendeesByPeriodAsync` mock setup replaces `CountActiveByEditionAsync`
- [ ] `CreateAsync_WithFirstWeekMember_UsesWeekPricing`
- [ ] `CreateAsync_WhenPartialAttendanceNotAllowedByEdition_ThrowsBusinessRuleException`
- [ ] `CreateAsync_WhenFirstWeekAtCapacity_ThrowsBusinessRuleException`
- [ ] `CreateAsync_WhenCompleteMemberWouldExceedEitherPeriod_ThrowsBusinessRuleException`
- [ ] `CreateAsync_WithWeekendVisitMember_UsesWeekendPricing`
- [ ] `CreateAsync_WhenWeekendVisitNotAllowedByEdition_ThrowsBusinessRuleException`
- [ ] `CreateAsync_WhenWeekendAtCapacity_ThrowsBusinessRuleException`
- [ ] `CreateAsync_WhenVisitDatesOutsideCampBounds_ThrowsBusinessRuleException`

### Unit Tests — `CreateRegistrationValidatorTests.cs`
- [ ] All existing tests migrated from `MemberIds` to `Members`
- [ ] `Validator_WhenMembersIsEmpty_Fails`
- [ ] `Validator_WhenSameMemberAppearsInMembersTwice_Fails`
- [ ] `Validator_WeekendVisit_WhenVisitStartDateNull_Fails`
- [ ] `Validator_WeekendVisit_WhenVisitEndDateNull_Fails`
- [ ] `Validator_WeekendVisit_WhenDurationExceedsThreeDays_Fails`
- [ ] `Validator_WeekendVisit_WhenEndBeforeStart_Fails`
- [ ] `Validator_NonWeekendVisit_WhenVisitStartDateSet_Fails`
- [ ] `Validator_NonWeekendVisit_WhenVisitEndDateSet_Fails`

---

## Error Response Format

All errors use `ApiResponse<T>` envelope (existing pattern, no changes needed):

```json
{
  "success": false,
  "error": {
    "code": "BUSINESS_RULE_VIOLATION",
    "message": "Esta edición no permite inscripción parcial por semanas"
  }
}
```

HTTP status mapping:
- `422 Unprocessable Entity` — `BusinessRuleException` (capacity exceeded, partial attendance not allowed, visit dates out of bounds)
- `400 Bad Request` — FluentValidation failures (invalid `AttendancePeriod`, null visit dates for `WeekendVisit`, duration > 3 days)
- `404 Not Found` — camp edition / family member not found

---

## Breaking Changes

| Location | Before | After | Impact |
|----------|--------|-------|--------|
| `CreateRegistrationRequest` | `List<Guid> MemberIds` | `List<MemberAttendanceRequest> Members` | All callers must update |
| `UpdateRegistrationMembersRequest` | `List<Guid> MemberIds` | `List<MemberAttendanceRequest> Members` | All callers must update |
| `GetPriceForCategory` signature | `(AgeCategory, CampEdition)` | `(AgeCategory, AttendancePeriod, CampEdition)` | Internal — fixed in Step 11 |
| `MemberPricingDetail` record | 5 fields | 9 fields | Frontend consumers must handle new fields |
| `AvailableCampEditionResponse` record | 14 fields | 29 fields | Frontend consumers must handle new fields |

---

## Dependencies

### NuGet packages
No new NuGet packages required. All changes use existing dependencies:
- `Microsoft.EntityFrameworkCore` (EF Core, existing)
- `FluentValidation` (existing)

### Migration command
```bash
dotnet ef migrations add AddAttendancePeriodToRegistrations --project src/Abuvi.API
dotnet ef database update --project src/Abuvi.API
```

---

## Notes

### Business Rules
1. `FirstWeek`/`SecondWeek` attendance requires `PricePerAdultWeek` to be set on the edition; otherwise throws `BusinessRuleException`
2. `WeekendVisit` requires `PricePerAdultWeekend` to be set on the edition; otherwise throws `BusinessRuleException`
3. `WeekendVisit` members require `VisitStartDate` and `VisitEndDate` in the request
4. Visit dates must fall within `[edition.StartDate, edition.EndDate]`
5. Weekend duration: `VisitEndDate - VisitStartDate ≤ 3 days` (DayNumber difference)
6. `Complete` members count toward BOTH `FirstWeek` and `SecondWeek` capacity
7. Weekend capacity is tracked independently from week capacity
8. `MaxWeekendCapacity`, if null, falls back to `MaxCapacity` for weekend checks
9. Existing registrations with `Complete` attendance remain fully backward compatible (column defaults to `'Complete'`)

### Language Requirements
- All validation error messages in **Spanish** (as per existing project convention)
- All code (variable names, methods, comments) in **English**
- Test names in **English**

### Concurrency
- Capacity checks are NOT atomic under high concurrent load
- Add `// TODO: wrap in REPEATABLE READ transaction for production correctness` comment at the capacity check location in `RegistrationsService.cs`
- Full transaction support requires injecting `AbuviDbContext` into the service — tracked as a separate task

### `CampEditionResponse` vs `AvailableCampEditionResponse`
- `CampEditionResponse` is the **board-facing** response (admins see full configuration)
- `AvailableCampEditionResponse` is the **family-facing** response (only Open editions, includes week/weekend pricing and availability info)
- Both need updating but in different files (`CampsModels.cs` and `RegistrationsModels.cs` respectively)

### `CountActiveByEditionAsync`
Do NOT remove from the repository interface or implementation. It may be used by other service methods not covered in this spec. Verify no other callers remain after updating `GetAvailableEditionsAsync`, but keep the method regardless for backward compatibility.

---

## Next Steps After Backend Implementation

1. Frontend implementation following `feat-registration-attendance-period_enriched.md` frontend section
2. API integration testing (manual or Testcontainers-based)
3. Transaction wrapping for capacity checks (separate task — requires `AbuviDbContext` injection)
4. Admin dashboard per-period headcounts (out of scope for this ticket)

---

## Implementation Verification

### Code Quality
- [ ] C# nullable reference types handled (`DateOnly?`, `decimal?` etc.)
- [ ] No compiler warnings introduced
- [ ] `public static` on `GetPeriodDays` (required for use in static extension method `ToResponse`)

### Functionality
- [ ] `GET /registrations/available` returns all new fields including weekend info
- [ ] `POST /registrations` accepts `members` with `AttendancePeriod` per member
- [ ] `PUT /registrations/{id}/members` accepts `members` list
- [ ] Pricing correctly uses week/weekend prices when `AttendancePeriod` is not `Complete`
- [ ] Capacity checks correctly handle `Complete` (counts as both weeks) and weekend pool

### Testing
- [ ] All existing tests still pass (no regression)
- [ ] 90%+ unit test coverage
- [ ] Tests follow `MethodName_StateUnderTest_ExpectedBehavior` naming
- [ ] AAA pattern used throughout

### Integration
- [ ] EF Core migration generates correct SQL (verify `UP` migration has all 13 new columns)
- [ ] `dotnet build` passes with 0 errors, 0 warnings
- [ ] `dotnet test` passes all tests

### Documentation
- [ ] `ai-specs/specs/data-model.md` updated
- [ ] `ai-specs/specs/api-spec.yml` updated
