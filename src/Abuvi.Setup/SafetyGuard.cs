namespace Abuvi.Setup;

using Abuvi.API.Data;
using Microsoft.EntityFrameworkCore;

public class SafetyGuard(AbuviDbContext db, SetupConfig config)
{
    /// <summary>
    /// In production, reset requires --confirm flag.
    /// Returns true if reset is allowed, false otherwise.
    /// </summary>
    public bool EnsureResetAllowed()
    {
        if (!config.IsProduction) return true;

        if (!config.Confirm)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.Error.WriteLine(
                "PRODUCTION RESET BLOCKED: this will DELETE ALL DATA.");
            Console.Error.WriteLine(
                "Add --confirm flag to proceed: dotnet run reset --env=production --confirm");
            Console.ResetColor();
            return false;
        }

        Console.ForegroundColor = ConsoleColor.Red;
        Console.Write("You are about to RESET a PRODUCTION database. Type 'YES' to confirm: ");
        Console.ResetColor();
        var input = Console.ReadLine()?.Trim();
        if (input != "YES")
        {
            Console.Error.WriteLine("Aborted.");
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
