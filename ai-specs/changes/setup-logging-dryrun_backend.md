# Backend Implementation Plan: setup-logging-dryrun

## Overview

Two enhancements to the `Abuvi.Setup` console tool:

1. **Serilog Logging** — Replace all `Console.Write*` calls with structured Serilog logging (console + file sinks)
2. **Dry-Run Mode** — Add a `--dry-run` flag that runs the full import pipeline inside a transaction that is always rolled back, producing a report without side effects

This is a standalone console app (`src/Abuvi.Setup/`), not the API project. There are no Minimal API endpoints, no DI container, no `appsettings.json`. Serilog is configured programmatically.

## Architecture Context

- **Project**: `src/Abuvi.Setup/` (console app, references `Abuvi.API` for `AbuviDbContext`)
- **Files to modify**: `Abuvi.Setup.csproj`, `Program.cs`, `SetupConfig.cs`, `SafetyGuard.cs`, `SeedRunner.cs`, `Models.cs`
- **Files to update (docs)**: `docs/setup-tool-deployment.md`
- **Files to update (gitignore)**: `.gitignore` (add `logs/` entry)
- **No new feature slices** — this is infrastructure work on an existing CLI tool

## Implementation Steps

### Step 0: Create Feature Branch

- **Action**: Create and switch to a new feature branch
- **Branch Naming**: `feature/setup-logging-dryrun-backend`
- **Implementation Steps**:
  1. Ensure you're on the latest `dev` branch
  2. Pull latest changes: `git pull origin dev`
  3. Create new branch: `git checkout -b feature/setup-logging-dryrun-backend`
  4. Verify branch creation: `git branch`

---

### Step 1: Add Serilog NuGet Packages

- **File**: `src/Abuvi.Setup/Abuvi.Setup.csproj`
- **Action**: Add Serilog dependencies
- **Implementation Steps**:
  1. Add the following PackageReferences to the `<ItemGroup>` that contains `BCrypt.Net-Next`:
     ```xml
     <PackageReference Include="Serilog" Version="4.*" />
     <PackageReference Include="Serilog.Sinks.Console" Version="6.*" />
     <PackageReference Include="Serilog.Sinks.File" Version="6.*" />
     ```
  2. Run `dotnet restore src/Abuvi.Setup` to verify packages resolve

---

### Step 2: Add `DryRun` to SetupConfig

- **File**: `src/Abuvi.Setup/SetupConfig.cs`
- **Action**: Parse `--dry-run` from CLI args
- **Implementation Steps**:
  1. Add property to `SetupConfig`:
     ```csharp
     public bool DryRun { get; init; }
     ```
  2. In `Parse()`, add parsing:
     ```csharp
     var dryRun = args.Contains("--dry-run");
     ```
  3. Add `DryRun = dryRun` to the returned object initializer

---

### Step 3: Initialize Serilog in Program.cs

- **File**: `src/Abuvi.Setup/Program.cs`
- **Action**: Replace the top-level Console output with Serilog. Wrap the entire body in try/finally for cleanup.
- **Implementation Steps**:
  1. Add `using Serilog;` and `using Serilog.Events;` and `using Serilog.Sinks.SystemConsole.Themes;` at the top
  2. Right after `var config = SetupConfig.Parse(args);`, initialize Serilog:
     ```csharp
     var minLevel = LogEventLevel.Information;
     var envLevel = Environment.GetEnvironmentVariable("SETUP_LOG_LEVEL");
     if (Enum.TryParse<LogEventLevel>(envLevel, true, out var parsed))
         minLevel = parsed;

     Log.Logger = new LoggerConfiguration()
         .MinimumLevel.Is(minLevel)
         .WriteTo.Console(theme: AnsiConsoleTheme.Code)
         .WriteTo.File(
             path: Path.Combine(AppContext.BaseDirectory, "logs", "setup-.log"),
             rollingInterval: RollingInterval.Day,
             retainedFileCountLimit: 31,
             outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
         .CreateLogger();
     ```
  3. Wrap everything after logger init in `try { ... } finally { await Log.CloseAndFlushAsync(); }`
  4. Replace all `Console.Write*` calls in Program.cs:

     | Before | After |
     |--------|-------|
     | `Console.ForegroundColor = ...; Console.WriteLine($"=== Abuvi Setup Tool [{config.Env}] ===\n"); Console.ResetColor();` | `Log.Information("=== Abuvi Setup Tool [{Environment}] ===", config.Env);` |
     | `Console.WriteLine($"Connected to database.\n");` | `Log.Information("Connected to database");` |
     | `Console.ForegroundColor = ConsoleColor.Red; Console.Error.WriteLine($"Cannot connect to database: {ex.Message}"); Console.ResetColor();` | `Log.Error("Cannot connect to database: {Error}", ex.Message);` |
     | `Console.Error.WriteLine("Usage: import <entity>");` | `Log.Error("Usage: import <entity>");` |
     | The entire `default:` usage block | `Log.Error("Unknown command: {Command}. ...", command);` — keep the full usage text as the message |

  5. Add dry-run banner after config parsing:
     ```csharp
     if (config.DryRun)
         Log.Information("=== DRY-RUN MODE — No changes will be saved ===");
     ```
  6. Pass `config.DryRun` to `SeedRunner` constructor:
     ```csharp
     var runner = new SeedRunner(db, guard, config.DryRun);
     ```
  7. In the `run-all` case, skip reset when dry-run:
     ```csharp
     case "run-all":
         if (!config.DryRun)
         {
             if (config.IsProduction && !guard.EnsureResetAllowed()) return 1;
             await runner.ResetAsync();
         }
         else
         {
             Log.Warning("Dry-run mode: Skipping database reset");
         }
         await runner.ImportAllAsync(config.SeedDir);
         break;
     ```
  8. In the `reset` case, skip when dry-run:
     ```csharp
     case "reset":
         if (config.DryRun)
         {
             Log.Warning("Dry-run mode: Reset has no effect");
             break;
         }
         if (!guard.EnsureResetAllowed()) return 1;
         await runner.ResetAsync();
         break;
     ```
  9. For dry-run, always return 0 at the end (already does by default)

---

### Step 4: Update SafetyGuard to Use Serilog

- **File**: `src/Abuvi.Setup/SafetyGuard.cs`
- **Action**: Replace `Console.Write*` with `Log.*` calls. Keep `Console.Write` and `Console.ReadLine()` ONLY for the interactive confirmation prompt.
- **Implementation Steps**:
  1. Add `using Serilog;` at the top
  2. In `EnsureResetAllowed()`:
     - Replace the "PRODUCTION RESET BLOCKED" error writes with:
       ```csharp
       Log.Error("PRODUCTION RESET BLOCKED: this will DELETE ALL DATA");
       Log.Error("Add --confirm flag to proceed: dotnet run reset --env=production --confirm");
       ```
     - **Keep** `Console.Write("You are about to RESET...")` and `Console.ReadLine()` as-is (interactive prompt cannot go through logger)
     - Replace `Console.Error.WriteLine("Aborted.")` with `Log.Warning("Reset aborted by user");`
  3. In `EnsureImportAllowedAsync()`:
     - Replace the blocked message with:
       ```csharp
       Log.Error("{Entity}: BLOCKED — table already has data (production mode)", entity);
       Log.Error("Use 'reset' first or switch to --env=dev for incremental imports");
       ```
  4. Add dry-run bypass: when `config.DryRun` is true, both methods should return `true` without checking. Add at the beginning of each method:
     ```csharp
     if (config.DryRun) return true; // No data will be written
     ```
     For `EnsureImportAllowedAsync`, this means it returns `true` immediately.
     For `EnsureResetAllowed`, same — but `ResetAsync` itself is already skipped in Program.cs.

  **Note**: `SetupConfig` is already available via the primary constructor parameter.

---

### Step 5: Update SeedRunner to Use Serilog + Dry-Run Transaction

- **File**: `src/Abuvi.Setup/SeedRunner.cs`
- **Action**: Add `_dryRun` field, replace Console output with Serilog, wrap imports in transaction when dry-run
- **Implementation Steps**:
  1. Add `using Serilog;` at top
  2. Change the primary constructor to accept `bool dryRun = false`:
     ```csharp
     public class SeedRunner(AbuviDbContext db, SafetyGuard guard, bool dryRun = false)
     ```
  3. In `ResetAsync()`:
     - Replace `Console.WriteLine("Resetting database...");` → `Log.Information("Resetting database...");`
     - Replace the green "completed" block → `Log.Information("Database reset completed");`
  4. In `ImportAllAsync(string seedDir)`:
     - Replace `Console.WriteLine($"Importing from: {seedDir}\n");` → `Log.Information("Importing from: {SeedDir}", seedDir);`
     - Replace `Console.WriteLine($"  {entity}: skipped (file not found: {file})");` → `Log.Warning("{Entity}: skipped (file not found: {File})", entity, file);`
     - Replace `Console.WriteLine("\nSetup complete.");` → `Log.Information("Setup complete");`
     - **Dry-run wrapping**: Wrap the import loop in a transaction when `dryRun` is true:
       ```csharp
       IDbContextTransaction? transaction = null;
       if (dryRun)
           transaction = await db.Database.BeginTransactionAsync();

       try
       {
           var results = new List<SeedResult>();
           foreach (var (file, entity, guardKey, import) in importers)
           {
               // ... existing logic (check file, guard, import, print) ...
               var result = await import(path);
               results.Add(result);
               result.Print(dryRun);
           }

           if (dryRun)
               PrintDryRunSummary(results);
           else
               Log.Information("Setup complete");
       }
       finally
       {
           if (transaction != null)
           {
               await transaction.RollbackAsync();
               await transaction.DisposeAsync();
               Log.Information("Dry-run complete — all changes rolled back");
           }
       }
       ```
     - Add `using Microsoft.EntityFrameworkCore.Storage;` for `IDbContextTransaction`
  5. In `ImportSingleAsync(string seedDir, string entity)`:
     - Replace `Console.Error.WriteLine($"File not found: {path}");` → `Log.Error("File not found: {Path}", path);`
     - Apply same dry-run transaction pattern:
       ```csharp
       IDbContextTransaction? transaction = null;
       if (dryRun)
           transaction = await db.Database.BeginTransactionAsync();

       try
       {
           var result = /* ... existing switch ... */;
           result.Print(dryRun);

           if (dryRun)
               PrintDryRunSummary([result]);
       }
       finally
       {
           if (transaction != null)
           {
               await transaction.RollbackAsync();
               await transaction.DisposeAsync();
               Log.Information("Dry-run complete — all changes rolled back");
           }
       }
       ```
  6. Add `PrintDryRunSummary` private method:
     ```csharp
     private static void PrintDryRunSummary(List<SeedResult> results)
     {
         Log.Information("=== DRY-RUN SUMMARY ===");
         var totalRows = results.Sum(r => r.TotalRows);
         var totalImported = results.Sum(r => r.Imported);
         var totalSkipped = results.Sum(r => r.Skipped);
         var totalFailed = results.Sum(r => r.Rows.Count(row => !row.Success));

         Log.Information("Total entities: {Count}", results.Count);
         Log.Information("Total rows: {Total}", totalRows);
         Log.Information("Would import: {Imported}", totalImported);
         Log.Information("Would skip: {Skipped}", totalSkipped);
         Log.Information("Would fail: {Failed}", totalFailed);
         Log.Information("=======================");
     }
     ```

---

### Step 6: Update Models.cs (SeedResult.Print)

- **File**: `src/Abuvi.Setup/Models.cs`
- **Action**: Replace Console output with Serilog, add dry-run awareness
- **Implementation Steps**:
  1. Add `using Serilog;`
  2. Change `Print()` to accept an optional `bool dryRun = false` parameter:
     ```csharp
     public void Print(bool dryRun = false)
     {
         var verb = dryRun ? "would import" : "imported";
         var skipVerb = dryRun ? "would skip" : "skipped";

         if (Skipped > 0)
             Log.Warning("{Entity}: {Imported}/{Total} {Verb}, {Skipped} {SkipVerb}",
                 Entity, Imported, TotalRows, verb, Skipped, skipVerb);
         else
             Log.Information("{Entity}: {Imported}/{Total} {Verb}, {Skipped} {SkipVerb}",
                 Entity, Imported, TotalRows, verb, Skipped, skipVerb);

         foreach (var row in Rows.Where(r => !r.Success))
             Log.Warning("  Row {Row}: {Error}", row.Row, row.Error);
     }
     ```

---

### Step 7: Add `logs/` to .gitignore

- **File**: `.gitignore`
- **Action**: Ensure log files are not committed
- **Implementation Steps**:
  1. Add at the end of `.gitignore`:
     ```
     # Setup tool logs
     logs/
     ```

---

### Step 8: Update Documentation

- **File**: `docs/setup-tool-deployment.md`
- **Action**: Document `--dry-run` flag and logging
- **Implementation Steps**:
  1. Add a new section **"7. Logging"** after section 6:
     ```markdown
     ## 7. Logging

     The setup tool writes structured logs to two destinations:

     - **Console**: Colored output (same as before, now via Serilog)
     - **File**: `logs/setup-YYYYMMDD.log` in the tool's directory (daily rolling, 31-day retention)

     ### Changing log level

     Set the `SETUP_LOG_LEVEL` environment variable before running:

     ```bash
     export SETUP_LOG_LEVEL=Debug
     ./Abuvi.Setup setup --env=dev
     ```

     Valid levels: `Verbose`, `Debug`, `Information` (default), `Warning`, `Error`, `Fatal`
     ```

  2. Add `--dry-run` to the commands summary table (section 6):

     | Command | Description | Dev | Production |
     |---------|-------------|-----|------------|
     | (existing rows) | ... | ... | ... |

     Add a new row or note under the table:
     ```markdown
     ### Global flags

     | Flag | Description |
     |------|-------------|
     | `--dry-run` | Run full pipeline (parse, validate, map) without saving. Prints a report of what would happen. Safe for repeated use. |
     ```

  3. Add a dry-run usage example:
     ```markdown
     ## 8. Dry-run mode

     Preview what the import would do without writing to the database:

     ```bash
     ./Abuvi.Setup setup \
       --env=production \
       --connection="Host=...;Database=...;Username=...;Password=..." \
       --dir=./seed/ \
       --dry-run
     ```

     - Parses all CSV files and validates every row
     - Checks for duplicates and resolves FK references
     - Reports what would be imported, skipped, or fail
     - **No data is written** — uses a transaction that is always rolled back
     - Does not require `--confirm` even in production mode
     - Exit code is always 0 (it's a report, not an action)
     ```

---

## Implementation Order

1. **Step 0**: Create feature branch
2. **Step 1**: Add Serilog NuGet packages
3. **Step 2**: Add `DryRun` to `SetupConfig`
4. **Step 3**: Initialize Serilog in `Program.cs` + dry-run logic in command dispatch
5. **Step 4**: Update `SafetyGuard` to use Serilog + dry-run bypass
6. **Step 5**: Update `SeedRunner` with Serilog + dry-run transaction wrapping
7. **Step 6**: Update `Models.cs` (`SeedResult.Print`)
8. **Step 7**: Add `logs/` to `.gitignore`
9. **Step 8**: Update documentation

## Testing Checklist

Since this is a CLI tool without a test project currently, verify manually:

- [ ] `dotnet build src/Abuvi.Setup` compiles without errors
- [ ] `dotnet run --project src/Abuvi.Setup -- setup --env=dev` produces colored console output via Serilog
- [ ] After running, `logs/setup-{date}.log` file exists with structured log entries
- [ ] `dotnet run --project src/Abuvi.Setup -- setup --dry-run` runs without writing data:
  - Verify with a `SELECT count(*) FROM "Users"` before and after — counts should be identical
- [ ] `dotnet run --project src/Abuvi.Setup -- run-all --dry-run` skips the reset step (logged warning visible)
- [ ] `dotnet run --project src/Abuvi.Setup -- reset --dry-run` logs warning and does nothing
- [ ] `dotnet run --project src/Abuvi.Setup -- import users --dry-run` works for single entity
- [ ] `SETUP_LOG_LEVEL=Debug dotnet run --project src/Abuvi.Setup -- setup` shows debug-level output
- [ ] Production mode dry-run works without `--confirm`: `--env=production --dry-run`
- [ ] Dry-run summary shows correct would-import / would-skip / would-fail counts

## Dependencies

### NuGet Packages (new)

| Package | Version | Purpose |
|---------|---------|---------|
| `Serilog` | 4.* | Core logging framework |
| `Serilog.Sinks.Console` | 6.* | Colored console output |
| `Serilog.Sinks.File` | 6.* | Rolling file log output |

### No EF Core migrations needed

No schema changes — this is purely application-level.

## Notes

- **Interactive prompts stay as Console I/O**: `SafetyGuard.EnsureResetAllowed()` uses `Console.Write` + `Console.ReadLine()` for the "Type YES" confirmation. This **must not** go through Serilog — it's user interaction, not logging. However the prompt text should also be logged to file (use `Log.Warning` before the `Console.Write`).
- **No `Serilog.AspNetCore`**: This is a console app. Use the static `Log.Logger` directly — no need for DI, `ILogger<T>`, or `ReadFrom.Configuration()`.
- **Transaction approach for dry-run**: Importers call `SaveChangesAsync()` per row. In dry-run, a wrapping transaction ensures all those saves are visible within the transaction (so FK lookups for subsequent importers work correctly) but rolled back at the end. This means zero code changes needed inside `Importers/*.cs`.
- **Exit code in dry-run**: Always return 0. The purpose is reporting, not execution.
- **All technical artifacts in English** as per base-standards.
