# Implementation Plan: feat-db-setup-tool — Console App for DB Reset & CSV Setup

## 2. Overview

A **standalone .NET console application** (`Abuvi.Setup`) that serves as the **single setup tool** for both development and production environments:

1. **Reset the database** — wipes all data in FK-safe order, re-seeds the default admin user.
2. **Import initial data from CSV files** — reads from a configurable directory, processes in dependency order.

The tool operates in two modes controlled by `--env`:

| Mode | `reset` | `import` on non-empty tables | Confirmation required |
|------|---------|------------------------------|----------------------|
| `dev` (default) | Allowed freely | Allowed (skips duplicates) | No |
| `production` | Requires `--confirm` | Only on empty tables (aborts otherwise) | Yes, for destructive ops |

The console app references the API project to reuse `AbuviDbContext` and entity models directly — no HTTP, no auth tokens, no API overhead. CSV columns are aligned with database columns (user provides files matching the schema).

**Import dependency order (strict):**

```
Users → FamilyUnits → FamilyMembers → Camps → CampEditions
```

---

## 3. Architecture Context

**New project:** `src/Abuvi.Setup/`

### Project structure

```
src/Abuvi.Setup/
├── Abuvi.Setup.csproj             # Console app, references Abuvi.API
├── Program.cs                      # CLI entry point
├── SetupConfig.cs                  # Parsed CLI options (env, dir, connection, confirm)
├── SeedRunner.cs                   # Orchestrates reset + imports
├── SafetyGuard.cs                  # Production safety checks
├── Importers/
│   ├── UserImporter.cs
│   ├── FamilyUnitImporter.cs
│   ├── FamilyMemberImporter.cs
│   ├── CampImporter.cs
│   └── CampEditionImporter.cs
├── CsvHelper.cs                    # Generic CSV parsing utility
├── Models.cs                       # SeedResult, SeedRowResult records
└── seed/                           # Default CSV directory (user-provided files)
    ├── users.csv
    ├── family-units.csv
    ├── family-members.csv
    ├── camps.csv
    └── camp-editions.csv
```

### Files to create

| File | Purpose |
|------|---------|
| `src/Abuvi.Setup/Abuvi.Setup.csproj` | Console app project file |
| `src/Abuvi.Setup/Program.cs` | CLI entry point with argument parsing |
| `src/Abuvi.Setup/SetupConfig.cs` | Strongly-typed CLI configuration |
| `src/Abuvi.Setup/SeedRunner.cs` | Orchestrates reset and CSV import pipeline |
| `src/Abuvi.Setup/SafetyGuard.cs` | Production safety checks (empty table verification, confirmation) |
| `src/Abuvi.Setup/Models.cs` | `SeedResult`, `SeedRowResult` records |
| `src/Abuvi.Setup/CsvHelper.cs` | Generic CSV line parser (comma-separated, UTF-8) |
| `src/Abuvi.Setup/Importers/UserImporter.cs` | Reads `users.csv`, inserts into `users` table |
| `src/Abuvi.Setup/Importers/FamilyUnitImporter.cs` | Reads `family-units.csv`, resolves representative by email |
| `src/Abuvi.Setup/Importers/FamilyMemberImporter.cs` | Reads `family-members.csv`, resolves family unit by name |
| `src/Abuvi.Setup/Importers/CampImporter.cs` | Reads `camps.csv`, inserts into `camps` table |
| `src/Abuvi.Setup/Importers/CampEditionImporter.cs` | Reads `camp-editions.csv`, resolves camp by name |
| `src/Abuvi.Tests/Unit/Setup/SeedRunnerTests.cs` | Unit tests for orchestration |
| `src/Abuvi.Tests/Unit/Setup/SafetyGuardTests.cs` | Unit tests for safety checks |
| `src/Abuvi.Tests/Unit/Setup/CsvHelperTests.cs` | Unit tests for CSV parsing |
| `src/Abuvi.Tests/Unit/Setup/Importers/UserImporterTests.cs` | Importer unit tests |

### Files to modify

| File | Change |
|------|--------|
| `Abuvi.sln` | Add `Abuvi.Setup` project reference |

---

## 4. Implementation Steps

### Step 0: Create Feature Branch

```bash
git checkout main && git pull origin main
git checkout -b feature/feat-db-setup-tool
```

---

### Step 1: Create console project and wire EF Core

**File:** `src/Abuvi.Setup/Abuvi.Setup.csproj`

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net9.0</TargetFramework>
  </PropertyGroup>
  <ItemGroup>
    <ProjectReference Include="..\Abuvi.API\Abuvi.API.csproj" />
  </ItemGroup>
  <ItemGroup>
    <PackageReference Include="BCrypt.Net-Next" Version="4.*" />
  </ItemGroup>
</Project>
```

> **Key:** Reuses `AbuviDbContext`, all entity models, and EF configurations from the API project.

---

### Step 2: Implement `SetupConfig.cs`

```csharp
namespace Abuvi.Setup;

public enum SetupEnv { Dev, Production }

public class SetupConfig
{
    public SetupEnv Env { get; init; } = SetupEnv.Dev;
    public string ConnectionString { get; init; } = null!;
    public string SeedDir { get; init; } = null!;
    public bool Confirm { get; init; }

    public bool IsProduction => Env == SetupEnv.Production;

    public static SetupConfig Parse(string[] args)
    {
        var env = args.FirstOrDefault(a => a.StartsWith("--env="))
            ?.Replace("--env=", "");

        var connection = args.FirstOrDefault(a => a.StartsWith("--connection="))
            ?.Replace("--connection=", "")
            ?? Environment.GetEnvironmentVariable("DATABASE_URL")
            ?? "Host=localhost;Database=abuvi;Username=postgres;Password=postgres";

        var dir = args.FirstOrDefault(a => a.StartsWith("--dir="))
            ?.Replace("--dir=", "")
            ?? Path.Combine(AppContext.BaseDirectory, "seed");

        var confirm = args.Contains("--confirm");

        return new SetupConfig
        {
            Env = env?.ToLowerInvariant() == "production"
                ? SetupEnv.Production
                : SetupEnv.Dev,
            ConnectionString = connection,
            SeedDir = dir,
            Confirm = confirm
        };
    }
}
```

---

### Step 3: Implement `SafetyGuard.cs`

```csharp
namespace Abuvi.Setup;

using Abuvi.API.Data;
using Microsoft.EntityFrameworkCore;

public class SafetyGuard(AbuviDbContext db, SetupConfig config)
{
    /// <summary>
    /// In production, reset requires --confirm flag.
    /// </summary>
    public void EnsureResetAllowed()
    {
        if (!config.IsProduction) return;

        if (!config.Confirm)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.Error.WriteLine(
                "PRODUCTION RESET BLOCKED: this will DELETE ALL DATA.");
            Console.Error.WriteLine(
                "Add --confirm flag to proceed: dotnet run reset --env=production --confirm");
            Console.ResetColor();
            Environment.Exit(1);
        }

        // Double-check: interactive confirmation
        Console.ForegroundColor = ConsoleColor.Red;
        Console.Write("You are about to RESET a PRODUCTION database. Type 'YES' to confirm: ");
        Console.ResetColor();
        var input = Console.ReadLine()?.Trim();
        if (input != "YES")
        {
            Console.Error.WriteLine("Aborted.");
            Environment.Exit(1);
        }
    }

    /// <summary>
    /// In production, import is only allowed on empty tables (initial setup).
    /// Returns true if import can proceed.
    /// </summary>
    public async Task<bool> EnsureImportAllowed(string entity, CancellationToken ct = default)
    {
        if (!config.IsProduction) return true;

        var hasData = entity.ToLowerInvariant() switch
        {
            "users" => await db.Users.CountAsync(ct) > 1, // >1 because admin is always seeded
            "familyunits" => await db.FamilyUnits.AnyAsync(ct),
            "familymembers" => await db.FamilyMembers.AnyAsync(ct),
            "camps" => await db.Camps.AnyAsync(ct),
            "campeditions" => await db.CampEditions.AnyAsync(ct),
            _ => false
        };

        if (hasData)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.Error.WriteLine(
                $"  {entity}: BLOCKED — table already has data (production mode).");
            Console.Error.WriteLine(
                "  Use 'reset' first or switch to --env=dev for incremental imports.");
            Console.ResetColor();
            return false;
        }

        return true;
    }
}
```

---

### Step 4: Implement `Program.cs` — CLI entry point

```csharp
using Abuvi.API.Data;
using Abuvi.Setup;
using Microsoft.EntityFrameworkCore;

var config = SetupConfig.Parse(args);

// Show environment banner
Console.ForegroundColor = config.IsProduction ? ConsoleColor.Red : ConsoleColor.Cyan;
Console.WriteLine($"=== Abuvi Setup Tool [{config.Env}] ===\n");
Console.ResetColor();

var options = new DbContextOptionsBuilder<AbuviDbContext>()
    .UseNpgsql(config.ConnectionString)
    .Options;

await using var db = new AbuviDbContext(options);

// Verify DB connection
try
{
    await db.Database.CanConnectAsync();
    Console.WriteLine($"Connected to database.\n");
}
catch (Exception ex)
{
    Console.ForegroundColor = ConsoleColor.Red;
    Console.Error.WriteLine($"Cannot connect to database: {ex.Message}");
    Console.ResetColor();
    return 1;
}

var guard = new SafetyGuard(db, config);
var runner = new SeedRunner(db, guard, config);

// Parse command (first positional arg that is not a flag)
var command = args.FirstOrDefault(a => !a.StartsWith("--")) ?? "run-all";

switch (command)
{
    case "reset":
        guard.EnsureResetAllowed();
        await runner.ResetAsync();
        break;

    case "run-all":
        if (config.IsProduction) guard.EnsureResetAllowed();
        await runner.ResetAsync();
        await runner.ImportAllAsync(config.SeedDir);
        break;

    case "setup":
        // Production-friendly: import only, no reset, only on empty tables
        await runner.ImportAllAsync(config.SeedDir);
        break;

    case "import":
        var entity = args.Skip(1).FirstOrDefault(a => !a.StartsWith("--"));
        if (entity is null)
        {
            Console.Error.WriteLine("Usage: import <entity>");
            return 1;
        }
        await runner.ImportSingleAsync(config.SeedDir, entity);
        break;

    default:
        Console.Error.WriteLine(
            "Usage: dotnet run [reset|run-all|setup|import <entity>] [options]\n\n" +
            "Commands:\n" +
            "  run-all              Reset + import all CSVs (default)\n" +
            "  setup                Import only (no reset, production-safe)\n" +
            "  reset                Wipe all data, re-seed admin\n" +
            "  import <entity>      Import a single entity CSV\n\n" +
            "Options:\n" +
            "  --env=dev|production Environment mode (default: dev)\n" +
            "  --dir=<path>         CSV files directory (default: ./seed/)\n" +
            "  --connection=<str>   PostgreSQL connection string\n" +
            "  --confirm            Required for production destructive ops\n");
        return 1;
}

return 0;
```

---

### Step 5: Implement `Models.cs`

```csharp
namespace Abuvi.Setup;

public record SeedRowResult(int Row, bool Success, string? Error);

public record SeedResult(
    string Entity,
    int TotalRows,
    int Imported,
    int Skipped,
    IReadOnlyList<SeedRowResult> Rows)
{
    public void Print()
    {
        var color = Skipped > 0 ? ConsoleColor.Yellow : ConsoleColor.Green;
        Console.ForegroundColor = color;
        Console.WriteLine($"  {Entity}: {Imported}/{TotalRows} imported, {Skipped} skipped");
        Console.ResetColor();
        foreach (var row in Rows.Where(r => !r.Success))
            Console.WriteLine($"    Row {row.Row}: {row.Error}");
    }
}
```

---

### Step 6: Implement `CsvHelper.cs`

```csharp
namespace Abuvi.Setup;

public static class CsvHelper
{
    /// <summary>
    /// Reads a CSV file and returns rows as dictionaries (header -> value).
    /// Comma-separated, UTF-8, first row is header.
    /// </summary>
    public static IReadOnlyList<Dictionary<string, string>> Parse(string filePath)
    {
        var lines = File.ReadAllLines(filePath, System.Text.Encoding.UTF8)
            .Where(l => !string.IsNullOrWhiteSpace(l))
            .ToList();

        if (lines.Count < 1)
            return [];

        var headers = lines[0].Split(',').Select(h => h.Trim()).ToArray();
        var rows = new List<Dictionary<string, string>>();

        for (var i = 1; i < lines.Count; i++)
        {
            var values = lines[i].Split(',').Select(v => v.Trim()).ToArray();
            var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            for (var j = 0; j < headers.Length && j < values.Length; j++)
                dict[headers[j]] = values[j];
            rows.Add(dict);
        }

        return rows;
    }

    public static string Require(Dictionary<string, string> row, string key)
    {
        if (row.TryGetValue(key, out var val) && !string.IsNullOrWhiteSpace(val))
            return val;
        throw new InvalidOperationException($"Missing required field: {key}");
    }

    public static string? Optional(Dictionary<string, string> row, string key)
    {
        return row.TryGetValue(key, out var val) && !string.IsNullOrWhiteSpace(val)
            ? val : null;
    }

    public static decimal RequireDecimal(Dictionary<string, string> row, string key)
    {
        var val = Require(row, key);
        return decimal.Parse(val, System.Globalization.CultureInfo.InvariantCulture);
    }

    public static int? OptionalInt(Dictionary<string, string> row, string key)
    {
        var val = Optional(row, key);
        return val is not null ? int.Parse(val) : null;
    }
}
```

---

### Step 7: Implement `SeedRunner.cs`

```csharp
namespace Abuvi.Setup;

using Abuvi.API.Data;
using Abuvi.Setup.Importers;
using Microsoft.EntityFrameworkCore;

public class SeedRunner(AbuviDbContext db, SafetyGuard guard, SetupConfig config)
{
    private static readonly Guid AdminId = new("00000000-0000-0000-0000-000000000001");

    public async Task ResetAsync()
    {
        Console.WriteLine("Resetting database...");

        // Delete in FK-safe order (children before parents)
        await db.Payments.ExecuteDeleteAsync();
        await db.RegistrationExtras.ExecuteDeleteAsync();
        await db.RegistrationMembers.ExecuteDeleteAsync();
        await db.Registrations.ExecuteDeleteAsync();
        await db.CampEditionExtras.ExecuteDeleteAsync();
        await db.CampEditions.ExecuteDeleteAsync();
        await db.CampPhotos.ExecuteDeleteAsync();
        await db.Camps.ExecuteDeleteAsync();
        await db.MembershipFees.ExecuteDeleteAsync();
        await db.Memberships.ExecuteDeleteAsync();
        await db.Guests.ExecuteDeleteAsync();
        await db.FamilyMembers.ExecuteDeleteAsync();
        await db.FamilyUnits.ExecuteDeleteAsync();
        await db.UserRoleChangeLogs.ExecuteDeleteAsync();
        await db.Users.Where(u => u.Id != AdminId).ExecuteDeleteAsync();

        // Re-seed admin if missing
        if (!await db.Users.AnyAsync(u => u.Id == AdminId))
        {
            db.Users.Add(new Abuvi.API.Features.Users.User
            {
                Id = AdminId,
                Email = "admin@abuvi.local",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("Admin@123456", workFactor: 12),
                FirstName = "System",
                LastName = "Administrator",
                Role = Abuvi.API.Features.Users.UserRole.Admin,
                IsActive = true,
                EmailVerified = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });
            await db.SaveChangesAsync();
        }

        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine("Database reset completed.\n");
        Console.ResetColor();
    }

    public async Task ImportAllAsync(string seedDir)
    {
        Console.WriteLine($"Importing from: {seedDir}\n");

        // Strict dependency order
        var importers = new (string file, string entity, string guardKey,
            Func<string, Task<SeedResult>> import)[]
        {
            ("users.csv",          "Users",         "users",
                f => new UserImporter(db).ImportAsync(f)),
            ("family-units.csv",   "FamilyUnits",   "familyunits",
                f => new FamilyUnitImporter(db).ImportAsync(f)),
            ("family-members.csv", "FamilyMembers", "familymembers",
                f => new FamilyMemberImporter(db).ImportAsync(f)),
            ("camps.csv",          "Camps",          "camps",
                f => new CampImporter(db).ImportAsync(f)),
            ("camp-editions.csv",  "CampEditions",   "campeditions",
                f => new CampEditionImporter(db).ImportAsync(f)),
        };

        foreach (var (file, entity, guardKey, import) in importers)
        {
            var path = Path.Combine(seedDir, file);
            if (!File.Exists(path))
            {
                Console.WriteLine($"  {entity}: skipped (file not found: {file})");
                continue;
            }

            // Production safety: block import if table already has data
            if (!await guard.EnsureImportAllowed(guardKey))
                continue;

            var result = await import(path);
            result.Print();
        }

        Console.WriteLine("\nSetup complete.");
    }

    public async Task ImportSingleAsync(string seedDir, string entity)
    {
        var (file, guardKey) = entity.ToLowerInvariant() switch
        {
            "users"          => ("users.csv", "users"),
            "family-units"   => ("family-units.csv", "familyunits"),
            "family-members" => ("family-members.csv", "familymembers"),
            "camps"          => ("camps.csv", "camps"),
            "camp-editions"  => ("camp-editions.csv", "campeditions"),
            _ => throw new ArgumentException($"Unknown entity: {entity}")
        };

        var path = Path.Combine(seedDir, file);
        if (!File.Exists(path))
        {
            Console.Error.WriteLine($"File not found: {path}");
            return;
        }

        // Production safety check
        if (!await guard.EnsureImportAllowed(guardKey))
            return;

        var result = entity.ToLowerInvariant() switch
        {
            "users"          => await new UserImporter(db).ImportAsync(path),
            "family-units"   => await new FamilyUnitImporter(db).ImportAsync(path),
            "family-members" => await new FamilyMemberImporter(db).ImportAsync(path),
            "camps"          => await new CampImporter(db).ImportAsync(path),
            "camp-editions"  => await new CampEditionImporter(db).ImportAsync(path),
            _ => throw new ArgumentException($"Unknown entity: {entity}")
        };

        result.Print();
    }
}
```

---

### Step 8: Implement importers

Each importer follows the same pattern:

1. Parse CSV via `CsvHelper.Parse(filePath)`
2. For each row: validate required fields, check duplicates in DB, create entity, `db.Add()`
3. `db.SaveChangesAsync()` per row (so partial imports survive errors)
4. Return `SeedResult` with per-row details

#### `UserImporter.cs` (representative example)

```csharp
namespace Abuvi.Setup.Importers;

using Abuvi.API.Data;
using Abuvi.API.Features.Users;
using Microsoft.EntityFrameworkCore;

public class UserImporter(AbuviDbContext db)
{
    public async Task<SeedResult> ImportAsync(string filePath)
    {
        var rows = CsvHelper.Parse(filePath);
        var results = new List<SeedRowResult>();
        var imported = 0;

        for (var i = 0; i < rows.Count; i++)
        {
            try
            {
                var r = rows[i];
                var email = CsvHelper.Require(r, "email").ToLowerInvariant();

                // Duplicate check
                if (await db.Users.AnyAsync(u => u.Email == email))
                {
                    results.Add(new(i + 1, false, $"Duplicate email: {email}"));
                    continue;
                }

                var user = new User
                {
                    Id = Guid.NewGuid(),
                    Email = email,
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword(
                        CsvHelper.Require(r, "password"), workFactor: 12),
                    FirstName = CsvHelper.Require(r, "firstName"),
                    LastName = CsvHelper.Require(r, "lastName"),
                    Phone = CsvHelper.Optional(r, "phone"),
                    DocumentNumber = CsvHelper.Optional(r, "documentNumber"),
                    Role = Enum.Parse<UserRole>(
                        CsvHelper.Require(r, "role"), ignoreCase: true),
                    IsActive = true,
                    EmailVerified = true,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                db.Users.Add(user);
                await db.SaveChangesAsync();
                imported++;
                results.Add(new(i + 1, true, null));
            }
            catch (Exception ex)
            {
                results.Add(new(i + 1, false, ex.Message));
            }
        }

        return new("Users", rows.Count, imported, rows.Count - imported, results);
    }
}
```

#### `FamilyUnitImporter.cs` — resolves representative by email lookup

```csharp
// Key logic: resolve representativeEmail -> User.Id
var email = CsvHelper.Require(r, "representativeEmail").ToLowerInvariant();
var user = await db.Users.FirstOrDefaultAsync(u => u.Email == email);
if (user is null) { /* skip row with error */ }

var unit = new FamilyUnit
{
    Id = Guid.NewGuid(),
    Name = CsvHelper.Require(r, "name"),
    RepresentativeUserId = user.Id,
    CreatedAt = DateTime.UtcNow,
    UpdatedAt = DateTime.UtcNow
};

// Also update user.FamilyUnitId to link back
user.FamilyUnitId = unit.Id;
```

#### `FamilyMemberImporter.cs` — resolves family unit by name

```csharp
// Key logic: resolve familyUnitName -> FamilyUnit.Id
var unitName = CsvHelper.Require(r, "familyUnitName");
var unit = await db.FamilyUnits.FirstOrDefaultAsync(
    u => u.Name.ToLower() == unitName.ToLower());
if (unit is null) { /* skip row with error */ }

var member = new FamilyMember
{
    Id = Guid.NewGuid(),
    FamilyUnitId = unit.Id,
    FirstName = CsvHelper.Require(r, "firstName"),
    LastName = CsvHelper.Require(r, "lastName"),
    DateOfBirth = DateOnly.Parse(CsvHelper.Require(r, "dateOfBirth")),
    Relationship = Enum.Parse<FamilyRelationship>(
        CsvHelper.Require(r, "relationship"), ignoreCase: true),
    DocumentNumber = CsvHelper.Optional(r, "documentNumber"),
    Email = CsvHelper.Optional(r, "email"),
    Phone = CsvHelper.Optional(r, "phone"),
    CreatedAt = DateTime.UtcNow,
    UpdatedAt = DateTime.UtcNow
};
```

#### `CampImporter.cs` — direct insert

```csharp
var camp = new Camp
{
    Id = Guid.NewGuid(),
    Name = CsvHelper.Require(r, "name"),
    Description = CsvHelper.Optional(r, "description"),
    Location = CsvHelper.Optional(r, "location"),
    PricePerAdult = CsvHelper.RequireDecimal(r, "pricePerAdult"),
    PricePerChild = CsvHelper.RequireDecimal(r, "pricePerChild"),
    PricePerBaby = CsvHelper.RequireDecimal(r, "pricePerBaby"),
    IsActive = true,
    CreatedAt = DateTime.UtcNow,
    UpdatedAt = DateTime.UtcNow
};
```

#### `CampEditionImporter.cs` — resolves camp by name, sets status directly

```csharp
// Key logic: resolve campName -> Camp.Id, set status directly (bypass workflow)
var campName = CsvHelper.Require(r, "campName");
var camp = await db.Camps.FirstOrDefaultAsync(
    c => c.Name.ToLower() == campName.ToLower());
if (camp is null) { /* skip row with error */ }

var edition = new CampEdition
{
    Id = Guid.NewGuid(),
    CampId = camp.Id,
    Year = int.Parse(CsvHelper.Require(r, "year")),
    StartDate = DateTime.Parse(CsvHelper.Require(r, "startDate")),
    EndDate = DateTime.Parse(CsvHelper.Require(r, "endDate")),
    PricePerAdult = CsvHelper.RequireDecimal(r, "pricePerAdult"),
    PricePerChild = CsvHelper.RequireDecimal(r, "pricePerChild"),
    PricePerBaby = CsvHelper.RequireDecimal(r, "pricePerBaby"),
    MaxCapacity = CsvHelper.OptionalInt(r, "maxCapacity"),
    Status = Enum.Parse<CampEditionStatus>(
        CsvHelper.Require(r, "status"), ignoreCase: true),
    Notes = CsvHelper.Optional(r, "notes"),
    CreatedAt = DateTime.UtcNow,
    UpdatedAt = DateTime.UtcNow
};
```

> **Note:** Status is set directly on the entity — no workflow validation. This is a setup tool, not a business operation.

---

### Step 9 (TDD): Write unit tests

Tests mock `AbuviDbContext` using InMemory provider (acceptable here since the console app only does simple CRUD — no Npgsql-specific features).

**Test files:**

| File | Key test cases |
|------|---------------|
| `SeedRunnerTests.cs` | `ResetAsync_DeletesInCorrectOrder`, `ImportAllAsync_SkipsMissingFiles` |
| `SafetyGuardTests.cs` | `EnsureResetAllowed_InProduction_WithoutConfirm_Exits`, `EnsureImportAllowed_InProduction_WithData_ReturnsFalse`, `EnsureImportAllowed_InDev_AlwaysReturnsTrue` |
| `CsvHelperTests.cs` | `Parse_ValidFile_ReturnsRows`, `Parse_EmptyFile_ReturnsEmpty`, `Require_MissingField_Throws` |
| `UserImporterTests.cs` | `ImportAsync_ValidCsv_CreatesUsers`, `ImportAsync_DuplicateEmail_Skips` |

---

## 5. CSV File Formats

All files: **comma separator**, **UTF-8**, **first row = header**, **ISO 8601 dates** (`YYYY-MM-DD`).
Leave column empty for optional fields.

### `users.csv`

```csv
email,password,firstName,lastName,phone,role,documentNumber
board@abuvi.local,Board@123456,Ana,Lopez,+34612345678,Board,12345678A
member1@abuvi.local,Member@123456,Carlos,Garcia,+34698765432,Member,87654321B
member2@abuvi.local,Member@123456,Laura,Martin,,Member,
```

### `family-units.csv`

```csv
name,representativeEmail
Garcia Family,member1@abuvi.local
Lopez Family,board@abuvi.local
Martin Family,member2@abuvi.local
```

### `family-members.csv`

```csv
familyUnitName,firstName,lastName,dateOfBirth,relationship,documentNumber,email,phone
Garcia Family,Carlos,Garcia,1982-03-15,Parent,87654321B,member1@abuvi.local,+34698765432
Garcia Family,Laura,Garcia,1985-07-22,Spouse,,laura@example.com,
Garcia Family,Pablo,Garcia,2010-11-05,Child,,,
Garcia Family,Sofia,Garcia,2014-04-18,Child,,,
Lopez Family,Ana,Lopez,1979-01-30,Parent,12345678A,board@abuvi.local,+34612345678
```

### `camps.csv`

```csv
name,description,location,pricePerAdult,pricePerChild,pricePerBaby
Camp Sierra,Annual camp in the mountains,Sierra de Guadarrama,150.00,100.00,0.00
Camp Costa,Summer coastal camp,Costa Brava,180.00,120.00,0.00
```

### `camp-editions.csv`

```csv
campName,year,startDate,endDate,pricePerAdult,pricePerChild,pricePerBaby,maxCapacity,status,notes
Camp Sierra,2027,2027-07-01,2027-07-15,150.00,100.00,0.00,100,Draft,Test edition
Camp Costa,2027,2027-08-01,2027-08-10,180.00,120.00,0.00,,Open,Summer edition
```

---

## 6. Usage Guide

### Development — quick iteration

```bash
# Full reset + seed (default: --env=dev)
dotnet run --project src/Abuvi.Setup

# Reset only
dotnet run --project src/Abuvi.Setup reset

# Import a single entity (no reset)
dotnet run --project src/Abuvi.Setup import users

# Custom CSV directory
dotnet run --project src/Abuvi.Setup run-all --dir=./my-test-data/
```

### Production — initial setup (run once)

```bash
# Safe import: only inserts on empty tables, no reset
dotnet run --project src/Abuvi.Setup setup \
  --env=production \
  --connection="Host=prod-host;Database=abuvi;..." \
  --dir=./production-data/

# Full reset + re-seed (requires double confirmation)
dotnet run --project src/Abuvi.Setup run-all \
  --env=production \
  --confirm \
  --connection="Host=prod-host;Database=abuvi;..."
```

### Expected output

```
=== Abuvi Setup Tool [Dev] ===

Connected to database.

Resetting database...
Database reset completed.

Importing from: ./seed/

  Users: 3/3 imported, 0 skipped
  FamilyUnits: 3/3 imported, 0 skipped
  FamilyMembers: 5/5 imported, 0 skipped
  Camps: 2/2 imported, 0 skipped
  CampEditions: 2/2 imported, 0 skipped

Setup complete.
```

### Production blocked output (tables not empty)

```
=== Abuvi Setup Tool [Production] ===

Connected to database.

Importing from: ./production-data/

  Users: BLOCKED — table already has data (production mode).
  Use 'reset' first or switch to --env=dev for incremental imports.
```

---

## 7. Commands Summary

| Command | Description | Dev | Production |
|---------|-------------|-----|------------|
| `run-all` | Reset + import all CSVs (default) | Free | Requires `--confirm` + interactive "YES" |
| `setup` | Import only (no reset) | Skips duplicates | Only on empty tables |
| `reset` | Wipe all data, re-seed admin | Free | Requires `--confirm` + interactive "YES" |
| `import <entity>` | Import a single CSV | Skips duplicates | Only on empty table |

---

## 8. Security & Constraints

- The console app is a **separate project** — it is never deployed as part of the API.
- Production mode requires **`--confirm` flag + interactive "YES" confirmation** for destructive operations.
- Production `setup` command **refuses to import into tables that already have data** — prevents accidental duplicates.
- Passwords in CSV are plain text; BCrypt-hashed (cost 12) before insertion.
- `medicalNotes` and `allergies` are **excluded** — sensitive fields must be entered through the API.
- `CampEdition.Status` is set directly (no workflow transitions) since this is a setup tool.
- Default connection string targets `localhost` — production requires explicit `--connection` or `DATABASE_URL`.

---

## 9. Pre-submission Checklist

- [ ] Console app compiles: `dotnet build src/Abuvi.Setup`
- [ ] Unit tests pass: `dotnet test`
- [ ] `run-all` works end-to-end with sample CSVs in dev mode
- [ ] `setup` correctly blocks on non-empty tables in production mode
- [ ] `reset` in production requires `--confirm` + interactive "YES"
- [ ] `reset` properly wipes all data and preserves admin user
- [ ] Individual `import` commands work without resetting
- [ ] Skipped rows display clear error messages
- [ ] No sensitive/real data committed to `seed/` directory (fake data only)
