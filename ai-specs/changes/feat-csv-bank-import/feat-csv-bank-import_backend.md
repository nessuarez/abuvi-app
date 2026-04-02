# Backend Implementation Plan: feat-csv-bank-import Banc Sabadell Norma43 Import

## Overview

This plan implements the backend for reconciling Banc Sabadell Norma43 bank statement exports with SEPA CORE direct debit transactions to pending membership fees. The feature follows Vertical Slice Architecture, encapsulating all membership fee import logic within the Memberships feature slice.

**Key Architecture Principles:**

- Vertical Slice: All imports logic in `Features/Memberships/`
- Separation of Concerns: Service handles business logic, Repository handles data, Endpoints handle HTTP
- Error Handling: BusinessRuleException for domain errors, standard HTTP status codes
- Async-first: All I/O operations async with CancellationToken support

---

## Architecture Context

### Feature Slice Location

`src/Abuvi.API/Features/Memberships/`

### Files to Create/Modify

**Create:**

- `CsvImportModels.cs` — DTOs, enums, internal records for Norma43 parsing
- `CsvImportService.cs` — Norma43 parsing, fuzzy matching, bulk confirmation logic

**Modify:**

- `MembershipsRepository.cs` — Add `GetPendingFeesForMatchingAsync` query
- `MembershipsEndpoints.cs` — Add CSV import endpoints (`/parse`, `/confirm`)
- `Program.cs` — Register service, map endpoints

**No schema changes** — leverages existing `MembershipFee`, `Membership`, `FamilyUnit` entities.

### Cross-Cutting Concerns

- **Authorization**: Board/Admin role check on both endpoints
- **CSRF Protection**: Parse endpoint uses `.DisableAntiforgery()` (multipart/form-data)
- **Error Handling**: `BusinessRuleException` for invalid files; standard `ApiResponse<T>` envelope
- **Logging**: Errors in bulk confirm logged per fee
- **Input Validation**: File format validation in service (no FluentValidation for multipart)

---

## Implementation Steps

### **Step 0: Create Feature Branch**

**Action**: Create and switch to a new feature branch for backend implementation only

**Branch Naming**: `feature/feat-csv-bank-import-backend` (separate from frontend work)

**Implementation Steps**:

1. Ensure you're on the latest `dev` branch (main branch for feature PRs)
2. Pull latest changes: `git pull origin dev`
3. Create new branch: `git checkout -b feature/feat-csv-bank-import-backend`
4. Verify branch creation: `git branch`

**Notes**:

- Follow workflow: feature branch → dev → main (hotfix only)
- Commit message format: `feat(memberships): [description]`

---

### **Step 1: Create CsvImportModels.cs with DTOs and Enums**

**File**: `src/Abuvi.API/Features/Memberships/CsvImportModels.cs` (NEW)

**Action**: Define all request/response DTOs and internal models for Norma43 import feature

**Contents**:

```csharp
namespace Abuvi.API.Features.Memberships;

// Norma43 column mapping (kept for API compatibility, ignored for Norma43)
public record CsvColumnMapping(
    string SenderNameColumn = "",
    string AmountColumn = "",
    string DateColumn = "",
    string? DescriptionColumn = null
);

// Match confidence level
public enum MatchConfidence { High, Medium, Low, None }

// Parse response: one Norma43 debit transaction + matched fee
public record CsvMatchResult(
    int RowIndex,
    string RawTransactionReference,
    string RawConceptLines,
    decimal Amount,
    DateOnly ValueDate,
    string? TransactionType,
    Guid? FeeId,
    Guid? MembershipId,
    string? FamilyUnitName,
    string? MemberName,
    decimal? FeeAmount,
    MatchConfidence Confidence
);

// Confirmed match (user-selected after review)
public record CsvConfirmItem(
    int RowIndex,
    Guid FeeId,
    Guid MembershipId,
    DateOnly PaidDate,
    string? PaymentReference
);

// Bulk confirm request
public record CsvBulkConfirmRequest(
    IReadOnlyList<CsvConfirmItem> Items
);

// Per-item result
public record CsvConfirmItemResult(
    int RowIndex,
    bool Success,
    string? Error
);

// Aggregate result
public record CsvBulkConfirmResult(
    int Confirmed,
    int Failed,
    IReadOnlyList<CsvConfirmItemResult> Results
);

// Internal: pending fee for matching
public record PendingFeeForMatching(
    Guid FeeId,
    Guid MembershipId,
    string FamilyUnitName,
    string MemberName,
    decimal Amount
);

// Internal: parsed Norma43 debit transaction
public class Norma43DebitTransaction
{
    public string TransactionReference { get; set; } = "";
    public string TransactionType { get; set; } = "05";
    public DateOnly ValueDate { get; set; }
    public decimal Amount { get; set; }
    public string ConceptLines { get; set; } = "";
}
```

**Dependencies**: Standard .NET namespaces only

**Implementation Notes**:

- All records are public (used in API responses)
- `Norma43DebitTransaction` is internal class (mutable for parsing)
- `PendingFeeForMatching` is internal record (projection from DB)
- No validation attributes (validation in service, not DTOs)

---

### **Step 2: Add GetPendingFeesForMatchingAsync to MembershipsRepository**

**File**: `src/Abuvi.API/Features/Memberships/MembershipsRepository.cs`

**Action**: Add repository method to load all pending membership fees for a given year

**Method Signature**:

```csharp
Task<IReadOnlyList<PendingFeeForMatching>> GetPendingFeesForMatchingAsync(int year, CancellationToken ct);
```

**Interface Addition** (to `IMembershipsRepository`):

Add after line 28 (after `GetFeeByYearAsync`):

```csharp
// CSV Import support
Task<IReadOnlyList<PendingFeeForMatching>> GetPendingFeesForMatchingAsync(int year, CancellationToken ct);
```

**Implementation** (to `MembershipsRepository` class):

```csharp
public async Task<IReadOnlyList<PendingFeeForMatching>> GetPendingFeesForMatchingAsync(int year, CancellationToken ct)
    => await db.MembershipFees
        .AsNoTracking()
        .Where(f => f.Year == year && f.Status == FeeStatus.Pending && f.Membership.IsActive)
        .Select(f => new PendingFeeForMatching(
            f.Id,
            f.MembershipId,
            f.Membership.FamilyMember.FamilyUnit.Name,
            f.Membership.FamilyMember.FirstName + " " + f.Membership.FamilyMember.LastName,
            f.Amount
        ))
        .ToListAsync(ct);
```

**Dependencies**:

- `using Microsoft.EntityFrameworkCore;` (already present)
- `using Abuvi.API.Data;` (already present)

**Implementation Notes**:

- Filters: year match, status = Pending, membership active
- EF Core projection avoids N+1 (select specific fields only)
- AsNoTracking() for read-only query (no updates)
- Returns readonly list for immutability

---

### **Step 3: Create CsvImportService.cs with Norma43 Parsing and Matching**

**File**: `src/Abuvi.API/Features/Memberships/CsvImportService.cs` (NEW)

**Action**: Implement Norma43 parsing, fuzzy matching, and bulk confirmation logic

**Public Methods**:

```csharp
public class CsvImportService(IMembershipsRepository repository, IMembershipsService membershipsService)
{
    public async Task<IReadOnlyList<CsvMatchResult>> ParseAndMatchAsync(
        IFormFile norma43File,
        CsvColumnMapping mapping,  // Ignored for Norma43 (fixed format)
        CancellationToken ct);

    public async Task<CsvBulkConfirmResult> BulkConfirmAsync(
        CsvBulkConfirmRequest request,
        CancellationToken ct);
}
```

**Implementation Steps**:

1. **ParseAndMatchAsync Method**:
   - Validate file is not null/empty → throw `BusinessRuleException`
   - Call `ParseNorma43DebitTransactionsAsync()` to extract debits
   - Load pending fees via `repository.GetPendingFeesForMatchingAsync(currentYear, ct)`
   - For each debit: call `BestMatchByAmountAndConcept()` to find fee match
   - Build `CsvMatchResult` with match details and confidence level
   - Return list of results

2. **BulkConfirmAsync Method**:
   - For each item in request.Items:
     - Validate fee exists and is Pending
     - Call `membershipsService.PayFeeAsync()` with payment reference
     - Catch exceptions and add error to results
   - Return `CsvBulkConfirmResult` with confirmed/failed counts

3. **Helper: ParseNorma43DebitTransactionsAsync**:
   - Open file stream with ISO-8859-1 encoding (Norma43 standard)
   - Read line by line
   - If line starts with "2": parse Record Type 2 (transaction detail)
     - Extract fields: txn type (pos 10-11), value date (pos 12-17 YYMMDD), amount (pos 22-33), bank ref, payer ref
     - Filter only txn type "05" (SEPA CORE debit)
     - Add to debits list
   - If line starts with "3" and debits exist: parse Record Type 3 (concept line)
     - Extract concept line (pos 12-81, 70 chars)
     - Append to last debit's concept lines with space separator
   - Return list of debits

4. **Helper: BestMatchByAmountAndConcept**:
   - Three-pass algorithm:
     - **Pass 1**: Exact amount match → filter candidates, fuzzy match family name on concept lines (score ≥ 0.50) → return best
     - **Pass 2**: Amount within ±10% tolerance → fuzzy name match (score ≥ 0.50) → return best
     - **Pass 3**: No amount constraint → fuzzy name match (score ≥ 0.70) → return best
   - Return null if no match found

5. **Helper: Fuzzy Matching Functions**:
   - `Normalize(string)`: Remove accents (NFD decomposition), lowercase, keep alphanumeric + spaces
   - `Score(string a, string b)`: Substring containment (0.85) or Levenshtein ratio (1 - distance/maxLen)
   - `LevenshteinDistance(string a, string b)`: Dynamic programming implementation
   - `ConfidenceFor(match, conceptLines)`: Score ≥ 0.90 = High, ≥ 0.70 = Medium, ≥ 0.50 = Low, else None

6. **Helper: TryParseNorma43Date**:
   - Parse YYMMDD format (6 chars)
   - Convert 2-digit year: ≤ 50 → 2000+YY, > 50 → 1900+YY
   - Return DateOnly or false if invalid

**Dependencies**:

```csharp
using System.Globalization;
using System.Text;
using Abuvi.API.Common.Exceptions;
using Microsoft.AspNetCore.Http;
```

**Implementation Notes**:

- All methods are private except `ParseAndMatchAsync` and `BulkConfirmAsync`
- Norma43 uses ISO-8859-1 (Latin-1) encoding: `Encoding.GetEncoding("iso-8859-1")`
- Amount parsing: Norma43 stores as 12 chars (10 digits + 2 decimals), often negative for debits → take absolute value
- Date parsing: Handle both 20xx and 19xx years based on 2-digit YY
- Fuzzy matching: Balance precision with tolerance (10% for amount, 0.50+ score for name)
- Error handling: Log exceptions in bulk confirm, return error per item (not transaction-based)

---

### **Step 4: Create and Register CSV Import Endpoints in MembershipsEndpoints.cs**

**File**: `src/Abuvi.API/Features/Memberships/MembershipsEndpoints.cs`

**Action**: Add new endpoint mapping method with two endpoints

**New Static Method**:

```csharp
public static void MapCsvImportEndpoints(this IEndpointRouteBuilder app)
{
    var group = app.MapGroup("/api/memberships/fees/csv-import")
        .WithTags("Membership Fees - CSV Import")
        .RequireAuthorization();

    group.MapPost("/parse", ParseCsvImport)
        .WithName("ParseCsvImport")
        .WithSummary("Parse and match Norma43 debit transactions to pending membership fees")
        .DisableAntiforgery()  // Multipart form-data
        .Produces<ApiResponse<List<CsvMatchResult>>>()
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status403Forbidden);

    group.MapPost("/confirm", ConfirmCsvImport)
        .WithName("ConfirmCsvImport")
        .WithSummary("Bulk confirm matched Norma43 transactions as paid fees")
        .AddEndpointFilter<ValidationFilter<CsvBulkConfirmRequest>>()
        .Produces<ApiResponse<CsvBulkConfirmResult>>()
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status403Forbidden);
}

private static async Task<IResult> ParseCsvImport(
    HttpContext ctx,
    CsvImportService service,
    CancellationToken ct)
{
    // Auth check
    var userRole = ctx.User.GetUserRole();
    if (userRole != "Admin" && userRole != "Board")
        return Results.Forbid();

    // Extract file and mapping from multipart form
    var file = ctx.Request.Form.Files.FirstOrDefault();
    var mappingJson = ctx.Request.Form["mapping"].FirstOrDefault();

    if (file == null || string.IsNullOrEmpty(mappingJson))
        return Results.BadRequest(ApiResponse<object>.Error("Missing file or mapping parameter."));

    try
    {
        var mapping = System.Text.Json.JsonSerializer.Deserialize<CsvColumnMapping>(mappingJson)
            ?? new CsvColumnMapping();

        var matches = await service.ParseAndMatchAsync(file, mapping, ct);
        return Results.Ok(ApiResponse<List<CsvMatchResult>>.Ok(matches.ToList()));
    }
    catch (Exception ex) when (ex is Common.Exceptions.BusinessRuleException)
    {
        return Results.BadRequest(ApiResponse<object>.Error(ex.Message));
    }
    catch (Exception ex)
    {
        return Results.BadRequest(ApiResponse<object>.Error($"Failed to parse file: {ex.Message}"));
    }
}

private static async Task<IResult> ConfirmCsvImport(
    CsvBulkConfirmRequest request,
    ClaimsPrincipal user,
    CsvImportService service,
    CancellationToken ct)
{
    // Auth check
    var userRole = user.GetUserRole();
    if (userRole != "Admin" && userRole != "Board")
        return Results.Forbid();

    try
    {
        var result = await service.BulkConfirmAsync(request, ct);
        return Results.Ok(ApiResponse<CsvBulkConfirmResult>.Ok(result));
    }
    catch (Exception ex)
    {
        return Results.InternalServerError(ApiResponse<object>.Error($"Bulk confirm failed: {ex.Message}"));
    }
}
```

**Integration Point**: Call this method from `Program.cs` (see Step 6)

**Implementation Notes**:

- Parse endpoint: multipart form-data, `.DisableAntiforgery()` required
- Confirm endpoint: JSON body, uses `ValidationFilter<CsvBulkConfirmRequest>`
- Both require Board or Admin role (checked via `GetUserRole()` extension)
- Auth check done manually in handler (not declarative) for multipart endpoint

---

### **Step 5: Register Service and Endpoints in Program.cs**

**File**: `src/Abuvi.API/Program.cs`

**Action**: Register service in DI container and map endpoints

**Service Registration** (around line 181, in Memberships section):

```csharp
// Memberships
builder.Services.AddScoped<IMembershipsRepository, MembershipsRepository>();
builder.Services.AddScoped<MembershipsService>();
builder.Services.AddScoped<CsvImportService>();  // <-- Add this line
```

**Endpoint Mapping** (around line 398, after other membership endpoints):

```csharp
app.MapMembershipsEndpoints();
app.MapMembershipAdminEndpoints();
app.MapMembershipFeeEndpoints();
app.MapCsvImportEndpoints();  // <-- Add this line
```

**Implementation Notes**:

- `CsvImportService` registered as `AddScoped` (new instance per request, appropriate for stateless business logic)
- Endpoint mapping must happen after `app.UseAuthentication()` and `app.UseAuthorization()`

---

### **Step 6: Write Unit and Integration Tests**

**File**: `src/Abuvi.API.Tests/Features/Memberships/CsvImportServiceTests.cs` (NEW)

**Action**: Test Norma43 parsing, fuzzy matching, and bulk confirmation

**Test Classes and Cases**:

#### **CsvImportServiceTests**

**Successful Cases:**

- ParseAndMatchAsync: Valid Norma43 file with 3 debits → Returns 3 match results
- ParseAndMatchAsync: Debits match exact amount → Confidence = High
- ParseAndMatchAsync: Debits within ±10% tolerance → Confidence = Medium
- ParseAndMatchAsync: Only fuzzy name match → Confidence = Low
- ParseAndMatchAsync: No match found → Confidence = None
- BulkConfirmAsync: 3 confirmed items, all pending → Returns 3 successes, 0 failures
- BulkConfirmAsync: Mixed success/failure → Returns correct counts and error messages

**Validation Errors:**

- ParseAndMatchAsync: Null file → Throws BusinessRuleException("CSV file is empty.")
- ParseAndMatchAsync: Empty file → Throws BusinessRuleException
- BulkConfirmAsync: Fee not found → Returns error result for that item (no exception)
- BulkConfirmAsync: Fee already paid → Returns error result (not Business Rule exception, just error message)

**Edge Cases:**

- ParseNorma43Date: YY=50 → 2050, YY=51 → 1951 (year boundary)
- Normalize: Accents stripped (é → e, ñ → n)
- Normalize: Special chars removed, spaces preserved
- LevenshteinDistance: Empty strings, identical strings, completely different strings
- Fuzzy matching: Substring "García" in "García López" → 0.85 score
- Amount parsing: Negative values (debits) → Converted to positive
- Concept lines: Up to 3 Record Type 3 lines → Concatenated with spaces

**Integration Tests** (using `WebApplicationFactory`):

- POST `/api/memberships/fees/csv-import/parse`:
  - Valid file → Returns 200 with matches
  - Unauthorized (no token) → Returns 401
  - Insufficient role (User) → Returns 403
  - Invalid file → Returns 400

- POST `/api/memberships/fees/csv-import/confirm`:
  - Valid request → Returns 200 with results
  - Empty items list → Returns 200 with 0 confirmed
  - Fee not found → Returns 200 with failure details

**Test Framework**: xUnit + FluentAssertions + NSubstitute

**Example Test Structure**:

```csharp
[Fact]
public async Task ParseAndMatchAsync_WithValidNorma43File_ReturnsMatchResults()
{
    // Arrange
    var file = CreateMockNorma43File(3); // 3 debit transactions
    var mockRepository = new Mock<IMembershipsRepository>();
    mockRepository
        .Setup(r => r.GetPendingFeesForMatchingAsync(2026, It.IsAny<CancellationToken>()))
        .ReturnsAsync(new List<PendingFeeForMatching>
        {
            new(Guid.NewGuid(), Guid.NewGuid(), "García López", "Juan García", 50m),
            new(Guid.NewGuid(), Guid.NewGuid(), "Martínez García", "Carlos Martínez", 50m),
            new(Guid.NewGuid(), Guid.NewGuid(), "López García", "María López", 75m),
        });

    var service = new CsvImportService(mockRepository.Object, new Mock<IMembershipsService>().Object);

    // Act
    var results = await service.ParseAndMatchAsync(file, new CsvColumnMapping(), CancellationToken.None);

    // Assert
    results.Should().HaveCount(3);
    results[0].Confidence.Should().Be(MatchConfidence.High); // Exact match
    results[2].Confidence.Should().Be(MatchConfidence.Medium); // Fuzzy match
}
```

---

### **Step 7: Create EF Core Migration (if needed)**

**Action**: Check if any schema changes are needed

**Note**: **NO MIGRATION REQUIRED** for this feature.

- Existing tables: `MembershipFees`, `Memberships`, `FamilyUnits`, `FamilyMembers`
- No new columns, no table changes
- Only adding business logic on existing data

**Verification**:

```bash
dotnet ef migrations list
# No new migration needed
```

---

### **Step 8: Update Technical Documentation**

**File**: `docs/api/memberships.md` (or create if doesn't exist)

**Action**: Document new endpoints and Norma43 format

**Documentation Content**:

1. **Endpoint: POST /api/memberships/fees/csv-import/parse**
   - Purpose: Parse Norma43 file and match debits to pending fees
   - Request: multipart/form-data (file + mapping)
   - Response: ApiResponse<List<CsvMatchResult>>
   - Auth: Board/Admin
   - Example request/response

2. **Endpoint: POST /api/memberships/fees/csv-import/confirm**
   - Purpose: Bulk confirm matched debits as paid fees
   - Request: CsvBulkConfirmRequest
   - Response: ApiResponse<CsvBulkConfirmResult>
   - Auth: Board/Admin
   - Example request/response

3. **Norma43 Format Explanation**
   - Record types (0-9)
   - Record Type 2: Transaction detail (date, amount, reference)
   - Record Type 3: Concept lines (family name, mandate reference)
   - Example Norma43 snippet

4. **Matching Algorithm**
   - Three-pass approach (exact amount, ±10% tolerance, fuzzy name)
   - Fuzzy scoring (Levenshtein distance)
   - Confidence levels

---

### **Step 9: Code Quality and Verification**

**Action**: Verify code meets project standards

**Code Quality Checks**:

1. **C# Nullable Reference Types**: All parameters/returns properly annotated
   - `IFormFile norma43File` — non-null (param)
   - `string? PaymentReference` — nullable (DTO field)

2. **Naming Conventions**:
   - Methods: PascalCase (`ParseAndMatchAsync`, `BestMatchByAmountAndConcept`)
   - Private helpers: PascalCase (`Normalize`, `Score`)
   - Records: PascalCase (`CsvMatchResult`, `PendingFeeForMatching`)
   - Variables: camelCase (`norma43File`, `pendingFees`)

3. **Code Analyzer**: Run `dotnet build` and fix any warnings

4. **Test Coverage**: Aim for 90%+ coverage of business logic
   - All matching algorithm paths
   - Error conditions
   - Edge cases (year boundary, empty results)

---

## Implementation Order

1. **Step 0**: Create feature branch (`feature/feat-csv-bank-import-backend`)
2. **Step 1**: Create `CsvImportModels.cs`
3. **Step 2**: Add `GetPendingFeesForMatchingAsync` to repository
4. **Step 3**: Create `CsvImportService.cs`
5. **Step 4**: Add endpoints to `MembershipsEndpoints.cs`
6. **Step 5**: Register service and endpoints in `Program.cs`
7. **Step 6**: Write unit and integration tests
8. **Step 7**: Verify no migration needed
9. **Step 8**: Update technical documentation
10. **Step 9**: Code quality verification

---

## Testing Checklist

### Unit Tests

- [ ] Norma43 parsing: Valid debits extracted correctly
- [ ] Norma43 parsing: Record Type 3 concept lines concatenated
- [ ] Norma43 parsing: Only "05" transactions extracted
- [ ] Norma43 date parsing: YYMMDD → DateOnly conversion
- [ ] String normalization: Accents removed, special chars stripped
- [ ] Fuzzy matching: Substring containment (0.85 score)
- [ ] Fuzzy matching: Levenshtein distance calculation
- [ ] Matching algorithm: Exact amount match prioritized
- [ ] Matching algorithm: ±10% tolerance applied
- [ ] Matching algorithm: Fuzzy name match fallback
- [ ] Confidence levels: High (≥0.90), Medium (≥0.70), Low (≥0.50)
- [ ] Bulk confirm: All items processed (success + failures)
- [ ] Bulk confirm: Exceptions caught per item, not thrown

### Integration Tests

- [ ] POST /parse: Valid file → 200 OK with matches
- [ ] POST /parse: Missing file → 400 Bad Request
- [ ] POST /parse: Unauthorized → 401 Unauthorized
- [ ] POST /parse: Insufficient role → 403 Forbidden
- [ ] POST /confirm: Valid request → 200 OK with results
- [ ] POST /confirm: Fee not found → 200 OK with error per item
- [ ] POST /confirm: Already paid fee → 200 OK with error per item

### End-to-End

- [ ] Parse Banc Sabadell export, review matches
- [ ] Confirm matched debits, verify fees marked Paid
- [ ] Check `PaymentReference` field contains Norma43 reference
- [ ] Re-import same file, verify already-paid fees not duplicated

---

## Error Response Format

**Parse Endpoint Errors:**

```json
// 400 Bad Request - missing file
{
  "success": false,
  "error": {
    "message": "Missing file or mapping parameter."
  }
}

// 400 Bad Request - invalid format
{
  "success": false,
  "error": {
    "message": "Failed to parse file: Invalid Norma43 format."
  }
}

// 403 Forbidden
{
  "success": false,
  "error": {
    "message": "Forbidden"
  }
}
```

**Confirm Endpoint Errors:**

```json
// 400 Bad Request - validation error
{
  "success": false,
  "errors": {
    "items": ["Items cannot be empty."]
  }
}

// 200 OK - with per-item errors (NOT 400)
{
  "success": true,
  "data": {
    "confirmed": 2,
    "failed": 1,
    "results": [
      { "rowIndex": 1, "success": true, "error": null },
      { "rowIndex": 2, "success": false, "error": "Fee is already Paid." },
      { "rowIndex": 3, "success": true, "error": null }
    ]
  }
}
```

---

## Dependencies

### NuGet Packages (already present)

- `Microsoft.EntityFrameworkCore` — EF Core
- `FluentAssertions` — test assertions
- `NSubstitute` — mocking
- `xUnit` — test framework

### No additional NuGet packages required for Norma43 parsing (using only .NET standard library)

### EF Core Migration Commands

```bash
# Not needed for this feature (no schema changes)
```

---

## Notes

### Important Reminders

1. **Norma43 Encoding**: Always parse as ISO-8859-1 (Latin-1), not UTF-8

   ```csharp
   new StreamReader(stream, Encoding.GetEncoding("iso-8859-1"))
   ```

2. **SEPA CORE Debits Only**: Filter Record Type 2 where transaction type = "05"
   - Other types (01=debit, 02=credit, etc.) are non-SEPA and should be ignored

3. **Amount Tolerance**: ±10% is appropriate for rounding differences in SEPA processing
   - Bank may post slightly different amount due to fees or rounding

4. **Concept Lines**: May contain mandate reference, family name, or both
   - Use fuzzy matching to handle variations in family names (accents, formatting)

5. **No Transaction Wrapping**: Bulk confirm returns per-item success/failure
   - Do NOT use database transactions to all-or-nothing confirm
   - Board should see which debits failed and retry later

6. **Audit Trail**: Store Norma43 transaction reference in `PaymentReference` field
   - Enables traceability back to bank statement

### Business Rules

- Only pending fees for current year are matched
- Membership must be active (IsActive = true)
- Already-paid fees are not matched (Status != Pending)
- Fee confirmation calls existing `MembershipsService.PayFeeAsync()` (respects all existing business rules)

### Language Requirements

- Code: English (variable names, comments, error messages)
- All comments should document the "why", not the "what"
- API documentation: English (API spec), Spanish (UI user-facing strings in frontend)

---

## Next Steps After Implementation

1. **Frontend Implementation**: Create Norma43 upload wizard, review table, confirmation flow
2. **Integration Testing**: Test with real Banc Sabadell Norma43 files
3. **User Training**: Document for board members how to export Norma43 and use the import feature
4. **Monitoring**: Log parse/confirm operations for audit trail (consider audit table in future)

---

## Implementation Verification Checklist

### Final Verification Before PR

- [ ] **Code Quality**
  - [ ] No C# analyzer warnings (`dotnet build`)
  - [ ] Nullable reference types properly annotated
  - [ ] Naming conventions consistent (PascalCase methods, camelCase variables)
  - [ ] No hardcoded magic numbers (constants defined)

- [ ] **Functionality**
  - [ ] Parse endpoint returns 200 with valid Norma43 file
  - [ ] Parse endpoint returns 400 for invalid/empty file
  - [ ] Parse endpoint returns 403 for insufficient role
  - [ ] Confirm endpoint returns 200 with per-item success/failure
  - [ ] Confirm endpoint calls existing `PayFeeAsync` correctly
  - [ ] All matching algorithm paths tested (exact, tolerance, fuzzy)

- [ ] **Testing**
  - [ ] Unit tests: 90%+ coverage of business logic
  - [ ] Integration tests: All endpoint scenarios covered
  - [ ] Tests use xUnit + FluentAssertions + NSubstitute
  - [ ] Test names describe intent (`TestCondition_When_Then` format)

- [ ] **Data Integrity**
  - [ ] EF Core queries optimized (AsNoTracking for reads, no N+1)
  - [ ] Transactions handled correctly (no data corruption)
  - [ ] Amounts stored as `decimal` (no floating-point)

- [ ] **Documentation**
  - [ ] API endpoint documentation updated
  - [ ] Norma43 format explained with examples
  - [ ] Matching algorithm documented
  - [ ] Example request/response payloads provided

- [ ] **Git Hygiene**
  - [ ] Commits are logical, focused, with clear messages
  - [ ] Branch has not diverged significantly from `dev`
  - [ ] No merge conflicts
  - [ ] Ready for squash-and-merge into `dev`
