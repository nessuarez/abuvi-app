# Backend Implementation Plan: feat-db-setup-tool — Console App for DB Reset & CSV Setup

## 1. Overview

Create a standalone .NET 9 console application (`Abuvi.Setup`) that serves as the single database setup tool for both development and production environments. The tool resets the database (FK-safe wipe + admin re-seed) and imports initial data from CSV files in strict dependency order.

This is **not** a feature slice inside the API — it is a separate console project that references `Abuvi.API` to reuse `AbuviDbContext` and all entity models. No Minimal API endpoints, no HTTP, no auth.

**Source spec:** `ai-specs/changes/feat-dev-db-reset-and-csv-seed.md`

---

## 2. Architecture Context

**New project:** `src/Abuvi.Setup/` (Console Application)

### Project structure

```
src/Abuvi.Setup/
├── Abuvi.Setup.csproj
├── Program.cs
├── SetupConfig.cs
├── SeedRunner.cs
├── SafetyGuard.cs
├── Models.cs
├── CsvHelper.cs
└── Importers/
    ├── UserImporter.cs
    ├── FamilyUnitImporter.cs
    ├── FamilyMemberImporter.cs
    ├── CampImporter.cs
    └── CampEditionImporter.cs
```

### Files to create

| File | Purpose |
|------|---------|
| `src/Abuvi.Setup/Abuvi.Setup.csproj` | Console app, references `Abuvi.API` |
| `src/Abuvi.Setup/Program.cs` | CLI entry point, argument parsing, DB connection |
| `src/Abuvi.Setup/SetupConfig.cs` | Strongly-typed CLI options (`--env`, `--dir`, `--connection`, `--confirm`) |
| `src/Abuvi.Setup/SafetyGuard.cs` | Production safety: confirmation prompts, empty-table checks |
| `src/Abuvi.Setup/SeedRunner.cs` | Orchestrates reset + import pipeline |
| `src/Abuvi.Setup/Models.cs` | `SeedRowResult`, `SeedResult` records |
| `src/Abuvi.Setup/CsvHelper.cs` | Generic CSV parser (comma-separated, UTF-8, dictionary-based) |
| `src/Abuvi.Setup/Importers/UserImporter.cs` | Parses `users.csv`, hashes passwords, inserts users |
| `src/Abuvi.Setup/Importers/FamilyUnitImporter.cs` | Parses `family-units.csv`, resolves representative by email |
| `src/Abuvi.Setup/Importers/FamilyMemberImporter.cs` | Parses `family-members.csv`, resolves family unit by name |
| `src/Abuvi.Setup/Importers/CampImporter.cs` | Parses `camps.csv`, inserts camps |
| `src/Abuvi.Setup/Importers/CampEditionImporter.cs` | Parses `camp-editions.csv`, resolves camp by name, sets status directly |
| `src/Abuvi.Tests/Unit/Setup/CsvHelperTests.cs` | Unit tests for CSV parsing |
| `src/Abuvi.Tests/Unit/Setup/SafetyGuardTests.cs` | Unit tests for production safety checks |
| `src/Abuvi.Tests/Unit/Setup/Importers/UserImporterTests.cs` | Unit tests for user import logic |
| `src/Abuvi.Tests/Unit/Setup/Importers/FamilyUnitImporterTests.cs` | Unit tests |
| `src/Abuvi.Tests/Unit/Setup/Importers/FamilyMemberImporterTests.cs` | Unit tests |
| `src/Abuvi.Tests/Unit/Setup/Importers/CampImporterTests.cs` | Unit tests |
| `src/Abuvi.Tests/Unit/Setup/Importers/CampEditionImporterTests.cs` | Unit tests |
| `src/Abuvi.Tests/Helpers/TestFiles/seed/users.csv` | Test fixture CSV |
| `src/Abuvi.Tests/Helpers/TestFiles/seed/family-units.csv` | Test fixture CSV |
| `src/Abuvi.Tests/Helpers/TestFiles/seed/family-members.csv` | Test fixture CSV |
| `src/Abuvi.Tests/Helpers/TestFiles/seed/camps.csv` | Test fixture CSV |
| `src/Abuvi.Tests/Helpers/TestFiles/seed/camp-editions.csv` | Test fixture CSV |

### Files to modify

| File | Change |
|------|--------|
| `Abuvi.slnx` | Add `Abuvi.Setup` project reference |
| `src/Abuvi.Tests/Abuvi.Tests.csproj` | Add project reference to `Abuvi.Setup` |

---

## 3. Implementation Steps

### Step 0: Create Feature Branch

- **Action**: Create and switch to a new feature branch
- **Branch Naming**: `feature/feat-db-setup-tool-backend`
- **Implementation Steps**:
  1. `git checkout main && git pull origin main`
  2. `git checkout -b feature/feat-db-setup-tool-backend`
  3. `git branch` — verify you are on the new branch

---

### Step 1: Create Console Project and Solution Reference

- **Files**: `src/Abuvi.Setup/Abuvi.Setup.csproj`, `Abuvi.slnx`
- **Action**: Create the console app project and register it in the solution

**Implementation Steps**:

1. Create directory `src/Abuvi.Setup/`
2. Create `Abuvi.Setup.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net9.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
  </PropertyGroup>
  <ItemGroup>
    <ProjectReference Include="..\Abuvi.API\Abuvi.API.csproj" />
  </ItemGroup>
  <ItemGroup>
    <PackageReference Include="BCrypt.Net-Next" Version="4.*" />
  </ItemGroup>
</Project>
```

3. Add to `Abuvi.slnx`:

```xml
<Solution>
  <Folder Name="/src/">
    <Project Path="src/Abuvi.API/Abuvi.API.csproj" />
    <Project Path="src/Abuvi.Tests/Abuvi.Tests.csproj" />
    <Project Path="src/Abuvi.Setup/Abuvi.Setup.csproj" />
  </Folder>
</Solution>
```

4. Add project reference in `src/Abuvi.Tests/Abuvi.Tests.csproj`:

```xml
<ProjectReference Include="..\Abuvi.Setup\Abuvi.Setup.csproj" />
```

5. Verify: `dotnet build`

---

### Step 2 (TDD — RED): Write Unit Tests for `CsvHelper`

- **File**: `src/Abuvi.Tests/Unit/Setup/CsvHelperTests.cs`
- **Action**: Write failing tests before implementing CsvHelper
- **Dependencies**: xUnit, FluentAssertions

**Test Cases**:

```csharp
// Parse
Parse_WithValidFile_ReturnsCorrectRowCount
Parse_WithEmptyFile_ReturnsEmptyList
Parse_WithHeaderOnly_ReturnsEmptyList
Parse_WithExtraWhitespace_TrimsAllFields
Parse_HeadersAreCaseInsensitive

// Require
Require_WithExistingKey_ReturnsValue
Require_WithMissingKey_ThrowsInvalidOperationException
Require_WithEmptyValue_ThrowsInvalidOperationException

// Optional
Optional_WithExistingKey_ReturnsValue
Optional_WithMissingKey_ReturnsNull
Optional_WithEmptyValue_ReturnsNull

// RequireDecimal
RequireDecimal_WithValidDecimal_ReturnsParsedValue
RequireDecimal_WithInvalidValue_ThrowsException

// OptionalInt
OptionalInt_WithValidInt_ReturnsParsedValue
OptionalInt_WithEmptyValue_ReturnsNull
```

**Implementation Notes**:
- Tests use temporary files written to disk via `Path.GetTempFileName()` — clean up in `Dispose()`
- Fixture CSVs stored at `src/Abuvi.Tests/Helpers/TestFiles/seed/` for integration-style tests
- Each CSV fixture must be marked `<None CopyToOutputDirectory="PreserveNewest" />` in the test csproj

---

### Step 3 (TDD — GREEN): Implement `CsvHelper.cs`

- **File**: `src/Abuvi.Setup/CsvHelper.cs`
- **Action**: Generic CSV parser returning `IReadOnlyList<Dictionary<string, string>>`

**Implementation Steps**:

1. Read file with `File.ReadAllLines(filePath, Encoding.UTF8)`
2. Filter empty lines
3. First line = headers (split by `,`, trim)
4. Remaining lines = data rows (split by `,`, trim)
5. Build `Dictionary<string, string>` per row (case-insensitive keys via `StringComparer.OrdinalIgnoreCase`)
6. Static helper methods: `Require()`, `Optional()`, `RequireDecimal()`, `OptionalInt()`

**Implementation Notes**:
- Use `System.Globalization.CultureInfo.InvariantCulture` for decimal parsing
- `Require()` throws `InvalidOperationException` with descriptive message including the field name

---

### Step 4: Implement `Models.cs`

- **File**: `src/Abuvi.Setup/Models.cs`
- **Action**: Create result records for import reporting

```csharp
namespace Abuvi.Setup;

public record SeedRowResult(int Row, bool Success, string? Error);

public record SeedResult(
    string Entity,
    int TotalRows,
    int Imported,
    int Skipped,
    IReadOnlyList<SeedRowResult> Rows)
{
    public void Print()
    {
        var color = Skipped > 0 ? ConsoleColor.Yellow : ConsoleColor.Green;
        Console.ForegroundColor = color;
        Console.WriteLine($"  {Entity}: {Imported}/{TotalRows} imported, {Skipped} skipped");
        Console.ResetColor();
        foreach (var row in Rows.Where(r => !r.Success))
            Console.WriteLine($"    Row {row.Row}: {row.Error}");
    }
}
```

---

### Step 5: Implement `SetupConfig.cs`

- **File**: `src/Abuvi.Setup/SetupConfig.cs`
- **Action**: Parse CLI arguments into strongly-typed config

**Implementation Steps**:

1. Define `SetupEnv` enum: `Dev`, `Production`
2. Parse `--env=`, `--connection=`, `--dir=`, `--confirm` from `string[] args`
3. Fallback chain for connection string: `--connection=` → `DATABASE_URL` env var → localhost default
4. Fallback for seed dir: `--dir=` → `./seed/` relative to executable
5. Expose `IsProduction` computed property

---

### Step 6 (TDD — RED): Write Unit Tests for `SafetyGuard`

- **File**: `src/Abuvi.Tests/Unit/Setup/SafetyGuardTests.cs`
- **Action**: Write failing tests for production safety checks

**Test Cases**:

```csharp
// EnsureImportAllowed
EnsureImportAllowed_InDevMode_AlwaysReturnsTrue
EnsureImportAllowed_InProduction_EmptyTable_ReturnsTrue
EnsureImportAllowed_InProduction_TableHasData_ReturnsFalse
EnsureImportAllowed_InProduction_UsersTable_IgnoresAdminUser

// EnsureResetAllowed is harder to unit test (calls Environment.Exit)
// Test via integration or by refactoring to return bool
```

**Implementation Notes**:
- Use EF Core InMemory provider for these tests — `SafetyGuard` only does `AnyAsync()` / `CountAsync()`, no Npgsql-specific features
- Consider refactoring `EnsureResetAllowed()` to return a `bool` instead of calling `Environment.Exit()` directly, to make it testable. The `Program.cs` caller can handle the exit.

---

### Step 7 (TDD — GREEN): Implement `SafetyGuard.cs`

- **File**: `src/Abuvi.Setup/SafetyGuard.cs`
- **Action**: Production safety checks

**Implementation Steps**:

1. `EnsureResetAllowed()`:
   - In dev mode: return `true` immediately
   - In production: check `config.Confirm` flag → if missing, print error and return `false`
   - If confirmed: prompt interactive "Type 'YES' to confirm" → return `bool`
2. `EnsureImportAllowed(entity)`:
   - In dev mode: return `true` immediately
   - In production: check if target table has data
     - `users`: `Count > 1` (admin is always present)
     - All others: `AnyAsync()`
   - If data exists: print blocking message, return `false`

**Implementation Notes**:
- Return `bool` instead of calling `Environment.Exit()` — let caller decide
- Accept `CancellationToken` parameter on async methods

---

### Step 8 (TDD — RED): Write Unit Tests for Importers

- **Files**: `src/Abuvi.Tests/Unit/Setup/Importers/UserImporterTests.cs` (and one per importer)
- **Action**: Write failing tests for all importers

**Test Cases per Importer**:

#### `UserImporterTests.cs`
```csharp
ImportAsync_WithValidCsv_CreatesAllUsers
ImportAsync_WithDuplicateEmail_SkipsRowAndReports
ImportAsync_WithMissingRequiredField_SkipsRowAndReports
ImportAsync_HashesPasswordWithBCrypt
ImportAsync_SetsEmailVerifiedTrue
ImportAsync_SetsIsActiveTrue
ImportAsync_ParsesRoleEnum
```

#### `FamilyUnitImporterTests.cs`
```csharp
ImportAsync_WithValidCsv_CreatesFamilyUnits
ImportAsync_WhenRepresentativeEmailNotFound_SkipsRowAndReports
ImportAsync_LinksUserFamilyUnitIdBack
ImportAsync_WithDuplicateName_SkipsRowAndReports
```

#### `FamilyMemberImporterTests.cs`
```csharp
ImportAsync_WithValidCsv_CreatesMembers
ImportAsync_WhenFamilyUnitNameNotFound_SkipsRowAndReports
ImportAsync_ParsesDateOfBirthCorrectly
ImportAsync_ParsesRelationshipEnum
ImportAsync_OptionalFieldsCanBeEmpty
```

#### `CampImporterTests.cs`
```csharp
ImportAsync_WithValidCsv_CreatesCamps
ImportAsync_WithDuplicateName_SkipsRowAndReports
ImportAsync_SetsIsActiveTrue
ImportAsync_ParsesDecimalPrices
```

#### `CampEditionImporterTests.cs`
```csharp
ImportAsync_WithValidCsv_CreatesEditions
ImportAsync_WhenCampNameNotFound_SkipsRowAndReports
ImportAsync_SetsStatusDirectly_NoWorkflowValidation
ImportAsync_WithDuplicateCampYear_SkipsRowAndReports
ImportAsync_OptionalMaxCapacity_CanBeNull
```

**Implementation Notes**:
- All importer tests use EF Core InMemory provider
- Pre-seed test data (users, family units, camps) where needed for FK resolution tests
- Use helper methods to build test CSV content strings

---

### Step 9 (TDD — GREEN): Implement Importers

- **Files**: `src/Abuvi.Setup/Importers/*.cs`
- **Action**: Implement all 5 importers

**Common Pattern for All Importers**:

1. Accept `AbuviDbContext` via constructor
2. `ImportAsync(string filePath)` method returns `Task<SeedResult>`
3. Parse CSV via `CsvHelper.Parse(filePath)`
4. Loop each row:
   a. Try-catch block wrapping the row processing
   b. Validate required fields via `CsvHelper.Require()`
   c. Check for duplicates in DB (entity-specific uniqueness key)
   d. Build entity object
   e. `db.Add(entity)` + `db.SaveChangesAsync()` per row
   f. Record `SeedRowResult(rowIndex, true/false, error)`
5. Return `SeedResult` with totals

**Duplicate Detection Keys**:

| Importer | Uniqueness Check |
|----------|-----------------|
| `UserImporter` | `db.Users.AnyAsync(u => u.Email == email)` |
| `FamilyUnitImporter` | `db.FamilyUnits.AnyAsync(u => u.Name.ToLower() == name.ToLower())` |
| `FamilyMemberImporter` | `familyUnitId + firstName + lastName + dateOfBirth` composite |
| `CampImporter` | `db.Camps.AnyAsync(c => c.Name.ToLower() == name.ToLower())` |
| `CampEditionImporter` | `db.CampEditions.AnyAsync(e => e.CampId == campId && e.Year == year)` |

**Entity-Specific Logic**:

- **UserImporter**: Hash password with `BCrypt.Net.BCrypt.HashPassword(password, workFactor: 12)`. Set `IsActive = true`, `EmailVerified = true`.
- **FamilyUnitImporter**: Resolve `representativeEmail` → `User.Id` via DB lookup. After creating unit, update `user.FamilyUnitId = unit.Id` to link back.
- **FamilyMemberImporter**: Resolve `familyUnitName` → `FamilyUnit.Id` via DB lookup. Parse `DateOnly` from ISO date string. Parse `FamilyRelationship` enum.
- **CampImporter**: Direct insert. Set `IsActive = true`.
- **CampEditionImporter**: Resolve `campName` → `Camp.Id` via DB lookup. Parse `CampEditionStatus` enum directly — **no workflow validation** (this is a setup tool). Parse `DateTime` for start/end dates. `MaxCapacity` is optional.

---

### Step 10: Implement `SeedRunner.cs`

- **File**: `src/Abuvi.Setup/SeedRunner.cs`
- **Action**: Orchestrate reset and import pipeline

**Implementation Steps**:

1. Constructor receives `AbuviDbContext`, `SafetyGuard`, `SetupConfig`
2. `ResetAsync()`:
   - Execute `ExecuteDeleteAsync()` on all DbSets in FK-safe order (children first):
     ```
     Payments → RegistrationExtras → RegistrationMembers → Registrations →
     CampEditionExtras → CampEditions → CampPhotos → Camps →
     MembershipFees → Memberships → Guests →
     FamilyMembers → FamilyUnits →
     UserRoleChangeLogs → Users (WHERE Id != AdminId)
     ```
   - Re-seed admin user if missing (Id = `00000000-0000-0000-0000-000000000001`)
   - Admin credentials: `admin@abuvi.local` / `Admin@123456` (BCrypt hashed)

3. `ImportAllAsync(seedDir)`:
   - Define ordered list of `(fileName, entityName, guardKey, importerFactory)`
   - For each: check file exists → call `guard.EnsureImportAllowed()` → run importer → print result
   - **Strict order**: users → family-units → family-members → camps → camp-editions

4. `ImportSingleAsync(seedDir, entityName)`:
   - Map entity name to file name and guard key
   - Safety check → run single importer → print result

---

### Step 11: Implement `Program.cs`

- **File**: `src/Abuvi.Setup/Program.cs`
- **Action**: CLI entry point with argument parsing and command dispatch

**Implementation Steps**:

1. Parse `SetupConfig` from `args`
2. Print environment banner (cyan for dev, red for production)
3. Build `DbContextOptions<AbuviDbContext>` with `UseNpgsql(connectionString)`
4. Verify DB connection with `db.Database.CanConnectAsync()` — exit 1 on failure
5. Instantiate `SafetyGuard` and `SeedRunner`
6. Parse command (first non-flag argument, default `run-all`):
   - `run-all`: guard reset (if production) → `ResetAsync()` → `ImportAllAsync()`
   - `setup`: `ImportAllAsync()` only (no reset, production-safe)
   - `reset`: guard reset → `ResetAsync()` only
   - `import <entity>`: `ImportSingleAsync()`
   - default: print usage help, exit 1
7. Return exit code 0 on success

**CLI Interface**:

```
Usage: dotnet run [reset|run-all|setup|import <entity>] [options]

Commands:
  run-all              Reset + import all CSVs (default)
  setup                Import only (no reset, production-safe)
  reset                Wipe all data, re-seed admin
  import <entity>      Import a single entity CSV

Options:
  --env=dev|production Environment mode (default: dev)
  --dir=<path>         CSV files directory (default: ./seed/)
  --connection=<str>   PostgreSQL connection string
  --confirm            Required for production destructive ops
```

---

### Step 12: Create Test Fixture CSV Files

- **Files**: `src/Abuvi.Tests/Helpers/TestFiles/seed/*.csv`
- **Action**: Create sample CSV files for use in tests and as documentation

**Implementation Steps**:

1. Create `users.csv` with 3 rows (one per role: Admin, Board, Member)
2. Create `family-units.csv` with 2-3 rows referencing user emails
3. Create `family-members.csv` with 5+ rows covering all `FamilyRelationship` values
4. Create `camps.csv` with 2 rows
5. Create `camp-editions.csv` with 2-3 rows covering different statuses
6. Mark all CSV files in test csproj: `<None CopyToOutputDirectory="PreserveNewest" />`

---

### Step 13 (TDD — REFACTOR): Coverage Review and Edge Cases

- **Action**: Ensure 90%+ test coverage, add missing edge cases

**Additional Edge Cases to Verify**:

- Empty CSV (header only) → returns `SeedResult` with 0 rows
- CSV with all rows failing → `imported = 0`, `skipped = totalRows`
- Partial failure → first rows succeed, later rows fail, already-imported rows persist
- Unicode characters in names (accents: García, López) — UTF-8 parsing
- Decimal values with `.` separator (invariant culture)
- `DateOnly` parsing with ISO format

---

### Step 14: Update Technical Documentation

- **Action**: Review and update technical documentation
- **Implementation Steps**:
  1. **Review Changes**: Analyze all code created during implementation
  2. **Update `ai-specs/specs/data-model.md`**: No schema changes — document the setup tool instead
  3. **Update `ai-specs/specs/development_guide.md`**: Add section about the setup tool CLI usage for dev and production
  4. **Verify Documentation**: Confirm all changes are accurately reflected, English only
  5. **Report Updates**: List which files were updated

---

## 4. Implementation Order

1. **Step 0**: Create feature branch `feature/feat-db-setup-tool-backend`
2. **Step 1**: Create console project, update solution file, verify build
3. **Step 2** (TDD RED): Write failing `CsvHelperTests`
4. **Step 3** (TDD GREEN): Implement `CsvHelper`
5. **Step 4**: Implement `Models.cs` (records only, no logic to test)
6. **Step 5**: Implement `SetupConfig.cs`
7. **Step 6** (TDD RED): Write failing `SafetyGuardTests`
8. **Step 7** (TDD GREEN): Implement `SafetyGuard`
9. **Step 8** (TDD RED): Write failing importer tests (all 5)
10. **Step 9** (TDD GREEN): Implement all 5 importers
11. **Step 10**: Implement `SeedRunner`
12. **Step 11**: Implement `Program.cs`
13. **Step 12**: Create test fixture CSV files
14. **Step 13** (TDD REFACTOR): Coverage review and edge cases
15. **Step 14**: Update technical documentation

---

## 5. Testing Checklist

### Unit Tests — CsvHelper

| Test | Scenario |
|------|---------|
| `Parse_WithValidFile_ReturnsCorrectRowCount` | 3 data rows → 3 dictionaries |
| `Parse_WithEmptyFile_ReturnsEmptyList` | Empty file → empty list |
| `Parse_WithHeaderOnly_ReturnsEmptyList` | Header only → empty list |
| `Parse_WithExtraWhitespace_TrimsAllFields` | ` email ` → `email` |
| `Require_WithMissingKey_ThrowsInvalidOperationException` | Missing key → exception |
| `Require_WithEmptyValue_ThrowsInvalidOperationException` | Blank value → exception |
| `RequireDecimal_WithValidDecimal_ReturnsParsedValue` | `"150.00"` → `150.00m` |
| `OptionalInt_WithEmptyValue_ReturnsNull` | `""` → `null` |

### Unit Tests — SafetyGuard

| Test | Scenario |
|------|---------|
| `EnsureImportAllowed_InDevMode_AlwaysReturnsTrue` | Dev → always allowed |
| `EnsureImportAllowed_InProduction_EmptyTable_ReturnsTrue` | Empty table → allowed |
| `EnsureImportAllowed_InProduction_TableHasData_ReturnsFalse` | Data exists → blocked |
| `EnsureImportAllowed_InProduction_UsersWithOnlyAdmin_ReturnsTrue` | Only admin → allowed |
| `EnsureResetAllowed_InDevMode_ReturnsTrue` | Dev → always allowed |
| `EnsureResetAllowed_InProduction_WithoutConfirm_ReturnsFalse` | No `--confirm` → blocked |

### Unit Tests — Importers

| Test | Scenario |
|------|---------|
| `UserImporter_ValidCsv_CreatesAllUsers` | 3 rows → 3 users |
| `UserImporter_DuplicateEmail_SkipsRow` | Existing email → skip |
| `UserImporter_HashesPasswordWithBCrypt` | Password is hashed, not plain text |
| `FamilyUnitImporter_ValidCsv_CreatesFamilyUnits` | 2 rows → 2 units |
| `FamilyUnitImporter_RepresentativeNotFound_SkipsRow` | Unknown email → skip |
| `FamilyUnitImporter_LinksUserFamilyUnitIdBack` | User.FamilyUnitId is set |
| `FamilyMemberImporter_ValidCsv_CreatesMembers` | 5 rows → 5 members |
| `FamilyMemberImporter_FamilyUnitNotFound_SkipsRow` | Unknown unit → skip |
| `CampImporter_ValidCsv_CreatesCamps` | 2 rows → 2 camps |
| `CampImporter_DuplicateName_SkipsRow` | Existing name → skip |
| `CampEditionImporter_ValidCsv_CreatesEditions` | 2 rows → 2 editions |
| `CampEditionImporter_CampNotFound_SkipsRow` | Unknown camp → skip |
| `CampEditionImporter_DuplicateCampYear_SkipsRow` | Same camp+year → skip |
| `CampEditionImporter_SetsStatusDirectly` | Status = Open, no workflow |

### Coverage Target

- **90%+** for `CsvHelper`, `SafetyGuard`, all `Importers`
- `SeedRunner` and `Program.cs` are orchestration — test coverage via integration/manual

---

## 6. Error Response Format

Not applicable — this is a console application, not an API. Errors are reported via:

- **Console output**: colored messages (red for errors, yellow for warnings, green for success)
- **Exit codes**: `0` = success, `1` = failure
- **Per-row reporting**: `SeedResult.Print()` shows each failed row with its error message

---

## 7. Dependencies

### NuGet Packages

| Package | Project | Purpose |
|---------|---------|---------|
| `BCrypt.Net-Next` | `Abuvi.Setup` | Password hashing |
| `Npgsql.EntityFrameworkCore.PostgreSQL` | via `Abuvi.API` reference | DB access |
| `Microsoft.EntityFrameworkCore.InMemory` | `Abuvi.Tests` (already present) | Test DB |

### Build Commands

```bash
# Build console app
dotnet build src/Abuvi.Setup

# Run tests
dotnet test src/Abuvi.Tests

# Run setup tool (dev, default)
dotnet run --project src/Abuvi.Setup

# Run setup tool (production)
dotnet run --project src/Abuvi.Setup setup --env=production --connection="Host=..." --dir=./data/
```

---

## 8. Notes

- **No EF Core migration needed** — no schema changes; tool operates on existing tables
- **No API changes** — this is a standalone tool, no endpoints added to `Program.cs`
- **Password security**: CSV contains plain-text passwords for convenience; BCrypt hashes them before DB insertion. For production, consider distributing CSVs with pre-hashed passwords or forcing password reset on first login.
- **Sensitive fields excluded**: `medicalNotes` and `allergies` from `FamilyMember` are NOT imported via CSV — they contain health data subject to GDPR and must be entered via the API with proper encryption.
- **`CampEdition.Status`** is set directly on the entity — no workflow state machine validation. This is intentional for a setup tool.
- **DateTime handling**: All `DateTime` values use `DateTime.UtcNow` for `CreatedAt`/`UpdatedAt`. The `AbuviDbContext.UtcDateTimeConverter` ensures UTC kind. CSV dates for `StartDate`/`EndDate` are parsed and stored as UTC.
- **Language**: All code, comments, variable names, error messages, and CSV headers must be in English.

---

## 9. Next Steps After Implementation

1. Create production-ready CSV files with real data (stored securely, NOT committed to repo)
2. Test full `setup` flow against a staging database
3. Document the production setup runbook in the ops documentation
4. Consider adding a `--dry-run` flag for production validation without actual inserts

---

## 10. Implementation Verification

- [ ] **Code Quality**: C# analyzers pass, nullable reference types enabled, no warnings
- [ ] **Build**: `dotnet build` succeeds for entire solution (API + Setup + Tests)
- [ ] **Tests**: All unit tests pass with 90%+ coverage on core classes
- [ ] **Dev Flow**: `dotnet run --project src/Abuvi.Setup` resets and seeds local DB successfully
- [ ] **Production Safety**: `--env=production` blocks reset without `--confirm`, blocks import on non-empty tables
- [ ] **Single Import**: `import users` works independently without resetting
- [ ] **Error Reporting**: Failed rows show clear messages with row numbers
- [ ] **Documentation**: Updated development guide with setup tool usage
