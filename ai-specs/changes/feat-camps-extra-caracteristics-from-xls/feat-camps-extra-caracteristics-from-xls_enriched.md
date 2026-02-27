# Add Extra Camp Characteristics from CAMPAMENTOS.csv (ENRICHED)

## Executive Summary

Add 34 extra fields from the internal `CAMPAMENTOS.csv` spreadsheet into the `Camp` entity. This extends the data model and introduces two new audit/history mechanisms: a **`CampObservation`** table (structured notes with authorship) and a **`CampAuditLog`** table (automatic field-level change tracking for sensitive fields).

> **Scope note:** This feature adds the new columns and API endpoints so that the information currently tracked in the CSV can be entered and managed through the application. It does **not** include a CSV file importer — data will be entered manually via the API/UI.

**Target Entities:** `Camp`, `AccommodationCapacity` (JSON), `CampObservation` (NEW), `CampAuditLog` (NEW)
**Primary Use Case:** Enrich Camp records with contact info, facilities, capacity, and internal tracking; provide full traceability of who changed what over time.
**Key Dependencies:** Backend only (no frontend changes in this phase)

---

## Table of Contents

1. [Objective](#objective)
2. [Context and CSV Column Reference](#context-and-csv-column-reference)
3. [Field Coverage Analysis](#field-coverage-analysis)
4. [Data Model Changes](#data-model-changes)
5. [Audit and Observations Design](#audit-and-observations-design)
6. [Backend Implementation](#backend-implementation)
7. [Testing Specifications (TDD)](#testing-specifications-tdd)
8. [Security and Authorization](#security-and-authorization)
9. [Migration and Deployment](#migration-and-deployment)
10. [Acceptance Criteria](#acceptance-criteria)

---

## Objective

1. Enrich `Camp` records with characteristics currently tracked in `CAMPAMENTOS.csv`: contact info, pricing, facilities, capacity, and internal ABUVI tracking.
2. Introduce a proper **`CampObservation`** entity — each observation is append-only and records its author.
3. Automatically track **who changed what** for a defined set of sensitive fields via a **`CampAuditLog`** entity.

---

## Context and CSV Column Reference

The `CAMPAMENTOS.csv` file is the reference document that defines which new fields need to be added. The CSV itself will **not** be imported programmatically — it serves only as the source of truth for the data model design.

### CSV Structure (for reference)

- **Encoding:** Windows-1252 (ISO-8859-1)
- **Separator:** Semicolons (`;`)
- **Header row:** Row 1 (34 columns)

### Column Mapping — New Fields to Add

| # | CSV Column | Mapping Strategy | New/Existing |
|---|-----------|-----------------|-------------|
| 1 | `N°` | → `Camp.ExternalSourceId` | NEW |
| 2 | `Gestión por` | → `Camp.AbuviManagedByUserId` (FK to User with Board role, set manually via API) | NEW |
| 3 | `Contactado` | → `Camp.AbuviContactedAt` | NEW |
| 4 | `Posibilidad` | → `Camp.AbuviPossibility` | NEW |
| 5 | `Visitado ABUVI` | → `Camp.AbuviLastVisited` | NEW |
| 6 | `NOMBRE` | → `Camp.Name` | EXISTING |
| 7 | `Comunidad Autónoma` | → `Camp.AdministrativeArea` | EXISTING |
| 8 | `Provincia` | → `Camp.Province` | NEW |
| 9 | `Localidad / Municipio / Pueblo` | → `Camp.Locality` | EXISTING |
| 10 | `Nombre` (contact) | → `Camp.ContactPerson` | NEW |
| 11 | `Empresa` | → `Camp.ContactCompany` | NEW |
| 12 | `Teléfono 1` | → `Camp.PhoneNumber` | EXISTING |
| 13 | `Teléfono 2` | → `Camp.NationalPhoneNumber` | EXISTING |
| 14 | `WEB` | → `Camp.WebsiteUrl` | EXISTING |
| 15 | `WEB2` | → `Camp.SecondaryWebsiteUrl` | NEW |
| 16 | `EMAIL` | → `Camp.ContactEmail` | NEW |
| 17 | `Observaciones 2025 2026` | → `CampObservation` (season 2025/2026) | NEW ENTITY |
| 18 | `PRECIO` | → `Camp.BasePrice` | NEW |
| 19 | `IVA` | → `Camp.VatIncluded` | NEW |
| 20 | `nº estancias` | → `AccommodationCapacity.TotalCapacity` | NEW |
| 21 | `nº habitac` | → `AccommodationCapacity.RoomsDescription` | NEW |
| 22 | `nº cabañas` | → `AccommodationCapacity.BungalowsDescription` | NEW |
| 23 | `nº tiendas` | → `AccommodationCapacity.TentsDescription` | NEW |
| 24 | `campa para tiendas` | → `AccommodationCapacity.TentAreaDescription` | NEW |
| 25 | `nº aparcamientos` | → `AccommodationCapacity.ParkingSpots` | NEW |
| 26 | `menú adaptado` | → `AccommodationCapacity.HasAdaptedMenu` | NEW |
| 27 | `Comedor cerrado` | → `AccommodationCapacity.HasEnclosedDiningRoom` | NEW |
| 28 | `Piscina` | → `AccommodationCapacity.HasSwimmingPool` | NEW |
| 29 | `Pista polideportiva` | → `AccommodationCapacity.HasSportsCourt` | NEW |
| 30 | `Pinar o similar en campa` | → `AccommodationCapacity.HasForestArea` | NEW |
| 31 | `Observaciones campa 24` | → `CampObservation` (season 2024) | NEW ENTITY |
| 32 | `Observaciones 23` | → `CampObservation` (season 2023) | NEW ENTITY |
| 33 | `Observaciones 25` | → `CampObservation` (season 2025) | NEW ENTITY |
| 34 | `Datos erroneos` | → `Camp.AbuviHasDataErrors` | NEW |

---

## Field Coverage Analysis

This section maps every **existing** `Camp` and `AccommodationCapacity` field against the CSV to identify gaps, overlaps, and potential redundancies.

### Camp — existing fields vs CSV

| Existing Field | CSV equivalent | Status | Notes |
|----------------|---------------|--------|-------|
| `Id` | — | ✅ Internal | PK, not in CSV |
| `Name` | `NOMBRE` | ✅ Mapped | |
| `Description` | — | ⬜ No CSV data | Free-text description — not in spreadsheet |
| `Location` | — | ⚠️ Overlap | Free-text address; partially superseded by `Locality` + `Province` + `AdministrativeArea`. Consider whether it's still needed once Google Places address fields are populated. |
| `Latitude` | — | ⬜ No CSV data | From Google Places only |
| `Longitude` | — | ⬜ No CSV data | From Google Places only |
| `GooglePlaceId` | — | ⬜ No CSV data | Google Places identifier |
| `FormattedAddress` | — | ⬜ No CSV data | Full formatted address from Google Places |
| `StreetAddress` | — | ⬜ No CSV data | From Google Places |
| `Locality` | `Localidad / Municipio / Pueblo` | ✅ Mapped | |
| `AdministrativeArea` | `Comunidad Autónoma` | ✅ Mapped | |
| `PostalCode` | — | ⬜ No CSV data | From Google Places only |
| `Country` | — | ⬜ No CSV data | From Google Places (always Spain for this dataset) |
| `PhoneNumber` | `Teléfono 1` | ✅ Mapped | |
| `NationalPhoneNumber` | `Teléfono 2` | ✅ Mapped | |
| `WebsiteUrl` | `WEB` | ✅ Mapped | |
| `GoogleMapsUrl` | — | ⬜ No CSV data | From Google Places only |
| `GoogleRating` | — | ⬜ No CSV data | From Google Places only |
| `GoogleRatingCount` | — | ⬜ No CSV data | From Google Places only |
| `LastGoogleSyncAt` | — | ⬜ No CSV data | Internal sync timestamp |
| `BusinessStatus` | — | ⬜ No CSV data | From Google Places only |
| `PlaceTypes` | — | ⬜ No CSV data | From Google Places only |
| `PricePerAdult` | — | ⚠️ Overlap | Age-based pricing — not in CSV. The CSV has a single `PRECIO` → new `BasePrice`. **Two pricing models coexist**: age-based (PricePerAdult/Child/Baby) used for registrations, and `BasePrice` as a reference/catalogue price. |
| `PricePerChild` | — | ⚠️ Overlap | See PricePerAdult |
| `PricePerBaby` | — | ⚠️ Overlap | See PricePerAdult |
| `IsActive` | — | ⬜ No CSV data | |
| `CreatedAt` | — | ✅ Internal | Timestamp |
| `UpdatedAt` | — | ✅ Internal | Timestamp |
| `AccommodationCapacityJson` | Partial | ✅ Extended | New fields added to this JSON |

### AccommodationCapacity — existing fields vs CSV

| Existing Field | CSV equivalent | Status | Notes |
|----------------|---------------|--------|-------|
| `PrivateRoomsWithBathroom` | — | ⬜ No CSV data | Structured room data not in spreadsheet |
| `PrivateRoomsSharedBathroom` | — | ⬜ No CSV data | |
| `SharedRooms` (list of `SharedRoomInfo`) | `nº habitac` | ⚠️ Overlap | The CSV column is raw text (e.g., "12 + 2 grandes"). We add `RoomsDescription` (string). The structured `SharedRooms` list is populated via Google Places / manual entry only. |
| `Bungalows` (int) | `nº cabañas` | ⚠️ Overlap | CSV value is raw text ("6 (de 8 personas)"). We add `BungalowsDescription` (string). The existing `Bungalows` int field is for structured data. |
| `CampOwnedTents` (int) | `nº tiendas` | ⚠️ Overlap | Same situation as `Bungalows`. CSV text goes to `TentsDescription`; existing int field is for structured data. |
| `MemberTentAreaSquareMeters` | — | ⬜ No CSV data | Precise area in m² — not in spreadsheet |
| `MemberTentCapacityEstimate` | — | ⬜ No CSV data | |
| `MotorhomeSpots` | — | ⬜ No CSV data | Not in spreadsheet |
| `Notes` (in AccommodationCapacity) | — | ⬜ No CSV data | Free-text notes on capacity, not in CSV |

### Summary of Potential Issues

| Category | Fields | Decision needed |
|----------|--------|-----------------|
| **Pure Google Places fields** | `GooglePlaceId`, `FormattedAddress`, `StreetAddress`, `PostalCode`, `Country`, `GoogleMapsUrl`, `GoogleRating`, `GoogleRatingCount`, `LastGoogleSyncAt`, `BusinessStatus`, `PlaceTypes` | These have no CSV counterpart and are only populated via Google Places sync. **No action needed** — they serve a different purpose. |
| **`Location` (free-text address)** | `Location` | With `Locality`, `Province`, `AdministrativeArea`, and `FormattedAddress` all available, `Location` may be a legacy catch-all. Evaluate if it can be deprecated in favour of the structured fields. |
| **Dual pricing models** | `PricePerAdult/Child/Baby` vs new `BasePrice` | `PricePerAdult/Child/Baby` drives registration invoicing (age-based). `BasePrice` is the camp's catalogue price (from the spreadsheet, before ABUVI's internal age-based split). They coexist intentionally — but the UI should clearly distinguish them. |
| **Structured vs text duplicates** | `Bungalows`/`CampOwnedTents` (int) vs `BungalowsDescription`/`TentsDescription` (string); `SharedRooms` list vs `RoomsDescription` | The structured int/list fields are populated from Google Places or manual structured input. The description strings come from the CSV raw text. Both can coexist, but consider displaying only one in the UI to avoid confusion. |

---

## Data Model Changes

### 1. `Camp` Entity — New Fields

Add to `src/Abuvi.API/Features/Camps/CampsModels.cs`:

```csharp
// Province (e.g., "CORDOBA", "HUELVA")
public string? Province { get; set; }

// Contact info
public string? ContactEmail { get; set; }
public string? ContactPerson { get; set; }     // "Nombre" column
public string? ContactCompany { get; set; }    // "Empresa" column
public string? SecondaryWebsiteUrl { get; set; }

// Pricing
public decimal? BasePrice { get; set; }        // PRECIO column
public bool? VatIncluded { get; set; }         // IVA: true = "Si", false = "No", null = unknown

// ABUVI internal tracking
public int? ExternalSourceId { get; set; }     // N° — original CSV row number
public Guid? AbuviManagedByUserId { get; set; } // FK → ApplicationUser with Board role; the person responsible for contacting and updating this camp
public string? AbuviContactedAt { get; set; }  // "Contactado" (raw text — date serial or flag)
public string? AbuviPossibility { get; set; }  // "Posibilidad" (si/no/$/@ etc.)
public string? AbuviLastVisited { get; set; }  // "Visitado ABUVI" (e.g., "si 2023", "si 2018?")
public bool? AbuviHasDataErrors { get; set; }  // "Datos erroneos"

// Audit: who last modified this camp
public Guid? LastModifiedByUserId { get; set; }

// Navigation properties (new)
public ApplicationUser? AbuviManagedByUser { get; set; }  // the assigned Board member
public ICollection<CampObservation> Observations { get; set; } = new List<CampObservation>();
public ICollection<CampAuditLog> AuditLogs { get; set; } = new List<CampAuditLog>();
```

### 2. `CampObservation` Entity (NEW)

Each observation is **append-only** — it cannot be edited or deleted to preserve the historical record.

```csharp
/// <summary>
/// Append-only observation/note about a camp, created by a user.
/// </summary>
public class CampObservation
{
    public Guid Id { get; set; }
    public Guid CampId { get; set; }

    public string Text { get; set; } = string.Empty;  // The observation text
    public string? Season { get; set; }                // Optional tag: "2023", "2024", "2025", "2025/2026"

    public Guid? CreatedByUserId { get; set; }         // Null = system-created
    public DateTime CreatedAt { get; set; }

    // Navigation
    public Camp Camp { get; set; } = null!;
}
```

**DB table:** `camp_observations`

**Business rules:**

- No `UPDATE` or `DELETE` allowed on this table (enforced at service level + no update endpoint)
- A camp can have unlimited observations

### 3. `CampAuditLog` Entity (NEW)

Automatically written by `CampsService` when a `PUT /api/camps/{id}` call changes any of the **tracked fields**.

```csharp
/// <summary>
/// Automatic audit record for sensitive Camp field changes.
/// Written by CampsService on every update that modifies a tracked field.
/// </summary>
public class CampAuditLog
{
    public Guid Id { get; set; }
    public Guid CampId { get; set; }

    public string FieldName { get; set; } = string.Empty;  // e.g., "BasePrice"
    public string? OldValue { get; set; }                  // Serialized previous value
    public string? NewValue { get; set; }                  // Serialized new value

    public Guid ChangedByUserId { get; set; }
    public DateTime ChangedAt { get; set; }

    // Navigation
    public Camp Camp { get; set; } = null!;
}
```

**DB table:** `camp_audit_logs`

**Tracked fields** (only these generate audit records):

| Field | Rationale |
|-------|----------|
| `BasePrice` | Price negotiation history |
| `VatIncluded` | Pricing structure change |
| `AbuviPossibility` | Feasibility decision |
| `AbuviLastVisited` | Visit record |
| `AbuviContactedAt` | Contact history |
| `AbuviManagedByUserId` | Responsibility assignment change (Board member) |
| `IsActive` | Activation/deactivation |
| `ContactPerson` | Key contact changed |
| `ContactEmail` | Key contact changed |

### 4. `AccommodationCapacity` Class — New Fields

Extend in `CampsModels.cs`:

```csharp
// Capacity descriptions
public int? TotalCapacity { get; set; }             // "nº estancias"
public string? RoomsDescription { get; set; }       // "nº habitac" (raw text)
public string? BungalowsDescription { get; set; }   // "nº cabañas" (raw text)
public string? TentsDescription { get; set; }       // "nº tiendas" (raw text)
public string? TentAreaDescription { get; set; }    // "campa para tiendas"
public int? ParkingSpots { get; set; }              // "nº aparcamientos"

// Facility flags
public bool? HasAdaptedMenu { get; set; }           // "menú adaptado"
public bool? HasEnclosedDiningRoom { get; set; }    // "Comedor cerrado"
public bool? HasSwimmingPool { get; set; }          // "Piscina"
public bool? HasSportsCourt { get; set; }           // "Pista polideportiva"
public bool? HasForestArea { get; set; }            // "Pinar o similar en campa"
```

These serialize into the existing `accommodation_capacity_json` column — no new DB column needed.

### 5. EF Core Configuration

#### `CampConfiguration.cs` — New columns

```csharp
builder.Property(c => c.Province).HasMaxLength(100).HasColumnName("province");
builder.Property(c => c.ContactEmail).HasMaxLength(200).HasColumnName("contact_email");
builder.Property(c => c.ContactPerson).HasMaxLength(200).HasColumnName("contact_person");
builder.Property(c => c.ContactCompany).HasMaxLength(200).HasColumnName("contact_company");
builder.Property(c => c.SecondaryWebsiteUrl).HasMaxLength(500).HasColumnName("secondary_website_url");
builder.Property(c => c.BasePrice).HasPrecision(10, 2).HasColumnName("base_price");
builder.Property(c => c.VatIncluded).HasColumnName("vat_included");
builder.Property(c => c.ExternalSourceId).HasColumnName("external_source_id");
builder.HasIndex(c => c.ExternalSourceId).HasDatabaseName("ix_camps_external_source_id");
builder.Property(c => c.AbuviManagedByUserId).HasColumnName("abuvi_managed_by_user_id");
builder.HasOne(c => c.AbuviManagedByUser)
    .WithMany()
    .HasForeignKey(c => c.AbuviManagedByUserId)
    .OnDelete(DeleteBehavior.SetNull);
builder.Property(c => c.AbuviContactedAt).HasMaxLength(100).HasColumnName("abuvi_contacted_at");
builder.Property(c => c.AbuviPossibility).HasMaxLength(100).HasColumnName("abuvi_possibility");
builder.Property(c => c.AbuviLastVisited).HasMaxLength(200).HasColumnName("abuvi_last_visited");
builder.Property(c => c.AbuviHasDataErrors).HasColumnName("abuvi_has_data_errors");
builder.Property(c => c.LastModifiedByUserId).HasColumnName("last_modified_by_user_id");

// Relationships
builder.HasMany(c => c.Observations)
    .WithOne(o => o.Camp)
    .HasForeignKey(o => o.CampId)
    .OnDelete(DeleteBehavior.Cascade);

builder.HasMany(c => c.AuditLogs)
    .WithOne(a => a.Camp)
    .HasForeignKey(a => a.CampId)
    .OnDelete(DeleteBehavior.Cascade);
```

#### New file: `CampObservationConfiguration.cs`

```csharp
public class CampObservationConfiguration : IEntityTypeConfiguration<CampObservation>
{
    public void Configure(EntityTypeBuilder<CampObservation> builder)
    {
        builder.ToTable("camp_observations");
        builder.HasKey(o => o.Id);
        builder.Property(o => o.Id).HasColumnName("id");
        builder.Property(o => o.CampId).HasColumnName("camp_id").IsRequired();
        builder.Property(o => o.Text).HasMaxLength(4000).HasColumnName("text").IsRequired();
        builder.Property(o => o.Season).HasMaxLength(20).HasColumnName("season");
        builder.Property(o => o.CreatedByUserId).HasColumnName("created_by_user_id");
        builder.Property(o => o.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("NOW()");

        builder.HasIndex(o => o.CampId).HasDatabaseName("ix_camp_observations_camp_id");
    }
}
```

#### New file: `CampAuditLogConfiguration.cs`

```csharp
public class CampAuditLogConfiguration : IEntityTypeConfiguration<CampAuditLog>
{
    public void Configure(EntityTypeBuilder<CampAuditLog> builder)
    {
        builder.ToTable("camp_audit_logs");
        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id).HasColumnName("id");
        builder.Property(a => a.CampId).HasColumnName("camp_id").IsRequired();
        builder.Property(a => a.FieldName).HasMaxLength(100).HasColumnName("field_name").IsRequired();
        builder.Property(a => a.OldValue).HasMaxLength(2000).HasColumnName("old_value");
        builder.Property(a => a.NewValue).HasMaxLength(2000).HasColumnName("new_value");
        builder.Property(a => a.ChangedByUserId).HasColumnName("changed_by_user_id").IsRequired();
        builder.Property(a => a.ChangedAt).HasColumnName("changed_at").HasDefaultValueSql("NOW()");

        builder.HasIndex(a => a.CampId).HasDatabaseName("ix_camp_audit_logs_camp_id");
        builder.HasIndex(a => new { a.CampId, a.ChangedAt }).HasDatabaseName("ix_camp_audit_logs_camp_id_changed_at");
    }
}
```

#### `AbuviDbContext.cs` — Add new DbSets

```csharp
public DbSet<CampObservation> CampObservations => Set<CampObservation>();
public DbSet<CampAuditLog> CampAuditLogs => Set<CampAuditLog>();
```

### 6. EF Core Migration

```bash
dotnet ef migrations add AddCampExternalSourceFieldsAndAudit --project src/Abuvi.API
```

---

## Audit and Observations Design

### `CampObservation` — Usage Patterns

| Scenario | `CreatedByUserId` | `Season` | `Text` |
|----------|------------------|---------|--------|
| Manual note by user | `<userId>` | optional | user-typed text |

**API endpoints for observations:**

```
POST  /api/camps/{campId}/observations   → Add a new observation (Board+)
GET   /api/camps/{campId}/observations   → List all observations, ordered by CreatedAt DESC (Board+)
```

No `PUT` or `DELETE` — observations are immutable.

### `CampAuditLog` — Service Integration

`CampsService.UpdateAsync` must:

1. Accept `Guid updatedByUserId` as parameter (passed from endpoint via `ClaimsPrincipal`)
2. Load the existing `Camp` from the repository **before** applying changes
3. Compare old vs new values for each tracked field
4. For each field that changed, insert a `CampAuditLog` record
5. Set `Camp.LastModifiedByUserId = updatedByUserId` before saving

```csharp
// Pseudocode in CampsService.UpdateAsync
public async Task<CampDetailResponse?> UpdateAsync(
    Guid id, UpdateCampRequest request, Guid updatedByUserId, CancellationToken ct)
{
    var existing = await repository.GetByIdAsync(id, ct);
    if (existing is null) return null;

    var auditEntries = BuildAuditEntries(existing, request, updatedByUserId);

    // Apply changes to existing...
    existing.LastModifiedByUserId = updatedByUserId;
    existing.UpdatedAt = DateTime.UtcNow;

    await repository.UpdateAsync(existing, ct);

    if (auditEntries.Count > 0)
        await repository.AddAuditLogsAsync(auditEntries, ct);

    return existing.ToDetailResponse();
}

private static List<CampAuditLog> BuildAuditEntries(
    Camp existing, UpdateCampRequest request, Guid userId)
{
    var entries = new List<CampAuditLog>();
    var now = DateTime.UtcNow;

    void Track(string field, string? oldVal, string? newVal)
    {
        if (oldVal != newVal)
            entries.Add(new CampAuditLog
            {
                Id = Guid.NewGuid(),
                CampId = existing.Id,
                FieldName = field,
                OldValue = oldVal,
                NewValue = newVal,
                ChangedByUserId = userId,
                ChangedAt = now
            });
    }

    Track("BasePrice", existing.BasePrice?.ToString(), request.BasePrice?.ToString());
    Track("VatIncluded", existing.VatIncluded?.ToString(), request.VatIncluded?.ToString());
    Track("AbuviPossibility", existing.AbuviPossibility, request.AbuviPossibility);
    Track("AbuviLastVisited", existing.AbuviLastVisited, request.AbuviLastVisited);
    Track("AbuviContactedAt", existing.AbuviContactedAt, request.AbuviContactedAt);
    Track("AbuviManagedByUserId", existing.AbuviManagedByUserId?.ToString(), request.AbuviManagedByUserId?.ToString());
    Track("IsActive", existing.IsActive.ToString(), request.IsActive.ToString());
    Track("ContactPerson", existing.ContactPerson, request.ContactPerson);
    Track("ContactEmail", existing.ContactEmail, request.ContactEmail);

    return entries;
}
```

**API endpoints for audit log:**

```
GET /api/camps/{campId}/audit-log   → List audit entries, ordered by ChangedAt DESC (Admin only)
```

---

## Backend Implementation

### Files to Create / Modify

#### New files

| File | Purpose |
|------|---------|
| `Features/Camps/CampObservationsService.cs` | Add/list observations |
| `Features/Camps/ICampObservationsService.cs` | Interface |
| `Features/Camps/ICampObservationsRepository.cs` | Interface |
| `Features/Camps/CampObservationsRepository.cs` | Implementation |
| `Data/Configurations/CampObservationConfiguration.cs` | EF config |
| `Data/Configurations/CampAuditLogConfiguration.cs` | EF config |

#### Modified files

| File | Change |
|------|--------|
| `CampsModels.cs` | Add new Camp fields + new entities + AccommodationCapacity fields + DTOs |
| `CampConfiguration.cs` | Add column mappings + relationships to new entities |
| `AbuviDbContext.cs` | Add `CampObservations` and `CampAuditLogs` DbSets |
| `ICampsRepository.cs` | Add `AddAuditLogsAsync` |
| `CampsRepository.cs` | Implement new repository methods |
| `CampsService.cs` | Accept `updatedByUserId` in `UpdateAsync`; write audit logs |
| `CampsEndpoints.cs` | Add observation endpoints, audit log endpoint; pass userId to UpdateAsync |
| `Program.cs` | Register new services |

### DTOs — additions to `CampsModels.cs`

```csharp
// Observation request/response
public record AddCampObservationRequest(string Text, string? Season);

public record CampObservationResponse(
    Guid Id,
    string Text,
    string? Season,
    Guid? CreatedByUserId,
    DateTime CreatedAt
);

// Audit log response
public record CampAuditLogResponse(
    Guid Id,
    string FieldName,
    string? OldValue,
    string? NewValue,
    Guid ChangedByUserId,
    DateTime ChangedAt
);
```

Update `UpdateCampRequest` to include the new writable fields:

```csharp
public record UpdateCampRequest(
    string Name,
    string? Description,
    string? Location,
    decimal? Latitude,
    decimal? Longitude,
    string? GooglePlaceId,
    decimal PricePerAdult,
    decimal PricePerChild,
    decimal PricePerBaby,
    bool IsActive,
    AccommodationCapacity? AccommodationCapacity = null,
    // New fields
    string? Province = null,
    string? ContactEmail = null,
    string? ContactPerson = null,
    string? ContactCompany = null,
    string? SecondaryWebsiteUrl = null,
    decimal? BasePrice = null,
    bool? VatIncluded = null,
    Guid? AbuviManagedByUserId = null,
    string? AbuviContactedAt = null,
    string? AbuviPossibility = null,
    string? AbuviLastVisited = null,
    bool? AbuviHasDataErrors = null
);
```

Update `CampDetailResponse` to include all new fields and the collections:

```csharp
public record CampDetailResponse(
    // ... all existing fields ...
    string? Province,
    string? ContactEmail,
    string? ContactPerson,
    string? ContactCompany,
    string? SecondaryWebsiteUrl,
    decimal? BasePrice,
    bool? VatIncluded,
    int? ExternalSourceId,
    Guid? AbuviManagedByUserId,
    string? AbuviManagedByUserName,       // Display name of the assigned Board member (null if unassigned)
    string? AbuviContactedAt,
    string? AbuviPossibility,
    string? AbuviLastVisited,
    bool? AbuviHasDataErrors,
    Guid? LastModifiedByUserId,
    IReadOnlyList<CampObservationResponse> Observations
    // Note: AuditLogs are fetched via a separate endpoint, not included here
);
```

### Validation — `AbuviManagedByUserId`

When `AbuviManagedByUserId` is provided in `UpdateCampRequest`, `CampsService` must verify that the referenced user exists **and has the `Board` role** (or higher). If the user does not exist or is not a Board member, return a `400 Bad Request` with a descriptive error message. This prevents assigning a non-Board user as the camp's responsible contact.

```csharp
// In CampsService.UpdateAsync, before applying changes:
if (request.AbuviManagedByUserId.HasValue)
{
    var isBoard = await usersRepository.IsUserInRoleAsync(
        request.AbuviManagedByUserId.Value, "Board", ct);
    if (!isBoard)
        throw new ValidationException("AbuviManagedByUserId must reference a user with Board role.");
}
```

Add a new method to `IUsersRepository`:

```csharp
Task<bool> IsUserInRoleAsync(Guid userId, string role, CancellationToken ct = default);
```

### Endpoints — Summary of New Additions

```
POST /api/camps/{campId}/observations       Board+ — add manual observation
GET  /api/camps/{campId}/observations       Board+ — list observations (desc)
GET  /api/camps/{campId}/audit-log          Admin only — list audit entries (desc)
```

The existing `PUT /api/camps/{id}` endpoint must be updated to extract the `userId` from `ClaimsPrincipal` and pass it to `CampsService.UpdateAsync`.

### `ICampsRepository` — New Methods

```csharp
Task AddAuditLogsAsync(IEnumerable<CampAuditLog> entries, CancellationToken ct = default);
```

---

## Testing Specifications (TDD)

All unit tests go in `tests/Abuvi.Tests/Unit/Features/Camps/`.

### `CampsServiceTests.cs` — Audit log tests

```
UpdateAsync_WhenBasePriceChanges_CreatesAuditLogEntry
UpdateAsync_WhenIsActiveChanges_CreatesAuditLogEntry
UpdateAsync_WhenNoTrackedFieldChanges_DoesNotCreateAuditLog
UpdateAsync_WhenMultipleTrackedFieldsChange_CreatesOneEntryPerField
UpdateAsync_AlwaysSetsLastModifiedByUserId
UpdateAsync_WhenCampNotFound_ReturnsNull
UpdateAsync_WhenAbuviManagedByUserIdChanges_CreatesAuditLogEntry
UpdateAsync_WhenAbuviManagedByUserIdIsValidBoardUser_Succeeds
UpdateAsync_WhenAbuviManagedByUserIdIsNotBoardUser_ThrowsValidationException
UpdateAsync_WhenAbuviManagedByUserIdDoesNotExist_ThrowsValidationException
UpdateAsync_WhenAbuviManagedByUserIdIsNull_ClearsAssignment
```

### `CampObservationsServiceTests.cs`

```
AddObservationAsync_WhenCampExists_CreatesAndReturnsObservation
AddObservationAsync_WhenCampDoesNotExist_ThrowsNotFoundException
GetObservationsAsync_ReturnsObservationsOrderedByCreatedAtDesc
```

---

## Security and Authorization

| Endpoint | Role | Notes |
|----------|------|-------|
| `POST /api/camps/{id}/observations` | Board+ | Text max 4000 chars |
| `GET /api/camps/{id}/observations` | Board+ | |
| `GET /api/camps/{id}/audit-log` | Admin | Sensitive: shows who changed what |

- No user-supplied data executed as SQL — EF Core parameterized queries throughout
- Audit log cannot be modified or deleted via API (no write endpoints)
- Observations cannot be edited or deleted via API (append-only)

---

## Migration and Deployment

### Step 1 — Data model (tests first)

1. Add properties to `Camp`, `AccommodationCapacity`, new entities to `CampsModels.cs`
2. Add EF configurations
3. Add `DbSet`s to `AbuviDbContext`
4. `dotnet ef migrations add AddCampExternalSourceFieldsAndAudit --project src/Abuvi.API`
5. `dotnet ef database update --project src/Abuvi.API`

### Step 2 — Observations service (TDD)

1. Write failing tests in `CampObservationsServiceTests.cs`
2. Implement `CampObservationsService` + repository
3. Add endpoints

### Step 3 — Audit log in CampsService (TDD)

1. Write failing audit tests in `CampsServiceTests.cs`
2. Update `CampsService.UpdateAsync` to accept `updatedByUserId` and write audit entries
3. Update `CampsEndpoints.UpdateCamp` to pass userId

---

## Acceptance Criteria

- [ ] 15 new `Camp` columns in DB (contact info + ABUVI tracking + `LastModifiedByUserId` + `AbuviManagedByUserId` FK)
- [ ] `camp_observations` table: append-only, `created_by_user_id` nullable
- [ ] `camp_audit_logs` table: auto-written on update for the 9 tracked fields
- [ ] `AccommodationCapacity` JSON holds 11 new fields
- [ ] `PUT /api/camps/{id}` writes audit entries only for changed tracked fields
- [ ] `PUT /api/camps/{id}` sets `LastModifiedByUserId` on every save
- [ ] `PUT /api/camps/{id}` rejects `AbuviManagedByUserId` values that do not reference a Board-role user (400)
- [ ] `CampDetailResponse` includes `AbuviManagedByUserId` and `AbuviManagedByUserName`
- [ ] `POST /api/camps/{campId}/observations` creates an immutable observation
- [ ] `GET /api/camps/{campId}/observations` returns observations ordered newest-first
- [ ] `GET /api/camps/{campId}/audit-log` returns field-level change history (Admin only)
- [ ] Unit test coverage ≥ 90% for `CampsService`, `CampObservationsService`
