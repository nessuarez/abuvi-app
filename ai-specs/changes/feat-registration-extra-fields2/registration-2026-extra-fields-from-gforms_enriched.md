# Registration 2026: Extra Fields from Google Forms — Enriched Technical Specification

## Overview

The current Google Forms registration form for camp "El Clar del Bosc 2026" collects several fields that are not yet captured in the web application's registration flow. This feature maps those Google Forms fields to the existing data model, extends the model where necessary, and surfaces the new fields in both the registration wizard (`RegisterForCampPage.vue`) and the registration detail page.

### ⚠️ Scope Reduction (2026-02-26)

This ticket was scoped down to only implement **SpecialNeeds**, **CampatesPreference**, **GuardianName**, and **GuardianDocumentNumber**. The following fields were extracted to separate tickets that require richer domain models:

| Field | New Ticket | Reason |
| ----- | ---------- | ------ |
| `AccommodationPreferences` | [`feat-registration-accommodations`](../feat-registration-accommodations/feat-registration-accommodations.md) | Structured model needed for family placement within camp facilities |
| `VegetarianCount` | [`feat-camp-edition-extras-registration`](../feat-camp-edition-extras-registration/feat-camp-edition-extras-registration.md) | Generic extras system — may have costs in some years |
| `NeedsTruck` | [`feat-camp-edition-extras-registration`](../feat-camp-edition-extras-registration/feat-camp-edition-extras-registration.md) | Generic extras system — may have costs in some years |
| `Activities` | [`feat-registration-activities`](../feat-registration-activities/feat-registration-activities.md) | Structured model with per-edition conditions and participation tracking |

See the updated backend and frontend implementation plans for the reduced scope.

**Depends on**:

- `feat-camps-registration` (backend registration entities — implemented and merged)
- `feat-registration-attendance-period` (per-member `AttendancePeriod` — spec enriched, check implementation status before starting)

---

## Google Forms Field Inventory

The following fields were extracted from the live form at:
`https://docs.google.com/forms/d/e/1FAIpQLSfkeoytsInWXJDVmR8WyONDWxAz-6FYz2PqX3ExLzm2vRYIkw/viewform`

| # | Form Field | Type | Required | Options / Notes |
|---|-----------|------|----------|-----------------|
| 1 | Email address | Short text | Yes | Already on `FamilyMember.Email` |
| 2 | Number of participants | Dropdown | Yes | 1–10. Derived from `MemberIds` count — no new field needed |
| 3–8 | Participant N: Name & Surname | Short text | No | Already in `FamilyMember.FirstName/LastName` |
| 3–8 | Participant N: Period | Multiple choice | No | Full camp / Week 1 / Week 2 / Visit — covered by `feat-registration-attendance-period` |
| 3–8 | Participant N: Age (if minor) | Dropdown (1–17) | No | Already derived from `FamilyMember.DateOfBirth` |
| 9 | Accommodation type preferences | Grid (ranking) | No | Lodge / Caravan / Personal tent — **NEW on `Registration`** |
| 10 | Food allergies / intolerances | Long text | No | Already on `FamilyMember.Allergies` |
| 11 | Vegetarian menu needs | Short text | No | **NEW on `Registration`** (family-level dietary preference) |
| 12 | Other specific needs | Long text | No | **NEW on `Registration`** (`SpecialNeeds`) |
| 13 | Truck usage required | Multiple choice (Yes/No) | No | **NEW on `Registration`** (`NeedsTruck: bool`) |
| 14 | Activity participation | Checkboxes | No | Camp coordination / Cooking / Hikes / Culture+Theater / Sports / Crafts / Parties / Children's activities — **NEW on `Registration`** (stored as flags or comma-separated enum) |
| 15 | First payment amount (€) | Short text | No | Out of scope: belongs to the Payments feature, not registration |
| 16 | Campmates preference | Long text | No | **NEW on `Registration`** (`CampatesPreference`) |
| 17 | Visit days (max 3) | Short text | No | Covered by `feat-registration-attendance-period` (`WeekendVisit` dates on `RegistrationMember`) |
| 18 | Guardian name and ID | Long text | No | **NEW on `RegistrationMember`** for minors only |
| 19 | Additional minor under guardianship | Short text | No | **NEW on `RegistrationMember`** for minors |

---

## What Already Exists vs. What Is New

### Already Covered — No Changes Needed

| Form Field | Existing Location |
|-----------|------------------|
| Email | `FamilyMember.Email` |
| Name and Surname | `FamilyMember.FirstName` / `FamilyMember.LastName` |
| Age | Derived from `FamilyMember.DateOfBirth` at registration time |
| Allergies / food intolerances | `FamilyMember.Allergies` (encrypted) |
| Attendance period | `RegistrationMember.AttendancePeriod` (via `feat-registration-attendance-period`) |
| Visit days | `CampEdition.WeekendStartDate` / `WeekendEndDate` (via `feat-registration-attendance-period`) |
| First payment amount | Out of scope — belongs to the Payments feature |

### New Fields — Scope of This Feature

#### On `Registration` (family-level)

| Field Name | DB Column | Type | Notes |
|-----------|-----------|------|-------|
| `AccommodationPreferences` | `accommodation_preferences` | `string?` (max 200) | Comma-separated ranked choices: `Lodge`, `Caravan`, `Tent` |
| `VegetarianCount` | `vegetarian_count` | `int` (default 0) | Number of family members needing vegetarian menu |
| `SpecialNeeds` | `special_needs` | `string?` (max 2000) | Free-text for other specific needs |
| `NeedsTruck` | `needs_truck` | `bool` (default false) | Whether the family needs the truck |
| `Activities` | `activities` | `string?` (max 500) | Comma-separated list of volunteered activities |
| `CampatesPreference` | `campates_preference` | `string?` (max 500) | Free-text: preferred campmates |

#### On `RegistrationMember` (per-member)

| Field Name | DB Column | Type | Notes |
|-----------|-----------|------|-------|
| `GuardianName` | `guardian_name` | `string?` (max 200) | Only meaningful for minors (AgeCategory: Baby or Child) |
| `GuardianDocumentNumber` | `guardian_document_number` | `string?` (max 50) | Guardian ID/DNI for minors |

#### Enum: `AccommodationOption`

```csharp
// src/Abuvi.API/Features/Registrations/RegistrationsModels.cs
public enum AccommodationOption { Lodge, Caravan, Tent }

// Stored as comma-separated string of ranked preferences, e.g. "Lodge,Tent,Caravan"
```

#### Enum: `CampActivity`

```csharp
public enum CampActivity
{
    Coordination,   // Camp coordination
    Cooking,
    Hikes,
    Culture,        // Culture / theater
    Sports,
    Crafts,
    Parties,
    ChildrenActivities
}

// Stored as comma-separated string, e.g. "Cooking,Hikes,Sports"
```

---

## Data Model Changes

### `Registration` Entity (additions to existing class in `RegistrationsModels.cs`)

```csharp
// New fields to add to the Registration class
public string? AccommodationPreferences { get; set; }  // e.g. "Lodge,Tent,Caravan"
public int VegetarianCount { get; set; } = 0;
public string? SpecialNeeds { get; set; }
public bool NeedsTruck { get; set; } = false;
public string? Activities { get; set; }                 // e.g. "Cooking,Hikes"
public string? CampatesPreference { get; set; }
```

### `RegistrationMember` Entity (additions to existing class in `RegistrationsModels.cs`)

```csharp
// New fields to add to the RegistrationMember class
public string? GuardianName { get; set; }
public string? GuardianDocumentNumber { get; set; }
```

---

## EF Core Configuration Changes

### `RegistrationConfiguration.cs` — Add new property mappings

```csharp
// Add inside Configure(EntityTypeBuilder<Registration> builder):
builder.Property(r => r.AccommodationPreferences)
    .HasMaxLength(200).HasColumnName("accommodation_preferences");
builder.Property(r => r.VegetarianCount)
    .IsRequired().HasDefaultValue(0).HasColumnName("vegetarian_count");
builder.Property(r => r.SpecialNeeds)
    .HasMaxLength(2000).HasColumnName("special_needs");
builder.Property(r => r.NeedsTruck)
    .IsRequired().HasDefaultValue(false).HasColumnName("needs_truck");
builder.Property(r => r.Activities)
    .HasMaxLength(500).HasColumnName("activities");
builder.Property(r => r.CampatesPreference)
    .HasMaxLength(500).HasColumnName("campates_preference");
```

### `RegistrationMemberConfiguration.cs` — Add new property mappings

```csharp
// Add inside Configure(EntityTypeBuilder<RegistrationMember> builder):
builder.Property(m => m.GuardianName)
    .HasMaxLength(200).HasColumnName("guardian_name");
builder.Property(m => m.GuardianDocumentNumber)
    .HasMaxLength(50).HasColumnName("guardian_document_number");
```

---

## Migration

After updating both entity classes and their configurations, create a single migration:

```bash
dotnet ef migrations add AddRegistrationExtraFields2026 --project src/Abuvi.API
dotnet ef database update --project src/Abuvi.API
```

Expected SQL additions:

- `ALTER TABLE registrations ADD COLUMN accommodation_preferences VARCHAR(200) NULL`
- `ALTER TABLE registrations ADD COLUMN vegetarian_count INTEGER NOT NULL DEFAULT 0`
- `ALTER TABLE registrations ADD COLUMN special_needs VARCHAR(2000) NULL`
- `ALTER TABLE registrations ADD COLUMN needs_truck BOOLEAN NOT NULL DEFAULT FALSE`
- `ALTER TABLE registrations ADD COLUMN activities VARCHAR(500) NULL`
- `ALTER TABLE registrations ADD COLUMN campates_preference VARCHAR(500) NULL`
- `ALTER TABLE registration_members ADD COLUMN guardian_name VARCHAR(200) NULL`
- `ALTER TABLE registration_members ADD COLUMN guardian_document_number VARCHAR(50) NULL`

---

## API Changes

### Request DTOs

#### `CreateRegistrationRequest` — extend with new optional fields

```csharp
// src/Abuvi.API/Features/Registrations/RegistrationsModels.cs
public record CreateRegistrationRequest(
    Guid CampEditionId,
    Guid FamilyUnitId,
    List<Guid> MemberIds,                        // or List<MemberAttendanceRequest> if attendance-period is merged
    string? Notes,
    // New fields:
    List<string>? AccommodationPreferences,       // ordered list: ["Lodge", "Tent", "Caravan"]
    int VegetarianCount = 0,
    string? SpecialNeeds = null,
    bool NeedsTruck = false,
    List<string>? Activities = null,
    string? CampatesPreference = null
);
```

> **Note on MemberIds vs. Members**: If `feat-registration-attendance-period` has been merged, `List<Guid> MemberIds` will already have been replaced with `List<MemberAttendanceRequest> Members`. Adapt accordingly — do not regress that change.

#### `MemberAttendanceRequestExtension` — add guardian fields per member

If `feat-registration-attendance-period` is merged, the `MemberAttendanceRequest` record should be extended with:

```csharp
public record MemberAttendanceRequest(
    Guid MemberId,
    AttendancePeriod Period,
    // New fields:
    string? GuardianName = null,
    string? GuardianDocumentNumber = null
);
```

If `feat-registration-attendance-period` is NOT yet merged, add guardian fields to a new `MemberRegistrationRequest` wrapper instead of the raw `Guid` list. Check with the team before introducing a second breaking change to `MemberIds`.

#### `RegistrationResponse` — add new fields to response

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
    // New fields:
    List<string>? AccommodationPreferences,
    int VegetarianCount,
    string? SpecialNeeds,
    bool NeedsTruck,
    List<string>? Activities,
    string? CampatesPreference
);
```

#### `MemberPricingDetail` — add guardian fields

```csharp
public record MemberPricingDetail(
    Guid FamilyMemberId,
    string FullName,
    int AgeAtCamp,
    AgeCategory AgeCategory,
    decimal IndividualAmount,
    // New fields:
    string? GuardianName,
    string? GuardianDocumentNumber
);
```

---

## Endpoints Affected

No new endpoints are added. The following existing endpoints change their request/response shapes:

| Endpoint | Change |
|----------|--------|
| `POST /api/registrations` | `CreateRegistrationRequest` gains 6 new optional fields + member guardian fields |
| `GET /api/registrations/{id}` | `RegistrationResponse` gains 6 new fields; `MemberPricingDetail` gains 2 guardian fields |
| `GET /api/registrations` | `RegistrationListResponse` — no change (list view does not expose these fields) |
| `PUT /api/registrations/{id}/members` | `UpdateRegistrationMembersRequest` — add guardian fields per member if attendance-period is merged |

---

## Validator Changes

### `CreateRegistrationValidator.cs` — add new rules

```csharp
// Add inside CreateRegistrationValidator constructor:

RuleFor(x => x.VegetarianCount)
    .GreaterThanOrEqualTo(0)
    .WithMessage("El número de menús vegetarianos no puede ser negativo")
    .LessThanOrEqualTo(50)
    .WithMessage("El número de menús vegetarianos parece demasiado alto");

RuleFor(x => x.SpecialNeeds)
    .MaximumLength(2000)
    .WithMessage("Las necesidades especiales no pueden superar los 2000 caracteres")
    .When(x => x.SpecialNeeds is not null);

RuleFor(x => x.CampatesPreference)
    .MaximumLength(500)
    .WithMessage("La preferencia de acampantes no puede superar los 500 caracteres")
    .When(x => x.CampatesPreference is not null);

RuleFor(x => x.AccommodationPreferences)
    .Must(prefs => prefs == null || prefs.Count <= 3)
    .WithMessage("No puede indicar más de 3 preferencias de alojamiento")
    .Must(prefs => prefs == null || prefs.All(p =>
        p is "Lodge" or "Caravan" or "Tent"))
    .WithMessage("Tipo de alojamiento no válido. Use: Lodge, Caravan, Tent");

RuleFor(x => x.Activities)
    .Must(acts => acts == null || acts.Count <= 8)
    .WithMessage("El número de actividades seleccionadas no puede superar 8")
    .Must(acts => acts == null || acts.All(a => IsValidActivity(a)))
    .WithMessage("Actividad no válida");
```

Add per-member guardian validation inside the `MemberAttendanceRequest` child rules (if applicable):

```csharp
// If the member's age category is Baby or Child (determined at validation time),
// guardian fields are recommended but not strictly required at this stage.
// (Age category is only known after DB lookup in the service layer — do not block submission.)
RuleFor(m => m.GuardianName)
    .MaximumLength(200)
    .WithMessage("El nombre del tutor no puede superar los 200 caracteres")
    .When(m => m.GuardianName is not null);

RuleFor(m => m.GuardianDocumentNumber)
    .MaximumLength(50)
    .WithMessage("El documento del tutor no puede superar los 50 caracteres")
    .When(m => m.GuardianDocumentNumber is not null);
```

---

## Service Changes

### `RegistrationsService.CreateAsync`

Add after building the `Registration` object:

```csharp
// Map new fields from request
registration.AccommodationPreferences = request.AccommodationPreferences is { Count: > 0 }
    ? string.Join(",", request.AccommodationPreferences)
    : null;
registration.VegetarianCount = request.VegetarianCount;
registration.SpecialNeeds = request.SpecialNeeds;
registration.NeedsTruck = request.NeedsTruck;
registration.Activities = request.Activities is { Count: > 0 }
    ? string.Join(",", request.Activities)
    : null;
registration.CampatesPreference = request.CampatesPreference;
```

And for each `RegistrationMember`, add (if the request includes guardian fields per member):

```csharp
registrationMember.GuardianName = memberRequest.GuardianName;
registrationMember.GuardianDocumentNumber = memberRequest.GuardianDocumentNumber;
```

### `RegistrationMappingExtensions.ToResponse`

Update `MemberPricingDetail` projection:

```csharp
new MemberPricingDetail(
    m.FamilyMemberId,
    $"{m.FamilyMember.FirstName} {m.FamilyMember.LastName}",
    m.AgeAtCamp, m.AgeCategory, m.IndividualAmount,
    m.GuardianName,            // new
    m.GuardianDocumentNumber   // new
)
```

Update `RegistrationResponse` construction to include the 6 new family-level fields:

```csharp
// Parse stored comma-separated strings back to List<string>
AccommodationPreferences: r.AccommodationPreferences?.Split(',').ToList(),
VegetarianCount: r.VegetarianCount,
SpecialNeeds: r.SpecialNeeds,
NeedsTruck: r.NeedsTruck,
Activities: r.Activities?.Split(',').ToList(),
CampatesPreference: r.CampatesPreference
```

---

## Frontend Changes

### `frontend/src/types/registration.ts`

Extend `CreateRegistrationRequest`:

```typescript
export interface CreateRegistrationRequest {
  campEditionId: string
  familyUnitId: string
  memberIds: string[]           // or members: MemberAttendanceRequest[] if period spec is merged
  notes?: string | null
  // New fields:
  accommodationPreferences?: string[] | null   // e.g. ['Lodge', 'Tent', 'Caravan']
  vegetarianCount?: number
  specialNeeds?: string | null
  needsTruck?: boolean
  activities?: string[] | null
  campatesPreference?: string | null
}
```

Extend `MemberPricingDetail`:

```typescript
export interface MemberPricingDetail {
  familyMemberId: string
  fullName: string
  ageAtCamp: number
  ageCategory: AgeCategory
  individualAmount: number
  guardianName: string | null       // new
  guardianDocumentNumber: string | null  // new
}
```

Extend `RegistrationResponse`:

```typescript
export interface RegistrationResponse {
  // ...existing fields...
  accommodationPreferences: string[] | null
  vegetarianCount: number
  specialNeeds: string | null
  needsTruck: boolean
  activities: string[] | null
  campatesPreference: string | null
}
```

Add new local constant types:

```typescript
export type AccommodationOption = 'Lodge' | 'Caravan' | 'Tent'

export const ACCOMMODATION_LABELS: Record<AccommodationOption, string> = {
  Lodge: 'Refugio',
  Caravan: 'Caravana / Tienda glamping',
  Tent: 'Tienda personal'
}

export type CampActivity =
  | 'Coordination'
  | 'Cooking'
  | 'Hikes'
  | 'Culture'
  | 'Sports'
  | 'Crafts'
  | 'Parties'
  | 'ChildrenActivities'

export const ACTIVITY_LABELS: Record<CampActivity, string> = {
  Coordination: 'Coordinación del campamento',
  Cooking: 'Cocina',
  Hikes: 'Excursiones',
  Culture: 'Cultura / Teatro',
  Sports: 'Deportes',
  Crafts: 'Manualidades',
  Parties: 'Fiestas',
  ChildrenActivities: 'Actividades infantiles'
}
```

### `frontend/src/views/registrations/RegisterForCampPage.vue`

Add a new **Step 4: Additional Information** between the current Extras step and Confirm step (or integrate within Confirm step as a collapsible section — team to decide on UX).

The new form section collects:

1. **Accommodation preferences** — an ordered list (drag-to-rank or three dropdowns) for Lodge / Caravan / Tent.
2. **Vegetarian menu count** — `InputNumber` (min: 0, max: number of selected members).
3. **Special needs** — `Textarea` (max 2000 chars).
4. **Truck required** — `ToggleSwitch` or two radio buttons (Yes / No).
5. **Activities** — multi-select `Checkbox` group (8 activities).
6. **Campmates preference** — `Textarea` (max 500 chars).
7. **Guardian info per minor member** — shown only for members whose age is < 18. Two text inputs: Guardian name and Guardian ID.

New reactive state in the `setup` block:

```typescript
const accommodationPreferences = ref<string[]>([])
const vegetarianCount = ref<number>(0)
const specialNeeds = ref<string>('')
const needsTruck = ref<boolean>(false)
const selectedActivities = ref<string[]>([])
const campatesPreference = ref<string>('')
const guardianInfoMap = ref<Record<string, { name: string; documentNumber: string }>>({})
```

Pass these values in `handleConfirm`:

```typescript
const created = await createRegistration({
  campEditionId: editionId.value,
  familyUnitId: familyUnit.value.id,
  memberIds: selectedMemberIds.value,
  notes: notes.value || null,
  accommodationPreferences: accommodationPreferences.value.length > 0
    ? accommodationPreferences.value : null,
  vegetarianCount: vegetarianCount.value,
  specialNeeds: specialNeeds.value || null,
  needsTruck: needsTruck.value,
  activities: selectedActivities.value.length > 0 ? selectedActivities.value : null,
  campatesPreference: campatesPreference.value || null
})
```

### `frontend/src/views/registrations/RegistrationDetailPage.vue`

Add a new section below the existing Notes section displaying the extra fields:

```vue
<!-- Accommodation preferences -->
<div v-if="registration.accommodationPreferences?.length" class="mb-6 ...">
  <h2 class="...">Preferencias de alojamiento</h2>
  <ol class="list-decimal list-inside ...">
    <li v-for="pref in registration.accommodationPreferences" :key="pref">
      {{ ACCOMMODATION_LABELS[pref] ?? pref }}
    </li>
  </ol>
</div>

<!-- Vegetarian, special needs, truck, activities, campates -->
```

Guardian info is shown inline inside the existing member list within `RegistrationPricingBreakdown.vue`, if `guardianName` is present.

---

## Files to Modify

### Backend

| File | Change |
|------|--------|
| `src/Abuvi.API/Features/Registrations/RegistrationsModels.cs` | Add 6 fields to `Registration`, 2 fields to `RegistrationMember`, 2 new enums, extend `CreateRegistrationRequest`, extend `MemberPricingDetail`, extend `RegistrationResponse`, update `ToResponse` mapping |
| `src/Abuvi.API/Data/Configurations/RegistrationConfiguration.cs` | Add 6 property mappings |
| `src/Abuvi.API/Data/Configurations/RegistrationMemberConfiguration.cs` | Add 2 property mappings |
| `src/Abuvi.API/Features/Registrations/RegistrationsService.cs` | Map new request fields when building `Registration` and `RegistrationMember` objects |
| `src/Abuvi.API/Features/Registrations/CreateRegistrationValidator.cs` | Add validation rules for new fields |
| `src/Abuvi.API/Migrations/` | New migration file `AddRegistrationExtraFields2026` |

### Frontend

| File | Change |
|------|--------|
| `frontend/src/types/registration.ts` | Extend 3 interfaces, add 2 new type aliases and label maps |
| `frontend/src/views/registrations/RegisterForCampPage.vue` | Add new form fields (accommodation, vegetarian count, special needs, truck, activities, campates, guardian info per minor) |
| `frontend/src/views/registrations/RegistrationDetailPage.vue` | Display new fields in read-only view |
| `frontend/src/components/registrations/RegistrationPricingBreakdown.vue` | Show guardian info for member rows where `guardianName` is set |

### Tests

| File | Change |
|------|--------|
| `src/Abuvi.Tests/Unit/Features/Registrations/CreateRegistrationValidatorTests.cs` | Add tests for the 6 new validator rules |
| `src/Abuvi.Tests/Unit/Features/Registrations/RegistrationsServiceTests.cs` | Add tests for new field mapping in `CreateAsync` |
| `src/Abuvi.Tests/Helpers/Builders/RegistrationBuilder.cs` | Add builder methods for new fields |
| `frontend/src/components/registrations/__tests__/RegistrationPricingBreakdown.test.ts` | Add test: guardian info displays when present |

---

## Validation Rules Summary

| Field | Rule |
|-------|------|
| `VegetarianCount` | `>= 0`, `<= 50` (sanity cap) |
| `SpecialNeeds` | Max 2000 chars |
| `CampatesPreference` | Max 500 chars |
| `AccommodationPreferences` | Max 3 items; each must be `Lodge`, `Caravan`, or `Tent` |
| `Activities` | Max 8 items; each must be a valid `CampActivity` value |
| `GuardianName` | Max 200 chars |
| `GuardianDocumentNumber` | Max 50 chars |

---

## Business Rules

1. **Accommodation preferences are optional**. An empty or null list means the family has no accommodation preference. The camp organiser uses this for logistics planning only; it does not affect pricing.
2. **Vegetarian count is informational**. It is not validated against the number of selected members — a family may order vegetarian for guests or external reasons. Apply a sanity cap of 50.
3. **Guardian info is per-member and optional at submission time**. The service does not enforce guardian presence for minors because age category is only known after age calculation in the service. A future enhancement could add a post-creation warning.
4. **Activities list is for volunteering interest only**. It does not affect pricing or capacity.
5. **Truck field** (`NeedsTruck`) is a logistical flag visible to camp organisers. It has no pricing effect.
6. **All new fields are nullable / optional at the API level**. Sending `null` is equivalent to not providing a value. Default values: `VegetarianCount = 0`, `NeedsTruck = false`.

---

## Non-Functional Requirements

- **Security**: Guardian names and document numbers are personal data. They must never be exposed in list endpoints (`GET /api/registrations`). Only the full detail endpoint (`GET /api/registrations/{id}`) returns them. Admin/Board access rules for the detail endpoint remain unchanged.
- **Performance**: No new indexes are needed. The new columns are not used in any WHERE clause or JOIN condition at this time.
- **Backward compatibility**: All new request fields are optional with sensible defaults. Existing API clients sending the old `CreateRegistrationRequest` shape (without the new fields) must continue to work without errors.
- **Privacy**: `GuardianDocumentNumber` must not be logged in application logs. Use `logger.LogInformation(..., registration.Id, ...)` patterns that do not include PII.

---

## TDD Implementation Steps

Follow the Red-Green-Refactor cycle strictly.

### Step 1: Unit tests for new validator rules

Write failing tests in `CreateRegistrationValidatorTests.cs`:

- `VegetarianCount_WhenNegative_ReturnsError`
- `VegetarianCount_WhenAbove50_ReturnsError`
- `VegetarianCount_WhenZero_IsValid`
- `AccommodationPreferences_WhenInvalidOption_ReturnsError`
- `AccommodationPreferences_WhenMoreThan3_ReturnsError`
- `Activities_WhenInvalidActivity_ReturnsError`
- `SpecialNeeds_WhenExceeds2000Chars_ReturnsError`
- `GuardianName_WhenExceeds200Chars_ReturnsError`

### Step 2: Extend entity + EF Core config + run migration

Add new fields to `Registration` and `RegistrationMember` classes. Update EF configurations. Create and apply migration.

### Step 3: Unit tests for service mapping

Write failing tests in `RegistrationsServiceTests.cs`:

- `CreateAsync_WithAccommodationPreferences_PersistsAsCommaSeparatedString`
- `CreateAsync_WithVegetarianCount_PersistsValue`
- `CreateAsync_WithNeedsTruck_True_PersistsTrue`
- `CreateAsync_WithActivities_PersistsAsCommaSeparatedString`
- `CreateAsync_WithGuardianInfoOnMember_PersistsGuardianFields`
- `CreateAsync_WithNullOptionalFields_UsesDefaults`

### Step 4: Implement service changes

Update `RegistrationsService.CreateAsync` to map the new fields. Implement `ToResponse` mapping. Make all unit tests pass.

### Step 5: Validator implementation

Update `CreateRegistrationValidator` with the new rules. Confirm all validator tests pass.

### Step 6: Frontend — types

Add new TypeScript interfaces and label maps to `frontend/src/types/registration.ts`.

### Step 7: Frontend — wizard form fields

Add the new input controls to `RegisterForCampPage.vue`. No unit tests required for the new UI section (it follows the existing wizard pattern), but update the Cypress e2e spec `registration.cy.ts` if it covers the full wizard flow.

### Step 8: Frontend — detail view

Update `RegistrationDetailPage.vue` and `RegistrationPricingBreakdown.vue` to display the new fields.

---

## Open Questions

1. **Guardian fields for minors**: Should guardian info be required at submission for members with `AgeCategory = Baby` or `AgeCategory = Child`? Currently designed as optional. Confirm with the product owner before enforcing it.
2. **Accommodation preference UX**: The form uses a ranked grid (first / second / last choice). The simplest frontend implementation is three ordered dropdowns. A drag-to-rank component is more faithful to the GForms UX but has no existing component in the PrimeVue library — team to decide.
3. **Attendance period dependency**: If `feat-registration-attendance-period` is not yet merged, the guardian info cannot be attached per-member unless the `CreateRegistrationRequest.MemberIds: List<Guid>` is replaced with a richer list type. Decide on order of merging with the team to avoid a double breaking change.
4. **Admin visibility**: Should the new fields appear in an admin registration list/export view? If so, a separate admin endpoint or export feature is out of scope here and should be tracked in a new ticket.

---

## Document Control

- **Feature**: `feat-registration-extra-fields2`
- **Original Spec**: `ai-specs/changes/feat-registration-extra-fields2/registration-2026-extra-fields-from-gforms.md`
- **Google Form**: `https://docs.google.com/forms/d/e/1FAIpQLSfkeoytsInWXJDVmR8WyONDWxAz-6FYz2PqX3ExLzm2vRYIkw/viewform`
- **Depends On**: `feat-camps-registration` (merged), `feat-registration-attendance-period` (check status)
- **Version**: 1.0 (Enriched)
- **Date**: 2026-02-25
- **Status**: Ready for Implementation
