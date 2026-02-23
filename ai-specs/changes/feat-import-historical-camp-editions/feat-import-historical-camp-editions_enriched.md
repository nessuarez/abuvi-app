# Enriched User Story: Import Historical Camp Editions

**Feature ID:** `feat-import-historical-camp-editions`
**Date:** 2026-02-23
**Status:** ✅ Ready for implementation
**Type:** One-time data migration / operational tooling

---

## Context & Problem Statement

ABUVI has been running summer camps since 1975 (50th anniversary in 2025). All previous camp editions need to be recorded in the system so that:

- The 50th anniversary page can display a complete historical timeline
- The interactive historical map (via `CampLocation` entity) can be populated
- `GET /api/camps/editions?status=Completed` returns the full history for admin dashboards
- Other anniversary-related features have structured data to query

### The Blocking Validation

The normal API flow to create and complete a `CampEdition` is:

```
POST /api/camps/editions/propose    → Status: Proposed
POST /api/camps/editions/{id}/promote → Status: Draft
PATCH /api/camps/editions/{id}/status → Status: Open  ← BLOCKED for past dates
PATCH /api/camps/editions/{id}/status → Status: Closed
PATCH /api/camps/editions/{id}/status → Status: Completed
```

**The blocker is in `CampEditionsService.ValidateDateConstraintsForTransition`:**

```csharp
// Line 375 in CampEditionsService.cs:
if (newStatus == CampEditionStatus.Open && edition.StartDate.Date < today)
    throw new InvalidOperationException(
        "No se puede abrir el registro de una edición con fecha de inicio en el pasado");
```

For all historical editions (1975–2024), `StartDate < today` is always true, so `Draft → Open` is permanently blocked.

### Why the `force` Flag Is Not the Answer

The `feat-open-edition-amendments` spec adds an Admin-only `force` flag to bypass this date constraint. However, even with that feature:

- This still requires **4 API calls per edition** (Propose → Promote → Open+force → Closed → Completed)
- For ~50 years of camps = **~200–250 API calls**, executed in sequence, with manual Camp IDs
- Not a viable bulk-import strategy
- Puts operational data through a state machine meant for future editions

**Historical data is not operational data.** It should not go through the proposal/approval workflow.

---

## Decision: EF Core Data Migration (Recommended)

Historical camp editions are **known-good data** that should bypass application validation entirely. The appropriate mechanism is a versioned, idempotent **EF Core migration**, consistent with the existing `SeedInitialAdminUser_v2` pattern.

### Why This Approach

| Criteria | EF Core Migration | Admin API Endpoint | Python Script |
|---|---|---|---|
| Runs automatically on deploy | ✅ | ❌ | ❌ |
| Version-controlled | ✅ | ✅ | ✅ |
| Idempotent | ✅ | Needs implementation | Needs implementation |
| No new API surface | ✅ | ❌ | ✅ |
| Bypasses status machine | ✅ | Needs `isHistorical` flag | ✅ |
| Consistent with project patterns | ✅ (admin seeder) | ⚠️ | ✅ (csv-families) |

### Strategy

Historical editions are inserted **directly with `Status = Completed`**. No status machine traversal. Camp templates that don't already exist in the database are also seeded with predictable static UUIDs.

---

## Data Entities Required

Two entities must be seeded:

### 1. `Camp` (Historical Templates)

Some historical camps may have been held at the same location in multiple years. Each distinct physical location gets one `Camp` record. Reuse existing Camp records where possible; create new ones with static UUIDs for historical-only locations.

**Minimum required fields per Camp:**

- `id`: Static UUID in `00000000-0000-0000-0002-xxxxxxxxxxxx` range (e.g., `00000000-0000-0000-0002-000000000001`)
- `name`: Location name (e.g., "Camping El Escorial")
- `location`: Free-text location (e.g., "El Escorial, Madrid")
- `latitude` / `longitude`: Coordinates (required for map features)
- `price_per_adult`, `price_per_child`, `price_per_baby`: Use `0` for historical (actual prices unknown)
- `is_active`: `false` — historical-only camps should not be proposable for new editions
- `created_at` / `updated_at`: Use a fixed past date (e.g., `1975-01-01T00:00:00Z`)

### 2. `CampEdition` (Historical Editions)

One record per year per camp location.

**Minimum required fields per CampEdition:**

- `id`: Static UUID in `00000000-0000-0000-0003-xxxxxxxxxxxx` range
- `camp_id`: FK → the corresponding historical Camp
- `year`: The year (e.g., `1985`)
- `start_date`: Approximate start date if known (e.g., `1985-07-15T00:00:00Z`); use July 15 of that year if exact date unknown
- `end_date`: Approximate end date if known; use August 1 if unknown
- `price_per_adult`, `price_per_child`, `price_per_baby`: Use `0` (historical prices not tracked)
- `status`: `'Completed'` — directly set, no state machine traversal
- `max_capacity`: `null` (historical capacity unknown)
- `is_archived`: `false`
- `use_custom_age_ranges`: `false`
- `created_at` / `updated_at`: Use a fixed past date

---

## Implementation Plan

### Phase 1 — Prepare the Data

Before writing code, prepare the historical data CSV/JSON with the team:

```
year | location_name          | location_city     | latitude  | longitude | start_month | end_month
1975 | ...                    | ...               | ...       | ...       | 7           | 8
1976 | ...
...
2024 | ...
```

**Group locations**: If the same place was used multiple years, group them into one `Camp` record.

### Phase 2 — Create the Migration

**File:** `src/Abuvi.API/Migrations/{timestamp}_SeedHistoricalCampEditions.cs`

```csharp
public partial class SeedHistoricalCampEditions : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        var now = new DateTime(1975, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        // Step 1: Insert historical Camp templates (one per distinct location)
        // Use ON CONFLICT DO NOTHING pattern via conditional insert
        migrationBuilder.Sql(@"
            INSERT INTO camps (""Id"", name, location, latitude, longitude,
                price_per_adult, price_per_child, price_per_baby,
                is_active, created_at, updated_at)
            VALUES
                ('00000000-0000-0000-0002-000000000001', 'Camping El Escorial', 'El Escorial, Madrid',
                 40.5833, -4.1167, 0, 0, 0, false, '1975-01-01', '1975-01-01'),
                -- Add all historical locations here
                ('00000000-0000-0000-0002-000000000002', '...', '...', 0, 0, 0, 0, 0, false, '1975-01-01', '1975-01-01')
            ON CONFLICT (""Id"") DO NOTHING;
        ");

        // Step 2: Insert historical CampEditions with status = Completed
        migrationBuilder.Sql(@"
            INSERT INTO camp_editions (""Id"", camp_id, year, start_date, end_date,
                price_per_adult, price_per_child, price_per_baby,
                status, is_archived, use_custom_age_ranges,
                created_at, updated_at)
            VALUES
                ('00000000-0000-0000-0003-000000000001', '00000000-0000-0000-0002-000000000001',
                 1975, '1975-07-15', '1975-08-01', 0, 0, 0, 'Completed', false, false, '1975-01-01', '1975-01-01'),
                -- Repeat for each year
            ON CONFLICT (""Id"") DO NOTHING;
        ");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Remove all seeded historical editions (IDs in the reserved range)
        migrationBuilder.Sql(@"
            DELETE FROM camp_editions
            WHERE ""Id"" LIKE '00000000-0000-0000-0003-%';

            DELETE FROM camps
            WHERE ""Id"" LIKE '00000000-0000-0000-0002-%'
            AND is_active = false;
        ");
    }
}
```

> **Important:** Check the actual PostgreSQL column names using the existing migration files. The `camps` table uses snake_case columns as defined by EF Core conventions.

### Phase 3 — Also Seed `CampLocation` for the Map

The `CampLocation` entity is specifically designed for the historical map feature (interactive map on 50th anniversary page). For each historical camp edition, a corresponding `CampLocation` should also be seeded.

**File:** Same migration or a separate `SeedHistoricalCampLocations.cs`

```sql
INSERT INTO camp_locations (""Id"", name, year, latitude, longitude, address, description, created_at, updated_at)
VALUES
    ('00000000-0000-0000-0004-000000000001', 'El Escorial', 1975, 40.5833, -4.1167,
     'El Escorial, Madrid', NULL, '1975-01-01', '1975-01-01'),
    -- One row per year per location
ON CONFLICT (""Id"") DO NOTHING;
```

`CampLocation` is the primary entity for anniversary map display; `CampEdition` is for operational history and statistics.

---

## Alternative: Admin Bulk Import API Endpoint

If the team prefers a non-deployment-dependent approach (e.g., filling data via Postman/Swagger), a purpose-built endpoint can be created:

**Endpoint:** `POST /api/camps/editions/historical/import`
**Auth:** Admin only
**Request body:**

```json
{
  "editions": [
    {
      "campId": "...",
      "year": 1985,
      "startDate": "1985-07-15",
      "endDate": "1985-08-01",
      "locationName": "Camping El Escorial",
      "notes": "Historical data imported 2026-02"
    }
  ]
}
```

**Service behaviour:**

- Creates Camp record if `campId` not found (auto-creates historical camp with `isActive = false`)
- Sets `status = Completed` directly — **no status machine traversal**
- Skips duplicate check for `(campId, year)` if `isHistorical = true` flag present, OR checks that existing edition is already `Completed`
- Returns import summary: `{ imported: 48, skipped: 2, errors: [] }`

**Trade-off:** More flexible for iterative imports, but requires a new endpoint, validator, and service method. Adds an API surface that should be disabled or removed post-import.

---

## UUID Ranges (Reserved for Historical Seeds)

To keep historical seeded data identifiable and prevent conflicts with real (random) UUIDs:

| Entity | UUID prefix |
|--------|-------------|
| Historical `Camp` templates | `00000000-0000-0000-0002-xxxxxxxxxxxx` |
| Historical `CampEdition` records | `00000000-0000-0000-0003-xxxxxxxxxxxx` |
| Historical `CampLocation` records | `00000000-0000-0000-0004-xxxxxxxxxxxx` |

These are deterministic and idempotent. `ON CONFLICT DO NOTHING` prevents double-inserts on re-deploy.

---

## What Historical Editions Are Used For

Once seeded, historical editions are accessible via:

| Use case | Query |
|---|---|
| Full camp history | `GET /api/camps/editions?status=Completed` |
| Editions for a specific year | `GET /api/camps/editions?year=1985` |
| Count of camps per location | `GET /api/camps/editions?campId={id}` |
| 50th anniversary timeline | Frontend queries all Completed editions, ordered by year |
| Interactive map | `CampLocation` entity (separate query/endpoint) |

---

## Validation Rules That Do NOT Apply to Historical Data

The following validations exist in the API but are **intentionally bypassed** by the migration approach:

| Validation | Why it doesn't apply |
|---|---|
| `Draft → Open` requires `StartDate >= today` | Historical editions skip the state machine entirely |
| `ExistsAsync` duplicate check | Idempotent inserts use `ON CONFLICT DO NOTHING` |
| Camp must be `isActive = true` to propose | Historical camp templates are `isActive = false` (read-only) |
| Prices must be >= 0 | Using `0` as placeholder for unknown historical prices |

---

## Acceptance Criteria

- [ ] A single EF Core migration seeds all historical camp editions (1975–2024) with `status = Completed`
- [ ] Historical `Camp` templates are seeded with `isActive = false` so they cannot be proposed for new editions
- [ ] `CampLocation` records are seeded for the interactive historical map
- [ ] Migration is idempotent — re-running it does not create duplicate records (`ON CONFLICT DO NOTHING`)
- [ ] Migration has a `Down` method that removes all seeded records (rollback support)
- [ ] `GET /api/camps/editions?status=Completed` returns all historical editions after migration runs
- [ ] Static UUIDs follow the reserved prefix scheme (`00000000-0000-0000-000x-...`)
- [ ] Historical editions do NOT appear in `GET /api/camps/editions/active` or `GET /api/camps/editions/current`
- [ ] Application starts successfully after the migration runs (auto-migrate on startup)
- [ ] No changes to application validation logic (this is a data concern, not a code concern)

---

## Out of Scope

- Importing registration history (who attended each historical camp) — future spec
- Importing payment history for historical camps — not tracked historically
- UI management of historical editions — they are read-only (Completed status, no updates allowed)
- Actual historical prices — use `0` as placeholder; can be updated later if needed
- `feat-open-edition-amendments` — the `force` flag is for re-opening active editions, not for historical import

---

## Dependencies

- `feat-camp-editions-management` (Phase 4) must be merged — `CampEdition` CRUD and `GetAllAsync` endpoints must exist
- Actual historical data (list of years, locations, dates) must be provided by the association before the migration can be written
- Column names in PostgreSQL must be verified against the most recent migration snapshot

---

## Notes for the Developer

1. **Check column names first.** Run `\d camp_editions` on the dev DB or read the latest migration snapshot to confirm column names. EF Core uses snake_case by default (e.g., `camp_id`, `start_date`, `use_custom_age_ranges`).
2. **The `status` column stores enum as string.** Per the project's standards ("Enums stored as strings for readability"), use `'Completed'` not `4`.
3. **Do not use `HasData` in `DbContext.OnModelCreating`** — that pattern forces static UUIDs to be embedded in every future migration snapshot. The raw `migrationBuilder.Sql` / `InsertData` approach (as used in `SeedInitialAdminUser_v2`) is preferred.
4. **Test locally first.** Run `dotnet ef migrations add SeedHistoricalCampEditions` and `dotnet ef database update` in the dev environment before committing.
5. **This ticket is blocked until the historical data spreadsheet is ready.** The migration structure can be implemented (with placeholder rows) but the actual data rows require confirmation from the association.
