# Spec: Remove DocumentNumber from User Entity

## Problem

`User.DocumentNumber` is a redundant field. The account holder is always also a `FamilyMember`, and their document number is already stored (and validated) on the `FamilyMember` entity. Confirmed: no rows in the `Users` table have a non-null `document_number` value in production. Keeping the field creates two sources of truth for the same piece of data and exposes it unnecessarily through the registration API.

## Goal

Remove `DocumentNumber` entirely from the `User` entity, API DTOs, validation, service logic, EF Core configuration, and the CSV importer. Drop the database column and index in a migration. Update all affected tests.

This change does **not** touch `FamilyMember.DocumentNumber`, `Guest.DocumentNumber`, or `RegistrationMember.GuardianDocumentNumber` — those fields remain unchanged.

---

## Scope

### Backend

| File | Change |
|---|---|
| `src/Abuvi.API/Features/Users/UsersModels.cs` | Remove `string? DocumentNumber` from `User` class |
| `src/Abuvi.API/Features/Users/IUsersRepository.cs` | Remove `GetByDocumentNumberAsync` method signature |
| `src/Abuvi.API/Features/Users/UsersRepository.cs` | Remove `GetByDocumentNumberAsync` implementation |
| `src/Abuvi.API/Features/Auth/RegisterUserRequest.cs` | Remove `string? DocumentNumber` parameter |
| `src/Abuvi.API/Features/Auth/RegisterUserValidator.cs` | Remove `DocumentNumber` validation rule |
| `src/Abuvi.API/Features/Auth/AuthService.cs` | Remove duplicate-document check + `DocumentNumber` assignment in `RegisterUserAsync` |
| `src/Abuvi.API/Features/Auth/AuthEndpoints.cs` | Simplify error code — always `"EMAIL_EXISTS"`, no `"DOCUMENT_EXISTS"` |
| `src/Abuvi.API/Data/Configurations/UserConfiguration.cs` | Remove `DocumentNumber` property config and `IX_Users_DocumentNumber` index |
| `src/Abuvi.Setup/Importers/UserImporter.cs` | Remove `DocumentNumber` CSV mapping |

### Database Migration

Create a new EF Core migration to:
1. Drop index `IX_Users_DocumentNumber` on the `Users` table
2. Drop column `document_number` from the `Users` table

Command: `dotnet ef migrations add RemoveDocumentNumberFromUsers --project src/Abuvi.API`

### Frontend

| File | Change |
|---|---|
| `src/Abuvi.Web/src/components/auth/RegistrationForm.vue` | Remove `documentNumber` field from the registration form, remove `validateDocumentNumber` function |

### Tests to update

| File | Change |
|---|---|
| `src/Abuvi.Tests/Unit/Features/Auth/AuthServiceTests_Registration.cs` | Remove `DocumentNumber` from `RegisterUserRequest` instantiations; remove `GetByDocumentNumberAsync` mock setup; remove `DocumentNumber` assertion |
| `src/Abuvi.Tests/Unit/Features/Auth/RegisterUserRequestTests.cs` | Remove `DocumentNumber` param and assertion |
| `src/Abuvi.Tests/Unit/Data/Entities/UserTests.cs` | Delete `User_DocumentNumber_ShouldAcceptValidFormat` test method |
| `src/Abuvi.Tests/Unit/Features/Auth/RegisterUserValidatorTests.cs` | Remove any tests for `DocumentNumber` validation rules |
| `src/Abuvi.Tests/Unit/Setup/Importers/UserImporterTests.cs` | Remove any test assertions on `DocumentNumber` |

---

## Implementation Order

1. Remove `DocumentNumber` from `User` entity, `IUsersRepository`, `UsersRepository`
2. Remove from `RegisterUserRequest` and `RegisterUserValidator`
3. Remove logic from `AuthService.RegisterUserAsync` and simplify `AuthEndpoints` error code
4. Remove EF Core configuration from `UserConfiguration`
5. Create and apply migration: `RemoveDocumentNumberFromUsers`
6. Update `UserImporter`
7. Update all affected tests — build must pass with zero errors and zero warnings
8. Update frontend registration form

---

## Notes

- No change to `FamilyMember.DocumentNumber` — that is the correct place for a person's document number.
- After this change, `RegisterUserRequest` will no longer accept a `documentNumber` field. Existing API clients that send it will have the field silently ignored by the JSON deserializer (no breaking HTTP error).
- The `"DOCUMENT_EXISTS"` error code in `AuthEndpoints` is currently reachable and returned to the frontend. The frontend `RegistrationForm.vue` must be updated to no longer send or display the field, and any client-side error handling for `"DOCUMENT_EXISTS"` can be removed.
- This is a destructive migration. Confirm the column is empty in production before deploying (the precondition has already been verified).
