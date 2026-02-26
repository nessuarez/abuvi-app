# Backend Implementation Plan: Camp CRUD with Age-Based Pricing & Extras

## 🔴 TDD Approach - CRITICAL

**Every ticket MUST follow Red-Green-Refactor:**

1. ✍️ Write failing tests FIRST
2. ✅ Implement minimum code to pass
3. 🔧 Refactor while keeping tests green

---

## Phase 0: Foundation (Database & Core Entities)

### Ticket 1: Database Schema - Camps & Association Settings

**Priority:** P0 (Blocker)
**Estimated Effort:** 2-3 hours

#### TDD Steps

1. **RED**: Write entity configuration tests
   - Test: Camp entity has all required fields (age-based pricing)
   - Test: AssociationSettings entity structure
   - Test: Database constraints (latitude/longitude ranges, price >= 0)

2. **GREEN**: Implement
   - Create `Camp` entity with age-based pricing fields
   - Create `AssociationSettings` entity
   - Create EF Core configurations (`CampConfiguration.cs`, `AssociationSettingsConfiguration.cs`)
   - Create migration: `20260213_AddCampsAndSettings.cs`
   - Apply migration

3. **REFACTOR**:
   - Verify indexes are optimal
   - Ensure naming conventions match standards

#### Acceptance

- [ ] Migration creates tables with correct schema
- [ ] All constraints enforced at DB level
- [ ] Entity configuration tests pass

---

### Ticket 2: Database Schema - Camp Editions & Extras

**Priority:** P0 (Blocker)
**Estimated Effort:** 3-4 hours

#### TDD Steps

1. **RED**: Write entity configuration tests
   - Test: CampEdition with status workflow fields
   - Test: CampEditionExtra with pricing types
   - Test: RegistrationExtra linking
   - Test: Unique constraint (camp_id, year)
   - Test: Custom age ranges validation

2. **GREEN**: Implement
   - Create `CampEdition` entity
   - Create `CampEditionExtra` entity
   - Create `RegistrationExtra` entity
   - Update `Registration` entity (add extras relationship)
   - Create EF Core configurations
   - Create migration: `20260213_AddCampEditionsAndExtras.cs`

3. **REFACTOR**:
   - Optimize foreign key relationships
   - Verify cascade delete behavior

#### Acceptance

- [ ] All entities created with relationships
- [ ] Unique constraints prevent duplicate editions
- [ ] Age range validation works at DB level

---

## Phase 1: Camp Locations Management

### Ticket 3: Camp Locations - Repository & Service (TDD)

**Priority:** P1
**Estimated Effort:** 4-5 hours

#### TDD Steps

**1. RED - Write Repository Tests First:**

`tests/Abuvi.Tests/Unit/Features/Camps/CampsRepositoryTests.cs`

```csharp
[Fact]
public async Task GetByIdAsync_WithValidId_ReturnsCamp()
{
    // Test gets camp by ID
}

[Fact]
public async Task GetAllAsync_WithStatusFilter_ReturnsFilteredCamps()
{
    // Test filtering by Active/Inactive status
}

[Fact]
public async Task AddAsync_WithValidCamp_AddsCamp()
{
    // Test camp creation
}
```

**2. RED - Write Service Tests:**

`tests/Abuvi.Tests/Unit/Features/Camps/CampsServiceTests.cs`

```csharp
[Fact]
public async Task CreateAsync_WithValidPricingTemplate_CreatesCamp()
{
    // Arrange: Mock repository
    // Act: Create camp with adult=180, child=120, baby=60
    // Assert: Camp created with correct pricing
}

[Fact]
public async Task CreateAsync_WithNegativePrices_ThrowsValidationException()
{
    // Test validation rejects negative prices
}

[Fact]
public async Task CreateAsync_WithInvalidCoordinates_ThrowsValidationException()
{
    // Test latitude/longitude validation
}

[Fact]
public async Task UpdateAsync_WithCamp_UpdatesSuccessfully()
{
    // Test camp update
}

[Fact]
public async Task DeleteAsync_WithActiveEditions_ThrowsException()
{
    // Test cannot delete camp with editions
}
```

**3. GREEN - Implement to Pass Tests:**

- Create `ICampsRepository` interface
- Implement `CampsRepository` with EF Core
- Create `CampsService` with business logic
- Create `CreateCampRequest`, `UpdateCampRequest`, `CampResponse` DTOs
- Implement `CreateCampRequestValidator` (FluentValidation)

**4. REFACTOR:**

- Extract common validation logic
- Optimize queries with `.AsNoTracking()` where appropriate

#### Files to Create

```
src/Abuvi.API/Features/Camps/
├── CampsModels.cs          # Camp entity, DTOs
├── CampsRepository.cs      # ICampsRepository, implementation
├── CampsService.cs         # Business logic
└── CreateCampValidator.cs  # FluentValidation
```

#### Acceptance

- [ ] All repository tests pass
- [ ] All service tests pass
- [ ] 90%+ code coverage on service layer

---

### Ticket 4: Camp Locations - API Endpoints (TDD)

**Priority:** P1
**Estimated Effort:** 3-4 hours

#### TDD Steps

**1. RED - Write Endpoint Tests:**

`tests/Abuvi.Tests/Unit/Features/Camps/CampsEndpointsTests.cs`

```csharp
[Fact]
public async Task GetCamps_AsBoard_ReturnsOk()
{
    // Test GET /api/camps returns 200
}

[Fact]
public async Task CreateCamp_WithValidRequest_ReturnsCreated()
{
    // Test POST /api/camps with valid data returns 201
}

[Fact]
public async Task CreateCamp_AsMember_ReturnsForbidden()
{
    // Test authorization - only Board can create
}

[Fact]
public async Task UpdateCamp_WithValidData_ReturnsOk()
{
    // Test PUT /api/camps/{id}
}

[Fact]
public async Task DeleteCamp_WithActiveEditions_ReturnsBadRequest()
{
    // Test cascade prevention
}
```

**2. GREEN - Implement Endpoints:**

`src/Abuvi.API/Features/Camps/CampsEndpoints.cs`

```csharp
public static class CampsEndpoints
{
    public static void MapCampsEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/camps")
            .WithTags("Camps")
            .RequireAuthorization(); // Board only

        group.MapGet("", GetCamps);
        group.MapGet("{id:guid}", GetCampById);
        group.MapPost("", CreateCamp);
        group.MapPut("{id:guid}", UpdateCamp);
        group.MapDelete("{id:guid}", DeleteCamp);
    }
}
```

**3. REFACTOR:**

- Add OpenAPI documentation
- Standardize error responses

#### Acceptance

- [ ] All endpoint tests pass
- [ ] Authorization enforced (Board+ only)
- [ ] Validation errors return 400 with details
- [ ] OpenAPI docs generated

---

## Phase 2: Association Settings

### Ticket 5: Age Ranges Configuration (TDD)

**Priority:** P1
**Estimated Effort:** 3-4 hours

#### TDD Steps

**1. RED - Write Service Tests:**

`tests/Abuvi.Tests/Unit/Features/AssociationSettings/AssociationSettingsServiceTests.cs`

```csharp
[Fact]
public async Task GetAgeRangesAsync_ReturnsDefaultRanges()
{
    // Test: Returns baby=0-2, child=3-12, adult=13+
}

[Fact]
public async Task UpdateAgeRangesAsync_WithValidRanges_Updates()
{
    // Test: Update age ranges successfully
}

[Fact]
public async Task UpdateAgeRangesAsync_WithChildMinLessThanBabyMax_ThrowsValidation()
{
    // Test: Child min must be > baby max
}

[Fact]
public async Task UpdateAgeRangesAsync_LogsChangeWithUserId()
{
    // Test: Audit trail includes updatedBy
}
```

**2. GREEN - Implement:**

- Create `AssociationSettingsService`
- Create `AssociationSettingsRepository`
- Create `UpdateAgeRangesRequest`, `AgeRangesResponse` DTOs
- Create `UpdateAgeRangesValidator`
- Seed default age ranges in migration

**3. REFACTOR:**

- Cache age ranges (rarely change)

#### Acceptance

- [ ] All service tests pass
- [ ] Validation prevents invalid age ranges
- [ ] Audit trail captured

---

### Ticket 6: Age Ranges API Endpoints (TDD)

**Priority:** P1
**Estimated Effort:** 2-3 hours

#### TDD Steps

**1. RED - Write Endpoint Tests:**

```csharp
[Fact]
public async Task GetAgeRanges_AsBoard_ReturnsOk()

[Fact]
public async Task UpdateAgeRanges_AsMember_ReturnsForbidden()

[Fact]
public async Task UpdateAgeRanges_WithInvalidRanges_ReturnsBadRequest()
```

**2. GREEN - Implement:**

- `GET /api/settings/age-ranges`
- `PUT /api/settings/age-ranges`
- Authorization: Board+ only

**3. REFACTOR:**

- Add caching middleware

#### Acceptance

- [ ] Endpoint tests pass
- [ ] Only Board can modify settings

---

## Phase 3: Camp Editions - Proposal Workflow

### Ticket 7: Camp Editions - Proposed Status (TDD)

**Priority:** P2
**Estimated Effort:** 5-6 hours

#### TDD Steps

**1. RED - Write Service Tests:**

`tests/Abuvi.Tests/Unit/Features/CampEditions/CampEditionsServiceTests.cs`

```csharp
[Fact]
public async Task ProposeAsync_WithValidData_CreatesProposedEdition()
{
    // Test: Creates edition with status=Proposed
}

[Fact]
public async Task ProposeAsync_AllowsMultipleProposedForSameYear()
{
    // Test: Can create multiple proposals for same year
}

[Fact]
public async Task GetProposedAsync_FiltersByYear_ReturnsProposed()
{
    // Test: GET proposed editions for 2026
}

[Fact]
public async Task PromoteToDraftAsync_ChangesStatus_FromProposedToDraft()
{
    // Test: Promote proposed → draft
}

[Fact]
public async Task PromoteToDraftAsync_FromNonProposed_ThrowsException()
{
    // Test: Only Proposed can be promoted
}

[Fact]
public async Task RejectAsync_SoftDeletesProposal()
{
    // Test: Reject marks isArchived=true
}
```

**2. GREEN - Implement:**

- Create `CampEditionsService`
- Create `CampEditionsRepository`
- Implement proposal workflow methods
- Create validators

**3. REFACTOR:**

- Extract status transition logic to separate class

#### Acceptance

- [ ] All service tests pass (90%+ coverage)
- [ ] Multiple proposals allowed per year
- [ ] Status transitions validated

---

### Ticket 8: Camp Editions Proposal - API Endpoints (TDD)

**Priority:** P2
**Estimated Effort:** 3-4 hours

#### TDD Steps

**1. RED - Write Endpoint Tests:**

```csharp
[Fact]
public async Task ProposeEdition_AsBoard_ReturnsCreated()

[Fact]
public async Task GetProposed_ReturnsOnlyProposedStatus()

[Fact]
public async Task PromoteToDraft_AsBoard_ReturnsOk()

[Fact]
public async Task RejectProposal_AsBoard_ReturnsNoContent()
```

**2. GREEN - Implement:**

- `POST /api/camps/editions/propose`
- `GET /api/camps/editions/proposed?year=2026`
- `POST /api/camps/editions/{id}/promote`
- `DELETE /api/camps/editions/{id}/reject`

**3. REFACTOR:**

- Standardize response formats

#### Acceptance

- [ ] All endpoint tests pass
- [ ] Authorization enforced

---

## Phase 4: Camp Editions - Full Lifecycle

### Ticket 9: Camp Editions - CRUD Operations (TDD)

**Priority:** P2
**Estimated Effort:** 5-6 hours

#### TDD Steps

**1. RED - Write Service Tests:**

```csharp
[Fact]
public async Task CreateAsync_WithValidEdition_CreatesEdition()

[Fact]
public async Task CreateAsync_DuplicateYearForCamp_ThrowsException()
{
    // Test: Unique constraint (camp_id, year)
}

[Fact]
public async Task CreateAsync_InheritsAgeRangesFromSettings()
{
    // Test: useCustomAgeRanges=false uses global settings
}

[Fact]
public async Task CreateAsync_WithCustomAgeRanges_UsesCustom()
{
    // Test: useCustomAgeRanges=true uses provided ranges
}

[Fact]
public async Task GetActiveAsync_ReturnsCurrentYearOpenEdition()

[Fact]
public async Task UpdateAsync_WithEdition_UpdatesSuccessfully()

[Fact]
public async Task DeleteAsync_WithRegistrations_ThrowsException()
```

**2. GREEN - Implement:**

- Complete `CampEditionsService` CRUD methods
- Handle age ranges logic (global vs custom)
- Create validators

**3. REFACTOR:**

- Extract age range resolution logic

#### Acceptance

- [ ] All service tests pass
- [ ] Unique constraint enforced
- [ ] Age ranges logic works correctly

---

### Ticket 10: Camp Editions - Status Transitions (TDD)

**Priority:** P2
**Estimated Effort:** 4-5 hours

#### TDD Steps

**1. RED - Write Status Transition Tests:**

```csharp
[Fact]
public async Task TransitionStatus_DraftToOpen_ValidatesFutureDates()
{
    // Test: Cannot open if start date in past
}

[Fact]
public async Task TransitionStatus_OpenToClosed_AlwaysAllowed()

[Fact]
public async Task TransitionStatus_ClosedToCompleted_ValidatesPastEndDate()

[Fact]
public async Task TransitionStatus_BackwardsTransition_ThrowsException()
{
    // Test: Cannot go Open → Draft
}

[Fact]
public async Task TransitionStatus_LogsAuditTrail()
```

**2. GREEN - Implement:**

- Create `CampEditionStatusService` or add to existing service
- Implement workflow validation
- Add audit logging

**3. REFACTOR:**

- State machine pattern for transitions

#### Acceptance

- [ ] All transition tests pass
- [ ] Audit trail logged

---

### Ticket 11: Camp Editions - API Endpoints (TDD)

**Priority:** P2
**Estimated Effort:** 3-4 hours

#### TDD Steps

**1. RED - Write Endpoint Tests:**

```csharp
[Fact]
public async Task GetActiveEdition_ReturnsCurrentYearEdition()

[Fact]
public async Task CreateEdition_WithValidData_ReturnsCreated()

[Fact]
public async Task UpdateEdition_AsBoard_ReturnsOk()

[Fact]
public async Task TransitionStatus_DraftToOpen_ReturnsOk()

[Fact]
public async Task DeleteEdition_WithRegistrations_ReturnsBadRequest()
```

**2. GREEN - Implement:**

- `GET /api/camps/editions/active`
- `GET /api/camps/editions/{id}`
- `POST /api/camps/editions`
- `PUT /api/camps/editions/{id}`
- `POST /api/camps/editions/{id}/status`
- `DELETE /api/camps/editions/{id}`

**3. REFACTOR:**

- Add response caching for active edition

#### Acceptance

- [ ] All endpoint tests pass
- [ ] OpenAPI docs complete

---

## Phase 5: Camp Edition Extras

### Ticket 12: Camp Edition Extras - Service Layer (TDD)

**Priority:** P2
**Estimated Effort:** 5-6 hours

#### TDD Steps

**1. RED - Write Service Tests:**

`tests/Abuvi.Tests/Unit/Features/CampEditionExtras/CampEditionExtrasServiceTests.cs`

```csharp
[Fact]
public async Task CreateAsync_WithPerPersonOneTime_CreatesExtra()
{
    // Test: Kayak rental, €25, per person, one-time
}

[Fact]
public async Task CreateAsync_WithPerPersonPerDay_CreatesExtra()
{
    // Test: Vegan menu, €5, per person, per day
}

[Fact]
public async Task CreateAsync_WithPerFamilyOneTime_CreatesExtra()
{
    // Test: Workshop, €50, per family, one-time
}

[Fact]
public async Task CreateAsync_WithMaxQuantity_EnforcesLimit()
{
    // Test: Max quantity validation
}

[Fact]
public async Task UpdateAsync_WithRegistrations_ShowsWarning()
{
    // Test: Warn when extra already selected
}

[Fact]
public async Task DeleteAsync_WithRegistrations_ThrowsException()
{
    // Test: Cannot delete if in use
}

[Fact]
public async Task ActivateAsync_SetsIsActiveTrue()

[Fact]
public async Task DeactivateAsync_SetsIsActiveFalse()
```

**2. GREEN - Implement:**

- Create `CampEditionExtrasService`
- Create `CampEditionExtrasRepository`
- Create validators (pricing types, max quantity)
- Implement CRUD operations

**3. REFACTOR:**

- Extract pricing calculation logic

#### Acceptance

- [ ] All service tests pass (90%+ coverage)
- [ ] All pricing types work correctly
- [ ] Max quantity enforced

---

### Ticket 13: Camp Edition Extras - API Endpoints (TDD)

**Priority:** P2
**Estimated Effort:** 3-4 hours

#### TDD Steps

**1. RED - Write Endpoint Tests:**

```csharp
[Fact]
public async Task GetExtras_ForEdition_ReturnsAll()

[Fact]
public async Task CreateExtra_WithValidData_ReturnsCreated()

[Fact]
public async Task UpdateExtra_AsBoard_ReturnsOk()

[Fact]
public async Task DeleteExtra_WithRegistrations_ReturnsBadRequest()
```

**2. GREEN - Implement:**

- `GET /api/camps/editions/{editionId}/extras`
- `POST /api/camps/editions/{editionId}/extras`
- `PUT /api/camps/editions/{editionId}/extras/{extraId}`
- `DELETE /api/camps/editions/{editionId}/extras/{extraId}`

**3. REFACTOR:**

- Add pagination for large lists

#### Acceptance

- [ ] All endpoint tests pass
- [ ] Authorization enforced

---

## Phase 6: Registration Pricing Calculator

### Ticket 14: Registration Pricing Service (TDD)

**Priority:** P3
**Estimated Effort:** 6-8 hours

#### TDD Steps

**1. RED - Write Comprehensive Pricing Tests:**

`tests/Abuvi.Tests/Unit/Features/Registrations/RegistrationPricingServiceTests.cs`

```csharp
[Fact]
public async Task CalculateBaseAmount_MixedAgeFamily_CalculatesCorrectly()
{
    // Family: 2 adults, 1 child (10), 1 baby (1)
    // Edition: Adult=€180, Child=€120, Baby=€60
    // Expected: 180 + 180 + 120 + 60 = €540
}

[Fact]
public async Task CalculateBaseAmount_UsesGlobalAgeRanges_WhenCustomDisabled()
{
    // Test: useCustomAgeRanges=false → uses AssociationSettings
}

[Fact]
public async Task CalculateBaseAmount_UsesCustomAgeRanges_WhenEnabled()
{
    // Test: useCustomAgeRanges=true → uses edition ranges
}

[Fact]
public async Task CalculateExtrasAmount_PerPersonOneTime_MultipliesByQuantity()
{
    // Kayak rental: €25 × 2 people = €50
}

[Fact]
public async Task CalculateExtrasAmount_PerPersonPerDay_CalculatesDuration()
{
    // Vegan menu: €5 × 1 person × 10 days = €50
}

[Fact]
public async Task CalculateExtrasAmount_PerFamilyOneTime_IgnoresQuantity()
{
    // Workshop: €50 (fixed, regardless of family size)
}

[Fact]
public async Task CalculateTotalAmount_WithExtrasAndDiscount_CalculatesCorrectly()
{
    // Base: €540 + Extras: €100 - Discount: €50 = €590
}

[Fact]
public async Task CalculateTotalAmount_AddsRequiredExtrasAutomatically()
{
    // Test: Required extras auto-added to calculation
}
```

**2. GREEN - Implement:**

`src/Abuvi.API/Features/Registrations/RegistrationPricingService.cs`

```csharp
public class RegistrationPricingService
{
    public async Task<PricingBreakdown> CalculateAsync(
        Guid registrationId,
        CancellationToken ct)
    {
        // 1. Get registration with family members
        // 2. Get camp edition with age ranges & pricing
        // 3. Calculate base amount by age category
        // 4. Calculate extras amount
        // 5. Apply discounts
        // 6. Return breakdown
    }

    private decimal CalculateMemberPrice(Member member, CampEdition edition)
    {
        var age = CalculateAge(member.DateOfBirth);
        var ageRanges = GetAgeRanges(edition);

        if (age <= ageRanges.BabyMaxAge)
            return edition.PricePerBaby;
        if (age >= ageRanges.ChildMinAge && age <= ageRanges.ChildMaxAge)
            return edition.PricePerChild;
        return edition.PricePerAdult;
    }

    private decimal CalculateExtraAmount(
        RegistrationExtra regExtra,
        CampEdition edition)
    {
        var extra = regExtra.Extra;
        var basePrice = extra.Price * regExtra.Quantity;

        if (extra.PricingPeriod == PricingPeriod.PerDay)
        {
            var days = (edition.EndDate - edition.StartDate).Days;
            return basePrice * days;
        }

        return basePrice;
    }
}
```

**3. REFACTOR:**

- Extract age calculation to helper
- Cache age ranges lookup
- Optimize queries with eager loading

#### Acceptance

- [ ] All pricing tests pass (90%+ coverage)
- [ ] Age-based pricing correct
- [ ] All extra pricing types calculated correctly
- [ ] Performance: <100ms for typical family

---

### Ticket 15: Update Registration Model & Service (TDD)

**Priority:** P3
**Estimated Effort:** 4-5 hours

#### TDD Steps

**1. RED - Write Updated Registration Tests:**

```csharp
[Fact]
public async Task CreateRegistration_CalculatesPricing_Automatically()
{
    // Test: On create, pricing calculated via RegistrationPricingService
}

[Fact]
public async Task AddExtra_UpdatesTotalAmount()
{
    // Test: Adding extra recalculates total
}

[Fact]
public async Task RemoveExtra_UpdatesTotalAmount()

[Fact]
public async Task CreateRegistration_AddsRequiredExtras_Automatically()
```

**2. GREEN - Implement:**

- Update `Registration` entity (add extras relationships)
- Update `RegistrationsService` to use `RegistrationPricingService`
- Update `CreateRegistrationRequest` to include selected extras
- Create `RegistrationExtrasRepository`

**3. REFACTOR:**

- Separate concerns: registration creation vs pricing

#### Acceptance

- [ ] Registration tests pass
- [ ] Pricing integration works
- [ ] Required extras auto-added

---

## Phase 7: Integration & Testing

### Ticket 16: End-to-End Integration Tests

**Priority:** P3
**Estimated Effort:** 6-8 hours

#### Test Scenarios

**1. Full Camp Lifecycle:**

```csharp
[Fact]
public async Task FullCampWorkflow_FromProposalToCompletion()
{
    // 1. Create camp location
    // 2. Propose 3 candidates for 2026
    // 3. Promote one to Draft
    // 4. Add extras
    // 5. Open registrations
    // 6. Create registration with extras
    // 7. Verify pricing
    // 8. Close registrations
    // 9. Mark as completed
}
```

**2. Pricing Edge Cases:**

```csharp
[Fact]
public async Task Pricing_AllBabies_CalculatesCorrectly()

[Fact]
public async Task Pricing_AllAdults_CalculatesCorrectly()

[Fact]
public async Task Pricing_CustomAgeRanges_OverridesGlobal()

[Fact]
public async Task Pricing_MultipleExtras_SumsCorrectly()
```

**3. Validation & Security:**

```csharp
[Fact]
public async Task Member_CannotCreateCamp_Returns403()

[Fact]
public async Task DuplicateYearEdition_ThrowsException()

[Fact]
public async Task NegativePrices_RejectedByValidation()
```

#### Acceptance

- [ ] All integration tests pass
- [ ] No N+1 query issues
- [ ] Performance acceptable

---

### Ticket 17: Database Migration & Seed Data

**Priority:** P0 (Run Early)
**Estimated Effort:** 2-3 hours

#### Tasks

1. Create seed migration for default age ranges
2. Create sample camp locations (optional)
3. Test migration rollback
4. Document migration in README

#### Seed Data

```csharp
migrationBuilder.InsertData(
    table: "association_settings",
    columns: new[] { "id", "setting_key", "setting_value", "updated_by" },
    values: new object[] {
        Guid.NewGuid(),
        "age_ranges",
        "{\"babyMaxAge\":2,\"childMinAge\":3,\"childMaxAge\":12,\"adultMinAge\":13}",
        /* admin user id */
    }
);
```

#### Acceptance

- [ ] Seed data applied successfully
- [ ] Rollback works without data loss

---

## Summary

### Total Estimated Effort: **60-75 hours** (7-9 days)

### Ticket Priorities

- **P0 (Blockers):** Tickets 1, 2, 17 - Database foundation
- **P1 (High):** Tickets 3-6 - Camp locations & settings
- **P2 (Medium):** Tickets 7-13 - Camp editions & extras
- **P3 (Normal):** Tickets 14-16 - Pricing & integration

### TDD Coverage Target

- **Unit Tests:** 90%+ for services, validators
- **Integration Tests:** Key workflows covered
- **Total Test Count:** ~150-200 tests

### Dependencies

```
Ticket 1,2,17 → Ticket 3 → Ticket 4
             → Ticket 5 → Ticket 6
             → Ticket 7 → Ticket 8
             → Ticket 9,10 → Ticket 11
             → Ticket 12 → Ticket 13
             → Ticket 14 → Ticket 15
All → Ticket 16
```

### Key Risks

1. **Age range logic complexity** - Mitigated by comprehensive tests
2. **Pricing calculation edge cases** - Mitigated by extensive test scenarios
3. **Migration complexity** - Mitigated by early testing and rollback plan

---

## Next Steps

1. **Review & Approve Plan** - Get stakeholder sign-off
2. **Set Up Test Infrastructure** - Ensure xUnit, FluentAssertions, NSubstitute ready
3. **Start with Ticket 1** - Database foundation (TDD from day 1)
4. **Daily Test Reviews** - Ensure 90%+ coverage maintained
5. **Continuous Integration** - Run tests on every commit

**Remember: Write tests FIRST, then implement. No exceptions.** 🔴✅🔧

---

## Document Control

- **Version**: 1.0
- **Date**: 2026-02-13
- **Author**: Development Team
- **Status**: Ready for Implementation
- **Related Spec**: [camp-crud-enriched.md](./camp-crud-enriched.md)
