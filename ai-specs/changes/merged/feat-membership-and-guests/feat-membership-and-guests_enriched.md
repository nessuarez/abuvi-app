# Enriched User Stories: Membership and Guests System

**Source:** [membership-and-guests-system.md](./feat-membership-and-guests/membership-and-guests-system.md)
**Status:** Ready for Implementation
**Priority:** High (Phase 1), Medium (Phase 2)
**Architecture:** Vertical Slice Architecture
**Approach:** Test-Driven Development (TDD)

---

## Table of Contents

- [Overview](#overview)
- [Phase 1: Membership System](#phase-1-membership-system)
  - [Epic 1.1: Membership Entities and Database](#epic-11-membership-entities-and-database)
  - [Epic 1.2: Membership Management API](#epic-12-membership-management-api)
  - [Epic 1.3: Membership Fee Management](#epic-13-membership-fee-management)
  - [Epic 1.4: Automated Fee Generation](#epic-14-automated-fee-generation)
- [Phase 2: Guests System](#phase-2-guests-system)
  - [Epic 2.1: Guest Entities and Database](#epic-21-guest-entities-and-database)
  - [Epic 2.2: Guest Management API](#epic-22-guest-management-api)
- [Technical Requirements](#technical-requirements)
- [Testing Requirements](#testing-requirements)
- [Documentation Requirements](#documentation-requirements)

---

## Overview

This document breaks down the Membership and Guests system into implementable user stories following the project's Vertical Slice Architecture and TDD approach.

### Context

The Family Units feature needs to distinguish between:

1. **Members (Socios)**: FamilyMembers who are active association members (pay annual fee)
2. **Non-Members**: FamilyMembers who are not association members
3. **Guests (Invitados)**: External people invited by families to attend camps

### Architecture Alignment

Following Vertical Slice Architecture, each feature will be organized as:

```
src/Abuvi.API/Features/
├── Memberships/                    # NEW
│   ├── MembershipsEndpoints.cs
│   ├── MembershipsModels.cs
│   ├── MembershipsService.cs
│   ├── MembershipsRepository.cs
│   ├── CreateMembershipValidator.cs
│   └── PayFeeValidator.cs
└── Guests/                         # NEW
    ├── GuestsEndpoints.cs
    ├── GuestsModels.cs
    ├── GuestsService.cs
    ├── GuestsRepository.cs
    ├── CreateGuestValidator.cs
    └── UpdateGuestValidator.cs
```

---

## Phase 1: Membership System

### Epic 1.1: Membership Entities and Database

**Goal:** Create database schema for membership and fee tracking

#### Story 1.1.1: Create Membership Entity and Configuration

**As a** developer
**I want** to create the Membership entity and EF Core configuration
**So that** we can store membership data in the database

**Acceptance Criteria:**

1. ✅ Create `Membership` entity in `Features/Memberships/MembershipsModels.cs`
2. ✅ Create `MembershipFee` entity in same file
3. ✅ Create `FeeStatus` enum in same file
4. ✅ Create EF Core configuration in `Data/Configurations/MembershipConfiguration.cs`
5. ✅ Create EF Core configuration in `Data/Configurations/MembershipFeeConfiguration.cs`
6. ✅ Add DbSets to `AbuviDbContext.cs`
7. ✅ Create and apply EF Core migration

**Files to Create/Modify:**

```
src/Abuvi.API/Features/Memberships/MembershipsModels.cs
src/Abuvi.API/Data/Configurations/MembershipConfiguration.cs
src/Abuvi.API/Data/Configurations/MembershipFeeConfiguration.cs
src/Abuvi.API/Data/AbuviDbContext.cs (modify - add DbSets)
```

**Entity Definitions:**

```csharp
// src/Abuvi.API/Features/Memberships/MembershipsModels.cs

namespace Abuvi.API.Features.Memberships;

/// <summary>
/// Represents an active membership for a family member
/// </summary>
public class Membership
{
    public Guid Id { get; set; }
    public Guid FamilyMemberId { get; set; }  // FK to FamilyMember
    public DateTime StartDate { get; set; }
    public DateTime? EndDate { get; set; }    // Nullable for active memberships
    public bool IsActive { get; set; }

    // Navigation
    public FamilyMember FamilyMember { get; set; } = null!;
    public ICollection<MembershipFee> Fees { get; set; } = new List<MembershipFee>();

    // Audit
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

/// <summary>
/// Represents an annual membership fee
/// </summary>
public class MembershipFee
{
    public Guid Id { get; set; }
    public Guid MembershipId { get; set; }
    public int Year { get; set; }
    public decimal Amount { get; set; }
    public FeeStatus Status { get; set; }
    public DateTime? PaidDate { get; set; }
    public string? PaymentReference { get; set; }

    // Navigation
    public Membership Membership { get; set; } = null!;

    // Audit
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public enum FeeStatus
{
    Pending,
    Paid,
    Overdue
}
```

**EF Core Configuration:**

```csharp
// src/Abuvi.API/Data/Configurations/MembershipConfiguration.cs

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Abuvi.API.Features.Memberships;

namespace Abuvi.API.Data.Configurations;

public class MembershipConfiguration : IEntityTypeConfiguration<Membership>
{
    public void Configure(EntityTypeBuilder<Membership> builder)
    {
        builder.HasKey(m => m.Id);
        builder.Property(m => m.Id).HasDefaultValueSql("gen_random_uuid()");

        // Unique constraint: one active membership per family member
        builder.HasIndex(m => m.FamilyMemberId).IsUnique();

        builder.Property(m => m.StartDate).IsRequired();
        builder.Property(m => m.IsActive).IsRequired();

        // Relationship to FamilyMember
        builder.HasOne(m => m.FamilyMember)
            .WithOne()
            .HasForeignKey<Membership>(m => m.FamilyMemberId)
            .OnDelete(DeleteBehavior.Restrict);

        // Relationship to Fees
        builder.HasMany(m => m.Fees)
            .WithOne(f => f.Membership)
            .HasForeignKey(f => f.MembershipId)
            .OnDelete(DeleteBehavior.Cascade);

        // Audit fields
        builder.Property(m => m.CreatedAt).IsRequired();
        builder.Property(m => m.UpdatedAt).IsRequired();
    }
}
```

```csharp
// src/Abuvi.API/Data/Configurations/MembershipFeeConfiguration.cs

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Abuvi.API.Features.Memberships;

namespace Abuvi.API.Data.Configurations;

public class MembershipFeeConfiguration : IEntityTypeConfiguration<MembershipFee>
{
    public void Configure(EntityTypeBuilder<MembershipFee> builder)
    {
        builder.HasKey(f => f.Id);
        builder.Property(f => f.Id).HasDefaultValueSql("gen_random_uuid()");

        // Unique constraint: one fee per membership per year
        builder.HasIndex(f => new { f.MembershipId, f.Year }).IsUnique();

        builder.Property(f => f.Year).IsRequired();
        builder.Property(f => f.Amount).HasPrecision(10, 2).IsRequired();
        builder.Property(f => f.Status)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(f => f.PaymentReference).HasMaxLength(100);

        // Audit fields
        builder.Property(f => f.CreatedAt).IsRequired();
        builder.Property(f => f.UpdatedAt).IsRequired();
    }
}
```

**Migration Command:**

```bash
dotnet ef migrations add AddMembershipEntities --project src/Abuvi.API
dotnet ef database update --project src/Abuvi.API
```

**TDD Approach:**

Before implementing, create failing tests:

```
src/Abuvi.Tests/Unit/Features/Memberships/MembershipEntityTests.cs
src/Abuvi.Tests/Integration/Features/Memberships/MembershipDatabaseTests.cs
```

Test that:

- Membership can be created with required fields
- FamilyMemberId uniqueness constraint works
- MembershipFee year+membership uniqueness constraint works
- Cascade delete of fees when membership deleted
- Audit fields are populated

---

#### Story 1.1.2: Create Membership Repository

**As a** developer
**I want** to create the repository for membership data access
**So that** services can interact with membership data

**Acceptance Criteria:**

1. ✅ Define `IMembershipsRepository` interface
2. ✅ Implement `MembershipsRepository` with EF Core
3. ✅ Register repository in DI container
4. ✅ Write unit tests for all repository methods
5. ✅ Write integration tests with in-memory database

**Files to Create/Modify:**

```
src/Abuvi.API/Features/Memberships/MembershipsRepository.cs
src/Abuvi.API/Program.cs (modify - register services)
src/Abuvi.Tests/Unit/Features/Memberships/MembershipsRepositoryTests.cs
src/Abuvi.Tests/Integration/Features/Memberships/MembershipsRepositoryIntegrationTests.cs
```

**Implementation:**

```csharp
// src/Abuvi.API/Features/Memberships/MembershipsRepository.cs

using Microsoft.EntityFrameworkCore;
using Abuvi.API.Data;

namespace Abuvi.API.Features.Memberships;

public interface IMembershipsRepository
{
    Task<Membership?> GetByIdAsync(Guid id, CancellationToken ct);
    Task<Membership?> GetByFamilyMemberIdAsync(Guid familyMemberId, CancellationToken ct);
    Task<IReadOnlyList<Membership>> GetActiveAsync(CancellationToken ct);
    Task<IReadOnlyList<Membership>> GetOverdueAsync(CancellationToken ct);
    Task AddAsync(Membership membership, CancellationToken ct);
    Task UpdateAsync(Membership membership, CancellationToken ct);
    Task<MembershipFee?> GetFeeByIdAsync(Guid feeId, CancellationToken ct);
    Task<MembershipFee?> GetCurrentYearFeeAsync(Guid membershipId, CancellationToken ct);
    Task<IReadOnlyList<MembershipFee>> GetFeesByMembershipAsync(Guid membershipId, CancellationToken ct);
    Task AddFeeAsync(MembershipFee fee, CancellationToken ct);
    Task UpdateFeeAsync(MembershipFee fee, CancellationToken ct);
}

public class MembershipsRepository(AbuviDbContext db) : IMembershipsRepository
{
    public async Task<Membership?> GetByIdAsync(Guid id, CancellationToken ct)
        => await db.Memberships
            .AsNoTracking()
            .Include(m => m.Fees)
            .FirstOrDefaultAsync(m => m.Id == id, ct);

    public async Task<Membership?> GetByFamilyMemberIdAsync(Guid familyMemberId, CancellationToken ct)
        => await db.Memberships
            .AsNoTracking()
            .Include(m => m.Fees)
            .FirstOrDefaultAsync(m => m.FamilyMemberId == familyMemberId && m.IsActive, ct);

    public async Task<IReadOnlyList<Membership>> GetActiveAsync(CancellationToken ct)
        => await db.Memberships
            .AsNoTracking()
            .Where(m => m.IsActive)
            .Include(m => m.FamilyMember)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<Membership>> GetOverdueAsync(CancellationToken ct)
    {
        var currentYear = DateTime.UtcNow.Year;
        return await db.Memberships
            .AsNoTracking()
            .Where(m => m.IsActive)
            .Include(m => m.Fees)
            .Where(m => m.Fees.Any(f => f.Year == currentYear && f.Status == FeeStatus.Overdue))
            .ToListAsync(ct);
    }

    public async Task AddAsync(Membership membership, CancellationToken ct)
    {
        db.Memberships.Add(membership);
        await db.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(Membership membership, CancellationToken ct)
    {
        db.Memberships.Update(membership);
        await db.SaveChangesAsync(ct);
    }

    public async Task<MembershipFee?> GetFeeByIdAsync(Guid feeId, CancellationToken ct)
        => await db.MembershipFees
            .AsNoTracking()
            .FirstOrDefaultAsync(f => f.Id == feeId, ct);

    public async Task<MembershipFee?> GetCurrentYearFeeAsync(Guid membershipId, CancellationToken ct)
    {
        var currentYear = DateTime.UtcNow.Year;
        return await db.MembershipFees
            .AsNoTracking()
            .FirstOrDefaultAsync(f => f.MembershipId == membershipId && f.Year == currentYear, ct);
    }

    public async Task<IReadOnlyList<MembershipFee>> GetFeesByMembershipAsync(Guid membershipId, CancellationToken ct)
        => await db.MembershipFees
            .AsNoTracking()
            .Where(f => f.MembershipId == membershipId)
            .OrderByDescending(f => f.Year)
            .ToListAsync(ct);

    public async Task AddFeeAsync(MembershipFee fee, CancellationToken ct)
    {
        db.MembershipFees.Add(fee);
        await db.SaveChangesAsync(ct);
    }

    public async Task UpdateFeeAsync(MembershipFee fee, CancellationToken ct)
    {
        db.MembershipFees.Update(fee);
        await db.SaveChangesAsync(ct);
    }
}
```

**Service Registration:**

```csharp
// src/Abuvi.API/Program.cs (add this line)

builder.Services.AddScoped<IMembershipsRepository, MembershipsRepository>();
```

**TDD Tests:**

Create tests that verify:

- GetByFamilyMemberIdAsync returns active membership only
- GetActiveAsync returns all active memberships
- GetOverdueAsync returns only memberships with overdue fees for current year
- GetCurrentYearFeeAsync returns fee for current year
- Proper eager loading of related entities

---

### Epic 1.2: Membership Management API

#### Story 1.2.1: Create Membership Service with Business Logic

**As a** developer
**I want** to implement membership business logic
**So that** membership rules are enforced correctly

**Acceptance Criteria:**

1. ✅ Create `MembershipsService` with business logic
2. ✅ Implement CreateMembership with validation
3. ✅ Implement DeactivateMembership
4. ✅ Implement GetMembership
5. ✅ Validate that FamilyMember exists before creating membership
6. ✅ Validate that FamilyMember doesn't already have active membership
7. ✅ Write comprehensive unit tests (TDD)

**Files to Create:**

```
src/Abuvi.API/Features/Memberships/MembershipsService.cs
src/Abuvi.Tests/Unit/Features/Memberships/MembershipsServiceTests.cs
```

**Implementation:**

```csharp
// src/Abuvi.API/Features/Memberships/MembershipsService.cs

using Abuvi.API.Features.FamilyUnits;
using Abuvi.API.Common.Exceptions;

namespace Abuvi.API.Features.Memberships;

public class MembershipsService(
    IMembershipsRepository repository,
    IFamilyUnitsRepository familyUnitsRepository)
{
    public async Task<MembershipResponse> CreateAsync(
        Guid familyMemberId,
        CreateMembershipRequest request,
        CancellationToken ct)
    {
        // Validate FamilyMember exists
        var familyMember = await familyUnitsRepository.GetFamilyMemberByIdAsync(familyMemberId, ct);
        if (familyMember is null)
            throw new NotFoundException(nameof(FamilyMember), familyMemberId);

        // Validate no active membership exists
        var existing = await repository.GetByFamilyMemberIdAsync(familyMemberId, ct);
        if (existing is not null)
            throw new BusinessRuleException("El miembro ya tiene una membresía activa");

        // Create membership
        var membership = new Membership
        {
            Id = Guid.NewGuid(),
            FamilyMemberId = familyMemberId,
            StartDate = request.StartDate,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        await repository.AddAsync(membership, ct);

        return membership.ToResponse();
    }

    public async Task<MembershipResponse> GetByFamilyMemberIdAsync(
        Guid familyMemberId,
        CancellationToken ct)
    {
        var membership = await repository.GetByFamilyMemberIdAsync(familyMemberId, ct);
        if (membership is null)
            throw new NotFoundException("Membership", familyMemberId);

        return membership.ToResponse();
    }

    public async Task DeactivateAsync(Guid familyMemberId, CancellationToken ct)
    {
        var membership = await repository.GetByFamilyMemberIdAsync(familyMemberId, ct);
        if (membership is null)
            throw new NotFoundException("Membership", familyMemberId);

        membership.IsActive = false;
        membership.EndDate = DateTime.UtcNow;
        membership.UpdatedAt = DateTime.UtcNow;

        await repository.UpdateAsync(membership, ct);
    }
}

// Extension methods for mapping
public static class MembershipExtensions
{
    public static MembershipResponse ToResponse(this Membership membership)
        => new(
            membership.Id,
            membership.FamilyMemberId,
            membership.StartDate,
            membership.EndDate,
            membership.IsActive,
            membership.Fees.Select(f => f.ToResponse()).ToList(),
            membership.CreatedAt,
            membership.UpdatedAt
        );

    public static MembershipFeeResponse ToResponse(this MembershipFee fee)
        => new(
            fee.Id,
            fee.MembershipId,
            fee.Year,
            fee.Amount,
            fee.Status,
            fee.PaidDate,
            fee.PaymentReference,
            fee.CreatedAt
        );
}
```

**DTOs in MembershipsModels.cs:**

```csharp
// Request DTOs
public record CreateMembershipRequest(DateTime StartDate);

// Response DTOs
public record MembershipResponse(
    Guid Id,
    Guid FamilyMemberId,
    DateTime StartDate,
    DateTime? EndDate,
    bool IsActive,
    IReadOnlyList<MembershipFeeResponse> Fees,
    DateTime CreatedAt,
    DateTime UpdatedAt
);

public record MembershipFeeResponse(
    Guid Id,
    Guid MembershipId,
    int Year,
    decimal Amount,
    FeeStatus Status,
    DateTime? PaidDate,
    string? PaymentReference,
    DateTime CreatedAt
);
```

**Service Registration:**

```csharp
// src/Abuvi.API/Program.cs

builder.Services.AddScoped<MembershipsService>();
```

**TDD Tests:**

Test scenarios:

- Creating membership for valid FamilyMember succeeds
- Creating membership for non-existent FamilyMember throws NotFoundException
- Creating membership when active membership exists throws BusinessRuleException
- Deactivating membership sets IsActive=false and EndDate
- Getting membership by FamilyMemberId returns correct data

---

#### Story 1.2.2: Create Membership Validators

**As a** developer
**I want** to validate membership requests
**So that** invalid data is rejected at the API boundary

**Acceptance Criteria:**

1. ✅ Create `CreateMembershipValidator`
2. ✅ Validation messages in Spanish
3. ✅ Validate StartDate is not in the future
4. ✅ Write validator unit tests

**Files to Create:**

```
src/Abuvi.API/Features/Memberships/CreateMembershipValidator.cs
src/Abuvi.Tests/Unit/Features/Memberships/CreateMembershipValidatorTests.cs
```

**Implementation:**

```csharp
// src/Abuvi.API/Features/Memberships/CreateMembershipValidator.cs

using FluentValidation;

namespace Abuvi.API.Features.Memberships;

public class CreateMembershipValidator : AbstractValidator<CreateMembershipRequest>
{
    public CreateMembershipValidator()
    {
        RuleFor(x => x.StartDate)
            .NotEmpty().WithMessage("La fecha de inicio es obligatoria")
            .LessThanOrEqualTo(DateTime.UtcNow).WithMessage("La fecha de inicio no puede ser futura");
    }
}
```

**Validator Registration:**

Validators are automatically registered via:

```csharp
// src/Abuvi.API/Program.cs (already exists)

builder.Services.AddValidatorsFromAssemblyContaining<Program>();
```

---

#### Story 1.2.3: Create Membership API Endpoints

**As a** user
**I want** to manage memberships via API
**So that** I can activate/deactivate member status

**Acceptance Criteria:**

1. ✅ POST `/api/family-units/{familyUnitId}/members/{memberId}/membership` - Create membership
2. ✅ GET `/api/family-units/{familyUnitId}/members/{memberId}/membership` - Get membership
3. ✅ DELETE `/api/family-units/{familyUnitId}/members/{memberId}/membership` - Deactivate membership
4. ✅ All endpoints have proper OpenAPI documentation
5. ✅ Validation filter applied to POST endpoint
6. ✅ Authorization checks (Representative or Admin/Board)
7. ✅ Integration tests for all endpoints

**Files to Create:**

```
src/Abuvi.API/Features/Memberships/MembershipsEndpoints.cs
src/Abuvi.Tests/Integration/Features/Memberships/MembershipsEndpointsTests.cs
```

**Implementation:**

```csharp
// src/Abuvi.API/Features/Memberships/MembershipsEndpoints.cs

using Microsoft.AspNetCore.Mvc;
using Abuvi.API.Common.Models;
using Abuvi.API.Common.Filters;

namespace Abuvi.API.Features.Memberships;

public static class MembershipsEndpoints
{
    public static void MapMembershipsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/family-units/{familyUnitId:guid}/members/{memberId:guid}/membership")
            .WithTags("Memberships")
            .RequireAuthorization(); // All require authentication

        group.MapPost("/", CreateMembership)
            .WithName("CreateMembership")
            .AddEndpointFilter<ValidationFilter<CreateMembershipRequest>>()
            .Produces<ApiResponse<MembershipResponse>>(StatusCodes.Status201Created)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status409Conflict);

        group.MapGet("/", GetMembership)
            .WithName("GetMembership")
            .Produces<ApiResponse<MembershipResponse>>()
            .Produces(StatusCodes.Status404NotFound);

        group.MapDelete("/", DeactivateMembership)
            .WithName("DeactivateMembership")
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status404NotFound);
    }

    private static async Task<IResult> CreateMembership(
        [FromRoute] Guid familyUnitId,
        [FromRoute] Guid memberId,
        [FromBody] CreateMembershipRequest request,
        MembershipsService service,
        CancellationToken ct)
    {
        // TODO: Add authorization check (Representative of family or Admin/Board)

        var membership = await service.CreateAsync(memberId, request, ct);
        return Results.Created(
            $"/api/family-units/{familyUnitId}/members/{memberId}/membership",
            ApiResponse<MembershipResponse>.Ok(membership));
    }

    private static async Task<IResult> GetMembership(
        [FromRoute] Guid familyUnitId,
        [FromRoute] Guid memberId,
        MembershipsService service,
        CancellationToken ct)
    {
        // TODO: Add authorization check

        var membership = await service.GetByFamilyMemberIdAsync(memberId, ct);
        return Results.Ok(ApiResponse<MembershipResponse>.Ok(membership));
    }

    private static async Task<IResult> DeactivateMembership(
        [FromRoute] Guid familyUnitId,
        [FromRoute] Guid memberId,
        MembershipsService service,
        CancellationToken ct)
    {
        // TODO: Add authorization check

        await service.DeactivateAsync(memberId, ct);
        return Results.NoContent();
    }
}
```

**Endpoint Registration:**

```csharp
// src/Abuvi.API/Program.cs (add after other endpoint mappings)

app.MapMembershipsEndpoints();
```

**Integration Tests:**

Test scenarios:

- POST with valid data returns 201 Created
- POST with invalid FamilyMember returns 404 Not Found
- POST when active membership exists returns 409 Conflict
- GET returns membership data
- DELETE deactivates membership and returns 204

---

### Epic 1.3: Membership Fee Management

#### Story 1.3.1: Create Fee Management Service

**As a** developer
**I want** to implement fee management logic
**So that** annual fees can be tracked and paid

**Acceptance Criteria:**

1. ✅ Implement GetFees (list all fees for a membership)
2. ✅ Implement GetCurrentYearFee
3. ✅ Implement PayFee (mark fee as paid)
4. ✅ Validate payment date is not future
5. ✅ Prevent double payment
6. ✅ Write unit tests

**Files to Modify:**

```
src/Abuvi.API/Features/Memberships/MembershipsService.cs (add methods)
src/Abuvi.Tests/Unit/Features/Memberships/MembershipsServiceTests.cs (add tests)
```

**Implementation:**

```csharp
// Add to MembershipsService

public async Task<IReadOnlyList<MembershipFeeResponse>> GetFeesAsync(
    Guid membershipId,
    CancellationToken ct)
{
    var fees = await repository.GetFeesByMembershipAsync(membershipId, ct);
    return fees.Select(f => f.ToResponse()).ToList();
}

public async Task<MembershipFeeResponse> GetCurrentYearFeeAsync(
    Guid membershipId,
    CancellationToken ct)
{
    var fee = await repository.GetCurrentYearFeeAsync(membershipId, ct);
    if (fee is null)
        throw new NotFoundException("MembershipFee", membershipId);

    return fee.ToResponse();
}

public async Task<MembershipFeeResponse> PayFeeAsync(
    Guid feeId,
    PayFeeRequest request,
    CancellationToken ct)
{
    var fee = await repository.GetFeeByIdAsync(feeId, ct);
    if (fee is null)
        throw new NotFoundException(nameof(MembershipFee), feeId);

    if (fee.Status == FeeStatus.Paid)
        throw new BusinessRuleException("La cuota ya está pagada");

    fee.Status = FeeStatus.Paid;
    fee.PaidDate = request.PaidDate;
    fee.PaymentReference = request.PaymentReference;
    fee.UpdatedAt = DateTime.UtcNow;

    await repository.UpdateFeeAsync(fee, ct);

    return fee.ToResponse();
}
```

**DTOs:**

```csharp
// Add to MembershipsModels.cs

public record PayFeeRequest(
    DateTime PaidDate,
    string? PaymentReference = null
);
```

**Validator:**

```csharp
// src/Abuvi.API/Features/Memberships/PayFeeValidator.cs

using FluentValidation;

namespace Abuvi.API.Features.Memberships;

public class PayFeeValidator : AbstractValidator<PayFeeRequest>
{
    public PayFeeValidator()
    {
        RuleFor(x => x.PaidDate)
            .NotEmpty().WithMessage("La fecha de pago es obligatoria")
            .LessThanOrEqualTo(DateTime.UtcNow).WithMessage("La fecha de pago no puede ser futura");

        RuleFor(x => x.PaymentReference)
            .MaximumLength(100).WithMessage("La referencia de pago no puede exceder 100 caracteres");
    }
}
```

---

#### Story 1.3.2: Create Fee Management Endpoints

**As a** user
**I want** to manage membership fees via API
**So that** I can track and mark payments

**Acceptance Criteria:**

1. ✅ GET `/api/memberships/{membershipId}/fees` - List fees
2. ✅ GET `/api/memberships/{membershipId}/fees/current` - Get current year fee
3. ✅ POST `/api/memberships/{membershipId}/fees/{feeId}/pay` - Mark fee as paid
4. ✅ All endpoints documented
5. ✅ Integration tests

**Files to Modify:**

```
src/Abuvi.API/Features/Memberships/MembershipsEndpoints.cs (add fee endpoints)
src/Abuvi.Tests/Integration/Features/Memberships/MembershipsEndpointsTests.cs (add tests)
```

**Implementation:**

```csharp
// Add to MembershipsEndpoints

public static void MapMembershipFeeEndpoints(this IEndpointRouteBuilder app)
{
    var group = app.MapGroup("/api/memberships/{membershipId:guid}/fees")
        .WithTags("Membership Fees")
        .RequireAuthorization();

    group.MapGet("/", GetFees)
        .WithName("GetMembershipFees")
        .Produces<ApiResponse<IReadOnlyList<MembershipFeeResponse>>>();

    group.MapGet("/current", GetCurrentYearFee)
        .WithName("GetCurrentYearFee")
        .Produces<ApiResponse<MembershipFeeResponse>>()
        .Produces(StatusCodes.Status404NotFound);

    group.MapPost("/{feeId:guid}/pay", PayFee)
        .WithName("PayFee")
        .AddEndpointFilter<ValidationFilter<PayFeeRequest>>()
        .Produces<ApiResponse<MembershipFeeResponse>>()
        .Produces(StatusCodes.Status404NotFound)
        .Produces(StatusCodes.Status409Conflict);
}

private static async Task<IResult> GetFees(
    [FromRoute] Guid membershipId,
    MembershipsService service,
    CancellationToken ct)
{
    var fees = await service.GetFeesAsync(membershipId, ct);
    return Results.Ok(ApiResponse<IReadOnlyList<MembershipFeeResponse>>.Ok(fees));
}

private static async Task<IResult> GetCurrentYearFee(
    [FromRoute] Guid membershipId,
    MembershipsService service,
    CancellationToken ct)
{
    var fee = await service.GetCurrentYearFeeAsync(membershipId, ct);
    return Results.Ok(ApiResponse<MembershipFeeResponse>.Ok(fee));
}

private static async Task<IResult> PayFee(
    [FromRoute] Guid membershipId,
    [FromRoute] Guid feeId,
    [FromBody] PayFeeRequest request,
    MembershipsService service,
    CancellationToken ct)
{
    var fee = await service.PayFeeAsync(feeId, request, ct);
    return Results.Ok(ApiResponse<MembershipFeeResponse>.Ok(fee));
}
```

**Endpoint Registration:**

```csharp
// src/Abuvi.API/Program.cs

app.MapMembershipFeeEndpoints();
```

---

### Epic 1.4: Automated Fee Generation

#### Story 1.4.1: Create Annual Fee Generation Background Service

**As a** system administrator
**I want** annual fees to be generated automatically
**So that** members are billed each year

**Acceptance Criteria:**

1. ✅ Create background service that runs on January 1st
2. ✅ Generate fees for all active memberships
3. ✅ Set default amount from configuration
4. ✅ Log fee generation events
5. ✅ Handle errors gracefully
6. ✅ Write unit tests

**Files to Create:**

```
src/Abuvi.API/Common/BackgroundServices/AnnualFeeGenerationService.cs
src/Abuvi.Tests/Unit/BackgroundServices/AnnualFeeGenerationServiceTests.cs
```

**Implementation:**

```csharp
// src/Abuvi.API/Common/BackgroundServices/AnnualFeeGenerationService.cs

using Abuvi.API.Features.Memberships;

namespace Abuvi.API.Common.BackgroundServices;

public class AnnualFeeGenerationService(
    IServiceProvider serviceProvider,
    IConfiguration configuration,
    ILogger<AnnualFeeGenerationService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var now = DateTime.UtcNow;

            // Calculate time until January 1st at 00:00
            var nextRun = new DateTime(now.Year + 1, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            if (now.Month == 1 && now.Day == 1 && now.Hour == 0)
            {
                await GenerateAnnualFeesAsync(stoppingToken);
                nextRun = nextRun.AddYears(1);
            }

            var delay = nextRun - now;
            logger.LogInformation("Next annual fee generation scheduled for {NextRun}", nextRun);

            await Task.Delay(delay, stoppingToken);
        }
    }

    private async Task GenerateAnnualFeesAsync(CancellationToken ct)
    {
        logger.LogInformation("Starting annual fee generation for year {Year}", DateTime.UtcNow.Year);

        try
        {
            using var scope = serviceProvider.CreateScope();
            var repository = scope.ServiceProvider.GetRequiredService<IMembershipsRepository>();

            var activeMemberships = await repository.GetActiveAsync(ct);
            var defaultAmount = configuration.GetValue<decimal>("Membership:AnnualFeeAmount", 50.00m);
            var currentYear = DateTime.UtcNow.Year;

            var generatedCount = 0;

            foreach (var membership in activeMemberships)
            {
                // Check if fee already exists for this year
                var existingFee = await repository.GetCurrentYearFeeAsync(membership.Id, ct);
                if (existingFee is not null)
                {
                    logger.LogWarning(
                        "Fee already exists for membership {MembershipId} for year {Year}",
                        membership.Id,
                        currentYear);
                    continue;
                }

                var fee = new MembershipFee
                {
                    Id = Guid.NewGuid(),
                    MembershipId = membership.Id,
                    Year = currentYear,
                    Amount = defaultAmount,
                    Status = FeeStatus.Pending,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                await repository.AddFeeAsync(fee, ct);
                generatedCount++;

                logger.LogInformation(
                    "Generated fee {FeeId} for membership {MembershipId}, amount {Amount}",
                    fee.Id,
                    membership.Id,
                    fee.Amount);
            }

            logger.LogInformation(
                "Annual fee generation completed. Generated {Count} fees for year {Year}",
                generatedCount,
                currentYear);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error during annual fee generation");
        }
    }
}
```

**Service Registration:**

```csharp
// src/Abuvi.API/Program.cs

builder.Services.AddHostedService<AnnualFeeGenerationService>();
```

**Configuration:**

```json
// src/Abuvi.API/appsettings.json

{
  "Membership": {
    "AnnualFeeAmount": 50.00
  }
}
```

---

## Phase 2: Guests System

### Epic 2.1: Guest Entities and Database

#### Story 2.1.1: Create Guest Entity and Configuration

**As a** developer
**I want** to create the Guest entity and database schema
**So that** families can register external guests

**Acceptance Criteria:**

1. ✅ Create `Guest` entity in `Features/Guests/GuestsModels.cs`
2. ✅ Create EF Core configuration
3. ✅ Add DbSet to AbuviDbContext
4. ✅ Create and apply migration
5. ✅ Add relationship to FamilyUnit (cascade delete)
6. ✅ Encrypt sensitive fields (MedicalNotes, Allergies)
7. ✅ Write entity and database tests

**Files to Create/Modify:**

```
src/Abuvi.API/Features/Guests/GuestsModels.cs
src/Abuvi.API/Data/Configurations/GuestConfiguration.cs
src/Abuvi.API/Data/AbuviDbContext.cs (modify)
src/Abuvi.Tests/Unit/Features/Guests/GuestEntityTests.cs
src/Abuvi.Tests/Integration/Features/Guests/GuestDatabaseTests.cs
```

**Entity Definition:**

```csharp
// src/Abuvi.API/Features/Guests/GuestsModels.cs

namespace Abuvi.API.Features.Guests;

/// <summary>
/// External guest invited by a family to attend camps
/// </summary>
public class Guest
{
    public Guid Id { get; set; }
    public Guid FamilyUnitId { get; set; }

    // Personal data
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public DateOnly DateOfBirth { get; set; }
    public string? DocumentNumber { get; set; }  // Uppercase alphanumeric
    public string? Email { get; set; }
    public string? Phone { get; set; }  // E.164 format

    // Encrypted sensitive health data
    public string? MedicalNotes { get; set; }  // Encrypted
    public string? Allergies { get; set; }     // Encrypted

    // Status
    public bool IsActive { get; set; }

    // Navigation
    public FamilyUnit FamilyUnit { get; set; } = null!;

    // Audit
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
```

**EF Core Configuration:**

```csharp
// src/Abuvi.API/Data/Configurations/GuestConfiguration.cs

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Abuvi.API.Features.Guests;

namespace Abuvi.API.Data.Configurations;

public class GuestConfiguration : IEntityTypeConfiguration<Guest>
{
    public void Configure(EntityTypeBuilder<Guest> builder)
    {
        builder.HasKey(g => g.Id);
        builder.Property(g => g.Id).HasDefaultValueSql("gen_random_uuid()");

        // Personal data
        builder.Property(g => g.FirstName).IsRequired().HasMaxLength(100);
        builder.Property(g => g.LastName).IsRequired().HasMaxLength(100);
        builder.Property(g => g.DateOfBirth).IsRequired();
        builder.Property(g => g.DocumentNumber).HasMaxLength(50);
        builder.Property(g => g.Email).HasMaxLength(255);
        builder.Property(g => g.Phone).HasMaxLength(20);

        // Encrypted fields - stored as text
        builder.Property(g => g.MedicalNotes).HasColumnType("text");
        builder.Property(g => g.Allergies).HasColumnType("text");

        builder.Property(g => g.IsActive).IsRequired();

        // Relationship to FamilyUnit - cascade delete
        builder.HasOne(g => g.FamilyUnit)
            .WithMany()
            .HasForeignKey(g => g.FamilyUnitId)
            .OnDelete(DeleteBehavior.Cascade);

        // Audit fields
        builder.Property(g => g.CreatedAt).IsRequired();
        builder.Property(g => g.UpdatedAt).IsRequired();

        // Indexes
        builder.HasIndex(g => g.FamilyUnitId);
        builder.HasIndex(g => g.DocumentNumber);
    }
}
```

**Migration Command:**

```bash
dotnet ef migrations add AddGuestEntity --project src/Abuvi.API
dotnet ef database update --project src/Abuvi.API
```

**TDD Tests:**

Test that:

- Guest can be created with required fields
- FamilyUnitId foreign key constraint works
- Cascade delete removes guests when family unit deleted
- DocumentNumber and Email validation
- Audit fields populated

---

#### Story 2.1.2: Create Guests Repository

**As a** developer
**I want** to create the repository for guest data access
**So that** services can interact with guest data

**Acceptance Criteria:**

1. ✅ Define `IGuestsRepository` interface
2. ✅ Implement `GuestsRepository`
3. ✅ Implement encryption/decryption for sensitive fields
4. ✅ Register repository in DI
5. ✅ Write unit and integration tests

**Files to Create:**

```
src/Abuvi.API/Features/Guests/GuestsRepository.cs
src/Abuvi.Tests/Unit/Features/Guests/GuestsRepositoryTests.cs
src/Abuvi.Tests/Integration/Features/Guests/GuestsRepositoryIntegrationTests.cs
```

**Implementation:**

```csharp
// src/Abuvi.API/Features/Guests/GuestsRepository.cs

using Microsoft.EntityFrameworkCore;
using Abuvi.API.Data;
using Abuvi.API.Common.Services;

namespace Abuvi.API.Features.Guests;

public interface IGuestsRepository
{
    Task<Guest?> GetByIdAsync(Guid id, CancellationToken ct);
    Task<IReadOnlyList<Guest>> GetByFamilyUnitAsync(Guid familyUnitId, CancellationToken ct);
    Task AddAsync(Guest guest, CancellationToken ct);
    Task UpdateAsync(Guest guest, CancellationToken ct);
    Task DeleteAsync(Guid id, CancellationToken ct);
}

public class GuestsRepository(AbuviDbContext db, IEncryptionService encryption) : IGuestsRepository
{
    public async Task<Guest?> GetByIdAsync(Guid id, CancellationToken ct)
    {
        var guest = await db.Guests
            .AsNoTracking()
            .FirstOrDefaultAsync(g => g.Id == id, ct);

        if (guest is not null)
            DecryptSensitiveFields(guest);

        return guest;
    }

    public async Task<IReadOnlyList<Guest>> GetByFamilyUnitAsync(Guid familyUnitId, CancellationToken ct)
    {
        var guests = await db.Guests
            .AsNoTracking()
            .Where(g => g.FamilyUnitId == familyUnitId && g.IsActive)
            .OrderBy(g => g.LastName)
            .ThenBy(g => g.FirstName)
            .ToListAsync(ct);

        foreach (var guest in guests)
            DecryptSensitiveFields(guest);

        return guests;
    }

    public async Task AddAsync(Guest guest, CancellationToken ct)
    {
        EncryptSensitiveFields(guest);
        db.Guests.Add(guest);
        await db.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(Guest guest, CancellationToken ct)
    {
        EncryptSensitiveFields(guest);
        db.Guests.Update(guest);
        await db.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct)
    {
        var guest = await db.Guests.FindAsync([id], ct);
        if (guest is not null)
        {
            db.Guests.Remove(guest);
            await db.SaveChangesAsync(ct);
        }
    }

    private void EncryptSensitiveFields(Guest guest)
    {
        if (!string.IsNullOrEmpty(guest.MedicalNotes))
            guest.MedicalNotes = encryption.Encrypt(guest.MedicalNotes);

        if (!string.IsNullOrEmpty(guest.Allergies))
            guest.Allergies = encryption.Encrypt(guest.Allergies);
    }

    private void DecryptSensitiveFields(Guest guest)
    {
        if (!string.IsNullOrEmpty(guest.MedicalNotes))
            guest.MedicalNotes = encryption.Decrypt(guest.MedicalNotes);

        if (!string.IsNullOrEmpty(guest.Allergies))
            guest.Allergies = encryption.Decrypt(guest.Allergies);
    }
}
```

**Service Registration:**

```csharp
// src/Abuvi.API/Program.cs

builder.Services.AddScoped<IGuestsRepository, GuestsRepository>();
```

---

### Epic 2.2: Guest Management API

#### Story 2.2.1: Create Guests Service

**As a** developer
**I want** to implement guest business logic
**So that** guest management rules are enforced

**Acceptance Criteria:**

1. ✅ Implement CreateGuest
2. ✅ Implement UpdateGuest
3. ✅ Implement GetGuest
4. ✅ Implement ListGuests (by family)
5. ✅ Implement DeleteGuest (soft delete with IsActive)
6. ✅ Validate FamilyUnit exists
7. ✅ Normalize DocumentNumber to uppercase
8. ✅ Write unit tests

**Files to Create:**

```
src/Abuvi.API/Features/Guests/GuestsService.cs
src/Abuvi.Tests/Unit/Features/Guests/GuestsServiceTests.cs
```

**Implementation:**

```csharp
// src/Abuvi.API/Features/Guests/GuestsService.cs

using Abuvi.API.Features.FamilyUnits;
using Abuvi.API.Common.Exceptions;

namespace Abuvi.API.Features.Guests;

public class GuestsService(
    IGuestsRepository repository,
    IFamilyUnitsRepository familyUnitsRepository)
{
    public async Task<GuestResponse> CreateAsync(
        Guid familyUnitId,
        CreateGuestRequest request,
        CancellationToken ct)
    {
        // Validate FamilyUnit exists
        var familyUnit = await familyUnitsRepository.GetByIdAsync(familyUnitId, ct);
        if (familyUnit is null)
            throw new NotFoundException(nameof(FamilyUnit), familyUnitId);

        var guest = new Guest
        {
            Id = Guid.NewGuid(),
            FamilyUnitId = familyUnitId,
            FirstName = request.FirstName,
            LastName = request.LastName,
            DateOfBirth = request.DateOfBirth,
            DocumentNumber = request.DocumentNumber?.ToUpperInvariant(),
            Email = request.Email,
            Phone = request.Phone,
            MedicalNotes = request.MedicalNotes,
            Allergies = request.Allergies,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        await repository.AddAsync(guest, ct);

        return guest.ToResponse();
    }

    public async Task<GuestResponse> UpdateAsync(
        Guid id,
        UpdateGuestRequest request,
        CancellationToken ct)
    {
        var guest = await repository.GetByIdAsync(id, ct);
        if (guest is null)
            throw new NotFoundException(nameof(Guest), id);

        guest.FirstName = request.FirstName;
        guest.LastName = request.LastName;
        guest.DateOfBirth = request.DateOfBirth;
        guest.DocumentNumber = request.DocumentNumber?.ToUpperInvariant();
        guest.Email = request.Email;
        guest.Phone = request.Phone;
        guest.MedicalNotes = request.MedicalNotes;
        guest.Allergies = request.Allergies;
        guest.UpdatedAt = DateTime.UtcNow;

        await repository.UpdateAsync(guest, ct);

        return guest.ToResponse();
    }

    public async Task<GuestResponse> GetByIdAsync(Guid id, CancellationToken ct)
    {
        var guest = await repository.GetByIdAsync(id, ct);
        if (guest is null)
            throw new NotFoundException(nameof(Guest), id);

        return guest.ToResponse();
    }

    public async Task<IReadOnlyList<GuestResponse>> GetByFamilyUnitAsync(
        Guid familyUnitId,
        CancellationToken ct)
    {
        var guests = await repository.GetByFamilyUnitAsync(familyUnitId, ct);
        return guests.Select(g => g.ToResponse()).ToList();
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct)
    {
        var guest = await repository.GetByIdAsync(id, ct);
        if (guest is null)
            throw new NotFoundException(nameof(Guest), id);

        // Soft delete
        guest.IsActive = false;
        guest.UpdatedAt = DateTime.UtcNow;

        await repository.UpdateAsync(guest, ct);
    }
}

// Extension methods
public static class GuestExtensions
{
    public static GuestResponse ToResponse(this Guest guest)
        => new(
            guest.Id,
            guest.FamilyUnitId,
            guest.FirstName,
            guest.LastName,
            guest.DateOfBirth,
            guest.DocumentNumber,
            guest.Email,
            guest.Phone,
            HasMedicalNotes: !string.IsNullOrEmpty(guest.MedicalNotes),
            HasAllergies: !string.IsNullOrEmpty(guest.Allergies),
            guest.IsActive,
            guest.CreatedAt,
            guest.UpdatedAt
        );
}
```

**DTOs in GuestsModels.cs:**

```csharp
// Request DTOs
public record CreateGuestRequest(
    string FirstName,
    string LastName,
    DateOnly DateOfBirth,
    string? DocumentNumber = null,
    string? Email = null,
    string? Phone = null,
    string? MedicalNotes = null,
    string? Allergies = null
);

public record UpdateGuestRequest(
    string FirstName,
    string LastName,
    DateOnly DateOfBirth,
    string? DocumentNumber = null,
    string? Email = null,
    string? Phone = null,
    string? MedicalNotes = null,
    string? Allergies = null
);

// Response DTOs
public record GuestResponse(
    Guid Id,
    Guid FamilyUnitId,
    string FirstName,
    string LastName,
    DateOnly DateOfBirth,
    string? DocumentNumber,
    string? Email,
    string? Phone,
    bool HasMedicalNotes,    // Never expose encrypted data
    bool HasAllergies,       // Never expose encrypted data
    bool IsActive,
    DateTime CreatedAt,
    DateTime UpdatedAt
);
```

**Service Registration:**

```csharp
// src/Abuvi.API/Program.cs

builder.Services.AddScoped<GuestsService>();
```

---

#### Story 2.2.2: Create Guest Validators

**As a** developer
**I want** to validate guest requests
**So that** invalid data is rejected

**Acceptance Criteria:**

1. ✅ Create `CreateGuestValidator`
2. ✅ Create `UpdateGuestValidator`
3. ✅ Validation messages in Spanish
4. ✅ Validate required fields
5. ✅ Validate formats (email, phone, document)
6. ✅ Validate field lengths
7. ✅ Write validator tests

**Files to Create:**

```
src/Abuvi.API/Features/Guests/CreateGuestValidator.cs
src/Abuvi.API/Features/Guests/UpdateGuestValidator.cs
src/Abuvi.Tests/Unit/Features/Guests/CreateGuestValidatorTests.cs
src/Abuvi.Tests/Unit/Features/Guests/UpdateGuestValidatorTests.cs
```

**Implementation:**

```csharp
// src/Abuvi.API/Features/Guests/CreateGuestValidator.cs

using FluentValidation;

namespace Abuvi.API.Features.Guests;

public class CreateGuestValidator : AbstractValidator<CreateGuestRequest>
{
    public CreateGuestValidator()
    {
        RuleFor(x => x.FirstName)
            .NotEmpty().WithMessage("El nombre es obligatorio")
            .MaximumLength(100).WithMessage("El nombre no puede exceder 100 caracteres");

        RuleFor(x => x.LastName)
            .NotEmpty().WithMessage("Los apellidos son obligatorios")
            .MaximumLength(100).WithMessage("Los apellidos no pueden exceder 100 caracteres");

        RuleFor(x => x.DateOfBirth)
            .NotEmpty().WithMessage("La fecha de nacimiento es obligatoria")
            .LessThan(DateOnly.FromDateTime(DateTime.UtcNow))
                .WithMessage("La fecha de nacimiento debe ser una fecha pasada");

        RuleFor(x => x.DocumentNumber)
            .MaximumLength(50).WithMessage("El número de documento no puede exceder 50 caracteres")
            .Matches("^[A-Z0-9]*$").WithMessage("El número de documento solo puede contener letras mayúsculas y números")
            .When(x => !string.IsNullOrEmpty(x.DocumentNumber));

        RuleFor(x => x.Email)
            .EmailAddress().WithMessage("Formato de correo electrónico inválido")
            .MaximumLength(255).WithMessage("El correo electrónico no puede exceder 255 caracteres")
            .When(x => !string.IsNullOrEmpty(x.Email));

        RuleFor(x => x.Phone)
            .Matches(@"^\+[1-9]\d{1,14}$").WithMessage("El teléfono debe estar en formato E.164 (ej. +34612345678)")
            .When(x => !string.IsNullOrEmpty(x.Phone));

        RuleFor(x => x.MedicalNotes)
            .MaximumLength(2000).WithMessage("Las notas médicas no pueden exceder 2000 caracteres")
            .When(x => !string.IsNullOrEmpty(x.MedicalNotes));

        RuleFor(x => x.Allergies)
            .MaximumLength(1000).WithMessage("Las alergias no pueden exceder 1000 caracteres")
            .When(x => !string.IsNullOrEmpty(x.Allergies));
    }
}
```

```csharp
// src/Abuvi.API/Features/Guests/UpdateGuestValidator.cs

using FluentValidation;

namespace Abuvi.API.Features.Guests;

public class UpdateGuestValidator : AbstractValidator<UpdateGuestRequest>
{
    public UpdateGuestValidator()
    {
        // Same rules as CreateGuestValidator
        RuleFor(x => x.FirstName)
            .NotEmpty().WithMessage("El nombre es obligatorio")
            .MaximumLength(100).WithMessage("El nombre no puede exceder 100 caracteres");

        RuleFor(x => x.LastName)
            .NotEmpty().WithMessage("Los apellidos son obligatorios")
            .MaximumLength(100).WithMessage("Los apellidos no pueden exceder 100 caracteres");

        RuleFor(x => x.DateOfBirth)
            .NotEmpty().WithMessage("La fecha de nacimiento es obligatoria")
            .LessThan(DateOnly.FromDateTime(DateTime.UtcNow))
                .WithMessage("La fecha de nacimiento debe ser una fecha pasada");

        RuleFor(x => x.DocumentNumber)
            .MaximumLength(50).WithMessage("El número de documento no puede exceder 50 caracteres")
            .Matches("^[A-Z0-9]*$").WithMessage("El número de documento solo puede contener letras mayúsculas y números")
            .When(x => !string.IsNullOrEmpty(x.DocumentNumber));

        RuleFor(x => x.Email)
            .EmailAddress().WithMessage("Formato de correo electrónico inválido")
            .MaximumLength(255).WithMessage("El correo electrónico no puede exceder 255 caracteres")
            .When(x => !string.IsNullOrEmpty(x.Email));

        RuleFor(x => x.Phone)
            .Matches(@"^\+[1-9]\d{1,14}$").WithMessage("El teléfono debe estar en formato E.164 (ej. +34612345678)")
            .When(x => !string.IsNullOrEmpty(x.Phone));

        RuleFor(x => x.MedicalNotes)
            .MaximumLength(2000).WithMessage("Las notas médicas no pueden exceder 2000 caracteres")
            .When(x => !string.IsNullOrEmpty(x.MedicalNotes));

        RuleFor(x => x.Allergies)
            .MaximumLength(1000).WithMessage("Las alergias no pueden exceder 1000 caracteres")
            .When(x => !string.IsNullOrEmpty(x.Allergies));
    }
}
```

---

#### Story 2.2.3: Create Guest API Endpoints

**As a** user
**I want** to manage guests via API
**So that** I can register external people for camps

**Acceptance Criteria:**

1. ✅ POST `/api/family-units/{familyUnitId}/guests` - Create guest
2. ✅ GET `/api/family-units/{familyUnitId}/guests` - List guests
3. ✅ GET `/api/family-units/{familyUnitId}/guests/{guestId}` - Get guest
4. ✅ PUT `/api/family-units/{familyUnitId}/guests/{guestId}` - Update guest
5. ✅ DELETE `/api/family-units/{familyUnitId}/guests/{guestId}` - Delete guest
6. ✅ All endpoints documented
7. ✅ Authorization (Representative or Admin/Board)
8. ✅ Integration tests

**Files to Create:**

```
src/Abuvi.API/Features/Guests/GuestsEndpoints.cs
src/Abuvi.Tests/Integration/Features/Guests/GuestsEndpointsTests.cs
```

**Implementation:**

```csharp
// src/Abuvi.API/Features/Guests/GuestsEndpoints.cs

using Microsoft.AspNetCore.Mvc;
using Abuvi.API.Common.Models;
using Abuvi.API.Common.Filters;

namespace Abuvi.API.Features.Guests;

public static class GuestsEndpoints
{
    public static void MapGuestsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/family-units/{familyUnitId:guid}/guests")
            .WithTags("Guests")
            .RequireAuthorization();

        group.MapPost("/", CreateGuest)
            .WithName("CreateGuest")
            .AddEndpointFilter<ValidationFilter<CreateGuestRequest>>()
            .Produces<ApiResponse<GuestResponse>>(StatusCodes.Status201Created)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status404NotFound);

        group.MapGet("/", ListGuests)
            .WithName("ListGuests")
            .Produces<ApiResponse<IReadOnlyList<GuestResponse>>>();

        group.MapGet("/{guestId:guid}", GetGuest)
            .WithName("GetGuest")
            .Produces<ApiResponse<GuestResponse>>()
            .Produces(StatusCodes.Status404NotFound);

        group.MapPut("/{guestId:guid}", UpdateGuest)
            .WithName("UpdateGuest")
            .AddEndpointFilter<ValidationFilter<UpdateGuestRequest>>()
            .Produces<ApiResponse<GuestResponse>>()
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status404NotFound);

        group.MapDelete("/{guestId:guid}", DeleteGuest)
            .WithName("DeleteGuest")
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status404NotFound);
    }

    private static async Task<IResult> CreateGuest(
        [FromRoute] Guid familyUnitId,
        [FromBody] CreateGuestRequest request,
        GuestsService service,
        CancellationToken ct)
    {
        // TODO: Add authorization check (Representative or Admin/Board)

        var guest = await service.CreateAsync(familyUnitId, request, ct);
        return Results.Created(
            $"/api/family-units/{familyUnitId}/guests/{guest.Id}",
            ApiResponse<GuestResponse>.Ok(guest));
    }

    private static async Task<IResult> ListGuests(
        [FromRoute] Guid familyUnitId,
        GuestsService service,
        CancellationToken ct)
    {
        // TODO: Add authorization check

        var guests = await service.GetByFamilyUnitAsync(familyUnitId, ct);
        return Results.Ok(ApiResponse<IReadOnlyList<GuestResponse>>.Ok(guests));
    }

    private static async Task<IResult> GetGuest(
        [FromRoute] Guid familyUnitId,
        [FromRoute] Guid guestId,
        GuestsService service,
        CancellationToken ct)
    {
        // TODO: Add authorization check

        var guest = await service.GetByIdAsync(guestId, ct);
        return Results.Ok(ApiResponse<GuestResponse>.Ok(guest));
    }

    private static async Task<IResult> UpdateGuest(
        [FromRoute] Guid familyUnitId,
        [FromRoute] Guid guestId,
        [FromBody] UpdateGuestRequest request,
        GuestsService service,
        CancellationToken ct)
    {
        // TODO: Add authorization check

        var guest = await service.UpdateAsync(guestId, request, ct);
        return Results.Ok(ApiResponse<GuestResponse>.Ok(guest));
    }

    private static async Task<IResult> DeleteGuest(
        [FromRoute] Guid familyUnitId,
        [FromRoute] Guid guestId,
        GuestsService service,
        CancellationToken ct)
    {
        // TODO: Add authorization check

        await service.DeleteAsync(guestId, ct);
        return Results.NoContent();
    }
}
```

**Endpoint Registration:**

```csharp
// src/Abuvi.API/Program.cs

app.MapGuestsEndpoints();
```

---

## Technical Requirements

### Database Indexes

**Memberships:**

- `memberships.family_member_id` (unique)
- `membership_fees.membership_id`
- `membership_fees.year`
- Composite index on `(membership_id, year)` (unique)

**Guests:**

- `guests.family_unit_id`
- `guests.document_number`

### Encryption

All sensitive fields must use `IEncryptionService`:

- `Guest.MedicalNotes`
- `Guest.Allergies`

Never expose encrypted data in API responses. Use boolean flags (`HasMedicalNotes`, `HasAllergies`) instead.

### Error Messages (Spanish)

**Memberships:**

- "El miembro ya tiene una membresía activa"
- "La membresía no existe"
- "La cuota ya está pagada"
- "La cuota no existe"
- "El año de la cuota debe ser el año actual o futuro"

**Guests:**

- "El invitado no existe"
- "La unidad familiar no existe"
- "No tienes permiso para gestionar invitados de esta familia"

### Authorization

All endpoints require authentication (`RequireAuthorization()`).

Additional authorization checks needed:

- **Representative**: Can manage own family's memberships and guests
- **Admin/Board**: Can manage all memberships and guests

---

## Testing Requirements

### Test Coverage

Minimum 90% coverage for:

- Branches
- Functions
- Lines
- Statements

### Test Types

**Unit Tests:**

- Entity validation
- Service business logic
- Validators
- Repository methods (mocked DbContext)

**Integration Tests:**

- Database operations with in-memory DB
- Full HTTP pipeline with WebApplicationFactory
- End-to-end API scenarios

### Test Naming Convention

Format: `MethodName_StateUnderTest_ExpectedBehavior`

Examples:

- `CreateAsync_WhenValidRequest_CreatesMembership`
- `CreateAsync_WhenFamilyMemberNotFound_ThrowsNotFoundException`
- `PayFeeAsync_WhenFeeAlreadyPaid_ThrowsBusinessRuleException`

### Test Organization

```
src/Abuvi.Tests/
├── Unit/
│   └── Features/
│       ├── Memberships/
│       │   ├── MembershipsServiceTests.cs
│       │   ├── MembershipsRepositoryTests.cs
│       │   ├── CreateMembershipValidatorTests.cs
│       │   └── PayFeeValidatorTests.cs
│       └── Guests/
│           ├── GuestsServiceTests.cs
│           ├── GuestsRepositoryTests.cs
│           ├── CreateGuestValidatorTests.cs
│           └── UpdateGuestValidatorTests.cs
└── Integration/
    └── Features/
        ├── Memberships/
        │   ├── MembershipsEndpointsTests.cs
        │   └── MembershipDatabaseTests.cs
        └── Guests/
            ├── GuestsEndpointsTests.cs
            └── GuestDatabaseTests.cs
```

---

## Documentation Requirements

### Code Documentation

All public types and methods must have XML documentation comments:

```csharp
/// <summary>
/// Creates a new membership for a family member
/// </summary>
/// <param name="familyMemberId">ID of the family member</param>
/// <param name="request">Membership creation request</param>
/// <param name="ct">Cancellation token</param>
/// <returns>Created membership</returns>
/// <exception cref="NotFoundException">When family member not found</exception>
/// <exception cref="BusinessRuleException">When member already has active membership</exception>
public async Task<MembershipResponse> CreateAsync(
    Guid familyMemberId,
    CreateMembershipRequest request,
    CancellationToken ct)
```

### API Documentation

Use OpenAPI attributes for endpoint documentation:

```csharp
group.MapPost("/", CreateMembership)
    .WithName("CreateMembership")
    .WithSummary("Create a new membership for a family member")
    .WithDescription("Activates membership status for a family member, allowing them to register for camps")
    .Produces<ApiResponse<MembershipResponse>>(StatusCodes.Status201Created)
    .Produces(StatusCodes.Status400BadRequest)
    .Produces(StatusCodes.Status404NotFound)
    .Produces(StatusCodes.Status409Conflict);
```

### Update Project README

Add sections to `README.md`:

- Membership system overview
- Guest system overview
- API endpoint documentation
- Business rules

---

## Implementation Checklist

### Phase 1: Membership System

- [ ] Epic 1.1: Membership Entities and Database
  - [ ] Story 1.1.1: Create Membership Entity and Configuration
  - [ ] Story 1.1.2: Create Membership Repository
- [ ] Epic 1.2: Membership Management API
  - [ ] Story 1.2.1: Create Membership Service with Business Logic
  - [ ] Story 1.2.2: Create Membership Validators
  - [ ] Story 1.2.3: Create Membership API Endpoints
- [ ] Epic 1.3: Membership Fee Management
  - [ ] Story 1.3.1: Create Fee Management Service
  - [ ] Story 1.3.2: Create Fee Management Endpoints
- [ ] Epic 1.4: Automated Fee Generation
  - [ ] Story 1.4.1: Create Annual Fee Generation Background Service

### Phase 2: Guests System

- [ ] Epic 2.1: Guest Entities and Database
  - [ ] Story 2.1.1: Create Guest Entity and Configuration
  - [ ] Story 2.1.2: Create Guests Repository
- [ ] Epic 2.2: Guest Management API
  - [ ] Story 2.2.1: Create Guests Service
  - [ ] Story 2.2.2: Create Guest Validators
  - [ ] Story 2.2.3: Create Guest API Endpoints

---

## Success Criteria

**Phase 1 Complete When:**

- ✅ All membership entities created and migrated
- ✅ Membership CRUD API endpoints functional
- ✅ Fee management API endpoints functional
- ✅ Annual fee generation service running
- ✅ All tests passing with 90%+ coverage
- ✅ API documentation complete

**Phase 2 Complete When:**

- ✅ Guest entity created and migrated
- ✅ Guest CRUD API endpoints functional
- ✅ Encryption working for sensitive fields
- ✅ All tests passing with 90%+ coverage
- ✅ API documentation complete

---

**Document Version:** 1.0
**Created:** 2026-02-16
**Status:** Ready for Implementation
**Estimated Duration:**

- Phase 1: 3-4 days
- Phase 2: 2-3 days
- Total: 5-7 days
