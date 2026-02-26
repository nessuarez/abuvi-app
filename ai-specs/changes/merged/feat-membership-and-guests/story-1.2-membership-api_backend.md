# Backend Implementation Plan: Epic 1.2 - Membership Management API

**Epic ID:** 1.2
**Stories:** 1.2.1, 1.2.2, 1.2.3
**Type:** Backend Implementation
**Estimated Effort:** 4-6 hours
**Dependencies:** Story 1.1.1 (Membership Entity), Story 1.1.2 (Membership Repository)

---

## Epic Description

**As a** developer
**I want** to create the complete Membership Management API layer
**So that** users can manage memberships via HTTP endpoints with proper business logic and validation

This epic combines three related stories:
- **Story 1.2.1**: Create Membership Service with Business Logic
- **Story 1.2.2**: Create Membership Validators
- **Story 1.2.3**: Create Membership API Endpoints

---

## Acceptance Criteria

### Story 1.2.1 - Service Layer
1. ✅ Create `MembershipsService` with business logic
2. ✅ Implement CreateMembership with validation
3. ✅ Implement DeactivateMembership
4. ✅ Implement GetMembership
5. ✅ Validate FamilyMember exists before creating membership
6. ✅ Validate FamilyMember doesn't already have active membership

### Story 1.2.2 - Validators
1. ✅ Create `CreateMembershipValidator`
2. ✅ Validation messages in Spanish
3. ✅ Validate StartDate is not in the future

### Story 1.2.3 - Endpoints
1. ✅ POST `/api/family-units/{familyUnitId}/members/{memberId}/membership` - Create
2. ✅ GET `/api/family-units/{familyUnitId}/members/{memberId}/membership` - Get
3. ✅ DELETE `/api/family-units/{familyUnitId}/members/{memberId}/membership` - Deactivate
4. ✅ All endpoints have OpenAPI documentation
5. ✅ Validation filter applied
6. ✅ Integration tests for all endpoints

---

## Implementation Steps

### Step 0: Create Feature Branch

**Branch Name:** `feature/story-1.2-membership-api-backend`

**Bash Command:**
```bash
git checkout feat-family-units
git pull origin feat-family-units
git checkout -b feature/story-1.2-membership-api-backend
```

**Expected Result:** New branch created from latest feat-family-units

---

### Step 1: Add DTOs to MembershipsModels.cs

**File:** `src/Abuvi.API/Features/Memberships/MembershipsModels.cs`

**Action:** Append to existing file

**Code to Add:**
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

**Implementation Notes:**
- Add DTOs after the entity definitions
- Use record types for immutability
- Response DTOs match entity structure

---

### Step 2: Create MembershipsService

**File:** `src/Abuvi.API/Features/Memberships/MembershipsService.cs`

**Action:** Create new file

**Code:**
```csharp
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

**Implementation Notes:**
- Service handles business logic validation
- Throws NotFoundException for missing entities
- Throws BusinessRuleException for business rule violations
- Uses extension methods for entity-to-DTO mapping
- Primary constructor pattern for DI

---

### Step 3: Create CreateMembershipValidator

**File:** `src/Abuvi.API/Features/Memberships/CreateMembershipValidator.cs`

**Action:** Create new file

**Code:**
```csharp
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

**Implementation Notes:**
- Validates StartDate is required and not in future
- Error messages in Spanish per project standards
- Auto-registered via AddValidatorsFromAssemblyContaining<Program>()

---

### Step 4: Create MembershipsEndpoints

**File:** `src/Abuvi.API/Features/Memberships/MembershipsEndpoints.cs`

**Action:** Create new file

**Code:**
```csharp
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

**Implementation Notes:**
- RESTful endpoints following project patterns
- Validation filter applied to POST endpoint
- Proper HTTP status codes
- OpenAPI documentation via .Produces()
- Uses ApiResponse wrapper for consistency
- TODO markers for future authorization implementation

---

### Step 5: Register Service and Endpoints in Program.cs

**File:** `src/Abuvi.API/Program.cs`

**Action:** Modify existing file

**Service Registration (add after IMembershipsRepository):**
```csharp
// Memberships
builder.Services.AddScoped<IMembershipsRepository, MembershipsRepository>();
builder.Services.AddScoped<MembershipsService>();
```

**Endpoint Registration (add after app.MapFamilyUnitsEndpoints()):**
```csharp
app.MapMembershipsEndpoints();
```

**Implementation Notes:**
- Service registered with scoped lifetime
- Endpoints mapped after other feature endpoints

---

### Step 6: Create Service Unit Tests

**File:** `src/Abuvi.Tests/Unit/Features/Memberships/MembershipsServiceTests.cs`

**Action:** Create new file

**Code:**
```csharp
using Abuvi.API.Common.Exceptions;
using Abuvi.API.Features.FamilyUnits;
using Abuvi.API.Features.Memberships;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace Abuvi.Tests.Unit.Features.Memberships;

public class MembershipsServiceTests
{
    private readonly IMembershipsRepository _membershipsRepository;
    private readonly IFamilyUnitsRepository _familyUnitsRepository;
    private readonly MembershipsService _service;

    public MembershipsServiceTests()
    {
        _membershipsRepository = Substitute.For<IMembershipsRepository>();
        _familyUnitsRepository = Substitute.For<IFamilyUnitsRepository>();
        _service = new MembershipsService(_membershipsRepository, _familyUnitsRepository);
    }

    [Fact]
    public async Task CreateAsync_WhenFamilyMemberExists_CreatesMembership()
    {
        // Arrange
        var familyMemberId = Guid.NewGuid();
        var familyMember = CreateTestFamilyMember(familyMemberId);
        var request = new CreateMembershipRequest(DateTime.UtcNow.AddDays(-1));

        _familyUnitsRepository.GetFamilyMemberByIdAsync(familyMemberId, Arg.Any<CancellationToken>())
            .Returns(familyMember);
        _membershipsRepository.GetByFamilyMemberIdAsync(familyMemberId, Arg.Any<CancellationToken>())
            .Returns((Membership?)null);

        // Act
        var result = await _service.CreateAsync(familyMemberId, request, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.FamilyMemberId.Should().Be(familyMemberId);
        result.IsActive.Should().BeTrue();
        result.StartDate.Should().BeCloseTo(request.StartDate, TimeSpan.FromSeconds(1));

        await _membershipsRepository.Received(1).AddAsync(
            Arg.Is<Membership>(m => m.FamilyMemberId == familyMemberId && m.IsActive),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateAsync_WhenFamilyMemberDoesNotExist_ThrowsNotFoundException()
    {
        // Arrange
        var familyMemberId = Guid.NewGuid();
        var request = new CreateMembershipRequest(DateTime.UtcNow);

        _familyUnitsRepository.GetFamilyMemberByIdAsync(familyMemberId, Arg.Any<CancellationToken>())
            .Returns((FamilyMember?)null);

        // Act & Assert
        await Assert.ThrowsAsync<NotFoundException>(
            () => _service.CreateAsync(familyMemberId, request, CancellationToken.None));

        await _membershipsRepository.DidNotReceive().AddAsync(
            Arg.Any<Membership>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateAsync_WhenActiveMembershipExists_ThrowsBusinessRuleException()
    {
        // Arrange
        var familyMemberId = Guid.NewGuid();
        var familyMember = CreateTestFamilyMember(familyMemberId);
        var existingMembership = CreateTestMembership(familyMemberId);
        var request = new CreateMembershipRequest(DateTime.UtcNow);

        _familyUnitsRepository.GetFamilyMemberByIdAsync(familyMemberId, Arg.Any<CancellationToken>())
            .Returns(familyMember);
        _membershipsRepository.GetByFamilyMemberIdAsync(familyMemberId, Arg.Any<CancellationToken>())
            .Returns(existingMembership);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<BusinessRuleException>(
            () => _service.CreateAsync(familyMemberId, request, CancellationToken.None));

        exception.Message.Should().Contain("ya tiene una membresía activa");

        await _membershipsRepository.DidNotReceive().AddAsync(
            Arg.Any<Membership>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetByFamilyMemberIdAsync_WhenMembershipExists_ReturnsMembership()
    {
        // Arrange
        var familyMemberId = Guid.NewGuid();
        var membership = CreateTestMembership(familyMemberId);

        _membershipsRepository.GetByFamilyMemberIdAsync(familyMemberId, Arg.Any<CancellationToken>())
            .Returns(membership);

        // Act
        var result = await _service.GetByFamilyMemberIdAsync(familyMemberId, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.FamilyMemberId.Should().Be(familyMemberId);
        result.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task GetByFamilyMemberIdAsync_WhenMembershipDoesNotExist_ThrowsNotFoundException()
    {
        // Arrange
        var familyMemberId = Guid.NewGuid();

        _membershipsRepository.GetByFamilyMemberIdAsync(familyMemberId, Arg.Any<CancellationToken>())
            .Returns((Membership?)null);

        // Act & Assert
        await Assert.ThrowsAsync<NotFoundException>(
            () => _service.GetByFamilyMemberIdAsync(familyMemberId, CancellationToken.None));
    }

    [Fact]
    public async Task DeactivateAsync_WhenMembershipExists_DeactivatesMembership()
    {
        // Arrange
        var familyMemberId = Guid.NewGuid();
        var membership = CreateTestMembership(familyMemberId);

        _membershipsRepository.GetByFamilyMemberIdAsync(familyMemberId, Arg.Any<CancellationToken>())
            .Returns(membership);

        // Act
        await _service.DeactivateAsync(familyMemberId, CancellationToken.None);

        // Assert
        await _membershipsRepository.Received(1).UpdateAsync(
            Arg.Is<Membership>(m =>
                m.FamilyMemberId == familyMemberId &&
                !m.IsActive &&
                m.EndDate != null),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DeactivateAsync_WhenMembershipDoesNotExist_ThrowsNotFoundException()
    {
        // Arrange
        var familyMemberId = Guid.NewGuid();

        _membershipsRepository.GetByFamilyMemberIdAsync(familyMemberId, Arg.Any<CancellationToken>())
            .Returns((Membership?)null);

        // Act & Assert
        await Assert.ThrowsAsync<NotFoundException>(
            () => _service.DeactivateAsync(familyMemberId, CancellationToken.None));

        await _membershipsRepository.DidNotReceive().UpdateAsync(
            Arg.Any<Membership>(),
            Arg.Any<CancellationToken>());
    }

    // Helper methods
    private static FamilyMember CreateTestFamilyMember(Guid id) => new()
    {
        Id = id,
        FamilyUnitId = Guid.NewGuid(),
        FirstName = "John",
        LastName = "Doe",
        DateOfBirth = new DateOnly(1990, 1, 1),
        Relationship = FamilyRelationship.Parent,
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow
    };

    private static Membership CreateTestMembership(Guid familyMemberId) => new()
    {
        Id = Guid.NewGuid(),
        FamilyMemberId = familyMemberId,
        StartDate = DateTime.UtcNow.AddDays(-30),
        IsActive = true,
        Fees = new List<MembershipFee>(),
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow
    };
}
```

**Implementation Notes:**
- Uses NSubstitute for mocking repositories
- Tests all service methods with positive and negative scenarios
- Verifies repository interactions
- 7 comprehensive unit tests

---

### Step 7: Create Validator Unit Tests

**File:** `src/Abuvi.Tests/Unit/Features/Memberships/CreateMembershipValidatorTests.cs`

**Action:** Create new file

**Code:**
```csharp
using Abuvi.API.Features.Memberships;
using FluentAssertions;
using Xunit;

namespace Abuvi.Tests.Unit.Features.Memberships;

public class CreateMembershipValidatorTests
{
    private readonly CreateMembershipValidator _validator;

    public CreateMembershipValidatorTests()
    {
        _validator = new CreateMembershipValidator();
    }

    [Fact]
    public void Validate_WhenStartDateIsValid_PassesValidation()
    {
        // Arrange
        var request = new CreateMembershipRequest(DateTime.UtcNow.AddDays(-1));

        // Act
        var result = _validator.Validate(request);

        // Assert
        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public void Validate_WhenStartDateIsDefault_FailsValidation()
    {
        // Arrange
        var request = new CreateMembershipRequest(default);

        // Act
        var result = _validator.Validate(request);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(CreateMembershipRequest.StartDate));
        result.Errors.Should().Contain(e => e.ErrorMessage.Contains("obligatoria"));
    }

    [Fact]
    public void Validate_WhenStartDateIsInFuture_FailsValidation()
    {
        // Arrange
        var request = new CreateMembershipRequest(DateTime.UtcNow.AddDays(1));

        // Act
        var result = _validator.Validate(request);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(CreateMembershipRequest.StartDate));
        result.Errors.Should().Contain(e => e.ErrorMessage.Contains("futura"));
    }

    [Fact]
    public void Validate_WhenStartDateIsToday_PassesValidation()
    {
        // Arrange
        var request = new CreateMembershipRequest(DateTime.UtcNow);

        // Act
        var result = _validator.Validate(request);

        // Assert
        result.IsValid.Should().BeTrue();
    }
}
```

**Implementation Notes:**
- 4 validation tests covering all scenarios
- Tests Spanish error messages
- Tests boundary conditions

---

### Step 8: Create Integration Tests for Endpoints

**File:** `src/Abuvi.Tests/Integration/Features/Memberships/MembershipsEndpointsTests.cs`

**Action:** Create new file

**Code:**
```csharp
using System.Net;
using System.Net.Http.Json;
using Abuvi.API.Common.Models;
using Abuvi.API.Features.FamilyUnits;
using Abuvi.API.Features.Memberships;
using Abuvi.API.Features.Users;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Abuvi.Tests.Integration.Features.Memberships;

public class MembershipsEndpointsTests : IntegrationTestBase
{
    public MembershipsEndpointsTests(IntegrationTestFactory factory) : base(factory)
    {
    }

    [Fact]
    public async Task CreateMembership_WithValidData_Returns201Created()
    {
        // Arrange
        var (user, familyUnit, familyMember) = await SeedTestDataAsync();
        var request = new CreateMembershipRequest(DateTime.UtcNow.AddDays(-1));

        // Act
        var response = await AuthenticatedClient.PostAsJsonAsync(
            $"/api/family-units/{familyUnit.Id}/members/{familyMember.Id}/membership",
            request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var result = await response.Content.ReadFromJsonAsync<ApiResponse<MembershipResponse>>();
        result.Should().NotBeNull();
        result!.Data.Should().NotBeNull();
        result.Data!.FamilyMemberId.Should().Be(familyMember.Id);
        result.Data.IsActive.Should().BeTrue();
        result.Data.StartDate.Should().BeCloseTo(request.StartDate, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task CreateMembership_WithNonExistentFamilyMember_Returns404NotFound()
    {
        // Arrange
        var familyUnitId = Guid.NewGuid();
        var nonExistentMemberId = Guid.NewGuid();
        var request = new CreateMembershipRequest(DateTime.UtcNow);

        // Act
        var response = await AuthenticatedClient.PostAsJsonAsync(
            $"/api/family-units/{familyUnitId}/members/{nonExistentMemberId}/membership",
            request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task CreateMembership_WhenActiveMembershipExists_Returns409Conflict()
    {
        // Arrange
        var (user, familyUnit, familyMember) = await SeedTestDataAsync();
        var membership = await CreateTestMembershipAsync(familyMember.Id);
        var request = new CreateMembershipRequest(DateTime.UtcNow);

        // Act
        var response = await AuthenticatedClient.PostAsJsonAsync(
            $"/api/family-units/{familyUnit.Id}/members/{familyMember.Id}/membership",
            request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task CreateMembership_WithFutureStartDate_Returns400BadRequest()
    {
        // Arrange
        var (user, familyUnit, familyMember) = await SeedTestDataAsync();
        var request = new CreateMembershipRequest(DateTime.UtcNow.AddDays(1));

        // Act
        var response = await AuthenticatedClient.PostAsJsonAsync(
            $"/api/family-units/{familyUnit.Id}/members/{familyMember.Id}/membership",
            request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GetMembership_WhenExists_Returns200Ok()
    {
        // Arrange
        var (user, familyUnit, familyMember) = await SeedTestDataAsync();
        var membership = await CreateTestMembershipAsync(familyMember.Id);

        // Act
        var response = await AuthenticatedClient.GetAsync(
            $"/api/family-units/{familyUnit.Id}/members/{familyMember.Id}/membership");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<ApiResponse<MembershipResponse>>();
        result.Should().NotBeNull();
        result!.Data.Should().NotBeNull();
        result.Data!.Id.Should().Be(membership.Id);
        result.Data.FamilyMemberId.Should().Be(familyMember.Id);
    }

    [Fact]
    public async Task GetMembership_WhenNotExists_Returns404NotFound()
    {
        // Arrange
        var (user, familyUnit, familyMember) = await SeedTestDataAsync();

        // Act
        var response = await AuthenticatedClient.GetAsync(
            $"/api/family-units/{familyUnit.Id}/members/{familyMember.Id}/membership");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task DeactivateMembership_WhenExists_Returns204NoContent()
    {
        // Arrange
        var (user, familyUnit, familyMember) = await SeedTestDataAsync();
        var membership = await CreateTestMembershipAsync(familyMember.Id);

        // Act
        var response = await AuthenticatedClient.DeleteAsync(
            $"/api/family-units/{familyUnit.Id}/members/{familyMember.Id}/membership");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Verify membership was deactivated
        using var scope = Factory.Services.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IMembershipsRepository>();
        var updated = await repository.GetByFamilyMemberIdAsync(familyMember.Id, CancellationToken.None);
        updated.Should().BeNull(); // GetByFamilyMemberIdAsync only returns active memberships
    }

    [Fact]
    public async Task DeactivateMembership_WhenNotExists_Returns404NotFound()
    {
        // Arrange
        var (user, familyUnit, familyMember) = await SeedTestDataAsync();

        // Act
        var response = await AuthenticatedClient.DeleteAsync(
            $"/api/family-units/{familyUnit.Id}/members/{familyMember.Id}/membership");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // Helper methods
    private async Task<(User user, FamilyUnit familyUnit, FamilyMember familyMember)> SeedTestDataAsync()
    {
        using var scope = Factory.Services.CreateScope();
        var dbContext = GetDbContext(scope);

        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = $"test{Guid.NewGuid()}@example.com",
            FirstName = "Test",
            LastName = "User",
            PasswordHash = "hashedpassword",
            Role = UserRole.Member,
            EmailVerified = true,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        var familyUnit = new FamilyUnit
        {
            Id = Guid.NewGuid(),
            Name = "Test Family",
            RepresentativeUserId = user.Id,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        var familyMember = new FamilyMember
        {
            Id = Guid.NewGuid(),
            FamilyUnitId = familyUnit.Id,
            FirstName = "John",
            LastName = "Doe",
            DateOfBirth = new DateOnly(1990, 1, 1),
            Relationship = FamilyRelationship.Parent,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        dbContext.Users.Add(user);
        dbContext.FamilyUnits.Add(familyUnit);
        dbContext.FamilyMembers.Add(familyMember);
        await dbContext.SaveChangesAsync();

        return (user, familyUnit, familyMember);
    }

    private async Task<Membership> CreateTestMembershipAsync(Guid familyMemberId)
    {
        using var scope = Factory.Services.CreateScope();
        var dbContext = GetDbContext(scope);

        var membership = new Membership
        {
            Id = Guid.NewGuid(),
            FamilyMemberId = familyMemberId,
            StartDate = DateTime.UtcNow.AddDays(-30),
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        dbContext.Memberships.Add(membership);
        await dbContext.SaveChangesAsync();

        return membership;
    }
}
```

**Implementation Notes:**
- 8 integration tests covering all endpoints
- Tests positive and negative scenarios
- Uses IntegrationTestBase for authenticated requests
- Verifies database state after operations
- Tests validation at HTTP layer

---

### Step 9: Run All Tests

**Bash Commands:**
```bash
# Run unit tests for service
dotnet test --filter "FullyQualifiedName~MembershipsServiceTests" --logger "console;verbosity=detailed"

# Run unit tests for validator
dotnet test --filter "FullyQualifiedName~CreateMembershipValidatorTests" --logger "console;verbosity=detailed"

# Run integration tests for endpoints
dotnet test --filter "FullyQualifiedName~MembershipsEndpointsTests" --logger "console;verbosity=detailed"

# Run all Memberships tests
dotnet test --filter "FullyQualifiedName~Memberships" --logger "console;verbosity=detailed"
```

**Expected Result:** All tests should pass (26 existing + 19 new = 45 total)

---

### Step 10: Commit Changes

**Bash Command:**
```bash
git add src/Abuvi.API/Features/Memberships/MembershipsModels.cs \
        src/Abuvi.API/Features/Memberships/MembershipsService.cs \
        src/Abuvi.API/Features/Memberships/CreateMembershipValidator.cs \
        src/Abuvi.API/Features/Memberships/MembershipsEndpoints.cs \
        src/Abuvi.API/Program.cs \
        src/Abuvi.Tests/Unit/Features/Memberships/MembershipsServiceTests.cs \
        src/Abuvi.Tests/Unit/Features/Memberships/CreateMembershipValidatorTests.cs \
        src/Abuvi.Tests/Integration/Features/Memberships/MembershipsEndpointsTests.cs

git commit -m "$(cat <<'EOF'
feat(membership): Add Membership Management API with service layer and endpoints

Epic 1.2 - Membership Management API (Stories 1.2.1, 1.2.2, 1.2.3)

Service Layer (Story 1.2.1):
- Create MembershipsService with business logic
- Implement CreateMembership with FamilyMember validation
- Implement GetMembership and DeactivateMembership
- Add extension methods for entity-to-DTO mapping
- Add DTOs (CreateMembershipRequest, MembershipResponse, MembershipFeeResponse)

Validation (Story 1.2.2):
- Create CreateMembershipValidator with FluentValidation
- Validate StartDate is required and not in future
- Error messages in Spanish

Endpoints (Story 1.2.3):
- POST /api/family-units/{id}/members/{id}/membership - Create membership
- GET /api/family-units/{id}/members/{id}/membership - Get membership
- DELETE /api/family-units/{id}/members/{id}/membership - Deactivate membership
- Add OpenAPI documentation and validation filters
- Register service and endpoints in DI

Testing:
- Add 7 service unit tests (business logic validation)
- Add 4 validator unit tests (validation rules)
- Add 8 endpoint integration tests (HTTP layer)
- All 19 new tests passing

Implements Epic 1.2 from Phase 1 Membership System

Co-Authored-By: Claude Sonnet 4.5 <noreply@anthropic.com>
EOF
)"
```

**Expected Result:** Commit created with comprehensive changes

---

### Step 11: Push and Create Pull Request

**Bash Commands:**
```bash
# Push branch
git push -u origin feature/story-1.2-membership-api-backend

# Create PR
gh pr create --base feat-family-units \
  --title "feat(membership): Epic 1.2 - Membership Management API" \
  --body "$(cat <<'EOF'
## Summary
Implements Epic 1.2 (Stories 1.2.1, 1.2.2, 1.2.3) - Complete Membership Management API layer including service, validation, and HTTP endpoints.

### Changes

**Story 1.2.1 - Service Layer:**
- ✅ Create MembershipsService with business logic
- ✅ Implement CreateMembership with FamilyMember validation
- ✅ Implement GetMembership and DeactivateMembership operations
- ✅ Add entity-to-DTO mapping extensions
- ✅ Add DTOs (CreateMembershipRequest, MembershipResponse, MembershipFeeResponse)
- ✅ Business rules: validate FamilyMember exists, prevent duplicate active memberships

**Story 1.2.2 - Validation:**
- ✅ Create CreateMembershipValidator with FluentValidation
- ✅ Validate StartDate is required and not in future
- ✅ Error messages in Spanish per project standards

**Story 1.2.3 - HTTP Endpoints:**
- ✅ POST `/api/family-units/{id}/members/{id}/membership` - Create membership (201 Created)
- ✅ GET `/api/family-units/{id}/members/{id}/membership` - Get membership (200 OK)
- ✅ DELETE `/api/family-units/{id}/members/{id}/membership` - Deactivate membership (204 No Content)
- ✅ OpenAPI documentation with proper status codes
- ✅ Validation filters applied
- ✅ Authentication required for all endpoints

### Architecture
- **Service Layer**: Business logic and validation
- **Validators**: FluentValidation for request validation
- **Endpoints**: Minimal API with proper HTTP semantics
- **DTOs**: Clean separation between domain and API contracts

### Test Coverage
All 19 tests passing (7 service + 4 validator + 8 integration):
- **Service Tests**: Verify business logic, validation, and exception handling
- **Validator Tests**: Verify validation rules and Spanish error messages
- **Integration Tests**: Verify full HTTP flow with authenticated requests

### Dependencies
- ✅ Story 1.1.1 (Membership Entity)
- ✅ Story 1.1.2 (Membership Repository)

### Test plan
- [x] Run service unit tests: `dotnet test --filter "FullyQualifiedName~MembershipsServiceTests"`
- [x] Run validator unit tests: `dotnet test --filter "FullyQualifiedName~CreateMembershipValidatorTests"`
- [x] Run integration tests: `dotnet test --filter "FullyQualifiedName~MembershipsEndpointsTests"`
- [x] Verify all 45 Memberships tests pass
- [x] Code review for business logic correctness
- [ ] Manual QA: Test endpoints with Swagger UI
- [ ] Next: Story 1.3.1 (Fee Management Service)

🤖 Generated with [Claude Code](https://claude.com/claude-code)
EOF
)"
```

**Expected Result:** Pull request created successfully

---

## Testing Strategy

### Service Unit Tests (7 tests)
1. CreateAsync with valid FamilyMember succeeds
2. CreateAsync with non-existent FamilyMember throws NotFoundException
3. CreateAsync with existing active membership throws BusinessRuleException
4. GetByFamilyMemberIdAsync when exists returns membership
5. GetByFamilyMemberIdAsync when not exists throws NotFoundException
6. DeactivateAsync when exists sets IsActive=false and EndDate
7. DeactivateAsync when not exists throws NotFoundException

### Validator Unit Tests (4 tests)
1. Valid StartDate passes validation
2. Default StartDate fails validation
3. Future StartDate fails validation
4. Today StartDate passes validation

### Integration Tests (8 tests)
1. POST with valid data returns 201 Created
2. POST with non-existent FamilyMember returns 404 Not Found
3. POST with existing active membership returns 409 Conflict
4. POST with future StartDate returns 400 Bad Request
5. GET when membership exists returns 200 OK
6. GET when membership not exists returns 404 Not Found
7. DELETE when membership exists returns 204 No Content
8. DELETE when membership not exists returns 404 Not Found

---

## Success Criteria

✅ Service layer implements all business logic
✅ Validators enforce data integrity
✅ Endpoints follow RESTful conventions
✅ All 19 new tests passing (45 total with previous stories)
✅ OpenAPI documentation complete
✅ Authentication required
✅ Spanish error messages
✅ Code follows Vertical Slice Architecture
✅ Commit message follows project standards
✅ PR created and linked to epic
