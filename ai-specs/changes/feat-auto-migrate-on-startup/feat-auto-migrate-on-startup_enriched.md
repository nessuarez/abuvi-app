# Enriched User Story: Auto-Apply EF Core Migrations on Startup

**Original request:** "Can you change backend to automatically apply migrations on starting phase? Also register an event in Seq about pending migrations that are applied on start."

**Date:** 2026-02-18

---

## Summary

On every application start, automatically apply any pending EF Core migrations against the PostgreSQL database before the API begins serving requests. Additionally, emit a structured Serilog event (visible in Seq) that records which migrations were applied, how many there were, and how long the operation took.

If no migrations are pending, log a single informational event and continue normally. If migration fails, rethrow the exception so the application fails fast rather than starting with an inconsistent schema.

---

## Motivation

Currently developers must run `dotnet ef database update` manually after deploying or pulling new changes. This creates friction in local development workflows and risks deploying to an environment with an outdated schema. Automating migration application on startup is a standard pattern for web APIs in self-contained deployments and development environments.

---

## Scope

**Backend only.** Single file change: `src/Abuvi.Api/Program.cs`.

No new endpoints, no new services, no frontend changes.

---

## Acceptance Criteria

1. When the application starts and there are **pending migrations**, they are applied automatically before any HTTP request is processed.
2. When the application starts and the database is **already up to date**, no migration is attempted and a single log message confirms it.
3. A Serilog structured event is emitted (visible in Seq) for **each startup** including:
   - Whether migrations were applied or not
   - The list of migration names applied (if any)
   - The total count of applied migrations
   - The elapsed time in milliseconds for the migration operation
4. If migration **fails**, the exception propagates and the application exits with a non-zero code (fail fast — do not start serving requests with a potentially broken schema).
5. All log events use structured properties (not string interpolation) so Seq can filter and search by property values.

---

## Implementation

### File to modify

**`src/Abuvi.Api/Program.cs`**

### Where to insert

After `var app = builder.Build();` and before the middleware pipeline setup (`app.UseMiddleware<...>()`). This ensures the DI container is ready and services are resolvable, but the HTTP pipeline hasn't started yet.

### Implementation pattern

```csharp
// ========================================
// Auto-apply Pending Migrations
// ========================================
using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<AbuviDbContext>();
    var startupLogger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

    var pendingMigrations = (await dbContext.Database.GetPendingMigrationsAsync()).ToList();

    if (pendingMigrations.Count == 0)
    {
        startupLogger.LogInformation("Database schema is up to date. No pending migrations.");
    }
    else
    {
        startupLogger.LogInformation(
            "Applying {PendingMigrationCount} pending database migration(s): {PendingMigrations}",
            pendingMigrations.Count,
            pendingMigrations);

        var sw = System.Diagnostics.Stopwatch.StartNew();
        await dbContext.Database.MigrateAsync();
        sw.Stop();

        startupLogger.LogInformation(
            "Successfully applied {AppliedMigrationCount} database migration(s) in {ElapsedMs}ms: {AppliedMigrations}",
            pendingMigrations.Count,
            sw.ElapsedMilliseconds,
            pendingMigrations);
    }
}
```

### Structured log properties

| Property | Type | Description |
|---|---|---|
| `PendingMigrationCount` | `int` | Number of migrations before applying |
| `PendingMigrations` | `string[]` | Names of pending migrations |
| `AppliedMigrationCount` | `int` | Number of migrations actually applied |
| `AppliedMigrations` | `string[]` | Names of applied migrations |
| `ElapsedMs` | `long` | Duration of `MigrateAsync()` in milliseconds |

These properties flow through Serilog → Seq automatically since `Program.cs` already configures `.WriteTo.Seq(...)` and `.Enrich.FromLogContext()`.

### Error handling

Do **not** catch exceptions from `MigrateAsync()`. Let them propagate naturally. ASP.NET Core startup will catch them, log the exception, and exit with a non-zero process code. This is the correct fail-fast behaviour — the API must not start in an undefined state.

---

## Testing

### Unit / integration testing

Migration application is infrastructure startup code that runs in `Program.cs`. It cannot be meaningfully unit tested in isolation.

**Verification approach:**

1. Run the application with a fresh or outdated database and confirm in Seq that the migration event appears with correct properties.
2. Run the application against an up-to-date database and confirm the "no pending migrations" event appears.
3. Confirm `dotnet ef database update` is no longer required as part of local setup.

> Note: If integration tests using `WebApplicationFactory<Program>` are added in the future, the migration block will run against the test database during test startup. This is acceptable behavior — tests should run against a migrated schema.

---

## Constraints and notes

- **Idempotency**: `MigrateAsync()` is idempotent — calling it when no migrations are pending is a no-op. The explicit `GetPendingMigrationsAsync()` check exists only to produce informative log output, not as a safety guard.
- **Concurrency (multi-instance deployments)**: EF Core's migration history table (`__EFMigrationsHistory`) uses a database-level lock during migration. If two instances start simultaneously, one will wait and then detect no pending work. This is safe but worth monitoring in production.
- **Production readiness**: Auto-migration on startup is appropriate for this project's deployment model. For environments where manual migration control is preferred, the pattern can be gated behind a configuration flag (e.g., `"Database:AutoMigrate": true`). This is optional and not required for this ticket.
- **Logging framework**: The app uses Serilog configured in `Program.cs` with three sinks (Console, PostgreSQL, Seq). The startup logger obtained via `ILogger<Program>` from DI will write to all three sinks.

---

## Definition of Done

- [ ] `MigrateAsync()` is called on startup before the HTTP pipeline is active
- [ ] Structured log event is emitted when migrations are applied (properties: count, names, elapsed ms)
- [ ] Structured log event is emitted when no migrations are pending
- [ ] Event is visible in Seq with correct structured properties
- [ ] Application fails fast (exits) if `MigrateAsync()` throws
- [ ] `dotnet ef database update` is no longer required for local development after pulling changes
- [ ] TypeScript check passes (no frontend changes, N/A)
- [ ] Backend builds with `dotnet build` — no errors, no warnings
