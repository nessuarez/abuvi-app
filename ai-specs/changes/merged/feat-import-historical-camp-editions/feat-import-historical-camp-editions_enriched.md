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

## Phase 0 — Data Preparation: Interactive Jupyter Notebook

Before any migration can be written, the raw data from the PDF must be parsed, reviewed, and geocoded. This is a **one-time offline step** implemented as a Jupyter notebook — consistent with the `feat-import-csv-families` pattern.

**File:** `tools/import-historical-camps.ipynb`

---

### Input format (from the PDF)

Each line in the PDF follows this pattern:

```
YEAR - CAMP NAME [ROMAN NUMERAL] (PROVINCE)
```

Real example:

```
1977 - SELVA DE OZA I (Huesca)
1978 - SELVA DE OZA II (Huesca)
1979 - CANDANCHU (Huesca)
```

**Key observations:**

- **Roman numeral** (`I`, `II`, `III`, ...) indicates how many times ABUVI has camped at that same location. It is **not** part of the camp's name — it must be stripped.
- **Province** in parentheses is the Spanish province (Huesca, Madrid, Segovia, etc.) — this is used as the geocoding hint and as part of the Camp's location description.
- When the same (clean name + province) appears multiple times with incrementing roman numerals, they all map to the **same `Camp` entity** but to **different `CampEdition` rows**.

**Parsing rules:**

| Raw text | year | clean_name | roman_numeral | province |
| --- | --- | --- | --- | --- |
| `1977 - SELVA DE OZA I (Huesca)` | 1977 | `SELVA DE OZA` | 1 | `Huesca` |
| `1978 - SELVA DE OZA II (Huesca)` | 1978 | `SELVA DE OZA` | 2 | `Huesca` |
| `1979 - CANDANCHU (Huesca)` | 1979 | `CANDANCHU` | 1 (implied) | `Huesca` |

---

### Notebook structure

The notebook has 5 sections (cells). Google Places API is called **on demand** in Section 3 — the user reviews the parsed data first and can add extra details before calling the API.

---

#### Section 1 — Paste raw data from PDF

```python
# Paste the lines from the PDF here (one per line)
RAW_LINES = """
1975 - LUGAR DE INICIO I (Madrid)
1976 - LUGAR DE INICIO II (Madrid)
1977 - SELVA DE OZA I (Huesca)
1978 - SELVA DE OZA II (Huesca)
1979 - CANDANCHU (Huesca)
...
2024 - ULTIMO CAMPAMENTO (Guadalajara)
""".strip().splitlines()
```

---

#### Section 2 — Parse and review all editions

```python
import re
import pandas as pd

ROMAN = {"I":1,"II":2,"III":3,"IV":4,"V":5,"VI":6,"VII":7,"VIII":8,"IX":9,"X":10,
         "XI":11,"XII":12,"XIII":13,"XIV":14,"XV":15}

LINE_RE = re.compile(
    r"^(\d{4})\s*[-–]\s*(.+?)\s*((?:XI{0,3}|IX|IV|V?I{0,3})+)?\s*\(([^)]+)\)\s*$",
    re.IGNORECASE
)

def parse_line(line: str) -> dict | None:
    m = LINE_RE.match(line.strip())
    if not m:
        return None
    year_str, raw_name, roman, province = m.groups()
    roman = (roman or "").strip().upper()
    edition_num = ROMAN.get(roman, 1)
    clean_name = raw_name.strip().title()  # "SELVA DE OZA" → "Selva de Oza"
    return {
        "year": int(year_str),
        "clean_name": clean_name,
        "province": province.strip().title(),
        "edition_num": edition_num,
        "raw_line": line.strip(),
    }

rows = [parse_line(l) for l in RAW_LINES if l.strip()]
failed = [l for l, r in zip(RAW_LINES, rows) if r is None]
rows = [r for r in rows if r is not None]

df = pd.DataFrame(rows).sort_values(["clean_name", "province", "year"]).reset_index(drop=True)

if failed:
    print(f"⚠ {len(failed)} lines could not be parsed:")
    for l in failed:
        print(f"  {l}")

print(f"✅ Parsed {len(df)} editions across {df.groupby(['clean_name','province']).ngroups} unique locations\n")
display(df)
```

**Expected output preview:**

```
✅ Parsed 50 editions across 32 unique locations

  year  clean_name        province  edition_num
  1977  Selva de Oza      Huesca    1
  1978  Selva de Oza      Huesca    2
  1979  Candanchu         Huesca    1
  ...
```

**Review checkpoint:** Look at the DataFrame. Fix any parsing issues directly in `RAW_LINES` and re-run.

---

#### Section 3 — Preview unique locations and enrich before geocoding

```python
# Show one row per unique location so you can review/add extra info before calling the API
locations = (
    df.groupby(["clean_name", "province"])
    .agg(
        years=("year", list),
        total_editions=("edition_num", "max"),
    )
    .reset_index()
)

# Add columns for manual enrichment (fill in what you know before calling Google)
locations["search_override"] = ""   # optional: override the Google search query
locations["notes"] = ""             # optional: any historical notes
locations["latitude"] = None        # filled by Section 4
locations["longitude"] = None       # filled by Section 4
locations["formatted_address"] = "" # filled by Section 4
locations["place_id"] = ""          # filled by Section 4
locations["geocode_status"] = "pending"

print(f"Unique locations to geocode: {len(locations)}\n")
display(locations[["clean_name", "province", "years", "total_editions", "search_override", "notes"]])
```

**Fill in `search_override` for any ambiguous names.** For example:

- `Selva de Oza` in Huesca → the default query `"Selva de Oza Huesca Spain"` is probably fine
- A very generic name like `Prado` in `Madrid` might need `search_override = "Camping Prado Manzanares Madrid"`

---

#### Section 4 — Geocode on demand (per location)

```python
import requests
import time
import os

API_KEY = os.environ.get("GOOGLE_PLACES_API_KEY", "")
if not API_KEY:
    raise ValueError("Set GOOGLE_PLACES_API_KEY environment variable before running this cell")

FIND_PLACE_URL = "https://maps.googleapis.com/maps/api/findplacefromtext/json"

def geocode_location(name: str, province: str, search_override: str = "") -> dict:
    query = search_override if search_override.strip() else f"{name} {province} España"
    params = {
        "input": query,
        "inputtype": "textquery",
        "fields": "name,geometry,formatted_address,place_id",
        "language": "es",
        "key": API_KEY,
    }
    resp = requests.get(FIND_PLACE_URL, params=params, timeout=10)
    resp.raise_for_status()
    candidates = resp.json().get("candidates", [])
    if not candidates:
        return {"geocode_status": "not_found"}
    best = candidates[0]
    loc = best.get("geometry", {}).get("location", {})
    return {
        "google_name": best.get("name", ""),
        "formatted_address": best.get("formattedAddress") or best.get("formatted_address", ""),
        "latitude": loc.get("lat"),
        "longitude": loc.get("lng"),
        "place_id": best.get("placeId") or best.get("place_id", ""),
        "geocode_status": "ok",
    }

# Geocode only locations not yet resolved
for i, row in locations.iterrows():
    if row["geocode_status"] == "ok":
        continue
    name, province = row["clean_name"], row["province"]
    print(f"[{i+1}/{len(locations)}] Geocoding: {name} ({province})...")
    result = geocode_location(name, province, row.get("search_override", ""))
    for key, val in result.items():
        locations.at[i, key] = val
    if result["geocode_status"] == "ok":
        print(f"  ✅ {result['latitude']}, {result['longitude']} — {result['formatted_address']}")
    else:
        print(f"  ❌ Not found — fill in latitude/longitude manually")
    time.sleep(0.3)

# Summary
not_found = locations[locations["geocode_status"] != "ok"]
print(f"\nDone. {len(locations) - len(not_found)}/{len(locations)} locations resolved.")
if len(not_found):
    print("\n⚠ Fill in coordinates manually for these locations:")
    display(not_found[["clean_name", "province"]])

display(locations[["clean_name", "province", "latitude", "longitude", "formatted_address", "geocode_status"]])
```

**Review checkpoint:** Examine the results. For any row with wrong coordinates:

```python
# Manually correct a specific location (run this in a new cell)
idx = locations[locations["clean_name"] == "Selva de Oza"].index[0]
locations.at[idx, "latitude"] = 42.7833
locations.at[idx, "longitude"] = -0.6833
locations.at[idx, "formatted_address"] = "Selva de Oza, Hecho, Huesca"
locations.at[idx, "geocode_status"] = "ok_manual"
```

---

#### Section 5 — Generate SQL and save to file

```python
from datetime import date

camp_sql_rows, edition_sql_rows, location_sql_rows = [], [], []

def q(s):
    """Escape single quotes for SQL."""
    return str(s).replace("'", "''")

for camp_idx, loc_row in locations.iterrows():
    camp_id = f"00000000-0000-0000-0002-{camp_idx+1:012d}"
    lat = loc_row["latitude"] if loc_row["latitude"] else "NULL"
    lng = loc_row["longitude"] if loc_row["longitude"] else "NULL"
    address = q(loc_row.get("formatted_address") or f"{loc_row['clean_name']}, {loc_row['province']}")

    camp_sql_rows.append(
        f"('{camp_id}', '{q(loc_row['clean_name'])}', '{address}', "
        f"{lat}, {lng}, 0, 0, 0, false, '1975-01-01', '1975-01-01')"
    )

    # Editions for this camp
    camp_editions = df[
        (df["clean_name"] == loc_row["clean_name"]) &
        (df["province"] == loc_row["province"])
    ].sort_values("year")

    for ed_idx, ed_row in camp_editions.iterrows():
        ed_id = f"00000000-0000-0000-0003-{ed_idx+1:012d}"
        year = ed_row["year"]
        start_date = date(year, 7, 15).isoformat()
        end_date   = date(year, 8,  1).isoformat()
        edition_sql_rows.append(
            f"('{ed_id}', '{camp_id}', {year}, '{start_date}', '{end_date}', "
            f"0, 0, 0, 'Completed', false, false, '1975-01-01', '1975-01-01')"
        )

        loc_id = f"00000000-0000-0000-0004-{ed_idx+1:012d}"
        location_sql_rows.append(
            f"('{loc_id}', '{q(loc_row['clean_name'])}', {year}, "
            f"{lat}, {lng}, '{address}', '1975-01-01', '1975-01-01')"
        )

sql = f"""
-- ============================================================
-- Historical ABUVI camp data — generated {date.today()}
-- Paste into EF Core migration Up() method as migrationBuilder.Sql(...)
-- ============================================================

INSERT INTO camps ("Id", name, location, latitude, longitude,
    price_per_adult, price_per_child, price_per_baby, is_active, created_at, updated_at)
VALUES
{",\n".join(camp_sql_rows)}
ON CONFLICT ("Id") DO NOTHING;

INSERT INTO camp_editions ("Id", camp_id, year, start_date, end_date,
    price_per_adult, price_per_child, price_per_baby,
    status, is_archived, use_custom_age_ranges, created_at, updated_at)
VALUES
{",\n".join(edition_sql_rows)}
ON CONFLICT ("Id") DO NOTHING;

INSERT INTO camp_locations ("Id", name, year, latitude, longitude, address, created_at, updated_at)
VALUES
{",\n".join(location_sql_rows)}
ON CONFLICT ("Id") DO NOTHING;
"""

output_path = "tools/historical-camps-migration.sql"
with open(output_path, "w", encoding="utf-8") as f:
    f.write(sql)

print(f"✅ SQL written to {output_path}")
print(f"   {len(camp_sql_rows)} camps")
print(f"   {len(edition_sql_rows)} editions")
print(f"   {len(location_sql_rows)} camp locations")
```

---

### Workflow summary

```
Section 1: Paste raw PDF lines
     ↓
Section 2: Parse & review DataFrame (fix parsing errors here)
     ↓
Section 3: Review unique locations, fill search_override for ambiguous names
     ↓
Section 4: Geocode on demand → review results → manually correct errors
     ↓
Section 5: Generate SQL → save to tools/historical-camps-migration.sql
     ↓
Copy SQL into EF Core migration Up() method
```

### API cost estimate for geocoding

| Operation | Calls | Cost (2026) |
| --- | --- | --- |
| Find Place from Text (~32 unique locations) | ~32 | Free tier: first 1,000/month free |
| Total | ~32 | **€0** |

Note: only **unique locations** are geocoded (not every edition) — so ~32 calls, not ~50.

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
