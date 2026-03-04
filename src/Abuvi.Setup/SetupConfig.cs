namespace Abuvi.Setup;

public enum SetupEnv { Dev, Production }

public class SetupConfig
{
    public SetupEnv Env { get; init; } = SetupEnv.Dev;
    public string ConnectionString { get; init; } = null!;
    public string SeedDir { get; init; } = null!;
    public bool Confirm { get; init; }
    public bool DryRun { get; init; }

    public bool IsProduction => Env == SetupEnv.Production;

    public static SetupConfig Parse(string[] args)
    {
        var env = args.FirstOrDefault(a => a.StartsWith("--env="))
            ?.Replace("--env=", "");

        var connection = args.FirstOrDefault(a => a.StartsWith("--connection="))
            ?.Replace("--connection=", "")
            ?? Environment.GetEnvironmentVariable("DATABASE_URL")
            ?? "Host=localhost;Database=abuvi;Username=postgres;Password=postgres";

        var dir = args.FirstOrDefault(a => a.StartsWith("--dir="))
            ?.Replace("--dir=", "")
            ?? Path.Combine(AppContext.BaseDirectory, "seed");

        var confirm = args.Contains("--confirm");
        var dryRun = args.Contains("--dry-run");

        return new SetupConfig
        {
            Env = env?.ToLowerInvariant() == "production"
                ? SetupEnv.Production
                : SetupEnv.Dev,
            ConnectionString = connection,
            SeedDir = dir,
            Confirm = confirm,
            DryRun = dryRun
        };
    }
}
