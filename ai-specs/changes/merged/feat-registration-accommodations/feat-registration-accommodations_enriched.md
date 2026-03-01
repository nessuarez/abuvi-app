# Feature Spec: feat-registration-accommodations — Accommodation Preferences System

## Status: ENRICHED — Ready for implementation planning

## Origin

Extracted from `feat-registration-extra-fields2` (Google Forms field #9: "Accommodation type preferences").

**Why extracted**: Accommodation preferences are used for **family placement within camp facilities**. A comma-separated string field is insufficient — this needs structured entities for capacity management, ranked preference ordering, and future assignment logic.

---

## Problem Statement

Each camp edition offers different accommodation options (lodge, caravan zone, tent area, etc.) with limited capacity. Families rank their preferences during registration. Camp organizers then use these preferences to assign families to physical locations.

**Current state**: The `Camp` entity already stores facility-level accommodation capacity as JSON (`AccommodationCapacityJson` in `CampsModels.cs`) describing the physical infrastructure (rooms, bungalows, tent areas, motorhome spots). However, there is no way to:

1. Define per-edition accommodation options that families can choose from
2. Collect ranked family preferences during registration
3. Track assignments (future scope)

---

## Scope

### In scope (this feature)

- `CampEditionAccommodation` entity — admin-configurable options per edition
- `RegistrationAccommodationPreference` entity — family ranked choices (submitted with registration)
- CRUD endpoints for managing accommodation options
- Registration wizard integration (new step or sub-step for accommodation preferences)
- Admin view showing preference counts per accommodation

### Out of scope (future features)

- `AccommodationAssignment` entity — actual family placement by admin
- Assignment workflow UI
- Capacity enforcement at registration time (preferences are just preferences)
- Automated assignment algorithms

---

## Data Model

### `CampEditionAccommodation` — Available options per edition

| Field | Type | Constraints | Description |
| ----- | ---- | ----------- | ----------- |
| `Id` | `Guid` | PK, auto-generated | Primary key |
| `CampEditionId` | `Guid` (FK) | NOT NULL, CASCADE delete | Which edition offers this accommodation |
| `Name` | `string` | NOT NULL, max 200 chars | e.g. "Refugio A", "Zona Caravanas Norte" |
| `AccommodationType` | `AccommodationType` enum | NOT NULL, stored as string(30) | `Lodge`, `Caravan`, `Tent`, `Bungalow`, `Motorhome` |
| `Description` | `string?` | max 1000 chars | Optional details about the accommodation |
| `Capacity` | `int?` | NULL or > 0 (check constraint) | Max families/units (null = unlimited) |
| `IsActive` | `bool` | NOT NULL, default `true` | Can be disabled without deletion |
| `SortOrder` | `int` | NOT NULL, default 0 | Display ordering in selection UI |
| `CreatedAt` | `DateTime` | NOT NULL, default UTC NOW | Record creation timestamp |
| `UpdatedAt` | `DateTime` | NOT NULL, default UTC NOW | Last update timestamp |

**Table name**: `camp_edition_accommodations`

### `RegistrationAccommodationPreference` — Family's ranked choices

| Field | Type | Constraints | Description |
| ----- | ---- | ----------- | ----------- |
| `Id` | `Guid` | PK, auto-generated | Primary key |
| `RegistrationId` | `Guid` (FK) | NOT NULL, CASCADE delete | Which registration |
| `CampEditionAccommodationId` | `Guid` (FK) | NOT NULL, RESTRICT delete | Which accommodation option |
| `PreferenceOrder` | `int` | NOT NULL, >= 1 (check constraint) | 1 = first choice, 2 = second, etc. |
| `CreatedAt` | `DateTime` | NOT NULL, default UTC NOW | Record creation timestamp |

**Table name**: `registration_accommodation_preferences`
**Unique index**: `(RegistrationId, CampEditionAccommodationId)` — no duplicate preferences
**Unique index**: `(RegistrationId, PreferenceOrder)` — no duplicate rank per registration

### Enum: `AccommodationType`

```csharp
public enum AccommodationType
{
    Lodge,       // Refugio / cabaña
    Caravan,     // Caravana
    Tent,        // Tienda de campaña (propia)
    Bungalow,    // Bungalow
    Motorhome    // Autocaravana
}
```

---

## Key Business Rules

1. Each family can rank **up to 3** accommodation preferences (configurable max, enforced in validation)
2. Preferences are strictly ordered (1st, 2nd, 3rd choice) — no ties
3. Accommodation options vary per camp edition (configurable by Board+ admin)
4. Capacity limits are **informational only** at registration time — not enforced (families can still select full accommodations)
5. A family cannot select the same accommodation twice in their preferences
6. Preferences are optional — a registration can have 0 preferences
7. Preferences are submitted/updated as part of the registration flow, not independently
8. Deactivated accommodations should not appear in the registration wizard but remain visible in admin views
9. Deleting an accommodation that has existing preferences should be prevented (RESTRICT delete behavior)

---

## Backend Implementation

### Files to Create

#### 1. Entity & DTOs — `src/Abuvi.API/Features/Camps/CampsModels.cs` (extend existing file)

Add to existing `CampsModels.cs`:

```csharp
// === Accommodation Types ===

public enum AccommodationType
{
    Lodge,
    Caravan,
    Tent,
    Bungalow,
    Motorhome
}

public class CampEditionAccommodation
{
    public Guid Id { get; set; }
    public Guid CampEditionId { get; set; }
    public string Name { get; set; } = string.Empty;
    public AccommodationType AccommodationType { get; set; }
    public string? Description { get; set; }
    public int? Capacity { get; set; }
    public bool IsActive { get; set; } = true;
    public int SortOrder { get; set; } = 0;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    // Navigation
    public CampEdition CampEdition { get; set; } = null!;
}

// === DTOs ===

public record CampEditionAccommodationResponse(
    Guid Id,
    Guid CampEditionId,
    string Name,
    AccommodationType AccommodationType,
    string? Description,
    int? Capacity,
    bool IsActive,
    int SortOrder,
    int CurrentPreferenceCount,  // How many registrations have this as any preference
    int FirstChoiceCount,        // How many registrations have this as 1st choice
    DateTime CreatedAt,
    DateTime UpdatedAt
);

public record CreateCampEditionAccommodationRequest(
    string Name,
    AccommodationType AccommodationType,
    string? Description,
    int? Capacity,
    int SortOrder = 0
);

public record UpdateCampEditionAccommodationRequest(
    string Name,
    AccommodationType AccommodationType,
    string? Description,
    int? Capacity,
    bool IsActive,
    int SortOrder
);
```

Add to `CampEdition` navigation properties:

```csharp
public ICollection<CampEditionAccommodation> Accommodations { get; set; } = [];
```

#### 2. Entity — `src/Abuvi.API/Features/Registrations/RegistrationsModels.cs` (extend existing file)

```csharp
public class RegistrationAccommodationPreference
{
    public Guid Id { get; set; }
    public Guid RegistrationId { get; set; }
    public Guid CampEditionAccommodationId { get; set; }
    public int PreferenceOrder { get; set; }
    public DateTime CreatedAt { get; set; }

    // Navigation
    public Registration Registration { get; set; } = null!;
    public CampEditionAccommodation CampEditionAccommodation { get; set; } = null!;
}
```

Add to `Registration` navigation properties:

```csharp
public ICollection<RegistrationAccommodationPreference> AccommodationPreferences { get; set; } = [];
```

Add DTOs for registration-side:

```csharp
public record AccommodationPreferenceRequest(
    Guid CampEditionAccommodationId,
    int PreferenceOrder
);

public record UpdateRegistrationAccommodationPreferencesRequest(
    List<AccommodationPreferenceRequest> Preferences
);

public record AccommodationPreferenceResponse(
    Guid CampEditionAccommodationId,
    string AccommodationName,
    AccommodationType AccommodationType,
    int PreferenceOrder
);
```

#### 3. EF Configuration — `src/Abuvi.API/Data/Configurations/CampEditionAccommodationConfiguration.cs` (new file)

Follow the pattern from `CampEditionExtraConfiguration.cs`:

- Table: `camp_edition_accommodations`
- Snake_case column naming
- FK to `camp_editions` with CASCADE delete
- Enum `AccommodationType` stored as string (max 30)
- Check constraints: `capacity IS NULL OR capacity > 0`, `sort_order >= 0`
- Default values: `is_active = true`, `sort_order = 0`, timestamps = `now()`

#### 4. EF Configuration — `src/Abuvi.API/Data/Configurations/RegistrationAccommodationPreferenceConfiguration.cs` (new file)

- Table: `registration_accommodation_preferences`
- FK to `registrations` with CASCADE delete
- FK to `camp_edition_accommodations` with RESTRICT delete
- Unique index on `(registration_id, camp_edition_accommodation_id)`
- Unique index on `(registration_id, preference_order)`
- Check constraint: `preference_order >= 1 AND preference_order <= 3`

#### 5. DbContext — `src/Abuvi.API/Data/AbuviDbContext.cs` (extend)

Add DbSet properties:

```csharp
public DbSet<CampEditionAccommodation> CampEditionAccommodations { get; set; }
public DbSet<RegistrationAccommodationPreference> RegistrationAccommodationPreferences { get; set; }
```

#### 6. Repository — `src/Abuvi.API/Features/Camps/CampEditionAccommodationsRepository.cs` (new file)

```csharp
public interface ICampEditionAccommodationsRepository
{
    Task<CampEditionAccommodation?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<List<CampEditionAccommodation>> GetByCampEditionAsync(Guid campEditionId, bool? activeOnly, CancellationToken ct = default);
    Task AddAsync(CampEditionAccommodation accommodation, CancellationToken ct = default);
    Task UpdateAsync(CampEditionAccommodation accommodation, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
    Task<bool> HasPreferencesAsync(Guid accommodationId, CancellationToken ct = default);
    Task<int> GetPreferenceCountAsync(Guid accommodationId, CancellationToken ct = default);
    Task<int> GetFirstChoiceCountAsync(Guid accommodationId, CancellationToken ct = default);
}
```

#### 7. Service — `src/Abuvi.API/Features/Camps/CampEditionAccommodationsService.cs` (new file)

Follow `CampEditionExtrasService.cs` pattern:

- Constructor-inject `ICampEditionAccommodationsRepository` + `ICampEditionsRepository`
- `CreateAsync(Guid campEditionId, CreateCampEditionAccommodationRequest request, CancellationToken ct)` → validate edition exists, map to entity, save, return response
- `UpdateAsync(Guid id, UpdateCampEditionAccommodationRequest request, CancellationToken ct)` → validate exists, update fields + UpdatedAt, save, return response
- `DeleteAsync(Guid id, CancellationToken ct)` → check for existing preferences before deletion, throw `InvalidOperationException` if preferences exist
- `GetByEditionAsync(Guid editionId, bool? activeOnly, CancellationToken ct)` → return list with preference counts
- `GetByIdAsync(Guid id, CancellationToken ct)` → return single with preference counts
- `ActivateAsync(Guid id, CancellationToken ct)` / `DeactivateAsync(Guid id, CancellationToken ct)`

#### 8. Validators — `src/Abuvi.API/Features/Camps/CampsValidators.cs` (extend existing file)

```csharp
public class CreateCampEditionAccommodationRequestValidator
    : AbstractValidator<CreateCampEditionAccommodationRequest>
{
    public CreateCampEditionAccommodationRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Name is required")
            .MaximumLength(200);

        RuleFor(x => x.AccommodationType)
            .IsInEnum().WithMessage("Invalid accommodation type");

        RuleFor(x => x.Description)
            .MaximumLength(1000)
            .When(x => x.Description != null);

        RuleFor(x => x.Capacity)
            .GreaterThan(0)
            .When(x => x.Capacity.HasValue);

        RuleFor(x => x.SortOrder)
            .GreaterThanOrEqualTo(0);
    }
}

public class UpdateCampEditionAccommodationRequestValidator
    : AbstractValidator<UpdateCampEditionAccommodationRequest>
{
    // Same rules as Create, plus IsActive validation
}

public class UpdateRegistrationAccommodationPreferencesRequestValidator
    : AbstractValidator<UpdateRegistrationAccommodationPreferencesRequest>
{
    public UpdateRegistrationAccommodationPreferencesRequestValidator()
    {
        RuleFor(x => x.Preferences)
            .Must(p => p == null || p.Count <= 3)
            .WithMessage("Maximum 3 accommodation preferences allowed");

        RuleFor(x => x.Preferences)
            .Must(p => p == null || p.Select(pref => pref.CampEditionAccommodationId).Distinct().Count() == p.Count)
            .WithMessage("Duplicate accommodation selections are not allowed");

        RuleFor(x => x.Preferences)
            .Must(p => p == null || p.Select(pref => pref.PreferenceOrder).Distinct().Count() == p.Count)
            .WithMessage("Duplicate preference orders are not allowed");

        RuleForEach(x => x.Preferences).ChildRules(pref =>
        {
            pref.RuleFor(p => p.PreferenceOrder)
                .InclusiveBetween(1, 3)
                .WithMessage("Preference order must be between 1 and 3");

            pref.RuleFor(p => p.CampEditionAccommodationId)
                .NotEmpty()
                .WithMessage("Accommodation ID is required");
        });
    }
}
```

#### 9. Endpoints — `src/Abuvi.API/Features/Camps/CampsEndpoints.cs` (extend existing file)

Add new route group after the existing extras endpoints:

```csharp
// === Camp Edition Accommodations (Board+ write, Member+ read) ===

var accommodationsWrite = app.MapGroup("/api/camps/editions/{editionId:guid}/accommodations")
    .WithTags("Camp Edition Accommodations")
    .WithOpenApi()
    .RequireAuthorization(policy => policy.RequireRole("Admin", "Board"));

accommodationsWrite.MapPost("/", CreateAccommodation)
    .WithName("CreateCampEditionAccommodation")
    .AddEndpointFilter<ValidationFilter<CreateCampEditionAccommodationRequest>>()
    .Produces<ApiResponse<CampEditionAccommodationResponse>>(201)
    .Produces(400).Produces(401).Produces(403);

var accommodationsWriteById = app.MapGroup("/api/camps/editions/accommodations")
    .WithTags("Camp Edition Accommodations")
    .WithOpenApi()
    .RequireAuthorization(policy => policy.RequireRole("Admin", "Board"));

accommodationsWriteById.MapPut("/{id:guid}", UpdateAccommodation)
    .AddEndpointFilter<ValidationFilter<UpdateCampEditionAccommodationRequest>>();
accommodationsWriteById.MapDelete("/{id:guid}", DeleteAccommodation);
accommodationsWriteById.MapPatch("/{id:guid}/activate", ActivateAccommodation);
accommodationsWriteById.MapPatch("/{id:guid}/deactivate", DeactivateAccommodation);

var accommodationsRead = app.MapGroup("/api/camps/editions")
    .WithTags("Camp Edition Accommodations")
    .WithOpenApi()
    .RequireAuthorization(policy => policy.RequireRole("Admin", "Board", "Member"));

accommodationsRead.MapGet("/{editionId:guid}/accommodations", GetAccommodationsByEdition);
accommodationsRead.MapGet("/accommodations/{id:guid}", GetAccommodationById);
```

#### 10. Registration Endpoints — extend `src/Abuvi.API/Features/Registrations/RegistrationsEndpoints.cs`

Add to existing registration endpoints:

```
PUT /api/registrations/{id}/accommodation-preferences → UpdateAccommodationPreferences
GET /api/registrations/{id}/accommodation-preferences → GetAccommodationPreferences
```

These should also be callable during the registration wizard flow. Include accommodation preferences in the main registration response DTO.

#### 11. Migration

Generate via:
```bash
dotnet ef migrations add AddCampEditionAccommodations --project src/Abuvi.API
```

#### 12. DI Registration — `src/Abuvi.API/Program.cs` (extend)

```csharp
builder.Services.AddScoped<ICampEditionAccommodationsRepository, CampEditionAccommodationsRepository>();
builder.Services.AddScoped<CampEditionAccommodationsService>();
```

---

## API Endpoints Summary

| Method | Endpoint | Description | Auth | Validation |
| ------ | -------- | ----------- | ---- | ---------- |
| GET | `/api/camps/editions/{editionId}/accommodations` | List accommodations for edition | Member+ | — |
| GET | `/api/camps/editions/accommodations/{id}` | Get single accommodation | Member+ | — |
| POST | `/api/camps/editions/{editionId}/accommodations` | Create accommodation option | Board+ | `CreateCampEditionAccommodationRequestValidator` |
| PUT | `/api/camps/editions/accommodations/{id}` | Update accommodation option | Board+ | `UpdateCampEditionAccommodationRequestValidator` |
| DELETE | `/api/camps/editions/accommodations/{id}` | Delete (only if no preferences) | Board+ | — |
| PATCH | `/api/camps/editions/accommodations/{id}/activate` | Activate accommodation | Board+ | — |
| PATCH | `/api/camps/editions/accommodations/{id}/deactivate` | Deactivate accommodation | Board+ | — |
| GET | `/api/registrations/{id}/accommodation-preferences` | Get registration's preferences | Member+ | — |
| PUT | `/api/registrations/{id}/accommodation-preferences` | Update registration's preferences | Member+ | `UpdateRegistrationAccommodationPreferencesRequestValidator` |

---

## Frontend Implementation

### Files to Create/Modify

#### 1. Types — `frontend/src/types/camp-edition.ts` (extend)

```typescript
export type AccommodationType = 'Lodge' | 'Caravan' | 'Tent' | 'Bungalow' | 'Motorhome'

export interface CampEditionAccommodation {
  id: string
  campEditionId: string
  name: string
  accommodationType: AccommodationType
  description?: string
  capacity?: number
  isActive: boolean
  sortOrder: number
  currentPreferenceCount: number
  firstChoiceCount: number
  createdAt: string
  updatedAt: string
}

export interface CreateCampEditionAccommodationRequest {
  name: string
  accommodationType: AccommodationType
  description?: string
  capacity?: number
  sortOrder?: number
}

export interface UpdateCampEditionAccommodationRequest {
  name: string
  accommodationType: AccommodationType
  description?: string
  capacity?: number
  isActive: boolean
  sortOrder: number
}
```

#### 2. Types — `frontend/src/types/registration.ts` (extend)

```typescript
export interface WizardAccommodationPreference {
  campEditionAccommodationId: string
  accommodationName: string
  accommodationType: AccommodationType
  preferenceOrder: number
}

export interface AccommodationPreferenceRequest {
  campEditionAccommodationId: string
  preferenceOrder: number
}

export interface UpdateRegistrationAccommodationPreferencesRequest {
  preferences: AccommodationPreferenceRequest[]
}
```

#### 3. Composable — `frontend/src/composables/useCampAccommodations.ts` (new file)

Follow `useCampExtras.ts` pattern:

```typescript
export function useCampAccommodations(editionId: string) {
  // State refs: accommodations, loading, error
  // CRUD methods: fetchAccommodations, createAccommodation, updateAccommodation, deleteAccommodation
  // Activate/deactivate methods
  // Return all refs + methods
}
```

#### 4. Component — `frontend/src/components/registrations/RegistrationAccommodationSelector.vue` (new file)

Registration wizard sub-component for selecting accommodation preferences:

- **Props**: `accommodations: CampEditionAccommodation[]`, `modelValue: WizardAccommodationPreference[]`
- **Emits**: `update:modelValue`
- **UI approach**: Three numbered dropdowns (1st, 2nd, 3rd choice)
  - Each dropdown shows available accommodations filtered to exclude already-selected ones
  - Show accommodation type icon/badge + name + capacity info
  - Optional: "No preference" option for 2nd and 3rd choice
  - PrimeVue `Select` component for each preference slot
- **Alternative UI**: PrimeVue `OrderList` for drag-to-rank (evaluate during implementation)
- **Display**: Show capacity info as subtitle: e.g. "Refugio A — Capacidad: 12 familias"
- **Empty state**: "No hay opciones de alojamiento para esta edición"

#### 5. Component — `frontend/src/components/camps/CampEditionAccommodationDialog.vue` (new file)

Admin CRUD dialog for managing accommodations per edition:

- **Trigger**: Button in camp edition admin view
- **Layout**: PrimeVue `Dialog` with form fields
  - `Name` — InputText (required)
  - `Accommodation Type` — Select/Dropdown with enum values
  - `Description` — Textarea (optional)
  - `Capacity` — InputNumber (optional, min 1)
  - `Sort Order` — InputNumber (min 0)
- **Table**: List of existing accommodations with edit/delete actions, active/inactive toggle
- **Capacity indicator**: Show `currentPreferenceCount / capacity` (or "∞" if unlimited)

#### 6. Wizard Integration — `frontend/src/views/registrations/RegisterForCampPage.vue` (modify)

Add accommodation preferences as a new step in the wizard:

- **New Step 3**: "Alojamiento" (between current Step 2 "Extras" and Step 3 "Confirmar")
- **Step ordering**: Participantes → Extras → Alojamiento → Confirmar
- **Data**: `accommodationPreferences = ref<WizardAccommodationPreference[]>([])`
- **Conditional**: Only show step if the edition has active accommodations
- **Review step**: Show selected preferences in order in the confirmation step

#### 7. Admin View Integration

Add accommodation management to the camp edition detail/admin page:

- Table/list of accommodations with CRUD actions
- Inline preference count display
- Sortable by `sortOrder`

---

## Acceptance Criteria

### Admin — Accommodation Management

- [ ] Board+ users can create accommodation options for a camp edition
- [ ] Board+ users can edit accommodation name, type, description, capacity, sort order
- [ ] Board+ users can activate/deactivate accommodations
- [ ] Board+ users can delete accommodations only if no registrations reference them
- [ ] Accommodation list shows preference counts (total + first-choice)
- [ ] Accommodations are sorted by `sortOrder` then `name`

### Registration Wizard — Preference Selection

- [ ] Registration wizard shows accommodation step when edition has active accommodations
- [ ] Families can select up to 3 ranked preferences
- [ ] Each preference slot shows a dropdown of available accommodations
- [ ] Already-selected accommodations are excluded from other dropdowns
- [ ] Capacity info is visible but does not block selection
- [ ] Preferences are optional (can be skipped)
- [ ] Selected preferences appear in the confirmation/review step

### API & Validation

- [ ] All endpoints require appropriate authorization (Board+ for write, Member+ for read)
- [ ] Create/update requests are validated via FluentValidation
- [ ] Delete returns 400 with message if accommodation has existing preferences
- [ ] Preference order is validated (1-3, unique per registration)
- [ ] No duplicate accommodation selections per registration

### Data Integrity

- [ ] Cascade delete: deleting a camp edition removes its accommodations
- [ ] Restrict delete: cannot delete accommodation with existing preferences
- [ ] Cascade delete: deleting a registration removes its preferences
- [ ] Unique constraints prevent duplicate preference entries

---

## Non-Functional Requirements

### Performance

- Accommodation list queries should use `Include()` sparingly — preference counts can be computed via separate count queries or projected in the response
- Index on `camp_edition_accommodations(camp_edition_id, is_active)` for filtered queries
- Index on `registration_accommodation_preferences(registration_id)` for preference lookups

### Security

- All endpoints require JWT authentication
- Write endpoints restricted to Admin/Board roles
- Users can only modify preferences for registrations they own (or are Admin/Board)

---

## Testing Requirements

### Backend Unit Tests

- Service: Create/update/delete accommodation happy paths
- Service: Delete with existing preferences throws error
- Service: Preference count computation
- Validator: All validation rules for create/update/preference requests
- Repository: Query filters (activeOnly, by edition)

### Backend Integration Tests

- Full CRUD cycle via HTTP endpoints
- Authorization enforcement (Member cannot create, Board can)
- Preference submission as part of registration
- Delete protection when preferences exist

### Frontend

- Wizard step visibility (conditional on accommodations existing)
- Dropdown filtering (exclude already-selected)
- Max 3 preferences enforcement
- Admin CRUD dialog interactions

---

## Dependencies

- `feat-camp-edition-extras` — follow the same pattern for edition-level configuration entities
- Existing Registration wizard infrastructure (RegisterForCampPage.vue)
- Existing CampEdition admin view

---

## Implementation Order

### Phase 1: Backend — Entity & CRUD (estimated ~3 files new, ~4 files modified)

1. Add entity + DTOs to `CampsModels.cs` and `RegistrationsModels.cs`
2. Create EF configurations
3. Update `AbuviDbContext.cs` with new DbSets
4. Generate and apply migration
5. Create repository + service
6. Add validators to `CampsValidators.cs`
7. Add endpoints to `CampsEndpoints.cs`
8. Register DI in `Program.cs`

### Phase 2: Backend — Registration Integration (~2 files modified)

1. Add preference endpoints to `RegistrationsEndpoints.cs`
2. Include preferences in registration response DTOs
3. Handle preference save/update in registration service

### Phase 3: Frontend — Admin CRUD (~2 files new, ~1 file modified)

1. Add types to `camp-edition.ts`
2. Create `useCampAccommodations.ts` composable
3. Create `CampEditionAccommodationDialog.vue`
4. Integrate into camp edition admin page

### Phase 4: Frontend — Registration Wizard (~2 files new, ~2 files modified)

1. Add types to `registration.ts`
2. Create `RegistrationAccommodationSelector.vue`
3. Add wizard step to `RegisterForCampPage.vue`
4. Update confirmation step to show preferences

---

## Document Control

- **Created**: 2026-02-26
- **Enriched**: 2026-02-27
- **Status**: ENRICHED — Ready for implementation planning with `/plan-backend-ticket` or `/plan-frontend-ticket`
- **Origin**: Extracted from `feat-registration-extra-fields2`
