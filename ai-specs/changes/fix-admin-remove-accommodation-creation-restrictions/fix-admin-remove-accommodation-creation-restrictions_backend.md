# Backend Implementation Plan: fix-admin-remove-accommodation-creation-restrictions

## Overview

Remove the `Closed` edition-status guard from accommodation and extra **creation** in the backend services. Room assignment happens after registrations close, so a `Closed` edition must be editable for accommodations/extras. Only `Completed` editions remain immutable.

This is a surgical two-line change across two service files, plus targeted test updates.  
No new endpoints, no migrations, no schema changes.

Architecture: **Vertical Slice** — both changes live inside `src/Abuvi.API/Features/Camps/`.

---

## Architecture Context

**Feature slice:** `src/Abuvi.API/Features/Camps/`

**Files to modify:**

| File | Change |
|---|---|
| `CampEditionAccommodationsService.cs` | Remove `CampEditionStatus.Closed` from status guard in `CreateAsync` |
| `CampEditionExtrasService.cs` | Same removal in `CreateAsync` |
| `src/Abuvi.Tests/Unit/Features/Camps/CampEditionAccommodationsServiceTests.cs` | Add success test for `Closed` edition; add/fix `Completed` guard test |
| `src/Abuvi.Tests/Unit/Features/Camps/CampEditionExtrasServiceTests.cs` | Fix existing `Closed` guard test (invert — should now succeed); fix `Completed` guard test message |

**Files NOT to modify:**
- `CampEditionsService.UpdateAsync` — its broader guard on `Closed` (for general edition edits: dates, prices, notes) is intentional and out of scope.
- `AccommodationZonesService` — no status guard exists there; no change needed.
- Any endpoints file — no endpoint-level guard applies here.

---

## Implementation Steps

### Step 0: Create Feature Branch

- **Action**: Create branch from `dev`
- **Branch name**: `feature/fix-admin-remove-accommodation-creation-restrictions-backend`
- **Steps**:
  1. `git checkout dev && git pull origin dev`
  2. `git checkout -b feature/fix-admin-remove-accommodation-creation-restrictions-backend`
  3. `git branch` — verify you are on the new branch

---

### Step 1: Fix `CampEditionAccommodationsService.CreateAsync`

- **File**: `src/Abuvi.API/Features/Camps/CampEditionAccommodationsService.cs`
- **Action**: Remove `CampEditionStatus.Closed` from the status guard (lines 44–46)
- **Before**:
  ```csharp
  if (edition.Status is CampEditionStatus.Completed or CampEditionStatus.Closed)
      throw new InvalidOperationException(
          "No se pueden añadir alojamientos a una edición cerrada o completada");
  ```
- **After**:
  ```csharp
  if (edition.Status is CampEditionStatus.Completed)
      throw new InvalidOperationException(
          "No se pueden añadir alojamientos a una edición completada");
  ```
- **Implementation Notes**:
  - Do not change any other logic in this method.
  - Error message stays in Spanish (project convention).

---

### Step 2: Fix `CampEditionExtrasService.CreateAsync`

- **File**: `src/Abuvi.API/Features/Camps/CampEditionExtrasService.cs`
- **Action**: Remove `CampEditionStatus.Closed` from the status guard (lines 44–46)
- **Before**:
  ```csharp
  if (edition.Status is CampEditionStatus.Completed or CampEditionStatus.Closed)
      throw new InvalidOperationException(
          "No se pueden añadir extras a una edición cerrada o completada");
  ```
- **After**:
  ```csharp
  if (edition.Status is CampEditionStatus.Completed)
      throw new InvalidOperationException(
          "No se pueden añadir extras a una edición completada");
  ```

---

### Step 3: Update `CampEditionAccommodationsServiceTests`

- **File**: `src/Abuvi.Tests/Unit/Features/Camps/CampEditionAccommodationsServiceTests.cs`
- **Action**: Add two new tests to cover the status-guard behavior after the fix

There are currently no status-guard tests for the accommodations service. Add them inside the `#region CreateAsync` section (or after the existing `CreateAsync` tests):

```csharp
[Fact]
public async Task CreateAsync_WhenEditionIsCompleted_ThrowsInvalidOperationException()
{
    // Arrange
    var edition = MakeEdition();
    edition.Status = CampEditionStatus.Completed;
    _editionsRepository.GetByIdAsync(edition.Id, Arg.Any<CancellationToken>()).Returns(edition);

    var request = new CreateCampEditionAccommodationRequest(
        "Habitación", AccommodationType.Lodge, null, 1);

    // Act
    var act = () => _sut.CreateAsync(edition.Id, request, CancellationToken.None);

    // Assert
    await act.Should().ThrowAsync<InvalidOperationException>()
        .WithMessage("*completada*");
}

[Fact]
public async Task CreateAsync_WhenEditionIsClosed_CreatesAccommodationSuccessfully()
{
    // Arrange
    var edition = MakeEdition();
    edition.Status = CampEditionStatus.Closed;
    _editionsRepository.GetByIdAsync(edition.Id, Arg.Any<CancellationToken>()).Returns(edition);
    _repository.GetPreferenceCountAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(0);
    _repository.GetFirstChoiceCountAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(0);

    var request = new CreateCampEditionAccommodationRequest(
        "Cabaña C1", AccommodationType.Lodge, null, 1);

    // Act
    var result = await _sut.CreateAsync(edition.Id, request, CancellationToken.None);

    // Assert
    result.Should().NotBeNull();
    result.Name.Should().Be("Cabaña C1");
    await _repository.Received(1).AddAsync(Arg.Any<CampEditionAccommodation>(), Arg.Any<CancellationToken>());
}
```

- **Implementation Note**: `MakeEdition()` currently creates a `Draft` edition. The tests above mutate `.Status` after construction to keep the helper minimal.

---

### Step 4: Update `CampEditionExtrasServiceTests`

- **File**: `src/Abuvi.Tests/Unit/Features/Camps/CampEditionExtrasServiceTests.cs`
- **Action**: Fix two existing tests that must change behaviour after the guard is removed

#### Test to invert: `CreateAsync_WhenEditionIsClosed_ThrowsInvalidOperationException` (lines 131–146)

This test currently asserts that creating an extra on a `Closed` edition throws. After the fix it must succeed. Replace it entirely:

```csharp
[Fact]
public async Task CreateAsync_WhenEditionIsClosed_CreatesExtraSuccessfully()
{
    // Arrange
    var edition = MakeEdition(CampEditionStatus.Closed);
    _editionsRepository.GetByIdAsync(edition.Id, Arg.Any<CancellationToken>())
        .Returns(edition);

    var request = new CreateCampEditionExtraRequest("Name", null, 10m,
        PricingType.PerPerson, PricingPeriod.OneTime, false, null);

    // Act
    var result = await _sut.CreateAsync(edition.Id, request);

    // Assert
    result.Should().NotBeNull();
    result.Name.Should().Be("Name");
    await _repository.Received(1).AddAsync(Arg.Any<CampEditionExtra>(), Arg.Any<CancellationToken>());
}
```

#### Test to update: `CreateAsync_WhenEditionIsCompleted_ThrowsInvalidOperationException` (lines 112–128)

The guard message now reads `"*completada*"` (not `"*cerrada o completada*"`). Update the assertion:

```csharp
// Before
await act.Should().ThrowAsync<InvalidOperationException>()
    .WithMessage("*cerrada o completada*");

// After
await act.Should().ThrowAsync<InvalidOperationException>()
    .WithMessage("*completada*");
```

---

### Step 5: Run Tests

```bash
dotnet test src/Abuvi.Tests --filter "FullyQualifiedName~CampEditionAccommodationsServiceTests|FullyQualifiedName~CampEditionExtrasServiceTests"
```

All tests must pass (green). Then run the full suite to check for regressions:

```bash
dotnet test src/Abuvi.Tests
```

---

### Step 6: Update Technical Documentation

- **File**: `ai-specs/specs/api-spec.yml` — no endpoint changes, no update needed.
- **File**: `ai-specs/specs/data-model.md` — no schema changes, no update needed.
- If a business-rules document references accommodation creation restrictions tied to `Closed` status, update it to reflect that `Closed` editions allow accommodation and extra management.

---

## Implementation Order

1. Step 0 — Create feature branch
2. Step 1 — Fix `CampEditionAccommodationsService.CreateAsync`
3. Step 2 — Fix `CampEditionExtrasService.CreateAsync`
4. Step 3 — Add tests for accommodations service
5. Step 4 — Fix tests for extras service
6. Step 5 — Run tests (full suite must be green)
7. Step 6 — Documentation review

---

## Testing Checklist

- [ ] `CreateAsync_WhenEditionIsCompleted_ThrowsInvalidOperationException` — accommodations (new)
- [ ] `CreateAsync_WhenEditionIsClosed_CreatesAccommodationSuccessfully` — accommodations (new)
- [ ] `CreateAsync_WhenEditionIsClosed_CreatesExtraSuccessfully` — extras (was failing assertion, now passes)
- [ ] `CreateAsync_WhenEditionIsCompleted_ThrowsInvalidOperationException` — extras message updated
- [ ] Full test suite green — no regressions in registrations, payments, or other camp flows

---

## Error Response Format

No new endpoints. Existing error flow for `Completed` guard:
- Service throws `InvalidOperationException("No se pueden añadir alojamientos a una edición completada")`
- Global `ExceptionMiddleware` catches it and returns `422 Unprocessable Entity` with `ApiResponse` error

---

## Dependencies

- No new NuGet packages
- No EF Core migrations

---

## Notes

- Error messages remain in **Spanish** (project convention: user-facing messages in Spanish, logs in English).
- The guard on `CampEditionsService.UpdateAsync` (line 230–232) blocks general edition edits for `Closed` editions and is **intentionally left unchanged**.
- `AccommodationZonesService` has no status guard — no change required there.
- The fix is purely in the service layer; no endpoint-level changes are needed.

---

## Next Steps After Implementation

1. Open a PR targeting `dev`.
2. Reference the enriched spec: `ai-specs/changes/fix-admin-remove-accommodation-creation-restrictions/fix-admin-remove-accommodation-creation-restrictions_enriched.md`.
3. The frontend fix is a separate task — see the enriched spec for frontend scope (`CampEditionDetailPage.vue` and `CampEditionExtrasList.vue`).

---

## Implementation Verification

- [ ] **Code Quality**: No compiler warnings; nullable references enabled (`TreatWarningsAsErrors` is on)
- [ ] **Functionality**: `POST /camps/editions/{id}/accommodations` returns 201 for a `Closed` edition
- [ ] **Functionality**: `POST /camps/editions/{id}/extras` returns 201 for a `Closed` edition
- [ ] **Functionality**: Both endpoints still return 422 for a `Completed` edition
- [ ] **Testing**: All new/updated tests pass; full suite green
- [ ] **No migrations**: Confirm `dotnet ef migrations list` shows no pending migrations
