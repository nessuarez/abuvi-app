# Add Description Field to CampEdition (ENRICHED)

## Executive Summary

Add an optional long-text `Description` field to the `CampEdition` entity so that board members can provide a rich, free-form description of each camp edition (activities overview, highlights, marketing copy, etc.). The field is **not required** and has no maximum length constraint beyond the PostgreSQL `text` type.

**Target Entity:** `CampEdition`
**Primary Use Case:** Allow board members to write a detailed description for each camp edition, visible in both management views and (eventually) public-facing pages.
**Scope:** Backend only — entity, DTOs, configuration, migration, service, and tests.

---

## Table of Contents

1. [Objective](#objective)
2. [Field Specification](#field-specification)
3. [Data Model Changes](#data-model-changes)
4. [Backend Implementation](#backend-implementation)
5. [Testing Specifications (TDD)](#testing-specifications-tdd)
6. [Security and Authorization](#security-and-authorization)
7. [Migration and Deployment](#migration-and-deployment)
8. [Acceptance Criteria](#acceptance-criteria)

---

## Objective

Add a single optional `Description` property (`string?`) to `CampEdition` so that each edition can carry a long-form textual description. The field:

- Is **nullable** (not required).
- Uses PostgreSQL `text` column type (unlimited length).
- Is editable in **all mutable statuses** (Proposed, Draft, Open) — same rule as `Notes`.
- Is exposed in propose, update, and response DTOs.

---

## Field Specification

| Attribute | Value |
|---|---|
| **Property name** | `Description` |
| **C# type** | `string?` |
| **DB column** | `description` |
| **DB type** | `text` (PostgreSQL — unlimited length) |
| **Nullable** | Yes |
| **Validation** | None (optional, no max length) |
| **Editable statuses** | Proposed, Draft, Open (same as `Notes`) |

> **Design note:** We intentionally use `text` (no `HasMaxLength`) instead of `character varying(N)` because Description is meant for long-form content (marketing copy, activity lists, HTML/markdown, etc.). This distinguishes it from `Notes` (internal, capped at 2000 chars).

---

## Data Model Changes

### 1. Entity — `CampsModels.cs`

Add `Description` property to the `CampEdition` class, next to the existing `Notes` field:

```csharp
// In CampEdition class, after Notes property (line ~207)
public string? Description { get; set; }
```

### 2. EF Configuration — `CampEditionConfiguration.cs`

Add column mapping after the `Notes` configuration block:

```csharp
// Description: optional, long text (no max length → PostgreSQL text)
builder.Property(e => e.Description)
    .HasColumnType("text")
    .HasColumnName("description");
```

### 3. DTOs — `CampsModels.cs`

**`ProposeCampEditionRequest`** — Add `string? Description = null` parameter (with default, to keep backward compatibility):

```csharp
public record ProposeCampEditionRequest(
    // ... existing params ...
    string? Notes,
    string? Description = null,   // NEW — optional long-form description
    // ... rest of params ...
);
```

**`UpdateCampEditionRequest`** — Add `string? Description = null` parameter:

```csharp
public record UpdateCampEditionRequest(
    // ... existing params ...
    string? Notes,
    string? Description = null,   // NEW — optional long-form description
    // ... rest of params ...
);
```

**`CampEditionResponse`** — Add `string? Description` to the response record:

```csharp
public record CampEditionResponse(
    // ... existing params ...
    string? Notes,
    string? Description,          // NEW
    // ... rest of params ...
);
```

**`ActiveCampEditionResponse`** — Add `string? Description`:

```csharp
public record ActiveCampEditionResponse(
    // ... existing params ...
    string? Notes,
    string? Description,          // NEW
    // ... rest of params ...
);
```

**`CurrentCampEditionResponse`** — Add `string? Description`:

```csharp
public record CurrentCampEditionResponse(
    // ... existing params ...
    string? Notes,
    string? Description,          // NEW
    // ... rest of params ...
);
```

---

## Backend Implementation

### Files to Modify

| # | File | Change |
|---|---|---|
| 1 | `src/Abuvi.API/Features/Camps/CampsModels.cs` | Add `Description` to entity + all 5 DTOs listed above |
| 2 | `src/Abuvi.API/Data/Configurations/CampEditionConfiguration.cs` | Add `Description` column mapping |
| 3 | `src/Abuvi.API/Features/Camps/CampEditionsService.cs` | Map `Description` in `ProposeAsync`, `UpdateAsync`, `MapToCampEditionResponse`, `GetCurrentAsync`, and `GetActiveEditionAsync` |
| 4 | New migration file (auto-generated) | `AddDescriptionToCampEdition` |

### Service Changes — `CampEditionsService.cs`

1. **`ProposeAsync`** (~line 75): Add `Description = request.Description` to the entity initializer.

2. **`UpdateAsync`** (~line 252): Add `edition.Description = request.Description;` alongside the existing `edition.Notes = request.Notes;` assignment. No additional status-based restriction needed — `Description` follows the same rule as `Notes` (editable in Open status).

3. **`MapToCampEditionResponse`** (~line 418): Add `Description: edition.Description` to the `CampEditionResponse` constructor call.

4. **`GetCurrentAsync`** (~line 314): Add `Description: edition.Description` to the `CurrentCampEditionResponse` constructor call.

5. **Any method returning `ActiveCampEditionResponse`**: Add `Description: edition.Description`.

### Migration

Generate via EF Core CLI:

```bash
dotnet ef migrations add AddDescriptionToCampEdition -p src/Abuvi.API
```

Expected migration:

```csharp
migrationBuilder.AddColumn<string>(
    name: "description",
    table: "camp_editions",
    type: "text",
    nullable: true);
```

---

## Testing Specifications (TDD)

### Unit Tests — `CampEditionsServiceTests.cs`

| # | Test | Expected |
|---|---|---|
| 1 | `ProposeAsync_WithDescription_SetsDescriptionOnEdition` | Propose with a description value → response.Description matches input |
| 2 | `ProposeAsync_WithoutDescription_DescriptionIsNull` | Propose without description → response.Description is null |
| 3 | `UpdateAsync_WithDescription_UpdatesDescription` | Update edition description → persisted and returned in response |
| 4 | `UpdateAsync_OpenEdition_CanUpdateDescription` | Open-status edition → description can be changed without error |
| 5 | `UpdateAsync_ClosedEdition_ThrowsException` | Closed-status edition → update is rejected (existing behavior, verify Description isn't special-cased) |

### Integration Tests — `CampEditionsEndpointsTests.cs`

| # | Test | Expected |
|---|---|---|
| 1 | `POST propose` with `description` in body | 201 Created, response includes `description` |
| 2 | `PUT` update with `description` | 200 OK, response reflects updated `description` |
| 3 | `GET` by ID | Response includes `description` field |

---

## Security and Authorization

No new authorization rules required. The `Description` field follows the same access pattern as `Notes`:

- **Read:** Any authenticated user (via `GET` endpoints).
- **Write:** Board+ role (via `POST propose` and `PUT update` endpoints, which already require Board authorization).

---

## Migration and Deployment

1. **Migration is additive only** — adds a nullable column with no default, so no data loss risk.
2. **Backward compatible** — all DTOs use `string? Description = null` defaults, so existing clients that don't send `description` will work without changes.
3. **No data backfill** needed — new column defaults to `NULL`.
4. **Deployment order:** Apply migration before deploying new API code (standard practice for additive changes).

---

## Acceptance Criteria

- [ ] `CampEdition` entity has a `Description` property (`string?`).
- [ ] PostgreSQL column `description` of type `text` exists in `camp_editions` table.
- [ ] `ProposeCampEditionRequest` accepts optional `Description`.
- [ ] `UpdateCampEditionRequest` accepts optional `Description`.
- [ ] `CampEditionResponse`, `ActiveCampEditionResponse`, and `CurrentCampEditionResponse` return `Description`.
- [ ] `Description` can be set/updated in Proposed, Draft, and Open statuses.
- [ ] `Description` cannot be updated in Closed or Completed statuses (inherited behavior).
- [ ] Existing editions without description return `null` for the field.
- [ ] EF Core migration applies cleanly.
- [ ] Unit tests pass for propose/update with and without description.
- [ ] Integration tests verify API round-trip for description field.
- [ ] `data-model.md` spec updated with `Description` field documentation.
