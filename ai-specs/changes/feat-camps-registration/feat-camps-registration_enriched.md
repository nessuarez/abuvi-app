# Camp Registration Flow — Enriched Technical Specification

## Overview

This feature implements the full camp registration workflow for ABUVI, allowing family representatives to register their family units for camp editions, select extras, and track payment status.

---

## Architecture Alignment

### Key Entities (actual codebase, not outdated data-model.md)

The codebase already has:
- `Camp` → camp template (location, default pricing)
- `CampEdition` → specific camp instance per year (with pricing, status, capacity, extras)
- `CampEditionExtra` → optional services per edition
- `FamilyUnit` / `FamilyMember` → families and their members

This feature adds:
- `Registration` → one per family unit per camp edition
- `RegistrationMember` → which family members attend, with individual pricing
- `RegistrationExtra` → selected extras per registration
- `Payment` → partial or full payments per registration

---

## Data Model Changes

### New Entities

#### Registration

```csharp
// src/Abuvi.API/Features/Registrations/RegistrationsModels.cs
public class Registration
{
    public Guid Id { get; set; }
    public Guid FamilyUnitId { get; set; }
    public Guid CampEditionId { get; set; }
    public Guid RegisteredByUserId { get; set; }

    public decimal BaseTotalAmount { get; set; }   // Sum of all member amounts
    public decimal ExtrasAmount { get; set; }      // Sum of all extra amounts
    public decimal TotalAmount { get; set; }       // BaseTotalAmount + ExtrasAmount

    public RegistrationStatus Status { get; set; } = RegistrationStatus.Pending;
    public string? Notes { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    // Navigation properties
    public FamilyUnit FamilyUnit { get; set; } = null!;
    public CampEdition CampEdition { get; set; } = null!;
    public User RegisteredByUser { get; set; } = null!;
    public ICollection<RegistrationMember> Members { get; set; } = new List<RegistrationMember>();
    public ICollection<RegistrationExtra> Extras { get; set; } = new List<RegistrationExtra>();
    public ICollection<Payment> Payments { get; set; } = new List<Payment>();
}

public enum RegistrationStatus
{
    Pending,
    Confirmed,
    Cancelled
}
```

#### RegistrationMember

```csharp
public class RegistrationMember
{
    public Guid Id { get; set; }
    public Guid RegistrationId { get; set; }
    public Guid FamilyMemberId { get; set; }
    public int AgeAtCamp { get; set; }
    public AgeCategory AgeCategory { get; set; }
    public decimal IndividualAmount { get; set; }
    public DateTime CreatedAt { get; set; }

    // Navigation properties
    public Registration Registration { get; set; } = null!;
    public FamilyMember FamilyMember { get; set; } = null!;
}

public enum AgeCategory
{
    Baby,
    Child,
    Adult
}
```

#### RegistrationExtra

```csharp
public class RegistrationExtra
{
    public Guid Id { get; set; }
    public Guid RegistrationId { get; set; }
    public Guid CampEditionExtraId { get; set; }
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }         // Snapshot of price at time of selection
    public int CampDurationDays { get; set; }      // Snapshot of camp duration (for PerDay extras)
    public decimal TotalAmount { get; set; }        // Calculated: price × quantity × (days if PerDay)
    public DateTime CreatedAt { get; set; }

    // Navigation properties
    public Registration Registration { get; set; } = null!;
    public CampEditionExtra CampEditionExtra { get; set; } = null!;
}
```

#### Payment

```csharp
public class Payment
{
    public Guid Id { get; set; }
    public Guid RegistrationId { get; set; }
    public decimal Amount { get; set; }
    public DateTime PaymentDate { get; set; }
    public PaymentMethod Method { get; set; }
    public PaymentStatus Status { get; set; }
    public string? ExternalReference { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    // Navigation properties
    public Registration Registration { get; set; } = null!;
}

public enum PaymentMethod { Card, Transfer, Cash }
public enum PaymentStatus { Pending, Completed, Failed, Refunded }
```

---

## Files to Create / Modify

### New Files

```
src/Abuvi.API/Features/Registrations/
    RegistrationsModels.cs          ← All entities and DTOs
    RegistrationsEndpoints.cs       ← Minimal API endpoint definitions
    RegistrationsService.cs         ← Business logic
    RegistrationPricingService.cs   ← Age/extras pricing calculator (separated concern)
    IRegistrationsRepository.cs     ← Repository interface
    RegistrationsRepository.cs      ← EF Core repository implementation
    IRegistrationExtrasRepository.cs
    RegistrationExtrasRepository.cs
    IPaymentsRepository.cs
    PaymentsRepository.cs
    CreateRegistrationValidator.cs  ← FluentValidation
    UpdateRegistrationMembersValidator.cs
    UpdateRegistrationExtrasValidator.cs

src/Abuvi.API/Data/Configurations/
    RegistrationConfiguration.cs
    RegistrationMemberConfiguration.cs
    RegistrationExtraConfiguration.cs
    PaymentConfiguration.cs

src/Abuvi.Tests/Unit/Features/Registrations/
    RegistrationPricingServiceTests.cs
    RegistrationsServiceTests.cs
    CreateRegistrationValidatorTests.cs

src/Abuvi.Tests/Integration/Features/Registrations/
    RegistrationsIntegrationTests.cs

src/Abuvi.Tests/Helpers/Builders/
    RegistrationBuilder.cs
    RegistrationMemberBuilder.cs
    PaymentBuilder.cs
```

### Modified Files

```
src/Abuvi.API/Data/AbuviDbContext.cs        ← Add DbSets
src/Abuvi.API/Program.cs                   ← Register services and map endpoints
```

---

## EF Core Configurations

### RegistrationConfiguration.cs

```csharp
public class RegistrationConfiguration : IEntityTypeConfiguration<Registration>
{
    public void Configure(EntityTypeBuilder<Registration> builder)
    {
        builder.ToTable("registrations");
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).HasDefaultValueSql("gen_random_uuid()").HasColumnName("id");

        builder.Property(r => r.FamilyUnitId).IsRequired().HasColumnName("family_unit_id");
        builder.Property(r => r.CampEditionId).IsRequired().HasColumnName("camp_edition_id");
        builder.Property(r => r.RegisteredByUserId).IsRequired().HasColumnName("registered_by_user_id");

        builder.Property(r => r.BaseTotalAmount).HasPrecision(10, 2).IsRequired().HasColumnName("base_total_amount");
        builder.Property(r => r.ExtrasAmount).HasPrecision(10, 2).IsRequired().HasDefaultValue(0m).HasColumnName("extras_amount");
        builder.Property(r => r.TotalAmount).HasPrecision(10, 2).IsRequired().HasColumnName("total_amount");

        builder.Property(r => r.Status).HasConversion<string>().IsRequired().HasMaxLength(20).HasColumnName("status");
        builder.Property(r => r.Notes).HasMaxLength(1000).HasColumnName("notes");

        builder.Property(r => r.CreatedAt).IsRequired().HasColumnName("created_at").HasDefaultValueSql("NOW()");
        builder.Property(r => r.UpdatedAt).IsRequired().HasColumnName("updated_at").HasDefaultValueSql("NOW()");

        // Unique: one registration per family per edition
        builder.HasIndex(r => new { r.FamilyUnitId, r.CampEditionId }).IsUnique()
            .HasDatabaseName("IX_Registrations_FamilyUnitId_CampEditionId");

        builder.HasIndex(r => r.Status).HasDatabaseName("IX_Registrations_Status");
        builder.HasIndex(r => r.CampEditionId).HasDatabaseName("IX_Registrations_CampEditionId");

        // Check: total = base + extras
        builder.ToTable(t => t.HasCheckConstraint("CK_Registrations_TotalAmount", "total_amount = base_total_amount + extras_amount"));

        builder.HasOne(r => r.FamilyUnit).WithMany().HasForeignKey(r => r.FamilyUnitId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(r => r.CampEdition).WithMany().HasForeignKey(r => r.CampEditionId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(r => r.RegisteredByUser).WithMany().HasForeignKey(r => r.RegisteredByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(r => r.Members).WithOne(m => m.Registration).HasForeignKey(m => m.RegistrationId).OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(r => r.Extras).WithOne(e => e.Registration).HasForeignKey(e => e.RegistrationId).OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(r => r.Payments).WithOne(p => p.Registration).HasForeignKey(p => p.RegistrationId).OnDelete(DeleteBehavior.Restrict);
    }
}
```

### RegistrationMemberConfiguration.cs

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
        builder.Property(m => m.AgeCategory).HasConversion<string>().IsRequired().HasMaxLength(10).HasColumnName("age_category");
        builder.Property(m => m.IndividualAmount).HasPrecision(10, 2).IsRequired().HasColumnName("individual_amount");
        builder.Property(m => m.CreatedAt).IsRequired().HasColumnName("created_at").HasDefaultValueSql("NOW()");

        builder.HasIndex(m => new { m.RegistrationId, m.FamilyMemberId }).IsUnique()
            .HasDatabaseName("IX_RegistrationMembers_RegistrationId_FamilyMemberId");
        builder.HasIndex(m => m.RegistrationId).HasDatabaseName("IX_RegistrationMembers_RegistrationId");

        builder.HasOne(m => m.FamilyMember).WithMany().HasForeignKey(m => m.FamilyMemberId).OnDelete(DeleteBehavior.Restrict);
    }
}
```

### RegistrationExtraConfiguration.cs

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
        builder.Property(e => e.TotalAmount).HasPrecision(10, 2).IsRequired().HasColumnName("total_amount");
        builder.Property(e => e.CreatedAt).IsRequired().HasColumnName("created_at").HasDefaultValueSql("NOW()");

        builder.HasIndex(e => new { e.RegistrationId, e.CampEditionExtraId }).IsUnique()
            .HasDatabaseName("IX_RegistrationExtras_RegistrationId_CampEditionExtraId");

        builder.HasOne(e => e.CampEditionExtra).WithMany().HasForeignKey(e => e.CampEditionExtraId).OnDelete(DeleteBehavior.Restrict);
    }
}
```

### PaymentConfiguration.cs

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
        builder.Property(p => p.Method).HasConversion<string>().IsRequired().HasMaxLength(20).HasColumnName("method");
        builder.Property(p => p.Status).HasConversion<string>().IsRequired().HasMaxLength(20).HasColumnName("status");
        builder.Property(p => p.ExternalReference).HasMaxLength(255).HasColumnName("external_reference");
        builder.Property(p => p.CreatedAt).IsRequired().HasColumnName("created_at").HasDefaultValueSql("NOW()");
        builder.Property(p => p.UpdatedAt).IsRequired().HasColumnName("updated_at").HasDefaultValueSql("NOW()");

        builder.HasIndex(p => p.RegistrationId).HasDatabaseName("IX_Payments_RegistrationId");
        builder.HasIndex(p => p.Status).HasDatabaseName("IX_Payments_Status");
    }
}
```

---

## AbuviDbContext Changes

Add to `src/Abuvi.API/Data/AbuviDbContext.cs`:

```csharp
public DbSet<Registration> Registrations => Set<Registration>();
public DbSet<RegistrationMember> RegistrationMembers => Set<RegistrationMember>();
public DbSet<RegistrationExtra> RegistrationExtras => Set<RegistrationExtra>();
public DbSet<Payment> Payments => Set<Payment>();
```

---

## Program.cs Changes

Add the following service registrations and endpoint mapping:

```csharp
// DI registrations
builder.Services.AddScoped<IRegistrationsRepository, RegistrationsRepository>();
builder.Services.AddScoped<IRegistrationExtrasRepository, RegistrationExtrasRepository>();
builder.Services.AddScoped<IPaymentsRepository, PaymentsRepository>();
builder.Services.AddScoped<RegistrationsService>();
builder.Services.AddScoped<RegistrationPricingService>();

// Endpoint mapping
app.MapRegistrationsEndpoints();
```

---

## API Endpoints

### RegistrationsEndpoints.cs

```
GET  /api/camps/editions/available         → List Open editions (Member+)
GET  /api/registrations                    → List my registrations (Member+)
GET  /api/registrations/{id}               → Get registration detail with pricing (Member+)
POST /api/registrations                    → Create registration (Member, representative only)
PUT  /api/registrations/{id}/members       → Update attending members (Member, representative only)
POST /api/registrations/{id}/extras        → Set extras selection (Member, representative only)
POST /api/registrations/{id}/cancel        → Cancel registration (Member, representative only)
```

**Authorization rules:**
- All endpoints require authentication (`RequireAuthorization()`)
- The representative check (family unit owner) is enforced in the service layer, not via policy, mirroring the existing `FamilyUnitsService` pattern
- Admin/Board can view all registrations via `GET /api/registrations?all=true` (add `[FromQuery] bool all = false` parameter)

**Important**: `DELETE /api/registrations/{id}` should be `POST /api/registrations/{id}/cancel` — it performs a status change (soft cancel), not an actual row deletion.

---

## Request/Response DTOs

```csharp
// POST /api/registrations
public record CreateRegistrationRequest(
    Guid CampEditionId,
    Guid FamilyUnitId,
    List<Guid> MemberIds,
    string? Notes
);

// PUT /api/registrations/{id}/members
public record UpdateRegistrationMembersRequest(List<Guid> MemberIds);

// POST /api/registrations/{id}/extras
public record UpdateRegistrationExtrasRequest(List<ExtraSelectionRequest> Extras);
public record ExtraSelectionRequest(Guid CampEditionExtraId, int Quantity);

// Response DTOs
public record AvailableCampEditionResponse(
    Guid Id, string CampName, int Year,
    DateTime StartDate, DateTime EndDate, string? Location,
    decimal PricePerAdult, decimal PricePerChild, decimal PricePerBaby,
    int? MaxCapacity, int CurrentRegistrations, int? SpotsRemaining,
    string Status, AgeRangesInfo AgeRanges
);

public record RegistrationResponse(
    Guid Id,
    FamilyUnitSummary FamilyUnit,
    CampEditionSummary CampEdition,
    RegistrationStatus Status,
    string? Notes,
    PricingBreakdown Pricing,
    List<PaymentSummary> Payments,
    decimal AmountPaid,
    decimal AmountRemaining,
    DateTime CreatedAt,
    DateTime UpdatedAt
);

public record PricingBreakdown(
    List<MemberPricingDetail> Members,
    decimal BaseTotalAmount,
    List<ExtraPricingDetail> Extras,
    decimal ExtrasAmount,
    decimal TotalAmount
);

public record MemberPricingDetail(
    Guid FamilyMemberId,
    string FullName,
    int AgeAtCamp,
    AgeCategory AgeCategory,
    decimal IndividualAmount
);

public record ExtraPricingDetail(
    Guid CampEditionExtraId,
    string Name,
    decimal UnitPrice,
    PricingType PricingType,
    PricingPeriod PricingPeriod,
    int Quantity,
    int? CampDurationDays,
    string Calculation,
    decimal TotalAmount
);
```

---

## Validators (Spanish messages as required by backend-standards.mdc)

### CreateRegistrationValidator.cs

```csharp
public class CreateRegistrationValidator : AbstractValidator<CreateRegistrationRequest>
{
    public CreateRegistrationValidator(
        ICampEditionsRepository editionsRepo,
        IFamilyMembersRepository membersRepo)
    {
        RuleFor(x => x.CampEditionId)
            .NotEmpty().WithMessage("La edición del campamento es obligatoria")
            .MustAsync(async (id, ct) =>
            {
                var edition = await editionsRepo.GetByIdAsync(id, ct);
                return edition?.Status == CampEditionStatus.Open;
            })
            .WithMessage("La edición del campamento no está abierta para inscripción");

        RuleFor(x => x.FamilyUnitId)
            .NotEmpty().WithMessage("La unidad familiar es obligatoria");

        RuleFor(x => x.MemberIds)
            .NotEmpty().WithMessage("Debe seleccionar al menos un miembro de la familia")
            .Must(ids => ids.Distinct().Count() == ids.Count)
            .WithMessage("No se puede incluir el mismo miembro dos veces");

        RuleFor(x => x.Notes)
            .MaximumLength(1000).WithMessage("Las notas no pueden superar los 1000 caracteres");
    }
}
```

### UpdateRegistrationExtrasValidator.cs

```csharp
public class UpdateRegistrationExtrasValidator : AbstractValidator<UpdateRegistrationExtrasRequest>
{
    public UpdateRegistrationExtrasValidator()
    {
        RuleForEach(x => x.Extras).ChildRules(extra =>
        {
            extra.RuleFor(e => e.CampEditionExtraId)
                .NotEmpty().WithMessage("El identificador del extra es obligatorio");
            extra.RuleFor(e => e.Quantity)
                .GreaterThan(0).WithMessage("La cantidad debe ser mayor que cero");
        });
    }
}
```

---

## Business Logic

### RegistrationsService.cs (key methods)

```csharp
// CreateAsync
// 1. Verify user is the family representative (throw BusinessRuleException if not)
// 2. Check no existing registration for this family + edition (409 if exists)
// 3. Verify edition is Open (409 if not)
// 4. Check capacity atomically using SELECT FOR UPDATE on camp_editions row OR
//    optimistic check: count existing non-cancelled registrations < MaxCapacity
//    (throw BusinessRuleException "El campamento ha alcanzado su capacidad máxima" if full)
// 5. For each memberId:
//    a. Verify member belongs to the family unit
//    b. Calculate age at edition.StartDate
//    c. Determine AgeCategory using configured age ranges
// 6. Calculate baseTotalAmount
// 7. Create Registration with Status=Pending, ExtrasAmount=0
// 8. Create RegistrationMember records in a single batch
// 9. Return full RegistrationResponse

// UpdateMembersAsync
// 1. Verify registration exists and status is Pending (422 if Confirmed/Cancelled)
// 2. Verify user is the family representative
// 3. Re-check capacity (adding new members could exceed capacity)
// 4. Delete existing RegistrationMembers
// 5. Recalculate pricing for new member set
// 6. Create new RegistrationMembers
// 7. Update Registration.BaseTotalAmount and TotalAmount
// 8. Return updated RegistrationResponse

// SetExtrasAsync
// 1. Verify registration exists and status is Pending
// 2. Verify user is the family representative
// 3. Verify each extra belongs to the same CampEdition as the registration
// 4. Check MaxQuantity constraints per extra
// 5. Delete existing RegistrationExtras
// 6. Calculate each extra's TotalAmount using RegistrationPricingService
// 7. Snapshot UnitPrice and CampDurationDays at time of creation
// 8. Create new RegistrationExtras in batch
// 9. Update Registration.ExtrasAmount and TotalAmount
// 10. Return updated response

// CancelAsync
// 1. Verify registration exists
// 2. Verify user is the family representative OR Admin/Board
// 3. Verify status is Pending or Confirmed (422 if already Cancelled)
// 4. Set status = Cancelled
// 5. Update UpdatedAt
// 6. Return success message (do not expose refund logic here)
```

### RegistrationPricingService.cs

```csharp
// CalculateAge(DateOnly dateOfBirth, DateTime campStartDate) → int
//   Calculate years between dateOfBirth and campStartDate

// GetAgeCategory(int age, AgeRanges ranges) → AgeCategory
//   Baby: age <= ranges.BabyMaxAge
//   Child: age >= ranges.ChildMinAge && age <= ranges.ChildMaxAge
//   Adult: age >= ranges.AdultMinAge
//   Throws BusinessRuleException if age fits no category

// GetAgeRanges(CampEdition edition) → AgeRanges
//   If edition.UseCustomAgeRanges: return custom values
//   Else: load from AssociationSettings key "age_ranges"

// GetPriceForCategory(AgeCategory category, CampEdition edition) → decimal
//   Adult → edition.PricePerAdult
//   Child → edition.PricePerChild
//   Baby  → edition.PricePerBaby

// CalculateExtraAmount(CampEditionExtra extra, int quantity, int campDurationDays) → decimal
//   PerPerson + OneTime:  price × quantity
//   PerPerson + PerDay:   price × quantity × campDurationDays
//   PerFamily + OneTime:  price
//   PerFamily + PerDay:   price × campDurationDays
```

### Capacity Check (Concurrency Safety)

Use optimistic check within a database transaction. In `RegistrationsRepository.CreateAsync`:

```csharp
// Within a transaction:
// 1. Count non-cancelled registrations for this edition
// 2. If count >= edition.MaxCapacity → throw BusinessRuleException
// 3. Insert registration
// Use IsolationLevel.RepeatableRead to prevent race conditions
```

### Auto-Confirm on Full Payment

In `PaymentsService.CompletePaymentAsync`:

```csharp
// After marking payment as Completed:
// 1. Sum all Completed payments for the registration
// 2. If totalPaid >= registration.TotalAmount AND status is Pending:
//    → Set status = Confirmed
//    → Save
//    → Optionally send confirmation email (future: IEmailService)
```

---

## IRegistrationsRepository

```csharp
public interface IRegistrationsRepository
{
    Task<Registration?> GetByIdAsync(Guid id, CancellationToken ct);
    Task<Registration?> GetByIdWithDetailsAsync(Guid id, CancellationToken ct);  // includes Members, Extras, Payments
    Task<IReadOnlyList<Registration>> GetByFamilyUnitAsync(Guid familyUnitId, CancellationToken ct);
    Task<IReadOnlyList<Registration>> GetByCampEditionAsync(Guid campEditionId, CancellationToken ct);
    Task<bool> ExistsAsync(Guid familyUnitId, Guid campEditionId, CancellationToken ct);
    Task<int> CountActiveByEditionAsync(Guid campEditionId, CancellationToken ct);  // non-cancelled
    Task AddAsync(Registration registration, CancellationToken ct);
    Task UpdateAsync(Registration registration, CancellationToken ct);
}
```

---

## Error Codes

| Situation | HTTP Status | Code |
|-----------|-------------|------|
| Camp edition not open | 422 | `EDITION_NOT_OPEN` |
| Duplicate registration | 409 | `REGISTRATION_EXISTS` |
| Camp at capacity | 409 | `CAMP_FULL` |
| Member not in family | 422 | `MEMBER_NOT_IN_FAMILY` |
| Extra not in edition | 422 | `EXTRA_NOT_IN_EDITION` |
| Registration not Pending | 422 | `REGISTRATION_NOT_EDITABLE` |
| Not the representative | 403 | `FORBIDDEN` |

---

## Test Coverage Requirements

### Unit Tests: RegistrationPricingServiceTests.cs

- `CalculateAge_OnBirthday_ReturnsExactAge`
- `CalculateAge_BeforeBirthday_ReturnsAgeMinusOne`
- `GetAgeCategory_WithGlobalRanges_ClassifiesCorrectly` (Baby/Child/Adult)
- `GetAgeCategory_WithCustomRanges_OverridesGlobal`
- `GetAgeCategory_AgeOutsideAllRanges_ThrowsBusinessRuleException`
- `CalculateExtraAmount_PerPersonOneTime_MultipliesByQuantity`
- `CalculateExtraAmount_PerPersonPerDay_MultipliesByQuantityAndDays`
- `CalculateExtraAmount_PerFamilyOneTime_ReturnsFixedPrice`
- `CalculateExtraAmount_PerFamilyPerDay_MultipliesByDays`

### Unit Tests: RegistrationsServiceTests.cs

- `CreateAsync_WhenEditionOpen_CreatesRegistrationWithCorrectPricing`
- `CreateAsync_WhenEditionNotOpen_ThrowsBusinessRuleException`
- `CreateAsync_WhenDuplicateRegistration_ThrowsBusinessRuleException`
- `CreateAsync_WhenCampFull_ThrowsBusinessRuleException`
- `CreateAsync_WhenMemberNotInFamily_ThrowsBusinessRuleException`
- `UpdateMembersAsync_WhenRegistrationPending_RecalculatesPricing`
- `UpdateMembersAsync_WhenRegistrationConfirmed_ThrowsBusinessRuleException`
- `SetExtrasAsync_WhenExtraNotInEdition_ThrowsBusinessRuleException`
- `SetExtrasAsync_WhenExceedsMaxQuantity_ThrowsBusinessRuleException`
- `CancelAsync_WhenStatusPending_SetsStatusCancelled`
- `CancelAsync_WhenStatusCancelled_ThrowsBusinessRuleException`

### Integration Tests: RegistrationsIntegrationTests.cs

- Full flow: create → set extras → get detail → cancel
- Capacity enforcement: register up to max, reject next
- Pricing recalculation when members updated
- Admin can view all registrations
- Non-representative member gets 403

---

## Migration

After implementing all entities and configurations, create a single migration:

```bash
dotnet ef migrations add AddRegistrationsPaymentsAndExtras --project src/Abuvi.API
dotnet ef database update --project src/Abuvi.API
```

---

## Implementation Steps (TDD)

1. **Create RegistrationPricingService + unit tests** (no DB dependency)
2. **Create entity configurations + migration** (run tests: `CampsDbSchemaTests` pattern)
3. **Create IRegistrationsRepository + RegistrationsRepository** (unit tests with NSubstitute mocks)
4. **Create RegistrationsService** (unit tests first, then implementation)
5. **Create validators** (unit tests for each rule)
6. **Create RegistrationsEndpoints** (integration tests with WebApplicationFactory)
7. **Wire up DI in Program.cs**
8. **Create test builders** (RegistrationBuilder, PaymentBuilder)

---

## Security Checklist

- [ ] Only the family representative can create/modify/cancel registrations
- [ ] Capacity check uses database-level transaction to prevent overbooking
- [ ] Pricing is always calculated server-side (never trust client amounts)
- [ ] Medical notes and allergies of FamilyMembers are never exposed in RegistrationMember responses
- [ ] Admin/Board can view all registrations but cannot modify member ones without being representative
- [ ] ExternalReference is required when Payment.Method = Card

---

## Outstanding Decisions

1. **Available editions endpoint**: Should it be Member-only or public? Recommend Member-only to match existing pattern.
2. **GET /api/registrations for Admin**: Add `?all=true` query parameter (Admin/Board only) or create a separate `GET /api/admin/registrations` endpoint. Recommend separate endpoint to keep authorization clear.
3. **Email notifications**: Confirmation emails on auto-confirm are out of scope for this sprint (no email service is fully wired yet per current codebase). Log the event instead.
4. **Payment endpoints**: Payment creation (`POST /api/payments`) and completion (`PATCH /api/payments/{id}/complete`) are a separate feature. The registration spec documents the auto-confirm trigger only.

---

## Document Control

- **Feature**: `feat-camp-registration-flow`
- **Original Spec**: `ai-specs/changes/feat-camps-registration/camp-registration-flow.md`
- **Depends On**: `feat-camps-definition` (CampEdition, CampEditionExtra already implemented)
- **Version**: 1.1 (Enriched)
- **Date**: 2026-02-17
- **Status**: Ready for Implementation
