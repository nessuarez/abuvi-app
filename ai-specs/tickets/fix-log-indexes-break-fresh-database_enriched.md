# Bug Fix: The Migration Chain Cannot Run on an Empty Database

## Problem

On a **genuinely empty** PostgreSQL database, applying the migrations fails partway through:

```
ERROR:  relation "logs" does not exist
ERROR:  current transaction is aborted, commands ignored until end of transaction block
```

Everything after that point is skipped, so the database is left half-built: the
`__EFMigrationsHistory` table exists, but almost no application tables do.

Reproduce:

```bash
docker exec abuvi-postgres psql -U abuvi_user -d postgres -c "CREATE DATABASE scratch OWNER abuvi_user;"
dotnet ef database update --project src/Abuvi.API --startup-project src/Abuvi.API \
  --connection "Host=localhost;Port=5432;Database=scratch;Username=abuvi_user;Password=dev_password"
```

## Root cause

Two independent mechanisms disagree about who owns the `logs` table.

**`20260216005928_AddLogIndexes` assumes it already exists.** The migration is five
`CREATE INDEX IF NOT EXISTS ... ON logs(...)` statements. `IF NOT EXISTS` guards the *index*, not
the *table*, so a missing table is still a hard error.

**No migration ever creates `logs`.** It is created by Serilog's PostgreSQL sink, configured with
`needAutoCreateTable: true` in `Program.cs`.

**And the sink creates it too late.** The sink is wrapped in `WriteTo.Async(...)` with
`batchSizeLimit: 100` and `period: TimeSpan.FromSeconds(5)`, so the table is only created when the
first batch is flushed — seconds into the process lifetime. `MigrateAsync` runs at startup
(`Program.cs:361`), well before that. On a fresh database the migration therefore always loses the
race.

So the ordering is: migrations run → `AddLogIndexes` needs `logs` → the sink has not created it yet
→ failure.

## Why it has gone unnoticed

The deployed environments were created before `AddLogIndexes` existed (February 2026), so by the
time it was added the `logs` table had been there for months and the migration found what it
expected. **Nothing is broken in any running environment.** The defect only bites when a database is
built from scratch:

- a new environment (staging, a demo instance, a second production);
- a developer cloning the repository for the first time;
- restoring from a schema-only baseline;
- any CI job that would spin up a database — of which there are none today, which is part of why
  this went unseen.

It was found while building `20260826082352_SeedHistoricalCamps`, whose test needed a
from-scratch database. The workaround used there was to create `logs` by hand before applying the
chain; that is a workaround, not a fix.

## Suggested fix

**Own the table in a migration.** The application depends on `logs` existing; a runtime side effect
of a logging sink is not a dependable owner. Add a migration that creates it, ordered before
`AddLogIndexes`.

The shape is already known — this is the live definition, taken from a working database:

```sql
CREATE TABLE IF NOT EXISTS logs (
    message          text,
    message_template text,
    level            text,
    "timestamp"      timestamp with time zone,
    exception        text,
    log_event        jsonb,
    properties       jsonb,
    user_id          character varying(50),
    client_ip        character varying(50),
    correlation_id   character varying(50)
);
```

It must match the `columnOptions` in `Program.cs` exactly, or the sink will fail to insert.

**Two ways to order it, and the choice matters:**

| Option | Trade-off |
| --- | --- |
| A new migration dated **before** `AddLogIndexes` | Correct ordering for a fresh database, but inserting a migration into the middle of an applied history is awkward: environments that already ran everything will never apply it. Needs `CREATE TABLE IF NOT EXISTS` so it is a no-op where the table exists, and the history row has to be back-filled or accepted as permanently pending |
| Fold the `CREATE TABLE IF NOT EXISTS` into **`AddLogIndexes` itself**, above the index statements | Simplest and self-contained. Editing an already-applied migration is normally a bad idea, but here the change is a no-op wherever it has already run, and `IF NOT EXISTS` makes it safe |

**Recommendation: the second.** It is one edit, it fixes the chain for every fresh database, and it
changes nothing for environments that already applied it. Note the objection honestly — editing
applied migrations is a habit worth avoiding — but a strictly additive, idempotent `CREATE TABLE IF
NOT EXISTS` at the top of the same migration is the narrow case where it is defensible.

Leaving `needAutoCreateTable: true` in the sink afterwards is fine: it becomes a harmless no-op.

## Verification

- [ ] Create an empty database and run `dotnet ef database update` against it: the whole chain
      applies with no errors, without creating anything by hand first.
- [ ] Count the tables afterwards and confirm the schema is complete, not just
      `__EFMigrationsHistory`.
- [ ] Confirm `logs` has the ten columns above and that the five `idx_logs_*` indexes exist.
- [ ] Start the API against that database and confirm Serilog writes rows into `logs`.
- [ ] Apply the chain to an **existing** environment database and confirm nothing changes and
      nothing fails.

## Affected Files

### Files to Modify

| File | Change |
| --- | --- |
| `src/Abuvi.API/Migrations/20260216005928_AddLogIndexes.cs` | Add `CREATE TABLE IF NOT EXISTS logs (...)` above the index statements (recommended option) |

### Reference Files

| File | Notes |
| --- | --- |
| `src/Abuvi.API/Program.cs:56-75` | The Serilog sink config; `columnOptions` is the authority on the column list |
| `src/Abuvi.API/Program.cs:361` | `MigrateAsync` at startup — the ordering that makes this fail |
| `src/Abuvi.API/Migrations/20260826082352_SeedHistoricalCamps.cs` | The migration whose testing surfaced this |

## Notes

- **No application code is at fault and no running environment is affected.** This is about being
  able to build one from nothing.
- Related, and worth its own ticket: **there is no CI running the test suites or a database
  migration**. A broken migration chain went unnoticed for six months precisely because nothing ever
  builds the schema from zero. See also
  [`fix-cypress-binary-fails-to-start_enriched.md`](./fix-cypress-binary-fails-to-start_enriched.md),
  which is the same shape of problem — a verification step that nobody runs.
- Priority: not urgent, but it is the sort of thing that surfaces at the worst moment, when someone
  needs a new environment in a hurry.
