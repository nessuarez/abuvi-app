# Camp Editions Management - Backend Implementation

## Overview

This specification covers the implementation of Camp Edition management functionality, including the proposal workflow for selecting camp candidates and full CRUD operations for managing camp editions throughout their lifecycle.

**Related Specs:**

- Parent: [camp-crud-enriched.md](../feat-camps-definition/camp-crud-enriched.md)
- Implementation Plan: [IMPLEMENTATION_PLAN.md](../feat-camps-definition/IMPLEMENTATION_PLAN.md)

**Status:** 🔶 Partial - Proposal workflow implemented, status machine needs verification

---

## Feature Description

Camp editions represent specific instances of camps scheduled for particular years. The system supports:

1. **Proposal Workflow** (Phase 3): Board members propose multiple camp candidates for a year, compare them, and select one to promote to Draft status. In that moment, the edition is created in the database with status = Draft. Rejected candidates are archived (soft deleted).

2. **Edition Lifecycle Management** (Phase 4): Full CRUD operations and status management for camp editions through their complete lifecycle.

### Edition Status Workflow

```
Proposed → Draft → Open → Closed → Completed
    ↓
Rejected (archived)
```

**Status Definitions:**

- **Proposed**: Candidate edition awaiting board approval
- **Draft**: Approved edition, not yet open for registration
- **Open**: Registrations are open. Camp details are visible to members.
- **Closed**: Registrations closed, camp upcoming or in progress
- **Completed**: Camp finished, final statistics available
- **Rejected**: Proposal rejected (archived/soft deleted)

---

## Database Schema

**Status:** ✅ COMPLETED - Migration already created

The `camp_editions` table includes:

- Basic info: `camp_id`, `year`, `start_date`, `end_date`
- Pricing: `price_per_adult`, `price_per_child`, `price_per_baby`
- Age ranges: `use_custom_age_ranges`, custom age fields
- Status: `status` (enum)
- Capacity: `max_capacity`
- Proposal fields: Notes for comparing candidates
- Audit: `created_at`, `updated_at`, `is_archived`

---

## Implementation Phases

### Phase 3: Camp Proposal/Candidate Workflow

**Duration**: 2-3 days
**User Stories**: US-CAMP-000, US-CAMP-001, US-CAMP-002, US-CAMP-003

#### Current Status

✅ **COMPLETED:**

- Endpoint: `POST /api/camps/editions/propose` - Create proposal
- Endpoint: `GET /api/camps/editions/proposed?year={year}` - List proposals
- Endpoint: `POST /api/camps/editions/{id}/promote` - Promote to draft
- Endpoint: `DELETE /api/camps/editions/{id}/reject` - Reject proposal
- Service methods implemented in `CampEditionsService.cs`
- Tests in `CampEditionsServiceTests.cs`

#### Acceptance Criteria

- [x] Can propose multiple candidates for same year
- [x] Proposed editions not visible to regular members (Board/Admin only)
- [x] Can promote one candidate to Draft
- [x] Can reject candidates
- [x] Board members can compare proposals side-by-side
- [x] Audit trail for all status changes

---

### Phase 4: Camp Edition Management

**Duration**: 3-4 days
**User Stories**: US-CAMP-201, US-CAMP-202, US-CAMP-204

#### Current Status

⚠️ **PARTIAL - Needs Verification:**

The basic structure exists but needs verification of:

1. Full status state machine implementation
2. All status transition validations
3. Field update restrictions based on status
4. Date constraint validations

#### Tasks Remaining

##### 4.1 Verify/Complete Status State Machine

**Valid Transitions:**

- `Proposed → Draft` (promotion)
- `Draft → Open` (open registrations)
- `Open → Closed` (close registrations)
- `Closed → Completed` (mark as finished)
- `Proposed → Rejected` (reject proposal)

**Invalid Transitions (must be blocked):**

- Any backward transitions (e.g., `Open → Draft`)
- Skip transitions (e.g., `Proposed → Open`)
- Reopening completed editions

**Test Coverage Needed:**

```csharp
[Theory]
[InlineData(CampEditionStatus.Proposed, CampEditionStatus.Draft)]  // Valid
[InlineData(CampEditionStatus.Draft, CampEditionStatus.Open)]      // Valid
[InlineData(CampEditionStatus.Open, CampEditionStatus.Closed)]     // Valid
[InlineData(CampEditionStatus.Closed, CampEditionStatus.Completed)] // Valid
public async Task ChangeStatusAsync_WithValidTransition_ChangesStatus(
    CampEditionStatus from,
    CampEditionStatus to)
{
    // Test valid transitions
}

[Theory]
[InlineData(CampEditionStatus.Open, CampEditionStatus.Draft)]      // Invalid
[InlineData(CampEditionStatus.Completed, CampEditionStatus.Open)]  // Invalid
[InlineData(CampEditionStatus.Proposed, CampEditionStatus.Open)]   // Invalid
public async Task ChangeStatusAsync_WithInvalidTransition_ThrowsException(
    CampEditionStatus from,
    CampEditionStatus to)
{
    // Test invalid transitions
}
```

**Implementation:**

- File: `src/Abuvi.API/Features/Camps/CampEditionsService.cs`
- Method: `ChangeStatusAsync(Guid id, CampEditionStatus newStatus, CancellationToken ct)`
- Add validator: `ChangeStatusRequestValidator.cs`

##### 4.2 Date Constraint Validation

**Business Rules:**

1. **Cannot open registration for past dates:**

   ```csharp
   [Fact]
   public async Task ChangeStatusAsync_DraftToOpen_WhenStartDateInPast_ThrowsException()
   {
       // StartDate must be >= Today when transitioning to Open
   }
   ```

2. **Cannot mark as completed before end date:**

   ```csharp
   [Fact]
   public async Task ChangeStatusAsync_ClosedToCompleted_WhenEndDateInFuture_ThrowsException()
   {
       // EndDate must be < Today when transitioning to Completed
   }
   ```

3. **End date must be after start date:**

   ```csharp
   [Fact]
   public async Task CreateAsync_WhenEndDateBeforeStartDate_ThrowsValidationException()
   {
       // Validation in CreateCampEditionValidator
   }
   ```

##### 4.3 Update Edition with Status Restrictions

**Business Rules:**

- **Proposed/Draft**: All fields can be updated
- **Open**: Limited updates (can't change dates, pricing)
- **Closed/Completed**: Read-only (or very limited updates)

**Implementation:**

```csharp
public async Task<CampEditionResponse> UpdateAsync(
    Guid id,
    UpdateCampEditionRequest request,
    CancellationToken ct)
{
    var edition = await _repository.GetByIdAsync(id, ct);

    if (edition == null)
        throw new NotFoundException("Camp edition not found");

    // Check what can be updated based on status
    if (edition.Status == CampEditionStatus.Open)
    {
        // Only allow updating notes, max_capacity
        if (request.StartDate != edition.StartDate ||
            request.EndDate != edition.EndDate ||
            request.PricePerAdult != edition.PricePerAdult)
        {
            throw new InvalidOperationException(
                "Cannot change dates or pricing for an open edition");
        }
    }

    if (edition.Status is CampEditionStatus.Closed or CampEditionStatus.Completed)
    {
        throw new InvalidOperationException(
            "Cannot update a closed or completed edition");
    }

    // Apply updates...
}
```

**Tests Needed:**

```csharp
[Fact]
public async Task UpdateAsync_WhenStatusIsDraft_AllowsAllUpdates()
{
    // Can update all fields
}

[Fact]
public async Task UpdateAsync_WhenStatusIsOpen_PreventsDateAndPriceChanges()
{
    // Only notes and capacity allowed
}

[Fact]
public async Task UpdateAsync_WhenStatusIsClosed_ThrowsException()
{
    // No updates allowed
}
```

##### 4.4 Get Active Camp Edition

**Endpoint:** `GET /api/camps/editions/active?year={year}`

**Business Logic:**

- Returns the current year's edition with status = `Open`
- If no year specified, uses current year
- Returns `null` if no active edition found
- Includes camp location details
- Includes registration statistics (count, capacity)

**Implementation:**

```csharp
public async Task<CampEditionResponse?> GetActiveEditionAsync(
    int? year,
    CancellationToken ct)
{
    var targetYear = year ?? DateTime.UtcNow.Year;

    var edition = await _repository.GetByYearAndStatusAsync(
        targetYear,
        CampEditionStatus.Open,
        ct);

    if (edition == null)
        return null;

    // Load camp details
    var camp = await _campsRepository.GetByIdAsync(edition.CampId, ct);

    // Get registration stats
    var registrationCount = await _registrationsRepository
        .CountByCampEditionAsync(edition.Id, ct);

    return edition.ToResponse(camp, registrationCount);
}
```

**Tests:**

```csharp
[Fact]
public async Task GetActiveEditionAsync_WhenOpenEditionExists_ReturnsEdition()
{
    // Test retrieval of active edition
}

[Fact]
public async Task GetActiveEditionAsync_WhenNoOpenEdition_ReturnsNull()
{
    // Test null return
}

[Fact]
public async Task GetActiveEditionAsync_IncludesCampLocationDetails()
{
    // Verify camp details are included
}

[Fact]
public async Task GetActiveEditionAsync_IncludesRegistrationStats()
{
    // Verify registration count and capacity
}
```

##### 4.5 Prevent Duplicate Editions

**Business Rule:** Cannot create two editions for the same `(camp_id, year)` combination.

**Implementation:**

- Add unique constraint in migration (if not present)
- Validation in service before creating

```csharp
public async Task<CampEditionResponse> CreateAsync(
    CreateCampEditionRequest request,
    CancellationToken ct)
{
    // Check for existing edition
    var exists = await _repository.ExistsAsync(
        request.CampId,
        request.Year,
        ct);

    if (exists)
    {
        throw new InvalidOperationException(
            $"An edition for this camp already exists for year {request.Year}");
    }

    // Create edition...
}
```

**Test:**

```csharp
[Fact]
public async Task CreateAsync_WhenEditionExistsForYear_ThrowsException()
{
    // Test duplicate prevention
}
```

##### 4.6 New Endpoints Needed

1. **Change Status:**

   ```
   PATCH /api/camps/editions/{id}/status
   Body: { "status": "Open" }
   ```

2. **Get Active Edition:**

   ```
   GET /api/camps/editions/active?year=2026
   ```

3. **Update Edition:**

   ```
   PUT /api/camps/editions/{id}
   Body: { full edition update }
   ```

4. **List All Editions:**

   ```
   GET /api/camps/editions?year=2026&status=Open&campId={guid}
   ```

#### Acceptance Criteria

- [ ] Status workflow enforced correctly (all transitions tested)
- [ ] Cannot create duplicate edition for same camp+year
- [ ] Active edition query works efficiently
- [ ] Edition updates validated based on status
- [ ] Date constraints enforced (cannot open past dates, etc.)
- [ ] Audit trail for all status changes
- [ ] All endpoints documented in Swagger
- [ ] 95%+ test coverage

---

## API Endpoints Summary

### Existing (Phase 3) ✅

| Method | Endpoint | Description | Auth |
|--------|----------|-------------|------|
| POST | `/api/camps/editions/propose` | Propose new camp edition | Board+ |
| GET | `/api/camps/editions/proposed?year={year}` | Get proposed editions | Board+ |
| POST | `/api/camps/editions/{id}/promote` | Promote to draft | Board+ |
| DELETE | `/api/camps/editions/{id}/reject` | Reject proposal | Board+ |

### Needed (Phase 4) ⚠️

| Method | Endpoint | Description | Auth |
|--------|----------|-------------|------|
| PATCH | `/api/camps/editions/{id}/status` | Change edition status | Board+ |
| GET | `/api/camps/editions/active?year={year}` | Get active edition | Member+ |
| PUT | `/api/camps/editions/{id}` | Update edition | Board+ |
| GET | `/api/camps/editions` | List all editions (filtered) | Board+ |
| GET | `/api/camps/editions/{id}` | Get edition by ID | Member+ |

---

## Testing Strategy

### Unit Tests Required

1. **Status State Machine Tests** (`StatusTransitionTests.cs`)
   - All valid transitions
   - All invalid transitions
   - Date-based constraints

2. **Service Tests** (`CampEditionsServiceTests.cs`)
   - Create edition (Draft)
   - Update edition (with status restrictions)
   - Change status
   - Get active edition
   - Duplicate prevention

3. **Validator Tests**
   - `CreateCampEditionValidator`
   - `UpdateCampEditionValidator`
   - `ChangeStatusValidator`

### Integration Tests Required

1. **Endpoint Tests** (`CampEditionsEndpointsTests.cs`)
   - All CRUD operations
   - Authorization checks
   - Status workflow via API

---

## File Structure

```
src/Abuvi.API/Features/Camps/
├── CampEditionsService.cs          # ⚠️ Needs completion
├── CampEditionsRepository.cs       # ⚠️ May need new methods
├── ICampEditionsRepository.cs      # ⚠️ May need new methods
├── CampsEndpoints.cs               # ⚠️ Add new endpoints
├── CampEditionsModels.cs           # ⚠️ Add new DTOs
├── Validators/
│   ├── ProposeCampEditionValidator.cs  # ✅ Exists
│   ├── CreateCampEditionValidator.cs   # ❌ Needed
│   ├── UpdateCampEditionValidator.cs   # ❌ Needed
│   └── ChangeStatusValidator.cs        # ❌ Needed

tests/Abuvi.Tests/Unit/Features/Camps/
├── CampEditionsServiceTests.cs     # ⚠️ Needs more tests
├── StatusTransitionTests.cs        # ❌ New file needed
└── Validators/
    ├── CreateCampEditionValidatorTests.cs   # ❌ Needed
    ├── UpdateCampEditionValidatorTests.cs   # ❌ Needed
    └── ChangeStatusValidatorTests.cs        # ❌ Needed

tests/Abuvi.Tests/Integration/Features/Camps/
└── CampEditionsEndpointsTests.cs   # ❌ Needed
```

---

## Development Tasks

### Sprint 1: Complete Status Machine (2-3 days)

1. [ ] Create `ChangeStatusValidator.cs`
2. [ ] Implement `ChangeStatusAsync` method in service
3. [ ] Add status transition tests (`StatusTransitionTests.cs`)
4. [ ] Add date constraint validation tests
5. [ ] Create `PATCH /api/camps/editions/{id}/status` endpoint
6. [ ] Integration tests for status changes

### Sprint 2: CRUD Operations (2-3 days)

1. [ ] Create `CreateCampEditionValidator.cs`
2. [ ] Create `UpdateCampEditionValidator.cs`
3. [ ] Implement `UpdateAsync` with status-based restrictions
4. [ ] Add duplicate edition prevention
5. [ ] Create `PUT /api/camps/editions/{id}` endpoint
6. [ ] Create `GET /api/camps/editions` endpoint (list)
7. [ ] Create `GET /api/camps/editions/{id}` endpoint
8. [ ] Integration tests for CRUD

### Sprint 3: Active Edition & Polish (1-2 days)

1. [ ] Implement `GetActiveEditionAsync`
2. [ ] Create `GET /api/camps/editions/active` endpoint
3. [ ] Add registration statistics to response
4. [ ] Performance optimization for queries
5. [ ] Complete API documentation
6. [ ] Final integration testing

---

## Success Metrics

- [ ] All 15+ status transition scenarios tested
- [ ] 95%+ code coverage
- [ ] All endpoints documented with examples
- [ ] Zero failing tests
- [ ] Manual E2E testing completed
- [ ] Performance: Active edition query < 100ms

---

## Related User Stories

### Phase 3 (Completed)

- **US-CAMP-000**: Propose Camp Candidate ✅
- **US-CAMP-001**: List Proposed Candidates ✅
- **US-CAMP-002**: Promote Candidate to Draft ✅
- **US-CAMP-003**: Reject Candidate ✅

### Phase 4 (Pending)

- **US-CAMP-201**: Create and Manage Camp Editions
- **US-CAMP-202**: Open/Close Registration Periods
- **US-CAMP-204**: View Active Camp Edition

---

## Notes

- The database schema is already in place via the `AddCampsAndSettings` migration
- The proposal workflow (Phase 3) is functional and tested
- Focus should be on completing the status machine and CRUD operations
- Integration with registration system will come later (separate spec)

---

## Document Control

- **Version**: 1.0
- **Created**: 2026-02-16
- **Status**: 🔶 Partial Implementation
- **Parent Spec**: [camp-crud-enriched.md](../feat-camps-definition/camp-crud-enriched.md)
