# Encaje de Bolillos — Accommodation Assignment Interface

## Summary

A new admin tool that lets the Board organise which families go into which accommodations for a given camp edition. It replicates the functionality of the React prototype at `C:\repos\tests\encaje-de-bolillos-abuvi`, wired to real data (Registrations → Families, CampEditionAccommodations), and extended with zones, saved proposals (versioning), and occupancy reports.

The feature is split into three sequential tasks:

| # | Task | Branch suffix |
|---|------|---------------|
| 1 | Data model changes | `-backend-model` |
| 2 | API extensions | `-backend-api` |
| 3 | Frontend interface | `-frontend` |

---

## Context

### Prototype behaviour (reference)

The prototype (`App.jsx`, ~660 LOC) shows two views:

1. **Assign** — left sidebar lists unassigned families; right area shows accommodation cards. A family is assigned by click-to-select + click-to-place. Smart signals indicate preference matches, capacity warnings, special needs. Auto-assign runs a greedy "tightest fit" algorithm.
2. **Summary** — accommodations grouped by building; unassigned families highlighted.

Key prototype concepts to carry over:

- **Preference ranking** (1st/2nd/3rd choice) — already stored in `RegistrationAccommodationPreference`.
- **Counting mode** per accommodation type: by-family (Caravan, Tent) vs. by-person (Lodge/Albergue).
- **Special needs and pet flags** for visual signals.
- **Friendship group** (`CampatesPreference` field) for cohesion hints.

### Existing data model (relevant entities)

| Entity | Table | Key fields |
|--------|-------|-----------|
| `Registration` | `registrations` | `FamilyUnitId`, `SpecialNeeds`, `HasPet`, `CampatesPreference`, `Status` |
| `RegistrationMember` | `registration_members` | `RegistrationId`, `AgeCategory` |
| `RegistrationAccommodationPreference` | `registration_accommodation_preferences` | `RegistrationId`, `CampEditionAccommodationId`, `PreferenceOrder` |
| `CampEditionAccommodation` | `camp_edition_accommodations` | `CampEditionId`, `Name`, `AccommodationType`, `Capacity`, `IsActive` |
| `FamilyUnit` | `family_units` | `Name`, `RepresentativeUserId` |

---

## Task 1 — Data Model Changes

### 1.1  New entity: `AccommodationZone`

Groups accommodations of the same type within a camp edition (e.g., "Edificio Arcs" groups 14 lodge rooms).

```csharp
// Features/Camps/CampsModels.cs  (add alongside existing entities)
public class AccommodationZone
{
    public Guid Id { get; set; }
    public Guid CampEditionId { get; set; }
    public AccommodationType AccommodationType { get; set; }
    public string Name { get; set; } = string.Empty;            // max 100
    public int? MaxCapacity { get; set; }                       // optional override; null = sum of child accommodations
    public string? DistributionNotes { get; set; }              // max 500 — free text, e.g. layout or rules
    public int SortOrder { get; set; } = 0;
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    // Navigation
    public CampEdition CampEdition { get; set; } = null!;
    public ICollection<CampEditionAccommodation> Accommodations { get; set; } = [];
}
```

EF configuration (`Data/Configurations/AccommodationZoneConfiguration.cs`):

```csharp
builder.ToTable("accommodation_zones");
builder.HasKey(z => z.Id);
builder.Property(z => z.Id).HasDefaultValueSql("gen_random_uuid()");
builder.Property(z => z.Name).IsRequired().HasMaxLength(100);
builder.Property(z => z.AccommodationType).HasConversion<string>().HasMaxLength(20);
builder.Property(z => z.DistributionNotes).HasMaxLength(500);
builder.Property(z => z.CreatedAt).HasDefaultValueSql("now() at time zone 'utc'");
builder.Property(z => z.UpdatedAt).HasDefaultValueSql("now() at time zone 'utc'");
builder.HasOne(z => z.CampEdition)
    .WithMany()
    .HasForeignKey(z => z.CampEditionId)
    .OnDelete(DeleteBehavior.Cascade);
builder.HasCheckConstraint("ck_accommodation_zones_sort_order", "sort_order >= 0");
```

Add `DbSet<AccommodationZone> AccommodationZones` to `AbuviDbContext`.

### 1.2  Extend `CampEditionAccommodation`

Add nullable FK to zone (accommodations without a zone are shown as "Sin zona"):

```csharp
public Guid? ZoneId { get; set; }           // nullable FK
public AccommodationZone? Zone { get; set; } // navigation
```

EF: `builder.HasOne(a => a.Zone).WithMany(z => z.Accommodations).HasForeignKey(a => a.ZoneId).OnDelete(DeleteBehavior.SetNull);`

### 1.3  New entity: `AccommodationAssignmentProposal`

A named, versioned plan. Multiple proposals can exist per edition; exactly one can be **active** at a time.

```csharp
public class AccommodationAssignmentProposal
{
    public Guid Id { get; set; }
    public Guid CampEditionId { get; set; }
    public string Name { get; set; } = string.Empty;     // max 100, e.g. "Propuesta A"
    public string? Notes { get; set; }                   // max 500
    public bool IsActive { get; set; } = false;
    public Guid CreatedByUserId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    // Navigation
    public CampEdition CampEdition { get; set; } = null!;
    public ICollection<AccommodationAssignment> Assignments { get; set; } = [];
}
```

EF configuration (`Data/Configurations/AccommodationAssignmentProposalConfiguration.cs`):

```csharp
builder.ToTable("accommodation_assignment_proposals");
builder.HasKey(p => p.Id);
builder.Property(p => p.Id).HasDefaultValueSql("gen_random_uuid()");
builder.Property(p => p.Name).IsRequired().HasMaxLength(100);
builder.Property(p => p.Notes).HasMaxLength(500);
builder.Property(p => p.CreatedAt).HasDefaultValueSql("now() at time zone 'utc'");
builder.Property(p => p.UpdatedAt).HasDefaultValueSql("now() at time zone 'utc'");
builder.HasOne(p => p.CampEdition)
    .WithMany()
    .HasForeignKey(p => p.CampEditionId)
    .OnDelete(DeleteBehavior.Cascade);
```

Add `DbSet<AccommodationAssignmentProposal> AccommodationAssignmentProposals` to context.

### 1.4  New entity: `AccommodationAssignment`

One row = one registration assigned to one accommodation, within one proposal.

```csharp
public class AccommodationAssignment
{
    public Guid Id { get; set; }
    public Guid ProposalId { get; set; }
    public Guid RegistrationId { get; set; }
    public Guid AccommodationId { get; set; }
    public Guid AssignedByUserId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    // Navigation
    public AccommodationAssignmentProposal Proposal { get; set; } = null!;
    public Registration Registration { get; set; } = null!;
    public CampEditionAccommodation Accommodation { get; set; } = null!;
}
```

EF configuration (`Data/Configurations/AccommodationAssignmentConfiguration.cs`):

```csharp
builder.ToTable("accommodation_assignments");
builder.HasKey(a => a.Id);
builder.Property(a => a.Id).HasDefaultValueSql("gen_random_uuid()");
builder.Property(a => a.CreatedAt).HasDefaultValueSql("now() at time zone 'utc'");
builder.Property(a => a.UpdatedAt).HasDefaultValueSql("now() at time zone 'utc'");
builder.HasOne(a => a.Proposal)
    .WithMany(p => p.Assignments)
    .HasForeignKey(a => a.ProposalId)
    .OnDelete(DeleteBehavior.Cascade);
builder.HasOne(a => a.Registration)
    .WithMany()
    .HasForeignKey(a => a.RegistrationId)
    .OnDelete(DeleteBehavior.Restrict);
builder.HasOne(a => a.Accommodation)
    .WithMany()
    .HasForeignKey(a => a.AccommodationId)
    .OnDelete(DeleteBehavior.Restrict);
// One registration per accommodation per proposal
builder.HasIndex(a => new { a.ProposalId, a.RegistrationId }).IsUnique();
```

Add `DbSet<AccommodationAssignment> AccommodationAssignments` to context.

### 1.5  Migration

```bash
dotnet ef migrations add AddAccommodationZonesAndAssignmentProposals --project src/Abuvi.API
```

Migration name must be descriptive and include the two main changes.

### Acceptance criteria (Task 1)

- [ ] All 4 entities compile with EF configurations
- [ ] Migration creates tables `accommodation_zones`, `accommodation_assignment_proposals`, `accommodation_assignments`, and adds `zone_id` FK column to `camp_edition_accommodations`
- [ ] `dotnet ef database update` runs clean on a fresh database
- [ ] Unit tests for unique index (one registration per proposal) and cascade delete

---

## Task 2 — Backend API Extensions

All new endpoints live inside `src/Abuvi.API/Features/Camps/`. Follow vertical slice: models/service/repository/endpoints in existing files or new feature-slice files if the module grows significantly.

### 2.1  Zone endpoints

Group: `/api/camp-editions/{campEditionId}/accommodation-zones`

| Method | Path | Auth | Purpose |
|--------|------|------|---------|
| GET | `/` | Board | List all zones for edition |
| POST | `/` | Board | Create zone |
| PUT | `/{zoneId}` | Board | Update zone name/capacity/notes |
| DELETE | `/{zoneId}` | Board | Delete zone (only if no active accommodations attached) |
| PATCH | `/{zoneId}/accommodations` | Board | Attach/detach accommodations to zone (body: `{ accommodationIds: Guid[] }`) |

**Request/Response DTOs:**

```csharp
public record CreateAccommodationZoneRequest(
    AccommodationType AccommodationType,
    string Name,
    int? MaxCapacity,
    string? DistributionNotes,
    int SortOrder = 0);

public record UpdateAccommodationZoneRequest(
    string Name,
    int? MaxCapacity,
    string? DistributionNotes,
    int SortOrder);

public record AccommodationZoneResponse(
    Guid Id,
    Guid CampEditionId,
    AccommodationType AccommodationType,
    string Name,
    int? MaxCapacity,
    string? DistributionNotes,
    int SortOrder,
    bool IsActive,
    IReadOnlyList<Guid> AccommodationIds);  // IDs of attached accommodations
```

**Business rules:**

- Zone `AccommodationType` must match all attached accommodations' types.
- Cannot delete a zone with attached accommodations that have active assignments in any proposal.
- `MaxCapacity` must be > 0 if provided.

**Validator (`CreateAccommodationZoneValidator`):**

```csharp
RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
RuleFor(x => x.MaxCapacity).GreaterThan(0).When(x => x.MaxCapacity.HasValue);
RuleFor(x => x.DistributionNotes).MaximumLength(500).When(x => x.DistributionNotes is not null);
RuleFor(x => x.SortOrder).GreaterThanOrEqualTo(0);
```

Error messages in Spanish: "El nombre de la zona es obligatorio", "La capacidad máxima debe ser mayor que cero".

### 2.2  Proposal endpoints

Group: `/api/camp-editions/{campEditionId}/assignment-proposals`

| Method | Path | Auth | Purpose |
|--------|------|------|---------|
| GET | `/` | Board | List all proposals for edition (summary, no assignments) |
| POST | `/` | Board | Create new proposal (optionally copy from existing) |
| PUT | `/{proposalId}` | Board | Rename / update notes |
| DELETE | `/{proposalId}` | Board | Delete proposal (cannot delete the only active one if it has assignments) |
| POST | `/{proposalId}/activate` | Board | Mark this proposal as the active one (deactivates all others for the edition) |

**Request/Response DTOs:**

```csharp
public record CreateProposalRequest(
    string Name,
    string? Notes,
    Guid? CopyFromProposalId = null);   // optional: clone all assignments from another proposal

public record UpdateProposalRequest(string Name, string? Notes);

public record ProposalSummaryResponse(
    Guid Id,
    Guid CampEditionId,
    string Name,
    string? Notes,
    bool IsActive,
    int AssignmentCount,
    int UnassignedCount,
    Guid CreatedByUserId,
    DateTime CreatedAt,
    DateTime UpdatedAt);
```

**Business rule — activation:** When `POST /{proposalId}/activate` is called, all other proposals for the same `CampEditionId` have `IsActive = false`. Only one can be active at a time. An admin cannot activate a proposal from a different edition.

### 2.3  Assignment endpoints

Group: `/api/camp-editions/{campEditionId}/assignment-proposals/{proposalId}/assignments`

| Method | Path | Auth | Purpose |
|--------|------|------|---------|
| GET | `/` | Board | Get full assignment state for proposal (families + accommodations) |
| PUT | `/` | Board | Replace all assignments in proposal (bulk idempotent write) |
| POST | `/{registrationId}` | Board | Assign a single registration to an accommodation |
| DELETE | `/{registrationId}` | Board | Unassign a registration |
| POST | `/auto-assign` | Board | Run server-side auto-assign algorithm |

**GET `/` response:**

```csharp
public record ProposalAssignmentStateResponse(
    Guid ProposalId,
    IReadOnlyList<AssignmentFamilyResponse> Families,         // all registrations for the edition
    IReadOnlyList<AssignmentAccommodationResponse> Accommodations,
    IReadOnlyList<AssignmentEntry> Assignments);              // current assignments

public record AssignmentFamilyResponse(
    Guid RegistrationId,
    Guid FamilyUnitId,
    string FamilyName,
    string RepresentativeName,
    int MemberCount,
    int AdultCount,
    int ChildCount,
    bool HasPet,
    string? SpecialNeeds,
    string? CampatesPreference,
    IReadOnlyList<AccommodationPreferenceItem> AccommodationPreferences);  // ordered 1→3

public record AccommodationPreferenceItem(Guid AccommodationId, int PreferenceOrder);

public record AssignmentAccommodationResponse(
    Guid Id,
    string Name,
    AccommodationType Type,
    int? Capacity,
    bool CountByFamily,            // true for Caravan/Tent, false for Lodge/Bungalow/Motorhome
    Guid? ZoneId,
    string? ZoneName,
    int SortOrder);

public record AssignmentEntry(Guid RegistrationId, Guid AccommodationId);
```

**PUT `/` bulk replace body:**

```csharp
public record BulkAssignRequest(IReadOnlyList<AssignmentEntry> Assignments);
```

Overwrites all assignments for the proposal atomically (within a DB transaction). Validates:

- Each `RegistrationId` belongs to the edition.
- Each `AccommodationId` belongs to the edition.
- Capacity is not exceeded after applying all assignments.
- A registration cannot be assigned to more than one accommodation.

**POST `/auto-assign` body:**

```csharp
public record AutoAssignRequest(bool OverwriteExisting = false);
```

Runs the greedy algorithm (see §2.4). Returns the updated `BulkAssignRequest`-equivalent (new assignment list) — **does not persist** unless `OverwriteExisting = true`.

### 2.4  Auto-assign algorithm (server-side)

Port of the prototype's `autoAssign` logic to C#:

```
1. Start from scratch (or from current assignments if OverwriteExisting=false, skip already assigned).
2. Sort unassigned registrations by MemberCount DESC (larger families first).
3. For each unassigned registration:
   a. Phase 1 — preferences: iterate PreferenceOrder 1→3.
      - Filter candidate accommodations where Type matches the preference AND remaining capacity ≥ family size (or remaining units ≥ 1 for by-family types).
      - Score: -(remaining capacity − family size) — tightest fit wins.
      - Assign to highest-scoring candidate. Continue to next family.
   b. Phase 2 — fallback: if Phase 1 found nothing,
      - Find all accommodations with available capacity regardless of type.
      - Sort by smallest remaining capacity.
      - Assign to first available.
   c. If still unassigned, skip (leave unassigned).
4. Return computed assignments.
```

Capacity calculation per accommodation:

- `CountByFamily` = true (Caravan, Tent): occupancy = count of already-assigned registrations, capacity = `Accommodation.Capacity` in units.
- `CountByFamily` = false (Lodge, Bungalow, Motorhome): occupancy = sum of `MemberCount` of assigned registrations, capacity = `Accommodation.Capacity` in persons.

### 2.5  Reports endpoints

Group: `/api/camp-editions/{campEditionId}/assignment-proposals/{proposalId}/reports`

| Method | Path | Auth | Purpose |
|--------|------|------|---------|
| GET | `/by-type` | Board | Occupancy summary and family list grouped by AccommodationType |
| GET | `/by-zone` | Board | Occupancy summary and family list grouped by zone |
| GET | `/unassigned` | Board | Families not yet assigned in this proposal |

**Response shape (shared base):**

```csharp
public record AssignmentReportGroupResponse(
    string GroupKey,          // type or zone name
    string GroupLabel,
    int TotalCapacity,        // sum of capacities in this group
    int UsedCapacity,         // occupied units/persons
    IReadOnlyList<AssignmentReportFamilyRow> Families);

public record AssignmentReportFamilyRow(
    Guid RegistrationId,
    string FamilyName,
    string RepresentativeName,
    int MemberCount,
    string? AccommodationName,    // null if unassigned
    string? ZoneName);
```

### 2.6  Updated `CampEditionAccommodation` responses

Extend `CampEditionAccommodationResponse` with:

```csharp
public Guid? ZoneId { get; init; }
public string? ZoneName { get; init; }
```

Update GET `/api/camp-editions/{id}/accommodations` to include zone info.

### Acceptance criteria (Task 2)

- [ ] All zone CRUD endpoints return correct responses and validate inputs
- [ ] Proposal activation is atomic (single transaction flips `IsActive`)
- [ ] `PUT /assignments` fails with 422 if capacity is exceeded or registration not found
- [ ] Auto-assign endpoint runs without persisting when `OverwriteExisting=false`
- [ ] Reports return correct grouped structure
- [ ] Unit tests for `AutoAssignService` covering: all assigned, partial preferences, fallback, capacity limits
- [ ] Integration tests for bulk assignment capacity validation

---

## Task 3 — Frontend Interface

### 3.1  Route and navigation

Add a new admin-only route:

```typescript
// router/index.ts
{
  path: '/admin/camp-editions/:campEditionId/assignment/:proposalId?',
  name: 'accommodation-assignment',
  component: () => import('@/views/admin/AccommodationAssignmentView.vue'),
  meta: { requiresAuth: true, requiresBoard: true, title: 'ABUVI | Distribución de Alojamientos' }
}
```

Add an entry point from the admin camp edition detail (button: "Gestionar distribución de alojamientos").

### 3.2  View structure

`/views/admin/AccommodationAssignmentView.vue` — page-level container:

- Toolbar with:
  - Camp edition name / year
  - Proposal selector (Dropdown of proposals with "Nueva propuesta" action)
  - Active proposal badge
  - "Guardar" button (manual save of bulk assignment state)
  - "Auto-asignar" button
  - Tab switcher: **Asignar** | **Resumen** | **Informes**
- Loads the full assignment state via `GET /assignments`
- Uses `useAccommodationAssignment` composable

### 3.3  Components

#### `AccommodationAssignmentPanel.vue` (tab: Asignar)

Two-panel layout (PrimeVue `Splitter` or CSS `grid`):

**Left panel — Family list**

- Search input (by family name / representative)
- Count badge: unassigned / total
- List of `FamilyAssignmentCard.vue` items (sorted alphabetically, unassigned first)
- Clicking a card selects the family; selected card highlighted

**`FamilyAssignmentCard.vue`**

```
[FAMILY NAME]  [N personas]
[Representative name]
[Pet icon if HasPet] [Special needs icon if SpecialNeeds not empty]
[Preferences: 1ª ★ / 2ª ★ / 3ª ★ showing accommodation type icons]
[Assigned: accommodation name — or "Sin asignar" in muted text]
```

**Right panel — Accommodation grid**

Grouped by `AccommodationType` and then by `ZoneName` (or "Sin zona"):

```
[ALBERGUE]
  [Zona: Edificio Arcs]  [12/30 pers.]
    [Card Hab.1] [Card Hab.2] ...
  [Zona: Edificio Ginestà]
    ...
[CARAVANA]
  ...
```

Each `AccommodationSlotCard.vue` shows:

- Accommodation name
- Capacity bar (PrimeVue `ProgressBar`)
- Assigned family chips (name + member count)
- Visual signals when a family is selected:
  - 🟢 Green border: 1st preference match
  - 🟡 Yellow border: 2nd/3rd preference match
  - 🔴 Red border: over capacity
  - 🔵 Blue border: available but no preference match

**Assignment interaction:**

1. Click family card → family becomes selected (`selectedRegistrationId`).
2. Click accommodation card → assign selected family there (call `POST /assignments/{registrationId}` body: `{ accommodationId }`).
3. Click the assigned family chip on an accommodation → unassign it (call `DELETE /assignments/{registrationId}`).

> Drag-and-drop is desirable but optional; implement only if PrimeVue `OrderList` or native HTML5 drag events can be wired cleanly. Do not introduce a new DnD library.

#### `AccommodationSummaryPanel.vue` (tab: Resumen)

Displays `GET /reports/by-zone` data in a PrimeVue `DataTable` with expandable rows per zone, showing family rows and remaining capacity.

Highlight rows where accommodation is over capacity (red badge).

Show a warning panel with unassigned families count and a list of family names.

#### `AccommodationReportsPanel.vue` (tab: Informes)

Two sub-tabs: "Por tipo de alojamiento" | "Por zona".

Each shows a `DataTable` with columns: Grupo | Capacidad total | Ocupación | % ocupación | Familias asignadas.

Expandable row reveals family list for that group.

Future: Export to CSV/Excel (out of scope for this ticket, add TODO comment).

### 3.4  Proposal management

`ProposalSelectorBar.vue` (inside toolbar):

- Shows current proposal name in a `Select` (Dropdown).
- "Nueva propuesta" option opens a `Dialog` with a name field (and optional "copiar de" selector).
- "Activar esta propuesta" button (only shown if current proposal is not active).
- "Renombrar" icon button opens inline edit.
- "Eliminar" icon button (disabled if proposal is active).

### 3.5  Composable: `useAccommodationAssignment`

`/composables/useAccommodationAssignment.ts`

```typescript
export function useAccommodationAssignment(campEditionId: Ref<string>) {
  const proposals = ref<ProposalSummaryResponse[]>([])
  const selectedProposalId = ref<string | null>(null)
  const assignmentState = ref<ProposalAssignmentStateResponse | null>(null)
  const selectedRegistrationId = ref<string | null>(null)
  const loading = ref(false)
  const saving = ref(false)
  const error = ref<string | null>(null)

  // local mutation: assignments map for O(1) lookup
  const assignmentsMap = computed(...)

  async function loadProposals(): Promise<void> { ... }
  async function loadAssignmentState(): Promise<void> { ... }
  async function assignFamily(registrationId: string, accommodationId: string): Promise<void> { ... }
  async function unassignFamily(registrationId: string): Promise<void> { ... }
  async function autoAssign(overwriteExisting: boolean): Promise<void> { ... }
  async function saveAll(): Promise<void> { /* PUT bulk */ ... }
  async function createProposal(name: string, copyFromId?: string): Promise<void> { ... }
  async function activateProposal(proposalId: string): Promise<void> { ... }
  async function deleteProposal(proposalId: string): Promise<void> { ... }

  return {
    proposals, selectedProposalId, assignmentState, selectedRegistrationId,
    loading, saving, error, assignmentsMap,
    loadProposals, loadAssignmentState, assignFamily, unassignFamily,
    autoAssign, saveAll, createProposal, activateProposal, deleteProposal,
  }
}
```

### 3.6  TypeScript types

`/types/accommodation-assignment.ts` — mirrors all backend DTOs:

```typescript
export interface AccommodationZoneResponse { ... }
export interface ProposalSummaryResponse { ... }
export interface ProposalAssignmentStateResponse { ... }
export interface AssignmentFamilyResponse { ... }
export interface AssignmentAccommodationResponse { ... }
export interface AssignmentEntry { ... }
export interface AssignmentReportGroupResponse { ... }
```

### 3.7  Zone management (admin)

Add a zone management section to the existing camp edition admin form (`CampEditionForm.vue` or equivalent). Board can:

- Add / rename / delete zones per accommodation type
- Set max capacity and distribution notes
- Drag-to-reorder zones (use PrimeVue `OrderList`)
- Attach / detach accommodations from a zone (multi-select)

This is secondary to the assignment interface; implement as a collapsible section in the edition detail page.

### Acceptance criteria (Task 3)

- [ ] `/admin/camp-editions/:id/assignment` loads without error for a valid edition with registrations
- [ ] Selecting a family highlights compatible accommodations with correct signal colours
- [ ] Assigning a family persists on the backend (reload confirms assignment)
- [ ] Unassigning works
- [ ] Auto-assign populates all families that can be placed
- [ ] Proposal creation, naming, and activation work
- [ ] Summary tab shows correct occupancy per zone and warns on unassigned families
- [ ] Reports tab shows grouped family lists

---

## Non-functional requirements

| Area | Requirement |
|------|-------------|
| Security | All new endpoints require `Board` or `Admin` role. Validate that the `CampEditionId` in the URL matches the proposal/zone being operated on. |
| Performance | `GET /assignments` must include all registration details in one query (use `.Include()` chains, no N+1). For editions > 200 families the endpoint should respond in < 500 ms. |
| Atomicity | Bulk assignment `PUT` and auto-assign persistence must execute in a single EF transaction. |
| Idempotency | `PUT /assignments` is idempotent: sending the same body twice produces the same result. |
| Validation messages | Spanish. E.g. "La zona no existe", "El alojamiento no tiene capacidad suficiente", "La inscripción no pertenece a esta edición". |
| Tests | ≥ 90% coverage for new service methods; unit tests for auto-assign algorithm edge cases (no preferences, over capacity, no accommodations available). |

---

## Files to create / modify

### Backend

| Action | File |
|--------|------|
| Create | `src/Abuvi.API/Features/Camps/AccommodationZonesService.cs` |
| Create | `src/Abuvi.API/Features/Camps/AccommodationZonesRepository.cs` |
| Create | `src/Abuvi.API/Features/Camps/AccommodationAssignmentProposalsService.cs` |
| Create | `src/Abuvi.API/Features/Camps/AccommodationAssignmentProposalsRepository.cs` |
| Create | `src/Abuvi.API/Features/Camps/AccommodationAssignmentsService.cs` |
| Create | `src/Abuvi.API/Features/Camps/AccommodationAssignmentsRepository.cs` |
| Create | `src/Abuvi.API/Features/Camps/AutoAssignService.cs` |
| Modify | `src/Abuvi.API/Features/Camps/CampsModels.cs` (add new entities + DTOs) |
| Modify | `src/Abuvi.API/Features/Camps/CampsEndpoints.cs` (add new route groups) |
| Create | `src/Abuvi.API/Data/Configurations/AccommodationZoneConfiguration.cs` |
| Create | `src/Abuvi.API/Data/Configurations/AccommodationAssignmentProposalConfiguration.cs` |
| Create | `src/Abuvi.API/Data/Configurations/AccommodationAssignmentConfiguration.cs` |
| Modify | `src/Abuvi.API/Data/AbuviDbContext.cs` (add 3 new DbSets) |
| Create | `src/Abuvi.API/Migrations/<timestamp>_AddAccommodationZonesAndAssignmentProposals.cs` |
| Create | `src/Abuvi.Tests/Unit/Features/Camps/AutoAssignServiceTests.cs` |
| Create | `src/Abuvi.Tests/Unit/Features/Camps/AccommodationAssignmentsServiceTests.cs` |

### Frontend

| Action | File |
|--------|------|
| Create | `frontend/src/views/admin/AccommodationAssignmentView.vue` |
| Create | `frontend/src/components/accommodation-assignment/AccommodationAssignmentPanel.vue` |
| Create | `frontend/src/components/accommodation-assignment/AccommodationSummaryPanel.vue` |
| Create | `frontend/src/components/accommodation-assignment/AccommodationReportsPanel.vue` |
| Create | `frontend/src/components/accommodation-assignment/FamilyAssignmentCard.vue` |
| Create | `frontend/src/components/accommodation-assignment/AccommodationSlotCard.vue` |
| Create | `frontend/src/components/accommodation-assignment/ProposalSelectorBar.vue` |
| Create | `frontend/src/composables/useAccommodationAssignment.ts` |
| Create | `frontend/src/types/accommodation-assignment.ts` |
| Modify | `frontend/src/router/index.ts` (add route) |
| Modify | Camp edition admin panel (add "Gestionar distribución" button) |
