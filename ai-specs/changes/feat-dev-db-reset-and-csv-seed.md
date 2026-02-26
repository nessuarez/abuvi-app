# Backend Implementation Plan: feat-dev-db-reset-and-csv-seed — Dev Database Reset & CSV Seed Setup

## 2. Overview

Provide a **development-only** mechanism to:

1. **Reset the database** — wipe all non-schema data in FK-safe order and re-seed the default admin user.
2. **Import initial data from CSV files** — one endpoint per entity type, processed in dependency order.

The feature is gated behind an `IsDevelopment()` guard so the endpoints are never registered in production. No migration is created — the existing schema is preserved; only data is affected.

**Import dependency order (strict):**

```
Users → FamilyUnits → FamilyMembers → Camps → CampEditions
```

---

## 3. Architecture Context

**Feature slice:** `src/Abuvi.API/Features/DevSeed/`

### Files to create

| File | Purpose |
|------|---------|
| `Features/DevSeed/DevSeedEndpoints.cs` | Minimal API endpoint registration (dev-only) |
| `Features/DevSeed/DevSeedService.cs` | Orchestrates reset and CSV parsing logic |
| `Features/DevSeed/IDevSeedService.cs` | Interface |
| `Features/DevSeed/DevSeedModels.cs` | DTOs: `SeedResult`, `SeedRowResult`, `SeedSummaryResponse` |
| `Features/DevSeed/Parsers/UserCsvParser.cs` | Parses `users.csv` → `CreateUserRequest` list |
| `Features/DevSeed/Parsers/FamilyUnitCsvParser.cs` | Parses `family-units.csv` → import rows |
| `Features/DevSeed/Parsers/FamilyMemberCsvParser.cs` | Parses `family-members.csv` → import rows |
| `Features/DevSeed/Parsers/CampCsvParser.cs` | Parses `camps.csv` → import rows |
| `Features/DevSeed/Parsers/CampEditionCsvParser.cs` | Parses `camp-editions.csv` → import rows |
| `src/Abuvi.Tests/Unit/Features/DevSeed/DevSeedServiceTests.cs` | Unit tests |
| `src/Abuvi.Tests/Unit/Features/DevSeed/Parsers/UserCsvParserTests.cs` | Parser unit tests |
| `src/Abuvi.Tests/Helpers/TestFiles/seed/users.csv` | Fixture CSV |
| `src/Abuvi.Tests/Helpers/TestFiles/seed/family-units.csv` | Fixture CSV |
| `src/Abuvi.Tests/Helpers/TestFiles/seed/family-members.csv` | Fixture CSV |
| `src/Abuvi.Tests/Helpers/TestFiles/seed/camps.csv` | Fixture CSV |
| `src/Abuvi.Tests/Helpers/TestFiles/seed/camp-editions.csv` | Fixture CSV |

### Files to modify

| File | Change |
|------|--------|
| `Program.cs` | Register `IDevSeedService` and map `DevSeedEndpoints` only when `app.Environment.IsDevelopment()` |
| `Features/Users/IUsersRepository.cs` | Add `DeleteAllExceptAdminAsync` |
| `Features/Users/UsersRepository.cs` | Implement `DeleteAllExceptAdminAsync` |
| `Features/FamilyUnits/IFamilyUnitsRepository.cs` | Add `DeleteAllAsync` |
| `Features/FamilyUnits/FamilyUnitsRepository.cs` | Implement `DeleteAllAsync` |
| `Features/Camps/ICampsRepository.cs` | Add `DeleteAllAsync` |
| `Features/Camps/CampsRepository.cs` | Implement `DeleteAllAsync` |
| `Features/Camps/ICampEditionsRepository.cs` | Add `DeleteAllAsync` |
| `Features/Camps/CampEditionsRepository.cs` | Implement `DeleteAllAsync` |

---

## 4. Implementation Steps

### Step 0: Create Feature Branch

- `git checkout main && git pull origin main`
- `git checkout -b feature/feat-dev-db-reset-and-csv-seed`

---

### Step 1 (TDD — RED): Write failing tests for `DevSeedService`

**File:** `src/Abuvi.Tests/Unit/Features/DevSeed/DevSeedServiceTests.cs`

Write all test cases first. They must fail before any implementation exists.

**Test cases:**

```csharp
// ResetAsync
ResetAsync_WhenCalled_DeletesAllEntitiesInFkSafeOrder
ResetAsync_WhenCalled_ReseedsAdminUser

// ImportUsersAsync
ImportUsersAsync_WithValidCsv_CreatesUsers
ImportUsersAsync_WithDuplicateEmail_SkipsAndReports
ImportUsersAsync_WithMissingRequiredField_ReturnsRowError

// ImportFamilyUnitsAsync
ImportFamilyUnitsAsync_WithValidCsv_CreatesFamilyUnits
ImportFamilyUnitsAsync_WhenRepresentativeEmailNotFound_SkipsAndReports

// ImportFamilyMembersAsync
ImportFamilyMembersAsync_WithValidCsv_CreatesMembersLinkedToUnit
ImportFamilyMembersAsync_WhenFamilyUnitNotFound_SkipsAndReports

// ImportCampsAsync
ImportCampsAsync_WithValidCsv_CreatesCamps
ImportCampsAsync_WithDuplicateName_SkipsAndReports

// ImportCampEditionsAsync
ImportCampEditionsAsync_WithValidCsv_CreatesEditions
ImportCampEditionsAsync_WhenCampNotFound_SkipsAndReports
ImportCampEditionsAsync_WithDuplicateYearForSameCamp_SkipsAndReports
```

---

### Step 2 (TDD — RED): Write failing parser tests

**File:** `src/Abuvi.Tests/Unit/Features/DevSeed/Parsers/UserCsvParserTests.cs`
(repeat pattern for each parser)

```csharp
Parse_WithValidCsv_ReturnsRows
Parse_WithEmptyFile_ReturnsEmptyList
Parse_WithMissingHeader_ThrowsCsvFormatException
Parse_WithExtraWhitespace_TrimsFields
```

---

### Step 3: Implement `DevSeedModels.cs`

```csharp
public record SeedRowResult(int Row, bool Success, string? Error);

public record SeedResult(
    string Entity,
    int TotalRows,
    int Imported,
    int Skipped,
    IReadOnlyList<SeedRowResult> Rows);

public record SeedSummaryResponse(
    bool Success,
    IReadOnlyList<SeedResult> Results,
    string? Error = null);
```

---

### Step 4 (TDD — GREEN): Implement CSV parsers

**Pattern for all parsers:**

```csharp
public static class UserCsvParser
{
    // Separator: comma (,). UTF-8 encoding.
    public static IReadOnlyList<UserCsvRow> Parse(string csvContent)
    {
        var lines = csvContent.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        // Skip header row
        // Parse each line, trim whitespace
        // Return list of UserCsvRow
    }
}

public record UserCsvRow(
    string Email,
    string Password,
    string FirstName,
    string LastName,
    string? Phone,
    string Role,            // "Admin" | "Board" | "Member"
    string? DocumentNumber);
```

Apply same pattern for `FamilyUnitCsvRow`, `FamilyMemberCsvRow`, `CampCsvRow`, `CampEditionCsvRow`.

---

### Step 5 (TDD — GREEN): Implement `DevSeedService`

**File:** `Features/DevSeed/DevSeedService.cs`

```csharp
public class DevSeedService(
    AbuviDbContext db,
    IUsersRepository usersRepo,
    IFamilyUnitsRepository familyUnitsRepo,
    ICampsRepository campsRepo,
    ICampEditionsRepository campEditionsRepo,
    IPasswordHasher passwordHasher,
    ILogger<DevSeedService> logger) : IDevSeedService
{
    private static readonly Guid AdminId = new("00000000-0000-0000-0000-000000000001");

    public async Task ResetAsync(CancellationToken ct)
    {
        // Delete in FK-safe order (children before parents)
        await db.Payments.ExecuteDeleteAsync(ct);
        await db.RegistrationExtras.ExecuteDeleteAsync(ct);
        await db.RegistrationMembers.ExecuteDeleteAsync(ct);
        await db.Registrations.ExecuteDeleteAsync(ct);
        await db.CampEditionExtras.ExecuteDeleteAsync(ct);
        await db.CampEditions.ExecuteDeleteAsync(ct);
        await db.CampPhotos.ExecuteDeleteAsync(ct);
        await db.Camps.ExecuteDeleteAsync(ct);
        await db.MembershipFees.ExecuteDeleteAsync(ct);
        await db.Memberships.ExecuteDeleteAsync(ct);
        await db.Guests.ExecuteDeleteAsync(ct);
        await db.FamilyMembers.ExecuteDeleteAsync(ct);
        await db.FamilyUnits.ExecuteDeleteAsync(ct);
        await db.UserRoleChangeLogs.ExecuteDeleteAsync(ct);
        // Delete all users EXCEPT the seeded admin
        await db.Users.Where(u => u.Id != AdminId).ExecuteDeleteAsync(ct);

        // Ensure admin user exists (re-seed if it was removed)
        if (!await db.Users.AnyAsync(u => u.Id == AdminId, ct))
            await SeedAdminAsync(ct);

        logger.LogInformation("Database reset completed");
    }

    public async Task<SeedResult> ImportUsersAsync(string csvContent, CancellationToken ct) { ... }
    public async Task<SeedResult> ImportFamilyUnitsAsync(string csvContent, CancellationToken ct) { ... }
    public async Task<SeedResult> ImportFamilyMembersAsync(string csvContent, CancellationToken ct) { ... }
    public async Task<SeedResult> ImportCampsAsync(string csvContent, CancellationToken ct) { ... }
    public async Task<SeedResult> ImportCampEditionsAsync(string csvContent, CancellationToken ct) { ... }

    private async Task SeedAdminAsync(CancellationToken ct) { ... }
}
```

**Per-import method pattern:**

1. Parse CSV → rows
2. For each row:
   a. Validate required fields (skip + log if invalid)
   b. Check for duplicates (skip + log if found)
   c. Create entity and persist
   d. Record `SeedRowResult`
3. Return `SeedResult` with summary

**Duplicate detection strategy:**

| Entity | Uniqueness key |
|--------|---------------|
| User | `email` |
| FamilyUnit | `name` (case-insensitive) |
| FamilyMember | `familyUnitId + firstName + lastName + dateOfBirth` |
| Camp | `name` (case-insensitive) |
| CampEdition | `campId + year` |

---

### Step 6: Implement `DevSeedEndpoints.cs`

```csharp
public static class DevSeedEndpoints
{
    public static void MapDevSeedEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/dev")
            .RequireAuthorization("AdminOnly")
            .WithTags("Dev");

        group.MapPost("/reset", async (IDevSeedService svc, CancellationToken ct) =>
        {
            await svc.ResetAsync(ct);
            return Results.Ok(new { message = "Database reset successfully" });
        });

        group.MapPost("/seed/users", async (
            IFormFile file, IDevSeedService svc, CancellationToken ct) =>
        {
            var csv = await ReadFileAsync(file);
            var result = await svc.ImportUsersAsync(csv, ct);
            return Results.Ok(result);
        }).DisableAntiforgery();

        // Same pattern for /seed/family-units, /seed/family-members,
        // /seed/camps, /seed/camp-editions

        group.MapPost("/seed/run-all", async (
            IFormFileCollection files, IDevSeedService svc, CancellationToken ct) =>
        {
            // Accepts multipart form with named files:
            // users, familyUnits, familyMembers, camps, campEditions
            // Runs reset first, then imports in dependency order
            await svc.ResetAsync(ct);
            var results = new List<SeedResult>();
            if (files["users"] is { } u)
                results.Add(await svc.ImportUsersAsync(await ReadFileAsync(u), ct));
            if (files["familyUnits"] is { } fu)
                results.Add(await svc.ImportFamilyUnitsAsync(await ReadFileAsync(fu), ct));
            if (files["familyMembers"] is { } fm)
                results.Add(await svc.ImportFamilyMembersAsync(await ReadFileAsync(fm), ct));
            if (files["camps"] is { } c)
                results.Add(await svc.ImportCampsAsync(await ReadFileAsync(c), ct));
            if (files["campEditions"] is { } ce)
                results.Add(await svc.ImportCampEditionsAsync(await ReadFileAsync(ce), ct));

            return Results.Ok(new SeedSummaryResponse(true, results));
        }).DisableAntiforgery();
    }

    private static async Task<string> ReadFileAsync(IFormFile file)
    {
        using var reader = new StreamReader(file.OpenReadStream());
        return await reader.ReadToEndAsync();
    }
}
```

---

### Step 7: Register in `Program.cs`

```csharp
// Register dev-only services
if (builder.Environment.IsDevelopment())
{
    builder.Services.AddScoped<IDevSeedService, DevSeedService>();
}

// ... after app.Build() ...

if (app.Environment.IsDevelopment())
{
    app.MapDevSeedEndpoints();
}
```

---

### Step 8 (TDD — REFACTOR): Ensure 90%+ coverage, clean up edge cases

Review test coverage, add missing edge-case tests (empty CSV, malformed rows, all-skipped imports), ensure all tests are green.

---

## 5. CSV File Formats

All files use **comma separator**, **UTF-8 encoding**, **first row is header**.
Dates use `YYYY-MM-DD` (ISO 8601).
Optional fields: leave column empty (consecutive commas).

---

### `users.csv`

```csv
email,password,firstName,lastName,phone,role,documentNumber
admin@abuvi.local,Admin@123456,System,Administrator,,Admin,
board@abuvi.local,Board@123456,Ana,López,+34612345678,Board,12345678A
member1@abuvi.local,Member@123456,Carlos,García,+34698765432,Member,87654321B
```

| Column | Required | Notes |
|--------|----------|-------|
| `email` | Yes | Unique, valid email |
| `password` | Yes | Min 8 chars, will be BCrypt-hashed on import |
| `firstName` | Yes | Max 100 chars |
| `lastName` | Yes | Max 100 chars |
| `phone` | No | E.164 format: `+34612345678` |
| `role` | Yes | `Admin` \| `Board` \| `Member` |
| `documentNumber` | No | Uppercase alphanumeric |

---

### `family-units.csv`

```csv
name,representativeEmail
García Family,member1@abuvi.local
López Family,board@abuvi.local
```

| Column | Required | Notes |
|--------|----------|-------|
| `name` | Yes | Unique (case-insensitive), max 200 chars |
| `representativeEmail` | Yes | Must match an existing user email after users import |

---

### `family-members.csv`

```csv
familyUnitName,firstName,lastName,dateOfBirth,relationship,documentNumber,email,phone
García Family,Carlos,García,1982-03-15,Parent,87654321B,member1@abuvi.local,+34698765432
García Family,Laura,García,1985-07-22,Spouse,,laura@example.com,
García Family,Pablo,García,2010-11-05,Child,,,
García Family,Sofía,García,2014-04-18,Child,,,
López Family,Ana,López,1979-01-30,Parent,12345678A,board@abuvi.local,+34612345678
```

| Column | Required | Notes |
|--------|----------|-------|
| `familyUnitName` | Yes | Must match an existing family unit name |
| `firstName` | Yes | Max 100 chars |
| `lastName` | Yes | Max 100 chars |
| `dateOfBirth` | Yes | `YYYY-MM-DD`, must be past date |
| `relationship` | Yes | `Parent` \| `Child` \| `Sibling` \| `Spouse` \| `Other` |
| `documentNumber` | No | Uppercase alphanumeric |
| `email` | No | Valid email format |
| `phone` | No | E.164 format |

---

### `camps.csv`

```csv
name,description,location,pricePerAdult,pricePerChild,pricePerBaby
Camp Abuvi 2027,Main annual camp,Sierra de Guadarrama, Madrid,150.00,100.00,0.00
Camp Coastal,Summer coastal camp,Costa Brava, Girona,180.00,120.00,0.00
```

| Column | Required | Notes |
|--------|----------|-------|
| `name` | Yes | Unique (case-insensitive), max 200 chars |
| `description` | No | Max 2000 chars |
| `location` | No | Free text location description |
| `pricePerAdult` | Yes | Decimal >= 0 |
| `pricePerChild` | Yes | Decimal >= 0 |
| `pricePerBaby` | Yes | Decimal >= 0 |

---

### `camp-editions.csv`

```csv
campName,year,startDate,endDate,pricePerAdult,pricePerChild,pricePerBaby,maxCapacity,status,notes
Camp Abuvi 2027,2027,2027-07-01,2027-07-15,150.00,100.00,0.00,100,Draft,First edition for testing
Camp Coastal,2027,2027-08-01,2027-08-10,180.00,120.00,0.00,,Proposed,
```

| Column | Required | Notes |
|--------|----------|-------|
| `campName` | Yes | Must match an existing camp name |
| `year` | Yes | Integer, e.g. `2027` |
| `startDate` | Yes | `YYYY-MM-DD` |
| `endDate` | Yes | `YYYY-MM-DD`, must be after startDate |
| `pricePerAdult` | Yes | Decimal >= 0 |
| `pricePerChild` | Yes | Decimal >= 0 |
| `pricePerBaby` | Yes | Decimal >= 0 |
| `maxCapacity` | No | Integer > 0, leave empty for unlimited |
| `status` | Yes | `Proposed` \| `Draft` \| `Open` \| `Closed` \| `Completed` |
| `notes` | No | Free text |

> **Note:** Importing a `CampEdition` with `status = Open` bypasses the usual date constraint because this is a dev-only seed operation.

---

## 6. API Endpoints

All endpoints require `Admin` role JWT. Only available in `Development` environment.

| Method | Path | Body | Description |
|--------|------|------|-------------|
| `POST` | `/api/dev/reset` | — | Wipe all data, re-seed admin user |
| `POST` | `/api/dev/seed/users` | `multipart/form-data` file | Import `users.csv` |
| `POST` | `/api/dev/seed/family-units` | `multipart/form-data` file | Import `family-units.csv` |
| `POST` | `/api/dev/seed/family-members` | `multipart/form-data` file | Import `family-members.csv` |
| `POST` | `/api/dev/seed/camps` | `multipart/form-data` file | Import `camps.csv` |
| `POST` | `/api/dev/seed/camp-editions` | `multipart/form-data` file | Import `camp-editions.csv` |
| `POST` | `/api/dev/seed/run-all` | `multipart/form-data` (named files) | Reset + full import in order |

### Response shape (all seed endpoints)

```json
{
  "entity": "Users",
  "totalRows": 3,
  "imported": 2,
  "skipped": 1,
  "rows": [
    { "row": 1, "success": true, "error": null },
    { "row": 2, "success": true, "error": null },
    { "row": 3, "success": false, "error": "Duplicate email: member1@abuvi.local" }
  ]
}
```

### `run-all` multipart field names

```
users          → users.csv
familyUnits    → family-units.csv
familyMembers  → family-members.csv
camps          → camps.csv
campEditions   → camp-editions.csv
```

All files are optional. Missing files are skipped. Import runs in dependency order regardless of submission order.

---

## 7. Testing Plan

### Unit Tests — `DevSeedServiceTests.cs`

Mock dependencies: `AbuviDbContext` (using InMemory EF provider for service-level tests), `ILogger<DevSeedService>`, `IPasswordHasher`.

| Test | Scenario |
|------|---------|
| `ResetAsync_WhenCalled_DeletesAllEntitiesInFkSafeOrder` | Verify each `ExecuteDeleteAsync` call is made |
| `ResetAsync_WhenAdminMissing_ReseedsAdminUser` | Admin is recreated if absent |
| `ImportUsersAsync_WithValidCsv_CreatesAllUsers` | 3 rows → 3 users created |
| `ImportUsersAsync_WithDuplicateEmail_SkipsRow` | Row 2 duplicate → `imported=1, skipped=1` |
| `ImportUsersAsync_WithMissingEmail_ReturnsRowError` | Empty email → row error message |
| `ImportFamilyUnitsAsync_WhenRepresentativeNotFound_SkipsRow` | Unknown email → skip + error |
| `ImportFamilyMembersAsync_WhenFamilyUnitNotFound_SkipsRow` | Unknown unit → skip + error |
| `ImportCampEditionsAsync_WithDuplicateYearForSameCamp_SkipsRow` | Same camp+year → skip |
| `ImportCampEditionsAsync_WithOpenStatus_BypassesDateConstraint` | `status=Open`, past date → still imports |

### Unit Tests — Parsers

| Test | Scenario |
|------|---------|
| `Parse_WithValidCsv_ReturnsCorrectRowCount` | 3 data rows → 3 records |
| `Parse_WithEmptyBody_ReturnsEmptyList` | Header only → empty list |
| `Parse_WithExtraWhitespace_TrimsAllFields` | ` email ` → `email` |
| `Parse_WithMissingHeader_ThrowsCsvFormatException` | No header row → exception |
| `Parse_WithWrongColumnCount_SkipsRowAndLogs` | Malformed row → skip |

---

## 8. Usage Guide (Dev Workflow)

### Option A — Full reset + seed from scratch

```bash
# 1. Get an admin JWT
TOKEN=$(curl -s -X POST http://localhost:5000/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{"email":"admin@abuvi.local","password":"Admin@123456"}' | jq -r '.token')

# 2. Reset + seed all at once
curl -X POST http://localhost:5000/api/dev/seed/run-all \
  -H "Authorization: Bearer $TOKEN" \
  -F "users=@./seed/users.csv" \
  -F "familyUnits=@./seed/family-units.csv" \
  -F "familyMembers=@./seed/family-members.csv" \
  -F "camps=@./seed/camps.csv" \
  -F "campEditions=@./seed/camp-editions.csv"
```

### Option B — Reset only (keep schema, wipe data)

```bash
curl -X POST http://localhost:5000/api/dev/reset \
  -H "Authorization: Bearer $TOKEN"
```

### Option C — Import incrementally (without reset)

```bash
curl -X POST http://localhost:5000/api/dev/seed/camps \
  -H "Authorization: Bearer $TOKEN" \
  -F "file=@./seed/camps.csv"
```

---

## 9. Security & Constraints

- Endpoints are **only registered** when `app.Environment.IsDevelopment()` is `true`. They do not exist at all in staging/production — no auth bypass needed.
- Passwords in CSV are plain text for convenience; they are BCrypt-hashed (cost 12) before storage.
- `medicalNotes` and `allergies` in `family-members.csv` are **intentionally excluded** — sensitive fields must be entered manually through the normal API after import.
- The `run-all` endpoint performs `reset` first, so it is a **destructive, idempotent** operation.

---

## 10. Pre-submission Checklist

- [ ] All unit tests pass (`dotnet test`)
- [ ] Coverage ≥ 90% for `DevSeedService` and all parsers
- [ ] Endpoints return 404 when called in non-Development environment
- [ ] `users.csv` fixture includes at least one user per role (`Admin`, `Board`, `Member`)
- [ ] `camp-editions.csv` fixture includes entries with both future and past dates
- [ ] `run-all` tested end-to-end manually in dev environment
- [ ] No sensitive data committed to test fixtures (use fake data only)
