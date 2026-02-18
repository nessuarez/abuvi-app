# Backend Implementation Plan: feat-camps-registration — Camp Registration Flow

## Overview

This plan implements the camp registration workflow for ABUVI following **Vertical Slice Architecture**.
A new `Registrations` feature slice is created containing all entities, DTOs, validators, service, repository, and endpoints needed to allow family representatives to register for open camp editions, select extras, and track payments.

The feature depends on the already-implemented `Camps` slice (`CampEdition`, `CampEditionExtra`) and `FamilyUnits` slice (`FamilyUnit`, `FamilyMember`).

Refer to the enriched spec at `ai-specs/changes/feat-camps-registration_enriched.md` for full business rules.

---

## Architecture Context

**Feature slice**: `src/Abuvi.API/Features/Registrations/`

**Cross-cutting concerns affected**:

- `src/Abuvi.API/Data/AbuviDbContext.cs` — add 4 new `DbSet<T>` properties
- `src/Abuvi.API/Program.cs` — register services and map endpoints
- 4 new EF Core entity configurations in `src/Abuvi.API/Data/Configurations/`
- 1 new EF Core migration

---

## Implementation Steps

---

### Step 0: Create Feature Branch

- **Action**: Create and switch to a dedicated backend feature branch.
- **Branch name**: `feature/feat-camps-registration-backend`
- **Implementation Steps**:
  1. `git checkout main && git pull origin main`
  2. `git checkout -b feature/feat-camps-registration-backend`
  3. `git branch` — verify new branch is active
- **Note**: Do NOT work on the general `feat-camps-registration` branch. Backend and frontend concerns must be separated.

---

### Step 1: Add Entities and Enums to RegistrationsModels.cs

- **File**: `src/Abuvi.API/Features/Registrations/RegistrationsModels.cs`
- **Action**: Create all domain entities, enums, and DTOs for this slice.
- **Namespace**: `Abuvi.API.Features.Registrations`
- **Implementation Steps**:

  1. **Domain entities** — add these classes:

     ```csharp
     public class Registration
     {
         public Guid Id { get; set; }
         public Guid FamilyUnitId { get; set; }
         public Guid CampEditionId { get; set; }
         public Guid RegisteredByUserId { get; set; }
         public decimal BaseTotalAmount { get; set; }
         public decimal ExtrasAmount { get; set; }
         public decimal TotalAmount { get; set; }
         public RegistrationStatus Status { get; set; } = RegistrationStatus.Pending;
         public string? Notes { get; set; }
         public DateTime CreatedAt { get; set; }
         public DateTime UpdatedAt { get; set; }

         // Navigation properties
         public FamilyUnit FamilyUnit { get; set; } = null!;     // from FamilyUnits namespace
         public CampEdition CampEdition { get; set; } = null!;   // from Camps namespace
         public User RegisteredByUser { get; set; } = null!;     // from Users namespace
         public ICollection<RegistrationMember> Members { get; set; } = [];
         public ICollection<RegistrationExtra> Extras { get; set; } = [];
         public ICollection<Payment> Payments { get; set; } = [];
     }

     public class RegistrationMember
     {
         public Guid Id { get; set; }
         public Guid RegistrationId { get; set; }
         public Guid FamilyMemberId { get; set; }
         public int AgeAtCamp { get; set; }
         public AgeCategory AgeCategory { get; set; }
         public decimal IndividualAmount { get; set; }
         public DateTime CreatedAt { get; set; }
         public Registration Registration { get; set; } = null!;
         public FamilyMember FamilyMember { get; set; } = null!;  // from FamilyUnits namespace
     }

     public class RegistrationExtra
     {
         public Guid Id { get; set; }
         public Guid RegistrationId { get; set; }
         public Guid CampEditionExtraId { get; set; }
         public int Quantity { get; set; }
         public decimal UnitPrice { get; set; }         // price snapshot at selection time
         public int CampDurationDays { get; set; }      // camp duration snapshot
         public decimal TotalAmount { get; set; }
         public DateTime CreatedAt { get; set; }
         public Registration Registration { get; set; } = null!;
         public CampEditionExtra CampEditionExtra { get; set; } = null!; // from Camps namespace
     }

     public class Payment
     {
         public Guid Id { get; set; }
         public Guid RegistrationId { get; set; }
         public decimal Amount { get; set; }
         public DateTime PaymentDate { get; set; }
         public PaymentMethod Method { get; set; }
         public PaymentStatus Status { get; set; } = PaymentStatus.Pending;
         public string? ExternalReference { get; set; }
         public DateTime CreatedAt { get; set; }
         public DateTime UpdatedAt { get; set; }
         public Registration Registration { get; set; } = null!;
     }
     ```

  2. **Enums** — add in the same file:

     ```csharp
     public enum RegistrationStatus { Pending, Confirmed, Cancelled }
     public enum AgeCategory { Baby, Child, Adult }
     public enum PaymentMethod { Card, Transfer, Cash }
     public enum PaymentStatus { Pending, Completed, Failed, Refunded }
     ```

  3. **Request DTOs** — records:

     ```csharp
     public record CreateRegistrationRequest(
         Guid CampEditionId,
         Guid FamilyUnitId,
         List<Guid> MemberIds,
         string? Notes
     );

     public record UpdateRegistrationMembersRequest(List<Guid> MemberIds);

     public record UpdateRegistrationExtrasRequest(List<ExtraSelectionRequest> Extras);

     public record ExtraSelectionRequest(Guid CampEditionExtraId, int Quantity);
     ```

  4. **Response DTOs** — records:

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
         AgeRangesInfo AgeRanges
     );

     public record AgeRangesInfo(int BabyMaxAge, int ChildMinAge, int ChildMaxAge, int AdultMinAge);

     public record RegistrationResponse(
         Guid Id,
         RegistrationFamilyUnitSummary FamilyUnit,
         RegistrationCampEditionSummary CampEdition,
         RegistrationStatus Status,
         string? Notes,
         PricingBreakdown Pricing,
         List<PaymentSummary> Payments,
         decimal AmountPaid,
         decimal AmountRemaining,
         DateTime CreatedAt,
         DateTime UpdatedAt
     );

     public record RegistrationFamilyUnitSummary(Guid Id, string Name);
     public record RegistrationCampEditionSummary(Guid Id, string CampName, int Year, DateTime StartDate, DateTime EndDate, int Duration);
     public record PricingBreakdown(
         List<MemberPricingDetail> Members,
         decimal BaseTotalAmount,
         List<ExtraPricingDetail> Extras,
         decimal ExtrasAmount,
         decimal TotalAmount
     );
     public record MemberPricingDetail(Guid FamilyMemberId, string FullName, int AgeAtCamp, AgeCategory AgeCategory, decimal IndividualAmount);
     public record ExtraPricingDetail(Guid CampEditionExtraId, string Name, decimal UnitPrice, string PricingType, string PricingPeriod, int Quantity, int? CampDurationDays, string Calculation, decimal TotalAmount);
     public record PaymentSummary(Guid Id, decimal Amount, DateTime PaymentDate, string Method, string Status);

     // Lightweight response for list endpoints (no full pricing breakdown)
     public record RegistrationListResponse(
         Guid Id,
         RegistrationFamilyUnitSummary FamilyUnit,
         RegistrationCampEditionSummary CampEdition,
         RegistrationStatus Status,
         decimal TotalAmount,
         decimal AmountPaid,
         decimal AmountRemaining,
         DateTime CreatedAt
     );

     // Response after cancel
     public record CancelRegistrationResponse(string Message);
     ```

  5. **ToResponse() extension methods** — add a static helper class in the same file to keep mapping logic out of the service:

     ```csharp
     public static class RegistrationMappingExtensions
     {
         public static RegistrationResponse ToResponse(
             this Registration r,
             decimal amountPaid) => new(
             r.Id,
             new(r.FamilyUnit.Id, r.FamilyUnit.Name),
             new(r.CampEdition.Id, r.CampEdition.Camp.Name, r.CampEdition.Year,
                 r.CampEdition.StartDate, r.CampEdition.EndDate,
                 (r.CampEdition.EndDate - r.CampEdition.StartDate).Days),
             r.Status,
             r.Notes,
             new PricingBreakdown(
                 r.Members.Select(m => new MemberPricingDetail(
                     m.FamilyMemberId,
                     $"{m.FamilyMember.FirstName} {m.FamilyMember.LastName}",
                     m.AgeAtCamp, m.AgeCategory, m.IndividualAmount)).ToList(),
                 r.BaseTotalAmount,
                 r.Extras.Select(e => new ExtraPricingDetail(
                     e.CampEditionExtraId, e.CampEditionExtra.Name, e.UnitPrice,
                     e.CampEditionExtra.PricingType.ToString(),
                     e.CampEditionExtra.PricingPeriod.ToString(),
                     e.Quantity, e.CampDurationDays,
                     BuildCalculation(e), e.TotalAmount)).ToList(),
                 r.ExtrasAmount,
                 r.TotalAmount),
             r.Payments.Select(p => new PaymentSummary(
                 p.Id, p.Amount, p.PaymentDate, p.Method.ToString(), p.Status.ToString())).ToList(),
             amountPaid,
             r.TotalAmount - amountPaid,
             r.CreatedAt,
             r.UpdatedAt
         );

         private static string BuildCalculation(RegistrationExtra e)
         {
             var extra = e.CampEditionExtra;
             return (extra.PricingType, extra.PricingPeriod) switch
             {
                 (PricingType.PerPerson, PricingPeriod.OneTime) =>
                     $"€{e.UnitPrice} × {e.Quantity} persona(s)",
                 (PricingType.PerPerson, PricingPeriod.PerDay) =>
                     $"€{e.UnitPrice} × {e.Quantity} persona(s) × {e.CampDurationDays} días",
                 (PricingType.PerFamily, PricingPeriod.OneTime) =>
                     $"€{e.UnitPrice} (por familia)",
                 (PricingType.PerFamily, PricingPeriod.PerDay) =>
                     $"€{e.UnitPrice} × {e.CampDurationDays} días",
                 _ => string.Empty
             };
         }
     }
     ```

- **Dependencies**: `using Abuvi.API.Features.Camps;`, `using Abuvi.API.Features.FamilyUnits;`, `using Abuvi.API.Features.Users;`
- **Note**: `PricingType` and `PricingPeriod` enums already exist in `Camps` namespace — do not redefine them.

---

### Step 2: Create EF Core Entity Configurations

#### Step 2a: RegistrationConfiguration.cs

- **File**: `src/Abuvi.API/Data/Configurations/RegistrationConfiguration.cs`
- **Action**: Full EF Core Fluent API configuration.

  ```csharp
  public class RegistrationConfiguration : IEntityTypeConfiguration<Registration>
  {
      public void Configure(EntityTypeBuilder<Registration> builder)
      {
          builder.ToTable("registrations");
          builder.HasKey(r => r.Id);
          builder.Property(r => r.Id)
              .HasDefaultValueSql("gen_random_uuid()").HasColumnName("id");

          builder.Property(r => r.FamilyUnitId).IsRequired().HasColumnName("family_unit_id");
          builder.Property(r => r.CampEditionId).IsRequired().HasColumnName("camp_edition_id");
          builder.Property(r => r.RegisteredByUserId).IsRequired().HasColumnName("registered_by_user_id");

          builder.Property(r => r.BaseTotalAmount)
              .HasPrecision(10, 2).IsRequired().HasColumnName("base_total_amount");
          builder.Property(r => r.ExtrasAmount)
              .HasPrecision(10, 2).IsRequired().HasDefaultValue(0m).HasColumnName("extras_amount");
          builder.Property(r => r.TotalAmount)
              .HasPrecision(10, 2).IsRequired().HasColumnName("total_amount");

          builder.Property(r => r.Status)
              .HasConversion<string>().IsRequired().HasMaxLength(20).HasColumnName("status");
          builder.Property(r => r.Notes).HasMaxLength(1000).HasColumnName("notes");

          builder.Property(r => r.CreatedAt).IsRequired().HasColumnName("created_at")
              .HasDefaultValueSql("NOW()");
          builder.Property(r => r.UpdatedAt).IsRequired().HasColumnName("updated_at")
              .HasDefaultValueSql("NOW()");

          builder.HasIndex(r => new { r.FamilyUnitId, r.CampEditionId }).IsUnique()
              .HasDatabaseName("IX_Registrations_FamilyUnitId_CampEditionId");
          builder.HasIndex(r => r.CampEditionId).HasDatabaseName("IX_Registrations_CampEditionId");
          builder.HasIndex(r => r.Status).HasDatabaseName("IX_Registrations_Status");

          builder.ToTable(t => t.HasCheckConstraint(
              "CK_Registrations_TotalAmount",
              "total_amount = base_total_amount + extras_amount"));

          builder.HasOne(r => r.FamilyUnit).WithMany()
              .HasForeignKey(r => r.FamilyUnitId).OnDelete(DeleteBehavior.Restrict);
          builder.HasOne(r => r.CampEdition).WithMany()
              .HasForeignKey(r => r.CampEditionId).OnDelete(DeleteBehavior.Restrict);
          builder.HasOne(r => r.RegisteredByUser).WithMany()
              .HasForeignKey(r => r.RegisteredByUserId).OnDelete(DeleteBehavior.Restrict);
          builder.HasMany(r => r.Members).WithOne(m => m.Registration)
              .HasForeignKey(m => m.RegistrationId).OnDelete(DeleteBehavior.Cascade);
          builder.HasMany(r => r.Extras).WithOne(e => e.Registration)
              .HasForeignKey(e => e.RegistrationId).OnDelete(DeleteBehavior.Cascade);
          builder.HasMany(r => r.Payments).WithOne(p => p.Registration)
              .HasForeignKey(p => p.RegistrationId).OnDelete(DeleteBehavior.Restrict);
      }
  }
  ```

#### Step 2b: RegistrationMemberConfiguration.cs

- **File**: `src/Abuvi.API/Data/Configurations/RegistrationMemberConfiguration.cs`

  ```csharp
  public class RegistrationMemberConfiguration : IEntityTypeConfiguration<RegistrationMember>
  {
      public void Configure(EntityTypeBuilder<RegistrationMember> builder)
      {
          builder.ToTable("registration_members");
          builder.HasKey(m => m.Id);
          builder.Property(m => m.Id).HasDefaultValueSql("gen_random_uuid()").HasColumnName("id");
          builder.Property(m => m.RegistrationId).IsRequired().HasColumnName("registration_id");
          builder.Property(m => m.FamilyMemberId).IsRequired().HasColumnName("family_member_id");
          builder.Property(m => m.AgeAtCamp).IsRequired().HasColumnName("age_at_camp");
          builder.Property(m => m.AgeCategory)
              .HasConversion<string>().IsRequired().HasMaxLength(10).HasColumnName("age_category");
          builder.Property(m => m.IndividualAmount)
              .HasPrecision(10, 2).IsRequired().HasColumnName("individual_amount");
          builder.Property(m => m.CreatedAt).IsRequired().HasColumnName("created_at")
              .HasDefaultValueSql("NOW()");

          builder.HasIndex(m => new { m.RegistrationId, m.FamilyMemberId }).IsUnique()
              .HasDatabaseName("IX_RegistrationMembers_RegistrationId_FamilyMemberId");
          builder.HasIndex(m => m.RegistrationId)
              .HasDatabaseName("IX_RegistrationMembers_RegistrationId");

          builder.HasOne(m => m.FamilyMember).WithMany()
              .HasForeignKey(m => m.FamilyMemberId).OnDelete(DeleteBehavior.Restrict);
      }
  }
  ```

#### Step 2c: RegistrationExtraConfiguration.cs

- **File**: `src/Abuvi.API/Data/Configurations/RegistrationExtraConfiguration.cs`

  ```csharp
  public class RegistrationExtraConfiguration : IEntityTypeConfiguration<RegistrationExtra>
  {
      public void Configure(EntityTypeBuilder<RegistrationExtra> builder)
      {
          builder.ToTable("registration_extras");
          builder.HasKey(e => e.Id);
          builder.Property(e => e.Id).HasDefaultValueSql("gen_random_uuid()").HasColumnName("id");
          builder.Property(e => e.RegistrationId).IsRequired().HasColumnName("registration_id");
          builder.Property(e => e.CampEditionExtraId).IsRequired().HasColumnName("camp_edition_extra_id");
          builder.Property(e => e.Quantity).IsRequired().HasColumnName("quantity");
          builder.Property(e => e.UnitPrice).HasPrecision(10, 2).IsRequired().HasColumnName("unit_price");
          builder.Property(e => e.CampDurationDays).IsRequired().HasColumnName("camp_duration_days");
          builder.Property(e => e.TotalAmount)
              .HasPrecision(10, 2).IsRequired().HasColumnName("total_amount");
          builder.Property(e => e.CreatedAt).IsRequired().HasColumnName("created_at")
              .HasDefaultValueSql("NOW()");

          builder.HasIndex(e => new { e.RegistrationId, e.CampEditionExtraId }).IsUnique()
              .HasDatabaseName("IX_RegistrationExtras_RegistrationId_CampEditionExtraId");

          builder.HasOne(e => e.CampEditionExtra).WithMany()
              .HasForeignKey(e => e.CampEditionExtraId).OnDelete(DeleteBehavior.Restrict);
      }
  }
  ```

#### Step 2d: PaymentConfiguration.cs

- **File**: `src/Abuvi.API/Data/Configurations/PaymentConfiguration.cs`

  ```csharp
  public class PaymentConfiguration : IEntityTypeConfiguration<Payment>
  {
      public void Configure(EntityTypeBuilder<Payment> builder)
      {
          builder.ToTable("payments");
          builder.HasKey(p => p.Id);
          builder.Property(p => p.Id).HasDefaultValueSql("gen_random_uuid()").HasColumnName("id");
          builder.Property(p => p.RegistrationId).IsRequired().HasColumnName("registration_id");
          builder.Property(p => p.Amount).HasPrecision(10, 2).IsRequired().HasColumnName("amount");
          builder.ToTable(t => t.HasCheckConstraint("CK_Payments_Amount", "amount > 0"));
          builder.Property(p => p.PaymentDate).IsRequired().HasColumnName("payment_date");
          builder.Property(p => p.Method)
              .HasConversion<string>().IsRequired().HasMaxLength(20).HasColumnName("method");
          builder.Property(p => p.Status)
              .HasConversion<string>().IsRequired().HasMaxLength(20).HasColumnName("status");
          builder.Property(p => p.ExternalReference).HasMaxLength(255).HasColumnName("external_reference");
          builder.Property(p => p.CreatedAt).IsRequired().HasColumnName("created_at")
              .HasDefaultValueSql("NOW()");
          builder.Property(p => p.UpdatedAt).IsRequired().HasColumnName("updated_at")
              .HasDefaultValueSql("NOW()");

          builder.HasIndex(p => p.RegistrationId).HasDatabaseName("IX_Payments_RegistrationId");
          builder.HasIndex(p => p.Status).HasDatabaseName("IX_Payments_Status");
      }
  }
  ```

---

### Step 3: Update AbuviDbContext

- **File**: `src/Abuvi.API/Data/AbuviDbContext.cs`
- **Action**: Add the 4 new `DbSet<T>` properties alongside existing ones.

  ```csharp
  public DbSet<Registration> Registrations => Set<Registration>();
  public DbSet<RegistrationMember> RegistrationMembers => Set<RegistrationMember>();
  public DbSet<RegistrationExtra> RegistrationExtras => Set<RegistrationExtra>();
  public DbSet<Payment> Payments => Set<Payment>();
  ```

---

### Step 4: Create EF Core Migration

- **Action**: Generate and review the migration. Run only after Steps 1–3 are complete.
- **Command**:

  ```bash
  dotnet ef migrations add AddRegistrationsPaymentsAndExtras --project src/Abuvi.API
  ```

- **Review**: Open the generated migration file and verify:
  - Tables `registrations`, `registration_members`, `registration_extras`, `payments` are created
  - All FK relationships, indexes, and check constraints are present
  - `gen_random_uuid()` default is set on each `id` column
- **Apply**:

  ```bash
  dotnet ef database update --project src/Abuvi.API
  ```

---

### Step 5: Create RegistrationPricingService (TDD — write tests first)

#### Step 5a: Write unit tests first

- **File**: `src/Abuvi.Tests/Unit/Features/Registrations/RegistrationPricingServiceTests.cs`
- **Namespace**: `Abuvi.Tests.Unit.Features.Registrations`
- **Test cases** (all use `[Fact]`):

  ```
  CalculateAge_WhenBirthdayIsOnCampStartDate_ReturnsExactAge
  CalculateAge_WhenBirthdayIsAfterCampStartDate_ReturnsAgeMinusOne
  CalculateAge_WhenBornOnLeapDay_HandlesCorrectly

  GetAgeCategory_WhenAgeFitsGlobalBabyRange_ReturnsBaby
  GetAgeCategory_WhenAgeFitsGlobalChildRange_ReturnsChild
  GetAgeCategory_WhenAgeFitsGlobalAdultRange_ReturnsAdult
  GetAgeCategory_WhenAgeOutsideAllRanges_ThrowsBusinessRuleException
  GetAgeCategory_WhenEditionHasCustomRanges_OverridesGlobalRanges

  CalculateExtraAmount_PerPersonOneTime_MultipliesPriceByQuantity
  CalculateExtraAmount_PerPersonPerDay_MultipliesPriceByQuantityAndDays
  CalculateExtraAmount_PerFamilyOneTime_ReturnsFixedPrice
  CalculateExtraAmount_PerFamilyPerDay_MultipliesPriceByDaysOnly
  ```

- **Setup** (no DB — mock `IAssociationSettingsRepository`):

  ```csharp
  public class RegistrationPricingServiceTests
  {
      private readonly IAssociationSettingsRepository _settingsRepo;
      private readonly RegistrationPricingService _sut;

      public RegistrationPricingServiceTests()
      {
          _settingsRepo = Substitute.For<IAssociationSettingsRepository>();
          _sut = new RegistrationPricingService(_settingsRepo);
      }
  }
  ```

#### Step 5b: Implement RegistrationPricingService

- **File**: `src/Abuvi.API/Features/Registrations/RegistrationPricingService.cs`
- **Dependencies**: `IAssociationSettingsRepository` (already exists in `Camps` slice)
- **Public methods**:

  ```csharp
  public class RegistrationPricingService(IAssociationSettingsRepository settingsRepo)
  {
      // Calculates age as of campStartDate
      public int CalculateAge(DateOnly dateOfBirth, DateTime campStartDate)

      // Determines AgeCategory from age and configured ranges
      // Throws BusinessRuleException if age fits no category
      public async Task<AgeCategory> GetAgeCategoryAsync(int age, CampEdition edition, CancellationToken ct)

      // Returns the price in euros for a given AgeCategory from the edition pricing
      public decimal GetPriceForCategory(AgeCategory category, CampEdition edition)

      // Calculates extra total: handles PerPerson/PerFamily × OneTime/PerDay
      public decimal CalculateExtraAmount(CampEditionExtra extra, int quantity, int campDurationDays)
  }
  ```

- **Age calculation logic**:

  ```csharp
  public int CalculateAge(DateOnly dateOfBirth, DateTime campStartDate)
  {
      var campDate = DateOnly.FromDateTime(campStartDate);
      var age = campDate.Year - dateOfBirth.Year;
      if (campDate < dateOfBirth.AddYears(age)) age--;
      return age;
  }
  ```

- **Age ranges**: If `edition.UseCustomAgeRanges` is true, use `edition.CustomBabyMaxAge`, etc. Otherwise load the `"age_ranges"` setting via `settingsRepo.GetByKeyAsync("age_ranges", ct)` and deserialize.
  - The JSON value of `"age_ranges"` is: `{"babyMaxAge": N, "childMinAge": N, "childMaxAge": N, "adultMinAge": N}`
  - Use `System.Text.Json` to deserialize.

- **Extra calculation**:

  ```csharp
  public decimal CalculateExtraAmount(CampEditionExtra extra, int quantity, int campDurationDays)
  {
      var baseAmount = extra.PricingType == PricingType.PerPerson
          ? extra.Price * quantity
          : extra.Price;

      return extra.PricingPeriod == PricingPeriod.PerDay
          ? baseAmount * campDurationDays
          : baseAmount;
  }
  ```

---

### Step 6: Create IRegistrationsRepository + RegistrationsRepository (TDD)

- **File**: `src/Abuvi.API/Features/Registrations/RegistrationsRepository.cs`
- **Action**: Interface and implementation in the same file, following the `IFamilyUnitsRepository` pattern.

  ```csharp
  public interface IRegistrationsRepository
  {
      Task<Registration?> GetByIdAsync(Guid id, CancellationToken ct);
      // Includes Members.FamilyMember, Extras.CampEditionExtra, Payments, FamilyUnit, CampEdition.Camp
      Task<Registration?> GetByIdWithDetailsAsync(Guid id, CancellationToken ct);
      Task<IReadOnlyList<Registration>> GetByFamilyUnitAsync(Guid familyUnitId, CancellationToken ct);
      Task<bool> ExistsAsync(Guid familyUnitId, Guid campEditionId, CancellationToken ct);
      // Counts non-Cancelled registrations for capacity check
      Task<int> CountActiveByEditionAsync(Guid campEditionId, CancellationToken ct);
      Task AddAsync(Registration registration, CancellationToken ct);
      Task UpdateAsync(Registration registration, CancellationToken ct);
  }
  ```

- **Repository implementation notes**:
  - `GetByIdWithDetailsAsync` — use `.Include()` chains to load all navigation properties needed for `ToResponse()`:

    ```csharp
    .Include(r => r.FamilyUnit)
    .Include(r => r.CampEdition).ThenInclude(e => e.Camp)
    .Include(r => r.RegisteredByUser)
    .Include(r => r.Members).ThenInclude(m => m.FamilyMember)
    .Include(r => r.Extras).ThenInclude(e => e.CampEditionExtra)
    .Include(r => r.Payments)
    ```

  - `CountActiveByEditionAsync` — `WHERE camp_edition_id = id AND status != 'Cancelled'`
  - `AddAsync` / `UpdateAsync` — follow existing pattern (no Unit of Work, call `SaveChangesAsync` directly)
  - `UpdateAsync` — set `UpdatedAt = DateTime.UtcNow` before calling `db.Update()`

- **Separate repositories for RegistrationExtra and Payment** (simpler, referenced from service):

  ```csharp
  // IRegistrationExtrasRepository + RegistrationExtrasRepository
  // in src/Abuvi.API/Features/Registrations/RegistrationExtrasRepository.cs
  public interface IRegistrationExtrasRepository
  {
      Task<IReadOnlyList<RegistrationExtra>> GetByRegistrationIdAsync(Guid registrationId, CancellationToken ct);
      Task DeleteByRegistrationIdAsync(Guid registrationId, CancellationToken ct);
      Task AddRangeAsync(IEnumerable<RegistrationExtra> extras, CancellationToken ct);
  }

  // IPaymentsRepository + PaymentsRepository
  // in src/Abuvi.API/Features/Registrations/PaymentsRepository.cs
  public interface IPaymentsRepository
  {
      Task<decimal> GetTotalCompletedAsync(Guid registrationId, CancellationToken ct);
  }
  ```

---

### Step 7: Create FluentValidation Validators (TDD)

#### Step 7a: CreateRegistrationValidator

- **File**: `src/Abuvi.API/Features/Registrations/CreateRegistrationValidator.cs`
- **Tests file**: `src/Abuvi.Tests/Unit/Features/Registrations/CreateRegistrationValidatorTests.cs`

  ```csharp
  public class CreateRegistrationValidator : AbstractValidator<CreateRegistrationRequest>
  {
      public CreateRegistrationValidator(ICampEditionsRepository editionsRepo)
      {
          RuleFor(x => x.CampEditionId)
              .NotEmpty()
              .WithMessage("La edición del campamento es obligatoria")
              .MustAsync(async (id, ct) =>
              {
                  var edition = await editionsRepo.GetByIdAsync(id, ct);
                  return edition?.Status == CampEditionStatus.Open;
              })
              .WithMessage("La edición del campamento no está abierta para inscripción");

          RuleFor(x => x.FamilyUnitId)
              .NotEmpty()
              .WithMessage("La unidad familiar es obligatoria");

          RuleFor(x => x.MemberIds)
              .NotEmpty()
              .WithMessage("Debe seleccionar al menos un miembro de la familia")
              .Must(ids => ids != null && ids.Distinct().Count() == ids.Count)
              .WithMessage("No se puede incluir el mismo miembro dos veces");

          RuleFor(x => x.Notes)
              .MaximumLength(1000)
              .WithMessage("Las notas no pueden superar los 1000 caracteres");
      }
  }
  ```

- **Note**: `ICampEditionsRepository` is already defined in the `Camps` slice. Import it via `using Abuvi.API.Features.Camps;`.

#### Step 7b: UpdateRegistrationMembersValidator

- **File**: `src/Abuvi.API/Features/Registrations/UpdateRegistrationMembersValidator.cs`

  ```csharp
  public class UpdateRegistrationMembersValidator : AbstractValidator<UpdateRegistrationMembersRequest>
  {
      public UpdateRegistrationMembersValidator()
      {
          RuleFor(x => x.MemberIds)
              .NotEmpty()
              .WithMessage("Debe seleccionar al menos un miembro de la familia")
              .Must(ids => ids.Distinct().Count() == ids.Count)
              .WithMessage("No se puede incluir el mismo miembro dos veces");
      }
  }
  ```

#### Step 7c: UpdateRegistrationExtrasValidator

- **File**: `src/Abuvi.API/Features/Registrations/UpdateRegistrationExtrasValidator.cs`

  ```csharp
  public class UpdateRegistrationExtrasValidator : AbstractValidator<UpdateRegistrationExtrasRequest>
  {
      public UpdateRegistrationExtrasValidator()
      {
          RuleFor(x => x.Extras)
              .NotNull()
              .WithMessage("La lista de extras es obligatoria");

          RuleForEach(x => x.Extras).ChildRules(extra =>
          {
              extra.RuleFor(e => e.CampEditionExtraId)
                  .NotEmpty()
                  .WithMessage("El identificador del extra es obligatorio");
              extra.RuleFor(e => e.Quantity)
                  .GreaterThan(0)
                  .WithMessage("La cantidad debe ser mayor que cero");
          });
      }
  }
  ```

- **Validator tests** — test each rule:
  - `Validate_WhenMemberIdsEmpty_ShouldFail` with message check
  - `Validate_WhenDuplicateMemberIds_ShouldFail`
  - `Validate_WhenCampEditionClosed_ShouldFailEditionRule`
  - `Validate_WhenNotesExceedsMaxLength_ShouldFail`

---

### Step 8: Implement RegistrationsService (TDD — write tests first)

#### Step 8a: Write service tests first

- **File**: `src/Abuvi.Tests/Unit/Features/Registrations/RegistrationsServiceTests.cs`
- **Setup** (use NSubstitute):

  ```csharp
  public class RegistrationsServiceTests
  {
      private readonly IRegistrationsRepository _repo;
      private readonly IRegistrationExtrasRepository _extrasRepo;
      private readonly IFamilyUnitsRepository _familyUnitsRepo;
      private readonly ICampEditionsRepository _editionsRepo;
      private readonly RegistrationPricingService _pricingService;
      private readonly IAssociationSettingsRepository _settingsRepo;
      private readonly ILogger<RegistrationsService> _logger;
      private readonly RegistrationsService _sut;

      public RegistrationsServiceTests()
      {
          _repo = Substitute.For<IRegistrationsRepository>();
          _extrasRepo = Substitute.For<IRegistrationExtrasRepository>();
          _familyUnitsRepo = Substitute.For<IFamilyUnitsRepository>();
          _editionsRepo = Substitute.For<ICampEditionsRepository>();
          _settingsRepo = Substitute.For<IAssociationSettingsRepository>();
          _logger = Substitute.For<ILogger<RegistrationsService>>();
          _pricingService = new RegistrationPricingService(_settingsRepo);
          _sut = new RegistrationsService(
              _repo, _extrasRepo, _familyUnitsRepo, _editionsRepo, _pricingService, _logger);
      }
  }
  ```

- **Test list**:

  ```
  CreateAsync_WhenEditionOpen_CreatesRegistrationWithCorrectPricing
  CreateAsync_WhenEditionNotOpen_ThrowsBusinessRuleException
  CreateAsync_WhenDuplicateRegistration_ThrowsBusinessRuleException
  CreateAsync_WhenCampFull_ThrowsBusinessRuleException
  CreateAsync_WhenMemberNotInFamilyUnit_ThrowsBusinessRuleException
  CreateAsync_WhenUserNotRepresentative_ThrowsBusinessRuleException
  UpdateMembersAsync_WhenRegistrationPending_RecalculatesPricingCorrectly
  UpdateMembersAsync_WhenRegistrationConfirmed_ThrowsBusinessRuleException
  UpdateMembersAsync_WhenRegistrationCancelled_ThrowsBusinessRuleException
  SetExtrasAsync_WhenExtrasValid_UpdatesExtrasAmountAndTotal
  SetExtrasAsync_WhenExtraNotInEdition_ThrowsBusinessRuleException
  SetExtrasAsync_WhenQuantityExceedsMax_ThrowsBusinessRuleException
  SetExtrasAsync_WhenRegistrationNotPending_ThrowsBusinessRuleException
  CancelAsync_WhenStatusPending_SetsStatusCancelled
  CancelAsync_WhenStatusConfirmed_SetsStatusCancelled
  CancelAsync_WhenStatusAlreadyCancelled_ThrowsBusinessRuleException
  GetAvailableEditionsAsync_ReturnsOnlyOpenEditions
  ```

#### Step 8b: Implement RegistrationsService

- **File**: `src/Abuvi.API/Features/Registrations/RegistrationsService.cs`
- **Primary constructor**:

  ```csharp
  public class RegistrationsService(
      IRegistrationsRepository registrationsRepo,
      IRegistrationExtrasRepository extrasRepo,
      IFamilyUnitsRepository familyUnitsRepo,
      ICampEditionsRepository campEditionsRepo,
      RegistrationPricingService pricingService,
      ILogger<RegistrationsService> logger)
  ```

- **Method: `CreateAsync(Guid userId, CreateRegistrationRequest request, CancellationToken ct)`**:
  1. Load FamilyUnit — throw `NotFoundException("Unidad Familiar", request.FamilyUnitId)` if not found
  2. Verify `familyUnit.RepresentativeUserId == userId` — throw `BusinessRuleException("No tienes permiso para inscribir esta unidad familiar")` if not
  3. Load CampEdition — throw `NotFoundException` if not found
  4. Verify `edition.Status == CampEditionStatus.Open` — throw `BusinessRuleException("La edición del campamento no está abierta para inscripción")`
  5. Check for duplicate: `await registrationsRepo.ExistsAsync(request.FamilyUnitId, request.CampEditionId, ct)` — throw `BusinessRuleException("Ya existe una inscripción para esta familia en este campamento")`
  6. Check capacity: if `edition.MaxCapacity.HasValue`:
     - `var activeCount = await registrationsRepo.CountActiveByEditionAsync(request.CampEditionId, ct)`
     - If `activeCount >= edition.MaxCapacity.Value` → throw `BusinessRuleException("El campamento ha alcanzado su capacidad máxima")`
  7. Load and validate all FamilyMembers:
     - For each `memberId` in `request.MemberIds`: load via `familyUnitsRepo.GetFamilyMemberByIdAsync(memberId, ct)`
     - Verify `member.FamilyUnitId == request.FamilyUnitId` — throw `BusinessRuleException("El miembro {firstName} {lastName} no pertenece a esta unidad familiar")`
     - Calculate age: `pricingService.CalculateAge(member.DateOfBirth, edition.StartDate)`
     - Get category: `await pricingService.GetAgeCategoryAsync(age, edition, ct)`
     - Get price: `pricingService.GetPriceForCategory(category, edition)`
  8. Calculate `baseTotalAmount = sum of all individualAmounts`
  9. Build `Registration` entity and `List<RegistrationMember>`, set `Registration.Members` from the list
  10. Call `await registrationsRepo.AddAsync(registration, ct)` — this must save both registration and members atomically (because `Members` is a navigation property; EF Core will insert them in the same `SaveChangesAsync`)
  11. Log `logger.LogInformation("Registration {RegistrationId} created for family {FamilyUnitId} in edition {EditionId}", ...)`
  12. Reload with `GetByIdWithDetailsAsync` and return `registration.ToResponse(amountPaid: 0)`

- **Method: `UpdateMembersAsync(Guid registrationId, Guid userId, UpdateRegistrationMembersRequest request, CancellationToken ct)`**:
  1. Load registration — throw `NotFoundException` if not found
  2. Verify `registration.FamilyUnit.RepresentativeUserId == userId` (load with FamilyUnit) — throw `BusinessRuleException` if not representative
  3. Verify `registration.Status == RegistrationStatus.Pending` — throw `BusinessRuleException("Solo se pueden modificar inscripciones en estado Pendiente")`
  4. Validate new members (same as CreateAsync steps 7a-7c)
  5. Recalculate baseTotalAmount
  6. Delete existing members: use `db.RegistrationMembers.Where(m => m.RegistrationId == registrationId)` — implement a `DeleteMembersByRegistrationIdAsync` method in the repository
  7. Create new `RegistrationMember` list and add to registration
  8. Update `registration.BaseTotalAmount`, `registration.TotalAmount = baseTotalAmount + registration.ExtrasAmount`
  9. Call `await registrationsRepo.UpdateAsync(registration, ct)`
  10. Reload with details and return response

- **Method: `SetExtrasAsync(Guid registrationId, Guid userId, UpdateRegistrationExtrasRequest request, CancellationToken ct)`**:
  1. Load registration with details — throw `NotFoundException` if not found
  2. Verify representative
  3. Verify status is Pending
  4. Validate each extra:
     - Load extra via `campEditionsRepo.GetExtraByIdAsync(extraReq.CampEditionExtraId, ct)`
     - Verify `extra.CampEditionId == registration.CampEditionId`
     - If `extra.MaxQuantity.HasValue && extraReq.Quantity > extra.MaxQuantity.Value` → throw `BusinessRuleException`
     - Verify `extra.IsActive == true`
  5. Calculate camp duration: `(registration.CampEdition.EndDate - registration.CampEdition.StartDate).Days`
  6. Delete existing extras: `await extrasRepo.DeleteByRegistrationIdAsync(registrationId, ct)`
  7. Build new `RegistrationExtra` list — snapshot `UnitPrice = extra.Price` and `CampDurationDays`
  8. Calculate `totalAmount` per extra using `pricingService.CalculateExtraAmount(extra, quantity, campDurationDays)`
  9. `await extrasRepo.AddRangeAsync(newExtras, ct)`
  10. Update `registration.ExtrasAmount = newExtras.Sum(e => e.TotalAmount)` and `registration.TotalAmount`
  11. `await registrationsRepo.UpdateAsync(registration, ct)`
  12. Reload and return response

- **Method: `CancelAsync(Guid registrationId, Guid userId, bool isAdminOrBoard, CancellationToken ct)`**:
  1. Load registration — throw `NotFoundException` if not found
  2. If not `isAdminOrBoard`: verify representative
  3. Verify `status != RegistrationStatus.Cancelled` — throw `BusinessRuleException("La inscripción ya ha sido cancelada")`
  4. Set `registration.Status = RegistrationStatus.Cancelled`
  5. `await registrationsRepo.UpdateAsync(registration, ct)`
  6. Log cancellation
  7. Return `new CancelRegistrationResponse("Inscripción cancelada correctamente")`

- **Method: `GetAvailableEditionsAsync(CancellationToken ct)`**:
  1. Load all editions with status Open and not archived via `campEditionsRepo`
  2. For each edition, count active registrations and calculate `SpotsRemaining`
  3. Build `AvailableCampEditionResponse` list
  - **Note**: This requires adding a method to `ICampEditionsRepository`: `GetOpenEditionsAsync(CancellationToken ct)` — this change must be made in the `Camps` slice.

- **Method: `GetByIdAsync(Guid registrationId, Guid userId, bool isAdminOrBoard, CancellationToken ct)`**:
  1. Load with details
  2. If not Admin/Board, verify `registration.FamilyUnit.RepresentativeUserId == userId`
  3. Calculate `amountPaid = registration.Payments.Where(p => p.Status == PaymentStatus.Completed).Sum(p => p.Amount)`
  4. Return `registration.ToResponse(amountPaid)`

- **Method: `GetByFamilyUnitAsync(Guid userId, CancellationToken ct)`**:
  1. Load FamilyUnit by representative user ID
  2. Return all registrations for that family unit (list view, no full pricing detail)

---

### Step 9: Add `GetOpenEditionsAsync` to ICampEditionsRepository

- **File**: `src/Abuvi.API/Features/Camps/CampEditionsRepository.cs`
- **Action**: Add to the existing interface and implementation (minimally — only what is needed).

  ```csharp
  // Interface addition:
  Task<IReadOnlyList<CampEdition>> GetOpenEditionsAsync(CancellationToken ct);

  // Implementation addition:
  public async Task<IReadOnlyList<CampEdition>> GetOpenEditionsAsync(CancellationToken ct)
      => await db.CampEditions
          .AsNoTracking()
          .Include(e => e.Camp)
          .Where(e => e.Status == CampEditionStatus.Open && !e.IsArchived)
          .OrderBy(e => e.StartDate)
          .ToListAsync(ct);
  ```

- **Note**: Also ensure `ICampEditionsRepository` already has `GetExtraByIdAsync(Guid extraId, CancellationToken ct)` — check the existing interface. If not present, add:

  ```csharp
  Task<CampEditionExtra?> GetExtraByIdAsync(Guid extraId, CancellationToken ct);
  ```

---

### Step 10: Create RegistrationsEndpoints

- **File**: `src/Abuvi.API/Features/Registrations/RegistrationsEndpoints.cs`
- **Namespace**: `Abuvi.API.Features.Registrations`

  ```csharp
  public static class RegistrationsEndpoints
  {
      public static IEndpointRouteBuilder MapRegistrationsEndpoints(this IEndpointRouteBuilder app)
      {
          // Available camp editions — any authenticated user
          var campsGroup = app.MapGroup("/api/camps/editions")
              .WithTags("Camp Editions")
              .WithOpenApi()
              .RequireAuthorization();

          campsGroup.MapGet("/available", GetAvailableEditions)
              .WithName("GetAvailableEditions")
              .WithSummary("Get open camp editions available for registration")
              .Produces<ApiResponse<List<AvailableCampEditionResponse>>>();

          // Registrations
          var group = app.MapGroup("/api/registrations")
              .WithTags("Registrations")
              .WithOpenApi()
              .RequireAuthorization();

          group.MapGet("/", GetMyRegistrations)
              .WithName("GetMyRegistrations")
              .WithSummary("Get registrations for the current user's family")
              .Produces<ApiResponse<List<RegistrationListResponse>>>();

          group.MapGet("/{id:guid}", GetRegistrationById)
              .WithName("GetRegistrationById")
              .WithSummary("Get registration detail with full pricing breakdown")
              .Produces<ApiResponse<RegistrationResponse>>()
              .Produces(403).Produces(404);

          group.MapPost("/", CreateRegistration)
              .WithName("CreateRegistration")
              .WithSummary("Register a family for a camp edition (representative only)")
              .AddEndpointFilter<ValidationFilter<CreateRegistrationRequest>>()
              .Produces<ApiResponse<RegistrationResponse>>(201)
              .Produces(400).Produces(403).Produces(409);

          group.MapPut("/{id:guid}/members", UpdateRegistrationMembers)
              .WithName("UpdateRegistrationMembers")
              .WithSummary("Update attending family members (representative only)")
              .AddEndpointFilter<ValidationFilter<UpdateRegistrationMembersRequest>>()
              .Produces<ApiResponse<RegistrationResponse>>()
              .Produces(400).Produces(403).Produces(404).Produces(422);

          group.MapPost("/{id:guid}/extras", SetRegistrationExtras)
              .WithName("SetRegistrationExtras")
              .WithSummary("Set extras selection (representative only)")
              .AddEndpointFilter<ValidationFilter<UpdateRegistrationExtrasRequest>>()
              .Produces<ApiResponse<RegistrationResponse>>()
              .Produces(400).Produces(403).Produces(404).Produces(422);

          group.MapPost("/{id:guid}/cancel", CancelRegistration)
              .WithName("CancelRegistration")
              .WithSummary("Cancel registration (representative or Admin/Board)")
              .Produces<ApiResponse<CancelRegistrationResponse>>()
              .Produces(403).Produces(404).Produces(422);

          return app;
      }

      private static async Task<IResult> GetAvailableEditions(
          RegistrationsService service, CancellationToken ct)
      {
          var result = await service.GetAvailableEditionsAsync(ct);
          return TypedResults.Ok(ApiResponse<List<AvailableCampEditionResponse>>.Ok(result));
      }

      private static async Task<IResult> GetMyRegistrations(
          RegistrationsService service, ClaimsPrincipal user, CancellationToken ct)
      {
          var userId = user.GetUserId()
              ?? throw new UnauthorizedAccessException("Usuario no autenticado");
          var result = await service.GetByFamilyUnitAsync(userId, ct);
          return TypedResults.Ok(ApiResponse<List<RegistrationListResponse>>.Ok(result));
      }

      private static async Task<IResult> GetRegistrationById(
          Guid id, RegistrationsService service, ClaimsPrincipal user, CancellationToken ct)
      {
          var userId = user.GetUserId()
              ?? throw new UnauthorizedAccessException("Usuario no autenticado");
          var userRole = user.GetUserRole();
          var isAdminOrBoard = userRole is "Admin" or "Board";

          try
          {
              var result = await service.GetByIdAsync(id, userId, isAdminOrBoard, ct);
              return TypedResults.Ok(ApiResponse<RegistrationResponse>.Ok(result));
          }
          catch (NotFoundException ex)
          {
              return TypedResults.NotFound(ApiResponse<object>.NotFound(ex.Message));
          }
          catch (BusinessRuleException)
          {
              return TypedResults.Forbid();
          }
      }

      private static async Task<IResult> CreateRegistration(
          CreateRegistrationRequest request,
          RegistrationsService service,
          ClaimsPrincipal user,
          CancellationToken ct)
      {
          var userId = user.GetUserId()
              ?? throw new UnauthorizedAccessException("Usuario no autenticado");

          try
          {
              var result = await service.CreateAsync(userId, request, ct);
              return TypedResults.Created(
                  $"/api/registrations/{result.Id}",
                  ApiResponse<RegistrationResponse>.Ok(result));
          }
          catch (NotFoundException ex)
          {
              return TypedResults.NotFound(ApiResponse<object>.NotFound(ex.Message));
          }
          catch (BusinessRuleException ex) when (
              ex.Message.Contains("Ya existe") || ex.Message.Contains("capacidad"))
          {
              return TypedResults.Conflict(ApiResponse<object>.Fail(ex.Message, "REGISTRATION_CONFLICT"));
          }
          catch (BusinessRuleException ex)
          {
              return TypedResults.UnprocessableEntity(
                  ApiResponse<object>.Fail(ex.Message, "BUSINESS_RULE_VIOLATION"));
          }
      }

      private static async Task<IResult> UpdateRegistrationMembers(
          Guid id,
          UpdateRegistrationMembersRequest request,
          RegistrationsService service,
          ClaimsPrincipal user,
          CancellationToken ct)
      {
          var userId = user.GetUserId()
              ?? throw new UnauthorizedAccessException("Usuario no autenticado");

          try
          {
              var result = await service.UpdateMembersAsync(id, userId, request, ct);
              return TypedResults.Ok(ApiResponse<RegistrationResponse>.Ok(result));
          }
          catch (NotFoundException ex)
          {
              return TypedResults.NotFound(ApiResponse<object>.NotFound(ex.Message));
          }
          catch (BusinessRuleException ex)
          {
              return TypedResults.UnprocessableEntity(
                  ApiResponse<object>.Fail(ex.Message, "BUSINESS_RULE_VIOLATION"));
          }
      }

      private static async Task<IResult> SetRegistrationExtras(
          Guid id,
          UpdateRegistrationExtrasRequest request,
          RegistrationsService service,
          ClaimsPrincipal user,
          CancellationToken ct)
      {
          var userId = user.GetUserId()
              ?? throw new UnauthorizedAccessException("Usuario no autenticado");

          try
          {
              var result = await service.SetExtrasAsync(id, userId, request, ct);
              return TypedResults.Ok(ApiResponse<RegistrationResponse>.Ok(result));
          }
          catch (NotFoundException ex)
          {
              return TypedResults.NotFound(ApiResponse<object>.NotFound(ex.Message));
          }
          catch (BusinessRuleException ex)
          {
              return TypedResults.UnprocessableEntity(
                  ApiResponse<object>.Fail(ex.Message, "BUSINESS_RULE_VIOLATION"));
          }
      }

      private static async Task<IResult> CancelRegistration(
          Guid id,
          RegistrationsService service,
          ClaimsPrincipal user,
          CancellationToken ct)
      {
          var userId = user.GetUserId()
              ?? throw new UnauthorizedAccessException("Usuario no autenticado");
          var userRole = user.GetUserRole();
          var isAdminOrBoard = userRole is "Admin" or "Board";

          try
          {
              var result = await service.CancelAsync(id, userId, isAdminOrBoard, ct);
              return TypedResults.Ok(ApiResponse<CancelRegistrationResponse>.Ok(result));
          }
          catch (NotFoundException ex)
          {
              return TypedResults.NotFound(ApiResponse<object>.NotFound(ex.Message));
          }
          catch (BusinessRuleException ex)
          {
              return TypedResults.UnprocessableEntity(
                  ApiResponse<object>.Fail(ex.Message, "BUSINESS_RULE_VIOLATION"));
          }
      }
  }
  ```

- **Authorization note**: The representative-only check is handled at the service level (following the `FamilyUnitsEndpoints` pattern). `Forbid()` is returned when `BusinessRuleException` indicates lack of permission. Use `TypedResults.Forbid()` (not `Results.Forbid()`).

---

### Step 11: Register Services and Map Endpoints in Program.cs

- **File**: `src/Abuvi.API/Program.cs`
- **Action**: Add after existing service registrations:

  ```csharp
  // Registrations feature
  builder.Services.AddScoped<IRegistrationsRepository, RegistrationsRepository>();
  builder.Services.AddScoped<IRegistrationExtrasRepository, RegistrationExtrasRepository>();
  builder.Services.AddScoped<IPaymentsRepository, PaymentsRepository>();
  builder.Services.AddScoped<RegistrationPricingService>();
  builder.Services.AddScoped<RegistrationsService>();
  ```

- **Action**: Add after existing endpoint mappings:

  ```csharp
  app.MapRegistrationsEndpoints();
  ```

---

### Step 12: Create Test Builders

- **File**: `src/Abuvi.Tests/Helpers/Builders/RegistrationBuilder.cs`

  ```csharp
  public class RegistrationBuilder
  {
      private Guid _id = Guid.NewGuid();
      private Guid _familyUnitId = Guid.NewGuid();
      private Guid _campEditionId = Guid.NewGuid();
      private Guid _registeredByUserId = Guid.NewGuid();
      private decimal _baseTotalAmount = 300m;
      private decimal _extrasAmount = 0m;
      private RegistrationStatus _status = RegistrationStatus.Pending;

      public RegistrationBuilder WithId(Guid id) { _id = id; return this; }
      public RegistrationBuilder WithFamilyUnitId(Guid id) { _familyUnitId = id; return this; }
      public RegistrationBuilder WithCampEditionId(Guid id) { _campEditionId = id; return this; }
      public RegistrationBuilder WithStatus(RegistrationStatus status) { _status = status; return this; }
      public RegistrationBuilder WithBaseTotalAmount(decimal amount) { _baseTotalAmount = amount; return this; }
      public RegistrationBuilder WithExtrasAmount(decimal amount) { _extrasAmount = amount; return this; }

      public Registration Build() => new()
      {
          Id = _id,
          FamilyUnitId = _familyUnitId,
          CampEditionId = _campEditionId,
          RegisteredByUserId = _registeredByUserId,
          BaseTotalAmount = _baseTotalAmount,
          ExtrasAmount = _extrasAmount,
          TotalAmount = _baseTotalAmount + _extrasAmount,
          Status = _status,
          CreatedAt = DateTime.UtcNow,
          UpdatedAt = DateTime.UtcNow,
          FamilyUnit = new FamilyUnit
          {
              Id = _familyUnitId,
              Name = "Test Family",
              RepresentativeUserId = _registeredByUserId
          },
          Members = [],
          Extras = [],
          Payments = []
      };
  }
  ```

- **File**: `src/Abuvi.Tests/Helpers/Builders/PaymentBuilder.cs` — similar pattern for `Payment` entity.

---

### Step 13: Write Integration Tests

- **File**: `src/Abuvi.Tests/Integration/Features/Registrations/RegistrationsIntegrationTests.cs`
- **Approach**: Follow the `WebApplicationFactory<Program>` + in-memory DB pattern from existing integration tests.
- **Test cases**:

  ```
  CreateRegistration_WithValidData_Returns201Created
  CreateRegistration_WhenEditionClosed_Returns422
  CreateRegistration_WhenDuplicate_Returns409
  CreateRegistration_WhenUserNotRepresentative_Returns403
  GetAvailableEditions_ReturnsOnlyOpenEditions
  GetRegistrationById_WhenOwner_ReturnsFullDetail
  GetRegistrationById_WhenNotOwner_Returns403
  UpdateMembers_WhenPending_RecalculatesTotal
  UpdateMembers_WhenConfirmed_Returns422
  SetExtras_WithValidExtras_UpdatesExtrasAmount
  CancelRegistration_WhenPending_Returns200WithCancelledStatus
  CancelRegistration_WhenAlreadyCancelled_Returns422
  ```

---

### Step 14: Update Technical Documentation

- **Action**: Review all changes and update the following documentation files.
- **Implementation Steps**:
  1. **`ai-specs/specs/data-model.md`**:
     - Add `Registration`, `RegistrationMember`, `RegistrationExtra`, `Payment` entities with their fields, validation rules, and relationships
     - Update the Mermaid ERD to include the new entities and their relationships
  2. **`ai-specs/specs/api-endpoints.md`**:
     - Add a new "Registration Endpoints" section documenting all 7 endpoints with request/response formats, validation rules, and error codes
  3. **No changes needed** to `backend-standards.mdc` or architecture docs — this feature follows existing patterns without introducing new conventions.

---

## Implementation Order

```
0. Create feature branch
1. Create RegistrationsModels.cs (entities + DTOs + extensions)
2a-2d. Create EF Core configurations (4 files)
3. Update AbuviDbContext (add DbSets)
4. Create and apply migration
5a. Write RegistrationPricingServiceTests → 5b. Implement RegistrationPricingService
6. Create RegistrationsRepository (+ RegistrationExtrasRepository + PaymentsRepository)
7a-7c. Write validator tests → Implement validators
8a. Write RegistrationsServiceTests → 8b. Implement RegistrationsService
9. Add GetOpenEditionsAsync to CampEditionsRepository (existing slice modification)
10. Create RegistrationsEndpoints
11. Register services and map endpoints in Program.cs
12. Create test builders (RegistrationBuilder, PaymentBuilder)
13. Write integration tests
14. Update documentation (data-model.md, api-endpoints.md)
```

---

## Testing Checklist

- [ ] `RegistrationPricingServiceTests` — all pricing calculations pass (no DB dependency)
- [ ] `CreateRegistrationValidatorTests` — all FluentValidation rules covered
- [ ] `RegistrationsServiceTests` — happy path and all business rule violations covered
- [ ] `RegistrationsIntegrationTests` — full HTTP pipeline tested with WebApplicationFactory
- [ ] `dotnet test` passes with 0 failures
- [ ] 90% coverage threshold met

---

## Error Response Format

All errors use `ApiResponse<T>` envelope:

| Situation | Status Code | Error Code |
|-----------|------------|------------|
| Camp edition not Open | 422 | `BUSINESS_RULE_VIOLATION` |
| Duplicate registration | 409 | `REGISTRATION_CONFLICT` |
| Camp at full capacity | 409 | `REGISTRATION_CONFLICT` |
| Member not in family | 422 | `BUSINESS_RULE_VIOLATION` |
| Extra not in edition | 422 | `BUSINESS_RULE_VIOLATION` |
| Registration not Pending | 422 | `BUSINESS_RULE_VIOLATION` |
| Not the representative | 403 | (TypedResults.Forbid — no body) |
| Resource not found | 404 | `NOT_FOUND` |
| Validation error | 400 | `VALIDATION_ERROR` |

---

## Dependencies

**No new NuGet packages required** — all existing packages (EF Core, FluentValidation, NSubstitute, FluentAssertions, xUnit) already cover this feature.

**Migration command**:

```bash
dotnet ef migrations add AddRegistrationsPaymentsAndExtras --project src/Abuvi.API
dotnet ef database update --project src/Abuvi.API
```

---

## Notes

- **Language**: All user-facing validation messages and `BusinessRuleException` messages must be in **Spanish**. Developer logs (`logger.Log*`) must be in **English**.
- **TypedResults**: Always use `TypedResults` (not `Results`) in endpoints — this is the project standard.
- **`user.GetUserId()`**: Use `HttpContextExtensions.GetUserId(this ClaimsPrincipal)` — already defined in `Common/Extensions/HttpContextExtensions.cs`.
- **`user.GetUserRole()`**: Use `HttpContextExtensions.GetUserRole(this ClaimsPrincipal)` for Admin/Board checks.
- **Representative pattern**: Authorization for representative-only operations is checked in the endpoint handler (call `service.IsRepresentativeAsync` or inline check) — not via policy attributes. Follow `FamilyUnitsEndpoints` exactly.
- **No actual deletion**: `CancelRegistration` sets status to `Cancelled`, it does NOT delete the database row.
- **Price snapshot**: When creating `RegistrationExtra`, snapshot `extra.Price` into `UnitPrice` to preserve historical accuracy if the camp price changes later.
- **Capacity check is NOT atomic in this implementation**: The `CountActiveByEditionAsync` + insert approach has a small race condition window. For this association's scale (< 100 registrations), this is acceptable. A future improvement would use `SELECT FOR UPDATE` at the database level.
- **Sensitive data**: `FamilyMember.MedicalNotes` and `FamilyMember.Allergies` must NEVER appear in registration responses — they are not part of any DTO in this slice.
- **`CampEdition.StartDate` is `DateTime`**: The existing entity uses `DateTime`, not `DateOnly`. The pricing service `CalculateAge` must accept `DateTime` for camp start date.

---

## Next Steps After Implementation

1. Frontend registration flow (separate ticket — `feat-camps-registration-frontend`)
2. Payment endpoints — `POST /api/payments` and `PATCH /api/payments/{id}/complete` with auto-confirm logic (separate ticket)
3. Email notifications on registration confirmation (future sprint — requires Resend templates)
4. Redsys payment integration for Card payments
