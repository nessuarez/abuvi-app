# Backend Implementation Plan: feat-registration-accommodations — Accommodation Preferences System

## Context

Families registering for camp editions need to express their accommodation preferences (lodge, caravan, tent, etc.). Currently the `Camp` entity stores facility capacity as JSON, but there's no way to define per-edition accommodation options or collect ranked family preferences during registration. This feature adds two new entities: `CampEditionAccommodation` (admin-configurable options) and `RegistrationAccommodationPreference` (family ranked choices 1st/2nd/3rd).

**Important distinction**: This is a **preference ranking system**, NOT a pricing/quantity system like CampEditionExtras. Accommodations have no price — families simply rank up to 3 choices. Capacity is informational only.

---

## Architecture Context

- **Feature slice**: `src/Abuvi.API/Features/Camps/` (accommodation CRUD) + `src/Abuvi.API/Features/Registrations/` (preference selection)
- **Pattern reference**: Mirrors `CampEditionExtra`/`CampEditionExtrasService`/`CampEditionExtrasRepository` for the CRUD side
- **Branch**: `feature/feat-registration-accommodations-backend`

---

## Implementation Steps

### Step 0: Create Feature Branch

- **Action**: Create `feature/feat-registration-accommodations-backend` from `main`

- ```bash
  git fetch origin main
  git checkout -b feature/feat-registration-accommodations-backend origin/main
  ```

### Step 1: Entity & DTOs — CampsModels.cs

- **File**: `src/Abuvi.API/Features/Camps/CampsModels.cs` (extend)
- **Action**: Add `AccommodationType` enum, `CampEditionAccommodation` entity, request/response DTOs, ToResponse extension

**Add enum:**

```csharp
public enum AccommodationType
{
    Lodge,       // Refugio / cabaña
    Caravan,     // Caravana
    Tent,        // Tienda de campaña
    Bungalow,    // Bungalow
    Motorhome    // Autocaravana
}
```

**Add entity:**

```csharp
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
    public CampEdition CampEdition { get; set; } = null!;
}
```

**Add DTOs:**

```csharp
public record CampEditionAccommodationResponse(
    Guid Id, Guid CampEditionId, string Name, AccommodationType AccommodationType,
    string? Description, int? Capacity, bool IsActive, int SortOrder,
    int CurrentPreferenceCount, int FirstChoiceCount,
    DateTime CreatedAt, DateTime UpdatedAt);

public record CreateCampEditionAccommodationRequest(
    string Name, AccommodationType AccommodationType, string? Description,
    int? Capacity, int SortOrder = 0);

public record UpdateCampEditionAccommodationRequest(
    string Name, AccommodationType AccommodationType, string? Description,
    int? Capacity, bool IsActive, int SortOrder);
```

**Add to `CampEdition` class navigation properties:**

```csharp
public ICollection<CampEditionAccommodation> Accommodations { get; set; } = [];
```

**Add ToResponse extension** (similar pattern to `CampEditionExtraExtensions`):

```csharp
internal static class CampEditionAccommodationExtensions
{
    public static CampEditionAccommodationResponse ToResponse(
        this CampEditionAccommodation a, int currentPreferenceCount, int firstChoiceCount)
        => new(a.Id, a.CampEditionId, a.Name, a.AccommodationType,
            a.Description, a.Capacity, a.IsActive, a.SortOrder,
            currentPreferenceCount, firstChoiceCount, a.CreatedAt, a.UpdatedAt);
}
```

### Step 2: Entity & DTOs — RegistrationsModels.cs

- **File**: `src/Abuvi.API/Features/Registrations/RegistrationsModels.cs` (extend)
- **Action**: Add `RegistrationAccommodationPreference` entity, request/response DTOs

**Add entity:**

```csharp
public class RegistrationAccommodationPreference
{
    public Guid Id { get; set; }
    public Guid RegistrationId { get; set; }
    public Guid CampEditionAccommodationId { get; set; }
    public int PreferenceOrder { get; set; }
    public DateTime CreatedAt { get; set; }
    public Registration Registration { get; set; } = null!;
    public CampEditionAccommodation CampEditionAccommodation { get; set; } = null!;
}
```

**Add to `Registration` class navigation properties:**

```csharp
public ICollection<RegistrationAccommodationPreference> AccommodationPreferences { get; set; } = [];
```

**Add DTOs:**

```csharp
public record AccommodationPreferenceRequest(Guid CampEditionAccommodationId, int PreferenceOrder);

public record UpdateRegistrationAccommodationPreferencesRequest(
    List<AccommodationPreferenceRequest> Preferences);

public record AccommodationPreferenceResponse(
    Guid CampEditionAccommodationId, string AccommodationName,
    AccommodationType AccommodationType, int PreferenceOrder);
```

### Step 3: EF Configuration — CampEditionAccommodationConfiguration.cs

- **File**: `src/Abuvi.API/Data/Configurations/CampEditionAccommodationConfiguration.cs` (NEW)
- **Pattern**: Copy `CampEditionExtraConfiguration.cs` structure

Key differences from extras:

- Table: `camp_edition_accommodations`
- No price/pricing columns
- `accommodation_type` enum as string(30)
- `sort_order` int with check `>= 0`, default 0
- `capacity` int? with check `IS NULL OR > 0`
- FK to `camp_editions` with CASCADE delete

### Step 4: EF Configuration — RegistrationAccommodationPreferenceConfiguration.cs

- **File**: `src/Abuvi.API/Data/Configurations/RegistrationAccommodationPreferenceConfiguration.cs` (NEW)
- **Pattern**: Similar to `RegistrationExtraConfiguration.cs`

Key points:

- Table: `registration_accommodation_preferences`
- `preference_order` int, check `>= 1 AND <= 3`
- FK to `registrations` with CASCADE
- FK to `camp_edition_accommodations` with RESTRICT
- Unique index: `(registration_id, camp_edition_accommodation_id)`
- Unique index: `(registration_id, preference_order)`
- Id with `gen_random_uuid()` default

### Step 5: DbContext

- **File**: `src/Abuvi.API/Data/AbuviDbContext.cs` (extend)
- **Action**: Add 2 DbSet properties:

```csharp
public DbSet<CampEditionAccommodation> CampEditionAccommodations => Set<CampEditionAccommodation>();
public DbSet<RegistrationAccommodationPreference> RegistrationAccommodationPreferences => Set<RegistrationAccommodationPreference>();
```

### Step 6: Repository Interface

- **File**: `src/Abuvi.API/Features/Camps/ICampEditionAccommodationsRepository.cs` (NEW)
- **Pattern**: Copy `ICampEditionExtrasRepository.cs`

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

### Step 7: Repository Implementation

- **File**: `src/Abuvi.API/Features/Camps/CampEditionAccommodationsRepository.cs` (NEW)
- **Pattern**: Copy `CampEditionExtrasRepository.cs`

Key differences:

- `GetByCampEditionAsync` orders by `SortOrder` then `Name` (not `CreatedAt`)
- `HasPreferencesAsync` queries `RegistrationAccommodationPreferences` table
- `GetPreferenceCountAsync` counts distinct registrations that have this accommodation as any preference
- `GetFirstChoiceCountAsync` counts registrations where `PreferenceOrder == 1`

### Step 8: Service

- **File**: `src/Abuvi.API/Features/Camps/CampEditionAccommodationsService.cs` (NEW)
- **Pattern**: Copy `CampEditionExtrasService.cs`

Methods (all follow extras pattern but simpler — no pricing logic):

- `GetByEditionAsync(Guid campEditionId, bool? activeOnly, CancellationToken ct)` — returns list with preference counts
- `GetByIdAsync(Guid id, CancellationToken ct)` — returns single with preference counts
- `CreateAsync(Guid campEditionId, CreateCampEditionAccommodationRequest request, CancellationToken ct)` — validate edition exists + not closed, create entity
- `UpdateAsync(Guid id, UpdateCampEditionAccommodationRequest request, CancellationToken ct)` — validate exists, update fields
- `DeleteAsync(Guid id, CancellationToken ct)` — check `HasPreferencesAsync`, throw if preferences exist, else delete
- `ActivateAsync(Guid id, CancellationToken ct)` / `DeactivateAsync(Guid id, CancellationToken ct)`

**Delete business rule**: Cannot delete if `HasPreferencesAsync` returns true. Error: "No se puede eliminar el alojamiento '{name}' porque ha sido seleccionado en preferencias de inscripción. Considera desactivarlo en su lugar."

### Step 9: Validators

- **File**: `src/Abuvi.API/Features/Camps/CampsValidators.cs` (extend)
- **Action**: Add 3 validators at end of file

**`CreateCampEditionAccommodationRequestValidator`**:

- Name: NotEmpty, MaxLength(200)
- AccommodationType: IsInEnum
- Description: MaxLength(1000) when not null
- Capacity: GreaterThan(0) when HasValue
- SortOrder: GreaterThanOrEqualTo(0)

**`UpdateCampEditionAccommodationRequestValidator`**:

- Same rules as Create

**`UpdateRegistrationAccommodationPreferencesRequestValidator`**:

- Preferences: count <= 3
- Preferences: no duplicate CampEditionAccommodationId
- Preferences: no duplicate PreferenceOrder
- Each: PreferenceOrder InclusiveBetween(1, 3)
- Each: CampEditionAccommodationId NotEmpty

### Step 10: Endpoints — CampsEndpoints.cs

- **File**: `src/Abuvi.API/Features/Camps/CampsEndpoints.cs` (extend)
- **Action**: Add accommodation route groups + handlers after the extras section (before `return app;`)
- **Pattern**: Exact same 4-group pattern as extras (write edition-scoped, read edition-scoped, write by-id, read by-id)

**Route groups** (add before `return app;` at line ~363):

1. `accommodationsWriteGroup` — `POST /api/camps/editions/{editionId:guid}/accommodations` (Board+)
2. `accommodationsReadGroup` — `GET /api/camps/editions/{editionId:guid}/accommodations` (Member+)
3. `accommodationsByIdWriteGroup` — `PUT, DELETE, PATCH activate/deactivate /api/camps/editions/accommodations/{id:guid}` (Board+)
4. `accommodationsByIdReadGroup` — `GET /api/camps/editions/accommodations/{id:guid}` (Member+)

**Handlers** (add after extras handlers at line ~920):

- `GetAccommodationsByEdition`, `GetAccommodationById`, `CreateAccommodation`, `UpdateAccommodation`, `DeleteAccommodation`, `ActivateAccommodation`, `DeactivateAccommodation`
- Follow exact same try/catch pattern as extras handlers

### Step 11: Registration Endpoints — RegistrationsEndpoints.cs

- **File**: `src/Abuvi.API/Features/Registrations/RegistrationsEndpoints.cs` (extend)
- **Action**: Add 2 endpoints to existing group

```
PUT  /api/registrations/{id:guid}/accommodation-preferences → SetAccommodationPreferences
GET  /api/registrations/{id:guid}/accommodation-preferences → GetAccommodationPreferences
```

- PUT handler: validate user owns registration, clear existing preferences, save new ones
- GET handler: return ordered list of `AccommodationPreferenceResponse`

### Step 12: Registration Service Integration

- **File**: `src/Abuvi.API/Features/Registrations/RegistrationsService.cs` (extend)
- **Action**: Add `SetAccommodationPreferencesAsync` and `GetAccommodationPreferencesAsync` methods

`SetAccommodationPreferencesAsync(Guid registrationId, Guid userId, UpdateRegistrationAccommodationPreferencesRequest request, CancellationToken ct)`:

1. Load registration, verify user is family representative or Admin/Board
2. Verify registration status is Pending
3. Validate each accommodation exists, belongs to the edition, and is active
4. Delete existing preferences for this registration
5. Insert new preferences
6. Return list of `AccommodationPreferenceResponse`

`GetAccommodationPreferencesAsync(Guid registrationId, CancellationToken ct)`:

1. Load preferences with `Include(p => p.CampEditionAccommodation)`
2. Return ordered by `PreferenceOrder`

### Step 13: DI Registration — Program.cs

- **File**: `src/Abuvi.API/Program.cs` (extend)
- **Action**: Add after line 158 (CampEditionExtrasService):

```csharp
builder.Services.AddScoped<ICampEditionAccommodationsRepository, CampEditionAccommodationsRepository>();
builder.Services.AddScoped<CampEditionAccommodationsService>();
```

### Step 14: Migration

```bash
dotnet ef migrations add AddCampEditionAccommodations --project src/Abuvi.API
```

Tables created:

- `camp_edition_accommodations` (with check constraints, indexes)
- `registration_accommodation_preferences` (with unique indexes, FK constraints)

### Step 15: Update Technical Documentation

- Update `ai-specs/specs/data-model.md` with new entities
- Update `ai-specs/specs/api-endpoints.md` with new endpoints
- Update enriched spec status to "IN PROGRESS" or "IMPLEMENTED"

---

## Files Summary

### NEW files (7)

1. `src/Abuvi.API/Features/Camps/ICampEditionAccommodationsRepository.cs`
2. `src/Abuvi.API/Features/Camps/CampEditionAccommodationsRepository.cs`
3. `src/Abuvi.API/Features/Camps/CampEditionAccommodationsService.cs`
4. `src/Abuvi.API/Data/Configurations/CampEditionAccommodationConfiguration.cs`
5. `src/Abuvi.API/Data/Configurations/RegistrationAccommodationPreferenceConfiguration.cs`

### MODIFIED files (7)

1. `src/Abuvi.API/Features/Camps/CampsModels.cs` — enum + entity + DTOs + extension
2. `src/Abuvi.API/Features/Registrations/RegistrationsModels.cs` — entity + DTOs + Registration nav property
3. `src/Abuvi.API/Features/Camps/CampsValidators.cs` — 3 new validators
4. `src/Abuvi.API/Features/Camps/CampsEndpoints.cs` — 4 route groups + 7 handlers
5. `src/Abuvi.API/Features/Registrations/RegistrationsEndpoints.cs` — 2 endpoints + 2 handlers
6. `src/Abuvi.API/Features/Registrations/RegistrationsService.cs` — 2 new methods
7. `src/Abuvi.API/Data/AbuviDbContext.cs` — 2 DbSets
8. `src/Abuvi.API/Program.cs` — 2 DI registrations

---

## Implementation Order

1. Step 0: Create feature branch
2. Step 1-2: Entities & DTOs (CampsModels + RegistrationsModels)
3. Step 3-4: EF Configurations (2 new files)
4. Step 5: DbContext (add DbSets)
5. Step 14: Migration (generate & verify)
6. Step 6-7: Repository (interface + implementation)
7. Step 8: Service
8. Step 9: Validators
9. Step 10: Camps endpoints
10. Step 13: DI registration (Program.cs)
11. Step 11-12: Registration endpoints + service integration
12. Build & verify compilation
13. Step 15: Documentation update

---

## Testing Checklist

### Unit Tests

- [ ] Service: CreateAsync happy path
- [ ] Service: CreateAsync with closed edition throws
- [ ] Service: UpdateAsync happy path
- [ ] Service: DeleteAsync with no preferences succeeds
- [ ] Service: DeleteAsync with existing preferences throws
- [ ] Service: ActivateAsync / DeactivateAsync
- [ ] Validator: CreateRequest all rules
- [ ] Validator: UpdateRequest all rules
- [ ] Validator: PreferencesRequest max 3, no duplicates, order 1-3

### Integration Tests

- [ ] CRUD cycle via HTTP (create, list, update, get, delete)
- [ ] Authorization: Member cannot POST/PUT/DELETE, Board can
- [ ] Preference submission: PUT /registrations/{id}/accommodation-preferences
- [ ] Preference retrieval: GET /registrations/{id}/accommodation-preferences
- [ ] Delete protection: 400 when accommodation has preferences

---

## Error Response Format

All errors use `ApiResponse<T>` envelope:

- **400 Bad Request**: Validation errors, business rule violations (`OPERATION_ERROR`)
- **401 Unauthorized**: Missing/invalid JWT
- **403 Forbidden**: Insufficient role
- **404 Not Found**: Entity not found
- **204 No Content**: Successful delete

---

## Verification

After implementation:

1. `dotnet build` — no compilation errors
2. `dotnet ef migrations list` — new migration listed
3. Run the API and verify Swagger shows all 9 new endpoints
4. Test CRUD flow via Swagger/curl
5. Test preference submission via registration endpoint
