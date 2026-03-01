# Backend Implementation Plan: feat-registration-extra-fields2 — Registration Guardian & Preference Fields

## Overview

This feature extends the existing `Registration` and `RegistrationMember` entities with a subset of additional fields collected by the Google Forms registration flow for "El Clar del Bosc 2026". It adds 2 family-level fields to `Registration` and 2 per-member guardian fields to `RegistrationMember`, updates request/response DTOs, validators, service mapping, EF Core configurations, and creates a new migration.

**Architecture**: Vertical Slice Architecture — all changes are scoped to `src/Abuvi.API/Features/Registrations/`.

**Prerequisite**: `feat-registration-attendance-period` is **already merged** (confirmed: `MemberAttendanceRequest` already includes `AttendancePeriod`, `VisitStartDate`, `VisitEndDate`).

### Scope Reduction

The following fields from the original Google Forms spec were **extracted to separate tickets**:

| Field | Extracted To | Reason |
|-------|-------------|--------|
| `AccommodationPreferences` | `feat-registration-accommodations` | Needs structured model for family placement logic |
| `VegetarianCount` | `feat-camp-edition-extras-registration` | Generic extras system (existing `CampEditionExtra` entity) |
| `NeedsTruck` | `feat-camp-edition-extras-registration` | Generic extras system (existing `CampEditionExtra` entity) |
| `Activities` | `feat-registration-activities` | Needs structured model with conditions per edition |

**This ticket only implements**: `SpecialNeeds`, `CampatesPreference`, `GuardianName`, `GuardianDocumentNumber`.

---

## Architecture Context

- **Feature slice**: `src/Abuvi.API/Features/Registrations/`
- **Files to modify**:
  - `RegistrationsModels.cs` — entities, DTOs, mapping extensions
  - `RegistrationsService.cs` — field mapping in `CreateAsync` and `UpdateMembersAsync`
  - `CreateRegistrationValidator.cs` — new validation rules
- **Configuration files to modify**:
  - `src/Abuvi.API/Data/Configurations/RegistrationConfiguration.cs`
  - `src/Abuvi.API/Data/Configurations/RegistrationMemberConfiguration.cs`
- **Test files to modify/create**:
  - `src/Abuvi.Tests/Unit/Features/Registrations/CreateRegistrationValidatorTests.cs`
  - `src/Abuvi.Tests/Unit/Features/Registrations/RegistrationsServiceTests.cs`
  - `src/Abuvi.Tests/Helpers/Builders/RegistrationBuilder.cs`
- **New files**: Migration file (auto-generated)
- **Cross-cutting concerns**: None affected

---

## Implementation Steps

### Step 0: Create Feature Branch

- **Action**: Create and switch to a backend-specific feature branch
- **Branch Naming**: `feature/feat-registration-extra-fields2-backend`
- **Implementation Steps**:
  1. Ensure you're on the latest `main` branch
  2. Pull latest changes: `git pull origin main`
  3. Create new branch: `git checkout -b feature/feat-registration-extra-fields2-backend`
  4. Verify branch creation: `git branch`
- **Notes**: This must be the FIRST step before any code changes.

---

### Step 1: Extend `Registration` Entity with 2 New Fields

- **File**: `src/Abuvi.API/Features/Registrations/RegistrationsModels.cs`
- **Action**: Add 2 new properties to the `Registration` class, before the navigation properties section (before `// Navigation properties`)

**Code to add** (after `Notes` property, before navigation properties):

```csharp
// Extra fields from Google Forms 2026
public string? SpecialNeeds { get; set; }
public string? CampatesPreference { get; set; }
```

- **Implementation Notes**:
  - Both fields are nullable — they are optional by business logic
  - No explicit default values needed on properties — EF config handles DB defaults

---

### Step 2: Extend `RegistrationMember` Entity with 2 Guardian Fields

- **File**: `src/Abuvi.API/Features/Registrations/RegistrationsModels.cs`
- **Action**: Add 2 new guardian properties to the `RegistrationMember` class, after `VisitEndDate`, before `CreatedAt`

**Code to add**:

```csharp
// Guardian info (only meaningful for minors: AgeCategory Baby or Child)
public string? GuardianName { get; set; }
public string? GuardianDocumentNumber { get; set; }
```

---

### Step 3: Extend `MemberAttendanceRequest` DTO with Guardian Fields

- **File**: `src/Abuvi.API/Features/Registrations/RegistrationsModels.cs`
- **Action**: Add guardian fields to the existing `MemberAttendanceRequest` record

**Current code**:

```csharp
public record MemberAttendanceRequest(
    Guid MemberId,
    AttendancePeriod AttendancePeriod,
    DateOnly? VisitStartDate = null,
    DateOnly? VisitEndDate = null
);
```

**Replace with**:

```csharp
public record MemberAttendanceRequest(
    Guid MemberId,
    AttendancePeriod AttendancePeriod,
    DateOnly? VisitStartDate = null,
    DateOnly? VisitEndDate = null,
    string? GuardianName = null,
    string? GuardianDocumentNumber = null
);
```

- **Implementation Notes**: Guardian fields are nullable because they only apply to minors (AgeCategory Baby or Child).

---

### Step 4: Extend `CreateRegistrationRequest` DTO with 2 New Fields

- **File**: `src/Abuvi.API/Features/Registrations/RegistrationsModels.cs`
- **Action**: Extend the `CreateRegistrationRequest` record

**Current code**:

```csharp
public record CreateRegistrationRequest(
    Guid CampEditionId,
    Guid FamilyUnitId,
    List<MemberAttendanceRequest> Members,
    string? Notes
);
```

**Replace with**:

```csharp
public record CreateRegistrationRequest(
    Guid CampEditionId,
    Guid FamilyUnitId,
    List<MemberAttendanceRequest> Members,
    string? Notes,
    string? SpecialNeeds,
    string? CampatesPreference
);
```

- **Implementation Notes**: No backward compatibility needed — application is not in production. All existing callers and tests must be updated to include the new fields.

---

### Step 5: Extend `RegistrationResponse` DTO with 2 New Fields

- **File**: `src/Abuvi.API/Features/Registrations/RegistrationsModels.cs`
- **Action**: Extend the `RegistrationResponse` record

Add 2 new fields after `UpdatedAt`:

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
    string? CampatesPreference
);
```

---

### Step 6: Extend `MemberPricingDetail` DTO with Guardian Fields

- **File**: `src/Abuvi.API/Features/Registrations/RegistrationsModels.cs`
- **Action**: Extend the `MemberPricingDetail` record

Add 2 new fields after `IndividualAmount`:

```csharp
public record MemberPricingDetail(
    Guid FamilyMemberId,
    string FullName,
    int AgeAtCamp,
    AgeCategory AgeCategory,
    AttendancePeriod AttendancePeriod,
    int AttendanceDays,
    DateOnly? VisitStartDate,
    DateOnly? VisitEndDate,
    decimal IndividualAmount,
    string? GuardianName,
    string? GuardianDocumentNumber
);
```

---

### Step 7: Update `RegistrationMappingExtensions.ToResponse`

- **File**: `src/Abuvi.API/Features/Registrations/RegistrationsModels.cs`
- **Action**: Update the `ToResponse` method to include the new fields

**7a. Update `MemberPricingDetail` projection** (inside `r.Members.Select(m => new MemberPricingDetail(...))`):

Add these two fields after `m.IndividualAmount`:

```csharp
m.GuardianName,
m.GuardianDocumentNumber
```

**7b. Update `RegistrationResponse` construction** — add 2 new fields after `r.UpdatedAt`:

```csharp
r.SpecialNeeds,
r.CampatesPreference
```

---

### Step 8: Update EF Core Configuration for `Registration`

- **File**: `src/Abuvi.API/Data/Configurations/RegistrationConfiguration.cs`
- **Action**: Add 2 new property mappings inside `Configure(EntityTypeBuilder<Registration> builder)`, after the `Notes` property mapping

**Code to add**:

```csharp
builder.Property(r => r.SpecialNeeds)
    .HasMaxLength(2000).HasColumnName("special_needs");
builder.Property(r => r.CampatesPreference)
    .HasMaxLength(500).HasColumnName("campates_preference");
```

---

### Step 9: Update EF Core Configuration for `RegistrationMember`

- **File**: `src/Abuvi.API/Data/Configurations/RegistrationMemberConfiguration.cs`
- **Action**: Add 2 new property mappings inside `Configure(EntityTypeBuilder<RegistrationMember> builder)`, after the `VisitEndDate` property mapping

**Code to add**:

```csharp
builder.Property(m => m.GuardianName)
    .HasMaxLength(200).HasColumnName("guardian_name");
builder.Property(m => m.GuardianDocumentNumber)
    .HasMaxLength(50).HasColumnName("guardian_document_number");
```

---

### Step 10: Update `RegistrationsService.CreateAsync` — Map New Fields

- **File**: `src/Abuvi.API/Features/Registrations/RegistrationsService.cs`
- **Action**: Map the 2 new family-level fields when building the `Registration` object, and the 2 guardian fields when building each `RegistrationMember`

**10a. In `CreateAsync`, inside the `Registration` object initializer** (before `Members = registrationMembers`), add:

```csharp
SpecialNeeds = request.SpecialNeeds,
CampatesPreference = request.CampatesPreference,
```

**10b. In `CreateAsync`, inside the `RegistrationMember` builder**, add guardian fields after `VisitEndDate = m.VisitEndDate,`:

```csharp
GuardianName = m.GuardianName,
GuardianDocumentNumber = m.GuardianDocumentNumber,
```

**10c. In `UpdateMembersAsync`, inside the `RegistrationMember` builder**, add the same guardian fields after `VisitEndDate = m.VisitEndDate,`:

```csharp
GuardianName = m.GuardianName,
GuardianDocumentNumber = m.GuardianDocumentNumber,
```

- **Implementation Notes**:
  - `UpdateMembersAsync` does NOT update the family-level extra fields (those are set during registration creation only). If updating them is needed later, a separate endpoint or extension would be required.
  - Privacy: `GuardianDocumentNumber` must NOT appear in log messages. The current `logger.LogInformation` calls only log registration IDs, which is correct.

---

### Step 11: Update `CreateRegistrationValidator` — Add New Validation Rules

- **File**: `src/Abuvi.API/Features/Registrations/CreateRegistrationValidator.cs`
- **Action**: Add validation rules for the 2 new family-level fields and 2 guardian fields

**11a. Add family-level validation rules** — add after the `Notes` rule, before the closing `}` of the constructor:

```csharp
RuleFor(x => x.SpecialNeeds)
    .MaximumLength(2000)
    .WithMessage("Las necesidades especiales no pueden superar los 2000 caracteres")
    .When(x => x.SpecialNeeds is not null);

RuleFor(x => x.CampatesPreference)
    .MaximumLength(500)
    .WithMessage("La preferencia de acampantes no puede superar los 500 caracteres")
    .When(x => x.CampatesPreference is not null);
```

**11b. Add guardian field validation** — add inside the existing `RuleForEach(x => x.Members).ChildRules(member => { ... })` block, after the weekend visit date rules:

```csharp
member.RuleFor(m => m.GuardianName)
    .MaximumLength(200)
    .WithMessage("El nombre del tutor no puede superar los 200 caracteres")
    .When(m => m.GuardianName is not null);

member.RuleFor(m => m.GuardianDocumentNumber)
    .MaximumLength(50)
    .WithMessage("El documento del tutor no puede superar los 50 caracteres")
    .When(m => m.GuardianDocumentNumber is not null);
```

- **Implementation Notes**:
  - Guardian info is optional at submission time (age category is only known after DB lookup in service layer).

---

### Step 12: Create EF Core Migration

- **Action**: Generate and apply the migration

```bash
dotnet ef migrations add AddRegistrationGuardianAndPreferenceFields --project src/Abuvi.API
dotnet ef database update --project src/Abuvi.API
```

**Expected SQL**:

- `ALTER TABLE registrations ADD COLUMN special_needs VARCHAR(2000) NULL`
- `ALTER TABLE registrations ADD COLUMN campates_preference VARCHAR(500) NULL`
- `ALTER TABLE registration_members ADD COLUMN guardian_name VARCHAR(200) NULL`
- `ALTER TABLE registration_members ADD COLUMN guardian_document_number VARCHAR(50) NULL`

---

### Step 13: Write Unit Tests — Validator

- **File**: `src/Abuvi.Tests/Unit/Features/Registrations/CreateRegistrationValidatorTests.cs`
- **Action**: Add tests for all new validation rules

**Tests to add** (follow existing pattern — create `CreateRegistrationRequest` with an open edition mock):

```csharp
// SpecialNeeds validation
[Fact] SpecialNeeds_WhenExceeds2000Chars_ShouldFail

// CampatesPreference validation
[Fact] CampatesPreference_WhenExceeds500Chars_ShouldFail

// Guardian field validation (per-member)
[Fact] GuardianName_WhenExceeds200Chars_ShouldFail
[Fact] GuardianDocumentNumber_WhenExceeds50Chars_ShouldFail
```

**Pattern for each test** (follow existing `CreateRegistrationValidatorTests` style):

1. Create an open edition mock
2. Build a `CreateRegistrationRequest` with the field under test set to an invalid value
3. Call `_sut.TestValidateAsync(request)`
4. Assert with `result.ShouldHaveValidationErrorFor(x => x.FieldName).WithErrorMessage(...)`

**Important**: The existing tests build requests with the 4-field constructor `CreateRegistrationRequest(editionId, familyUnitId, members, notes)`. All existing tests must be updated to include the 2 new fields in their constructor calls. No backward compatibility needed — application is not in production.

---

### Step 14: Write Unit Tests — Service Mapping

- **File**: `src/Abuvi.Tests/Unit/Features/Registrations/RegistrationsServiceTests.cs`
- **Action**: Add tests verifying the new fields are correctly mapped in `CreateAsync`

**Tests to add**:

```csharp
[Fact] CreateAsync_WithSpecialNeeds_PersistsValue
// Verify: registration.SpecialNeeds == "Wheelchair access needed"

[Fact] CreateAsync_WithCampatesPreference_PersistsValue
// Verify: registration.CampatesPreference == "Near family Garcia"

[Fact] CreateAsync_WithGuardianInfoOnMember_PersistsGuardianFields
// Verify: captured RegistrationMember.GuardianName and .GuardianDocumentNumber match request

[Fact] CreateAsync_WithNullOptionalFields_UsesDefaults
// Verify: all new fields are null when not supplied

[Fact] UpdateMembersAsync_WithGuardianInfo_PersistsOnNewMembers
// Verify: guardian fields are mapped in UpdateMembersAsync
```

**Pattern** (follow existing `CreateAsync_WhenEditionOpen_CreatesRegistrationWithCorrectPricing` style):

1. Set up mocks (familyUnit, edition, member, repository)
2. Capture the `Registration` passed to `_repo.AddAsync(...)` using `Arg.Do<Registration>(r => captured = r)`
3. Call `_sut.CreateAsync(userId, request, ct)`
4. Assert captured registration's new field values

---

### Step 15: Update `RegistrationBuilder` for Tests

- **File**: `src/Abuvi.Tests/Helpers/Builders/RegistrationBuilder.cs`
- **Action**: Add builder methods for the 2 new `Registration` fields so other tests can easily set them

**Add private fields** (after `_status` field):

```csharp
private string? _specialNeeds;
private string? _campatesPreference;
```

**Add builder methods**:

```csharp
public RegistrationBuilder WithSpecialNeeds(string? value) { _specialNeeds = value; return this; }
public RegistrationBuilder WithCampatesPreference(string? value) { _campatesPreference = value; return this; }
```

**Update `Build()` method** — add inside the `Registration` object initializer (after `Status = _status,`):

```csharp
SpecialNeeds = _specialNeeds,
CampatesPreference = _campatesPreference,
```

---

### Step 16: Update Technical Documentation

- **Action**: Review and update technical documentation according to changes made
- **Implementation Steps**:
  1. **Update `ai-specs/specs/data-model.md`**: Add the 2 new `Registration` fields and 2 new `RegistrationMember` fields to the Registration and RegistrationMember entity documentation.
  2. **Update `ai-specs/specs/api-spec.yml`** (if it exists): Update `CreateRegistrationRequest` schema with 2 new fields. Update `MemberAttendanceRequest` schema with 2 guardian fields. Update `RegistrationResponse` and `MemberPricingDetail` schemas.
  3. Verify auto-generated OpenAPI matches.
- **Notes**: All documentation must be written in English. This step is MANDATORY.

---

## Implementation Order

1. **Step 0**: Create feature branch `feature/feat-registration-extra-fields2-backend`
2. **Step 1**: Add 2 new fields to `Registration` entity
3. **Step 2**: Add 2 guardian fields to `RegistrationMember` entity
4. **Step 3**: Extend `MemberAttendanceRequest` DTO with guardian fields
5. **Step 4**: Extend `CreateRegistrationRequest` DTO with 2 new fields
6. **Step 5**: Extend `RegistrationResponse` DTO with 2 new fields
7. **Step 6**: Extend `MemberPricingDetail` DTO with guardian fields
8. **Step 7**: Update `ToResponse` mapping (MemberPricingDetail + RegistrationResponse)
9. **Step 8**: Update `RegistrationConfiguration.cs` (2 new property mappings)
10. **Step 9**: Update `RegistrationMemberConfiguration.cs` (2 new property mappings)
11. **Step 12**: Create and apply EF Core migration
12. **Step 13**: Write validator unit tests (RED phase)
13. **Step 11**: Implement validator rules (GREEN phase)
14. **Step 14**: Write service mapping unit tests (RED phase)
15. **Step 10**: Implement service changes — map new fields (GREEN phase)
16. **Step 15**: Update `RegistrationBuilder` for tests
17. **Step 16**: Update technical documentation

---

## Testing Checklist

### Validator Tests (`CreateRegistrationValidatorTests.cs`)

- [ ] `SpecialNeeds` > 2000 chars → error
- [ ] `CampatesPreference` > 500 chars → error
- [ ] `GuardianName` > 200 chars → error
- [ ] `GuardianDocumentNumber` > 50 chars → error

### Service Mapping Tests (`RegistrationsServiceTests.cs`)

- [ ] `CreateAsync` with `SpecialNeeds` → persisted
- [ ] `CreateAsync` with `CampatesPreference` → persisted
- [ ] `CreateAsync` with guardian info on member → persisted
- [ ] `CreateAsync` with null optional fields → defaults applied
- [ ] `UpdateMembersAsync` with guardian info → persisted on new members

### Testing Framework

- xUnit + FluentAssertions + NSubstitute
- `FluentValidation.TestHelper` for validator tests (`TestValidateAsync`, `ShouldHaveValidationErrorFor`)
- Target: 90% coverage on new code

---

## Error Response Format

Standard `ApiResponse<T>` envelope — no changes. Validation errors return HTTP 400 with field-level error details.

| Scenario | HTTP Status |
|----------|-------------|
| `SpecialNeeds` exceeds max length | 400 |
| `CampatesPreference` exceeds max length | 400 |
| `GuardianName` exceeds max length | 400 |
| `GuardianDocumentNumber` exceeds max length | 400 |

---

## Dependencies

### NuGet Packages

No new packages required. All existing dependencies suffice:

- `FluentValidation` — already in use
- `Microsoft.EntityFrameworkCore` — already in use

### EF Core Migration Commands

```bash
dotnet ef migrations add AddRegistrationGuardianAndPreferenceFields --project src/Abuvi.API
dotnet ef database update --project src/Abuvi.API
```

---

## Notes

### Business Rules

1. All new fields are nullable/optional by business logic — `SpecialNeeds` and `CampatesPreference` are free-text, guardian fields only apply to minors.
2. Guardian info is optional at submission — age category is only determined at service layer.

### Privacy / GDPR

- `GuardianDocumentNumber` is personal data. It must **never** appear in log messages.
- Guardian fields are only exposed in the detail endpoint (`GET /api/registrations/{id}`), never in list endpoints.
- Current logging only references registration IDs — verify this remains true after changes.

### Language

- All code (variables, comments, test names) in English
- Validation error messages in Spanish (matching existing convention)

### Backward Compatibility

- **Not required** — the registration process is not in production yet.
- All existing callers, tests, and constructors should be updated directly to include the new fields.
- No need for default parameter values or optional parameters purely for compatibility.

### Important Caveats

- The enriched spec mentions `UpdateRegistrationMembersRequest` should also support guardian fields. This is **already covered** because it uses `List<MemberAttendanceRequest> Members`, and we are extending `MemberAttendanceRequest` with guardian fields (Step 3). The `UpdateMembersAsync` service method just needs to map the new fields (Step 10c).
- Do **not** add new endpoints. The enriched spec explicitly states no new endpoints.

### Extracted Tickets

The following features were extracted from the original scope to be implemented as separate tickets:

1. **`feat-registration-accommodations`** — Accommodation preferences system with structured model for family placement (ranked preferences, capacity management, assignment logic)
2. **`feat-registration-activities`** — Activity sign-up system with per-edition activity definitions, conditions, and member participation tracking
3. **`feat-camp-edition-extras-registration`** — Connect the existing `CampEditionExtra` entity (DB table already exists) to the registration flow. VegetarianCount and NeedsTruck become configurable extras with optional cost per edition.

---

## Next Steps After Implementation

1. Frontend implementation: Extend `registration.ts` types, update `RegisterForCampPage.vue` wizard, update `RegistrationDetailPage.vue`
2. Implement extracted tickets: accommodations, activities, extras-registration
3. Consider adding a post-creation warning if minors are registered without guardian info (future enhancement)
4. Admin export view for the new fields (separate ticket)
