# Feature Spec: feat-registration-tshirt-sizes — T-Shirt Size Selection per Participant

## Status: ENRICHED — Ready for implementation planning

## Origin

User request: "Me preguntan si podemos poner en la inscripción que se indique la talla de camiseta de cada uno. El rango de valores para adultos va de XS a XL pero las de niños irá por edades: 2, 4, 6, 8 años. Es un ejemplo ya que aun no tenemos los rangos definitivos."

---

## Problem Statement

During camp registration, organizers need to collect each participant's t-shirt size so they can order the correct quantities before camp starts. T-shirt sizes differ by age group:

- **Adults**: standard textile sizes (e.g. XS, S, M, L, XL)
- **Children**: sized by age (e.g. 2, 4, 6, 8 años)
- **Babies**: may or may not need a size (TBD by organizers)

The exact size options are **not yet finalized** and may change between editions, so the system must allow organizers to configure available sizes per camp edition rather than hardcoding them.

---

## Scope

### In scope (this feature)

- `CampEditionTShirtSize` entity — admin-configurable size options per edition, categorized by age group
- `RegistrationMember.TShirtSizeId` — each participant selects their size during registration
- Admin UI to manage available sizes per edition (CRUD)
- Registration wizard integration — size selection per member in the Participants step
- Admin report/export view: size summary counts per edition

### Out of scope

- Inventory/stock management for t-shirts
- T-shirt pricing (currently t-shirts are included; if pricing is needed, use the existing `CampEditionExtra` system)
- Automatic size suggestion based on age (organizers define the options, users choose)

---

## Data Model

### `CampEditionTShirtSize` — Available sizes per edition

| Field | Type | Constraints | Description |
| ----- | ---- | ----------- | ----------- |
| `Id` | `Guid` | PK, auto-generated | Primary key |
| `CampEditionId` | `Guid` (FK) | NOT NULL, CASCADE delete | Which edition offers this size |
| `AgeCategory` | `AgeCategory` enum | NOT NULL, stored as string(10) | `Baby`, `Child`, `Adult` — reuses existing enum |
| `Label` | `string` | NOT NULL, max 20 chars | Display label (e.g. "M", "XL", "4 años") |
| `SortOrder` | `int` | NOT NULL, default 0 | Display ordering within age category |
| `IsActive` | `bool` | NOT NULL, default `true` | Can be disabled without deletion |
| `CreatedAt` | `DateTime` | NOT NULL, default UTC NOW | Record creation timestamp |
| `UpdatedAt` | `DateTime` | NOT NULL, default UTC NOW | Last update timestamp |

**Table name**: `camp_edition_tshirt_sizes`
**Unique index**: `(camp_edition_id, age_category, label)` — no duplicate labels within same category and edition

### `RegistrationMember` — Extend existing entity

| New Field | Type | Constraints | Description |
| --------- | ---- | ----------- | ----------- |
| `TShirtSizeId` | `Guid?` (FK) | NULL, RESTRICT delete | Selected t-shirt size (optional until sizes are configured) |

**FK**: `registration_members.tshirt_size_id → camp_edition_tshirt_sizes.id` with RESTRICT delete

### RegistrationMember behavior

- `TShirtSizeId` is **nullable** — if no sizes are configured for the edition, this field stays null
- When sizes ARE configured for the edition, the registration wizard will show a dropdown per member
- The selected size must match the member's `AgeCategory` (validated server-side)

---

## Key Business Rules

1. Size options are **configurable per camp edition** — different editions can have different size ranges
2. Sizes are **categorized by `AgeCategory`** (Baby, Child, Adult) — each member sees only the sizes for their age group
3. The member's `AgeCategory` is determined at registration time (already calculated by `RegistrationPricingService.GetAgeCategory`)
4. Size selection is **optional** — if the edition has no configured sizes, the step/field is hidden
5. When sizes ARE configured, selection should be **encouraged but not mandatory** at registration time (families may not know sizes upfront). The field can be updated later via the members endpoint
6. Admin can bulk-configure sizes with a "copy from previous edition" convenience action (nice-to-have)
7. Deleting a size that has existing registrations referencing it should be **prevented** (RESTRICT)
8. Deactivated sizes don't appear in the registration dropdown but remain visible in admin views and existing registrations

---

## Backend Implementation

### Files to Create/Modify

#### 1. Entity & DTOs — `src/Abuvi.API/Features/Camps/CampsModels.cs` (extend)

```csharp
// === T-Shirt Sizes ===

public class CampEditionTShirtSize
{
    public Guid Id { get; set; }
    public Guid CampEditionId { get; set; }
    public AgeCategory AgeCategory { get; set; }
    public string Label { get; set; } = string.Empty;
    public int SortOrder { get; set; } = 0;
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    // Navigation
    public CampEdition CampEdition { get; set; } = null!;
}

// === DTOs ===

public record CampEditionTShirtSizeResponse(
    Guid Id,
    Guid CampEditionId,
    AgeCategory AgeCategory,
    string Label,
    int SortOrder,
    bool IsActive,
    int UsageCount,  // How many registration members reference this size
    DateTime CreatedAt,
    DateTime UpdatedAt
);

public record CreateCampEditionTShirtSizeRequest(
    AgeCategory AgeCategory,
    string Label,
    int SortOrder = 0
);

public record UpdateCampEditionTShirtSizeRequest(
    AgeCategory AgeCategory,
    string Label,
    int SortOrder,
    bool IsActive
);

public record BulkCreateCampEditionTShirtSizesRequest(
    List<CreateCampEditionTShirtSizeRequest> Sizes
);
```

Add to `CampEdition` navigation properties:

```csharp
public ICollection<CampEditionTShirtSize> TShirtSizes { get; set; } = [];
```

#### 2. Extend RegistrationMember — `src/Abuvi.API/Features/Registrations/RegistrationsModels.cs`

Add field to `RegistrationMember`:

```csharp
public Guid? TShirtSizeId { get; set; }

// Navigation
public CampEditionTShirtSize? TShirtSize { get; set; }
```

Extend `RegistrationMemberResponse` to include:

```csharp
public record RegistrationMemberResponse(
    // ... existing fields ...
    Guid? TShirtSizeId,
    string? TShirtSizeLabel  // denormalized for display
);
```

Extend `MemberAttendanceRequest` (or create a separate update DTO):

```csharp
public record MemberAttendanceRequest(
    // ... existing fields ...
    Guid? TShirtSizeId
);
```

#### 3. EF Configuration — `src/Abuvi.API/Data/Configurations/CampEditionTShirtSizeConfiguration.cs` (new file)

- Table: `camp_edition_tshirt_sizes`
- Snake_case column naming
- FK to `camp_editions` with CASCADE delete
- Enum `AgeCategory` stored as string (max 10)
- Unique index on `(camp_edition_id, age_category, label)`
- Default values: `is_active = true`, `sort_order = 0`, timestamps = `now()`
- `label` max length 20

#### 4. Extend RegistrationMember Configuration — `src/Abuvi.API/Data/Configurations/RegistrationMemberConfiguration.cs` (modify)

Add:

```csharp
builder.Property(rm => rm.TShirtSizeId).HasColumnName("tshirt_size_id");
builder.HasOne(rm => rm.TShirtSize)
    .WithMany()
    .HasForeignKey(rm => rm.TShirtSizeId)
    .OnDelete(DeleteBehavior.Restrict);
```

#### 5. DbContext — `src/Abuvi.API/Data/AbuviDbContext.cs` (extend)

```csharp
public DbSet<CampEditionTShirtSize> CampEditionTShirtSizes { get; set; }
```

#### 6. Repository — `src/Abuvi.API/Features/Camps/CampEditionTShirtSizesRepository.cs` (new file)

```csharp
public interface ICampEditionTShirtSizesRepository
{
    Task<CampEditionTShirtSize?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<List<CampEditionTShirtSize>> GetByCampEditionAsync(Guid campEditionId, bool? activeOnly = null, CancellationToken ct = default);
    Task<List<CampEditionTShirtSize>> GetByCampEditionAndCategoryAsync(Guid campEditionId, AgeCategory category, CancellationToken ct = default);
    Task AddAsync(CampEditionTShirtSize size, CancellationToken ct = default);
    Task AddRangeAsync(IEnumerable<CampEditionTShirtSize> sizes, CancellationToken ct = default);
    Task UpdateAsync(CampEditionTShirtSize size, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
    Task<bool> HasRegistrationsAsync(Guid sizeId, CancellationToken ct = default);
    Task<int> GetUsageCountAsync(Guid sizeId, CancellationToken ct = default);
}
```

#### 7. Service — `src/Abuvi.API/Features/Camps/CampEditionTShirtSizesService.cs` (new file)

Follow `CampEditionExtrasService.cs` pattern:

- `CreateAsync(Guid campEditionId, CreateCampEditionTShirtSizeRequest request, CancellationToken ct)` → validate edition exists, check no duplicate label in same category, save
- `BulkCreateAsync(Guid campEditionId, BulkCreateCampEditionTShirtSizesRequest request, CancellationToken ct)` → create multiple sizes at once (for initial setup)
- `UpdateAsync(Guid id, UpdateCampEditionTShirtSizeRequest request, CancellationToken ct)` → validate, update fields + UpdatedAt
- `DeleteAsync(Guid id, CancellationToken ct)` → check for existing registrations, throw if referenced
- `GetByEditionAsync(Guid editionId, bool? activeOnly, CancellationToken ct)` → return list grouped by age category with usage counts
- `CopyFromEditionAsync(Guid sourceEditionId, Guid targetEditionId, CancellationToken ct)` → copy all sizes from one edition to another (nice-to-have)

#### 8. Validators — `src/Abuvi.API/Features/Camps/CampsValidators.cs` (extend)

```csharp
public class CreateCampEditionTShirtSizeRequestValidator
    : AbstractValidator<CreateCampEditionTShirtSizeRequest>
{
    public CreateCampEditionTShirtSizeRequestValidator()
    {
        RuleFor(x => x.AgeCategory)
            .IsInEnum().WithMessage("Categoría de edad no válida");

        RuleFor(x => x.Label)
            .NotEmpty().WithMessage("La talla es obligatoria")
            .MaximumLength(20).WithMessage("La talla no puede superar los 20 caracteres");

        RuleFor(x => x.SortOrder)
            .GreaterThanOrEqualTo(0).WithMessage("El orden debe ser mayor o igual a 0");
    }
}

public class BulkCreateCampEditionTShirtSizesRequestValidator
    : AbstractValidator<BulkCreateCampEditionTShirtSizesRequest>
{
    public BulkCreateCampEditionTShirtSizesRequestValidator()
    {
        RuleFor(x => x.Sizes)
            .NotEmpty().WithMessage("Debe incluir al menos una talla");

        RuleForEach(x => x.Sizes)
            .SetValidator(new CreateCampEditionTShirtSizeRequestValidator());
    }
}
```

#### 9. Extend Registration Validation

In the `MemberAttendanceRequest` validator or registration service, add server-side validation:

- If `TShirtSizeId` is provided, validate that:
  - The size belongs to the same camp edition as the registration
  - The size's `AgeCategory` matches the member's calculated `AgeCategory`
  - The size `IsActive` is true

#### 10. Endpoints — `src/Abuvi.API/Features/Camps/CampsEndpoints.cs` (extend)

```csharp
// === Camp Edition T-Shirt Sizes (Board+ write, Member+ read) ===

// Write endpoints (Board+)
POST   /api/camps/editions/{editionId}/tshirt-sizes       → CreateTShirtSize
POST   /api/camps/editions/{editionId}/tshirt-sizes/bulk   → BulkCreateTShirtSizes
PUT    /api/camps/editions/tshirt-sizes/{id}               → UpdateTShirtSize
DELETE /api/camps/editions/tshirt-sizes/{id}               → DeleteTShirtSize

// Read endpoints (Member+)
GET    /api/camps/editions/{editionId}/tshirt-sizes        → GetTShirtSizesByEdition
  // Query param: ?ageCategory=Adult (optional filter)
```

#### 11. Extend Registration Endpoints

The existing `PUT /api/registrations/{id}/members` endpoint already accepts `MemberAttendanceRequest[]`. Extend this request to include `TShirtSizeId` per member. No new endpoint needed.

Include `TShirtSizeId` and `TShirtSizeLabel` in the registration detail response (`GET /api/registrations/{id}`).

#### 12. Include sizes in available editions endpoint

Extend `GET /api/camps/editions/available` or create a sub-resource so the frontend knows if the edition has t-shirt sizes configured.

#### 13. Migration

```bash
dotnet ef migrations add AddCampEditionTShirtSizes --project src/Abuvi.API
```

#### 14. DI Registration — `src/Abuvi.API/Program.cs` (extend)

```csharp
builder.Services.AddScoped<ICampEditionTShirtSizesRepository, CampEditionTShirtSizesRepository>();
builder.Services.AddScoped<CampEditionTShirtSizesService>();
```

---

## API Endpoints Summary

| Method | Endpoint | Description | Auth | Validation |
| ------ | -------- | ----------- | ---- | ---------- |
| GET | `/api/camps/editions/{editionId}/tshirt-sizes` | List sizes for edition (optional `?ageCategory` filter) | Member+ | — |
| POST | `/api/camps/editions/{editionId}/tshirt-sizes` | Create single size option | Board+ | `CreateCampEditionTShirtSizeRequestValidator` |
| POST | `/api/camps/editions/{editionId}/tshirt-sizes/bulk` | Create multiple sizes at once | Board+ | `BulkCreateCampEditionTShirtSizesRequestValidator` |
| PUT | `/api/camps/editions/tshirt-sizes/{id}` | Update size option | Board+ | `UpdateCampEditionTShirtSizeRequestValidator` |
| DELETE | `/api/camps/editions/tshirt-sizes/{id}` | Delete (only if not referenced) | Board+ | — |
| PUT | `/api/registrations/{id}/members` | Update members (now includes `tShirtSizeId`) | Member+ | Existing + size validation |

---

## Frontend Implementation

### Files to Create/Modify

#### 1. Types — `frontend/src/types/camp-edition.ts` (extend)

```typescript
export interface CampEditionTShirtSize {
  id: string
  campEditionId: string
  ageCategory: AgeCategory
  label: string
  sortOrder: number
  isActive: boolean
  usageCount: number
  createdAt: string
  updatedAt: string
}

export interface CreateCampEditionTShirtSizeRequest {
  ageCategory: AgeCategory
  label: string
  sortOrder?: number
}

export interface BulkCreateCampEditionTShirtSizesRequest {
  sizes: CreateCampEditionTShirtSizeRequest[]
}
```

#### 2. Types — `frontend/src/types/registration.ts` (extend)

Add `tShirtSizeId?: string` to `MemberAttendanceRequest` and `WizardMemberSelection`.

Add `tShirtSizeId?: string` and `tShirtSizeLabel?: string` to registration member response types.

#### 3. Composable — `frontend/src/composables/useCampTShirtSizes.ts` (new file)

Follow `useCampExtras.ts` pattern:

```typescript
export function useCampTShirtSizes(editionId: string) {
  // State: sizes (grouped by ageCategory), loading, error
  // Methods: fetchSizes, createSize, bulkCreateSizes, updateSize, deleteSize
  // Computed: sizesByCategory (Map<AgeCategory, CampEditionTShirtSize[]>)
}
```

#### 4. Integrate into RegistrationMemberSelector — `frontend/src/components/registrations/RegistrationMemberSelector.vue` (modify)

For each selected member, add a t-shirt size dropdown:

- **Conditional**: Only show if the edition has t-shirt sizes configured for that member's age category
- **Dropdown content**: Filtered by the member's `AgeCategory` (computed from their age at camp start)
- **Position**: Below the attendance period selector, next to guardian fields
- **Label**: "Talla de camiseta" with a PrimeVue `Select` component
- **Placeholder**: "Selecciona talla"
- **Not required**: Member can skip (null value allowed)

UI structure per member card:
```
[Member Name] — [Age at camp] — [Age Category badge]
├── Attendance Period: [Complete ▼]
├── T-Shirt Size: [M ▼]              ← NEW
├── Guardian Name: [________]          (if minor)
└── Guardian Document: [________]      (if minor)
```

#### 5. Admin Component — `frontend/src/components/camps/CampEditionTShirtSizeDialog.vue` (new file)

Admin dialog for managing sizes per edition:

- **Layout**: PrimeVue `Dialog` with a tab or section per `AgeCategory`
- **Per category section**:
  - Table of existing sizes (label, sort order, usage count, active toggle)
  - Inline add form: label input + add button
  - Delete button (disabled if `usageCount > 0`)
- **Bulk add**: Quick-fill buttons like "Añadir tallas estándar adulto (XS-XL)" and "Añadir tallas infantiles (2-8 años)"
- **Copy from edition**: Dropdown to select a previous edition and copy its sizes

#### 6. Integration into admin camp edition view

Add a "Tallas de camiseta" button/section in the camp edition admin page (alongside extras and accommodations management).

#### 7. Confirmation step — `RegisterForCampPage.vue` (modify)

Show each member's selected t-shirt size in the review/confirmation step:

```
Juan García — Adulto — Completa — Talla: L
María García — Niña (8 años) — Completa — Talla: 8 años
```

---

## Acceptance Criteria

### Admin — Size Management

- [ ] Board+ users can create t-shirt size options for a camp edition, categorized by age group
- [ ] Board+ users can bulk-create common size sets (e.g. "XS, S, M, L, XL" for adults)
- [ ] Board+ users can edit size label, sort order, active status
- [ ] Board+ users can delete sizes only if no registrations reference them
- [ ] Size list shows usage counts per size
- [ ] Sizes are displayed grouped by age category, sorted by `sortOrder`

### Registration Wizard — Size Selection

- [ ] T-shirt size dropdown appears per member when the edition has sizes configured
- [ ] Each member only sees sizes matching their age category
- [ ] Size selection is optional (can proceed without selecting)
- [ ] Selected sizes appear in the confirmation/review step
- [ ] Sizes are saved when updating registration members

### API & Validation

- [ ] All endpoints require appropriate authorization (Board+ for write, Member+ for read)
- [ ] Server validates that selected size belongs to the correct edition and age category
- [ ] Server validates size is active when selected
- [ ] Delete returns 400 with message if size is referenced by registrations
- [ ] Duplicate label within same edition+category is rejected

### Data Integrity

- [ ] Cascade delete: deleting a camp edition removes its t-shirt sizes
- [ ] Restrict delete: cannot delete a size referenced by registration members
- [ ] Unique constraint prevents duplicate labels per edition+category

---

## Non-Functional Requirements

### Performance

- Index on `camp_edition_tshirt_sizes(camp_edition_id, age_category, is_active)` for filtered queries
- Size list per edition is small (typically <20 items) — no pagination needed

### Security

- All endpoints require JWT authentication
- Write endpoints restricted to Admin/Board roles
- Users can only set sizes on their own registrations (or Admin/Board)

---

## Testing Requirements

### Backend Unit Tests

- Service: Create/bulk-create/update/delete size happy paths
- Service: Delete with existing registrations throws error
- Service: Duplicate label in same edition+category rejected
- Validator: All validation rules
- Registration service: TShirtSizeId validated against edition and age category

### Backend Integration Tests

- Full CRUD cycle via HTTP endpoints
- Authorization enforcement
- Size selection as part of member update
- Delete protection when referenced

### Frontend

- T-shirt dropdown visibility (conditional on sizes existing)
- Dropdown filtering by age category
- Admin CRUD dialog interactions
- Confirmation step displays selected sizes

---

## Implementation Order

### Phase 1: Backend — Entity & CRUD (~3 files new, ~3 files modified)

1. Add entity + DTOs to `CampsModels.cs`
2. Create EF configuration (`CampEditionTShirtSizeConfiguration.cs`)
3. Update `AbuviDbContext.cs` with new DbSet
4. Generate and apply migration
5. Create repository + service
6. Add validators to `CampsValidators.cs`
7. Add endpoints to `CampsEndpoints.cs`
8. Register DI in `Program.cs`

### Phase 2: Backend — Registration Integration (~3 files modified)

1. Add `TShirtSizeId` to `RegistrationMember` entity
2. Update EF configuration for the FK
3. Add migration for the new column
4. Extend member update logic to validate and persist size
5. Include size in registration response DTOs

### Phase 3: Frontend — Admin CRUD (~2 files new, ~1 file modified)

1. Add types to `camp-edition.ts`
2. Create `useCampTShirtSizes.ts` composable
3. Create `CampEditionTShirtSizeDialog.vue`
4. Integrate into camp edition admin page

### Phase 4: Frontend — Registration Wizard (~2 files modified)

1. Extend types in `registration.ts`
2. Add size dropdown to `RegistrationMemberSelector.vue`
3. Update confirmation step in `RegisterForCampPage.vue`

---

## Document Control

- **Created**: 2026-03-01
- **Status**: ENRICHED — Ready for implementation planning with `/plan-backend-ticket` or `/plan-frontend-ticket`
- **Origin**: User request for t-shirt size collection during registration
