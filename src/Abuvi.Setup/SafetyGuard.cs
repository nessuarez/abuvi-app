namespace Abuvi.Setup;

using Abuvi.API.Data;
using Microsoft.EntityFrameworkCore;
using Serilog;

public class SafetyGuard(AbuviDbContext db, SetupConfig config)
{
    /// <summary>
    /// In production, reset requires --confirm flag.
    /// Returns true if reset is allowed, false otherwise.
    /// </summary>
    public bool EnsureResetAllowed()
    {
        if (config.DryRun) return true;
        if (!config.IsProduction) return true;

        if (!config.Confirm)
        {
            Log.Error("PRODUCTION RESET BLOCKED: this will DELETE ALL DATA");
            Log.Error("Add --confirm flag to proceed: dotnet run reset --env=production --confirm");
            return false;
        }

        Log.Warning("Production reset confirmation required");
        Console.ForegroundColor = ConsoleColor.Red;
        Console.Write("You are about to RESET a PRODUCTION database. Type 'YES' to confirm: ");
        Console.ResetColor();
        var input = Console.ReadLine()?.Trim();
        if (input != "YES")
        {
            Log.Warning("Reset aborted by user");
            return false;
        }

        return true;
    }

    /// <summary>
    /// In production, import is only allowed on empty tables (initial setup).
    /// Returns true if import can proceed.
    /// </summary>
    public async Task<bool> EnsureImportAllowedAsync(string entity, CancellationToken ct = default)
    {
        if (config.DryRun) return true;
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
            Log.Error("{Entity}: BLOCKED — table already has data (production mode)", entity);
            Log.Error("Use 'reset' first or switch to --env=dev for incremental imports");
            return false;
        }

        return true;
    }
}
