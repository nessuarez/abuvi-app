using Abuvi.API.Data;
using Abuvi.API.Features.GooglePlaces;
using Abuvi.Setup;
using Abuvi.Setup.Geocoding;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
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

    // 'geocode' works on CSV files only — no database involved, so it runs
    // before the connection is established.
    if (args.FirstOrDefault(a => !a.StartsWith("--")) == "geocode")
        return await RunGeocodeAsync(args);

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
                "  import <entity>      Import a single entity CSV\n" +
                "  geocode --file=<csv>  Fill in coordinates from Google Places and write a review map\n\n" +
                "Options:\n" +
                "  --env=dev|production Environment mode (default: dev)\n" +
                "  --dir=<path>         CSV files directory (default: ./seed/)\n" +
                "  --connection=<str>   PostgreSQL connection string\n" +
                "  --confirm            Required for production destructive ops\n" +
                "  --dry-run            Run without saving changes (preview mode)\n" +
                "  --file=<path>        CSV to geocode (geocode command)\n" +
                "  --google-key=<key>   Google Places API key (default: user secrets GooglePlaces:ApiKey,\n" +
                "                       or env GOOGLE_PLACES_API_KEY)",
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

static async Task<int> RunGeocodeAsync(string[] args)
{
    var csvPath = ArgValue(args, "--file");
    if (csvPath is null)
    {
        Log.Error("Usage: geocode --file=<path-to-csv> [--google-key=<key>]");
        return 1;
    }

    if (!File.Exists(csvPath))
    {
        Log.Error("File not found: {Path}", csvPath);
        return 1;
    }

    // Key sources, lowest priority first. User secrets are shared with Abuvi.API,
    // so the key lives in exactly one place for both the API and this command.
    var configuration = new ConfigurationBuilder()
        .AddUserSecrets(typeof(GooglePlacesService).Assembly, optional: true)
        .AddEnvironmentVariables()
        .Build();

    var apiKey = ArgValue(args, "--google-key")
        ?? Environment.GetEnvironmentVariable("GOOGLE_PLACES_API_KEY")
        ?? configuration["GooglePlaces:ApiKey"];

    if (string.IsNullOrWhiteSpace(apiKey))
    {
        Log.Error(
            "Google Places API key required. Any of these works:\n" +
            "  dotnet user-secrets --project src/Abuvi.API set \"GooglePlaces:ApiKey\" \"<key>\"   (recommended)\n" +
            "  set GOOGLE_PLACES_API_KEY=<key>\n" +
            "  geocode --file=<csv> --google-key=<key>");
        return 1;
    }

    // Re-build with the resolved key winning over every other source.
    configuration = new ConfigurationBuilder()
        .AddConfiguration(configuration)
        .AddInMemoryCollection(new Dictionary<string, string?> { ["GooglePlaces:ApiKey"] = apiKey })
        .Build();

    using var httpClient = new HttpClient();
    var places = new GooglePlacesService(
        httpClient, configuration, NullLogger<GooglePlacesService>.Instance);

    var report = await new GeocodeRunner(places).RunAsync(csvPath);

    var mapPath = Path.Combine(
        Path.GetDirectoryName(csvPath) ?? ".",
        Path.GetFileNameWithoutExtension(csvPath) + "-geocode-review.html");
    ReviewMapWriter.Write(mapPath, report);

    Log.Information(
        "Geocoding: {Total} filas — {Ok} correctas, {Review} a revisar, {Failed} sin resolver, {Skipped} ya fijadas",
        report.Total, report.Ok, report.Review, report.Failed, report.Skipped);
    Log.Information("Mapa de revisión: {Path}", mapPath);

    if (!report.ReadyToImport)
    {
        Log.Warning("Hay filas pendientes de revisar. Corrige el CSV (geocodeStatus = ok_manual) antes de importar.");
        return 2;
    }

    Log.Information("Todas las ubicaciones verificadas. Listo para importar.");
    return 0;
}

static string? ArgValue(string[] args, string name) =>
    args.FirstOrDefault(a => a.StartsWith(name + "=", StringComparison.Ordinal))
        ?.Split('=', 2)[1];
