# Backend Implementation Plan: feat-e2e-test-credentials-setup — E2E Sandbox Infrastructure

## Overview

This plan covers the backend-side work required by the spec
`ai-specs/changes/feat-e2e-test-credentials-setup_enriched.md`:

1. Add an **E2E ASP.NET Core environment** (`appsettings.E2E.json` + launch profile) so the API
   can boot against a sandbox database (`abuvi_e2e`) without touching `abuvi_prod`.
2. Add a **`RegistrationSeeder`** to `Abuvi.Setup` that inserts pre-built registrations
   (using the same pricing logic as the API) so E2E tests have realistic data to act on.

No new API endpoints, EF Core migrations, or production schema changes are involved.

---

## Architecture Context

### Files to create

| File | Project | Notes |
| --- | --- | --- |
| `src/Abuvi.API/appsettings.E2E.json` | Abuvi.API | Overrides connection string only |
| `src/Abuvi.Setup/RegistrationSeeder.cs` | Abuvi.Setup | Hardcoded seed; uses EF Core directly |

### Files to modify

| File | Project | Change |
| --- | --- | --- |
| `src/Abuvi.API/Properties/launchSettings.json` | Abuvi.API | Add `E2E` profile on port 5080 |
| `src/Abuvi.Setup/SeedRunner.cs` | Abuvi.Setup | Call `RegistrationSeeder.SeedAsync()` at end of `ImportAllAsync()` |

### Cross-cutting notes

- `Abuvi.Setup.csproj` already has `<ProjectReference Include="..\Abuvi.API\Abuvi.API.csproj" />`,
  so `RegistrationSeeder` can use any type from `Abuvi.API` (entities, enums, `AbuviDbContext`).
- `RegistrationPricingService` (from `Abuvi.API`) is **not reused directly** because it requires
  DI (`IAssociationSettingsRepository`). Instead, the seeder queries `db.AssociationSettings`
  for the `"age_ranges"` key and inlines the same 4-line age/category logic.
- The API auto-runs `await dbContext.Database.MigrateAsync()` at startup
  (`Program.cs:328`). When the E2E profile is active, this creates and migrates `abuvi_e2e`
  automatically on the first boot — **no manual database creation script needed**.

---

## Implementation Steps

### Step 0: Create feature branch

- **Action**: Switch to the backend branch before any changes.
- **Implementation Steps**:
  1. `git checkout dev && git pull origin dev`
  2. `git checkout -b feature/feat-e2e-credentials-backend`
  3. `git branch` — verify you're on the new branch.

---

### Step 1: Create `src/Abuvi.API/appsettings.E2E.json`

- **File**: `src/Abuvi.API/appsettings.E2E.json`
- **Action**: Override only the connection string and log minimum level. Everything else
  (JWT, Resend, MinIO, etc.) inherits from `appsettings.json`.

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Port=5432;Database=abuvi_e2e;Username=abuvi_user;Password=dev_password"
  },
  "Serilog": {
    "MinimumLevel": {
      "Default": "Warning",
      "Override": {
        "Microsoft.AspNetCore": "Warning",
        "Microsoft.EntityFrameworkCore": "Warning"
      }
    }
  }
}
```

- **Implementation Notes**:
  - ASP.NET Core merges appsettings files by environment name automatically when
    `ASPNETCORE_ENVIRONMENT=E2E`.
  - `Warning`-only logging keeps the E2E terminal output clean.
  - Do **not** add secrets, API keys, or Resend config here — they fall through from
    `appsettings.json` (which has empty values, safe for local dev).

---

### Step 2: Add `E2E` profile to `src/Abuvi.API/Properties/launchSettings.json`

- **File**: `src/Abuvi.API/Properties/launchSettings.json`
- **Action**: Add the `E2E` profile alongside the existing `http` and `https` profiles.

Current file has two profiles. Add a third:

```json
"E2E": {
  "commandName": "Project",
  "dotnetRunMessages": true,
  "launchBrowser": false,
  "applicationUrl": "http://localhost:5080",
  "environmentVariables": {
    "ASPNETCORE_ENVIRONMENT": "E2E"
  }
}
```

- **Key detail**: Port `5080` (not `5079` which is the dev profile) so both API instances
  can run simultaneously during development.
- **Usage**: `dotnet run --project src/Abuvi.API --launch-profile E2E`
- **First run behaviour**: `MigrateAsync()` at `Program.cs:328` creates `abuvi_e2e` and
  applies all migrations. Subsequent runs are a no-op.

---

### Step 3: Create `src/Abuvi.Setup/RegistrationSeeder.cs`

- **File**: `src/Abuvi.Setup/RegistrationSeeder.cs`
- **Action**: Insert two seed registrations into `abuvi_e2e` (or whichever DB
  `AbuviDbContext` is connected to). Idempotent — skips if registrations already exist
  for the target edition.

**Seed data summary**:

| # | FamilyUnit | User | Status | Members | Total |
| --- | --- | --- | --- | --- | --- |
| 1 | Garcia Family | `member1@abuvi.local` | `Pending` | Carlos (Adult, 45), Laura (Adult, 41), Pablo (Child, 16), Sofia (Child, 13) | 600 € |
| 2 | Lopez Family | `board@abuvi.local` | `Confirmed` + 1 payment | Ana (Adult, 48) | 180 € |

Ages computed against `Camp Costa 2027` start date `2027-08-01`:

| Member | DOB | Age | Category | Price |
| --- | --- | --- | --- | --- |
| Carlos Garcia | 1982-03-15 | 45 | Adult | 180 € |
| Laura Garcia | 1985-07-22 | 41 | Adult | 180 € |
| Pablo Garcia | 2010-11-05 | 16 | Child | 120 € |
| Sofia Garcia | 2014-04-18 | 13 | Child | 120 € |
| Ana Lopez | 1979-01-30 | 48 | Adult | 180 € |

**Implementation**:

```csharp
namespace Abuvi.Setup;

using System.Text.Json;
using Abuvi.API.Data;
using Abuvi.API.Features.Registrations;
using Microsoft.EntityFrameworkCore;
using Serilog;

public class RegistrationSeeder(AbuviDbContext db)
{
    private record AgeRanges(int BabyMaxAge, int ChildMinAge, int ChildMaxAge, int AdultMinAge);

    public async Task SeedAsync()
    {
        Log.Information("Seeding registrations...");

        // Resolve Camp Costa 2027
        var edition = await db.CampEditions
            .Include(e => e.Camp)
            .FirstOrDefaultAsync(e =>
                e.Camp.Name == "Camp Costa" && e.Year == 2027);

        if (edition is null)
        {
            Log.Warning("Registrations: skipped (Camp Costa 2027 not found)");
            return;
        }

        // Idempotency guard — skip if already seeded
        if (await db.Registrations.AnyAsync(r => r.CampEditionId == edition.Id))
        {
            Log.Information("Registrations: already seeded, skipping");
            return;
        }

        // Resolve age ranges from AssociationSettings
        var setting = await db.AssociationSettings
            .FirstOrDefaultAsync(s => s.SettingKey == "age_ranges");

        if (setting is null)
        {
            Log.Warning("Registrations: skipped (age_ranges setting not found)");
            return;
        }

        var ranges = JsonSerializer.Deserialize<AgeRanges>(
            setting.SettingValue,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        if (ranges is null)
        {
            Log.Warning("Registrations: skipped (age_ranges JSON is invalid)");
            return;
        }

        var now = DateTime.UtcNow;

        await SeedGarciaRegistrationAsync(edition, ranges, now);
        await SeedLopezRegistrationAsync(edition, ranges, now);

        Log.Information("Registrations: seeded successfully");
    }

    private async Task SeedGarciaRegistrationAsync(
        API.Features.Camps.CampEdition edition,
        AgeRanges ranges,
        DateTime now)
    {
        var familyUnit = await db.FamilyUnits
            .FirstOrDefaultAsync(f => f.Name == "Garcia Family");
        var registeredBy = await db.Users
            .FirstOrDefaultAsync(u => u.Email == "member1@abuvi.local");

        if (familyUnit is null || registeredBy is null)
        {
            Log.Warning("Registrations: skipped Garcia (family or user not found)");
            return;
        }

        // Resolve family members
        var members = await db.FamilyMembers
            .Where(m => m.FamilyUnitId == familyUnit.Id)
            .ToListAsync();

        var registrationMembers = new List<RegistrationMember>();
        decimal totalAmount = 0;

        foreach (var member in members)
        {
            var age = CalculateAge(member.DateOfBirth, edition.StartDate);
            var category = GetAgeCategory(age, ranges);
            if (category is null) continue;

            var price = GetPrice(category.Value, edition);
            totalAmount += price;

            registrationMembers.Add(new RegistrationMember
            {
                Id = Guid.NewGuid(),
                FamilyMemberId = member.Id,
                AgeAtCamp = age,
                AgeCategory = category.Value,
                IndividualAmount = price,
                AttendancePeriod = AttendancePeriod.Complete,
                CreatedAt = now,
            });
        }

        var registrationId = Guid.NewGuid();
        foreach (var m in registrationMembers)
            m.RegistrationId = registrationId;

        var registration = new Registration
        {
            Id = registrationId,
            FamilyUnitId = familyUnit.Id,
            CampEditionId = edition.Id,
            RegisteredByUserId = registeredBy.Id,
            BaseTotalAmount = totalAmount,
            ExtrasAmount = 0,
            TotalAmount = totalAmount,
            Status = RegistrationStatus.Pending,
            Members = registrationMembers,
            StatusHistory =
            [
                new RegistrationStatusHistory
                {
                    Id = Guid.NewGuid(),
                    RegistrationId = registrationId,
                    PreviousStatus = RegistrationStatus.Pending,
                    NewStatus = RegistrationStatus.Pending,
                    ChangedAt = now,
                    Trigger = StatusChangeTrigger.Automatic,
                }
            ],
            CreatedAt = now,
            UpdatedAt = now,
        };

        db.Registrations.Add(registration);
        await db.SaveChangesAsync();
    }

    private async Task SeedLopezRegistrationAsync(
        API.Features.Camps.CampEdition edition,
        AgeRanges ranges,
        DateTime now)
    {
        var familyUnit = await db.FamilyUnits
            .FirstOrDefaultAsync(f => f.Name == "Lopez Family");
        var registeredBy = await db.Users
            .FirstOrDefaultAsync(u => u.Email == "board@abuvi.local");

        if (familyUnit is null || registeredBy is null)
        {
            Log.Warning("Registrations: skipped Lopez (family or user not found)");
            return;
        }

        var member = await db.FamilyMembers
            .FirstOrDefaultAsync(m =>
                m.FamilyUnitId == familyUnit.Id &&
                m.FirstName == "Ana" &&
                m.LastName == "Lopez");

        if (member is null)
        {
            Log.Warning("Registrations: skipped Lopez (Ana Lopez not found)");
            return;
        }

        var age = CalculateAge(member.DateOfBirth, edition.StartDate);
        var category = GetAgeCategory(age, ranges) ?? AgeCategory.Adult;
        var price = GetPrice(category, edition);

        var registrationId = Guid.NewGuid();
        var paymentId = Guid.NewGuid();

        var registration = new Registration
        {
            Id = registrationId,
            FamilyUnitId = familyUnit.Id,
            CampEditionId = edition.Id,
            RegisteredByUserId = registeredBy.Id,
            BaseTotalAmount = price,
            ExtrasAmount = 0,
            TotalAmount = price,
            Status = RegistrationStatus.Confirmed,
            Members =
            [
                new RegistrationMember
                {
                    Id = Guid.NewGuid(),
                    RegistrationId = registrationId,
                    FamilyMemberId = member.Id,
                    AgeAtCamp = age,
                    AgeCategory = category,
                    IndividualAmount = price,
                    AttendancePeriod = AttendancePeriod.Complete,
                    CreatedAt = now,
                }
            ],
            Payments =
            [
                new Payment
                {
                    Id = paymentId,
                    RegistrationId = registrationId,
                    Amount = price,
                    PaymentDate = now.AddDays(-30),
                    Method = PaymentMethod.Transfer,
                    Status = PaymentStatus.Completed,
                    InstallmentNumber = 1,
                    IsManual = false,
                    CreatedAt = now.AddDays(-30),
                    UpdatedAt = now,
                }
            ],
            StatusHistory =
            [
                new RegistrationStatusHistory
                {
                    Id = Guid.NewGuid(),
                    RegistrationId = registrationId,
                    PreviousStatus = RegistrationStatus.Pending,
                    NewStatus = RegistrationStatus.Confirmed,
                    ChangedAt = now.AddDays(-29),
                    Trigger = StatusChangeTrigger.AdminAction,
                }
            ],
            CreatedAt = now.AddDays(-30),
            UpdatedAt = now,
        };

        db.Registrations.Add(registration);
        await db.SaveChangesAsync();
    }

    // Inlined from RegistrationPricingService (same logic, no DI required)
    private static int CalculateAge(DateOnly dateOfBirth, DateTime campStartDate)
    {
        var campDate = DateOnly.FromDateTime(campStartDate);
        var age = campDate.Year - dateOfBirth.Year;
        if (campDate < dateOfBirth.AddYears(age)) age--;
        return age;
    }

    private static AgeCategory? GetAgeCategory(int age, AgeRanges ranges)
    {
        if (age >= 0 && age <= ranges.BabyMaxAge)                          return AgeCategory.Baby;
        if (age >= ranges.ChildMinAge && age <= ranges.ChildMaxAge)        return AgeCategory.Child;
        if (age >= ranges.AdultMinAge)                                     return AgeCategory.Adult;
        return null; // Age fits no configured range — skip member
    }

    private static decimal GetPrice(AgeCategory category, API.Features.Camps.CampEdition edition)
        => category switch
        {
            AgeCategory.Adult => edition.PricePerAdult,
            AgeCategory.Child => edition.PricePerChild,
            AgeCategory.Baby  => edition.PricePerBaby,
            _                 => 0m,
        };
}
```

- **Implementation Notes**:
  - Idempotent: skips if any registration for `Camp Costa 2027` already exists.
  - `GetAgeCategory` returns `null` for ages that fall outside configured ranges (gap
    between baby and child) — those members are silently skipped.
  - `CalculateAge` is intentionally inlined (not calling `RegistrationPricingService`)
    because the seeder does not have the DI infrastructure to instantiate it.
  - All amounts are pre-calculated from the `CampEdition` pricing columns — no business
    logic divergence from the production service.
  - The `RegistrationStatusHistory` for Garcia starts as `Pending → Pending`
    (initial record, same as the API creates on `CreateAsync`).

---

### Step 4: Modify `src/Abuvi.Setup/SeedRunner.cs`

- **File**: `src/Abuvi.Setup/SeedRunner.cs`
- **Action**: Call `RegistrationSeeder.SeedAsync()` at the end of `ImportAllAsync()`,
  after all CSV importers have run.

Add at the end of the `try` block in `ImportAllAsync`, before `Log.Information("Setup complete")`:

```csharp
// Seed registrations (C# hardcoded, not CSV — complex pricing logic)
if (!dryRun)
{
    var registrationSeeder = new RegistrationSeeder(db);
    await registrationSeeder.SeedAsync();
}
else
{
    Log.Information("Dry-run: skipping registration seeder");
}
```

- **Implementation Notes**:
  - Registration seeder is skipped in `--dry-run` mode because it uses `SaveChangesAsync`
    directly (not wrapped in the dry-run transaction). This is consistent with the
    existing importers' dry-run behaviour via `IDbContextTransaction`.
  - `ResetAsync()` already deletes all `Registrations` and `Payments` via
    `ExecuteDeleteAsync()` before re-importing CSVs, so re-running `run-all` is
    fully idempotent.

---

### Step 5: Update technical documentation

- **Action**: Review and update relevant documentation files.
- **Implementation Steps**:
  1. **`ai-specs/specs/data-model.md`**: No changes — no new schema entities.
  2. **`ai-specs/specs/api-spec.yml`**: No changes — no new endpoints.
  3. **`.claude/e2e-credentials.md`**: Create this file (frontend task, listed here for
     completeness — see frontend plan).
  4. After implementation, verify `Abuvi.Setup` README or inline help text in
     `Program.cs` still accurately describes the `run-all` command (add a note that
     registrations are auto-seeded).

---

## Implementation Order

1. Step 0 — Create feature branch `feature/feat-e2e-credentials-backend`
2. Step 1 — Create `appsettings.E2E.json`
3. Step 2 — Add `E2E` launch profile to `launchSettings.json`
4. Step 3 — Create `RegistrationSeeder.cs`
5. Step 4 — Modify `SeedRunner.cs` to call the seeder
6. Step 5 — Documentation review

---

## Testing Checklist

### Manual verification after implementation

- [ ] `dotnet build` compiles without warnings (`TreatWarningsAsErrors` is on in `Abuvi.Tests.csproj`)
- [ ] `dotnet run --project src/Abuvi.API --launch-profile E2E` starts on port 5080
- [ ] First boot creates `abuvi_e2e` database and applies all migrations
- [ ] `dotnet run --project src/Abuvi.Setup run-all` (with default connection pointing to
  `abuvi_e2e` via `--connection`) completes without errors and logs seed counts
- [ ] After `run-all`, `abuvi_e2e` contains exactly 2 registrations:
  - `Garcia Family` → `Pending`, 4 members, `TotalAmount = 600.00`
  - `Lopez Family` → `Confirmed`, 1 member, `TotalAmount = 180.00`, 1 `Payment` with
    `Status = Completed`
- [ ] Running `run-all` a second time is a no-op for registrations (idempotency guard)
- [ ] `abuvi_prod` database is untouched throughout

### No xUnit tests required

`Abuvi.Setup` is a dev-only CLI tool with no production path. No unit tests are expected.
The manual checklist above is the acceptance gate.

---

## Error Response Format

N/A — this feature adds no API endpoints.

---

## Dependencies

No new NuGet packages. No EF Core migrations (no schema changes).

`Abuvi.Setup.csproj` already has:

```xml
<ProjectReference Include="..\Abuvi.API\Abuvi.API.csproj" />
```

This gives `RegistrationSeeder` access to `Registration`, `RegistrationMember`,
`Payment`, `RegistrationStatus`, `PaymentMethod`, `PaymentStatus`,
`AttendancePeriod`, `AgeCategory`, `StatusChangeTrigger`, and `AbuviDbContext`.

---

## Notes

- **No production risk**: All changes are either config files (`appsettings.E2E.json`,
  `launchSettings.json`) or dev-tool code (`Abuvi.Setup`). The production API path is
  unchanged.
- **`AssociationSettings` must exist**: The `age_ranges` setting must be present in the
  target database for `RegistrationSeeder` to work. On a fresh `abuvi_e2e`, this setting
  is seeded via the existing EF migration `SeedInitialData` (or equivalent). If missing,
  the seeder logs a warning and skips — it does not crash the `run-all` command.
- **`DateOfBirth` type**: `FamilyMember.DateOfBirth` is `DateOnly`. Verify the column
  exists in `FamilyMemberConfiguration` before implementing — if `DateOfBirth` is
  nullable (`DateOnly?`), add a null-guard in `CalculateAge`.
- **CORS**: The E2E API profile allows requests from `http://localhost:5173`
  (already in `appsettings.json` `AllowedOrigins`). No CORS changes needed.

---

## Next Steps After Implementation

1. Implement the **frontend plan** (`feat-e2e-test-credentials-setup_frontend.md`):
   - `cypress.env.json`, `cy.login()`, `e2e:seed` npm script, `cypress.config.ts` update
   - `.claude/e2e-credentials.md`
2. Merge both branches and run a full E2E smoke test with Cypress.
