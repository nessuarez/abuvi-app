# Backend Implementation Plan: feat-registration-has-pet — Añadir campo "viene con mascota"

## Overview

Add a boolean `HasPet` field to the `Registration` entity to track whether a family unit will attend camp with a pet. This is a simple, additive change that follows the exact same pattern used for `SpecialNeeds` and `CampatesPreference` — but as a boolean instead of a string.

**Architecture**: Vertical Slice — all changes are within the existing `Registrations` feature slice.

## Architecture Context

- **Feature slice**: `src/Abuvi.API/Features/Registrations/`
- **Files to modify** (no new files needed):
  - `RegistrationsModels.cs` — Entity + DTOs + Mapping
  - `RegistrationsService.cs` — Create + AdminEdit mapping
  - `RegistrationConfiguration.cs` (in `Data/Configurations/`)
  - `CreateRegistrationValidator.cs` — No validation needed for bool, but review
  - `AdminEditRegistrationValidator.cs` — No validation needed for bool?, but review
- **New file**:
  - `Migrations/` — New migration `AddHasPetToRegistrations`
- **Cross-cutting**: None. No middleware, filters, or shared types affected.

## Implementation Steps

### Step 0: Create Feature Branch

- **Action**: Create and switch to a new feature branch
- **Branch Naming**: `feature/feat-registration-has-pet-backend`
- **Implementation Steps**:
  1. Ensure you're on the latest `dev` branch
  2. Pull latest changes: `git pull origin dev`
  3. Create new branch: `git checkout -b feature/feat-registration-has-pet-backend`
  4. Verify branch creation: `git branch`
- **Notes**: PRs target `dev`, not `main`.

### Step 1: Update Entity — Add `HasPet` property

- **File**: `src/Abuvi.API/Features/Registrations/RegistrationsModels.cs`
- **Action**: Add `HasPet` boolean property to the `Registration` class
- **Implementation Steps**:
  1. At line 22, after `CampatesPreference`, add:
     ```csharp
     public bool HasPet { get; set; } = false;
     ```
- **Notes**: Default `false` matches the migration default. Place it alongside the other "extra fields from Google Forms 2026" block (lines 20-22).

### Step 2: Update EF Core Configuration

- **File**: `src/Abuvi.API/Data/Configurations/RegistrationConfiguration.cs`
- **Action**: Configure the `has_pet` column mapping
- **Implementation Steps**:
  1. After line 33 (the `CampatesPreference` configuration), add:
     ```csharp
     builder.Property(r => r.HasPet)
         .HasDefaultValue(false)
         .HasColumnName("has_pet");
     ```
- **Notes**: Boolean, not null, default false. No index needed.

### Step 3: Update `CreateRegistrationRequest` DTO

- **File**: `src/Abuvi.API/Features/Registrations/RegistrationsModels.cs`
- **Action**: Add `HasPet` parameter to the create request record
- **Implementation Steps**:
  1. At lines 136-143, update the record to add `HasPet` with a default value:
     ```csharp
     public record CreateRegistrationRequest(
         Guid CampEditionId,
         Guid FamilyUnitId,
         List<MemberAttendanceRequest> Members,
         string? Notes,
         string? SpecialNeeds,
         string? CampatesPreference,
         bool HasPet = false
     );
     ```
- **Notes**: Default `false` makes it backward-compatible — existing API clients that don't send `hasPet` will default to `false`.

### Step 4: Update `AdminEditRegistrationRequest` DTO

- **File**: `src/Abuvi.API/Features/Registrations/RegistrationsModels.cs`
- **Action**: Add `HasPet` nullable parameter to admin edit request
- **Implementation Steps**:
  1. At lines 336-343, update the record:
     ```csharp
     public record AdminEditRegistrationRequest(
         List<MemberAttendanceRequest>? Members,
         List<ExtraSelectionRequest>? Extras,
         List<AccommodationPreferenceRequest>? Preferences,
         string? Notes,
         string? SpecialNeeds,
         string? CampatesPreference,
         bool? HasPet
     );
     ```
- **Notes**: Nullable `bool?` to support partial updates — `null` means "don't change", `true`/`false` means "set to this value". This follows the same pattern as the other nullable fields in this DTO.

### Step 5: Update `RegistrationResponse` DTO

- **File**: `src/Abuvi.API/Features/Registrations/RegistrationsModels.cs`
- **Action**: Add `HasPet` to the response record
- **Implementation Steps**:
  1. At lines 198-213, add `HasPet` before `IsAdminModified`:
     ```csharp
     public record RegistrationResponse(
         Guid Id,
         RegistrationFamilyUnitSummary FamilyUnit,
         RegistrationCampEditionSummary CampEdition,
         RegistrationStatus Status,
         string? Notes,
         PricingBreakdown Pricing,
         List<PaymentSummary> Payments,
         decimal AmountPaid,
         decimal AmountRemaining,
         DateTime CreatedAt,
         DateTime UpdatedAt,
         string? SpecialNeeds,
         string? CampatesPreference,
         bool HasPet,
         bool IsAdminModified
     );
     ```

### Step 6: Update `ToResponse()` Mapping Extension

- **File**: `src/Abuvi.API/Features/Registrations/RegistrationsModels.cs`
- **Action**: Include `HasPet` in the `ToResponse()` mapping
- **Implementation Steps**:
  1. At lines 349-395, in the `ToResponse` method, add `r.HasPet` after `r.CampatesPreference` (line 393) and before the `IsAdminModified` expression:
     ```csharp
     r.SpecialNeeds,
     r.CampatesPreference,
     r.HasPet,
     r.AdminModifiedAt != null && r.Status == RegistrationStatus.Draft
     ```
- **Notes**: The record constructor is positional, so the order must match the updated `RegistrationResponse` definition.

### Step 7: Update Service — `CreateAsync` mapping

- **File**: `src/Abuvi.API/Features/Registrations/RegistrationsService.cs`
- **Action**: Map `HasPet` from request to entity in the create flow
- **Implementation Steps**:
  1. At line 143, after `CampatesPreference = request.CampatesPreference,` add:
     ```csharp
     HasPet = request.HasPet,
     ```

### Step 8: Update Service — `AdminEditAsync` mapping

- **File**: `src/Abuvi.API/Features/Registrations/RegistrationsService.cs`
- **Action**: Map `HasPet` from admin edit request when provided
- **Implementation Steps**:
  1. At line 785, after the `CampatesPreference` check, add:
     ```csharp
     if (request.HasPet != null) registration.HasPet = request.HasPet.Value;
     ```
- **Notes**: Only update if `HasPet` is not null (partial update pattern consistent with `Notes`, `SpecialNeeds`, `CampatesPreference`).

### Step 9: Update Email Data (if applicable)

- **File**: `src/Abuvi.API/Features/Registrations/RegistrationsService.cs`
- **Action**: Review email template data mapping around line 894
- **Implementation Steps**:
  1. Check if the confirmation email template needs to include pet info
  2. If the email template should show pet status, add `HasPet = registration.HasPet` to the email data object
  3. If not needed in email, skip this step
- **Notes**: This is optional — depends on whether the email template should mention the pet. For MVP, skip this unless explicitly required.

### Step 10: Create EF Core Migration

- **Action**: Generate a new migration for the `has_pet` column
- **Implementation Steps**:
  1. From the solution root, run:
     ```bash
     dotnet ef migrations add AddHasPetToRegistrations --project src/Abuvi.API
     ```
  2. Verify the generated migration contains:
     ```csharp
     migrationBuilder.AddColumn<bool>(
         name: "has_pet",
         table: "registrations",
         type: "boolean",
         nullable: false,
         defaultValue: false);
     ```
  3. Apply the migration to verify:
     ```bash
     dotnet ef database update --project src/Abuvi.API
     ```
- **Notes**: The `defaultValue: false` ensures all existing registrations get `has_pet = false` without breaking anything.

### Step 11: Verify Build

- **Action**: Build the project to ensure no compilation errors
- **Implementation Steps**:
  1. Run: `dotnet build src/Abuvi.API`
  2. Fix any compilation errors (likely from positional record constructor mismatches)
- **Notes**: The `RegistrationResponse` record is positional, so any code creating it manually (outside `ToResponse()`) will need the new `HasPet` parameter added in the correct position.

### Step 12: Update Technical Documentation

- **Action**: Review and update technical documentation according to changes made
- **Implementation Steps**:
  1. **Data model** → Update `ai-specs/specs/data-model.md`:
     - Add `hasPet` field to the `Registration` entity documentation
     - Description: "Whether the family unit will attend camp with a pet (required, default: false)"
  2. **API spec** → Update `ai-specs/specs/api-endpoints.md`:
     - Add `hasPet: boolean` to `POST /api/registrations/` request body docs
     - Add `hasPet: boolean` to `GET /api/registrations/{id}` response docs
     - Add `hasPet: boolean | null` to `PUT /api/registrations/{id}/admin-edit` request body docs
  3. Verify documentation follows existing structure and is written in English
- **References**: Follow `ai-specs/specs/documentation-standards.mdc`
- **Notes**: This step is MANDATORY before considering the implementation complete.

## Implementation Order

1. **Step 0**: Create feature branch `feature/feat-registration-has-pet-backend`
2. **Step 1**: Add `HasPet` property to `Registration` entity
3. **Step 2**: Update EF Core configuration
4. **Step 3**: Update `CreateRegistrationRequest` DTO
5. **Step 4**: Update `AdminEditRegistrationRequest` DTO
6. **Step 5**: Update `RegistrationResponse` DTO
7. **Step 6**: Update `ToResponse()` mapping extension
8. **Step 7**: Update `CreateAsync` in service
9. **Step 8**: Update `AdminEditAsync` in service
10. **Step 9**: Review email data (optional)
11. **Step 10**: Create EF Core migration
12. **Step 11**: Verify build
13. **Step 12**: Update technical documentation

## Testing Checklist

Since this is a simple boolean field addition with no business logic, testing is lightweight:

- [ ] **Build passes** with no errors
- [ ] **Migration applies** successfully (`dotnet ef database update`)
- [ ] **POST /api/registrations/** — sending `hasPet: true` persists correctly
- [ ] **POST /api/registrations/** — omitting `hasPet` defaults to `false`
- [ ] **GET /api/registrations/{id}** — response includes `hasPet` field
- [ ] **PUT /api/registrations/{id}/admin-edit** — sending `hasPet: true` updates the field
- [ ] **PUT /api/registrations/{id}/admin-edit** — omitting `hasPet` (null) doesn't change existing value
- [ ] **Existing registrations** — all have `has_pet = false` after migration

## Error Response Format

No new error responses. The `HasPet` boolean field:
- Cannot fail validation (any JSON boolean is valid)
- Has a safe default (`false`)
- Uses the existing `ApiResponse<T>` envelope for all responses

## Dependencies

- No new NuGet packages required
- EF Core migration command:
  ```bash
  dotnet ef migrations add AddHasPetToRegistrations --project src/Abuvi.API
  dotnet ef database update --project src/Abuvi.API
  ```

## Notes

- **Backward compatibility**: The default value (`false`) on both the DTO and the database column ensures existing clients and data are unaffected.
- **No RGPD/GDPR concerns**: Pet ownership is not sensitive personal data.
- **No index needed**: Filtering by `has_pet` is not an expected query pattern.
- **Scope**: Backend only. Frontend changes are tracked in a separate ticket/plan.
- **Language**: All user-facing validation messages should be in Spanish (per project convention), but there are no new validation rules for this field.
