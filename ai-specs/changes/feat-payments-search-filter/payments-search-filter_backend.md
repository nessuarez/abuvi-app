# Backend Implementation Plan: feat-payments-search-filter — Payments Admin Search Filter

## Overview

Add a server-side text search filter to the admin payments list endpoint (`GET /api/admin/payments`) so that admin/board users can find payments by family name or representative name. No schema migration is required — the change is purely a new optional query parameter + repository query expansion.

Architecture: **Vertical Slice Architecture** — all changes are contained in `src/Abuvi.API/Features/Payments/`.

---

## Architecture Context

- **Feature slice**: `src/Abuvi.API/Features/Payments/`
- **Files modified** (no new files):
  - `PaymentsModels.cs` — add `Search` field to `PaymentFilterRequest`
  - `PaymentsRepository.cs` — rewrite `GetFilteredAsync` to join `Users` and apply search predicate
  - `src/Abuvi.Tests/Unit/Features/Payments/PaymentsRepository_FilterTests.cs` — add 4 new test cases + update seed
- **Files NOT modified**: `IPaymentsRepository.cs`, `PaymentsService.cs`, `PaymentsEndpoints.cs`, `PaymentsValidators.cs`
- **No EF Core migration** needed — no schema changes.

---

## Implementation Steps

### Step 0: Create Feature Branch

- **Action**: Create and switch to a new feature branch.
- **Branch name**: `feature/feat-payments-search-filter-backend`
- **Steps**:
  1. `git checkout dev`
  2. `git pull origin dev`
  3. `git checkout -b feature/feat-payments-search-filter-backend`
  4. `git branch` — verify you are on the new branch

---

### Step 1: Add `Search` to `PaymentFilterRequest`

- **File**: `src/Abuvi.API/Features/Payments/PaymentsModels.cs`
- **Action**: Add the optional `Search` positional parameter to the record.
- **Current record**:
  ```csharp
  public record PaymentFilterRequest(
      PaymentStatus? Status = null,
      Guid? CampEditionId = null,
      int? InstallmentNumber = null,
      DateTime? FromDate = null,
      DateTime? ToDate = null,
      int Page = 1,
      int PageSize = 20
  );
  ```
- **New record**:
  ```csharp
  public record PaymentFilterRequest(
      PaymentStatus? Status = null,
      Guid? CampEditionId = null,
      int? InstallmentNumber = null,
      DateTime? FromDate = null,
      DateTime? ToDate = null,
      string? Search = null,
      int Page = 1,
      int PageSize = 20
  );
  ```
- **Notes**:
  - `[AsParameters]` binding in the endpoint maps query string `?Search=term` automatically.
  - `IPaymentsRepository` signature is unchanged — the record is passed by value, the interface just sees `PaymentFilterRequest`.

---

### Step 2: Rewrite `GetFilteredAsync` in `PaymentsRepository`

- **File**: `src/Abuvi.API/Features/Payments/PaymentsRepository.cs`
- **Action**: Replace the `.Include`-based query with a LINQ join query that exposes `FamilyUnit.Name` and `User.FirstName/LastName` for filtering. Project back to `Payment` for the final result to keep the return type unchanged.

- **Implementation Steps**:

  1. Change the query from `.Include(...)` to a LINQ join syntax, projecting into an anonymous type:
     ```csharp
     var query = from p in db.Payments.AsNoTracking()
                 join r in db.Registrations on p.RegistrationId equals r.Id
                 join fu in db.FamilyUnits on r.FamilyUnitId equals fu.Id
                 join u in db.Users on r.RegisteredByUserId equals u.Id
                 select new
                 {
                     Payment = p,
                     FamilyName = fu.Name,
                     RepresentativeName = u.FirstName + " " + u.LastName,
                     CampEditionId = r.CampEditionId
                 };
     ```

  2. Apply existing filters (status, campEditionId, installment, dates) via `.Where()` on `x.Payment.*` fields:
     ```csharp
     if (filter.Status.HasValue)
         query = query.Where(x => x.Payment.Status == filter.Status.Value);

     if (filter.CampEditionId.HasValue)
         query = query.Where(x => x.CampEditionId == filter.CampEditionId.Value);

     if (filter.InstallmentNumber.HasValue)
     {
         if (filter.InstallmentNumber.Value >= 3)
             query = query.Where(x => x.Payment.InstallmentNumber >= 3);
         else
             query = query.Where(x => x.Payment.InstallmentNumber == filter.InstallmentNumber.Value);
     }

     if (filter.FromDate.HasValue)
         query = query.Where(x => x.Payment.CreatedAt >= filter.FromDate.Value);

     if (filter.ToDate.HasValue)
         query = query.Where(x => x.Payment.CreatedAt <= filter.ToDate.Value);
     ```

  3. Apply the new search filter (after existing filters, before count):
     ```csharp
     if (!string.IsNullOrWhiteSpace(filter.Search))
     {
         var term = filter.Search.Trim().ToLower();
         query = query.Where(x =>
             x.FamilyName.ToLower().Contains(term) ||
             x.RepresentativeName.ToLower().Contains(term));
     }
     ```

  4. Count and paginate, then project back to `Payment`:
     ```csharp
     var totalCount = await query.CountAsync(ct);

     var items = await query
         .OrderByDescending(x => x.Payment.CreatedAt)
         .Skip((filter.Page - 1) * filter.PageSize)
         .Take(filter.PageSize)
         .Select(x => x.Payment)
         .ToListAsync(ct);

     return (items, totalCount);
     ```

- **Important notes**:
  - The `Payment` entity loaded by this query **will not have navigation properties populated** (`Registration`, etc.) because we switched from `.Include` to a projection. Check `PaymentsService.cs` — if `GetAllPaymentsAsync` uses any navigation property from the returned `Payment` objects after calling `GetFilteredAsync`, those fields must be re-included or the projection must be extended.
  - Look at how `PaymentsService.MapToAdminResponse` (or equivalent) builds `AdminPaymentResponse` — it needs `FamilyUnitName` and `CampEditionName`. These are currently sourced from the `.Include` navigations. After the rewrite you have two options:
    - **Option A (preferred)**: Extend the anonymous projection to also include `CampEditionName` and `CampName`, pass them through, and return them alongside the `Payment` — but this changes the `(List<Payment> Items, int TotalCount)` return type, requiring an interface change.
    - **Option B (simpler, no interface change)**: Keep returning `List<Payment>` but also include the navigation properties in the projection's `Payment` via EF's owned-entity loading. Use `.Include` inside the join via a subquery. This is less clean.
    - **Option C (recommended given current code)**: Look at how `PaymentsService` maps `Payment` → `AdminPaymentResponse`. If it uses `.Registration.FamilyUnit.Name` and `.Registration.CampEdition.Camp.Name`, these will be null after the rewrite. The cleanest solution without changing the interface is to use a split query:
      ```csharp
      // Keep the join only for filtering, but load the full entity with includes for the paginated result set
      var pagedIds = await query
          .OrderByDescending(x => x.Payment.CreatedAt)
          .Skip((filter.Page - 1) * filter.PageSize)
          .Take(filter.PageSize)
          .Select(x => x.Payment.Id)
          .ToListAsync(ct);

      var items = await db.Payments
          .Include(p => p.Registration)
              .ThenInclude(r => r.FamilyUnit)
          .Include(p => p.Registration)
              .ThenInclude(r => r.CampEdition)
                  .ThenInclude(ce => ce.Camp)
          .Where(p => pagedIds.Contains(p.Id))
          .AsNoTracking()
          .ToListAsync(ct);
      ```
      This is two queries but guarantees navigation properties are populated.

  - **Verify which option applies** by reading `PaymentsService.cs` `GetAllPaymentsAsync` method to see how it uses the returned `Payment` objects.

---

### Step 3: Update Unit Tests

- **File**: `src/Abuvi.Tests/Unit/Features/Payments/PaymentsRepository_FilterTests.cs`
- **Action**: Update the seed to include a `User` entity (the join now requires `Users` to exist), and add 4 new test cases.

- **Update `SeedBaseEntities()`**:
  - Add a static `UserId` field:
    ```csharp
    private static readonly Guid UserId = Guid.NewGuid();
    ```
  - Seed a `User` entity:
    ```csharp
    var user = new User
    {
        Id = UserId,
        Email = "test@example.com",
        PasswordHash = "hash",
        FirstName = "Maria",
        LastName = "Garcia",
        Role = UserRole.Parent
    };
    _context.Users.Add(user);
    ```
  - Update the `Registration` seed to use `RegisteredByUserId = UserId` (instead of `Guid.NewGuid()`).
  - Update `FamilyUnit` seed `Name` to `"Garcia Family"` (to test family name search).

- **New test cases** (add after the existing tests):

  ```csharp
  [Fact]
  public async Task GetFilteredAsync_WhenSearchMatchesFamilyName_ReturnsMatchingPayments()
  {
      // Arrange
      _context.Payments.Add(CreatePayment(1));
      await _context.SaveChangesAsync();
      var filter = new PaymentFilterRequest(Search: "garcia");

      // Act
      var (items, totalCount) = await _repository.GetFilteredAsync(filter, CancellationToken.None);

      // Assert
      totalCount.Should().Be(1);
      items.Should().ContainSingle();
  }

  [Fact]
  public async Task GetFilteredAsync_WhenSearchMatchesRepresentativeName_ReturnsMatchingPayments()
  {
      // Arrange
      _context.Payments.Add(CreatePayment(1));
      await _context.SaveChangesAsync();
      var filter = new PaymentFilterRequest(Search: "maria");

      // Act
      var (items, totalCount) = await _repository.GetFilteredAsync(filter, CancellationToken.None);

      // Assert
      totalCount.Should().Be(1);
      items.Should().ContainSingle();
  }

  [Fact]
  public async Task GetFilteredAsync_WhenSearchMatchesNoOne_ReturnsEmpty()
  {
      // Arrange
      _context.Payments.Add(CreatePayment(1));
      await _context.SaveChangesAsync();
      var filter = new PaymentFilterRequest(Search: "zzznomatch");

      // Act
      var (items, totalCount) = await _repository.GetFilteredAsync(filter, CancellationToken.None);

      // Assert
      totalCount.Should().Be(0);
      items.Should().BeEmpty();
  }

  [Fact]
  public async Task GetFilteredAsync_WhenSearchCombinedWithStatus_AppliesBothFilters()
  {
      // Arrange
      _context.Payments.AddRange(
          CreatePayment(1, PaymentStatus.Completed),
          CreatePayment(2, PaymentStatus.Pending));
      await _context.SaveChangesAsync();
      var filter = new PaymentFilterRequest(
          Status: PaymentStatus.Completed,
          Search: "garcia");

      // Act
      var (items, totalCount) = await _repository.GetFilteredAsync(filter, CancellationToken.None);

      // Assert
      totalCount.Should().Be(1);
      items.Should().ContainSingle()
          .Which.Status.Should().Be(PaymentStatus.Completed);
  }
  ```

- **Note on InMemory EF**: The `EF.Functions.Like` / `.ToLower().Contains()` pattern is supported by the EF InMemory provider for these tests.

---

### Step 4: Update Technical Documentation

- **File**: `ai-specs/specs/api-endpoints.md`
  - Find the `GET /admin/payments` entry and add `Search` to the query parameters table.
- **No data model changes** → `data-model.md` unchanged.

---

## Implementation Order

1. Step 0 — Create feature branch
2. Step 1 — Add `Search` to `PaymentFilterRequest`
3. Step 2 — Rewrite `GetFilteredAsync` (verify navigation property usage in service first)
4. Step 3 — Update tests (update seed + add 4 test cases)
5. Step 4 — Update API docs

---

## Testing Checklist

- [ ] All existing `PaymentsRepository_FilterTests` tests still pass
- [ ] 4 new search filter tests pass
- [ ] `dotnet test` exits 0
- [ ] Manual test: `GET /api/admin/payments?Search=garcia` returns only payments for family "Garcia Family"
- [ ] Manual test: search is case-insensitive (`search=GARCIA` same result)
- [ ] Manual test: search combined with `Status=Pending` returns correct intersection
- [ ] Manual test: empty search returns all payments (same as before)

---

## Error Response Format

No new error cases — `Search` is optional and defaults to `null` (no filtering). The existing `ApiResponse<T>` envelope is unchanged.

---

## Dependencies

- No new NuGet packages
- No EF Core migration

---

## Notes

- **Critical**: Before implementing Step 2, read `PaymentsService.cs` → `GetAllPaymentsAsync` to understand how `Payment` navigation properties are used when mapping to `AdminPaymentResponse`. Choose Option C (split query) if `Registration.FamilyUnit.Name` is accessed on the returned entities.
- **No validator change needed**: `Search` is a free-text filter; no length constraint is required (the DB query is parameterized, no injection risk). If a max-length rule is desired, add a `PaymentFilterRequestValidator` — but this is not required.
- **`IPaymentsRepository` unchanged**: The `PaymentFilterRequest` record gains a new property with a default value (`null`), which is backwards-compatible.
- All code and documentation must be in English.

---

## Next Steps After Implementation

- Frontend ticket: `feat-payments-search-filter-frontend` — adds the `InputText` search box to `PaymentsAllList.vue` and sends `Search` param.

---

## Implementation Verification

- [ ] **Code Quality**: No compiler warnings, nullable reference types respected
- [ ] **Functionality**: `GET /api/admin/payments?Search=term` returns filtered results; without `Search` behavior is unchanged
- [ ] **Testing**: All existing + new tests pass; coverage maintained
- [ ] **No migration**: `dotnet ef migrations list` shows no pending migrations
- [ ] **Documentation**: `api-endpoints.md` updated with `Search` parameter
