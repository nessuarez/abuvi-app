using Abuvi.API.Data;
using Abuvi.Setup;
using Microsoft.EntityFrameworkCore;
using Serilog;
using Serilog.Events;
using Serilog.Sinks.SystemConsole.Themes;

var config = SetupConfig.Parse(args);

// Initialize Serilog
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

try
{
    Log.Information("=== Abuvi Setup Tool [{Environment}] ===", config.Env);

    if (config.DryRun)
        Log.Information("=== DRY-RUN MODE — No changes will be saved ===");

    var options = new DbContextOptionsBuilder<AbuviDbContext>()
        .UseNpgsql(config.ConnectionString)
        .Options;

    await using var db = new AbuviDbContext(options);

    // Verify DB connection
    try
    {
        await db.Database.CanConnectAsync();
        Log.Information("Connected to database");
    }
    catch (Exception ex)
    {
        Log.Error("Cannot connect to database: {Error}", ex.Message);
        return 1;
    }

    var guard = new SafetyGuard(db, config);
    var runner = new SeedRunner(db, guard, config.DryRun);

    // Parse command (first positional arg that is not a flag)
    var command = args.FirstOrDefault(a => !a.StartsWith("--")) ?? "run-all";

    switch (command)
    {
        case "reset":
            if (config.DryRun)
            {
                Log.Warning("Dry-run mode: Reset has no effect");
                break;
            }
            if (!guard.EnsureResetAllowed()) return 1;
            await runner.ResetAsync();
            break;

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

        case "setup":
            await runner.ImportAllAsync(config.SeedDir);
            break;

        case "import":
            var entity = args.Skip(1).FirstOrDefault(a => !a.StartsWith("--"));
            if (entity is null)
            {
                Log.Error("Usage: import <entity>");
                return 1;
            }
            await runner.ImportSingleAsync(config.SeedDir, entity);
            break;

        default:
            Log.Error(
                "Unknown command: {Command}. " +
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
                "  --confirm            Required for production destructive ops\n" +
                "  --dry-run            Run without saving changes (preview mode)",
                command);
            return 1;
    }

    return 0;
}
catch (Exception ex)
{
    Log.Fatal(ex, "Unhandled exception");
    return 1;
}
finally
{
    await Log.CloseAndFlushAsync();
}
