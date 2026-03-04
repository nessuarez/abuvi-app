# Camp CRUD - Backend Implementation Plan (TDD)

## Overview

This document provides a Test-Driven Development (TDD) implementation plan for the Camp CRUD feature based on [camp-crud-enriched.md](./camp-crud-enriched.md).

**Key Principles**:

- ✅ **TDD First**: Write tests BEFORE implementation code
- ✅ **Red-Green-Refactor**: Follow strict TDD cycle
- ✅ **Vertical Slice Architecture**: Each feature is self-contained
- ✅ **Incremental Delivery**: Each phase is independently deployable
- ✅ **90% Test Coverage**: Target for all business logic

---

## Implementation Phases

### Phase 0: Foundation - Database Schema & Migrations

**Goal**: Set up database tables and EF Core entities

**Dependencies**: None

**Duration**: 1-2 days

#### Tasks

1. **Create database migration for new schema**
   - Add `association_settings` table
   - Add pricing columns to `camps` table (base_price_adult, base_price_child, base_price_baby)
   - Add age range columns to `camp_editions` table
   - Add `camp_edition_extras` table
   - Add `registration_extras` table
   - Add status "Proposed" to camp_editions enum
   - Add indexes for performance

2. **Create EF Core entity configurations**
   - `AssociationSettingsConfiguration.cs`
   - Update `CampConfiguration.cs` (add pricing fields)
   - Update `CampEditionConfiguration.cs` (add age ranges, proposal fields)
   - `CampEditionExtraConfiguration.cs` (new)
   - `RegistrationExtraConfiguration.cs` (new)

3. **Update DbContext**
   - Add new DbSets
   - Register configurations

#### Migration Steps

```bash
# Create migration
dotnet ef migrations add AddCampPricingAndExtras --project src/Abuvi.API

# Review generated SQL
dotnet ef migrations script --project src/Abuvi.API

# Apply to dev database
dotnet ef database update --project src/Abuvi.API
```

#### Data Migration Strategy

For existing data:

- Migrate `camps.base_price` → `base_price_adult`
- Calculate `base_price_child = base_price * 0.7` (70% of adult)
- Calculate `base_price_baby = base_price * 0.4` (40% of adult)
- Set default age ranges: Baby 0-2, Child 3-12, Adult 13+

#### Acceptance Criteria

- [ ] All migrations apply without errors
- [ ] No data loss during migration
- [ ] All constraints and indexes created
- [ ] EF Core can query all new entities
- [ ] Rollback migration works correctly

---

### Phase 1: Association Settings Management

**Goal**: Global age range configuration for the association

**Dependencies**: Phase 0

**Duration**: 2-3 days

**User Stories**: US-CAMP-104

#### 1.1 TDD: Get Age Ranges Settings

**Step 1: Write Failing Test (RED)**

`tests/Abuvi.Tests/Unit/Features/AssociationSettings/AssociationSettingsServiceTests.cs`:

```csharp
public class AssociationSettingsServiceTests
{
    [Fact]
    public async Task GetAgeRangesAsync_WhenSettingExists_ReturnsAgeRanges()
    {
        // Arrange
        var repository = Substitute.For<IAssociationSettingsRepository>();
        var expectedSettings = new AssociationSettings
        {
            Id = Guid.NewGuid(),
            SettingKey = "age_ranges",
            SettingValue = JsonDocument.Parse(@"{
                ""babyMaxAge"": 2,
                ""childMinAge"": 3,
                ""childMaxAge"": 12,
                ""adultMinAge"": 13
            }"),
            UpdatedBy = Guid.NewGuid(),
            UpdatedAt = DateTime.UtcNow
        };

        repository.GetByKeyAsync("age_ranges", Arg.Any<CancellationToken>())
            .Returns(expectedSettings);

        var sut = new AssociationSettingsService(repository);

        // Act
        var result = await sut.GetAgeRangesAsync(CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.BabyMaxAge.Should().Be(2);
        result.ChildMinAge.Should().Be(3);
        result.ChildMaxAge.Should().Be(12);
        result.AdultMinAge.Should().Be(13);
    }

    [Fact]
    public async Task GetAgeRangesAsync_WhenSettingNotFound_ReturnsDefaultAgeRanges()
    {
        // Arrange
        var repository = Substitute.For<IAssociationSettingsRepository>();
        repository.GetByKeyAsync("age_ranges", Arg.Any<CancellationToken>())
            .Returns((AssociationSettings?)null);

        var sut = new AssociationSettingsService(repository);

        // Act
        var result = await sut.GetAgeRangesAsync(CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.BabyMaxAge.Should().Be(2);  // Default values
        result.ChildMinAge.Should().Be(3);
        result.ChildMaxAge.Should().Be(12);
        result.AdultMinAge.Should().Be(13);
    }
}
```

**Step 2: Implement Minimum Code (GREEN)**

`src/Abuvi.API/Features/AssociationSettings/AssociationSettingsModels.cs`:

```csharp
public record AgeRangesResponse(
    int BabyMaxAge,
    int ChildMinAge,
    int ChildMaxAge,
    int AdultMinAge
);

public record UpdateAgeRangesRequest(
    int BabyMaxAge,
    int ChildMinAge,
    int ChildMaxAge,
    int AdultMinAge
);
```

`src/Abuvi.API/Features/AssociationSettings/AssociationSettingsService.cs`:

```csharp
public class AssociationSettingsService(IAssociationSettingsRepository repository)
{
    private static readonly AgeRangesResponse DefaultAgeRanges = new(
        BabyMaxAge: 2,
        ChildMinAge: 3,
        ChildMaxAge: 12,
        AdultMinAge: 13
    );

    public async Task<AgeRangesResponse> GetAgeRangesAsync(CancellationToken ct)
    {
        var setting = await repository.GetByKeyAsync("age_ranges", ct);

        if (setting == null)
            return DefaultAgeRanges;

        var json = setting.SettingValue;
        return new AgeRangesResponse(
            BabyMaxAge: json.RootElement.GetProperty("babyMaxAge").GetInt32(),
            ChildMinAge: json.RootElement.GetProperty("childMinAge").GetInt32(),
            ChildMaxAge: json.RootElement.GetProperty("childMaxAge").GetInt32(),
            AdultMinAge: json.RootElement.GetProperty("adultMinAge").GetInt32()
        );
    }
}
```

**Step 3: Refactor**

- Extract JSON parsing to helper method if needed
- Add XML documentation

#### 1.2 TDD: Update Age Ranges Settings

**Step 1: Write Failing Tests (RED)**

```csharp
[Fact]
public async Task UpdateAgeRangesAsync_WithValidRanges_UpdatesSuccessfully()
{
    // Arrange
    var repository = Substitute.For<IAssociationSettingsRepository>();
    var userId = Guid.NewGuid();
    var request = new UpdateAgeRangesRequest(2, 3, 12, 13);

    var sut = new AssociationSettingsService(repository);

    // Act
    var result = await sut.UpdateAgeRangesAsync(request, userId, CancellationToken.None);

    // Assert
    result.Should().NotBeNull();
    result.BabyMaxAge.Should().Be(2);

    await repository.Received(1).UpsertAsync(
        Arg.Is<AssociationSettings>(s =>
            s.SettingKey == "age_ranges" &&
            s.UpdatedBy == userId
        ),
        Arg.Any<CancellationToken>()
    );
}

[Fact]
public async Task UpdateAgeRangesAsync_WithInvalidRanges_ThrowsValidationException()
{
    // Child min age <= Baby max age should fail
    var repository = Substitute.For<IAssociationSettingsRepository>();
    var request = new UpdateAgeRangesRequest(
        BabyMaxAge: 5,
        ChildMinAge: 3,  // Invalid: should be > 5
        ChildMaxAge: 12,
        AdultMinAge: 13
    );

    var sut = new AssociationSettingsService(repository);

    // Act & Assert
    await sut.Invoking(s => s.UpdateAgeRangesAsync(request, Guid.NewGuid(), CancellationToken.None))
        .Should().ThrowAsync<ValidationException>();
}
```

**Step 2: Implement (GREEN)**

Create validator:

```csharp
public class UpdateAgeRangesRequestValidator : AbstractValidator<UpdateAgeRangesRequest>
{
    public UpdateAgeRangesRequestValidator()
    {
        RuleFor(x => x.BabyMaxAge)
            .GreaterThanOrEqualTo(0)
            .WithMessage("Baby maximum age must be 0 or greater");

        RuleFor(x => x.ChildMinAge)
            .GreaterThan(x => x.BabyMaxAge)
            .WithMessage("Child minimum age must be greater than baby maximum age");

        RuleFor(x => x.ChildMaxAge)
            .GreaterThanOrEqualTo(x => x.ChildMinAge)
            .WithMessage("Child maximum age must be greater than or equal to child minimum age");

        RuleFor(x => x.AdultMinAge)
            .GreaterThan(x => x.ChildMaxAge)
            .WithMessage("Adult minimum age must be greater than child maximum age");
    }
}
```

Implement service method:

```csharp
public async Task<AgeRangesResponse> UpdateAgeRangesAsync(
    UpdateAgeRangesRequest request,
    Guid userId,
    CancellationToken ct)
{
    // Validation happens via FluentValidation middleware

    var settingValue = JsonSerializer.SerializeToDocument(new
    {
        babyMaxAge = request.BabyMaxAge,
        childMinAge = request.ChildMinAge,
        childMaxAge = request.ChildMaxAge,
        adultMinAge = request.AdultMinAge
    });

    var setting = new AssociationSettings
    {
        Id = Guid.NewGuid(),
        SettingKey = "age_ranges",
        SettingValue = settingValue,
        Description = "Global age range definitions for camp pricing",
        UpdatedBy = userId,
        UpdatedAt = DateTime.UtcNow
    };

    await repository.UpsertAsync(setting, ct);

    return new AgeRangesResponse(
        request.BabyMaxAge,
        request.ChildMinAge,
        request.ChildMaxAge,
        request.AdultMinAge
    );
}
```

**Step 3: Refactor**

- Extract JSON serialization helper
- Add logging for audit trail

#### 1.3 Create API Endpoints

`src/Abuvi.API/Features/AssociationSettings/AssociationSettingsEndpoints.cs`:

```csharp
public static class AssociationSettingsEndpoints
{
    public static void MapAssociationSettingsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/settings")
            .WithTags("Association Settings")
            .RequireAuthorization();

        group.MapGet("/age-ranges", GetAgeRangesAsync)
            .WithName("GetAgeRanges")
            .Produces<AgeRangesResponse>();

        group.MapPut("/age-ranges", UpdateAgeRangesAsync)
            .WithName("UpdateAgeRanges")
            .Produces<AgeRangesResponse>()
            .RequireAuthorization(policy => policy.RequireRole("Board", "Admin"));
    }

    private static async Task<IResult> GetAgeRangesAsync(
        IAssociationSettingsService service,
        CancellationToken ct)
    {
        var result = await service.GetAgeRangesAsync(ct);
        return Results.Ok(result);
    }

    private static async Task<IResult> UpdateAgeRangesAsync(
        UpdateAgeRangesRequest request,
        IAssociationSettingsService service,
        ClaimsPrincipal user,
        CancellationToken ct)
    {
        var userId = user.GetUserId();
        var result = await service.UpdateAgeRangesAsync(request, userId, ct);
        return Results.Ok(result);
    }
}
```

#### 1.4 Repository Implementation

First write repository tests, then implement.

#### Acceptance Criteria

- [ ] All tests pass (GET and UPDATE)
- [ ] Validation prevents invalid age ranges
- [ ] Only Board/Admin can update settings
- [ ] Settings persist to database
- [ ] API endpoints return correct responses
- [ ] Changes are audited (UpdatedBy, UpdatedAt)

---

### Phase 2: Camp Location Management

**Goal**: CRUD operations for camp locations (inventory)

**Dependencies**: Phase 0

**Duration**: 3-4 days

**User Stories**: US-CAMP-101, US-CAMP-102, US-CAMP-103

#### 2.1 TDD: Create Camp Location

**Step 1: Write Failing Tests (RED)**

`tests/Abuvi.Tests/Unit/Features/Camps/CampsServiceTests.cs`:

```csharp
public class CampsServiceTests
{
    [Fact]
    public async Task CreateAsync_WithValidData_CreatesCampLocation()
    {
        // Arrange
        var repository = Substitute.For<ICampsRepository>();
        var sut = new CampsService(repository);

        var request = new CreateCampRequest(
            Name: "Mountain Camp",
            Description: "Beautiful alpine location",
            Latitude: 46.5833,
            Longitude: 7.9833,
            BasePriceAdult: 180.00m,
            BasePriceChild: 120.00m,
            BasePriceBaby: 60.00m,
            Status: CampStatus.Active
        );

        // Act
        var result = await sut.CreateAsync(request, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Name.Should().Be("Mountain Camp");
        result.BasePriceAdult.Should().Be(180.00m);
        result.BasePriceChild.Should().Be(120.00m);
        result.BasePriceBaby.Should().Be(60.00m);
        result.Latitude.Should().Be(46.5833);
        result.Longitude.Should().Be(7.9833);

        await repository.Received(1).AddAsync(
            Arg.Is<Camp>(c =>
                c.Name == "Mountain Camp" &&
                c.BasePriceAdult == 180.00m &&
                c.BasePriceChild == 120.00m &&
                c.BasePriceBaby == 60.00m &&
                c.Status == CampStatus.Active
            ),
            Arg.Any<CancellationToken>()
        );
    }

    [Theory]
    [InlineData(-91, 0)]  // Invalid latitude
    [InlineData(91, 0)]   // Invalid latitude
    [InlineData(0, -181)] // Invalid longitude
    [InlineData(0, 181)]  // Invalid longitude
    public async Task CreateAsync_WithInvalidCoordinates_ThrowsValidationException(
        double latitude,
        double longitude)
    {
        // Arrange
        var repository = Substitute.For<ICampsRepository>();
        var sut = new CampsService(repository);

        var request = new CreateCampRequest(
            "Test Camp",
            "Description",
            latitude,
            longitude,
            100m, 80m, 40m,
            CampStatus.Active
        );

        // Act & Assert
        await sut.Invoking(s => s.CreateAsync(request, CancellationToken.None))
            .Should().ThrowAsync<ValidationException>();
    }

    [Theory]
    [InlineData(-1, 100, 50)]   // Negative adult price
    [InlineData(100, -1, 50)]   // Negative child price
    [InlineData(100, 80, -1)]   // Negative baby price
    public async Task CreateAsync_WithNegativePrices_ThrowsValidationException(
        decimal adult,
        decimal child,
        decimal baby)
    {
        // Arrange
        var repository = Substitute.For<ICampsRepository>();
        var sut = new CampsService(repository);

        var request = new CreateCampRequest(
            "Test Camp",
            "Description",
            46.5, 7.9,
            adult, child, baby,
            CampStatus.Active
        );

        // Act & Assert
        await sut.Invoking(s => s.CreateAsync(request, CancellationToken.None))
            .Should().ThrowAsync<ValidationException>();
    }
}
```

**Step 2: Implement (GREEN)**

Models:

```csharp
public record CreateCampRequest(
    string Name,
    string Description,
    double Latitude,
    double Longitude,
    decimal BasePriceAdult,
    decimal BasePriceChild,
    decimal BasePriceBaby,
    CampStatus Status
);

public record CampResponse(
    Guid Id,
    string Name,
    string Description,
    double Latitude,
    double Longitude,
    decimal BasePriceAdult,
    decimal BasePriceChild,
    decimal BasePriceBaby,
    CampStatus Status,
    int EditionCount,
    DateTime CreatedAt,
    DateTime UpdatedAt
);
```

Validator:

```csharp
public class CreateCampRequestValidator : AbstractValidator<CreateCampRequest>
{
    public CreateCampRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .MaximumLength(200);

        RuleFor(x => x.Description)
            .MaximumLength(5000);

        RuleFor(x => x.Latitude)
            .InclusiveBetween(-90, 90)
            .WithMessage("Latitude must be between -90 and 90");

        RuleFor(x => x.Longitude)
            .InclusiveBetween(-180, 180)
            .WithMessage("Longitude must be between -180 and 180");

        RuleFor(x => x.BasePriceAdult)
            .GreaterThanOrEqualTo(0)
            .PrecisionScale(10, 2, true);

        RuleFor(x => x.BasePriceChild)
            .GreaterThanOrEqualTo(0)
            .PrecisionScale(10, 2, true);

        RuleFor(x => x.BasePriceBaby)
            .GreaterThanOrEqualTo(0)
            .PrecisionScale(10, 2, true);

        RuleFor(x => x.Status)
            .IsInEnum();
    }
}
```

Service:

```csharp
public class CampsService(ICampsRepository repository)
{
    public async Task<CampResponse> CreateAsync(CreateCampRequest request, CancellationToken ct)
    {
        var camp = new Camp
        {
            Id = Guid.NewGuid(),
            Name = request.Name,
            Description = request.Description,
            Latitude = request.Latitude,
            Longitude = request.Longitude,
            BasePriceAdult = request.BasePriceAdult,
            BasePriceChild = request.BasePriceChild,
            BasePriceBaby = request.BasePriceBaby,
            Status = request.Status,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        await repository.AddAsync(camp, ct);

        return camp.ToResponse();
    }
}
```

**Step 3: Refactor**

- Extract mapping logic to extension methods
- Add logging

#### 2.2 TDD: List Camp Locations (with pagination)

Write tests for:

- GetAllAsync with pagination
- Filtering by status
- Searching by name
- Sorting

Then implement.

#### 2.3 TDD: Update Camp Location

Write tests for:

- Successful update
- Validation errors
- Cannot change to Inactive if active editions exist

Then implement.

#### 2.4 TDD: Delete Camp Location

Write tests for:

- Successful deletion (no editions)
- Prevent deletion if editions exist
- Soft delete vs hard delete

Then implement.

#### 2.5 Create API Endpoints

Similar to Phase 1, create REST endpoints for all CRUD operations.

#### Acceptance Criteria

- [ ] All CRUD operations tested and working
- [ ] Validation prevents invalid data
- [ ] Pagination works correctly
- [ ] Cannot delete camps with editions
- [ ] API endpoints secured (Board/Admin only)
- [ ] Map coordinates validated properly

---

### Phase 3: Camp Proposal/Candidate Workflow

**Goal**: Propose, compare, and select camp candidates

**Dependencies**: Phase 0, Phase 1, Phase 2

**Duration**: 3-4 days

**User Stories**: US-CAMP-000, US-CAMP-001, US-CAMP-002, US-CAMP-003

#### 3.1 TDD: Propose Camp Candidate

**Step 1: Write Failing Tests (RED)**

```csharp
public class CampEditionsServiceTests
{
    [Fact]
    public async Task ProposeAsync_WithValidData_CreatesProposedEdition()
    {
        // Arrange
        var repository = Substitute.For<ICampEditionsRepository>();
        var campsRepo = Substitute.For<ICampsRepository>();

        var campId = Guid.NewGuid();
        var existingCamp = new Camp
        {
            Id = campId,
            Name = "Mountain Camp",
            BasePriceAdult = 180m,
            BasePriceChild = 120m,
            BasePriceBaby = 60m
        };

        campsRepo.GetByIdAsync(campId, Arg.Any<CancellationToken>())
            .Returns(existingCamp);

        var sut = new CampEditionsService(repository, campsRepo);

        var request = new ProposeCampEditionRequest(
            CampId: campId,
            Year: 2026,
            StartDate: new DateOnly(2026, 7, 10),
            EndDate: new DateOnly(2026, 7, 20),
            Location: "Grindelwald, Swiss Alps",
            Description: "Mountain camp with hiking",
            PricePerAdult: 180m,
            PricePerChild: 120m,
            PricePerBaby: 60m,
            MaxCapacity: 80,
            ProposalReason: "Excellent facilities",
            ProposalNotes: "Pros: Great location. Cons: Limited capacity"
        );

        // Act
        var result = await sut.ProposeAsync(request, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Status.Should().Be(CampEditionStatus.Proposed);
        result.Year.Should().Be(2026);
        result.ProposalReason.Should().Be("Excellent facilities");

        await repository.Received(1).AddAsync(
            Arg.Is<CampEdition>(e =>
                e.CampId == campId &&
                e.Status == CampEditionStatus.Proposed &&
                e.Year == 2026
            ),
            Arg.Any<CancellationToken>()
        );
    }

    [Fact]
    public async Task ProposeAsync_WithPastYear_ThrowsValidationException()
    {
        // Test that year must be current or future
    }

    [Fact]
    public async Task ProposeAsync_WithEndDateBeforeStartDate_ThrowsValidationException()
    {
        // Test date validation
    }
}
```

**Step 2: Implement (GREEN)**

Create validator, service method, and endpoint.

#### 3.2 TDD: List Proposed Candidates

Write tests for:

- GetProposedByYearAsync returns only Proposed status
- Filters by year correctly
- Includes camp location details
- Includes proposal notes

Then implement.

#### 3.3 TDD: Promote Candidate to Draft

Write tests for:

- Successful promotion from Proposed → Draft
- Only Proposed can be promoted
- Authorization check (Board/Admin only)
- Optionally reject other proposals for same year

Then implement.

#### 3.4 TDD: Reject Candidate

Write tests for:

- Successful rejection (soft delete or archive)
- Only Proposed can be rejected
- Rejection reason stored

Then implement.

#### Acceptance Criteria

- [ ] Can propose multiple candidates for same year
- [ ] Proposed editions not visible to regular members
- [ ] Can promote one candidate to Draft
- [ ] Can reject candidates with reason
- [ ] Board members can compare proposals side-by-side
- [ ] Audit trail for all status changes

---

### Phase 4: Camp Edition Management

**Goal**: Full CRUD and status management for camp editions

**Dependencies**: Phase 0, Phase 1, Phase 2, Phase 3

**Duration**: 4-5 days

**User Stories**: US-CAMP-201, US-CAMP-202, US-CAMP-204

#### 4.1 TDD: Create Camp Edition (from Draft)

Similar to Propose, but creates Draft edition directly (not Proposed).

Write tests first, then implement.

#### 4.2 TDD: Get Active Camp Edition

Write tests for:

- Returns current year's active edition (status = Open)
- Returns null if no active edition
- Includes camp location details
- Includes registration statistics

Then implement.

#### 4.3 TDD: Update Camp Edition

Write tests for:

- Can update Draft or Proposed editions
- Cannot update Open/Closed/Completed editions (or limited fields)
- Validation for all fields

Then implement.

#### 4.4 TDD: Change Edition Status

Write comprehensive tests for state machine:

```csharp
[Theory]
[InlineData(CampEditionStatus.Proposed, CampEditionStatus.Draft)]  // Valid: Promote
[InlineData(CampEditionStatus.Draft, CampEditionStatus.Open)]      // Valid: Open registrations
[InlineData(CampEditionStatus.Open, CampEditionStatus.Closed)]     // Valid: Close
[InlineData(CampEditionStatus.Closed, CampEditionStatus.Completed)] // Valid: Complete
public async Task ChangeStatusAsync_WithValidTransition_ChangesStatus(
    CampEditionStatus from,
    CampEditionStatus to)
{
    // Test valid transitions
}

[Theory]
[InlineData(CampEditionStatus.Open, CampEditionStatus.Draft)]      // Invalid: Cannot go back
[InlineData(CampEditionStatus.Completed, CampEditionStatus.Open)]  // Invalid: Cannot reopen
[InlineData(CampEditionStatus.Proposed, CampEditionStatus.Open)]   // Invalid: Must go through Draft
public async Task ChangeStatusAsync_WithInvalidTransition_ThrowsException(
    CampEditionStatus from,
    CampEditionStatus to)
{
    // Test invalid transitions
}

[Fact]
public async Task ChangeStatusAsync_DraftToOpen_WhenStartDateInPast_ThrowsException()
{
    // Cannot open registration for past date
}

[Fact]
public async Task ChangeStatusAsync_ClosedToCompleted_WhenEndDateInFuture_ThrowsException()
{
    // Cannot mark as completed if camp hasn't ended
}
```

Then implement state machine logic.

#### Acceptance Criteria

- [ ] Status workflow enforced correctly
- [ ] Cannot create duplicate edition for same camp+year
- [ ] Active edition query works efficiently
- [ ] Edition updates validated properly
- [ ] Date constraints enforced
- [ ] Audit trail for status changes

---

### Phase 5: Camp Edition Extras Management

**Goal**: Manage add-ons/extras for camp editions

**Dependencies**: Phase 4

**Duration**: 3-4 days

**User Stories**: US-CAMP-203

#### 5.1 TDD: Create Camp Edition Extra

Write tests for:

- Create extra with PerPerson/OneTime pricing
- Create extra with PerPerson/PerDay pricing
- Create extra with PerFamily/OneTime pricing
- Validate pricing >= 0
- Validate maxQuantity constraints
- Required extras vs optional

Then implement.

#### 5.2 TDD: Update/Delete Extras

Write tests for:

- Update extra (when no registrations use it)
- Update extra (when registrations exist) - should warn or prevent
- Delete extra (when no registrations)
- Prevent delete when registrations exist
- Activate/deactivate extras

Then implement.

#### 5.3 TDD: List Extras for Edition

Write tests for:

- Get all extras for edition
- Filter by active/inactive
- Sort by sortOrder
- Include current quantity sold

Then implement.

#### 5.4 TDD: Extra Quantity Tracking

Write tests for:

- Prevent selection when maxQuantity reached
- Decrement quantity when registration cancelled
- Concurrent registration handling

Then implement.

#### Acceptance Criteria

- [ ] All pricing types work correctly (PerPerson, PerFamily, OneTime, PerDay)
- [ ] Quantity limits enforced
- [ ] Cannot delete extras in use
- [ ] Required extras automatically added to registrations
- [ ] Extras can be activated/deactivated
- [ ] Display order respected

---

### Phase 6: Registration Integration (Separate Feature)

**Goal**: Update registration model and pricing calculator

**Note**: This phase is covered in the separate `feat-camp-registration-flow` specification.

**Dependencies**: Phase 5

**Duration**: 5-6 days (separate ticket)

See [camp-registration-flow.md](./camp-registration-flow.md) for details.

---

## Test Coverage Targets

### By Feature Area

| Feature | Unit Test Coverage | Integration Tests |
|---------|-------------------|-------------------|
| Association Settings | 95%+ | Yes |
| Camp Locations | 95%+ | Yes |
| Camp Editions | 95%+ | Yes |
| Camp Proposals | 95%+ | Yes |
| Camp Extras | 95%+ | Yes |
| Validators | 100% | N/A |

### Critical Test Scenarios

1. **Validation Tests**: Every validation rule must have tests
2. **State Machine Tests**: Every status transition tested
3. **Authorization Tests**: Role-based access enforced
4. **Edge Cases**: Boundary values, null handling, concurrent access
5. **Business Rules**: Age ranges, pricing calculations, capacity limits

---

## File Structure (Vertical Slice)

```
src/Abuvi.API/
├── Features/
│   ├── AssociationSettings/
│   │   ├── AssociationSettingsEndpoints.cs
│   │   ├── AssociationSettingsModels.cs
│   │   ├── AssociationSettingsService.cs
│   │   ├── AssociationSettingsRepository.cs
│   │   ├── IAssociationSettingsRepository.cs
│   │   └── UpdateAgeRangesValidator.cs
│   │
│   ├── Camps/
│   │   ├── CampsEndpoints.cs
│   │   ├── CampsModels.cs
│   │   ├── CampsService.cs
│   │   ├── CampsRepository.cs
│   │   ├── ICampsRepository.cs
│   │   ├── CreateCampValidator.cs
│   │   └── UpdateCampValidator.cs
│   │
│   ├── CampEditions/
│   │   ├── CampEditionsEndpoints.cs
│   │   ├── CampEditionsModels.cs
│   │   ├── CampEditionsService.cs
│   │   ├── CampEditionsRepository.cs
│   │   ├── ICampEditionsRepository.cs
│   │   ├── CreateCampEditionValidator.cs
│   │   ├── ProposeCampEditionValidator.cs
│   │   └── ChangeStatusValidator.cs
│   │
│   └── CampEditionExtras/
│       ├── CampEditionExtrasEndpoints.cs
│       ├── CampEditionExtrasModels.cs
│       ├── CampEditionExtrasService.cs
│       ├── CampEditionExtrasRepository.cs
│       ├── ICampEditionExtrasRepository.cs
│       └── CreateExtraValidator.cs
│
├── Data/
│   ├── AbuviDbContext.cs
│   ├── Configurations/
│   │   ├── AssociationSettingsConfiguration.cs
│   │   ├── CampConfiguration.cs
│   │   ├── CampEditionConfiguration.cs
│   │   └── CampEditionExtraConfiguration.cs
│   └── Migrations/
│       └── YYYYMMDD_AddCampPricingAndExtras.cs
│
└── Program.cs

tests/Abuvi.Tests/
├── Unit/
│   ├── Features/
│   │   ├── AssociationSettings/
│   │   │   ├── AssociationSettingsServiceTests.cs
│   │   │   └── UpdateAgeRangesValidatorTests.cs
│   │   ├── Camps/
│   │   │   ├── CampsServiceTests.cs
│   │   │   ├── CreateCampValidatorTests.cs
│   │   │   └── UpdateCampValidatorTests.cs
│   │   ├── CampEditions/
│   │   │   ├── CampEditionsServiceTests.cs
│   │   │   ├── StatusTransitionTests.cs
│   │   │   └── ProposalWorkflowTests.cs
│   │   └── CampEditionExtras/
│   │       ├── CampEditionExtrasServiceTests.cs
│   │       └── PricingCalculationTests.cs
│   └── ...
└── Integration/
    └── Features/
        ├── CampsEndpointsTests.cs
        ├── CampEditionsEndpointsTests.cs
        └── AssociationSettingsEndpointsTests.cs
```

---

## Development Workflow (Per Phase)

### For Each User Story

1. **RED**: Write failing tests
   - Unit tests for service logic
   - Unit tests for validators
   - Edge case tests

2. **GREEN**: Implement minimum code
   - Models/DTOs
   - Validator
   - Service method
   - Repository method
   - Endpoint

3. **REFACTOR**: Improve code
   - Extract common logic
   - Add XML documentation
   - Improve error messages
   - Add logging

4. **Verify**: Run all tests

   ```bash
   dotnet test
   ```

5. **Integration**: Create API endpoint tests

6. **Manual**: Test via Swagger/Postman

7. **Commit**: Commit after each user story

   ```bash
   git add .
   git commit -m "feat: implement US-CAMP-XXX [description]"
   ```

---

## Risk Mitigation

### Identified Risks

1. **Complex State Machine**: Status transitions have many rules
   - **Mitigation**: Comprehensive tests for all transitions

2. **Pricing Calculation Complexity**: Multiple pricing types and periods
   - **Mitigation**: Dedicated pricing service with extensive tests

3. **Data Migration**: Existing data must be migrated
   - **Mitigation**: Test migration on copy of production data first

4. **Age Range Changes**: Changing age ranges affects existing registrations
   - **Mitigation**: Warning message + recalculation logic

5. **Concurrent Registrations**: Multiple families registering simultaneously
   - **Mitigation**: Optimistic concurrency, capacity checks in transaction

---

## Definition of Done (Per Phase)

- [ ] All tests written BEFORE implementation
- [ ] All tests passing (90%+ coverage)
- [ ] No compiler warnings
- [ ] Code reviewed (self-review minimum)
- [ ] API endpoints documented in Swagger
- [ ] Database migration tested
- [ ] Integration tests passing
- [ ] Manual testing completed
- [ ] Committed to feature branch

---

## Deployment Strategy

### Per Phase Deployment

Each phase can be deployed independently:

1. **Phase 0**: Deploy schema changes (migrations)
2. **Phase 1**: Deploy Association Settings (minimal impact)
3. **Phase 2**: Deploy Camp Locations (Board-only feature)
4. **Phase 3**: Deploy Proposal workflow (Board-only feature)
5. **Phase 4**: Deploy Camp Editions (enables registration flow)
6. **Phase 5**: Deploy Extras (enhances registration)

### Feature Flags (Optional)

Consider feature flags for:

- Proposal workflow (can be disabled initially)
- Extras feature (can be enabled per edition)

---

## Timeline Estimate

| Phase | Duration | Dependencies |
|-------|----------|--------------|
| Phase 0: Foundation | 1-2 days | None |
| Phase 1: Association Settings | 2-3 days | Phase 0 |
| Phase 2: Camp Locations | 3-4 days | Phase 0 |
| Phase 3: Proposals | 3-4 days | Phase 0, 1, 2 |
| Phase 4: Editions | 4-5 days | Phase 0, 1, 2, 3 |
| Phase 5: Extras | 3-4 days | Phase 4 |
| **Total** | **16-22 days** | Sequential |

**Note**: Phases 1 and 2 can be done in parallel after Phase 0.

With parallelization: **14-18 days**

---

## Next Steps

1. Review and approve this implementation plan
2. Create GitHub issues for each phase
3. Set up development branch: `feature/camp-crud-enriched`
4. Begin Phase 0: Database migrations
5. Proceed through phases sequentially, following TDD

---

## Document Control

- **Version**: 1.0
- **Date**: 2026-02-13
- **Author**: AI Assistant (Claude)
- **Status**: Ready for Review
- **Related Specs**: [camp-crud-enriched.md](./camp-crud-enriched.md)
