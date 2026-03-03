# Setup Tool: Serilog Logging & Dry-Run Mode

## Task 1: Configure Serilog Logging with File Output

### User Story

**As a** developer/operator running the Setup tool,
**I want** structured logging via Serilog that writes to both console and log files,
**so that** I can review import results and troubleshoot issues after execution without relying solely on terminal scrollback.

### Acceptance Criteria

1. Serilog is configured as the logging provider for Abuvi.Setup
2. Logs are written to **two sinks simultaneously**:
   - **Console**: Colored, human-readable output (replaces current `Console.WriteLine` calls)
   - **File**: Structured logs written to `logs/setup-{Date}.log` relative to the executable directory
3. File logs use **rolling daily files** with a **31-day retention** policy
4. Log levels are used consistently:
   - `Information` — Banner, command selected, import start/complete, row counts
   - `Warning` — Row skipped (duplicate, missing FK reference), file not found
   - `Error` — Connection failure, parse errors, unexpected exceptions
   - `Debug` — Individual row details (parsed values, entity created)
5. All existing `Console.WriteLine` / `Console.Error.WriteLine` calls are replaced with `ILogger` calls
6. Console colored output is preserved via Serilog's console theme (e.g., `AnsiConsoleTheme.Code`)
7. Minimum log level configurable via environment variable: `SETUP_LOG_LEVEL` (default: `Information`)

### Technical Details

#### New Dependencies (Abuvi.Setup.csproj)

```xml
<PackageReference Include="Serilog" Version="4.*" />
<PackageReference Include="Serilog.Sinks.Console" Version="6.*" />
<PackageReference Include="Serilog.Sinks.File" Version="6.*" />
```

> **Note:** This is a standalone console app — no need for `Serilog.AspNetCore` or `ReadFrom.Configuration()`. Configure Serilog programmatically in `Program.cs`.

#### Files to Modify

| File | Changes |
|------|---------|
| `Abuvi.Setup.csproj` | Add Serilog NuGet packages |
| `Program.cs` | Initialize Serilog at startup, replace `Console.Write*` with logger calls, dispose on exit |
| `SeedRunner.cs` | Accept `ILogger` via constructor, replace all `Console.Write*` with structured log calls |
| `SafetyGuard.cs` | Accept `ILogger` via constructor, log safety check decisions. **Keep** `Console.ReadLine()` for interactive confirmation (not replaceable by logging) |
| `Models.cs` | Update `SeedResult.Print()` to accept `ILogger` parameter or replace with a method that uses the logger |
| Each importer (`Importers/*.cs`) | No changes needed — importers already return `SeedResult` and don't do console output directly |

#### Serilog Initialization (Program.cs)

```csharp
var levelSwitch = new LoggingLevelSwitch();
var envLevel = Environment.GetEnvironmentVariable("SETUP_LOG_LEVEL");
if (Enum.TryParse<LogEventLevel>(envLevel, true, out var parsed))
    levelSwitch.MinimumLevel = parsed;

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.ControlledBy(levelSwitch)
    .WriteTo.Console(theme: AnsiConsoleTheme.Code)
    .WriteTo.File(
        path: Path.Combine(AppContext.BaseDirectory, "logs", "setup-.log"),
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 31,
        outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
    .CreateLogger();
```

#### Structured Log Examples

```csharp
// Instead of: Console.WriteLine($"  Connected to database");
Log.Information("Connected to database on {Environment} environment", config.Env);

// Instead of: Console.ForegroundColor = ConsoleColor.Red; Console.Error.WriteLine(...)
Log.Error("Cannot connect to database: {Error}", ex.Message);

// Instead of: Console.WriteLine($"  {Entity}: {Imported}/{Total} imported, {Skipped} skipped");
Log.Information("Import complete for {Entity}: {Imported}/{Total} imported, {Skipped} skipped",
    entity, imported, total, skipped);

// Row-level detail at Debug
Log.Debug("Row {Row}: Created {Entity} with Id {Id}", rowNum, entityName, entity.Id);

// Skip warning
Log.Warning("Row {Row}: Skipped {Entity} — {Reason}", rowNum, entityName, "duplicate email");
```

#### Cleanup on Exit

```csharp
finally
{
    await Log.CloseAndFlushAsync();
}
```

### What NOT to Change

- **Interactive prompts** in `SafetyGuard` (`Console.ReadLine()`) must remain as direct console I/O — these are user interaction, not logging
- Do not add `appsettings.json` — this is a CLI tool, not a web app

### Definition of Done

- [ ] Serilog packages added to csproj
- [ ] Logger initialized in `Program.cs` with console + file sinks
- [ ] All `Console.WriteLine` / `Console.Error.WriteLine` replaced with appropriate `Log.*` calls
- [ ] Log files created under `logs/` directory with daily rolling
- [ ] Interactive prompts (ReadLine) preserved as-is
- [ ] Log level configurable via `SETUP_LOG_LEVEL` env var
- [ ] `logs/` directory added to `.gitignore` if not already present
- [ ] Tool runs successfully with visible console output and file output

---

## Task 2: Dry-Run Mode for Importers

### User Story

**As a** developer/operator,
**I want** a `--dry-run` flag that executes the full import pipeline (parse, validate, map to entities) without persisting to the database,
**so that** I can preview what would happen and catch errors before committing real data — enabling safe, repeatable test runs.

### Acceptance Criteria

1. A new CLI flag `--dry-run` is available
2. When `--dry-run` is active:
   - CSV files are parsed normally
   - All validation runs (required fields, enum parsing, decimal parsing)
   - FK lookups are attempted (user by email, family unit by name, camp by name)
   - Duplicate detection runs against existing DB data
   - **No `SaveChangesAsync()` is called** — no data is written to the database
   - A **dry-run report** is printed to console (and logged to file if Task 1 is implemented)
3. The report includes:
   - Total rows processed per entity
   - Rows that **would be imported** (with key identifying fields)
   - Rows that **would be skipped** (with reason: duplicate, missing FK, etc.)
   - Rows that **would fail** (with parse/validation error details)
   - A clear summary at the end
4. The tool exits with code **0** even if rows would fail (it's a report, not an action)
5. Dry-run works with all commands: `run-all`, `setup`, `import <entity>`
6. Dry-run **skips the `reset` step** in `run-all` (never deletes data in dry-run)
7. A banner clearly indicates dry-run mode is active

### Technical Details

#### CLI Flag Parsing (SetupConfig.cs)

Add a new property:

```csharp
public bool DryRun { get; init; }
```

Parse from args:

```csharp
DryRun = args.Any(a => a == "--dry-run")
```

#### Importer Changes Strategy

**Option chosen: Transaction rollback approach**

Wrap the entire import in a transaction that is always rolled back in dry-run mode. This approach:
- Requires **minimal changes** to existing importers
- Validates FK lookups correctly (since prior rows in the same transaction are visible)
- Ensures zero side-effects

#### SeedRunner.cs Changes

```csharp
public class SeedRunner
{
    private readonly AbuviDbContext _db;
    private readonly SafetyGuard _guard;
    private readonly bool _dryRun;

    public SeedRunner(AbuviDbContext db, SafetyGuard guard, bool dryRun = false)
    {
        _db = db;
        _guard = guard;
        _dryRun = dryRun;
    }
}
```

**In `ImportAllAsync` and `ImportSingleAsync`:**

```csharp
if (_dryRun)
{
    await using var transaction = await _db.Database.BeginTransactionAsync();
    try
    {
        // Run all importers normally (they call SaveChangesAsync per row)
        // ... existing import logic ...

        // Print dry-run report
        PrintDryRunReport(results);
    }
    finally
    {
        // Always rollback — nothing persists
        await transaction.RollbackAsync();
    }
}
else
{
    // Existing behavior unchanged
}
```

**In `ResetAsync`:**

```csharp
public async Task ResetAsync()
{
    if (_dryRun)
    {
        Log.Warning("Dry-run mode: Skipping database reset");
        return;
    }
    // ... existing reset logic ...
}
```

#### Program.cs Changes

```csharp
// After config parsing:
if (config.DryRun)
{
    Log.Information("=== DRY-RUN MODE — No changes will be saved ===");
}

var runner = new SeedRunner(db, guard, config.DryRun);
```

#### Dry-Run Report Format

```
╔══════════════════════════════════════════╗
║          DRY-RUN REPORT                  ║
╠══════════════════════════════════════════╣
║ Entity: Users                            ║
║   Would import: 3                        ║
║   Would skip:   1 (duplicate)            ║
║   Would fail:   0                        ║
║──────────────────────────────────────────║
║  ✓ Row 1: user@example.com (Member)      ║
║  ⊘ Row 2: admin@abuvi.local — duplicate  ║
║  ✓ Row 3: new@example.com (Board)        ║
╠══════════════════════════════════════════╣
║ Entity: FamilyUnits                      ║
║   Would import: 2                        ║
║   Would skip:   0                        ║
║   Would fail:   1                        ║
║──────────────────────────────────────────║
║  ✓ Row 1: Garcia Family                  ║
║  ✓ Row 2: Lopez Family                   ║
║  ✗ Row 3: Unknown Family — user          ║
║          "missing@email.com" not found    ║
╠══════════════════════════════════════════╣
║ SUMMARY                                  ║
║   Total entities: 5                       ║
║   Total rows:     15                      ║
║   Would import:   12                      ║
║   Would skip:     2                       ║
║   Would fail:     1                       ║
╚══════════════════════════════════════════╝
```

> The report format can be simpler/plainer than this mockup — the key information matters more than exact formatting. Use the existing `SeedResult` model, just label output as "would import" vs "imported".

#### Files to Modify

| File | Changes |
|------|---------|
| `SetupConfig.cs` | Add `DryRun` property, parse `--dry-run` flag |
| `Program.cs` | Pass `dryRun` to `SeedRunner`, show dry-run banner |
| `SeedRunner.cs` | Add `_dryRun` field, wrap imports in transaction + rollback, skip reset in dry-run, add report method |
| `Models.cs` | Optionally add `PrintDryRun()` variant to `SeedResult` that uses "would" language |
| `docs/setup-tool-deployment.md` | Document `--dry-run` flag in CLI reference |

#### Safety Interactions

- `--dry-run` **bypasses SafetyGuard** checks — since no data is written, production guards are unnecessary
- `--dry-run` does **not** require `--confirm` even in production mode

### What NOT to Change

- Importer logic remains unchanged — they still call `SaveChangesAsync()` as normal, the transaction rollback handles the "undo"
- Do not add a separate "validation-only" code path in each importer — keep it DRY

### Definition of Done

- [ ] `--dry-run` flag parsed in `SetupConfig`
- [ ] Dry-run banner displayed when flag is active
- [ ] Import runs inside a transaction that is rolled back
- [ ] Reset is skipped in dry-run mode
- [ ] SafetyGuard is bypassed in dry-run mode
- [ ] Dry-run report printed with would-import / would-skip / would-fail counts
- [ ] Exit code is 0 regardless of row failures in dry-run
- [ ] `docs/setup-tool-deployment.md` updated with `--dry-run` documentation
- [ ] Tool can be run N times with `--dry-run` with zero side effects

---

## Implementation Order

1. **Task 1 first** (Serilog) — so that Task 2's report output goes through the logger from the start
2. **Task 2 second** (Dry-run) — builds on the logging infrastructure

## CLI Usage Examples After Both Tasks

```bash
# Normal import (existing behavior, now with file logging)
dotnet run -- setup --connection="Host=..." --env=dev

# Dry-run in dev (preview what would happen)
dotnet run -- setup --connection="Host=..." --dry-run

# Dry-run against production (safe, no --confirm needed)
dotnet run -- setup --connection="Host=..." --env=production --dry-run

# Dry-run a single entity
dotnet run -- import users --connection="Host=..." --dry-run

# Verbose dry-run (see every row detail)
SETUP_LOG_LEVEL=Debug dotnet run -- setup --dry-run

# Check log files after run
cat logs/setup-20260303.log
```
