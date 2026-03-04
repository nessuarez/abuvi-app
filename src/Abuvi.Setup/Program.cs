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
var runner = new SeedRunner(db, guard);

// Parse command (first positional arg that is not a flag)
var command = args.FirstOrDefault(a => !a.StartsWith("--")) ?? "run-all";

switch (command)
{
    case "reset":
        if (!guard.EnsureResetAllowed()) return 1;
        await runner.ResetAsync();
        break;

    case "run-all":
        if (config.IsProduction && !guard.EnsureResetAllowed()) return 1;
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
