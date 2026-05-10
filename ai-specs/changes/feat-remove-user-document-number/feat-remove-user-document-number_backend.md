# Backend Implementation Plan: feat-remove-user-document-number — Remove DocumentNumber from User Entity

## Overview

Remove the `DocumentNumber` field entirely from the `User` entity, all related Auth feature components, EF Core configuration, and the CSV importer. The field is redundant because the account holder is always a `FamilyMember` whose document number is already authoritative on that entity. This change simplifies the `RegisterUserRequest` API surface, eliminates a duplicate-document check in `AuthService`, drops the `IX_Users_DocumentNumber` index and the `document_number` column from the database, and cleans up all affected tests.

Architecture principle: single slice (`Auth` + `Users`) plus `UserImporter` in `Abuvi.Setup`. No new abstractions are introduced; this is a pure deletion/simplification task.

---

## Architecture Context

**Feature slices affected:**
- `src/Abuvi.API/Features/Users/` — entity, repository interface, repository implementation
- `src/Abuvi.API/Features/Auth/` — request DTO, validator, service, endpoints
- `src/Abuvi.API/Data/Configurations/UserConfiguration.cs` — EF Core Fluent API config
- `src/Abuvi.API/Data/Migrations/` — new migration to drop index + column
- `src/Abuvi.Setup/Importers/UserImporter.cs` — CSV importer

**Test files affected:**
- `src/Abuvi.Tests/Unit/Features/Auth/AuthServiceTests_Registration.cs`
- `src/Abuvi.Tests/Unit/Features/Auth/RegisterUserRequestTests.cs`
- `src/Abuvi.Tests/Unit/Data/Entities/UserTests.cs`
- `src/Abuvi.Tests/Unit/Features/Auth/RegisterUserValidatorTests.cs`
- `src/Abuvi.Tests/Unit/Setup/Importers/UserImporterTests.cs`

**Cross-cutting concerns:** None. The global exception middleware and `ApiResponse<T>` wrapper are not changed. `UserResponse` DTO does not expose `DocumentNumber` so no response model changes are needed.

---

## Implementation Steps

### Step 0: Create Feature Branch

- **Action**: Create and switch to a new feature branch
- **Branch Naming**: `feature/feat-remove-user-document-number-backend`
- **Implementation Steps**:
  1. Ensure you are on `dev` and it is up to date: `git checkout dev && git pull origin dev`
  2. Create branch: `git checkout -b feature/feat-remove-user-document-number-backend`
  3. Verify: `git branch`
- **Notes**: Do not work directly on `dev`. All changes must be on this branch.

---

### Step 1: Remove `DocumentNumber` from the `User` Entity

- **File**: `src/Abuvi.API/Features/Users/UsersModels.cs`
- **Action**: Delete the `DocumentNumber` property from the `User` class.
- **Implementation Steps**:
  1. Remove the line `public string? DocumentNumber { get; set; }` (currently line 16).
  2. Also remove the inline comment `// NEW FIELDS FOR EMAIL VERIFICATION` above it if it becomes misleading (that comment appears to have been misplaced near `DocumentNumber`).
- **Implementation Notes**: No other model or DTO in this file references `DocumentNumber`. `UserResponse` already does not include it — no change needed there.

---

### Step 2: Remove `GetByDocumentNumberAsync` from the Repository Interface and Implementation

- **File 1**: `src/Abuvi.API/Features/Users/IUsersRepository.cs`
- **Action**: Remove the `GetByDocumentNumberAsync` method signature (lines 18–21) and its XML doc comment.

- **File 2**: `src/Abuvi.API/Features/Users/UsersRepository.cs`
- **Action**: Remove the `GetByDocumentNumberAsync` implementation (lines 25–30).
- **Implementation Notes**: No other caller in the codebase references this method after the AuthService changes in Step 3. Verify with a quick grep for `GetByDocumentNumberAsync` before deleting.

---

### Step 3: Remove `DocumentNumber` from `RegisterUserRequest` and `RegisterUserValidator`

#### 3a — `RegisterUserRequest`

- **File**: `src/Abuvi.API/Features/Auth/RegisterUserRequest.cs`
- **Action**: Remove `string? DocumentNumber` from the record's positional constructor parameters.
- **Before**:
  ```csharp
  public record RegisterUserRequest(
      string Email,
      string Password,
      string FirstName,
      string LastName,
      string? DocumentNumber,
      string? Phone,
      bool AcceptedTerms
  );
  ```
- **After**:
  ```csharp
  public record RegisterUserRequest(
      string Email,
      string Password,
      string FirstName,
      string LastName,
      string? Phone,
      bool AcceptedTerms
  );
  ```

#### 3b — `RegisterUserValidator`

- **File**: `src/Abuvi.API/Features/Auth/RegisterUserValidator.cs`
- **Action**: Remove the `RuleFor(x => x.DocumentNumber)` block entirely (lines 33–36).
- **Implementation Notes**: The remaining rules (Email, Password, FirstName, LastName, Phone, AcceptedTerms) are unchanged.

---

### Step 4: Simplify `AuthService.RegisterUserAsync`

- **File**: `src/Abuvi.API/Features/Auth/AuthService.cs`
- **Action**:
  1. Remove the duplicate-document-number check block (lines 117–124):
     ```csharp
     if (!string.IsNullOrWhiteSpace(request.DocumentNumber))
     {
         var existingByDocument = await _usersRepository.GetByDocumentNumberAsync(request.DocumentNumber, ct);
         if (existingByDocument is not null)
         {
             throw new BusinessRuleException("Ya existe una cuenta con este número de documento");
         }
     }
     ```
  2. Remove the `DocumentNumber = request.DocumentNumber,` assignment in the `User` object initializer (line 141).
- **Implementation Notes**: The `_usersRepository` field is still used for `GetByEmailAsync` and `CreateAsync`, so no constructor change is needed. After removing `GetByDocumentNumberAsync` from `IUsersRepository`, the compiler will catch any remaining references.

---

### Step 5: Simplify the `RegisterUser` Endpoint Error Code in `AuthEndpoints`

- **File**: `src/Abuvi.API/Features/Auth/AuthEndpoints.cs`
- **Action**: Replace the content-sniffing error code logic in the `RegisterUser` handler (lines 127–128) with the fixed code `"EMAIL_EXISTS"`.
- **Before**:
  ```csharp
  var errorCode = ex.Message.Contains("email") ? "EMAIL_EXISTS" : "DOCUMENT_EXISTS";
  ```
- **After**:
  ```csharp
  const string errorCode = "EMAIL_EXISTS";
  ```
- **Implementation Notes**: After Step 4, the only `BusinessRuleException` thrown by `RegisterUserAsync` is the email-exists check. `"DOCUMENT_EXISTS"` is no longer reachable. This also removes the fragile string-sniffing pattern.

---

### Step 6: Remove `DocumentNumber` from EF Core `UserConfiguration`

- **File**: `src/Abuvi.API/Data/Configurations/UserConfiguration.cs`
- **Action**: Remove the `DocumentNumber` property configuration block (lines 52–60):
  ```csharp
  builder.Property(u => u.DocumentNumber)
      .HasMaxLength(50)
      .HasColumnName("document_number");

  builder.HasIndex(u => u.DocumentNumber)
      .IsUnique()
      .HasDatabaseName("IX_Users_DocumentNumber")
      .HasFilter("document_number IS NOT NULL");
  ```
- **Implementation Notes**: Removing these lines makes EF Core unaware of the column, which is required for the migration in Step 7 to correctly generate the `DropIndex` + `DropColumn` operations.

---

### Step 7: Create and Verify the EF Core Migration

- **Action**: Generate the migration that drops the index and column.
- **Command** (run from the repository root):
  ```bash
  dotnet ef migrations add RemoveDocumentNumberFromUsers --project src/Abuvi.API
  ```
- **Implementation Steps**:
  1. Run the command above.
  2. Open the generated migration file in `src/Abuvi.API/Data/Migrations/`.
  3. Verify the `Up` method contains **both**:
     - `migrationBuilder.DropIndex(name: "IX_Users_DocumentNumber", table: "users");`
     - `migrationBuilder.DropColumn(name: "document_number", table: "users");`
  4. Verify the `Down` method re-adds the column and the partial index (EF Core should generate this automatically).
  5. Do **not** apply the migration to a live database during planning; that is a deployment step.
- **Implementation Notes**: If EF Core generates only the `DropIndex` or only the `DropColumn`, check that Step 6 removed both the `Property` and `HasIndex` calls. Both must be absent for the migration to be complete.

---

### Step 8: Remove `DocumentNumber` from `UserImporter`

- **File**: `src/Abuvi.Setup/Importers/UserImporter.cs`
- **Action**: Remove the `DocumentNumber = CsvHelper.Optional(r, "documentNumber"),` line from the `User` object initializer (line 37).
- **Implementation Notes**: The CSV files may still contain a `documentNumber` column — that is fine; `CsvHelper.Optional` will simply not be called for it, and any value in the CSV column will be silently ignored. No change to the CSV format is required.

---

### Step 9: Update All Affected Tests

Work through each test file. The goal is zero compilation errors and zero test failures — do not delete tests unless they test `DocumentNumber` behaviour specifically.

#### 9a — `AuthServiceTests_Registration.cs`

**File**: `src/Abuvi.Tests/Unit/Features/Auth/AuthServiceTests_Registration.cs`

Changes:
1. **`RegisterUserAsync_WithValidRequest_CreatesUserAndSendsEmail`**:
   - Remove `"12345678A"` from the `RegisterUserRequest` constructor call (currently the 5th positional argument). The new record has `Phone` as the 5th parameter and `AcceptedTerms` as the 6th.
   - Remove the mock setup: `_repository.GetByDocumentNumberAsync(request.DocumentNumber!, ...).Returns((User?)null);`
   - In the `Arg.Is<User>` predicate inside `_repository.Received(1).CreateAsync(...)`, remove `u.DocumentNumber == request.DocumentNumber &&`

2. **`RegisterUserAsync_WithDuplicateEmail_ThrowsBusinessRuleException`**:
   - Remove `"12345678A"` from the `RegisterUserRequest` constructor (5th positional arg).
   - Remove `DocumentNumber = "99999999Z"` from the `existingUser` object initializer (the `User` class no longer has this property).

3. **`VerifyEmailAsync_WithValidToken_ActivatesUser`**:
   - Remove `DocumentNumber = "12345678A"` from the `user` object initializer.

4. **`VerifyEmailAsync_WithExpiredToken_ThrowsBusinessRuleException`**:
   - Remove `DocumentNumber = "12345678A"` from the `user` object initializer.

#### 9b — `RegisterUserRequestTests.cs`

**File**: `src/Abuvi.Tests/Unit/Features/Auth/RegisterUserRequestTests.cs`

Changes in `RegisterUserRequest_ShouldBeRecord`:
- Remove `"12345678A"` from the positional constructor (5th arg).
- Remove the assertion `request.DocumentNumber.Should().Be("12345678A");`
- The test retains its value by verifying `Email` and `AcceptedTerms` remain in the record.

#### 9c — `UserTests.cs`

**File**: `src/Abuvi.Tests/Unit/Data/Entities/UserTests.cs`

- Delete the entire `User_DocumentNumber_ShouldAcceptValidFormat` test method (lines 42–56).
- The two remaining tests (`EmailVerified` default and `IsActive` default) are unchanged.

#### 9d — `RegisterUserValidatorTests.cs`

**File**: `src/Abuvi.Tests/Unit/Features/Auth/RegisterUserValidatorTests.cs`

Remove the following tests entirely — they test rules that no longer exist:
- `Validate_WithInvalidDocumentNumber_ShouldFail` (lines 108–129)
- `Validate_WithEmptyDocumentNumber_ShouldPass` (lines 131–152)

For **every remaining test** in this file that constructs a `RegisterUserRequest`, remove the `DocumentNumber` positional argument (`"12345678A"` or `documentNumber` variable). The record no longer has that parameter. Affected tests:
- `Validate_WithValidRequest_ShouldPass`
- `Validate_WithInvalidEmail_ShouldFail`
- `Validate_WithInvalidPassword_ShouldFail`
- `Validate_WithInvalidFirstName_ShouldFail`
- `Validate_WithTermsNotAccepted_ShouldFail`
- `Validate_WithPhone_ShouldValidateFormat`

#### 9e — `UserImporterTests.cs`

**File**: `src/Abuvi.Tests/Unit/Setup/Importers/UserImporterTests.cs`

The CSV strings in the test data include a `documentNumber` column. Since `CsvHelper.Optional` will simply not be called for it (after Step 8), **no test needs to be deleted**. However:
- The tests still pass `documentNumber` values in the CSV headers and rows. That is fine — extra columns are ignored.
- Verify that no test asserts on `user.DocumentNumber` (a quick grep confirms none do in the current file).
- If any assertion on `user.DocumentNumber` exists, remove it.

---

### Step 10: Build and Test Verification

- **Action**: Ensure the project compiles with zero errors and zero warnings, and all tests pass.
- **Commands**:
  ```bash
  dotnet build src/Abuvi.API
  dotnet build src/Abuvi.Setup
  dotnet test src/Abuvi.Tests
  ```
- **Implementation Notes**:
  - `TreatWarningsAsErrors` is enabled in the project files. Any warning (e.g., unused variable, obsolete member) will fail the build.
  - If EF Core migrations produce a build warning about shadow properties, re-check Step 6.

---

### Step 11: Update Technical Documentation

- **Action**: Update documentation to reflect the removal.
- **File**: `ai-specs/specs/data-model.md`
  - Find the `Users` table section and remove `document_number` from the column list.
- **Implementation Steps**:
  1. Open `ai-specs/specs/data-model.md`.
  2. Locate the `Users` entity section.
  3. Remove the `document_number` row (and any note about `IX_Users_DocumentNumber`).
  4. Save.
- **Notes**: All documentation must be in English. This step is **mandatory**.

---

## Implementation Order

1. **Step 0** — Create feature branch
2. **Step 1** — Remove `DocumentNumber` from `User` entity
3. **Step 2** — Remove `GetByDocumentNumberAsync` from `IUsersRepository` and `UsersRepository`
4. **Step 3** — Remove `DocumentNumber` from `RegisterUserRequest` and `RegisterUserValidator`
5. **Step 4** — Remove duplicate-document check and assignment from `AuthService`
6. **Step 5** — Simplify error code in `AuthEndpoints`
7. **Step 6** — Remove EF Core config from `UserConfiguration`
8. **Step 7** — Generate and verify migration `RemoveDocumentNumberFromUsers`
9. **Step 8** — Remove `DocumentNumber` from `UserImporter`
10. **Step 9** — Update all tests (9a → 9e); build after each file to catch positional argument errors early
11. **Step 10** — Full build + test run; fix any remaining issues
12. **Step 11** — Update `ai-specs/specs/data-model.md`

---

## Testing Checklist

- [ ] `AuthServiceTests_Registration.cs` — all 4 tests pass, no reference to `DocumentNumber` or `GetByDocumentNumberAsync`
- [ ] `RegisterUserRequestTests.cs` — test verifies record immutability without `DocumentNumber`
- [ ] `UserTests.cs` — 2 tests remain; `User_DocumentNumber_ShouldAcceptValidFormat` is deleted
- [ ] `RegisterUserValidatorTests.cs` — `DocumentNumber` validator tests deleted; all remaining tests construct `RegisterUserRequest` without the removed parameter
- [ ] `UserImporterTests.cs` — all 6 tests pass; no assertion on `DocumentNumber`
- [ ] `dotnet build` — zero errors, zero warnings
- [ ] `dotnet test` — all tests green

---

## Error Response Format

The `RegisterUser` endpoint still returns `ApiResponse<UserResponse>` on both success and failure:

```json
// 200 OK — success
{ "success": true, "data": { "id": "...", "email": "...", ... }, "error": null }

// 400 Bad Request — email already registered
{ "success": false, "data": null, "error": { "message": "Ya existe una cuenta con este correo electrónico", "code": "EMAIL_EXISTS" } }
```

`"DOCUMENT_EXISTS"` is permanently removed. Any frontend logic that handled this error code can be safely deleted.

---

## Dependencies

No new NuGet packages required.

**Migration commands:**
```bash
# Generate
dotnet ef migrations add RemoveDocumentNumberFromUsers --project src/Abuvi.API

# Apply (only on non-production environments during development)
dotnet ef database update --project src/Abuvi.API

# Generate idempotent SQL script for production deployment
dotnet ef migrations script --idempotent --project src/Abuvi.API
```

---

## Notes

- **`FamilyMember.DocumentNumber` is unchanged.** Do not touch `FamilyMembersModels.cs`, `FamilyMembersRepository`, or any `Guest`/`RegistrationMember` related fields.
- **Non-breaking HTTP change.** Existing API clients that send `documentNumber` in the JSON body will have it silently ignored by the JSON deserializer — no HTTP 400 is returned.
- **Destructive migration.** The spec confirms the column is empty in production. Apply via the idempotent script during the next deployment window.
- **Positional record constructors.** `RegisterUserRequest` is a positional record. Removing `DocumentNumber` shifts `Phone` from position 6 to position 5 and `AcceptedTerms` from position 7 to position 6. Every constructor call site in tests must be updated accordingly — compile errors will point directly to them.
- **`UserImporter` CSV format.** After removing the `DocumentNumber` mapping, CSVs that still contain a `documentNumber` column continue to work — the extra column is simply unused. No migration of existing import files is needed.
- **Branch target**: PR must target `dev`, not `main`.

---

## Next Steps After Implementation

- Frontend team removes `documentNumber` from `RegistrationForm.vue` and drops the `"DOCUMENT_EXISTS"` error handler (tracked in the frontend spec).
- Schedule the `RemoveDocumentNumberFromUsers` migration to run in the next production deployment.

---

## Implementation Verification

- [ ] **Code Quality**: No C# analyzer warnings; nullable reference types respected throughout; no `#pragma warning disable` suppressions added
- [ ] **Functionality**: `POST /api/auth/register-user` returns `200` for valid requests and `400` with `"EMAIL_EXISTS"` for duplicate emails; `"DOCUMENT_EXISTS"` is never returned
- [ ] **Testing**: All 5 affected test files compile and all tests pass with zero failures
- [ ] **Migration**: Generated migration file contains `DropIndex("IX_Users_DocumentNumber")` and `DropColumn("document_number", "users")` in `Up()`; `Down()` re-creates both
- [ ] **Documentation**: `ai-specs/specs/data-model.md` updated to remove `document_number` from the Users table definition
