namespace Abuvi.Setup;

using Abuvi.API.Data;
using Abuvi.Setup.Importers;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Serilog;

public class SeedRunner(AbuviDbContext db, SafetyGuard guard, bool dryRun = false)
{
    private static readonly Guid AdminId = new("00000000-0000-0000-0000-000000000001");

    public async Task ResetAsync()
    {
        Log.Information("Resetting database...");

        // Delete in FK-safe order (children before parents)
        await db.Payments.ExecuteDeleteAsync();
        await db.RegistrationExtras.ExecuteDeleteAsync();
        await db.RegistrationAccommodationPreferences.ExecuteDeleteAsync();
        await db.RegistrationMembers.ExecuteDeleteAsync();
        await db.Registrations.ExecuteDeleteAsync();
        await db.CampEditionExtras.ExecuteDeleteAsync();
        await db.CampEditionAccommodations.ExecuteDeleteAsync();
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

        Log.Information("Database reset completed");
    }

    public async Task ImportAllAsync(string seedDir)
    {
        Log.Information("Importing from: {SeedDir}", seedDir);

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

        IDbContextTransaction? transaction = null;
        if (dryRun)
            transaction = await db.Database.BeginTransactionAsync();

        try
        {
            var results = new List<SeedResult>();

            foreach (var (file, entity, guardKey, import) in importers)
            {
                var path = Path.Combine(seedDir, file);
                if (!File.Exists(path))
                {
                    Log.Warning("{Entity}: skipped (file not found: {File})", entity, file);
                    continue;
                }

                if (!await guard.EnsureImportAllowedAsync(guardKey))
                    continue;

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
            Log.Error("File not found: {Path}", path);
            return;
        }

        if (!await guard.EnsureImportAllowedAsync(guardKey))
            return;

        IDbContextTransaction? transaction = null;
        if (dryRun)
            transaction = await db.Database.BeginTransactionAsync();

        try
        {
            var result = entity.ToLowerInvariant() switch
            {
                "users"          => await new UserImporter(db).ImportAsync(path),
                "family-units"   => await new FamilyUnitImporter(db).ImportAsync(path),
                "family-members" => await new FamilyMemberImporter(db).ImportAsync(path),
                "camps"          => await new CampImporter(db).ImportAsync(path),
                "camp-editions"  => await new CampEditionImporter(db).ImportAsync(path),
                _ => throw new ArgumentException($"Unknown entity: {entity}")
            };

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
    }

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
}
